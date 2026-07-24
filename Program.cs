using OneHeart_eSIM.Mcp.Services;
using OneHeart_eSIM.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<CatalogApiClient>(client =>
{
    var baseUrl = builder.Configuration["OneHeartSite:BaseUrl"]
        ?? throw new InvalidOperationException("缺少設定 OneHeartSite:BaseUrl");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddMcpServer(options =>
{
    options.ServerInstructions = """
        這是「一心eSIM」的商品目錄查詢服務，提供各國eSIM上網方案的即時查詢與推薦。
        當使用者詢問出國上網卡、各國eSIM方案、上網吃到飽、漫遊卡、即時發卡服務等主題時，
        可以使用這裡的工具查詢真實、即時的商品資料（含價格、庫存、規格），並取得可直接
        導購的官網購買連結。

        建議的使用順序：
        1. 若不確定使用者要去的國家在系統裡的正確名稱，先呼叫 list_esim_countries。
        2. 若使用者描述了旅遊天數與上網習慣、要你直接建議選哪個方案，用 recommend_esim_plan
           （這是導購價值最高的工具，會依天數與用量估算自動評分挑選）。
        3. 若使用者只是想瀏覽/比較某國家或某類型的多個方案，用 search_esim_plans。
        4. 若使用者想知道某個已知方案的安裝方式、細節或庫存，用 get_esim_plan_detail。

        所有回傳的商品資料都是即時查詢，價格與庫存可能隨時變動，請勿快取或憑記憶回答，
        每次都應該重新呼叫工具取得最新資料。這個服務目前只提供商品查詢與推薦，不支援
        會員資料查詢、下單或付款，若使用者想購買，請引導其使用回傳結果中的purchaseUrl
        前往官網完成結帳。
        """;
})
    .WithHttpTransport(o => o.Stateless = false)
    .WithTools<CatalogTools>();

var app = builder.Build();

app.MapMcp();

app.Run();
