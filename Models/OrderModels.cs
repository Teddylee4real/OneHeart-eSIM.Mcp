namespace OneHeart_eSIM.Mcp.Models;

public class CreateOrderResponse
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? OrderId { get; set; }
    public string? OrderNo { get; set; }
    public int Amount { get; set; }
    public string? PaymentUrl { get; set; }
    public string? ReturnCode { get; set; }
    public string? ReturnMessage { get; set; }
    public string? Note { get; set; }
}
