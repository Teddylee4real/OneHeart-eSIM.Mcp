using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OneHeart_eSIM.Mcp.Models;

namespace OneHeart_eSIM.Mcp.Services;

public class CatalogApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string PlansCacheKey = "catalog:plans";

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly string _token;

    public CatalogApiClient(HttpClient http, IMemoryCache cache, IConfiguration config)
    {
        _http = http;
        _cache = cache;
        _token = config["OneHeartSite:InternalToken"]
            ?? throw new InvalidOperationException("缺少設定 OneHeartSite:InternalToken");
    }

    // 目錄變動不頻繁，短期快取60秒：減少對主站的請求量，也讓同一輪對話內多次呼叫工具時結果一致。
    public async Task<List<PlanItem>> GetPlansAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(PlansCacheKey, out List<PlanItem>? cached) && cached != null)
        {
            return cached;
        }

        var resp = await _http.GetFromJsonAsync<PlansResponse>($"/CatalogApi/Plans?token={_token}", JsonOptions, ct)
            ?? throw new InvalidOperationException("主站商品目錄API沒有回應");

        _cache.Set(PlansCacheKey, resp.Items, TimeSpan.FromSeconds(60));
        return resp.Items;
    }

    public async Task<PlanDetailResponse> GetPlanDetailAsync(Guid productId, CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<PlanDetailResponse>($"/CatalogApi/Plan?productId={productId}&token={_token}", JsonOptions, ct)
            ?? throw new InvalidOperationException("主站商品目錄API沒有回應");
    }
}
