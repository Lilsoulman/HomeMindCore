# 前端 API 集成指南

本文档是 HomeMind 后端（`HomeMind.Api`）的接口契约。
任何控制器变更必须同步更新本文档。

## 1. 基础信息

| 项目 | 值 |
| --- | --- |
| 基础地址（本地开发） | `http://localhost:5280` |
| API 前缀 | `/api/v1` |
| Content-Type | `application/json; charset=utf-8` |
| 认证头 | `Authorization: Bearer <accessToken>` |
| Swagger UI | `http://localhost:5280/swagger` |

## 2. 命名约定

| 方向 | 约定 | 示例 |
| --- | --- | --- |
| 请求体（JSON） | **小驼峰 camelCase** | `{"displayName":"Alex"}` |
| 请求 query / path 参数 | **小驼峰 camelCase** | `?from=2026-08-01&to=2026-08-31` |
| 响应封装字段 | PascalCase | `{"Code":0,"Msg":"ok","Data":{...}}` |
| 响应 `Data` 字段 | **大驼峰 PascalCase** | `"StartAt":"2026-08-01T09:00:00Z"` |

> 响应封装（`Code / Msg / Data`）固定不变。`Data` 内的实际业务负载
> 一律使用 PascalCase，与 C# 属性名保持一致。请求则接受 camelCase。

## 3. 统一响应封装

```json
{
  "Code": 0,
  "Msg": "ok",
  "Data": { ... }
}
```

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Code` | int | `0` = 成功；非零 = 业务/框架错误码 |
| `Msg` | string | 人类可读的消息，成功时为 `ok` |
| `Data` | object \| null | 业务负载；结构取决于具体接口 |

### 常见错误码

| HTTP | `Code` | 含义 |
| --- | --- | --- |
| 200 | 0 | 成功 |
| 400 | 400 | 参数校验错误（缺少必填字段等） |
| 401 | 401 | 缺少或无效的 Bearer 令牌 |
| 401 | 401 | 刷新令牌无效/过期/已撤销 |
| 404 | 404 | 资源不存在，或不属于调用者 |
| 409 | 409 | 冲突（例如手机号已绑定） |
| 422 | 422 | 业务校验失败 |
| 503 | 503 | 数据库服务暂时不可用 |
| 500 | 500 | 未处理的服务器错误 |
| 501 | 501 | 接口被门控，需提供外部配置后才能启用 |

## 4. 认证模块（`/api/v1/auth`）

除认证接口外，所有接口都需要有效的 Bearer 访问令牌。令牌携带
`user_id`、`tenant_id`、`device_id` 和 `role` 声明。刷新令牌是不透明的，
每次调用 `POST /api/v1/auth/refresh` 都会轮换。

### 4.1 `POST /api/v1/auth/register`

使用手机号 + 密码注册新的个人账户并返回会话令牌。
后端会自动创建个人租户。

请求：

```json
{
  "phone": "13800138000",
  "password": "my-strong-pwd",
  "displayName": "Alex",
  "installationId": "9c1f...uuid",
  "platform": "h5"
}
```

响应 `Data`：

```json
{
  "AccessToken": "<jwt>",
  "RefreshToken": "<opaque>",
  "UserId": 12,
  "TenantId": 12
}
```

当手机号已绑定且提供的密码正确时，该接口会创建会话并返回 `200`，
因此"注册并登录"的合并操作是幂等的。错误：`422`（缺少手机号或密码
少于 8 个字符）、`409`（手机号已绑定但密码不匹配）。

### 4.2 `POST /api/v1/auth/login`

手机号 + 密码登录。`installationId` 为必填，用于绑定当前设备会话；
服务器会在 `auth_devices` 表中执行 upsert。

请求：

```json
{
  "phone": "13800138000",
  "password": "my-strong-pwd",
  "installationId": "9c1f...uuid",
  "platform": "h5"
}
```

响应 `Data`：与注册接口结构相同。

### 4.3 `POST /api/v1/auth/refresh`

用刷新令牌换取新的 access + refresh 令牌对。之前的刷新令牌会被撤销；
重复使用已撤销的令牌会使整个令牌族失效。

请求：

```json
{ "refreshToken": "<opaque>" }
```

响应 `Data`：与注册接口结构相同。

### 4.4 `GET /api/v1/auth/me`

权限：`identity.read`。返回当前用户的资料。

```json
{
  "id": 12,
  "DisplayName": "Alex",
  "AvatarUrl": null,
  "status": "active",
  "timezone": "Asia/Shanghai",
  "locale": "zh-CN",
  "CreatedAt": "2026-08-01T03:11:22.123Z"
}
```

### 4.5 `POST /api/v1/auth/logout`

权限：`identity.read`。撤销当前访问令牌及该设备的刷新令牌。

```json
{ "loggedOut": true }
```

### 4.6 `POST /api/v1/auth/wechat/exchange`（受限）

当前返回 HTTP `501`，`Code=501`，消息为
`"WeChat AppId, secret and callback configuration are required before code
exchange can be enabled."`（微信 AppId、密钥和回调配置就绪前无法启用
code 交换）。客户端应将其视为配置阻塞，停止重试。

## 5. 待办事项模块（`/api/v1/todos`）

### 5.1 `GET /api/v1/todos`

权限：`todo.read`。查询参数（均可选）：`status`、`type`、
`from`、`to`（UTC `DateTime`）。

```json
[
  {
    "id": 1,
    "title": "Buy milk",
    "description": "low-fat",
    "type": "task",
    "priority": "p1",
    "color": "#ff8800",
    "status": "pending",
    "DueAt": "2026-08-10T09:00:00Z",
    "RemindAt": "2026-08-10T08:30:00Z",
    "CompletedAt": null,
    "pinned": true,
    "SortOrder": 10,
    "RepeatRule": null,
    "CreatedAt": "2026-08-01T03:11:22.123Z",
    "UpdatedAt": "2026-08-02T03:11:22.123Z"
  }
]
```

### 5.2 `POST /api/v1/todos`

权限：`todo.write`。

请求：

```json
{
  "title": "Buy milk",
  "description": "low-fat",
  "type": "task",
  "priority": "p1",
  "color": "#ff8800",
  "status": "pending",
  "dueAt": "2026-08-10T09:00:00Z",
  "remindAt": "2026-08-10T08:30:00Z",
  "pinned": true,
  "sortOrder": 10,
  "repeatRule": "FREQ=DAILY;COUNT=3",
  "listId": 1,
  "parentId": null
}
```

`status` 默认为 `pending`；`pinned` 默认为 `false`；
`sortOrder` 默认为 `0`。`title` 为必填。

响应 `Data`：与 `GET /api/v1/todos` 的单个行结构相同。

### 5.3 `PUT /api/v1/todos/{id}`

权限：`todo.write`。省略的字段保持不变。将 `status` 设为 `completed`
会写入 `CompletedAt`；重置为 `pending` 会清除它。

请求体：与 `POST /api/v1/todos` 相同。

### 5.4 `DELETE /api/v1/todos/{id}`

权限：`todo.write`。软删除；返回 `{ "id": 12 }`。

### 5.5 `POST /api/v1/todos/{id}/subtasks`

权限：`todo.write`。

```json
{ "text": "Pick up at store", "seq": 1 }
```

响应 `Data`：

```json
{ "id": 5, "text": "Pick up at store", "done": 0, "seq": 1 }
```

### 5.6 `PUT /api/v1/todos/{id}/subtasks/{subId}`

权限：`todo.write`。省略的字段保留原值。

```json
{ "text": "Pick up at store", "done": true, "seq": 1 }
```

### 5.7 `DELETE /api/v1/todos/{id}/subtasks/{subId}`

权限：`todo.write`。软删除；返回 `{ "id": 5 }`。

## 6. 日历模块（`/api/v1/calendar`）

### 6.1 `GET /api/v1/calendar/events`

权限：`calendar.read`。可选的 `from` / `to` 查询参数（UTC `DateTime`）。

```json
[
  {
    "id": 7,
    "title": "Sprint review",
    "description": "Demo + retro",
    "location": "Zoom",
    "StartAt": "2026-08-12T09:00:00Z",
    "EndAt": "2026-08-12T10:00:00Z",
    "timezone": "Asia/Shanghai",
    "AllDay": false,
    "color": "#3366ff",
    "opacity": 1.0,
    "RepeatRule": "FREQ=WEEKLY;BYDAY=WE",
    "CreatedAt": "2026-08-01T03:11:22.123Z",
    "UpdatedAt": "2026-08-02T03:11:22.123Z"
  }
]
```

### 6.2 `POST /api/v1/calendar/events`

权限：`calendar.write`。`title` 和 `startAt` 为必填。

```json
{
  "title": "Sprint review",
  "description": "Demo + retro",
  "location": "Zoom",
  "startAt": "2026-08-12T09:00:00Z",
  "endAt": "2026-08-12T10:00:00Z",
  "timezone": "Asia/Shanghai",
  "allDay": false,
  "color": "#3366ff",
  "opacity": 1.0,
  "repeatRule": "FREQ=WEEKLY;BYDAY=WE"
}
```

### 6.3 `PUT /api/v1/calendar/events/{id}` / `DELETE /api/v1/calendar/events/{id}`

权限：`calendar.write`。负载与创建接口相同，软删除返回 `{ "id": 7 }`。

### 6.4 `GET /api/v1/calendar/subscriptions`

权限：`calendar.read`。返回当前用户的外部 iCal 订阅。

