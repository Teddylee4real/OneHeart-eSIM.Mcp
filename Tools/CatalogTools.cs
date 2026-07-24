using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using OneHeart_eSIM.Mcp.Models;
using OneHeart_eSIM.Mcp.Services;

namespace OneHeart_eSIM.Mcp.Tools;

[McpServerToolType]
public sealed class CatalogTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // 解析 Spec 文字，對應主站兩種商品類型：
    // 總量型："10G/30天"（總流量/效期天數）；每日定量型："1G高速/天"（每日流量，效期由使用者輸入的天數決定）
    private static readonly Regex TotalSpecRegex = new(@"^(\d+(?:\.\d+)?)G/(\d+)天$", RegexOptions.Compiled);
    private static readonly Regex DailySpecRegex = new(@"^(\d+(?:\.\d+)?)G高速?/天", RegexOptions.Compiled);

    private readonly CatalogApiClient _catalog;

    public CatalogTools(CatalogApiClient catalog)
    {
        _catalog = catalog;
    }

    [McpServerTool, Description("列出一心eSIM目前有販售方案的所有國家/地區（英文與中文名稱對照）。查詢方案前可先呼叫這個工具確認國家名稱怎麼寫。")]
    public async Task<string> ListCountries()
    {
        var plans = await _catalog.GetPlansAsync();

        var countries = plans
            .GroupBy(p => p.Country, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                country = g.Key,
                // 同國家常有好幾個商品名稱版本（含行銷文案的長名稱、乾淨的短名稱），
                // 取最短的當代表，避免選到「中國 eSIM（免VPN免翻牆）不斷網 每日定量」這種長標題
                countryZh = StripSuffix(g.OrderBy(x => x.ProductName.Length).First().ProductName, "eSIM"),
                planCount = g.Count()
            })
            .OrderBy(c => c.country, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(new { count = countries.Count, countries }, JsonOptions);
    }

    [McpServerTool, Description("依國家/方案類型/關鍵字搜尋一心eSIM方案清單。country可用英文或中文（例如\"Japan\"或\"日本\"皆可，模糊比對）。planType可填\"總量型\"（總流量用完為止）或\"每日定量型\"（每天固定流量）。keyword可比對商品名稱、描述、規格文字。三個參數都可留空，留空時回傳全部方案（依價格排序並截斷筆數）。")]
    public async Task<string> SearchPlans(
        [Description("國家名稱，英文或中文皆可，模糊比對，例如 Japan、日本、韓國")] string? country = null,
        [Description("方案類型：總量型 或 每日定量型")] string? planType = null,
        [Description("自由關鍵字，比對商品名稱/描述/規格")] string? keyword = null,
        [Description("最多回傳幾筆，預設20，避免結果過多")] int limit = 20)
    {
        var plans = await _catalog.GetPlansAsync();
        IEnumerable<PlanItem> query = plans;

        if (!string.IsNullOrWhiteSpace(country))
        {
            query = query.Where(p =>
                p.Country.Contains(country, StringComparison.OrdinalIgnoreCase) ||
                p.ProductName.Contains(country, StringComparison.OrdinalIgnoreCase) ||
                p.ProductEngName.Contains(country, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(planType))
        {
            query = query.Where(p => p.Category.Contains(planType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p =>
                p.ProductName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (p.Desc ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                p.Spec.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        var matched = query.ToList();
        var results = matched
            .OrderBy(p => p.SalePrice ?? p.Price)
            .Take(Math.Clamp(limit, 1, 50))
            .ToList();

        return JsonSerializer.Serialize(new { count = results.Count, totalMatched = matched.Count, items = results }, JsonOptions);
    }

    [McpServerTool, Description("取得單一方案的完整詳情，包含商品說明、各規格庫存、購買連結。需要先從search_plans或recommend_plan取得productId。")]
    public async Task<string> GetPlanDetail(
        [Description("方案的productId（GUID字串），從search_plans或recommend_plan的結果取得")] string productId)
    {
        if (!Guid.TryParse(productId, out var id))
        {
            return JsonSerializer.Serialize(new { ok = false, error = "productId格式不正確，需要是GUID格式" }, JsonOptions);
        }

        var detail = await _catalog.GetPlanDetailAsync(id);
        return JsonSerializer.Serialize(detail, JsonOptions);
    }

    [McpServerTool, Description("依旅遊天數與上網用量習慣，推薦最合適的1-3個eSIM方案，並附上購買連結。這是幫使用者「選方案」最好用的工具，優先使用這個而不是自己從search_plans結果裡猜。")]
    public async Task<string> RecommendPlan(
        [Description("目的地國家，英文或中文皆可，例如 Japan、日本")] string country,
        [Description("預計使用天數")] int days,
        [Description("用量習慣：light(輕度，偶爾收信/地圖)、medium(中度，一般瀏覽/社群，預設)、heavy(重度，追劇/熱點分享)")] string usageLevel = "medium")
    {
        var plans = await _catalog.GetPlansAsync();

        var candidates = plans
            .Where(p =>
                p.Country.Contains(country, StringComparison.OrdinalIgnoreCase) ||
                p.ProductName.Contains(country, StringComparison.OrdinalIgnoreCase) ||
                p.ProductEngName.Contains(country, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            return JsonSerializer.Serialize(new { ok = false, error = $"找不到「{country}」的方案，建議先呼叫list_countries確認國家名稱" }, JsonOptions);
        }

        double gbPerDay = usageLevel.Trim().ToLowerInvariant() switch
        {
            "light" or "輕度" or "低" => 0.5,
            "heavy" or "重度" or "高" => 2.5,
            _ => 1.0
        };
        double estimatedNeedGb = gbPerDay * Math.Max(days, 1);

        var scored = new List<(PlanItem plan, double score, string reason)>();

        foreach (var p in candidates)
        {
            var parsed = ParseSpec(p.Spec);
            if (parsed == null) continue;

            var (specGb, specDays, isDaily) = parsed.Value;
            decimal effectivePrice = p.SalePrice ?? p.Price;

            if (isDaily)
            {
                if (specGb < gbPerDay * 0.7) continue;
                double fit = Math.Abs(specGb - gbPerDay) * 1000 + (double)effectivePrice;
                scored.Add((p, fit, $"每日定量型，每天{specGb:0.#}GB高速流量，符合{days}天的用量估算"));
            }
            else
            {
                if (specDays < days) continue;
                if (specGb < estimatedNeedGb * 0.8) continue;
                double fit = (double)effectivePrice;
                scored.Add((p, fit, $"總量型，效期{specDays}天可涵蓋{days}天行程，總流量{specGb:0.#}GB預估夠用"));
            }
        }

        var top = scored
            .OrderBy(x => x.score)
            .Take(3)
            .Select(x => new
            {
                x.plan.ProductId,
                x.plan.ProductName,
                x.plan.Category,
                x.plan.Spec,
                price = x.plan.Price,
                salePrice = x.plan.SalePrice,
                x.plan.PurchaseUrl,
                reason = x.reason
            })
            .ToList();

        if (top.Count == 0)
        {
            var fallback = candidates
                .OrderBy(p => p.SalePrice ?? p.Price)
                .Take(3)
                .Select(p => new { p.ProductId, p.ProductName, p.Category, p.Spec, price = p.Price, salePrice = p.SalePrice, p.PurchaseUrl })
                .ToList();

            return JsonSerializer.Serialize(new
            {
                ok = true,
                exactMatch = false,
                note = "沒有完全符合天數/用量估算的方案，以下是該國較便宜的方案供參考",
                recommendations = fallback
            }, JsonOptions);
        }

        return JsonSerializer.Serialize(new { ok = true, exactMatch = true, recommendations = top }, JsonOptions);
    }

    private static string StripSuffix(string value, string suffix)
    {
        value = (value ?? "").Trim();
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length].Trim()
            : value;
    }

    private static (double gb, int days, bool isDaily)? ParseSpec(string spec)
    {
        spec = (spec ?? "").Trim();

        var dailyMatch = DailySpecRegex.Match(spec);
        if (dailyMatch.Success)
        {
            return (double.Parse(dailyMatch.Groups[1].Value), 0, true);
        }

        var totalMatch = TotalSpecRegex.Match(spec);
        if (totalMatch.Success)
        {
            return (double.Parse(totalMatch.Groups[1].Value), int.Parse(totalMatch.Groups[2].Value), false);
        }

        return null;
    }
}
