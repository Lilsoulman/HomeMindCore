# 本地 API 实现

API 通过 `HomeMind.Api/appsettings.json` 连接本地 MySQL：

```text
Server=localhost;Database=nexus_mind;User=root;Password=123456;SslMode=None;AllowPublicKeyRetrieval=True;
```

这仅是本地开发配置。在任何共享部署之前，请将连接字符串和
`Auth:SigningKey` 迁移到环境变量或密钥存储中。

`Auth` 段还声明了访问令牌有效期（默认 15 分钟）和刷新令牌有效期
（默认 30 天），同样应通过环境变量或密钥存储在生产环境中配置。

## 已实现的路由

所有受保护路由位于 `/api/v1` 之下，需要 `Authorization: Bearer <accessToken>`。
服务端从访问令牌中派生 `user_id`、`tenant_id` 与 `role`，绝不信任
JSON 输入中的这些值。

| 模块 | 路由 |
| --- | --- |
| 认证 | `POST /api/v1/auth/register`、`/login`、`/refresh`、`/logout`、`/wechat/exchange`（未实现）；`GET /api/v1/auth/me` |
| 待办 | `GET/POST /api/v1/todos`；`PUT/DELETE /api/v1/todos/{id}`；`POST /api/v1/todos/{id}/subtasks`、`PUT/DELETE /api/v1/todos/{id}/subtasks/{subId}` |
| 日历 | `GET/POST /api/v1/calendar/events`、`PUT/DELETE /api/v1/calendar/events/{id}`；`GET/POST /api/v1/calendar/subscriptions`、`PUT/DELETE /api/v1/calendar/subscriptions/{id}`；`POST /api/v1/calendar/ical/fetch`（未实现） |
| 技能 | `GET/POST /api/v1/skills`；`PUT/DELETE /api/v1/skills/{id}`；Skill 独立运行（B24/B25）：`POST /api/v1/skills/{skillCode}/runs`、`POST /api/v1/skills/runs/{runId}/actions/{actionId}/confirm`（`ai.run` + `media.read`，SourceType=skill，不绑定专家） |
| AI 配置 | `GET/PUT /api/v1/ai/config`（B18 新增 `enabled` 字段，默认 `true`，切换开关不传 `apiKey` 即可保留密文）；`POST /api/v1/ai/{generate,chat,stream}`（B18 占位，启用 → 501，未启用 → 422 + `Code=42200`） |
| 专家目录 | `GET /api/v1/experts?scope=basic\|mine\|all`（B21 起支持来源过滤，默认 basic 向后兼容；列表项含 `Source` 字段，不暴露他人 owner）、`GET /api/v1/experts/{id}`（他人自建/已软删 404）；自建专家（B21）：`POST /api/v1/experts`、`PUT/DELETE /api/v1/experts/{id}`（`expert.mine.write`，PUT 携带 RowVersion 乐观锁 409/40903，更新生成 version+1 已发布版本） |
| 智能体运行时 / 专家运行 | `POST /api/v1/expert-runs`、`GET /api/v1/expert-runs/{id}`、`/events`、`/actions`、`/actions/{actionId}/confirm`、`/cancel`、`/retry`；`POST /api/v1/expert-runs/{id}/actions` 创建动作。路由名称为兼容性保留，但领域资源为 `AgentRun`。 |
| 专家文件（V1） | `POST/GET /api/v1/expert-files`、`POST /api/v1/expert-files/{fileId}/objects`、`DELETE /api/v1/expert-files/{fileId}`、`POST /api/v1/expert-files/{fileId}/read-token`、`POST /api/v1/experts/{expertId}/files`、`POST /api/v1/expert-runs/{runId}/files` |
| 团队运行（V1） | `POST /api/v1/team-runs`；`GET /api/v1/team-runs/{id}`、`/events`、`/members`、`/synthesis`；`POST /api/v1/team-runs/{id}/cancel`、`/retry` |
| 家庭管家（兼容） | `POST /api/v1/housekeeper-runs` 仅作为智能体运行时的兼容入口保留 |
| 智能家居 | `GET /api/v1/smart-home/spaces`、`/devices?spaceId=`、`/scenes`、`/devices/health?spaceId=`；`POST /api/v1/smart-home/scenes/{sceneKey}/run` 创建需确认的场景运行动作（B22 起为兼容代理：懒启用场景模板实例并转调场景运行链路）；归一化的设备发现/状态同步通过连接器路由完成 |
| 场景工作流（B22） | `GET /api/v1/smart-home/scenarios/templates`、`GET /api/v1/smart-home/scenarios/instances`（`smart_home.read`）；`POST /api/v1/smart-home/scenarios/templates/{templateCode}/enable`、`POST /api/v1/smart-home/scenarios/instances/{instanceId}/disable`（`smart_home.write`）；`POST /api/v1/smart-home/scenarios/instances/{instanceId}/run`、`POST /api/v1/smart-home/scenarios/runs/{runId}/actions/{actionId}/confirm`（`ai.run`）。平台模板 → 家庭实例 → 单场景动作运行；确认后逐步执行设备命令并按 success/partial/failed 汇总 |
| 家庭上下文 | `GET/POST /api/v1/homes/{homeId}/members`、`PUT /api/v1/homes/{homeId}/members/{id}`、`POST /api/v1/homes/{homeId}/members/{id}/correction`；`GET/POST /api/v1/homes/{homeId}/knowledge?category=`、`DELETE /api/v1/homes/{homeId}/knowledge/{id}`；`GET/POST /api/v1/homes/{homeId}/decisions`。所有 homeId 由 `RequireHomeOwner` 校验等于 JWT tenant_id。终态更正写 `family_audit_logs`；知识同 key 冲突按 latest/authority/majority 留痕 |
| 管家协同 | 管家动态：`GET /api/v1/homes/{homeId}/activities?limit=&cursor=`、`GET /api/v1/homes/{homeId}/activities/{id}`、`POST /api/v1/homes/{homeId}/activities/{id}/undo`；确认中心：`GET /api/v1/homes/{homeId}/confirmations?riskLevel=&status=`、`POST /api/v1/homes/{homeId}/confirmations/{id}/confirm`、`POST /api/v1/homes/{homeId}/confirmations/{id}/deny`、`POST /api/v1/homes/{homeId}/confirmations/batch-confirm`。只读用 `smart_home.read`，写操作用 `ai.run`；确认/拒绝/批量确认/撤销写 `family_audit_logs` 并生成管家动态 |
| 仪表板 | `GET /api/v1/dashboard` 聚合可独立降级的家、待确认事项、管家动态、场景、今日计划和最新建议模块；`homeSummary` 对应 `Home` 模块，`quickActions` 为前端静态入口 |
| 连接器 | 家庭级：`GET /api/v1/connector-providers`，`GET/POST /api/v1/connectors`（`bindingScope` 支持 household/personal），`POST /api/v1/connectors/{id}/test`、`/discovery`、`/sync`，`GET /api/v1/connectors/sync-jobs/{jobId}`，`GET /api/v1/connectors/{id}/authorization`、`PUT /api/v1/connectors/{id}/authorizations/{memberUserId}`；个人 OAuth（B18）：`POST /api/v1/connector-providers/{providerCode}/authorizations`、`GET/DELETE /api/v1/connector-authorizations/{id}`、服务端 callback `GET /api/v1/connector-providers/{providerCode}/callback`、Mock 授权页 `GET /api/v1/connector-providers/{providerCode}/authorize`（匿名） |
| 自动化 | `GET/POST/PATCH /api/v1/automation-rules` 管理租户隔离的、已授权的长期运行规则 |
| 成员受控管理（B19） | `GET /api/v1/homes/{homeId}/members`（`tenant.read`）；`PUT /api/v1/homes/{homeId}/members/{memberUserId}/role`、`PUT .../{memberUserId}/status`、`POST /api/v1/homes/{homeId}/owner-transfer`（`tenant.member.manage`，owner/admin） |
| 成员邀请（B19） | `GET /api/v1/homes/{homeId}/invitations?status=`（`tenant.read`）；`POST /api/v1/homes/{homeId}/invitations`、`DELETE /api/v1/homes/{homeId}/invitations/{invitationId}`（`tenant.member.manage`）；`POST /api/v1/invitations/accept`（`tenant.read`，受邀人接受，家庭由邀请记录推导） |
| Web 导航偏好（B19） | `GET /api/v1/web/navigation`（`tenant.read`，白名单合并当前角色偏好）；`PUT /api/v1/web/navigation`（`tenant.member.manage`，仅接受已发布 route_key） |
| 我的个人连接（B19） | `GET /api/v1/connector-authorizations/my`（`connector.authorize`，仅本人 personal 实例 + 最近授权会话状态） |
| 专家会话（B20） | `GET/POST /api/v1/conversations`（`conversation.read/write`，仅本人会话，跨用户/跨租户 404）；`GET/PUT/DELETE /api/v1/conversations/{id}`（PUT 携带 RowVersion 乐观锁，409/40903）；`GET /api/v1/conversations/{id}/messages?limit=&cursor=`（游标分页）；`POST /api/v1/conversations/{id}/messages`（发送 → 创建关联会话的 Expert Run → 落 user 消息，响应 `{RunId,Status,MessageId}`，终态由后台追加 assistant 消息） |

