# HomeMind API 精准接入指南

## 1. 事实来源与地址

- OpenAPI：开发环境访问 `http://localhost:5280/swagger`；生产使用实际 API 域名的 `/swagger`。
- 基础路径：`/api/v1`。所有 JSON 请求字段为 `camelCase`；响应封装及其 `Data` 使用当前服务输出的 PascalCase，因此客户端类型必须按实际 Swagger 生成或映射，不能猜测命名。
- 除登录/注册/刷新外，接口默认需要 `Authorization: Bearer <accessToken>`。

```http
Authorization: Bearer eyJ...
Content-Type: application/json
```

```json
{ "Code": 0, "Msg": "ok", "Data": {} }
```

`Code = 0` 表示业务成功；HTTP 状态仍必须先判断。响应包裹层和 `Data` 均使用 PascalCase；请求 JSON 使用 camelCase。常见失败：`401` 令牌无效、`403` 无权限、`404` 资源不可见、`409` 状态/并发冲突、`422` 参数或前置条件不满足、`502/503` 依赖不可用。

## 2. 两端统一请求层

1. 从登录响应保存 access token、refresh token 和设备标识；移动端存入系统安全存储，Web 存入受 XSS 防护约束的会话方案。
2. 每个请求附带 access token。收到一次 `401` 时，单飞调用 `POST /auth/refresh`，成功后仅重放原请求一次；刷新失败则清空会话并回登录页。
3. 解包 `{ Code, Msg, Data }`，将 HTTP 与业务 `Code` 一起传给页面状态层。
4. API 返回的 ID、角色、状态、权限和时间是唯一可信输入；不从本地推断家庭权限或运行结果。

## 3. 端到端必经流程

### 登录与家庭上下文

`POST /auth/register` 或 `POST /auth/login` → 保存会话 → `GET /auth/me` → 以响应的家庭/角色初始化页面。家庭资源的 `{homeId}` 必须使用令牌对应的家庭 ID；禁止用 URL、缓存或用户输入覆盖它。

### CRUD

列表 `GET` → 创建 `POST` 或更新 `PUT` → 以响应 `Data` 替换本地实体 → 失败则保持原状态并提示。删除使用 `DELETE` 后再移除本地项；`404` 视为资源已不可见，重新拉取列表。

### 异步运行与确认

创建 `expert-runs`、`skills/*/runs` 或场景运行 → 保存 `runId` → 轮询 `GET /expert-runs/{id}` 或读取 events → 当响应出现待确认 action 时展示确认卡 → 用户明确确认后调用对应 `confirm` → 继续轮询至终态。不要在客户端把“已提交”显示成“已完成”。

### 家庭管家

先从 `GET /dashboard` 和领域列表拉取事实数据；财务/缴费/宠物/快递/日程接口均带 `{homeId}`。确认类返回的 `confirmationId` 要跳转或刷新确认中心，由用户决定后续动作；产品不自动付款、下单或外呼。财务与缴费的精确路由、CSV 格式、跨端流程和验收见[家庭财务跨端接入](family-finance.md)。

## 4. 接口索引

以下是页面选型索引，不是 DTO 副本。字段、参数、状态码和示例以 Swagger 为准。

| 领域 | 路由前缀/关键接口 | 主要用途 |
| --- | --- | --- |
| 认证 | `auth/register`、`login`、`refresh`、`me`、`logout` | 会话建立与恢复 |
| 仪表盘 | `dashboard` | 家庭概览与快捷入口 |
| 日历/待办 | `calendar/events`、`calendar/subscriptions`、`todos` | 个人效率 CRUD |
| 家庭治理 | `homes/{homeId}/members`、`homes/{homeId}/invitations`、`invitations/accept` | 成员、角色和邀请 |
| 家庭上下文成员 | `homes/{homeId}/family-members` | 家庭资料成员列表 |
| 家庭上下文 | `homes/{homeId}/knowledge`、`decisions`、`schedule/*` | 知识、决策、协同日程 |
| 确认与动态 | `homes/{homeId}/confirmations`、`activities` | 待确认动作和家庭动态 |
| AI | `ai/config`、`experts`、`conversations`、`expert-runs`、`team-runs` | 配置、对话、运行和确认 |
| 技能/媒体 | `skills`、`skills/{skillCode}/runs`、`clipping/*`、`expert-files` | 技能执行、素材与文件 |
| 智能家居 | `smart-home/*`、`automation-rules`、`connectors/*` | 设备、场景、自动化和连接器；`home_assistant` 通过 Vault 凭据连接，客户端只消费归一化视图 |
| 生活与记忆 | `life/favorites`、`memories`、`memory-candidates` | 偏好与记忆治理 |
| 家庭财务 | `homes/{homeId}/finance/*`、`billing/*` | 账单导入、汇总、缴费提醒 |
| 家庭事务 | `courier/*`、`pets/*` | 快递状态、宠物档案/提醒 |

控制器路径位于 `HomeMind.Api/Controllers`；新增路由或字段时先更新 XML 注释并核对 Swagger，再修改本索引。

## 5. 权限、数据与安全

- 权限由后端策略判定，前端只做体验优化。常用范围包括 `calendar.*`、`todo.*`、`family.*`、`finance.*`、`pet.*`、`ai.*`、`connector.*`。
- `homeId` 路由表示家庭边界；个人资源仍按当前用户过滤。拿到别人的 ID 应得到 `404` 或 `403`，客户端不应尝试兜底读取。
- 文件上传、Connector 和媒体接口不得记录或展示凭据、绝对本地路径、Prompt、完整运单号、原始账单和证件资料。
- 需要 UUID 幂等键的请求在用户点击后生成一次并随重试复用；收到 `409` 时重新查询资源，而非盲目重复写入。

## 6. 联调检查表

- Swagger 中可看到路由、认证和请求模型。
- 移动端与 Web 用同一账号、同一家庭完成流程；再用低权限角色验证 `403`。
- 对每个页面验证正常、空数据、422、401 刷新、网络/5xx 和重复点击。
- 对运行/确认类流程验证状态从创建、待确认到完成或失败，且刷新页面后能够恢复。