```json
[
  {
    "id": 3,
    "name": "Holidays",
    "enabled": true,
    "RefreshIntervalMin": 60,
    "LastFetchAt": "2026-08-02T03:11:22.123Z",
    "LastError": null,
    "CreatedAt": "2026-08-01T03:11:22.123Z"
  }
]
```

### 6.5 `POST /api/v1/calendar/subscriptions`

权限：`calendar.write`。`url` 必须是绝对 URL，并在服务端加密存储。

```json
{
  "url": "https://example.com/holidays.ics",
  "name": "Holidays",
  "enabled": true,
  "refreshIntervalMin": 60
}
```

### 6.6 `PUT /api/v1/calendar/subscriptions/{id}` / `DELETE /api/v1/calendar/subscriptions/{id}`

权限：`calendar.write`。仅更新 `name` / `enabled` / `refreshIntervalMin`。
删除为软删除。

### 6.7 `POST /api/v1/calendar/ical/fetch`（受限）

返回 HTTP `501`，`Code=501`，消息为
`"iCal network fetch is disabled until SSRF allow-list rules are
configured."`（配置 SSRF 白名单规则前，iCal 网络拉取处于禁用状态）。

## 7. AI 技能模块（`/api/v1/skills`）

### 7.1 `GET /api/v1/skills`

权限：`ai.skills.read`。

```json
[
  {
    "id": 1,
    "name": "Polite rewrite",
    "prompt": "Rewrite the following text politely ...",
    "scopes": "[\"todos\"]",
    "IsBuiltin": true,
    "IsActive": true,
    "CreatedAt": "2026-08-01T03:11:22.123Z",
    "UpdatedAt": "2026-08-01T03:11:22.123Z"
  }
]
```

### 7.2 `POST /api/v1/skills`

权限：`ai.skills.write`。`name` 和 `prompt` 为必填；`scopes`
是 JSON 数组字符串；`isActive` 默认为 `true`。

```json
{
  "name": "Translate to English",
  "prompt": "Translate the following Chinese text to English ...",
  "scopes": "[\"todos\",\"skills\"]",
  "isActive": true
}
```

### 7.3 `PUT /api/v1/skills/{id}` / `DELETE /api/v1/skills/{id}`

权限：`ai.skills.write`。软删除返回 `{ "id": 2 }`。

### 7.4 `GET /api/v1/ai/config`

权限：`ai.config.read`。返回当前用户的 AI 配置（B18 起含启用开关）。未配置时返回默认值。

```json
{
  "Code": 0,
  "Msg": "查询成功。",
  "Data": {
    "Endpoint": "https://api.openai.com/v1",
    "Model": "gpt-4.1-mini",
    "Temperature": 0.7,
    "HasApiKey": true,
    "Enabled": true
  }
}
```

### 7.5 `PUT /api/v1/ai/config`

权限：`ai.config.write`。请求体小驼峰；`apiKey` 可空（不传或空字符串均保留已保存密文）；`enabled` 用于切换 AI 生成能力总开关。切换开关时仅传 `enabled` 即可，不会丢失 API Key。

```json
{
  "endpoint": "https://api.openai.com/v1",
  "model": "gpt-4.1-mini",
  "temperature": 0.7,
  "enabled": true,
  "apiKey": "sk-..."
}
```

### 7.6 `POST /api/v1/ai/{generate,chat,stream}`（B18 占位）

权限：`ai.run`。调用前服务端校验 `enabled`；未启用 → HTTP 422、`Code=42200`、`Msg="AI 生成能力已禁用，请在设置中开启。"`；启用 → 暂返 HTTP 501 占位（待后续切片接入）。请求体 `{ "prompt": "..." }`，当前切片仅占位。

## 8. AI 专家与 AgentRun 模块（`/api/v1/...`）

`ExpertsController` 挂载在 `api/v1` 下（而非 `api/v1/experts`），以保留
旧的 `/experts` 和 `/expert-runs` 路径。`/expert-runs` 是稳定的兼容路由名；
其领域资源和 Flutter DTO 均为 `AgentRun`。所有新的 AI 工作流必须使用
AgentRun。Expert 接口只提供策略（角色、提示词、允许的 Skill 和权限），
自身绝不执行 Skill 或 Connector 调用。

AgentRun 状态永远是以下之一：

```text
draft | queued | planning | running | completed | failed | cancelled
```

客户端只渲染可安全展示的 Run 事件和受控操作。它不得展示提示词、
思维链、供应商日志、凭据或厂商字段。

### 8.1 `GET /api/v1/experts`

权限：`ai.read`。可选查询参数：`query`、`category`、`type`
（`expert` | `group`）、`scope`（B21 起：`basic` 默认/`mine`/`all`）。
B21 起列表项为类型化 `ExpertCatalogItemView`，新增 `Source` 字段
（`basic`=平台基础、`mine`=本人自建）；移动端保持 `scope` 缺省（basic），
选择专家时以 `scope=all` 分组展示基础/自建来源。

```json
{
  "Code": 0, "Msg": "查询成功。", "Data": [
    {
      "Id": 1, "CatalogType": "expert", "Source": "basic",
      "Code": "writing-coach", "Name": "Writing coach",
      "Category": "writing", "Description": "...", "EstimatedCredits": 1
    },
    {
      "Id": 3, "CatalogType": "expert", "Source": "mine",
      "Code": "custom-a1b2c3d4", "Name": "我的助手",
      "Category": "travel", "Description": "...", "EstimatedCredits": 1
    },
    {
      "Id": 2, "CatalogType": "group", "Source": "basic",
      "Code": "research-team", "Name": "Research team",
      "Category": "research", "Description": "...", "EstimatedCredits": 3
    }
  ]
}
```

- `scope=mine` 仅返回 `OwnerUserId=本人` 的自建专家，不泄露他人；`scope=all` 为基础 + 本人自建。
- 跨用户自建专家不会出现在任何列表（前端不应预留他人自建专家的展示分支）。

### 8.2 `GET /api/v1/experts/{id}?type=expert|group`

权限：`ai.read`。B21 起返回类型化 `ExpertDetailView`（新增 `Source` 字段）；
他人自建专家与已软删专家返回 `404`。

```json
{
  "Id": 1,
  "Code": "writing-coach",
  "Name": "Writing coach",
  "Category": "writing",
  "Description": "...",
  "PrivacyScope": null,
  "Source": "basic",
  "VersionId": 4,
  "Version": 4,
  "Persona": "...",
  "Methodology": "...",
  "PromptTemplate": "...",
  "ToolPolicy": "{\"tools\":[\"web.search\"]}",
  "OutputSchema": "{\"type\":\"object\"}",
  "EstimatedCredits": 1
}
```

对于 `type=group`，persona/methodology/toolPolicy 字段由
`OrchestrationPolicy` 取代。

### 8.3 `POST /api/v1/expert-runs`（创建 AgentRun）

权限：`ai.run`。创建 AgentRun 并放入队列；响应 `Data` 与
`GET /api/v1/expert-runs/{id}` 相同。B20 起可携带可选的
`conversationId`（所属专家会话主键，会话发送消息时由服务端传入；
重复幂等键若会话归属不同返回 409）。

```json
{
  "sourceType": "expert",
  "sourceId": 1,
  "inputJson": "{\"messages\":[{\"role\":\"user\",\"content\":\"你好\"}]}",
  "idempotencyKey": "9c1f...uuid",
  "conversationId": 5
}
```

### 8.4 `GET /api/v1/expert-runs/{id}`（获取 AgentRun）

权限：`ai.run`。B20 起响应新增 `ConversationId` 字段（可空）。

```json
{
  "id": 9,
  "SourceType": "expert",
  "status": "queued",
  "Input": "{\"messages\":[{\"role\":\"user\",\"content\":\"你好\"}]}",
  "Result": null,
  "ResultSummary": null,
  "EstimatedCredits": 1,
  "ActualCredits": 0,
  "CreatedAt": "2026-08-02T03:11:22.123Z",
  "StartedAt": null,
  "FinishedAt": null,
  "ConversationId": 5
}
```

### 8.5 `GET /api/v1/expert-runs/{id}/events`

权限：`ai.run`。返回按顺序排列的 Run 事件。

```json
[
  {
    "id": 1,
    "sequence": 1,
    "EventType": "queued",
    "Payload": "{\"message\":\"Run queued\"}",
    "CreatedAt": "2026-08-02T03:11:22.123Z"
  }
]
```

### 8.6 `POST /api/v1/expert-runs/{id}/cancel`

权限：`ai.run`。尽力而为的取消；可立即取消的 Run 会被翻转为
`cancelled`，其他的记录 `cancelRequestedAt`。

```json
{ "id": 9, "cancelRequested": true }
```

### 8.7 `POST /api/v1/expert-runs/{id}/retry`

权限：`ai.run`。仅当 AgentRun 处于 `failed` 或 `cancelled` 状态时允许。

```json
{ "id": 9, "status": "queued" }
```

### 8.8 `POST /api/v1/expert-runs/{id}/actions`

权限：`ai.run`。为后续 Skill 执行创建一个受控操作；
创建操作不会执行任何外部影响。

```json
{
  "actionType": "todos",
  "requestJson": "{\"assignToListId\":1}",
  "idempotencyKey": "9c1f...uuid"
}
```