## 响应与错误码

每个 JSON 端点都返回以下统一响应。`Code` 是应用层错误码，而 HTTP
状态码表示协议层的结果。客户端不得将 `Code` 与 HTTP 状态码进行比较。

```json
{
  "Code": 20000,
  "Msg": "手机号或密码错误。",
  "Data": null
}
```

成功响应始终使用 `Code: 0`，包括 `200`、`201` 和 `202` 响应。
非成功响应的 `Data: null`，并使用以下错误码系列
（`HomeMind.Common.Model/ViewModel/Common/ApiErrorCodes.cs`）：

| 错误码 | 含义 | 常用 HTTP 状态 |
| --- | --- | --- |
| `10000` | 请求无效 | `400`、`405` |
| `10001` | 请求参数验证失败 | `422` |
| `42200` | 业务前置条件未满足（如 AI 配置已禁用） | `422` |
| `20000` | 登录凭据无效 | `400` |
| `20001` | 访问令牌缺失、无效、过期或已被吊销 | `401` |
| `20002` | 刷新令牌无效、过期或已被吊销 | `401` |
| `20003` | 已认证调用方缺少权限 | `403` |
| `30000` | 所请求资源对调用方不可用 | `404` |
| `40000` | 请求与当前资源状态冲突 | `409` |
| `50000` | 端点被刻意留为未实现 | `501` |
| `50001` | 所需依赖不可用 | `503` |
| `50002` | 上游依赖失败或超时 | `502`、`504` |
| `90000` | 未预期的服务器错误 | `500` |
| `30001` | 家庭成员邀请的受邀手机号不匹配当前账户已验证标识（B19） | `404` |
| `42201` | 家庭 owner 转让目标处于 suspended/away（B19） | `422` |
| `42202` | 家庭成员角色变更直接置 owner 已被拒（B19），请使用 owner-transfer | `422` |
| `42203` | Web 导航偏好提交了未发布的 route_key（B19） | `422` |
| `40901` | 家庭租户级乐观锁冲突（成员/邀请行版本不匹配，B19） | `409` |
| `40902` | 家庭成员邀请的受邀标识在当前家庭已存在未结邀请（B19） | `409` |
| `40903` | 个人资源（专家会话/自建专家）乐观锁冲突（B20/B21） | `409` |

当手机号和密码不匹配时，`POST /api/v1/auth/login` 返回 HTTP `400`
并附带 `Code: 20000`。这是交互式凭据错误，而非访问令牌过期，
因此客户端必须显示登录错误，不得触发令牌刷新或登出流程。
对于无效的刷新令牌，`POST /api/v1/auth/refresh` 返回 HTTP `401`
并附带 `Code: 20002`。对于无效的访问令牌，受保护路由返回
HTTP `401` 并附带 `Code: 20001`。`POST /api/v1/auth/logout`
同时撤销当前访问令牌和该设备的刷新令牌。

新的 API 业务失败必须使用 `ApiErrorCodes` 的具名值。`ServiceResult`
和 `AuthenticationResult` 暴露 `Code`；控制器必须将该值写入响应
统一格式，而不是复制 `StatusCode`。对于无法使用现有具名错误码
的新错误，请在同一变更中添加错误码和本表。

`StatusCodePages` 中间件会将未由控制器显式处理的协议层状态码
（400/401/403/404/405）映射为对应错误码与可读消息；控制器抛出的
未捕获异常会进入 `UseExceptionHandler`：MySql 异常归类为
`50001`，其余归类为 `90000`。

智能家居读路由仅返回归一化的空间、设备、场景与设备健康数据。
它们不返回连接器凭据、厂商实体 ID、协议字段或原始设备状态。
除非明确配置 `SmartHome:MockEnabled=true`，否则本地演示数据
默认禁用。

## 鉴权策略

`PermissionNames`（位于 `HomeMind.Api/Services/Authorization.cs`）
定义细粒度权限，并通过 `PermissionAuthorizationHandler` 校验角色：

