FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "OneHeart-eSIM.Mcp.csproj"
RUN dotnet publish "OneHeart-eSIM.Mcp.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# 唯讀查詢工具（list_esim_countries/search_esim_plans/get_esim_plan_detail/recommend_esim_plan）
# 只需要這個公開網址即可啟動並回應 MCP introspection；InternalToken/OrderInternalToken
# 只有實際「呼叫」下單相關工具時才需要，不影響啟動或 tools/list。
ENV OneHeartSite__BaseUrl=https://www.oneheartesim.com
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OneHeart-eSIM.Mcp.dll"]