`actionType` 必须是 `plan`、`todos`、`calendar_events`、
`smart_home_device` 之一。未来的 Skill 执行器会校验操作，然后通过
Connector 网关执行；Flutter 绝不能直接调用厂商、Home Assistant、MQTT、
Zigbee 或 Matter 协议。

### 8.9 `POST /api/v1/housekeeper-runs`（旧版兼容）

该接口为现有的 SmartHome Mock 工作流保留。新页面必须从
`POST /api/v1/expert-runs` 开始，并消费 AgentRun 事件/操作。
Home Assistant 是未来的 SmartHome Connector 适配器，不是 AgentRun
API 契约的依赖。

权限：`ai.run`。根据已同步的 SmartHome 读模型创建一条已完成、
可展示的家庭分析。它绝不发送设备命令。请求使用固定意图、可选的
当前租户空间，以及可选的 UUID 幂等键：

```json
{
  "intent": "sleep",
  "spaceId": 12,
  "idempotencyKey": "9c1f9a71-6d38-4e6a-b1c2-7ef6cf16d6d3"
}
```

`intent` 为 `sleep`、`away`、`arrive`、`environment_review` 之一。响应包含
`Id`、`Status`、`ResultSummary`、时间戳、`Events` 和 `Actions`。事件只包含
`Sequence`、`Type`、`Message` 和 `CreatedAt`。每个设备操作都有
`Status: "pending"`；它暴露规范的 `DeviceId`、显示名称、能力和目标值，
但不包含连接器凭据、厂商 ID 或协议字段。`environment_review` 不返回
设备操作。

```json
{
  "Code": 0,
  "Msg": "家庭管家分析完成。",
  "Data": {
    "Id": 42,
    "Status": "completed",
    "ResultSummary": "已完成家庭状态分析，并生成 1 个待确认行动。",
    "CreatedAt": "2026-08-04T10:00:00Z",
    "FinishedAt": "2026-08-04T10:00:00Z",
    "Events": [
      { "Sequence": 1, "Type": "running", "Message": "正在收集已同步的家庭状态。", "CreatedAt": "2026-08-04T10:00:00Z" }
    ],
    "Actions": [
      { "Id": 78, "ActionType": "smart_home_device", "Status": "pending", "Title": "关闭卧室照明", "Description": "睡眠准备建议关闭卧室照明。", "DeviceId": 34, "DeviceName": "卧室主灯", "Capability": "power", "TargetValue": false }
    ]
  }
}
```

不支持的意图返回 `422`；当迁移 `009` 尚未初始化家庭管家专家时，
该接口返回可读的 `503`。两种错误都不会执行设备操作。

### 8.10 `GET /api/v1/expert-runs/{id}/actions`

权限：`ai.run`。为当前用户和租户返回相同的安全家庭 Run/Event/Action
视图。返回 `404` 表示该 Run 不属于当前租户中的当前用户。

### 8.11 `POST /api/v1/expert-runs/{runId}/actions/{actionId}/confirm`

权限：`ai.run`。确认恰好一个待处理的 `smart_home_device` 操作。
客户端必须为该确认创建并保留一个 UUID 幂等键：

```json
{
  "idempotencyKey": "7c1e7702-e4af-4a9e-b9d4-5f913b50cc91"
}
```

发送命令之前，API 会重新检查：Run 属于当前用户和租户、设备仍然在线、
其能力可写且值类型匹配、Connector 健康，并且成员的 Connector 范围
包含该能力权限。使用相同键的重复请求会返回已记录的结果，
绝不再发送第二次命令。

```json
{
  "Code": 0,
  "Msg": "设备行动已执行。",
  "Data": {
    "ActionId": 78,
    "Status": "executed",
    "Message": "设备行动已执行。",
    "UpdatedAt": "2026-08-04T10:02:00Z"
  }
}
```

配置/密钥错误返回 `503`；远端设备服务故障返回 `502`。两种响应都
不暴露凭据、厂商 ID、服务名或原始供应商错误。跨用户或跨租户的操作
返回 `404`。

### 8.11.1 V2.2 L1 批量确认（已发布，完整契约见 8.19 确认中心）

`POST /api/v1/homes/{homeId}/confirmations/batch-confirm` 已于 B12
发布，请求与幂等重放语义见 8.19。仅当每个选中项都是 `pending`、
未过期且 `riskLevel: "L1"` 时，UI 才可展示批量操作；绝不可为 L2
或 L3 项提供此控件。该请求是"全有或全无"：任一违规项整体拒绝，
客户端必须刷新确认列表，而不是自动重试子集。网络重试时复用同一个
UUID；仅当用户改变了选中的集合后才创建新 UUID。

V2.2 家庭成员卡片将 `memberStatus` 渲染为 `active`、`away`、
`permanently_left` 或 `deceased`；终态修正属于受审计的管理操作，
不是普通开关。知识视图必须保留来源、置信度和冲突策略元数据。
设备健康视图只消费规范化的 `zigbeeRole`、`batteryLevel`、
`signalLqi` 和 `healthStatus`。

### 8.12 SmartHome 读模型（`/api/v1/smart-home`）

权限：`smart_home.read`。这些接口支持 Home+ 空间优先视图。
所有数据按访问令牌租户隔离；客户端不得发送租户 ID。
响应有意省略连接器凭据、厂商 ID 和协议特定字段。

`GET /api/v1/smart-home/spaces`

```json
[
  {
    "Id": 12,
    "Name": "客厅",
    "SpaceType": "living_room",
    "Summary": "环境舒适，主灯已开启。",
    "DeviceCount": 2,
    "UpdatedAt": "2026-08-04T09:00:00Z"
  }
]
```

`GET /api/v1/smart-home/devices?spaceId=12` 接受可选的 `spaceId`。
设备响应提供规范化的能力和状态新鲜度：

```json
[
  {
    "Id": 34,
    "SpaceId": 12,
    "Name": "客厅主灯",
    "DeviceType": "light",
    "OnlineStatus": "online",
    "StateSummary": "已开启，亮度 60%。",
    "StateUpdatedAt": "2026-08-04T09:00:00Z",
    "Capabilities": [
      { "Capability": "power", "ValueSchema": "{\"type\":\"boolean\"}", "Permission": "smart_home.light.write", "IsWritable": true }
    ]
  }
]
```

`GET /api/v1/smart-home/scenes` 返回启用的场景卡片（`Id`、`Key`、
`Name`、`Summary`、`Status`、`UpdatedAt`）。场景执行尚未开放；
在确认的 Action API 交付之前，UI 必须将其视为只读。

`GET /api/v1/smart-home/devices/health`（B10 发布）按家庭/空间聚合
`Healthy`、`Degraded`、`Offline`、`LowBattery` 计数与主导状态。

`GET /api/v1/smart-home/devices/{deviceId}/health`（B14 发布）返回
单台设备健康详情：

```json
{
  "Id": 34,
  "SpaceId": 12,
  "Name": "卧室空调",
  "DeviceType": "air_conditioner",
  "OnlineStatus": "online",
  "ZigbeeRole": "router",
  "BatteryLevel": 15,
  "SignalLqi": 90,
  "HealthStatus": "low_battery",
  "StateUpdatedAt": "2026-08-04T09:00:00Z"
}
```

跨家庭或不存在返回 `404`；`StateUpdatedAt` 是最近采样时间，过期
状态不得描述为实时。UI 应展示电量/信号/健康语义标签而非原始值。

### 8.13 仪表盘与场景运行（`/api/v1`）

`GET /api/v1/dashboard` 需要 `smart_home.read`。它返回一个按用户和
租户隔离的视图，包含 `GeneratedAt` 和 `PartialFailure`。`Home`、
`PendingConfirmations`、`StewardActivities`、`Scenes`、`Todos`、
`Calendar` 和 `Suggestion` 是独立模块。每个模块都有 `Status`
（`available` 或 `unavailable`）、`Data`、`UpdatedAt` 和可选的
可读 `Message`；当一个模块不可用时，UI 必须保留其他可用模块的
卡片，且待确认事项（`PendingConfirmations`）在任何模块失败时仍应
优先展示。

`Home.Data` 包含家庭统计和空间摘要（产品契约中的 `homeSummary`），
含设备在线/离线数量以及最新的规范化状态时间戳。
`PendingConfirmations.Data` 是最多 6 条未过期的待确认事项
（`Id`、`RiskLevel`、`Title`、`ImpactSummary`、`Status`、
`ExpiresAt`、`UpdatedAt`），按到期时间升序。
`StewardActivities.Data` 是最多 6 条最近管家动态（`Id`、`Category`、
`Title`、`RiskLevel`、`Status`、`ResultSummary`、`CreatedAt`）。
`quickActions`（快捷入口）为前端静态入口，不经过后端。
`Todos.Data` 和 `Calendar.Data` 最多包含今天当前用户的 6 条。
`Scenes.Data` 始终暴露标准的 `arrive_home`、`leave_home` 和
`sleep` 快捷方式。响应绝不暴露连接器凭据、厂商实体 ID、协议字段
或原始设备状态。

`POST /api/v1/smart-home/scenes/{sceneKey}/run` 需要 `ai.run`，接受：

```json
{ "idempotencyKey": "9c1f9a71-6d38-4e6a-b1c2-7ef6cf16d6d3" }
```

支持的键为 `arrive_home`、`leave_home` 和 `sleep`（别名 `arrive` 和
`away` 也可接受）。B22 起该路由为兼容代理：服务端懒启用对应场景
模板实例并转调场景运行链路，请求与响应契约不变；操作保持
`pending` 状态，直到通过确认接口执行。它绝不直接下发设备命令。

