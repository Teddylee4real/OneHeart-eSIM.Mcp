using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using OneHeart_eSIM.Mcp.Models;
using OneHeart_eSIM.Mcp.Services;

namespace OneHeart_eSIM.Mcp.Tools;

// 列舉成員名稱直接用中文，是因為要跟資料庫實際儲存、search_esim_plans內部拿去比對
// Category欄位的字串值（"總量型"/"每日定量型"）完全一致，省去多一層中英對照表、
// 也讓LLM在tools/list看到的JSON Schema enum值就是它應該原樣傳回來的值。
public enum EsimPlanType
{
    [Description("總量型：總流量用完為止，適合天數較長、用量抓得準的旅程")]
    總量型,
    [Description("每日定量型：每天固定配額，用完當日降速、隔日恢復，適合天數抓不準或想確保不斷網的旅程")]
    每日定量型
}

public enum EsimUsageLevel
{
    [Description("輕度：偶爾收信、看地圖、傳訊息")]
    light,
    [Description("中度：一般瀏覽網頁、社群媒體、地圖導航（預設）")]
    medium,
    [Description("重度：追劇、直播、開熱點分享給其他裝置")]
    heavy
}

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

    [McpServerTool, Description("""
        列出一心eSIM目前販售方案覆蓋的所有國家/地區（英文與中文名稱對照，含各國方案數量）。
        適用情境：使用者問「你們支援哪些國家」「有賣OO地區的eSIM嗎」；或你準備呼叫
        search_esim_plans/recommend_esim_plan，但不確定某國家在系統裡該用什麼名稱表示時，
        先呼叫這個工具確認，比用猜的準確。
        """)]
    public async Task<string> ListEsimCountries()
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

    [McpServerTool, Description("""
        依國家、方案類型、關鍵字搜尋一心eSIM的商品清單，一次回傳多筆結果讓使用者自己比較。
        適用情境：使用者想瀏覽/比較某國家的多個方案，或已經明確講出要哪種類型
        （例如指定「總量型」或「每日定量型」）。
        如果使用者是描述旅遊情境（去哪個國家玩幾天、上網習慣如何）要你直接建議選哪個，
        改用recommend_esim_plan讓它幫忙評分挑選，不要自己從這個工具的結果裡用猜的。
        """)]
    public async Task<string> SearchEsimPlans(
        [Description("國家名稱，英文或中文皆可，模糊比對，例如 Japan、日本、韓國。不確定系統裡的正確名稱時，先呼叫list_esim_countries確認")] string? country = null,
        [Description("方案類型，留空代表不限類型")] EsimPlanType? planType = null,
        [Description("自由關鍵字，比對商品名稱/描述/規格文字")] string? keyword = null,
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

        if (planType.HasValue)
        {
            var planTypeText = planType.Value.ToString();
            query = query.Where(p => p.Category.Contains(planTypeText, StringComparison.OrdinalIgnoreCase));
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

    [McpServerTool, Description("""
        取得單一方案的完整詳情，包含商品說明（安裝/啟用步驟等）、各規格庫存、購買連結。
        適用情境：使用者對某個已知方案想進一步了解安裝方式、使用細節、或確認庫存時。
        需要先從search_esim_plans或recommend_esim_plan的結果取得productId，不要自己編造。
        """)]
    public async Task<string> GetEsimPlanDetail(
        [Description("方案的productId（GUID字串），從search_esim_plans或recommend_esim_plan的結果取得")] string productId)
    {
        if (!Guid.TryParse(productId, out var id))
        {
            return JsonSerializer.Serialize(new { ok = false, error = "productId格式不正確，需要是GUID格式" }, JsonOptions);
        }

        var detail = await _catalog.GetPlanDetailAsync(id);
        return JsonSerializer.Serialize(detail, JsonOptions);
    }

    [McpServerTool, Description("""
        依旅遊天數與上網用量習慣，推薦最合適的1-3個eSIM方案，並附上購買連結，
        是「幫使用者選方案」最好用的工具。
        適用情境：使用者詢問出國上網卡、各國eSIM方案、即時發卡服務，並描述了
        「要去OO玩幾天」「想找適合的上網方案/漫遊卡」「幫我推薦/挑選eSIM」時優先使用這個，
        而不是自己呼叫search_esim_plans後用猜的。
        """)]
    public async Task<string> RecommendEsimPlan(
        [Description("目的地國家，英文或中文皆可，例如 Japan、日本。不確定系統裡的正確名稱時，先呼叫list_esim_countries確認")] string country,
        [Description("預計使用天數")] int days,
        [Description("用量習慣，留空預設為medium（中度）")] EsimUsageLevel usageLevel = EsimUsageLevel.medium)
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
            return JsonSerializer.Serialize(new { ok = false, error = $"找不到「{country}」的方案，建議先呼叫list_esim_countries確認國家名稱" }, JsonOptions);
        }

        double gbPerDay = usageLevel switch
        {
            EsimUsageLevel.light => 0.5,
            EsimUsageLevel.heavy => 2.5,
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
