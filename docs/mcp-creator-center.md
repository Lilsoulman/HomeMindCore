# 创作者中心本地 MCP Bridge

> 本文仅说明本地 Bridge 的边界；产品、开发计划和客户端 API 接入请分别阅读 [README](README.md)、[开发计划](development-plan.md) 和 [API 接入](api-integration.md)。

`HomeMind.CreatorMcp` 是一个通过 stdio 运行的 MCP Server。它从现有的 NexusMind API 同步创作者中心数据到本地 SQLite，并仅从本地数据库向 Agent 提供查询；同步不是后台隐式动作，Agent 必须显式调用 `sync_creator_center`。

## 数据范围与安全边界

- 同步范围：专家（`expert`）、专家组（`group`）和技能（`skill`）。
- 数据来源：`GET /api/v1/experts?type=expert|group` 与 `GET /api/v1/skills`，使用既有 Bearer Token 和权限策略。
- 默认不存储专家 `PromptTemplate` 或技能 `Prompt`，也不会从搜索工具返回任何提示词。
- 只有同时设置 `NEXUSMIND_MCP_ALLOW_SENSITIVE=true`，并在同步或读取工具中显式传入 `includeSensitiveData=true` 时，才允许保存或读取敏感字段。
- 本地 SQLite 是缓存和 Agent 上下文副本，不是创作者中心的写入入口；编辑仍通过 NexusMind 正常 API 完成。

## 环境变量

| 变量 | 说明 |
| --- | --- |
| `NEXUSMIND_API_BASE_URL` | NexusMind API 根地址；默认 `http://localhost:5280`。 |
| `NEXUSMIND_ACCESS_TOKEN` | 具有 `ai.read` 和 `ai.skills.read` 权限的访问令牌；同步时必填。 |
| `NEXUSMIND_LOCAL_DB_PATH` | SQLite 文件路径；默认 `data/creator-center.db`（相对于 MCP 程序目录）。N97 建议设为 `/data/nexusmind/creator-center.db`。 |
| `NEXUSMIND_MCP_ALLOW_SENSITIVE` | 仅在受控本机环境中设为 `true`；默认 `false`。 |

## 运行与 Codex 配置

先构建：

```powershell
dotnet build HomeMind.CreatorMcp/HomeMind.CreatorMcp.csproj
```

将以下配置放入 Codex 的 MCP 配置中，并把令牌改为受限、可轮换的访问令牌：

```json
{
  "mcp_servers": {
    "nexusmind_creator": {
      "command": "dotnet",
      "args": ["run", "--project", "D:/HomeMind/core/HomeMind.CreatorMcp/HomeMind.CreatorMcp.csproj", "--no-build"],
      "env": {
        "NEXUSMIND_API_BASE_URL": "http://localhost:5280",
        "NEXUSMIND_ACCESS_TOKEN": "replace-with-short-lived-token",
        "NEXUSMIND_LOCAL_DB_PATH": "D:/NexusMind/data/creator-center.db"
      }
    }
  }
}
```

可用 MCP 工具：`sync_creator_center`、`search_creator_center`、`get_creator_item` 与 `creator_sync_status`。

## 协议与部署选择

本实现使用 MCP 标准 stdio JSON-RPC transport，适合 Codex CLI 等本地 Agent。`POST /mcp` 不是所有 MCP 客户端通用的唯一端点；若需要远程连接，应在同一只读工具层上再增加带认证、会话和 Origin 校验的 Streamable HTTP transport，而不是让 Agent 直接访问数据库。