### 8.13.1 场景工作流（B22，`/api/v1/smart-home/scenarios`）

平台模板 → 家庭实例 → 单场景运行动作；确认/幂等/审计复用既有
Run Action 链路。

**模板与实例（`smart_home.read`）**

`GET /api/v1/smart-home/scenarios/templates` 返回平台模板列表：

```json
{
  "Code": 0,
  "Msg": "查询成功。",
  "Data": [
    {
      "Id": 1, "Code": "goodnight", "Name": "晚安", "Summary": "关闭卧室照明并将空调调至睡眠温度。",
      "Status": "active",
      "Steps": [
        { "Id": "step_1", "Name": "关闭卧室灯", "DeviceType": "light", "Room": "bedroom", "Capability": "power", "Value": false, "Optional": false }
      ]
    }
  ]
}
```

`GET /api/v1/smart-home/scenarios/instances` 返回家庭实例；步骤已
解析到 `DeviceId`，缺设备步骤 `StepStatus=unavailable` 并携带
`Reason`（如 `no matching device`），执行时跳过、不阻塞启用。

**启用（`smart_home.write`）**

`POST /api/v1/smart-home/scenarios/templates/{templateCode}/enable`
按 `device_type + room + capability` 匹配家庭设备生成实例；同一模板
重复启用返回既有实例（200），已禁用实例重复启用时恢复为 `enabled`。
`templateCode` 支持 `goodnight`/`arrive_home`/`leave_home`。

**禁用（`smart_home.write`）**

`POST /api/v1/smart-home/scenarios/instances/{instanceId}/disable`
将实例状态置为 `disabled`（200，幂等），返回的 `Data.Status` 为
`disabled`。禁用后 `POST /instances/{instanceId}/run` 返回 404
（「不存在或未启用」）；禁用只阻止新触发，已创建的待确认运行不受
影响，仍可正常确认执行。实例不存在、跨租户或已软删除返回 404。
前端可在实例卡片提供「禁用」入口，禁用后按钮态切换为「启用」。

**运行（`ai.run`）**

`POST /api/v1/smart-home/scenarios/instances/{instanceId}/run`：

```json
{ "idempotencyKey": "9c1f9a71-6d38-4e6a-b1c2-7ef6cf16d6d3" }
```

响应 `Data` 为 `ScenarioRunView`（`Id`/`Status`=`pending_actions`/
`ResultSummary`/`Events`/`Actions`）。`Actions[0]` 为单个 `scenario`
类型动作（`Title`=场景名、`Description` 含步骤数与风险等级、
`RiskLevel` 取步骤风险最大值）。

**确认执行（`ai.run`）**

`POST /api/v1/smart-home/scenarios/runs/{runId}/actions/{actionId}/confirm`：

```json
{ "idempotencyKey": "9c1f9a71-6d38-4e6a-b1c2-7ef6cf16d6d3" }
```

确认后逐步下发设备命令；required 步骤失败后继续执行后续步骤。
`run.Result` 输出汇总（消费方只读以下字段，**禁止解析 steps 明细
JSON**）：

```json
{
  "scenario": "晚安", "status": "partial", "summary": "场景「晚安」执行完成：1 项成功，1 项失败（关闭卧室灯（模拟执行失败））。",
  "success_count": 1, "failed_count": 1,
  "failed_steps": [{ "name": "关闭卧室灯", "reason": "模拟执行失败。" }]
}
```

`status` 规则：全部失败 → `failed`；required 有失败且存在成功 →
`partial`；仅 optional 失败或全部成功 → `success`。同一幂等键重复
确认重放首次结果，不重复执行设备命令；非法幂等键 422、动作不存在
404、已终态 409。

### 8.14 连接器管理（`/api/v1`）

Connector 响应绝不包含 `credentialRef`、URL、访问令牌、刷新令牌、
厂商实体 ID 或协议字段。租户从访问令牌中推导；客户端不得发送租户 ID。

`GET /api/v1/connector-providers`

权限：`connector.read`。返回启用的目录条目（`Id`、`Code`、
`Name`、`ConnectorType`、`Description`）。

`GET /api/v1/connectors`

权限：`connector.read`。所有者和管理员收到租户的连接器列表；
成员只收到拥有有效个人授权的连接器。每项包含 `Id`、`ProviderId`、
`ProviderCode`、`ProviderName`、`Name`、`Status`、`LastSyncAt`、
`LastHealthAt`、`CreatedAt`、`UpdatedAt`、`BindingScope`
（`household`/`personal`）和 `IsCurrentUserOwner`（个人实例且当前
用户为 owner 时为 `true`，绝不返回 owner 标识本身）。B18 起
`personal` 实例仅向所有者本人返回，owner/admin 亦不可见他人
个人实例。

`POST /api/v1/connectors`

权限：`connector.write`（所有者/管理员）。仅接受以下请求体字段；
未知或厂商凭据字段返回 `422`。

```json
{
  "providerId": 1,
  "name": "My home",
  "credentialRef": "vault://tenants/12/secrets/home-assistant",
  "bindingScope": "household"
}
```

`bindingScope` 可选，默认 `household`；`personal` 时所有者由服务端
从 JWT 推导（当前用户且为活跃成员），客户端不得覆盖。`credentialRef`
必须属于调用者的租户。它会经过校验但绝不返回。
当 `SecretVault:Enabled=false`（默认值）时，创建返回 `503` 和可读的
配置消息。创建成功后始终以 `disconnected` 状态开始。

`POST /api/v1/connectors/{id}/test`

权限：`connector.write`（所有者/管理员）。测试已配置的 Home Assistant
连接并更新其健康状态。成功响应只返回规范化的操作视图；绝不暴露
HA URL、令牌或实体信息。

```json
{
  "Code": 0,
  "Msg": "Home Assistant 连接测试成功。",
  "Data": {
    "ConnectorId": 8,
    "Status": "connected",
    "DeviceCount": 0,
    "LastHealthAt": "2026-08-04T10:00:00Z",
    "LastSyncAt": null
  }
}
```

`POST /api/v1/connectors/{id}/discovery` 和 `POST /api/v1/connectors/{id}/sync`

权限：`connector.write`（所有者/管理员）。两个请求都会查询 HA 状态
API，只将 light、switch、air-conditioner、cover 和 sensor 实体映射到
标准设备模型，然后写入当前规范化的状态快照。它们返回 `ConnectorId`、
`Status`、`DeviceCount`、`LastHealthAt` 和 `LastSyncAt`。未知的 HA
域会被忽略。`502` 表示 HA 不可达、拒绝了请求或返回了无效数据；
`503` 表示 Vault 不可用、拒绝了租户路径或包含无效密钥。两种响应都
不包含原始 HA 实体 ID 或协议字段。

`POST /api/v1/connectors/{id}/sync` 在持久化后台同步任务后返回 `202`。
使用 `connector.read` 轮询 `GET /api/v1/connectors/sync-jobs/{jobId}`。
任务视图只包含 `Id`、`ConnectorId`、`Status`、`Reason`、`AttemptNo`、
`AvailableAt`、`CompletedAt` 和 `UpdatedAt`；状态为 `queued`、`running`、
`completed` 或 `failed`。服务器应用 30 秒超时，最多三次尝试并带指数
退避。任务处于 `queued` 或 `running` 时，客户端不得通过创建并行请求
来重试。

运行时 Vault 要求：将 `SecretVault:Enabled=true` 和
`SecretVault:Endpoint` 设为 HashiCorp Vault 基础 URL，并仅通过
`SecretVault:TokenEnvironmentVariable` 指定的进程环境变量提供其令牌
（默认 `NEXUSMIND_SECRET_VAULT_TOKEN`）。对于
`vault://tenants/12/secrets/home-assistant`，适配器读取
`GET {Endpoint}/v1/tenants/12/secrets/home-assistant`。Vault KV 响应
可以使用 `data.baseUrl` / `data.accessToken` 或 KV v2 的
`data.data.baseUrl` / `data.data.accessToken`；两个值都保留在适配器
进程内存中。

`GET /api/v1/connectors/{id}/authorization`

权限：`connector.read`。只返回当前成员的授权：
`ConnectorId`、`UserId`、`Scopes`、`UpdatedAt`。没有授权的成员得到
`403`；当前租户之外的连接器得到 `404`。

`PUT /api/v1/connectors/{id}/authorizations/{memberUserId}`

权限：`connector.write`（所有者/管理员）。授予或替换当前租户成员的
范围。请求必须包含 1 到 32 个格式良好的范围。

```json
{ "scopes": ["smart_home.read", "smart_home.light.write"] }
```

### 8.14.1 个人 OAuth 授权（B18，`/api/v1`）

个人授权路由需要 `connector.authorize`（owner/admin/member）。
所有响应与日志均不含授权 code、访问令牌、刷新令牌或凭据引用；
会话 state 仅存哈希、10 分钟过期、单次使用。

`POST /api/v1/connector-providers/{providerCode}/authorizations`

请求体：

```json
{ "redirectUri": "https://app.example.com/callback" }
```

`redirectUri` 必须命中 Provider 预注册白名单（服务端配置
`ConnectorOAuth:AllowedRedirectUris`），否则 `422`。Vault 不可用时
`503` + `50001`。成功返回 `201`：