| 权限 | 含义 | 可访问角色 |
| --- | --- | --- |
| `identity.read` | 当前用户与认证信息 | owner / admin / member / viewer |
| `ai.read` | 专家目录与运行查询 | owner / admin / member / viewer |
| `ai.run` | 创建与确认 AgentRun、确认动作、运行场景 | owner / admin / member |
| `ai.skills.read` / `ai.skills.write` | 技能目录读写 | owner / admin / member（读全员，写 owner/admin/member） |
| `ai.config.read` / `ai.config.write` | AI 调用配置读写（endpoint/模型/温度/启用开关/密钥） | owner / admin / member（读全员，写 owner/admin/member） |
| `calendar.read` / `calendar.write` | 日历事件与订阅 | owner / admin / member / viewer（仅 owner/admin/member 写） |
| `todo.read` / `todo.write` | 待办与子任务 | owner / admin / member / viewer（仅 owner/admin/member 写） |
| `smart_home.read` | 空间 / 设备 / 场景 / 设备健康（聚合摘要与单设备健康详情）/ 场景模板与实例 | owner / admin / member / viewer |
| `smart_home.write` | 启用/禁用场景模板实例（B22/B23） | owner / admin / member |
| `family.read` | 家庭成员 / 家庭知识库 / 决策历史只读（B14 收敛） | owner / admin / member / viewer |
| `family.write` | 成员变更、终态更正、知识写入与删除、决策记录（B14 收敛） | owner / admin / member |
| `steward.activity.read` | 管家动态列表与详情（B14 收敛） | owner / admin / member / viewer |
| `confirmation.read` | 确认中心列表（B14 收敛） | owner / admin / member / viewer |
| `confirmation.write` | 单项确认 / 拒绝 / L1 批量确认 / 动态撤销（B14 收敛） | owner / admin / member |
| `life.favorite.read` / `life.favorite.write` | 个人偏好收藏读/写（B14 预注册，B15 起消费） | owner / admin / member / viewer（仅 owner/admin/member 写） |
| `connector.read` | 连接器目录、实例与同步任务 | owner / admin / member / viewer |
| `connector.write` | 创建连接器、连通性测试、发现、同步、成员授权 | owner / admin / member |
| `connector.authorize` | 个人 OAuth 授权发起 / 状态查询 / 撤销（B18）；本人个人连接汇总（B19） | owner / admin / member |
| `tenant.read` | 家庭成员列表、邀请列表、Web 导航偏好读取（B19） | owner / admin / member / viewer |
| `tenant.member.manage` | 成员角色变更 / 状态停启 / owner 转让 / 邀请创建与撤销 / Web 导航偏好写入（B19，owner/admin 专享） | owner / admin |
| `automation.read` | 自动化规则查询 | owner / admin / member / viewer |
| `automation.write` | 自动化规则创建与修改 | owner / admin / member |
| `expert_file.read` / `expert_file.write` | 专家文件读/写 | owner / admin / member / viewer（仅 owner/admin/member 写） |
| `team_run.read` / `team_run.write` | 团队运行读/写 | owner / admin / member（写）、全员（读） |
| `team.manage` | 团队管理预留策略（保留） | — |
| `conversation.read` / `conversation.write` | 专家会话与消息读/写（B20，仅本人会话） | owner / admin / member / viewer（仅 owner/admin/member 写） |
| `expert.mine.read` / `expert.mine.write` | 用户自建专家读/写（B20 预注册，B21 起消费；`scope=mine` 与自建专家 CRUD 仅作用于本人资源） | owner / admin / member / viewer（仅 owner/admin/member 写） |
| `media.read` | Skill 运行发起前置（B24，读取素材目录与产物登记） | owner / admin / member |

`member` 角色的 `ConnectorWrite` 需 `owner` 或 `admin` 才能创建或
修改连接器；当前实现通过控制器分支 `user.Role is "owner" or "admin"`
控制 `ListConnectors` 的可见范围。

## 仪表板与场景快捷方式

`GET /api/v1/dashboard` 需要 `smart_home.read` 权限，并从访问令牌
派生用户和租户。它返回独立的 `Home`、`Scenes`、`Todos`、`Calendar`
和 `Suggestion` 模块。每个模块都声明 `available` 或 `unavailable`
状态及其时间戳和可读消息，因此一个模块失败不会阻塞仪表板的
其他部分。

`POST /api/v1/smart-home/scenes/{sceneKey}/run` 需要 `ai.run` 权限。
支持的快捷方式为 `arrive_home`、`leave_home` 和 `sleep`；B22 起该
路由为兼容代理：校验场景键后**懒启用**对应场景模板实例并转调
场景运行链路（`scene_completed` 事件发布保留，`automation_rules`
动作引用零改动）。结果是一个无凭据的运行，包含 `pending` 的场景
动作，且现有的确认、授权、幂等性、适配器调度和审计规则仍然
必须遵守。

### B22 场景工作流

平台模板 → 家庭实例 → Run 执行的配置化场景体系；执行、确认、
幂等与审计全部复用 `AgentRun` / `ExpertRunAction` /
`ActionExecutionAudits` 链路，不新增执行引擎与步骤表。

- `GET /api/v1/smart-home/scenarios/templates`（`smart_home.read`）：
  平台模板列表（`Code`/`Name`/`Summary`/`Status`/`Steps`，步骤未
  解析设备：`device_type`/`room`/`capability`/`value`/`optional`）。
- `GET /api/v1/smart-home/scenarios/instances`（`smart_home.read`）：
  家庭实例列表；步骤已解析到 `deviceId`，缺设备步骤
  `stepStatus=unavailable` 且携带 `reason`，执行时跳过。
- `POST /api/v1/smart-home/scenarios/templates/{templateCode}/enable`
  （`smart_home.write`）：按 `device_type + room + capability` 匹配
  家庭设备生成实例；**缺设备不阻塞启用**；重复启用返回既有实例，
  已禁用实例重复启用时恢复为 `enabled`。
- `POST /api/v1/smart-home/scenarios/instances/{instanceId}/disable`
  （`smart_home.write`）：实例状态置为 `disabled`，不再允许触发新
  运行；重复禁用幂等。禁用只阻止新触发，已创建的待确认运行不受
  影响；实例不存在、跨租户或已软删除返回 404。
- `POST /api/v1/smart-home/scenarios/instances/{instanceId}/run`
  （`ai.run`）：创建单个 `scenario` 类型待确认动作，步骤上下文
  承载于动作 RequestJson（`scenario_id`/`scenario_name`/`steps`）；
  响应含 `Actions`（`Title`/`Description`/`RiskLevel`，风险取步骤
  最大值：lock/camera/security 类 L3，其余 L1）。
