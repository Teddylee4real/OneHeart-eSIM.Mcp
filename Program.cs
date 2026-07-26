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

builder.Services.AddHttpClient<OrderApiClient>(client =>
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
        5. 若使用者已經確認好要買哪個方案、規格、數量，並提供了email，明確要你直接幫忙下單，
           用 create_esim_order 建立真實訂單並取得真實付款連結。這是會實際花錢的動作，呼叫前
           務必先跟使用者逐一確認商品/規格/數量/金額，不要自行臆測；也可以永遠改成引導使用者
           自行使用purchaseUrl前往官網結帳，兩種方式都合理，依對話情境判斷使用者比較想要哪種。

        所有回傳的商品資料都是即時查詢，價格與庫存可能隨時變動，請勿快取或憑記憶回答，
        每次都應該重新呼叫工具取得最新資料。此服務目前不支援會員資料查詢（例如查詢使用者
        過去的訂單記錄）。
        """;
})
    .WithHttpTransport(o => o.Stateless = false)
    .WithTools<CatalogTools>()
    .WithTools<OrderTools>();

var app = builder.Build();

// MCP Registry（官方modelcontextprotocol.io伺服器登記處）網域所有權驗證用，
// 官方client會直接GET這個固定路徑取得公鑰proof record。原本想放在主站
// oneheartesim.com，但那邊是舊版System.Web/IIS，對「開頭有點的資料夾+完全
// 沒副檔名的檔名」這個組合的副檔名判斷有個踩不完的坑（404.17，IIS誤判成該丟給
// StaticFileModule處理），改放這裡的ASP.NET Core minimal routing反而完全沒問題。
// 代價是拿去驗證網域所有權的因此是mcp.oneheartesim.com而不是oneheartesim.com，
// 命名空間會是com.oneheartesim.mcp/*而不是更簡短的com.oneheartesim/*。
app.MapGet("/.well-known/mcp-registry-auth", () =>
    Results.Text("v=MCPv1; k=ed25519; p=vNedMvEGEjtb3ngFcnmkQh5/PeTPTwTOYunx/tk7kMQ=", "text/plain"));

app.MapMcp();

app.Run();