```json
{
  "Code": 0,
  "Msg": "授权会话已创建。",
  "Data": {
    "SessionId": 101,
    "ProviderCode": "mock_oauth",
    "ProviderName": "Mock OAuth（开发验证）",
    "Status": "pending",
    "ExpiresAt": "2026-08-07T10:10:00Z",
    "AuthorizationUrl": "http://localhost:5280/api/v1/connector-providers/mock_oauth/authorize?state=..."
  }
}
```

浏览器跳转 `AuthorizationUrl`；Mock Provider 授权页（匿名）直接跳转
服务端回调 `GET /api/v1/connector-providers/{providerCode}/callback?state=&code=`
（匿名），回调完成后 302 到会话 `redirectUri`。

`GET /api/v1/connector-authorizations/{id}`

仅本人可查，返回脱敏状态（`Status`/`ExpiresAt`/`RedirectUri`）；
非本人或跨租户统一 `404`。

`DELETE /api/v1/connector-authorizations/{id}`

撤销本人实例的凭据可用性（实例 `AuthStatus=revoked`、
`Status=disconnected`）并终止会话；重复撤销幂等返回既有结果；
写 `connector_authorize_revoked` 审计。

### 8.15 自动化规则（`/api/v1/automation-rules`）

`GET /api/v1/automation-rules` 需要 `automation.read`；`POST` 和
`PATCH /api/v1/automation-rules/{id}` 需要 `automation.write`
（所有者/管理员）。租户和所有者来自访问令牌。客户端不能提交
所有者、租户、凭据、厂商实体标识符或任意命令。

```json
{
  "name": "日落后回家照明",
  "triggerType": "time_schedule",
  "trigger": {
    "kind": "sun",
    "event": "sunset",
    "timeZone": "China Standard Time",
    "latitude": 39.9042,
    "longitude": 116.4074,
    "offsetMinutes": 5
  },
  "conditions": [],
  "actions": [{ "sceneKey": "arrive_home" }],
  "approvalPolicy": "manual_confirmation",
  "enabled": true
}
```

`triggerType` 为 `time_schedule`、`device_state_change`、`scene_completed`
或 `sync_completed`。时间触发器支持 `fixed_time`（`time: "21:30"`）、
`sun`（`sunrise`/`sunset`）和一次性 `countdown`（`fireAt` UTC）。
设备状态触发器需要一个租户拥有的 `deviceId`；场景触发器使用现有的
场景键；同步完成触发器可选地收窄到某个连接器 ID。条件使用 `deviceId`、
`capability`、可选的 `operator: "not_equals"` 和 `value` 比较规范化
的设备状态。操作仅限于内置的 `sceneKey` 值。

`approvalPolicy` 为 `manual_confirmation` 或 `auto_execute`。前者创建
正常的待处理 Run 操作。后者使用规则所有者的当前 Connector 授权和
现有的幂等确认/审计路径；它不暴露直接的设备命令 API。更新需要
`rowVersion`，并发变更时返回 `409`。响应只暴露规范化的规则视图。

### 8.16 专家文件（`/api/v1`）

所有响应和审计按租户隔离。文件、附件和读取令牌绝不包含凭据、
内部对象路径、存储提供方密钥、厂商实体 ID 或第三方文件 ID。

`POST /api/v1/expert-files` — 权限 `expert_file.write`。创建上传会话。
请求仅含元数据；二进制内容通过返回的短时 URL 上传。

```json
{
  "name": "周报模板.md",
  "mimeType": "text/markdown",
  "sizeBytes": 4096,
  "sha256": "9c1f9a71...",
  "quotaBytes": 4096,
  "idempotencyKey": "9c1f9a71-6d38-4e6a-b1c2-7ef6cf16d6d3"
}
```

```json
{
  "FileId": 901,
  "Status": "pending_upload",
  "UploadToken": "...",
  "UploadUrl": "api/v1/expert-files/901/objects/<objectKey>?uploadToken=...",
  "ExpiresAtUnixTime": 1722768000
}
```

`POST /api/v1/expert-files/{fileId}/objects` — 权限 `expert_file.write`。
提交已提交的对象元数据；服务器根据扫描结果将文件转为 `scanning`，
然后转为 `ready` 或 `rejected`。只有 `ready` 的文件才能被附加或读取。

`GET /api/v1/expert-files` — 权限 `expert_file.read`。返回最新的 100 行
摘要（`Id`、`Name`、`MimeType`、`SizeBytes`、`Status`、扫描字段、
过期时间、软删除标记、`RowVersion`）。

`DELETE /api/v1/expert-files/{fileId}` — 权限 `expert_file.write`。
软删除并清理存储；响应为删除后的摘要。

`POST /api/v1/expert-files/{fileId}/read-token?purpose=download` — 权限
`expert_file.read`。签发 10 分钟有效的 `readToken` 和 `readUrl`；
响应绝不包含内部对象键或存储路径。

`POST /api/v1/experts/{expertId}/files` 和
`POST /api/v1/expert-runs/{runId}/files` — 权限 `expert_file.write`。
请求体为 `{ "fileId": <id>, "idempotencyKey"?: "<uuid>" }`。只接受
调用者租户内 `ready` 状态的文件。

### 8.17 团队运行（`/api/v1/team-runs`）

`POST /team-runs`、`/cancel`、`/retry` 需要 `team_run.write` 权限；
其余接口需要 `team_run.read`。首个发布的 `teamVersion` 是 `1`；
客户端必须精确发送 `"teamVersion": "1"`。只接受三种模式：
`sequential`、`parallel`、`synthesis`。外部副作用（设备写入、
通知等）仍由现有的 Run 操作确认、适配器和审计链路治理；团队运行
只能编排调用者已经拥有的 Expert 和 ExpertFile 引用。

`POST /api/v1/team-runs` 请求体：

```json
{
  "teamVersion": "1",
  "mode": "sequential",
  "parentAgentRunId": 12345,
  "members": [
    { "expertVersionId": 11, "displayName": "梳理员", "stageOrder": 0 },
    { "expertVersionId": 12, "displayName": "评审员", "stageOrder": 1 }
  ],
  "fileIds": [901, 902],
  "idempotencyKey": "9c1f9a71-6d38-4e6a-b1c2-7ef6cf16d6d3"
}
```

服务器冻结团队，计算每个成员的权限交集（`ai.read`、`ai.run`，加上
ExpertVersion 的 `toolPolicy`），并写入审计条目 `team_run_create`。
`parentAgentRunId` 必须引用调用者租户中已有的 `AgentRun`。响应为
`TeamRunSummary`（`Id`、`Status`、`Mode`、`TeamVersion`、
`ParentAgentRunId`、时间戳、`RowVersion`）。

`GET /api/v1/team-runs/{id}` 返回 `TeamRunSummary`。`Status` 为
`pending | running | completed | failed | cancelled` 之一。

`GET /api/v1/team-runs/{id}/events` 返回最近的审计派生
`TeamRunEvent` 列表。不暴露任何提示词、模型输出或供应商日志。

`GET /api/v1/team-runs/{id}/members` 返回每个成员的显示名称、
`StageOrder`、`ExpertVersionId`、可选的 `ChildAgentRunId`、`Status`、
可选的 `LastErrorCode` 和 `PermissionIntersectionSummary`（逗号分隔的
范围名）。

`GET /api/v1/team-runs/{id}/synthesis` 仅当运行处于 `completed` 状态时
可用；否则返回 `409`。视图包含 `Summary`、`Highlights` 和 `CompletedAt`。
任何步骤都不会返回中间成员输出和提示词。

`POST /api/v1/team-runs/{id}/cancel` 仅在运行处于 `pending` 或
`running` 时有效。`POST /api/v1/team-runs/{id}/retry` 仅在终态之后
有效。两者都会写入审计条目并更新 `HomeMind.Automation` 计数器。
跨租户或未知的 `teamRunId` 返回 `404`。

### 8.18 管家动态（`/api/v1/homes/{homeId}/activities`）

所有 `homeId` 必须等于 JWT `tenant_id`；跨家庭资源一律返回 `404`。

`GET /api/v1/homes/{homeId}/activities?limit=&cursor=`

权限：`steward.activity.read`（B14 收敛）。游标分页（按 `CreatedAt`+`Id` 倒序，
`limit` 上限 50）。响应：

```json
{
  "Code": 0,
  "Msg": "查询成功。",
  "Data": {
    "Items": [
      {
        "Id": 1,
        "RunId": 42,
        "Category": "reporting",
        "Title": "已确认：调低热水器温度",
        "Description": null,
        "RiskLevel": "L2",
        "Status": "confirmed",
        "ResultSummary": null,
        "Undoable": false,
        "UndoneAt": null,
        "CreatedAt": "2026-08-05T10:00:00Z",
        "UpdatedAt": "2026-08-05T10:00:00Z"
      }
    ],
    "Cursor": null
  }
}
```

`GET /api/v1/homes/{homeId}/activities/{id}` 返回单条活动；不存在
返回 `404`。

`POST /api/v1/homes/{homeId}/activities/{id}/undo`

权限：`confirmation.write`（B14 收敛）。撤销仅接受 `Undoable=true` 且 `Status=completed` 的
活动；服务端在撤销前实时复验资源状态并写 `activity_undo` 审计。
非已完成或不可撤销返回 `422`，已撤销返回 `409`。撤销为本地状态
迁移（置位 `UndoneAt`、`Undoable=false`），当前不调用设备 Adapter。

### 8.19 确认中心（`/api/v1/homes/{homeId}/confirmations`）