- `POST /api/v1/smart-home/scenarios/runs/{runId}/actions/{actionId}/confirm`
  （`ai.run`）：确认后逐步下发设备命令；**required 步骤失败后继续
  执行后续步骤**。执行结果汇总规则：全部失败 → `failed`；required
  有失败且存在成功 → `partial`；仅 optional 失败或全部成功 →
  `success`。`run.Result` 输出 `{scenario, status, summary,
  success_count, failed_count, failed_steps:[{name, reason}]}`；
  消费方（Push/Dashboard/反馈）只读 `summary`/`status`/
  `failed_steps`，**禁止解析 steps 明细 JSON**。幂等键重复确认
  重放首次结果，不重复执行设备命令。

`GET /api/v1/smart-home/devices/health` 同样需要 `smart_home.read`
权限，按家庭/空间聚合 `healthy / degraded / offline / low_battery`
四个计数与主导状态。设备列表与该接口同时返回归一化的
`zigbeeRole`、`batteryLevel`、`signalLqi` 与 `healthStatus` 字段，
绝不返回原始协议或厂商字段。

### B14 单设备健康详情

`GET /api/v1/smart-home/devices/{deviceId}/health`（权限
`smart_home.read`）返回单台设备的标准化健康详情：

```json
{
  "Id": 201,
  "SpaceId": 101,
  "Name": "卧室空调",
  "DeviceType": "air_conditioner",
  "OnlineStatus": "online",
  "ZigbeeRole": "router",
  "BatteryLevel": 15,
  "SignalLqi": 90,
  "HealthStatus": "low_battery",
  "StateUpdatedAt": "2026-08-05T10:00:00Z"
}
```

跨家庭或不存在返回 `404`；`StateUpdatedAt` 为最近状态采样时间，
过期状态不得描述为实时。健康派生顺序：`offline` > `low_battery`
> `degraded` > `healthy`。

创建连接器仅接受租户拥有的 `credentialRef`，格式为
`vault://tenants/{tenantId}/...`。该引用永远不会被 API 返回。
`SecretVault:Enabled` 默认值为 `false`，因此在提供 Vault 配置之前，
创建请求会返回可读的 `503` 配置错误。运行时高可用访问使用
位于 `SecretVault:Endpoint` 的 HashiCorp Vault；其访问令牌仅来自
`SecretVault:TokenEnvironmentVariable` 指定的环境变量，高可用凭据
从租户拥有的 `credentialRef` 读取。这两个令牌都不会被本 API
存储或返回。

## 自动化与可靠同步

`GET/POST/PATCH /api/v1/automation-rules` 使用 `automation.read` 和
`automation.write` 策略。仅所有者和管理员可以创建或修改规则；
租户始终从访问令牌派生。规则具有以下四种触发类型之一：
`time_schedule`、`device_state_change`、`scene_completed` 或
`sync_completed`。时间调度器接受 `fixed_time`、`sun`（日出/日落，
可选择经纬度和偏移量），以及一次性 `countdown`（UTC 时间的
`fireAt`）。设备状态规则由归一化的连接器状态变更提供；同一
内部事件服务是 MQTT 订阅的适配器集成点，而 REST 轮询继续作为
回退方案。

规则动作被刻意限制为内置场景键。它们创建正常的无凭据管家运行
和已审计的设备动作。`manual_confirmation` 将动作保持为待处理。
`auto_execute` 在规则所有者的有效授权下调用相同的授权、幂等性、
适配器、审计和状态写入工作流；它绝不会绕过命令边界。
规则更新需要返回的 `rowVersion`。

`POST /api/v1/connectors/{id}/sync` 返回 `202` 和一个同步任务视图。
工作被持久化到 `connector_sync_jobs`，然后通过 `AutomationWorker`
消费的进程内 Channel 进行信号通知。工作最多三次尝试，使用
30 秒操作超时和指数重试延迟。定期扫描在进程重启后恢复工作。
结构化的 worker 日志和 `HomeMind.Automation` 指标暴露规则触发、
同步入队、同步重试和同步失败计数器，且不包含凭据或厂商标识。

## 智能体运行时与兼容性工作流

每个新的 AI 工作流都作为 `AgentRun` 创建和管理。持久化的数据表
仍为 `expert_runs`，仅用于保留现有的外键和客户端路由兼容性。
`AgentRun` 严格具有以下状态：

```text
draft | queued | planning | running | completed | failed | cancelled
```

`POST /api/v1/expert-runs` 解析请求的已发布专家或专家组策略，
存储一个 AgentRun，创建一个 `expert_jobs` 队列项并返回 `queued`。
模型调用不会在 API 请求线程中运行。专家定义角色、提示、允许
的技能和权限；它从不调度外部命令。技能是执行边界，连接器是
通往外部系统的唯一网关。

`POST /api/v1/expert-runs/{runId}/actions` 显式创建待确认动作；
`POST /api/v1/expert-runs/{runId}/actions/{actionId}/confirm`
在权限、版本与幂等校验通过后通过适配器调度命令。

`POST /api/v1/housekeeper-runs` 及其确认路由仅作为智能体运行时
之前的智能家居兼容性工作流保留。新的 Flutter 功能必须改为
创建 AgentRun。智能家居仍然是连接器领域：当前的前端契约是
归一化的读取数据和受控的动作草稿，而 Home Assistant、MQTT、
Zigbee 和 Matter 是连接器/适配器实现，而非核心业务逻辑。

`POST /api/v1/housekeeper-runs` 需要 `ai.run` 权限。访问令牌
决定用户和租户；调用方不能提供这两个值。它接受有限的、可审计
的意图，并可选择将分析范围缩小到一个租户拥有的空间：

```json
{
  "intent": "sleep",
  "spaceId": 12,
  "idempotencyKey": "9c1f9a71-6d38-4e6a-b1c2-7ef6cf16d6d3"
}
```

允许的意图为 `sleep`、`away`、`arrive` 和 `environment_review`。
服务仅读取归一化的空间、在线设备和可写能力；它从不读取连接器
凭据、厂商实体 ID 或协议字段。响应包含显示安全的事件和动作
草稿。设备草稿仅包含 `Id`、`ActionType`（`smart_home_device`）、
`Status`（`pending`）、标题、描述、规范化的设备 ID/名称、能力
和目标值。它不执行命令。

`GET /api/v1/expert-runs/{id}/actions` 为当前租户中的当前用户
返回相同的、显示安全的运行/事件/动作视图。跨租户或他人的运行
返回 `404`。

