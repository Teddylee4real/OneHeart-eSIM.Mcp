using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OneHeart_eSIM.Mcp.Services;

namespace OneHeart_eSIM.Mcp.Tools;

public enum EsimPaymentMethod
{
    [Description("LinePay：回傳的付款連結一開就是LinePay收銀台")]
    LinePay,
    [Description("ECPay綠界：回傳的付款連結會自動導向綠界收銀台，可選信用卡／ATM轉帳／超商代碼等付款方式")]
    ECPay
}

[McpServerToolType]
public sealed class OrderTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OrderApiClient _orderApi;

    public OrderTools(OrderApiClient orderApi)
    {
        _orderApi = orderApi;
    }

    [McpServerTool, Description("""
        建立一筆真實的一心eSIM訂單，並取得真實的付款連結（LinePay或ECPay綠界，二擇一）。
        這是會實際寫入正式訂單、串真實金流的動作，不是模擬或試算。

        呼叫前必須先完成：
        1. 已用search_esim_plans/recommend_esim_plan/get_esim_plan_detail取得正確的productId、
           spec，不要自己編造或憑記憶帶入。
        2. 已把商品名稱、規格、數量、單價與總金額，跟使用者逐一覆誦確認過，並取得使用者明確
           同意下單（不能因為使用者說「好」「幫我訂」等籠統回應就自行猜數量或規格）。
        3. 已取得使用者的email與稱呼/姓名（兩者都必填：email用於辨識訂單歸屬與寄送購買憑證/
           eSIM QR碼；姓名是訂單聯絡資料的必要欄位，不能留空或代填）。手機可選填。
        4. 已詢問使用者想用LinePay還是ECPay綠界付款（留空預設LinePay，但有得選的情況下應該
           主動問，不要自己幫使用者決定）。

        回傳的paymentUrl要完整、原樣提供給使用者（不要摘要或改寫），並提醒使用者付款完成前
        訂單不會出貨。若ok=false，訂單可能已建立但取得付款連結失敗，要把error/note內容如實
        告知使用者，不要自行重試建立第二筆訂單。
        """)]
    public async Task<string> CreateEsimOrder(
        [Description("方案的productId（GUID字串），必須從查詢工具的結果取得，不可編造")] string productId,
        [Description("方案規格，必須跟查詢工具回傳的spec完全一致")] string spec,
        [Description("購買數量，需與使用者確認過的數字一致")] int quantity,
        [Description("使用者的email，必填，訂單憑證與eSIM會寄到這裡")] string email,
        [Description("使用者的稱呼/姓名，必填，訂單聯絡資料需要，不可留空")] string userName,
        [Description("付款方式，留空預設LinePay")] EsimPaymentMethod paymentMethod = EsimPaymentMethod.LinePay,
        [Description("使用者的手機號碼，選填")] string? phone = null)
    {
        if (!Guid.TryParse(productId, out var id))
        {
            return JsonSerializer.Serialize(new { ok = false, error = "productId格式不正確，需要是GUID格式" }, JsonOptions);
        }
        if (string.IsNullOrWhiteSpace(userName))
        {
            return JsonSerializer.Serialize(new { ok = false, error = "userName不可留空，請先詢問使用者的稱呼/姓名" }, JsonOptions);
        }

        var result = await _orderApi.CreateOrderAsync(id, spec, quantity, email, phone, userName, paymentMethod.ToString());
        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