`GET /api/v1/homes/{homeId}/confirmations?riskLevel=&status=`

权限：`confirmation.read`（B14 收敛）。`riskLevel` 仅 `L1`/`L2`/`L3`，`status` 为
`pending`/`confirmed`/`denied`/`expired`/`cancelled`；非法参数返回
`422`。`pending` 项自动排除已过期项（过期采用计算语义，不写回填）。

`POST /api/v1/homes/{homeId}/confirmations/{id}/confirm`

权限：`confirmation.write`（B14 收敛）。请求体 `{ "IdempotencyKey": "uuid" }`（必填，仅校验
UUID 格式）。确认后写入 `confirmation_confirm` 审计并生成管家动态；
关联的 `pending` 活动转为 `confirmed`。重复确认已确认项返回现有
结果（`200` 重放，不重复审计）；已终态或过期返回 `409`。

`POST /api/v1/homes/{homeId}/confirmations/{id}/deny`

权限：`confirmation.write`（B14 收敛）。请求体 `{ "Reason": "1-512 字符" }`（必填）。拒绝
后写 `confirmation_deny` 审计（原因入审计）；关联 `pending` 活动
转为 `cancelled`。已拒绝返回 `200` 重放；已确认/终态/过期返回 `409`。

`POST /api/v1/homes/{homeId}/confirmations/batch-confirm`

权限：`confirmation.write`（B14 收敛）。仅 L1 批量确认，请求体：

```json
{
  "ConfirmationIds": [101, 102, 103],
  "IdempotencyKey": "bc20666d-1639-420f-94d4-f5acb45762e1"
}
```

服务在单事务内预验证所有 ID 属于当前 JWT 家庭、均为未过期
`pending` 的 L1 项且无重复 ID 后原子确认。任一违规整体拒绝：
形状非法（键非 UUID/空列表/重复 ID/超 50 项）→ `422`；任一 ID
跨家庭 → `404`；任一 L2/L3、非 pending、已终态、已过期 → `409`。
同一幂等键且同一 ID 集合的重复请求返回首次结果
（`200` 幂等重放），绝不重复确认；同键异集 → `409`。响应：

```json
{
  "Code": 0,
  "Msg": "批量确认完成。",
  "Data": {
    "ConfirmedCount": 2,
    "Items": [
      {
        "Id": 101,
        "ActivityId": null,
        "RiskLevel": "L1",
        "Title": "开阳台灯",
        "Description": null,
        "ImpactSummary": null,
        "SuggestedAction": null,
        "Status": "confirmed",
        "ExpiresAt": null,
        "ConfirmedAt": "2026-08-05T10:00:00Z",
        "DeniedAt": null,
        "ExpiredAt": null,
        "UpdatedAt": "2026-08-05T10:00:00Z"
      }
    ]
  }
}
```

确认、拒绝与批量确认均写入 `family_audit_logs` 与管家动态；响应
绝不包含凭据、供应商字段、Prompt 或模型思考链。

### 8.20 个人偏好收藏（`/api/v1/life/favorites`，B15 发布）

家庭归属由 JWT 推导，客户端不得发送家庭 ID；跨家庭与越权访问
一律返回 `404`。

| 路由 | 权限 | 说明 |
| --- | --- | --- |
| `GET /api/v1/life/favorites?category=&visibility=` | `life.favorite.read` | 列表；`private` 项仅归属成员本人可见 |
| `GET /api/v1/life/favorites/{id}` | `life.favorite.read` | 详情；不存在或不可见统一 `404` |
| `POST /api/v1/life/favorites` | `life.favorite.write` | 创建；`OwnerMemberId` 可空，缺省为当前成员 |
| `PUT /api/v1/life/favorites/{id}` | `life.favorite.write` | 更新；仅本人或家庭管理员，否则 `403` |
| `DELETE /api/v1/life/favorites/{id}` | `life.favorite.write` | 软删除；权限同上 |
| `POST /api/v1/life/favorites/import` | `life.favorite.write` | 对话导入；`Source` 留痕入审计 |

`GET` 响应视图：

```json
[
  {
    "Id": 501,
    "OwnerMemberId": 3,
    "Category": "restaurant",
    "Name": "老王面馆",
    "DetailJson": "{\"cuisine\":\"面食\",\"address\":\"...\",\"tags\":[\"面\"],\"source\":\"小红书\"}",
    "Visibility": "private",
    "CreatedAt": "2026-08-06T02:00:00Z",
    "UpdatedAt": "2026-08-06T02:00:00Z"
  }
]
```

创建/导入请求体：`{ "Category", "Name", "DetailJson"?, "Visibility"?
（默认 private）, "OwnerMemberId"?（创建）/ "Source"?（导入） }`。
UI 不得把 `private` 收藏展示给家庭其他成员；破坏性删除必须二次确认。

### 8.21 个人生活专家翻牌（`POST /api/v1/experts/personal-life-expert/runs`，B16 发布）

权限：`ai.run`。请求体：

```json
{
  "Intent": "recommend",
  "InputJson": "{\"time\":\"evening\",\"location\":\"城西\",\"taste\":\"面\"}",
  "IdempotencyKey": "bc20666d-1639-420f-94d4-f5acb45762e1"
}
```

`Intent` 仅 `recommend`（`plan` 待行程规划版本开放）；`InputJson` 必须
为合法 JSON。响应 `Data` 含 `Status`、`ResultSummary`、`Events`
时间线与 `Recommendations`（`FavoriteId`、`Name`、`Reason`、`Tags`）。
翻牌为只读 L1，不产生确认动作。专家未初始化（017 未应用）→ `503`。
UI 展示建议卡时呈现理由与标签，不渲染任何提示或思考链。

### 8.22 行程规划与日历同步（B17 发布）

`POST /api/v1/experts/personal-life-expert/runs`，`Intent: "plan"`：

```json
{
  "Intent": "plan",
  "InputJson": "{\"destination\":\"杭州\",\"days\":2}",
  "IdempotencyKey": "bc20666d-1639-420f-94d4-f5acb45762e1"
}
```

`InputJson` 支持 `destination`（必填，1-64 字符）与 `days`（1-7，默认 1）。
响应 `Data` 含 `Status: "pending_actions"`、`ResultSummary` 与 `Actions`
（`Id`、`ActionType: "calendar_create_event"`、`Status`、`Title`、
`Description`、`RiskLevel: "L1"`）。

`POST /api/v1/experts/personal-life-expert/runs/{runId}/actions/{actionId}/confirm`
请求体 `{ "IdempotencyKey": "uuid" }`：确认后逐日创建日历事件
（标题 `{目的地} 行程 D{n}`），重复幂等键返回首次结果，动作已终态
返回 `409`。UI 行程页应在确认前展示影响范围（N 天 → N 个日历事件）
与确认要求；提交中禁用按钮并使用新的幂等键。

### 8.23 成员与角色受控管理（B19，`/api/v1/homes/{homeId}/members`）

家庭归属由 JWT 推导，`{homeId}` 必须等于当前 JWT tenant_id；跨家庭一律 `404 + 30000`。
读接口 `tenant.read`（全员），写接口 `tenant.member.manage`（owner/admin）。

`GET /api/v1/homes/{homeId}/members`：

```json
{
  "Code": 0,
  "Msg": "查询成功。",
  "Data": [
    {
      "UserId": 12,
      "DisplayName": "Alex",
      "AvatarUrl": null,
      "Role": "owner",
      "Status": "active",
      "JoinedAt": "2026-08-01T03:11:22.123Z",
      "Timezone": "Asia/Shanghai",
      "Locale": "zh-CN",
      "IsCurrentUserOwner": true,
      "HasPendingInvitation": false,
      "RowVersion": 1
    }
  ]
}
```

`PUT /api/v1/homes/{homeId}/members/{memberUserId}/role`（`tenant.member.manage`）：

```json
{ "NewRole": "admin", "RowVersion": 3 }
```

- `NewRole` 仅 `admin`/`member`/`viewer`；直接置 `owner` 返回 `422 + 42202`，必须走 owner-transfer。
- `RowVersion` 与服务端不一致返回 `409 + 40901`（并发保护，UI 需刷新后重试）。

`PUT /api/v1/homes/{homeId}/members/{memberUserId}/status`（`tenant.member.manage`）：

```json
{ "NewStatus": "suspended", "Reason": "长期未使用", "RowVersion": 3 }
```

- `NewStatus` 仅 `active`/`suspended`；停用时 `Reason` 必填（1-512 字符）。
- 不能停用最后一名 active owner（返回 `422`）。UI 应对 owner 停用操作二次确认。

`POST /api/v1/homes/{homeId}/owner-transfer`（`tenant.member.manage`）：

```json
{ "NewOwnerUserId": 22, "RowVersion": 1 }
```

- 仅当前 active owner 可发起（非 owner 返回 `403`）；新 owner 必须为 active 成员（suspended/away 返回 `422 + 42201`）。
- 成功后原 owner 自动降为 `admin`，新 owner 升为 `owner`，`tenants.owner_user_id` 同步更新；响应为新 owner 摘要。
- UI 在转让成功后可切换到新 owner 视角；转让与成员列表刷新使用新的 `RowVersion`。

### 8.24 邀请流程（B19，`/api/v1/homes/{homeId}/invitations`）

`POST /api/v1/homes/{homeId}/invitations`（`tenant.member.manage`）：

```json
{ "Phone": "+8613800138000", "ProposedRole": "member" }
```

