namespace OneHeart_eSIM.Mcp.Models;

public class PlansResponse
{
    public bool Ok { get; set; }
    public int Count { get; set; }
    public List<PlanItem> Items { get; set; } = new();
}

public class PlanItem
{
    public Guid ProductId { get; set; }
    public string Country { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string ProductEngName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Spec { get; set; } = "";
    public int? PeriodDays { get; set; }
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public bool IsInOfferPeriod { get; set; }
    public int Quantity { get; set; }
    public string? Desc { get; set; }
    public string PurchaseUrl { get; set; } = "";
}

public class PlanDetailResponse
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public PlanItem? Item { get; set; }
    public string? Content { get; set; }
    public string? SpecContent { get; set; }
    public Dictionary<string, int>? SpecStock { get; set; }
}
