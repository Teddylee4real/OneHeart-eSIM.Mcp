using System.Net.Http.Json;
using System.Text.Json;
using OneHeart_eSIM.Mcp.Models;

namespace OneHeart_eSIM.Mcp.Services;

public class OrderApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _token;

    public OrderApiClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _token = config["OneHeartSite:OrderInternalToken"]
            ?? throw new InvalidOperationException("缺少設定 OneHeartSite:OrderInternalToken");
    }

    public async Task<CreateOrderResponse> CreateOrderAsync(
        Guid productId, string spec, int quantity, string email, string? phone, string userName, string paymentMethod, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["token"] = _token,
            ["productId"] = productId.ToString(),
            ["spec"] = spec,
            ["quantity"] = quantity.ToString(),
            ["email"] = email,
            ["phone"] = phone ?? "",
            ["userName"] = userName,
            ["paymentMethod"] = paymentMethod
        };

        var resp = await _http.PostAsync("/OrderApi/CreateOrder", new FormUrlEncodedContent(form), ct);
        var result = await resp.Content.ReadFromJsonAsync<CreateOrderResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("主站下單API沒有回應");
    }
}