- `Phone` 需为 E.164 格式（`+` 开头 8-15 位数字）；`ProposedRole` 仅 `admin`/`member`/`viewer`（不得为 owner）。
- 同手机号在当前家庭已存在未结邀请返回 `409 + 40902`；成功返回 `201`，邀请 7 天过期。

`GET /api/v1/homes/{homeId}/invitations?status=pending`（`tenant.read`）：
返回 `{ "Items": [...], "Cursor": null }`；`status` 可选 `pending`/`accepted`/`expired`/`revoked`。
每条含 `Id`/`InvitedByUserId`/`SubjectKind`/`SubjectHashHex`（十六进制摘要，不回传手机号明文）/
`ProposedRole`/`Status`/`ExpiresAt`/`AcceptedUserId`/`AcceptedAt`/`RevokedAt`/`CreatedAt`/`RowVersion`。

`DELETE /api/v1/homes/{homeId}/invitations/{invitationId}`（`tenant.member.manage`）：
撤销 pending 邀请；已终态（accepted/expired/revoked）返回 `409`。UI 撤销前二次确认。

`POST /api/v1/invitations/accept`（`tenant.read`，受邀人本人调用，无 homeId 路径参数）：

```json
{ "InvitationId": 101, "Phone": "+8613800138000" }
```

- 服务端重新计算手机号 SHA-256 并与当前账户已验证的 `user_identities` 匹配；未匹配/未验证/已吊销统一返回 `404 + 30001`。
- 成功以 `proposed_role` 创建 active `tenant_members` 并返回 `{ "TenantId": ..., "Role": "member" }`。
- UI 应引导受邀人先完成登录/手机号验证再调用；接受成功后跳到该家庭视角。

### 8.25 Web 导航偏好（B19，`/api/v1/web/navigation`）

`GET /api/v1/web/navigation`（`tenant.read`）：返回当前家庭当前角色的导航（8 个已发布 route_key 白名单 + 持久化偏好合并）。

```json
{
  "Code": 0,
  "Msg": "查询成功。",
  "Data": {
    "Role": "member",
    "Routes": [
      { "RouteKey": "tenant.dashboard", "Enabled": true, "SortOrder": 100, "IsCustomized": false },
      { "RouteKey": "tenant.life", "Enabled": false, "SortOrder": 3, "IsCustomized": true }
    ],
    "UpdatedAt": "2026-08-07T09:00:00Z"
  }
}
```

`PUT /api/v1/web/navigation`（`tenant.member.manage`，owner/admin）：

```json
{
  "TargetRole": "member",
  "Items": [
    { "RouteKey": "tenant.life", "Enabled": false, "SortOrder": 3 }
  ]
}
```

- `RouteKey` 必须命中后端已发布白名单（`tenant.dashboard`/`tenant.confirmations`/`tenant.steward`/`tenant.knowledge`/`tenant.family`/`tenant.life`/`tenant.connectors`/`tenant.connector.authorize`），未发布返回 `422 + 42203`。
- UI 只做显隐/排序，不存 URL、权限表达式或脚本；`Enabled=false` 仅隐藏菜单项，不删除数据。

### 8.26 我的个人连接汇总（B19，`/api/v1/connector-authorizations/my`）

`GET /api/v1/connector-authorizations/my`（`connector.authorize`）：仅返回当前用户作为 owner 的 personal 实例 + 最近一次授权会话状态，不返回凭据引用或 owner 标识。

```json
{
  "Code": 0,
  "Msg": "查询成功。",
  "Data": [
    {
      "ConnectorId": 8,
      "ProviderId": 1,
      "ProviderCode": "mock_oauth",
      "ProviderName": "Mock OAuth（开发验证）",
      "Name": "我的日历",
      "Status": "connected",
      "AuthStatus": "connected",
      "LastSyncAt": null,
      "LastHealthAt": "2026-08-07T09:00:00Z",
      "LastSessionId": 101,
      "LastSessionStatus": "completed",
      "LastSessionExpiresAt": "2026-08-07T10:00:00Z"
    }
  ]
}
```

UI 据此渲染"我的连接"列表：`Status` 为连接运行健康，`AuthStatus` 为授权生命周期；
`LastSessionStatus=pending` 且未过期时展示"等待完成授权"；`revoked` 展示重新授权入口。

### 8.27 专家会话（B20，已发布）

会话为个人资源，路由无 `homes` 前缀，`tenant_id`/`owner_user_id` 由 JWT 推导；
跨用户/跨租户/已软删一律 `404`。权限：读 `conversation.read`、写 `conversation.write`
（owner/admin/member/viewer 均可读；写仅 owner/admin/member）。

`GET /api/v1/conversations?limit=20&cursor=`（会话列表，按 `updatedAt` 倒序游标分页）：

```json
{ "Code": 0, "Msg": "查询成功。", "Data": {
  "Items": [
    { "Id": 5, "Title": "杭州周末行程", "ExpertId": 2, "ExpertVersionId": 21,
      "WorkspaceConnectorId": null, "CreatedAt": "2026-08-07T09:00:00Z",
      "UpdatedAt": "2026-08-07T09:30:00Z", "RowVersion": 1 }
  ],
  "Cursor": null } }
```

`POST /api/v1/conversations`（创建，请求 `{ "title": "杭州周末行程", "expertId": 2 }`）：
- `expertId`/`workspaceConnectorId` 均可空；绑定专家时解析最新已发布版本，不可见返回 `404 + 30000`；
- 成功 `201`，`Data` 为 `ConversationView`（同上结构，含 `RowVersion`）。

`PUT /api/v1/conversations/{id}`（全量更新：重命名/重绑，请求
`{ "title": "...", "expertId": null, "workspaceConnectorId": null, "rowVersion": 1 }`）：
- `RowVersion` 与服务端不一致返回 `409 + 40903`（刷新后重试）；
- `expertId: null` 即解绑专家（后续发送消息将返回 422）。

`DELETE /api/v1/conversations/{id}`：软删除（`200`），消息历史保留留档；重复删除 `404`。

`GET /api/v1/conversations/{id}/messages?limit=20&cursor=`（消息历史，按主键倒序）：

```json
{ "Code": 0, "Msg": "查询成功。", "Data": {
  "Items": [
    { "Id": 101, "Role": "user", "Content": "帮我规划周末去杭州", "RunId": 901,
      "CreatedAt": "2026-08-07T09:30:00Z" }
  ],
  "Cursor": null } }
```

`POST /api/v1/conversations/{id}/messages`（发送消息，
请求 `{ "content": "帮我规划周末去杭州", "idempotencyKey": "uuid?" }`）：
- 未绑定专家返回 `422 + 42200`（"该会话尚未绑定专家"）；
- 成功创建关联会话的 Expert Run（复用 `POST /expert-runs` 链路，`inputJson` 由服务端按会话历史拼接，
  客户端**不缓存**会话上下文）；响应 `Data` 为 `{ "RunId": 901, "Status": "queued", "MessageId": 101 }`，
  新建运行 `201`、幂等重放 `200`；
- 客户端轮询 `GET /expert-runs/{RunId}`；终态后由后台处理器自动追加 `assistant` 消息
  （内容为展示安全的结果摘要，`Role=assistant`、`RunId` 可追溯），无需客户端主动写入；
- 重复 `idempotencyKey` 用于其他会话返回 `409`。

### 8.28 自建专家（B21，已发布）

PC 用户端「我的专家」：自建/维护仅创建者本人可见可维护，跨用户/跨租户/已软删一律 404。
权限：`expert.mine.read`（读）/ `expert.mine.write`（写，owner/admin/member）。

`POST /api/v1/experts`（创建，请求
`{ "name": "我的助手", "category": "travel", "description": "...",
"persona": "你是我的旅行助手…", "methodology": "…", "promptTemplate": "…",
"toolPolicyJson": "{\"skills\":[]}", "estimatedCredits": 1 }`）：
- `name/category/persona/promptTemplate` 必填，缺失返回 `422 + 10001`；
- `toolPolicyJson` 非法 JSON 返回 `422`；
- 成功 `201`，`Data` 为 `ExpertDetailView`：`Code` 以 `custom-` 前缀自动生成，
  `Source="mine"`、`Version=1`（v1 已发布版本），列表选择时以 `scope=mine`/`all` 可见。

`PUT /api/v1/experts/{id}`（更新）：
- 请求结构同创建 + `rowVersion`；`RowVersion` 与服务端不一致返回 `409 + 40903`；
- 更新后自动生成 `version+1` 已发布版本（已固定版本的会话/运行不受影响）。

`DELETE /api/v1/experts/{id}`：软删除（`200`）；删除后该专家从目录
（`scope=mine`/`all`）、运行创建与会话发送全部消失；重复删除 `404`。

## 9. 权限汇总

