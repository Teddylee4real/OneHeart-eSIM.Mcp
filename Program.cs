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

builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = false)
    .WithTools<CatalogTools>();

var app = builder.Build();

app.MapMcp();

app.Run();