`POST /api/v1/expert-runs/{runId}/actions/{actionId}/confirm` 需要
`ai.run` 权限，并接受必需的 UUID `idempotencyKey`。它在通过
提供商适配器调度规范化的 `DeviceCommand` 之前，会重新检查
调用方的连接器范围、连接健康状态、设备所有权/在线状态以及
可写能力。响应仅包含动作 ID、执行状态、可读消息和时间戳。
凭据、厂商实体 ID 和协议字段绝不离开适配器。每次尝试的调度
都有一条无凭据的审计记录；成功的调度会写入归一化的设备状态
快照。

## V2.2 管家协同与确认中心（已发布）

以下路由已在 B11/B12 发布。成员生命周期 `active`、`away`、
`permanently_left`、`deceased` 与知识三策略冲突解决、设备健康
字段随 B11/B10 发布，见各自小节；本节记录管家动态与确认中心。

### 管家动态

| 路由 | 权限 | 说明 |
| --- | --- | --- |
| `GET /api/v1/homes/{homeId}/activities?limit=&cursor=` | `steward.activity.read` | 游标分页（created_at+id 倒序，limit 上限 50）；响应 `{ Items, Cursor }` |
| `GET /api/v1/homes/{homeId}/activities/{id}` | `steward.activity.read` | 详情；跨家庭或不存在返回 `404` |
| `POST /api/v1/homes/{homeId}/activities/{id}/undo` | `confirmation.write` | 撤销；仅接受 `undoable=true` 且 `completed` 的活动，实时复验资源状态并写 `activity_undo` 审计 |

撤销校验顺序：非已完成 → `422`；不可撤销 → `422`；已撤销 → `409`。
撤销成功置位 `undoneAt` 并置 `undoable=false`。撤销为本地状态迁移
（`AgentRun` 无连接器字段，当前不调用 Adapter）；逆向命令执行与
Adapter 健康复验随运行期撤销管线（B13/B14）落地。

### 确认中心

| 路由 | 权限 | 说明 |
| --- | --- | --- |
| `GET /api/v1/homes/{homeId}/confirmations?riskLevel=&status=` | `confirmation.read` | 列表过滤；riskLevel 仅 L1/L2/L3，status 为五个状态常量；非法参数 `422`；过期项按计算语义不返回 |
| `POST /api/v1/homes/{homeId}/confirmations/{id}/confirm` | `confirmation.write` | 单项确认（L2/L3 逐项，L1 亦可）；请求体 `{ "idempotencyKey": "uuid" }`；已确认 → `200` 重放现有视图；已终态/过期 → `409` |
| `POST /api/v1/homes/{homeId}/confirmations/{id}/deny` | `confirmation.write` | 拒绝；请求体 `{ "reason": "1-512 字符" }` 必填；已拒绝 → `200` 重放；已确认/终态/过期 → `409` |
| `POST /api/v1/homes/{homeId}/confirmations/batch-confirm` | `confirmation.write` | L1 批量确认；请求体见下；幂等键重放 |

单项确认/拒绝写入 `family_audit_logs`（`confirmation_confirm` /
`confirmation_deny`）并生成可展示的管家动态；关联的 `pending`
活动随之转为 `confirmed`（确认）或 `cancelled`（拒绝）。

`POST /api/v1/homes/{homeId}/confirmations/batch-confirm` 请求体：

```json
{
  "confirmationIds": [101, 102, 103],
  "idempotencyKey": "bc20666d-1639-420f-94d4-f5acb45762e1"
}
```

服务在单事务内预验证每个 ID 属于 `homeId`、处于 `pending` 状态、
未过期、`riskLevel: "L1"` 且无重复 ID 后原子确认，并返回每项确认
结果（`{ ConfirmedCount, Items }`）。重复的幂等键只返回首次记录的
结果（`confirmation_batch_records` 持久化，016 迁移），绝不重复
下游效果；同键不同 ID 集合返回 `409`。

| 场景 | HTTP | Code |
| --- | --- | --- |
| 键非 UUID / 空列表 / 重复 ID / 超 50 项 | `422` | `10001` |
| 任一 ID 不在当前家庭作用域（含跨家庭） | `404` | `30000` |
| 任一 L2/L3、非 pending、已终态、已过期；同键异集 | `409` | `40000` |
| 同键同集重放 | `200` | `0` |

### 仪表板 V2.2 聚合

`GET /api/v1/dashboard` 新增 `pendingConfirmations`（未过期待确认
事项，按到期升序前 6 条）与 `stewardActivities`（最近 6 条动态）
模块，与其他模块一样可独立降级（`available`/`unavailable`）；
`homeSummary` 对应既有 `Home` 模块；`quickActions` 为前端静态快捷
入口，不经过后端。

### 推送聚合约束（契约，无实现代码）

推送投递使用显示安全的活动和确认记录。它可以为同一资源、同一
场景/运行、30 分钟的低风险窗口或早/晚摘要合并重复事件。L2/L3、
安全风险和直接成员提及会绕过低风险聚合。当前仓库无推送服务
实现代码，此约束在推送服务落地时执行。

## V2.3 个人偏好收藏（已发布，B15）

以下路由已在 B15 发布。家庭归属一律由 JWT 推导，客户端不得指定
home_id；跨家庭与越权访问一律返回 `404`。

| 路由 | 权限 | 说明 |
| --- | --- | --- |
| `GET /api/v1/life/favorites?category=&visibility=` | `life.favorite.read` | 列表；category 仅 restaurant/travel/material，visibility 仅 private/family，非法 `422`；private 项仅归属成员本人可见 |
| `GET /api/v1/life/favorites/{id}` | `life.favorite.read` | 详情；不存在或不可见统一 `404` |
| `POST /api/v1/life/favorites` | `life.favorite.write` | 创建；请求体 `{ category, name, detailJson?, visibility?, ownerMemberId? }`，ownerMemberId 为空时默认解析为当前成员；写 `favorite_create` 审计 |
| `PUT /api/v1/life/favorites/{id}` | `life.favorite.write` | 更新；仅归属成员本人或家庭管理员（owner/admin），否则 `403`；写 `favorite_update` 审计 |
| `DELETE /api/v1/life/favorites/{id}` | `life.favorite.write` | 软删除；权限同上；写 `favorite_delete` 审计 |
| `POST /api/v1/life/favorites/import` | `life.favorite.write` | 对话导入；请求体 `{ category, name, detailJson?, visibility?, source?, conversationText? }`；来源留痕入审计原因；写 `favorite_import` 审计。AI 对话提取部分依赖 AI 运行时，按部署环境验证 |