| 接口组 | 策略 |
| --- | --- |
| `GET /api/v1/auth/me`、`POST /api/v1/auth/logout` | `identity.read` |
| `GET /api/v1/experts[...]`（含 `scope=mine` 过滤） | `ai.read` |
| `POST /api/v1/experts`、`PUT/DELETE /api/v1/experts/{id}` | `expert.mine.write`（B21） |
| `POST /api/v1/expert-runs[...]` | `ai.run` |
| `GET /api/v1/skills`、`POST /api/v1/skills`、`PUT/DELETE /api/v1/skills/{id}` | `ai.skills.read` / `ai.skills.write` |
| `GET /api/v1/ai/config` | `ai.config.read` |
| `PUT /api/v1/ai/config` | `ai.config.write` |
| `POST /api/v1/ai/{generate,chat,stream}`（B18 占位） | `ai.run` |
| `GET /api/v1/calendar/events`、`GET /api/v1/calendar/subscriptions` | `calendar.read` |
| `POST/PUT/DELETE /api/v1/calendar/...` | `calendar.write` |
| `GET /api/v1/todos`、`.../subtasks`（读取） | `todo.read` |
| `POST/PUT/DELETE /api/v1/todos[...]` | `todo.write` |
| `GET /api/v1/smart-home/spaces`、`/devices`、`/scenes`、`/devices/health`、`/devices/{id}/health`、`/scenarios/templates`、`/scenarios/instances` | `smart_home.read` |
| `POST /api/v1/smart-home/scenarios/templates/{templateCode}/enable`、`POST /api/v1/smart-home/scenarios/instances/{instanceId}/disable` | `smart_home.write`（B22/B23） |
| `POST /api/v1/smart-home/scenes/{sceneKey}/run`、`POST /api/v1/smart-home/scenarios/instances/{instanceId}/run`、`POST /api/v1/smart-home/scenarios/runs/{runId}/actions/{actionId}/confirm` | `ai.run` |
| `GET /api/v1/connector-providers`、`GET /api/v1/connectors`、`GET /api/v1/connectors/{id}/authorization` | `connector.read` |
| `POST /api/v1/connectors`、`/connectors/{id}/test`、`/connectors/{id}/discovery`、`/connectors/{id}/sync`、`PUT /api/v1/connectors/{id}/authorizations/{memberUserId}` | `connector.write` |
| `POST /api/v1/connector-providers/{providerCode}/authorizations`、`GET/DELETE /api/v1/connector-authorizations/{id}`（B18 个人授权）、`GET /api/v1/connector-authorizations/my`（B19 我的个人连接） | `connector.authorize` |
| `GET /api/v1/homes/{homeId}/members`、`GET /api/v1/homes/{homeId}/invitations`、`GET/PUT /api/v1/web/navigation`（读取） | `tenant.read`（B19） |
| `PUT /api/v1/homes/{homeId}/members/{id}/role`、`PUT .../{id}/status`、`POST /api/v1/homes/{homeId}/owner-transfer`、`POST/DELETE /api/v1/homes/{homeId}/invitations`、`PUT /api/v1/web/navigation`（写入） | `tenant.member.manage`（B19，owner/admin） |
| `GET /api/v1/automation-rules` | `automation.read` |
| `POST/PATCH /api/v1/automation-rules[...]` | `automation.write` |
| `GET /api/v1/expert-files`、`POST /api/v1/expert-files/{fileId}/read-token` | `expert_file.read` |
| `POST /api/v1/expert-files`、`/expert-files/{fileId}/objects`、`DELETE /api/v1/expert-files/{fileId}`、`POST /api/v1/experts/{expertId}/files`、`POST /api/v1/expert-runs/{runId}/files` | `expert_file.write` |
| `GET /api/v1/team-runs/{id}`、`/events`、`/members`、`/synthesis` | `team_run.read` |
| `POST /api/v1/team-runs`、`/cancel`、`/retry` | `team_run.write` |
| `GET /api/v1/homes/{homeId}/members`、`/knowledge`、`/decisions`（读取） | `family.read` |
| `POST/PUT /api/v1/homes/{homeId}/members[...]`、`/knowledge`、`/decisions` | `family.write` |
| `GET /api/v1/homes/{homeId}/activities`、`/activities/{id}` | `steward.activity.read` |
| `GET /api/v1/homes/{homeId}/confirmations` | `confirmation.read` |
| `POST /api/v1/homes/{homeId}/activities/{id}/undo`、`/confirmations/{id}/confirm`、`/confirmations/{id}/deny`、`/confirmations/batch-confirm` | `confirmation.write` |
| `GET/POST/PUT/DELETE /api/v1/life/favorites[...]`、`POST /api/v1/life/favorites/import` | `life.favorite.read` / `life.favorite.write`（B14 预注册，B15 起消费） |
| `GET /api/v1/conversations`、`GET /api/v1/conversations/{id}`、`GET /api/v1/conversations/{id}/messages` | `conversation.read`（B20，仅本人会话） |
| `POST /api/v1/conversations`、`PUT/DELETE /api/v1/conversations/{id}`、`POST /api/v1/conversations/{id}/messages` | `conversation.write`（B20，仅本人会话） |

角色（`owner` / `admin` / `member` / `viewer`）及允许的策略在
`HomeMind.Api/Services/Authorization.cs` 中定义。新增角色或范围时
请在那里调整。

## 10. 类型 / 枚举参考

| 字段 | 允许的值 |
| --- | --- |
| `Todo.status` | `pending`、`in_progress`、`completed` |
| `Todo.type` | `task`、`shopping`、`habit`、`note`（接受自由格式字符串） |
| `Todo.priority` | `p0`–`p3`（接受自由格式字符串） |
| `CalendarEvent.allDay` | 布尔值 |
| `CalendarEvent.repeatRule` | iCalendar RRULE 字符串 |
| `Todo.repeatRule` | iCalendar RRULE 字符串 |
| `AiConfig.enabled`（B18 起） | 布尔值；`false` 时 AI 生成能力（`/ai/{generate,chat,stream}` 与专家运行）整体不可用 |
| `ExpertCatalog.catalogType` | `expert` \| `group` |
| `ExpertCatalog.Source`（B21 起） | `basic`（平台基础）\| `mine`（本人自建） |
| `AgentRun.sourceType` | `expert` \| `group` |
| `AgentRun.conversationId`（B20 起） | 专家会话主键，可空 |
| `ConversationMessage.role` | `user` \| `assistant` |
| `Conversation.expertId` / `expertVersionId` | 同空或同非空（`expertId` 传 null 解绑） |
| `AgentRun.status` | `draft` \| `queued` \| `planning` \| `running` \| `completed` \| `failed` \| `cancelled` |
| `AgentRunAction.actionType` | `plan` \| `todos` \| `calendar_events` \| `smart_home_device` |
| `HousekeeperRun.intent` | `sleep` \| `away` \| `arrive` \| `environment_review` |
| `HousekeeperRunAction.status` | 旧版兼容操作；在其显式确认路由被调用前保持 `pending`。新页面使用 `AgentRunAction`。 |
| `SmartHomeDevice.onlineStatus` | `online` \| `offline` \| `unknown` |
| `FamilyMember.memberStatus`（V2.2 规划） | `active` \| `away` \| `permanently_left` \| `deceased` |
| `FamilyKnowledge.conflictResolutionStrategy`（V2.2 规划） | `latest` \| `authority` \| `majority` |
| `SmartHomeDevice.zigbeeRole`（V2.2 规划） | `end_device` \| `router` \| `coordinator` |
| `SmartHomeDevice.healthStatus`（V2.2 规划） | `healthy` \| `degraded` \| `offline` \| `low_battery` |
| `AutomationRule.triggerType` | `time_schedule` \| `device_state_change` \| `scene_completed` \| `sync_completed` |
| `AutomationRule.approvalPolicy` | `manual_confirmation` \| `auto_execute` |
| `ConnectorSyncJob.status` | `queued` \| `running` \| `completed` \| `failed` |
| `Subscription.platform`（认证） | `h5` \| `android` \| `ios` |
| `TenantMemberSummaryView.role`（B19） | `owner` \| `admin` \| `member` \| `viewer`（固定枚举，不可编辑） |
| `TenantMemberSummaryView.status`（B19） | `active` \| `suspended` |
| `TenantMemberInvitationView.status`（B19） | `pending` \| `accepted` \| `expired` \| `revoked` |
| `TenantMemberInvitationView.subjectKind`（B19） | `phone` |
| `TenantMemberInvitationCreateRequest.proposedRole`（B19） | `admin` \| `member` \| `viewer`（不得为 owner） |
| `WebNavigationRouteView.routeKey`（B19） | `tenant.dashboard` \| `tenant.confirmations` \| `tenant.steward` \| `tenant.knowledge` \| `tenant.family` \| `tenant.life` \| `tenant.connectors` \| `tenant.connector.authorize`（后端静态白名单） |
| `WebNavigationPreferenceUpdateItem.sortOrder`（B19） | 0-1000 整数；值越小越靠前 |

## 10. V2.4 Connector Scope 与个人授权

家庭/个人 Connector 与 Web 治理 API 已发布：客户端以 `bindingScope` 区分 `household` 与 `personal`；家庭实例只显示当前成员授权状态；个人实例只向 owner 显示本人归属和 OAuth 状态（B18）。B19 新增 `GET /connector-authorizations/my` 个人连接汇总、`web/navigation` 导航偏好、成员/邀请/owner 转让受控管理。客户端不得接收、存储或记录 OAuth code、access token、refresh token、`credentialRef` 或 Provider 原始回调。

个人授权流程（B18）：创建授权会话 -> 跳转 Provider -> 服务端 callback -> 查询脱敏状态 -> 本人撤销。角色使用现有 `tenant_members.role` 固定值 `owner/admin/member/viewer`；Web 路由随前端版本发布并按服务端权限码 + 已发布 `route_key` 白名单守卫，不存在客户端维护 API 路由或可编辑角色的接口（B19 导航偏好仅接受已发布 route_key 的显隐/排序）。
