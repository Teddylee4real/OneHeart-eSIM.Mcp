# ONE HEART eSIM MCP Server

[![Model Context Protocol](https://img.shields.io/badge/MCP-remote%20server-e80303)](https://mcp.oneheartesim.com/)

官方遠端 MCP (Model Context Protocol) 伺服器，讓支援 MCP 的 AI 助理（例如 Claude）能直接查詢 [ONE HEART eSIM（一心漫遊）](https://www.oneheartesim.com/) 的即時 eSIM 上網方案、依旅遊天數推薦合適方案，並協助建立訂單、取得付款連結。

- 人類可讀的產品介紹頁：<https://www.oneheartesim.com/mcp>
- 完整技術文件：<https://www.oneheartesim.com/mcp/docs>
- 官方 MCP Registry：`com.oneheartesim.mcp/esim`

## 連線資訊

| 項目 | 內容 |
|---|---|
| Endpoint | `https://mcp.oneheartesim.com/` |
| 傳輸方式 | Streamable HTTP |
| 身分驗證 | 公開，無需 API Key |

把上面的網址加進任何支援遠端 MCP 的 AI 助理／用戶端即可使用，設定方式依各用戶端而異。

## 提供的工具

| 工具 | 說明 |
|---|---|
| `list_esim_countries` | 列出目前販售方案覆蓋的所有國家/地區 |
| `search_esim_plans` | 依國家、方案類型、關鍵字搜尋方案清單 |
| `get_esim_plan_detail` | 取得單一方案完整詳情（安裝說明、庫存、購買連結） |
| `recommend_esim_plan` | 依旅遊天數與用量習慣，評分推薦最合適的 1-3 個方案 |
| `create_esim_order` | 建立真實訂單並取得真實付款連結（LinePay 或 ECPay 綠界） |

前 4 個為唯讀查詢；`create_esim_order` 會實際建立訂單並串接真實金流，詳細參數與已知限制（例如目前非冪等）請見[技術文件](https://www.oneheartesim.com/mcp/docs)。

## 架構

這是一個 ASP.NET Core (.NET 8) 專案，作為薄客戶端呼叫主站（ASP.NET MVC5 / .NET Framework 4.8）既有的內部 API 取得即時商品與訂單資料，維持單一資料真相來源。專案本身不包含任何機密資訊；實際部署所需的內部驗證 token 透過環境變數/User Secrets 於外部提供，不會出現在原始碼或版控紀錄中。

## 授權

MIT License，詳見 [LICENSE](LICENSE)。