`detailJson` 为结构化扩展信息 JSON（建议字段：`cuisine`、`address`、
`lat`、`lng`、`tags`、`note`、`source`）。`017` 迁移同时扩展
`family_audit_logs` 的 action/target_type CHECK（`favorite_create` /
`favorite_update` / `favorite_delete` / `favorite_import` 与
`personal_favorite`），C# 侧审计白名单与之同步。

## V2.3 个人生活专家翻牌（已发布，B16）

`POST /api/v1/experts/personal-life-expert/runs`（权限 `ai.run`，
`personal-life-expert` 由 `017` 迁移注册，category=`life`，version=1，
Skill 声明 `favorite.recommend`/`trip.plan`/`favorite.create`）：

请求体 `{ "intent": "recommend", "inputJson": "{...}", "idempotencyKey"? }`。
`intent` 仅限 `recommend`（`plan` 行程规划随后续版本开放，返回 `422`）；
`inputJson` 为合法 JSON，支持 `time`（morning/noon/evening）、`location`、
`taste`。服务按口味（tags/cuisine）、位置（address）与时段确定性评分，
返回 Top1-2 建议与理由；无匹配时以私藏店铺库兜底。翻牌为只读 L1，
不生成动作。错误码：专家未初始化（017 未应用）→ `503`；输入非法 → `422`；
重复幂等键 → `200` 重放；同键异源 → `409`。

响应 `Data`：

```json
{
  "Id": 9001,
  "Status": "completed",
  "ResultSummary": "为你推荐 2 家店铺。",
  "CreatedAt": "2026-08-06T02:00:00Z",
  "FinishedAt": "2026-08-06T02:00:01Z",
  "Events": [
    { "Sequence": 1, "Type": "running", "Message": "正在检索个人偏好收藏。", "CreatedAt": "..." },
    { "Sequence": 2, "Type": "recommendations_ready", "Message": "已筛选 2 家候选店铺。", "CreatedAt": "..." },
    { "Sequence": 3, "Type": "completed", "Message": "为你推荐 2 家店铺。", "CreatedAt": "..." }
  ],
  "Recommendations": [
    { "FavoriteId": 501, "Name": "老王面馆", "Reason": "口味匹配“面”，位置匹配“城西”", "Tags": ["面", "晚餐"] }
  ]
}
```

不返回提示、模型思考链、凭据或供应商字段。

## V2.3 行程规划与日历同步（已发布，B17）

`POST /api/v1/experts/personal-life-expert/runs`（权限 `ai.run`）
`intent=plan`：请求体 `{ "intent": "plan", "inputJson": "{...}", "idempotencyKey"? }`；
`inputJson` 支持 `destination`（1-64 字符，必填）与 `days`（1-7，默认 1）。
服务结合私藏库（travel/restaurant）与确定性 Mock 天气（晴/阴/雨轮换）
生成每日上午/下午/晚上安排，产出 1 个 `calendar_create_event` Run Action
（L1，`pending`），run 进入 `pending_actions`。输入非法 → `422`。

`POST /api/v1/experts/personal-life-expert/runs/{runId}/actions/{actionId}/confirm`
（权限 `ai.run`）：请求体 `{ "idempotencyKey": "uuid" }`（必填，仅校验 UUID
格式）。确认后按天经既有日历服务创建事件（标题 `{目的地} 行程 D{n}`，
每日一个事件）；重复幂等键返回首次结果（`ActionExecutionAudits` 重放，
`018` 迁移放宽该表连接器/设备两列为 NULL 以承载无设备动作），同一动作
已终态返回 `409`。动作状态 `pending → executing → executed/failed`；
失败返回 `502`。不返回提示、思考链、凭据或供应商字段。

## 刻意关闭的路由

- `POST /api/v1/auth/wechat/exchange` 在提供微信渠道配置
  （AppId、密钥、重定向回调和服务端交换）之前返回 `501`。
  它不伪造微信身份。
- `POST /api/v1/calendar/ical/fetch` 在配置 SSRF 白名单策略之前
  返回 `501`。

## 专家文件（V1）

`POST /api/v1/expert-files` 需要 `expert_file.write` 权限。请求
仅声明元数据（`name`、`mimeType`、`sizeBytes`、`sha256`，
可选的 `quotaBytes` 和 `idempotencyKey`）；文件二进制必须通过
返回的短期 `uploadUrl` 单独上传。响应包括 `fileId`、`status`
（成功时始终为 `pending_upload`）、`uploadToken`、`uploadUrl`
和 `expiresAtUnixTime`。不返回任何存储凭据、内部对象路径或
第三方文件 ID。默认情况下 `ExpertFiles:Storage:Enabled=false`
返回可读的 `503`；`ExpertFiles:Scanner:Enabled=false` 同样如此。

`POST /api/v1/expert-files/{fileId}/objects` 需要 `expert_file.write`
权限。客户端发布一个或多个已提交的对象元数据块（`objectKey`、
`offsetBytes`、`sizeBytes`、`sha256`）。服务器存储元数据，将
`status` 设为 `scanning`，然后设为 `ready` 或 `rejected`。仅
`ready` 状态的文件对后续的附件或读令牌调用可见。拒绝原因仅限于
扩展名/MIME/大小/SHA-256 不匹配以及本地扫描器开关。

`GET /api/v1/expert-files` 需要 `expert_file.read` 权限，并返回
租户范围的摘要列表（id、name、mimeType、sizeBytes、status、扫描
字段、过期时间、软删除标志、`rowVersion`）。跨租户的文件 ID
返回 `404`。

`DELETE /api/v1/expert-files/{fileId}` 需要 `expert_file.write`
权限，并且是软删除：行被标记为 `deleted`，附件被移除，存储
清理是尽力而为。会写入 `file_delete` 审计条目。

`POST /api/v1/expert-files/{fileId}/read-token` 需要
`expert_file.read` 权限，需要必需的 `purpose` 查询参数，并返回
短期的（`expiresAtUnixTime` 在 10 分钟内）`readToken` 以及不含
内部对象键或存储路径的 `readUrl`。每次颁发都会写入 `file_read`
审计条目。

`POST /api/v1/experts/{expertId}/files` 和
`POST /api/v1/expert-runs/{runId}/files` 需要 `expert_file.write`
权限。请求体为 `{ "fileId": <id>, "idempotencyKey"?: "<uuid>" }`。
仅接受同一租户内的 `ready` 状态文件；跨租户或非 `ready` 状态
的文件返回 `404`。附件仅追加并写入 `file_attach` 审计条目。

服务器响应永远不包含凭据、厂商实体 ID、扫描提供商密钥、
存储提供商密钥、内部对象路径或第三方文件 ID。

## 团队运行（V1）

`POST /api/v1/team-runs` 需要 `team_run.write` 权限。第一个已发布
的 `teamVersion` 为 `1`；客户端必须精确发送 `"teamVersion": "1"`。
仅接受三种模式：`sequential`、`parallel`、`synthesis`。请求体为：

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

服务器验证每个 `expertVersionId` 属于调用方的租户且状态为
`published`；每个 `fileId` 必须是同一租户内的 `ready` 状态文件。
然后服务器将团队冻结到 `team_run_template_version` 行中，计算
每个成员的权限交集（`ai.read`、`ai.run` 以及 ExpertVersion 上
声明的 `toolPolicy`），并创建一个 `team_runs` 行以及每个成员
对应的一个 `team_run_members` 行。返回的 `teamRunId` 是客户端
应保留的唯一标识符。

`team_run` 请求和响应体永远不包含成员级提示、模型思考链、
厂商日志、原始中间输出、跨成员上下文、文件二进制或存储路径。
客户端不得在此请求中提交 `prompt`、`messages`、`tools` 或
任意的 DAG 节点。

`GET /api/v1/team-runs/{id}` 返回 `TeamRunSummary`（`id`、`status`、
`mode`、`teamVersion`、`parentAgentRunId`、时间戳、`rowVersion`）。
状态为 `pending | running | completed | failed | cancelled` 之一。

`GET /api/v1/team-runs/{id}/events` 返回最近的审计派生的
`TeamRunEvent` 列表（`id`、`eventType`、`displayPayload`、
`createdAt`）。不包含提示或模型输出。

`GET /api/v1/team-runs/{id}/members` 返回每个成员的显示名称、
`stageOrder`、`expertVersionId`、可选的 `childAgentRunId`、
`status`、可选的 `lastErrorCode`，以及 `permissionIntersectionSummary`
（有效范围名称的逗号分隔列表）。

`GET /api/v1/team-runs/{id}/synthesis` 仅在团队运行为 `completed`
时可用。它返回 `TeamRunSynthesis` 视图（`summary`、`highlights`、
`completedAt`）。在此之前，端点返回 `409` 并附带可读消息。

`POST /api/v1/team-runs/{id}/cancel` 和
`POST /api/v1/team-runs/{id}/retry` 需要 `team_run.write` 权限。
`cancel` 仅在运行处于 `pending` 或 `running` 状态时有效；`retry`
仅在运行达到终止状态后有效。两个端点都会写入审计条目并增加
`HomeMind.Automation` 计数器（`team_runs_triggered_total`、
`team_run_members_failed_total`、`team_run_synthesis_failed_total`）。

团队运行的外部副作用仍由现有的运行动作确认、适配器和审计链
产生；团队编排绝不绕过该边界。跨租户或未知的 `teamRunId` 返回
`404`。

## V2.4 家庭/个人 Connector 与 Web 治理（B18 已发布个人 Connector 基线）

`workspace_connectors` 响应包含 `BindingScope`（`household`/`personal`）。`personal` 实例仅向 owner 返回 `IsCurrentUserOwner=true`，不向其他成员返回 owner 标识，owner/admin 亦不可见他人个人实例；`household` 实例继续用既有成员授权接口。创建或更新时服务端强制家庭实例 `owner_user_id IS NULL`，个人实例 owner 是当前 JWT 用户且为当前租户 active member；任何跨家庭/跨成员资源返回 404。

个人 OAuth 契约（B18）：
- `POST /api/v1/connector-providers/{providerCode}/authorizations`（权限 `connector.authorize`，所有成员可用）：请求体 `{ redirectUri }` 必须命中 Provider 预注册白名单（`ConnectorOAuth:AllowedRedirectUris`）；返回 `AuthorizationSessionView`（`sessionId`/`providerCode`/`providerName`/`status`/`expiresAt`/`authorizationUrl`），会话 10 分钟过期、state 仅存 SHA-256 哈希、PKCE 校验器仅存密文引用；Vault 不可用返回 503+`50001`。
- `GET /api/v1/connector-authorizations/{id}`（`connector.authorize`）：仅本人可查，返回脱敏状态与 `redirectUri`；非本人或跨租户统一 404+`30000`。
- `DELETE /api/v1/connector-authorizations/{id}`（`connector.authorize`）：撤销本人实例的凭据可用性（`auth_status=revoked`、`status=disconnected`）并终止会话；重复撤销幂等返回既有结果；写 `connector_authorize_revoked` 审计。
- 服务端回调 `GET /api/v1/connector-providers/{providerCode}/callback?state=&code=`（匿名）：校验一次性 state（重放/过期拒绝 400）、完成 Token 交换并落库 `vault://tenants/{tenantId}/connector/oauth/...` 凭据引用，成功 302 到会话 `redirectUri`；不返回任何 code/token/ref。
- Mock 授权页 `GET /api/v1/connector-providers/{providerCode}/authorize?state=`（匿名，仅开发/测试）：模拟 Provider 同意并跳转服务端回调。
- 请求、响应、日志、数据库均不含授权 code、access token、refresh token 或明文凭据；`connector_authorization_sessions` 仅存 `state_hash`（CHAR(64)）与 `pkce_verifier_ref`（`enc:` 密文引用）。

审计动作（`family_audit_logs`）：`connector_authorize_started` / `connector_authorize_completed` / `connector_authorize_revoked`，目标类型 `connector_authorization`。

角色维持 `tenant_members.role` 的 `owner/admin/member/viewer` 固定枚举，权限仍由 `PermissionAuthorizationHandler` 映射，不新增角色 CRUD。新增权限名 `connector.authorize`（owner/admin/member）。Web 路由是前端发布配置，不提供 API 路由维护；若未来发布菜单偏好 API，只接受已知 `routeKey`、`enabled`、`sortOrder`，且 owner/admin 才能写入。

## V2.4 专家会话（已发布，B20）

会话为个人资源：租户与所有者均由 JWT 推导，服务层按
`tenant_id + owner_user_id` 隔离，跨用户/跨租户/已软删资源一律 `404`。

| 端点 | 权限 | 契约要点 |
| --- | --- | --- |
| `GET /api/v1/conversations?limit=&cursor=` | `conversation.read` | 仅本人未删除会话，按 `updatedAt` 倒序游标分页；响应 `{ Items: ConversationView[], Cursor }` |
| `POST /api/v1/conversations` | `conversation.write` | 请求 `{ title, expertId?, workspaceConnectorId? }`；绑定专家解析最新 published 版本，不可见 404；成功 201 写 `conversation_create` 审计 |
| `GET /api/v1/conversations/{id}` | `conversation.read` | 非本人/已软删 404 |
| `PUT /api/v1/conversations/{id}` | `conversation.write` | 请求携带 `RowVersion`，不符返回 409/40903；全量替换语义（`expertId: null` 解绑）；写 `conversation_rename` 审计 |
| `DELETE /api/v1/conversations/{id}` | `conversation.write` | 软删除（`deleted_at`），消息历史保留留档；写 `conversation_delete` 审计 |
| `GET /api/v1/conversations/{id}/messages?limit=&cursor=` | `conversation.read` | 按主键倒序游标分页，非法游标按第一页处理；消息不返回 Prompt/思考链 |
| `POST /api/v1/conversations/{id}/messages` | `conversation.write` | 请求 `{ content, idempotencyKey? }`；未绑定专家 422/42200；成功创建关联会话的 Expert Run（携带 `conversationId`）并落 user 消息；响应 `{ RunId, Status, MessageId }`（201 新建/200 幂等重放）；Run 终态后由 `AgentRunProcessor` 追加 assistant 消息（内容取 `result_summary`，幂等按 `(conversation_id, run_id)`） |

上下文拼接：发送时取最近 20 条历史消息（升序），字符预算 12000 从最旧丢弃，
产出 `{"messages":[{"role":"user"|"assistant","content":"..."}]}` 作为
`expert_runs.input_json`（AgentRunProcessor 原样作为 LLM UserMessage）。

## V2.4 自建专家（已发布，B21）

`experts.owner_user_id` 为空表示平台基础专家（全家可见），非空表示用户自建专家
（仅创建者本人可见可维护，跨用户/跨租户/已软删一律 404）。`experts.deleted_at` 软删除
后专家从目录、运行解析（`ResolveSourceAsync`）与会话发送（`ResolveExpertAsync`）全链路消失。

| 端点 | 权限 | 契约要点 |
| --- | --- | --- |
| `GET /api/v1/experts?query=&category=&type=&scope=` | `ai.read` | `scope`：`basic`（默认，平台基础）/ `mine`（本人自建）/ `all`（两者）；列表项为 `ExpertCatalogItemView`（`Id/CatalogType/Source/Code/Name/Category/Description/EstimatedCredits`），不暴露他人 owner |
| `GET /api/v1/experts/{id}` | `ai.read` | 返回 `ExpertDetailView`（含最新已发布版本快照与 `Source`）；他人自建/已软删 404 |
| `POST /api/v1/experts` | `expert.mine.write` | 请求 `{ name, category, persona, promptTemplate, description?, methodology?, toolPolicyJson?, estimatedCredits? }`；缺必填 422/10001、toolPolicyJson 非法 JSON 422；成功 201 + `ExpertDetailView`（code=`custom-` 前缀、`Source=mine`、v1 published） |
| `PUT /api/v1/experts/{id}` | `expert.mine.write` | 全量替换头部字段并生成 `version+1` 已发布版本（版本不可变不变量）；携带 `RowVersion`，不符 409/40903 |
| `DELETE /api/v1/experts/{id}` | `expert.mine.write` | 软删除（`deleted_at`）；重复删除 404 |

自建专家不写 `family_audit_logs`（设计 §13.1 仅要求会话审计）。

## V2.5 快速剪辑 Skill（已发布，B24/B25）

Skill 独立执行（SkillExecutor 首个实现）：`029` 迁移新建平台级 `skills` 目录表
（`tenant_id=1`，同 `scenario_templates` 惯例）并种子注册 `quick-edit`
（category=`media`、`risk_level=L1`、`required_permission=media.read`）；运行创建
SourceType=skill 的 AgentRun（不绑定 Expert，`expert_id` 为空），复用确认/幂等/审计链路，
不新建运行时。B25 起确认后经剪辑 MCP 客户端（当前为确定性 Mock 实现 `MockClippingMcpClient`，
真实 jianying-mcp/capcut-mate 接入为部署环境验证项）生成 .draft 草稿内容并登记为生成文件。

| 端点 | 权限 | 契约要点 |
| --- | --- | --- |
| `POST /api/v1/skills/{skillCode}/runs` | `ai.run` + `media.read` | 请求 `{ idempotencyKey?, inputJson }`；`inputJson` 含 `media_location`（必填，素材位置）与 `instruction`（可选，创作目标和指令）。未知/未启用 Skill 422；`media_location` 缺失或 JSON 非法 422。成功 201 返回 `SkillRunView`（`Id/Status/ResultSummary/CreatedAt/FinishedAt/Events/Actions`，动作 `ActionType=draft_generate`、`RiskLevel=L1`）。确定性方案生成：从 `instruction` 提取目标时长（`N秒/N分钟`，1-600 秒，默认 15 秒），单片段方案承载于动作 RequestJson（`media_location`/`instruction`/`segments`/`audio`/`total_duration`）。幂等键重复创建返回既有运行（200）；同键已用于其他运行类型 409。创建写 `skill_run_created` 审计（目标 `skill_run`） |
| `POST /api/v1/skills/runs/{runId}/actions/{actionId}/confirm` | `ai.run` + `media.read` | 请求 `{ idempotencyKey }`（UUID 必填）。非法幂等键 422；动作不存在/非本人 404；已终态换键 409；同键重复确认重放首次结果（不重复登记）。确认后：经剪辑 MCP 生成 .draft 内容 → `RegisterGeneratedFileAsync` 登记为 Ready 生成文件（文件名 `quick_edit_{runId}.draft.json`，`application/json`，附件到 run）→ action `executed`、run `completed`；写 `skill_action_confirmed`（目标 `skill_run`）与 `skill_draft_registered`（目标 `skill_draft`）审计。成功 200 返回 `{ actionId, status, message, fileId }`，消息「草稿已生成，打开剪映即可编辑。」；登记失败 502、action/run `failed`。下载复用既有 `POST /api/v1/expert-files/{fileId}/read-token`（10 分钟 readToken） |

响应不包含素材目录内容、MCP 内部路径、草稿绝对路径或 Prompt。运行轮询/取消/重试
复用既有 `expert-runs` 契约（`GET /api/v1/expert-runs/{id}`、`/events`、`/cancel`、`/retry`），
B24/B25 不新建轮询端点。剪辑 MCP 端到端（真实 jianying-mcp 写入素材/剪映草稿目录）按部署环境验证。

## 本地运行

代码更改后，端口 `5280` 上的当前进程必须重启。运行：

```powershell
dotnet run --project .\HomeMind.Api
```

Swagger 位于 `http://localhost:5280/swagger`，根路径会自动跳转
到 `/swagger/index.html`。开发环境 CORS 默认为 `AllowAnyOrigin`，
便于 Flutter Web 调试；生产环境必须改回 `Cors:AllowedOrigins`
白名单策略（见 `Startup.cs` 中被注释的示例）。
