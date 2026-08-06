# NexusMind 后端开发设计

> **对应总纲：** `D:\HomeMind\core\docs\main\NexusMind-Product-Master-Design.md`  
> **代码仓库：** `D:\HomeMind\core`  
> **当前版本：** V2.3（承接 V2.2 家庭协同能力，新增个人生活专家与单人全流程治理基线）
> **维护要求：** 总纲涉及 API、数据模型、执行策略或 Connector 变化时，必须先更新产品总设计，并在同一变更中更新本文和后端开发计划；新增 HTTP 接口还必须更新 `docs/frontend-api-integration.md` 与 `docs/api-implementation.md`。

## 1. 当前代码位置与分层

| 层级 | 路径 | 职责 |
| --- | --- | --- |
| HTTP API | `HomeMind.Api/Controllers/**` | 请求校验、鉴权用户提取、HTTP 响应转换 |
| API 基础设施 | `HomeMind.Api/Services/**` | 注册、鉴权、统一响应等 |
| 业务接口 | `HomeMind.Business.IServices/**` | 定义领域服务边界 |
| 业务实现 | `HomeMind.Business.Services/**` | 跨表事务、领域规则与编排 |
| 实体与 DTO | `HomeMind.Common.Model/Entities`、`HomeMind.Common.Model/ViewModel` | 数据库映射及输入/输出模型 |
| 数据访问 | `HomeMind.Common.Repository/**`、`HomeMind.Common.IRepository/**` | DbContext、Repository、Unit of Work |
| 公共基础设施 | `HomeMind.Common.Infrastructure/**` | Token、密钥保护、DI 扩展 |
| 创作者中心本地 MCP | `HomeMind.CreatorMcp/**` | 独立 stdio MCP Server；经既有 API 显式同步专家、专家组和技能元数据到本地 SQLite，只读提供给本地 Agent |
| 数据库迁移 | `database/NNN_*.mysql.sql` | MySQL 版本化迁移，SQL 为事实来源 |

必须遵守现有 `DEVELOPMENT.md`：Controller 不直接查询 DbContext/实体；请求 JSON 使用 camelCase，响应信封及其 Data 使用 PascalCase；所有新增路由有中文 `Msg` 和接口文档。

## 2. V1 服务端边界

V1 在既有 AI/专家能力之上建立通用 Connector Framework，Smart Home 是首个垂直领域，完成：

1. 保存并授权 Mock Connector，验证通用 Connector、Tool 与 Permission 契约；
2. 使用模拟发现结果规范化并查询家庭空间和设备能力；
3. 运行“家庭管家”Expert，生成可解释的行动建议；
4. 对写动作执行显式确认、权限校验、幂等控制、审计和结果记录；
5. 支持回家、离家、睡眠三种场景的创建/执行。

不在 V1 后端范围内：多厂商正式集成、完整的长期记忆/多 Agent 协作、完全无人确认的高风险动作、医疗级健康分析和户型图理解。

## 3. 建议模块组织

在现有分层中先增加通用 `Connector` 模块，再由 `SmartHome` 作为其首个协议/领域实现，避免把外部适配逻辑混入 Todo、Calendar 或通用 AI：

```text
HomeMind.Api/Controllers/Connectors/
HomeMind.Business.IServices/Connectors/
HomeMind.Business.Services/Connectors/
HomeMind.Business.Services/Connectors/SmartHome/
HomeMind.Common.Model/Entities/Connectors/
HomeMind.Common.Model/Entities/SmartHome/
HomeMind.Common.Model/ViewModel/Data/Connectors/
```

AI Runtime 的编排职责位于 `Business.Services/AI` 或独立 `Business.Services/Automation`；其下调用 Connector 服务接口，不得直接依赖 Home Assistant HTTP、Google API 或其他第三方细节。

建议在 `Business.Services/Connectors/` 内按职责分离 `Adapters/`（协议/厂商转换）、`Tools/`（Tool 发现与执行）、`Authorization/`（授权与确认）和 `Sync/`（状态同步/订阅）；SmartHome 子模块再包含 `Discovery/`、`Commands/` 和设备映射。V1 保持为模块化单体；只有连接器调用、实时连接或家庭 Agent 规模需要独立伸缩时再提取服务，不能先以微服务复杂度替代产品验证。

## 4. 数据模型与迁移

### V1 表与职责

| 表 | 说明 | V1 关键字段 |
| --- | --- | --- |
| `connector_providers` | 全局可接入 Connector 目录 | code、name、type、provider、status |
| `workspace_connectors` | 租户级已授权 Connector 实例 | tenant_id、connector_provider_id、binding_scope（household/personal）、owner_user_id（个人必填，家庭为空）、name、status、auth_status、config、credential_ref、created_at、last_sync_at、last_health_at |
| `connector_tools` | Provider 声明的稳定外部工具契约 | connector_provider_id、name、description、input_schema、output_schema、permission、risk_level、require_confirm |
| `workspace_connector_tools` | 已授权实例实际可用的工具 | workspace_connector_id、connector_tool_id、status、availability_reason、last_checked_at |
| `connector_permission_grants` | 成员对连接/工具权限的显式许可 | user_id、workspace_connector_id、connector_tool_id 可空、permission、effect、confirmation_policy、granted_at、revoked_at |
| `smart_home_spaces` | 家庭空间 | tenant_id、name、space_type、sort_order |
| `smart_home_devices` | 已规范化设备 | tenant_id、workspace_connector_id、space_id、external_id、device_type、name、online_status |
| `device_capabilities` | 设备可读/写能力 | device_id、capability、value_schema、permission |
| `device_states` | 最近设备状态快照 | device_id、state_json、updated_at |
| `skills` | Skill 目录与权限声明 | key、input_schema、output_schema、required_permission |
| `experts` | Expert 目录 | code、name、category、builtin、status、owner_user_id（可空：空=平台基础专家，开发端维护、全家可见；非空=用户自建，仅创建者本人可见可维护） |
| `expert_versions` | 可复现的 Expert 策略版本 | expert_id、version、system_prompt、skill_policy、output_schema |
| `expert_skill_permissions` | Expert Version 调用 Skill 的限制 | expert_version_id、skill_id、max_calls、require_confirm |
| `expert_runs` | 一次专家运行 | user_id、tenant_id、expert_id、expert_version_id、status、input_context、token 用量、row_version、conversation_id（可空，关联会话） |
| `conversations` | 用户围绕某领域创建的对话项目（对话框），绑定专家与连接器 | tenant_id、owner_user_id、title、expert_id、expert_version_id（可空）、workspace_connector_id（可空，单值）、deleted_at、updated_at、row_version |
| `conversation_messages` | 会话内对话消息，一次消息对应一次可追溯的 Expert Run | conversation_id、role（user/assistant）、content、run_id（可空）、created_at；按 conversation_id + id 游标分页 |
| `run_events` | 客户端可展示的运行时间线 | run_id、sequence、type、step_id、status、display_message、created_at |
| `run_actions` | 运行中的建议/执行项 | run_id、action_type、payload、status、confirmed_at、idempotency_key、result |
| `scenes` / `scene_actions` | 家庭场景及其设备能力动作 | tenant_id、scene_id、device_id、capability、value |
| `automation_rules` | 已确认的长期规则 | tenant_id、trigger、actions、enabled、row_version |
| `personal_favorites` | 成员个人的偏好集合（店铺、旅行点、素材），支撑个人生活专家 | home_id、owner_member_id、category、name、detail_json、visibility、软删除、row_version |
| `connector_authorization_sessions` | 个人 OAuth 或受控家庭授权的短期服务端会话 | tenant_id、connector_provider_id、binding_scope、initiator_user_id、state_hash、pkce_verifier_ref、redirect_uri、status、expires_at、completed_at |

设计和创建迁移前，先核对现有 `AiEntities.cs` 与 002/003 迁移，避免重复创建 Expert/Skill 基础表。新增迁移使用下一个未占用序号；已执行迁移不修改。

### 数据与安全约束

- 所有数据均由 JWT 中的 `tenant_id` 隔离，禁止客户端传入值覆盖；
- Connector Provider 与 Workspace Connector 分离；`status` 表示连接健康，`auth_status` 表示授权生命周期；`config` 仅保存经 Provider Schema 校验的非敏感配置，凭据只以 `credential_ref` 指向密钥服务，响应模型永不回传明文；
- Tool 使用 JSON Schema 描述输入/输出，在 Provider 内按名称唯一；实例只有在 `workspace_connector_tools.status=enabled` 且请求者拥有有效 `connector_permission_grants` 时才能调用，默认拒绝；
- 可变业务表含 `deleted_at`、`updated_at`、`sync_version`；目录及运行记录采用 `row_version`；
- `external_id` 在同一 Workspace Connector 内唯一；写操作使用 `idempotency_key` 防止重复下发；
- 状态历史和执行结果可追溯，敏感输入应脱敏后写入审计。
- Run 必须固定 `expert_version_id` 和已解析的权限快照；Run Event 只保存用户可理解事件，禁止写入 Prompt、思考链或模型日志。
- 会话与消息按 `tenant_id` + `owner_user_id` 隔离，禁止客户端覆盖归属；自建专家（`experts.owner_user_id` 非空）仅创建者本人可见可维护，跨用户/跨租户一律 404；消息内容含用户输入，仅脱敏摘要入审计，不写 Prompt 或思考链；会话发送消息时按会话加载历史消息拼接输入上下文（复用 `input_context` 语义）。
- `binding_scope=household` 仅 owner/admin 可创建和配置，并通过成员 Permission Grant 使用；`binding_scope=personal` 的 `owner_user_id` 必须是同租户 active `tenant_members`，只允许 owner 读取、调用、撤销。个人实例不因家庭成员关系自动共享。
- OAuth 的 state、PKCE、回调、Token 交换、加密存储、刷新、撤销和审计都在服务端；授权会话单次使用且过期。数据库仅保存哈希/密钥引用，Controller、DTO 和日志不得出现授权 code 或 Token。

### 创作者中心本地 MCP Bridge

`HomeMind.CreatorMcp` 是面向受控本机 Agent 的开发工具进程，不是业务 Connector、Expert、Skill Executor 或 Flutter 的数据源。它以 MCP 标准 stdio JSON-RPC transport 运行，并只通过已鉴权的 `GET /api/v1/experts?type=expert|group` 与 `GET /api/v1/skills` 获取创作者中心数据；调用令牌至少需要 `ai.read` 和 `ai.skills.read`。该进程不得直连生产 MySQL、DbContext 或 Secret Vault，也不得反向写入 NexusMind API。

Bridge 仅提供 `sync_creator_center`、`search_creator_center`、`get_creator_item` 和 `creator_sync_status`。同步必须由 Agent 显式调用并记录最后成功时间，读取只命中本机 SQLite 缓存，不触发隐式网络请求。专家和技能提示词属于敏感数据：默认不写入缓存、不出现在搜索结果；只有 `NEXUSMIND_MCP_ALLOW_SENSITIVE=true` 且工具请求显式传入 `includeSensitiveData=true` 时，才允许同步或读取。所有错误、日志和工具响应仍不得泄漏 Bearer Token、凭据、供应商原始字段或模型思考链。

本地 SQLite 仅是开发设备缓存，不属于产品领域存储，不新增 MySQL 迁移、租户模型或移动端接口。远程 MCP 接入需在同一只读 Tool 层另行实现带认证、会话与 Origin 校验的 Streamable HTTP transport；不得将数据库或本地 SQLite 暴露给 Agent。运行、环境变量与 Codex 配置见 `docs/mcp-creator-center.md`。

## 5. 服务职责与执行链路

```text
ExpertsController
  → IExpertRunService（创建运行、返回建议）
  → IPlannerService（生成行动草案）
  → ISkillExecutor（权限校验、参数校验、分发）
  → IConnectorService（解析实例、Tool、授权和审计）
  → IDeviceAdapter / IDeviceDiscovery / IDeviceCommandExecutor（协议无关三契约，B13 起）
  → HomeAssistantAdapter（HA REST/WebSocket 实现）
```

会话由 `IConversationService`（会话 CRUD、消息历史、上下文拼接）承载：发送消息时校验会话归属（tenant + owner），按会话加载历史消息拼接输入上下文，复用 `IExpertRunService` 创建携带 `conversation_id` 的 Run；Run 终态后写入 `assistant` 消息并保留 `run_id` 供追溯。

`IConnectorService` 是对 Skill 的统一入口：列出可用 Tool、校验 JSON Schema 与 Permission、应用确认策略、创建/关联 Run Action，并把请求委派给 Adapter。B13 起设备边界拆分为 `IDeviceAdapter`（连接测试、单设备状态读取）、`IDeviceDiscovery`（设备发现）、`IDeviceCommandExecutor`（设备命令执行）三个协议无关接口，`CommandRelayService`/`DeviceSyncService` 两个桥接服务分别承载命令转发与状态同步落库。业务服务依赖这些接口以及标准化能力模型，不依赖 MQTT Topic、Zigbee 实体名或厂商 JSON 格式。

`ConnectorToolDefinition` 包含 `Name`、`Description`、`InputSchema`、`OutputSchema`、`Permission`、`RiskLevel` 和 `RequireConfirm`。`ConnectorToolCall` 必须包含 Connector 实例、Tool、经过验证的输入、操作者、Run Action ID 和幂等键。接口字段语义与 MCP Tool 保持兼容，但 V1 不引入 MCP 协议依赖。

Adapter 应进一步支持状态订阅或轮询同步；统一输入/输出使用 `SmartDevice`、`DeviceCapability`、`DeviceState` 和 `DeviceCommand`。`DeviceCommand` 必须包含 Workspace Connector、目标设备、能力、目标值、操作者、Run Action ID 与幂等键。禁止将 `light.turn_on`、`prop.power`、`switch_led` 等供应商字段泄露到 Controller、DTO 或业务规则中。

| Adapter | 职责 | V1 状态 |
| --- | --- | --- |
| Mock Connector Adapter | 提供确定性的 Tool、设备发现、状态和执行结果，供端到端验证 | 正式实现 |
| Home Assistant Adapter | 通过 HA REST/WebSocket 发现实体、读取状态、调用服务 | 第二阶段实现 |
| MQTT / Zigbee2MQTT Adapter | 消费标准化状态/命令主题，映射 Zigbee2MQTT JSON | 兼容层与本地优先设计 |
| Xiaomi Cloud Adapter | 将米家云字段映射为统一能力，管理 Token 刷新和限流 | 仅契约/后续实现 |
| Tuya Cloud Adapter | 将涂鸦云字段映射为统一能力，管理 Token 刷新和限流 | 仅契约/后续实现 |

MQTT 内部主题统一为 `nexusmind/home/{homeId}/device/{deviceId}/state` 和 `nexusmind/home/{homeId}/device/{deviceId}/command`。所有 command 消息必须来自已确认的 Run Action 或明确授权的 Automation Rule；消费者按 Action ID/幂等键去重，状态消息记录采样时间并触发 Dashboard 刷新。MQTT Topic 不是对 Flutter 或其他业务模块的 API。

写操作链路必须是：生成 `run_action`（`pending`）→ 用户确认 → 再次校验成员、连接、设备能力、Expert Version 与权限快照 → 下发 → 记录 `executed` / `failed`。运行状态可细分为 `draft`、`queued`、`planning`、`running`、`synthesizing`、`needs_input`、`completed`、`failed`、`cancelled`；前端由 Run Event 映射为简明时间线。重复确认请求必须返回既有结果，不能重复控制设备。

## 6. API 规划

所有受保护接口位于 `/api/v1`，使用现有统一 `ApiResponse<T>` 信封。以下是 V1 资源划分，具体字段加入接口文档后再实现。

| 资源 | 主要接口 | 用途 |
| --- | --- | --- |
| Connector Provider / 实例 | `GET /connector-providers`、`GET/POST /connectors`、`POST /connectors/{id}/test`、`/discovery`、`/sync`、`GET /connectors/{id}/tools`、`GET/PUT /connectors/{id}/permission-grants` | 查看可接入产品，安全管理实例、可用 Tool 与成员授权，并受控执行连通性测试、发现和轮询状态同步 |
| 家庭空间 | `GET /smart-home/spaces` | Home+ 页面空间与摘要 |
| 设备 | `GET /smart-home/devices`、`POST /connectors/{id}/discovery`、`POST /connectors/{id}/sync` | 查看、发现并同步标准化设备 |
| 场景 | `GET /smart-home/scenes`、`POST /smart-home/scenes/{key}/run` | 回家/离家/睡眠入口 |
| Expert Run | `POST /experts/{key}/runs`、`POST /housekeeper-runs`、`GET /expert-runs/{id}`、`GET /expert-runs/{id}/events`、`GET /expert-runs/{id}/actions` | 创建家庭管家分析、查看进度、事件和待确认方案；会话发送的消息创建的 Run 携带 `conversation_id` |
| Run Action | `POST /expert-runs/{id}/actions/{actionId}/confirm` | 明确确认待执行动作 |
| 会话（专家对话框） | `GET/POST /conversations`、`GET/PUT/DELETE /conversations/{id}`、`GET /conversations/{id}/messages`、`POST /conversations/{id}/messages` | 会话 CRUD（列表/新建/重命名/软删除+审计）、消息历史（游标分页）与发送；发送复用 Expert Run 链路并携带 `conversation_id` |
| Automation | `GET/POST/PATCH /automation-rules` | 管理已确认自动化 |
| 个人 Connector 授权（V2.4，B18 已发布） | `POST /connector-providers/{code}/authorizations`、服务端 OAuth callback、`GET /connector-authorizations/{id}`、`DELETE /connector-authorizations/{id}` | 仅当前成员（`connector.authorize`）发起/查看/撤销个人绑定；会话单次使用、state 哈希与 PKCE 密文引用、凭据仅 vault 引用落库 |

接口新增/变更的完成条件：`docs/frontend-api-integration.md` 含请求、响应、错误示例；`docs/api-implementation.md` 更新路由状态；前端实施文档的接口依赖同步更新。

### 面向 Flutter UI 的接口流程

前端真实实现位于 `D:\HomeMind\mobile\lib`，并遵循其 `docs/DEVELOPMENT_GUIDELINES.md` 和 `docs/UI_STYLE_GUIDE.md`。API 设计必须支持其页面状态，而非只返回设备原始数据：

| 前端流程 | 后端职责 | 必需返回/行为 |
| --- | --- | --- |
| Dashboard 初始化 | 聚合今日建议、Todo/Calendar、家庭摘要、场景 | 每条状态带采样/更新时间；部分数据失败不阻塞其他卡片 |
| Expert 开始分析 | 创建 `expert_run`，异步推进运行 | 立即返回 Run ID 与 `queued` 状态 |
| Run Timeline | 查询运行与行动进度 | 仅返回可展示的阶段、建议、确认需求和错误，不返回模型思考链 |
| 用户确认行动 | 校验权限、版本与幂等键后执行 | 重复提交返回原行动结果，状态可查询 |
| Home+ 空间页 | 返回标准化空间、设备摘要、场景 | 隐藏供应商协议字段，输出自然语言所需状态及更新时间 |
| Connector 管理 | 返回连接健康、授权与发现结果 | 凭据脱敏；断连/授权中/失败可被 UI 直接区分 |
| 专家对话框 | 创建关联会话的 Run、轮询、终态追加 assistant 消息 | 发送返回 Run ID 与 `queued` 状态；历史游标分页；消息内容脱敏，不返回 Prompt/思考链 |

对于 Expert Run，建议使用短轮询的 `GET /expert-runs/{id}` 作为 V1 标准；API 应提供稳定的 `status`、`actions`、`updatedAt` 和可安全展示的 `error` 字段。不要要求 Flutter 页面解析日志文本或推断状态。

## 7. 权限、可靠性与观测

- 权限至少区分环境读取、设备读取、灯光写入、空调写入、安全类写入和自动化管理；每个授权明确 `allow` / `deny`、有效期及确认策略；
- 写入动作默认要求确认，只有低风险、成员已显式授权且由已启用自动化规则触发的调用可自动运行；门锁、安防和创建长期自动化始终要求确认；
- 每一项外部调用记录 Connector、目标设备、参数摘要、发起者、时间、结果和错误码；
- Connector 不可用时，返回可读失败状态，不以本地成功掩盖远端失败；
- 设备状态设定缓存时效，Dashboard 显示采样时间，避免把过期数据描述为实时状态；
- 外部调用设置超时、有限重试和幂等语义，绝不因重试产生重复设备动作。
- Home Assistant 是设备驱动层，业务 API 不直接转发其控制接口；Zigbee2MQTT/MQTT 支持本地优先运行，厂商云 Adapter 必须处理限流、Token 刷新和云端延迟；
- 家庭 Agent 或 Node-RED 不能绕过权限校验、Run Action 审计与 `automation_rules`；离线补报必须保留原始发生时间与执行来源。
- 会话与消息 `conversation.read` / `conversation.write` 仅作用于 `owner_user_id=本人` 的会话；用户自建专家 `expert.mine.read` / `expert.mine.write` 仅作用于 `experts.owner_user_id=本人` 的资源，跨用户/跨租户一律 404。

## 8. 后端实施顺序与验收

本模块在项目路线图中对应 **M5.4 SmartHome Connector Layer**：它是通用 Connector Framework 的首个垂直实现，与 M5.2 并列，依赖 M5.1 的身份/租户/迁移基线和 M5.3 的 Skill Engine、Expert Run 与 Action 语义。完成定义是 AI Expert 可以通过受控 Skill 调用 Connector Tool；家庭设备能力是首个验证目标，不是单独提供一个设备控制 API。

与产品 12 个月节奏的后端对齐为：第 1 月完成 Expert/Skill 与 SmartHome Mock 链路；第 2–3 月交付 HA Adapter、五类设备、场景和审计；第 4–6 月强化家庭 Dashboard 聚合、稳定性与收费所需的连接/远程服务；第 7–9 月在权限和 `automation_rules` 基础上建设 Family Context Engine；第 10–12 月将 Host/Builder 试点隔离为独立租户、角色、审计和配置能力，避免污染 C 端家庭模型。

| 顺序 | 交付 | 验收 |
| --- | --- | --- |
| 1 | 数据迁移、实体和 Repository 映射 | 能按租户读写 Connector、空间、设备和能力 |
| 2 | Mock Connector 与连接测试/设备发现 | 能验证 Tool 发现、权限和标准化设备能力 |
| 3 | 家庭空间和设备查询 API | 前端可展示真实或受控模拟的空间摘要 |
| 4 | Skill Executor、家庭管家 Run | 能生成包含只读数据依据的行动草案 |
| 5 | 确认与执行 API | 同一确认不重复执行，执行结果可查询 |
| 6 | 三个场景与自动化草案 | 回家、离家、睡眠均可审计地完成 |
| 第二阶段 | Home Assistant Adapter | 在不改变 Skill、Tool、Permission 和 Run Action 契约的前提下接入真实设备 |
| 第三阶段 | 自动化与稳定性 | 规则触发、同步队列、重试与可观测性纳入生产基线 |
| 第四阶段 | Expert Files 与多专家团队编排 | 上传、扫描、附件、读取令牌；版本化 `sequential`/`parallel`/`synthesis` 团队；成员权限交集；不绕过既有 Action 边界 |
| 第五阶段 | V2.3 个人生活专家（B15-B17） | `personal_favorites` 迁移、CRUD 与审计；`personal-life-expert` 注册与翻牌/行程 Run 链路；日历同步确认与联调验收 |
| 第六阶段 | 专家会话与自建专家（B19-B20） | `conversations`/`conversation_messages` 迁移与实体、`experts.owner_user_id` 扩展、`expert_runs.conversation_id`、`IConversationService`（会话 CRUD/消息/上下文拼接）、会话与消息 API、`GET /experts?scope=basic\|mine\|all` 过滤；验收：归属隔离、上下文拼接、消息追溯（run_id）、scope 过滤与跨用户 404 |

阶段 8（Expert Files 与多专家团队编排）新增 8 张表 `expert_files`、`expert_file_objects`、`expert_file_attachments`、`team_run_templates`、`team_run_template_versions`、`team_runs`、`team_run_members`、`team_run_audits`，全部按 `tenant_id` 隔离并使用 UTC `DATETIME(3)`、乐观 `row_version`、状态/模式检查约束。文件二进制不进入数据库；对象存储由 `IExpertFileStorage` 抽象隔离，`LocalExpertFileStorage` 作为受控本地实现，`ExpertFiles:Storage:Enabled=false` 时返回可读 `503`。扫描走 `IExpertFileScanner`，默认仅做扩展名、MIME、大小、SHA-256 校验；状态固定为 `pending_upload | scanning | ready | rejected | deleted`，仅 `ready` 文件可被附加或被运行时读取。

团队编排仅支持显式 `sequential`、`parallel`、`synthesis` 三种模式；首个发布的 `teamVersion=1`；客户端提交时必须显式声明成员 `expertVersionId` 与文件 `fileIds`；服务端在创建时将模板冻结到 `team_run_template_versions`，并把每个成员的权限交集写入 `team_run_members`。`team_run_create`、`team_run_cancel`、`team_run_retry` 与成员、合成失败均落入 `team_run_audits`；`HomeMind.Automation` Meter 新增 `team_runs_triggered_total`、`team_run_members_failed_total`、`team_run_synthesis_failed_total` 计数器。所有响应 DTO 不返回存储凭据、内部对象路径、第三方文件 ID、成员 Prompt、模型思维链、供应商日志或原始中间输出；跨租户与跨用户的资源一律返回 `404`。外部效果仍由既有 `POST /api/v1/expert-runs/{id}/actions/{actionId}/confirm` 链路承担，团队编排不得绕过该边界。

## 9. 与前端的联动检查

前端目录与实施细节在 `D:\HomeMind\mobile\docs\main\NexusMind-Frontend-Development.md`；开发/UI 规则在 `D:\HomeMind\mobile\docs\DEVELOPMENT_GUIDELINES.md` 和 `D:\HomeMind\mobile\docs\UI_STYLE_GUIDE.md`。每次后端设计变更至少确认：接口是否已定义、加载/空/错误/确认状态是否可呈现、字段是否包含权限与最后更新时间、是否需要客户端刷新或轮询运行状态。

## 10. V2.2 家庭管家增量设计

V2.2 把服务端的产品语义从“生成 AI 建议并确认 Run Action”扩展为“AI 与家庭成员共同管理”。新增家庭上下文、面向用户的管家动态和三级风险确认，但不改变既有 JWT `tenant_id` 隔离、Connector、Skill、Run Action、授权和幂等执行边界。

### 10.1 模块与服务边界

```text
HomeMind.Business.IServices/Connector/
  IDeviceAdapter.cs                连接健康测试与单设备状态读取
  IDeviceDiscovery.cs              设备发现（标准化结果）
  IDeviceCommandExecutor.cs        设备命令执行
HomeMind.Business.Services/Connectors/
  Adapters/HomeAssistantAdapter.cs
  Adapters/ZigbeeDirectAdapter.cs       (Phase 3)
  Adapters/MijiaCloudAdapter.cs         (Phase 2)
  Adapters/TuyaCloudAdapter.cs          (Phase 2)
  Bridge/DeviceSyncService.cs
  Bridge/CommandRelayService.cs

HomeMind.Business.IServices/Family/
  IFamilyMemberService.cs
  IFamilyKnowledgeService.cs
  IDecisionHistoryService.cs
HomeMind.Business.IServices/Steward/
  IStewardActivityService.cs
  IConfirmationService.cs
```

业务层只能依赖 `IDeviceAdapter`、`IDeviceDiscovery` 和 `IDeviceCommandExecutor` 等统一接口（B13 已落实，原 `IConnectorAdapter` 已拆分删除）；Home Assistant Adapter 必须实现该契约。`DeviceSyncService` 负责把适配器状态转换为标准化设备状态和健康信息（发现 → 落库 → 自动化回调，B13 已由 `ConnectorRuntimeServices` 收敛至此），`CommandRelayService` 负责将已经授权、确认并具备幂等键的命令转发给适配器（健康检查通过才转发，B13 已由 `HousekeeperRunServices` 收敛至此）。后续直连 Zigbee 与厂商云 Adapter 只能作为该边界内的实现，不能让业务服务或 Controller 直接依赖 HA API。

### 10.2 迁移、实体与 DTO

`database/014_v2.2_family_and_steward.mysql.sql` 已完成代码侧追加，且只新增下列对象与既有表字段，未修改已执行迁移。它以当前 `tenants.id` 作为 `home_id` 的外键，使家庭作用域继续与 JWT `tenant_id` 一致；`family_knowledge` 和 `decision_history` 通过 `(home_id, member_id)` 复合外键阻止跨家庭成员来源。该迁移尚未在真实 MySQL 环境执行。

| 数据对象 | 关键字段 / 约束 |
| --- | --- |
| `family_members` | `home_id`、name、relation、birthday、is_elderly、is_child、`member_status`、preferences、created_by、软删除 |
| `family_knowledge` | `home_id`、category、key/value、notes、`source_member_id`、`confidence_score`、`conflict_resolution_strategy` |
| `decision_history` | `home_id`、scenario、decision_made、rationale、alternatives、made_by |
| `steward_activities` | `home_id`、可选 run_id、category、title、description、risk_level、status、result_summary、undoable |
| `confirmation_items` | `home_id`、可选 activity_id、risk_level、title、description、impact_summary、suggested_action、status、confirmed/denied/expired 信息 |

`family_members.member_status` 仅允许 `active`、`away`、`permanently_left`、`deceased`。`active` 与 `away` 可双向变更；二者可进入任一终态；终态只能由具备家庭管理权限的更正操作恢复，并记录操作者、原因和时间。软删除不替代生命周期状态，且不得物理删除被知识、决策或审计引用的成员。`family_knowledge.category` 固定为 `property`、`wifi`、`repair`、`cleaning`、`insurance`、`other`；`source_member_id` 必须引用同一家庭成员，`confidence_score` 的范围为 0 到 1，`conflict_resolution_strategy` 仅允许 `latest`、`authority`、`majority`。同 key 的冲突按该策略处理，同时保留来源与解决结果。`steward_activities.category` 固定为 `sensing`、`planning`、`executing`、`reporting`；其状态为 `pending`、`confirmed`、`executing`、`completed`、`failed`、`cancelled`。`confirmation_items.risk_level` 仅允许 `L1`、`L2`、`L3`。

同时扩展 `smart_home_devices`：`zigbee_role`（`end_device` / `router` / `coordinator`）、`battery_level`、`signal_lqi`、`health_status`（`healthy` / `degraded` / `offline` / `low_battery`）；扩展 `expert_runs`：`mode`（`single` / `steward`）与 `auto_confirm_policy`（`L3_only` / `L2_and_above` / `never`）。实体和 DTO 分别位于 `Entities/Family`、`Entities/Steward`、`ViewModel/Data/Family`、`ViewModel/Data/Steward`，并包含上述字段和时间戳。B9 已完成迁移、实体、`DbSet` 和 DTO 基线；B10 已落实：

- `DiscoveredDevice` 与 Home Assistant Adapter 把 `attributes.zigbee_role` / `battery_level` / `signal_lqi` 标准化为 4 个字段，并按 `online_status`、`battery_level`、`signal_lqi` 派生 `health_status`（`offline` > `low_battery` > `degraded` > `healthy`），Mock 同步写出 4 字段；任何未识别或缺失值都降级为 `null`/默认 `healthy`，不暴露 Topic、网络地址、厂商实体或原始协议。
- `SmartHomeDeviceView` 真实与 Mock 分支都返回新字段；新增 `GET /api/v1/smart-home/devices/health`（`DeviceHealthSummaryView`）按家庭/空间聚合 `healthy / degraded / offline / low_battery` 计数与主导状态。
- `HousekeeperRunPolicy` 提供 `CanAutoConfirm(policy, riskLevel)` 服务层纯函数；`L3` 在任何策略下都被显式拒绝；`HousekeeperRunServices.CreateAsync` 默认写入 `mode="steward"`、`auto_confirm_policy="L3_only"`，并在 `HousekeeperRunView` 中回填这两个字段。L1/L2 自动确认与确认中心联动属于 B12。
- `HomeMind.Business.Services.Tests` 工程已建立（`HomeAssistantConnectorAdapterMappingTests` / `HousekeeperRunPolicyTests` / `DeviceHealthSummaryTests`），待具备 NuGet 访问的环境执行 `dotnet test` 验证。

B12 已落实 `016_v2.2_confirmation_batch_records.mysql.sql`：新建 `confirmation_batch_records`（`home_id + idempotency_key` 唯一，`confirmation_ids_json`/`result_json` 承载首次请求集合与结果供重放），并扩展 `family_audit_logs` 两条 CHECK（新增 `confirmation_confirm`/`confirmation_deny`/`confirmation_batch`/`activity_undo` 与 `confirmation_item`/`steward_activity`），C# 侧审计白名单与之一致。幂等重放不落在审计表：`FamilyAuditLogger` 写库失败仅记警告（best-effort 合规记录），不具备持久化重放源语义，故批量确认幂等独立建表。单项确认/拒绝不建幂等记录——确认项自身状态流转即去重（已确认 → 200 重放现有视图，已终态 → 409）。过期（`expires_at`）采用计算语义 `ExpiresAt == null || ExpiresAt > now`，不写 `expired_at` 回填（无后台 Worker）。撤销在当前 schema 下只做权限与资源状态复验（`AgentRun` 无连接器字段，Adapter 复验不可达），逆向命令执行与 Adapter 健康复验随运行期撤销管线（B13/B14）落地。

### 10.3 API 契约与确认规则

所有接口位于 `/api/v1`，从 JWT 推导家庭/租户归属。Controller 只完成鉴权、请求映射和 HTTP 响应；领域校验、软删除、冲突消解、审计与事务在服务层完成。

| 资源 | 路由 | B12 实施状态 |
| --- | --- | --- |
| 家庭成员 | `GET/POST /homes/{homeId}/members`、`PUT /homes/{homeId}/members/{memberId}`、`POST /homes/{homeId}/members/{memberId}/correction` | ✅ 已发布（B11）；`RequireHomeOwner` 过滤 homeId；终态更正三字段 + 写 `family_audit_logs` |
| 家庭知识库 | `GET /homes/{homeId}/knowledge?category=`、`POST /homes/{homeId}/knowledge`、`DELETE /homes/{homeId}/knowledge/{knowledgeId}` | ✅ 已发布（B11）；事务内 latest/authority/majority 冲突解决 + 审计 |
| 家庭决策历史 | `GET /homes/{homeId}/decisions`、`POST /homes/{homeId}/decisions` | ✅ 已发布（B11）；仅追加 + 游标分页 + 审计 |
| 管家动态 | `GET /homes/{homeId}/activities?limit=&cursor=`、`GET /homes/{homeId}/activities/{activityId}`、`POST /homes/{homeId}/activities/{activityId}/undo` | ✅ 已发布（B12）；游标分页（created_at+id，limit 上限 50）、详情、撤销（仅 undoable 已完成活动，`activity_undo` 审计） |
| 确认中心 | `GET /homes/{homeId}/confirmations?riskLevel=&status=`、`POST .../{id}/confirm`、`POST .../{id}/deny`、`POST /homes/{homeId}/confirmations/batch-confirm` | ✅ 已发布（B12）；单项确认/拒绝 + L1 批量确认（幂等键重放），均写审计与管家动态 |
| 设备健康 | `GET /smart-home/devices/health`；设备列表额外返回健康与 Zigbee 字段 | ✅ 已随 B10 发布；`GET /smart-home/devices/{id}/health` 单设备健康详情已随 B14 发布 |
| 管家工作台 | `GET /dashboard` 返回 `pendingConfirmations`、`stewardActivities`、`homeSummary`、`quickActions` | ✅ 已发布（B12）；新增 `pendingConfirmations`/`stewardActivities` 模块；`homeSummary` 对应既有 `Home` 模块；`quickActions` 为前端静态快捷入口，无后端数据源 |

B11/B12 只读接口暂用 `smart_home.read` 策略，写入接口暂用 `ai.run` 策略；B14 收敛为 `family.read`/`family.write`/`steward.activity.read`/`confirmation.read`/`confirmation.write` 独立权限。审计表 `family_audit_logs`（`015` + `016` 迁移）与 `steward_activities` 分离：家庭域合规审计走 `family_audit_logs`，产品层管家动态走 `steward_activities`。

单项确认请求携带 `idempotencyKey`（仅校验 UUID 格式，去重由确认项状态流转保证：已确认 → 200 重放现有视图且不重复审计，已终态/过期 → 409）。批量确认的正式契约为 `POST /api/v1/homes/{homeId}/confirmations/batch-confirm`，请求体为 `{ confirmationIds: number[], idempotencyKey: uuid }`（1-50 项）；服务端先在一个事务中验证所有 ID 都属于当前 JWT 家庭、均为未过期 `pending` 的 L1 项且无重复 ID，再原子确认并返回每项确认结果。任一 L2/L3、跨家庭、已终态、过期或重复 ID 都必须整体拒绝（404/409/422），不能以部分成功绕过风险策略；同一幂等键仅返回首次记录的结果（`confirmation_batch_records` 持久化重放，同键异集 → 409）。错误码映射：形状非法（键非 UUID/空列表/重复 ID/超 50 项）→ 422 `10001`；任一 ID 跨家庭 → 404 `30000`；任一违规项或同键异集 → 409 `40000`；同键同集重放 → 200 `0`。L2 和 L3 一律逐项确认，L3 不允许被托管策略自动执行。`undo` 只接受 `undoable=true` 的已完成活动，撤销前实时复验权限与资源状态（状态/undoable/未撤销），`AgentRun` 无连接器字段故不调用 Adapter；逆向命令执行与 Adapter 健康复验随运行期撤销管线（B13/B14）落地。所有确认、拒绝、批量确认和撤销都要写入审计和可展示的管家动态。推送聚合约束（不合并或延迟 L2/L3、安防及直接点名提醒）为契约约束，当前无推送服务代码。

### 10.4 权限、前端同步与验收

B14 已收敛权限为 `family.read`、`family.write`、`steward.activity.read`、`confirmation.read`、`confirmation.write`（另随 V2.3 预注册 `life.favorite.read`/`life.favorite.write`）：`FamilyController` 只读/写入分别使用 `family.read`/`family.write`，`StewardController` 动态读取使用 `steward.activity.read`、确认中心读取使用 `confirmation.read`、撤销/确认/拒绝/批量确认使用 `confirmation.write`；`MemberPermissions` 覆盖全部写权限，`ViewerPermissions` 覆盖只读权限。跨家庭资源一律返回 `404`；拒绝、确认和写操作保留操作者与时间，任何 DTO、日志或错误响应均不得泄漏凭据、供应商原始字段、Prompt 或模型思考链。

推送服务消费的是已授权、脱敏后的管家活动和确认项，而不是 Adapter 原始事件。它必须按同类资源、一次场景/Run、30 分钟低风险时间窗口和早晚周期摘要聚合；L2/L3、安防风险和被直接点名的提醒绕过低风险合并，并保留来源活动引用与成员偏好判定。

实现 API 前必须将字段级请求、响应、错误和幂等示例同步到 `docs/frontend-api-integration.md` 与 `docs/api-implementation.md`。验收应覆盖迁移、租户隔离、成员状态机与终态更正、L1 批量确认的原子限制、L2/L3 逐项确认、知识冲突消解、设备健康映射、推送聚合不吞没高风险事项、可撤销活动、Dashboard 局部失败，以及 Adapter 业务层不依赖 HA 具体实现。

## 11. V2.3 单人全流程治理基线

V2.3 在承接 V2.2 家庭协同、三级风险、知识与设备健康契约的同时，新增个人生活专家（探店翻牌、行程规划）与单人全流程治理基线。治理要求让产品决策、后端设计、实施计划和代码状态保持同一事实链，防止实现细节反向改变产品语义。

### 11.1 来源优先级与文档职责

产品总设计是产品内容的唯一最终输出。后端文档只将其中已经确认的领域模型、存储、API、授权、幂等、审计、Connector 适配和运行时约束拆解为可实施设计；不得自行改变产品范围、五 Tab 语义、L1/L2/L3 风险规则、确认策略或 Smart Home 仅作为 Connector 的边界。

```text
产品总设计
  ↓ 已确认的领域、数据、API 与安全要求
后端开发设计（本文）
  ↓ 当前实现状态、依赖与最小验收
后端开发计划
  ↓
代码、迁移、测试、API 契约与完成状态
```

实现中发现产品语义冲突、风险等级变化、数据保留范围变化或跨端契约不可行时，必须先回到产品总设计记录决策；不得只修改 Controller、迁移、API 文档或开发计划来绕过该流程。

后续开发必须严格执行后端开发计划中当前切片的依赖、优先级和最小验收。每次实施、手工代码调整或缺陷修复后，先更新计划的实施状态、下一步和验证结果，再在同一变更中回写本文及产品总设计中受影响的技术设计、产品约束和实施状态。出现 Bug、测试不通过或契约偏差时，不得只修复代码：必须先调整计划，再同步调整设计；如涉及产品语义，仍以产品总设计先行决策为准。

### 11.2 后端交付门禁

每个后端开发切片关闭前必须满足：

1. 迁移、实体、服务、路由和权限均保持 JWT `tenant_id` 隔离，客户端不能指定家庭归属；
2. 写操作遵守风险取最高等级的规则，L1 批量确认、L2/L3 逐项确认、实时复验和幂等语义不被任何自动化或 Adapter 绕过；
3. API、审计、日志和推送输入只包含脱敏后的标准化领域数据，不包含凭据、供应商原始字段、Prompt 或模型思考链；
4. 受影响的 `docs/frontend-api-integration.md`、`docs/api-implementation.md` 和后端开发计划在同一变更中更新；计划仅保留当前已完成、下一步和最小验收，不作为变更历史；
5. 完成迁移验证、相关单元/集成测试和构建，并记录未执行的外部依赖验证（例如 MySQL、Vault、Home Assistant 或 MQTT）。

### 11.3 当前实施边界

当前实现序列为 V2.3 的 B16-B18 与创作者中心 MCP Bridge 验证收口：在已完成的 `personal_favorites` 迁移与 CRUD 基线上，完成 `personal-life-expert` 注册、翻牌/行程 Run 链路与日历同步联调、AI 配置启用开关（B18），并以受限 `ai.read`/`ai.skills.read` Token 完成 MCP Bridge 的显式同步、离线只读与敏感数据双重开关运行期验证（含修复 Windows stdio 管道未显式 UTF-8 导致中文查询乱码的缺陷）。B9-B15、Expert Files 和多专家团队编排已完成，后续不得因家庭管家或个人生活专家扩展绕过既有 Run Action、确认、权限交集或审计链路。

## 12. V2.3 个人生活专家增量设计

V2.3 前置“个人生活专家”（code=`personal-life-expert`，category=`life`），首期交付探店翻牌与行程规划。推荐与行程复用既有 Expert Run、Run Action、确认与审计链路，不新建运行时；后端新增个人偏好数据层与 CRUD API。OCR 第三方截图识别与短视频生成不进入本期。

### 12.0 B18 AI 配置启用开关

为落实「设置：AI 配置」三态交互（新增/只读/编辑）与只读态启用开关，`ai_configs` 表新增 `enabled TINYINT(1) NOT NULL DEFAULT 1` 列（迁移 `database/023_ai_config_enabled.mysql.sql` 与 EF Migration `20260806021643_AddAiConfigEnabled`），`AiConfigRequest` 扩展 `Enabled` 字段，`AiConfigController` 的 `GET/PUT /api/v1/ai/config` 返回与接收 `enabled`；切换开关不传 `apiKey` 即可保留密文。

闸门新增 `IAiConfigServices.IsEnabledAsync(userId)`，统一在两处消费：

1. `AiCapabilitiesController` 占位 `POST /api/v1/ai/{generate,chat,stream}`（权限 `ai.run`）：调用前校验，未启用 → HTTP 422 / `Code=42200` `Msg="AI 生成能力已禁用，请在设置中开启。"`；启用 → 暂返 HTTP 501 占位。
2. `AgentRunProcessor` 在调用 `ILLMClient` 之前校验，未启用 → `FailAsync(LlmErrorCodes.AiConfigDisabled, "AI 生成能力已禁用，请在设置中开启。")`，终态为 `failed`，不消耗模型配额。

`LlmErrorCodes.AiConfigDisabled = "ai_config_disabled"` 与既有 `AiConfigMissing` 区分语义（缺配置 vs 主动禁用），错误码与 `Code=42200` 仍由客户端解析。文档同步：`docs/api-implementation.md` §响应与错误码（增加 `42200`）、§鉴权策略（`ai.config.read/write` 字段说明）、§模块表（AI 配置行）；`docs/frontend-api-integration.md` §AI 配置示例与 §9 权限汇总待前端 PR 同步。

定向验证：`dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/b18-verify` 通过（0 errors）；`dotnet test` 全绿 81/81（新增 `AiConfigServicesTests` 5 项：未配置 → 不可用、默认启用 → 可用、显式禁用 → 不可用、缺省/空字符串 `apiKey` 保留密文）；真实 MySQL `023` 顺序迁移与 AI 端到端按部署环境验证。

### 12.1 数据模型与迁移

新增 `017_v2.3_personal_favorites.mysql.sql`：

| 字段 | 约束 / 说明 |
| --- | --- |
| `home_id` | FK `tenants.id`，JWT 隔离，禁止客户端覆盖 |
| `owner_member_id` | FK `family_members.id` 且同 home；收藏默认归属成员本人 |
| `category` | CHECK `restaurant` / `travel` / `material`（material 为后续短视频素材预留） |
| `name` | 店铺/地点/素材名称，列表展示用 |
| `detail_json` | 结构化扩展信息：cuisine、address、lat、lng、tags、note、source（如小红书/大众点评） |
| `visibility` | CHECK `private` / `family`，默认 `private` |
| 通用列 | `deleted_at`、`created_at`、`updated_at`、`row_version`；索引 `(home_id, owner_member_id, category, updated_at)` |

同一迁移扩展 `family_audit_logs`：action CHECK 增加 `favorite_create` / `favorite_update` / `favorite_delete` / `favorite_import`；target_type CHECK 增加 `personal_favorite`。C# 侧审计白名单与 CHECK 同步。

### 12.2 模块与服务边界

```text
HomeMind.Business.IServices/Life/
  IFavoriteService.cs
HomeMind.Business.Services/Life/
  FavoriteService.cs
```

`IFavoriteService` 负责收藏的 CRUD、可见性过滤（`private` 仅 `owner_member_id` 本人可读写；`family` 家庭内可读，写仍限本人或家庭管理员）、软删除与审计。翻牌与行程的读取由 Expert Skill 经该服务完成，Controller 不直接查询实体。

### 12.3 Expert 与 Skill

`017` 迁移注册 `personal-life-expert`（category=`life`）及首个 `expert_versions`（version=1），并声明 Skill：`favorite.recommend`（翻牌：输入时间/位置/口味 → 输出 Top1-2 店铺与理由）、`trip.plan`（行程：目的地/天数/偏好 → 每日安排，引用私藏库与天气）、`favorite.create`（AI 从对话提取收藏录入，记录来源）。行程的日历同步复用既有 Calendar 服务与 `calendar_create_event` Run Action，不新建日历能力。

**B16/B17 已落实：** 翻牌与行程由 `LifeExpertRunServices` 确定性编排（B16 翻牌只读 L1；B17 行程产出 `calendar_create_event` Run Action 并经确认执行），Skill 声明写入 `expert_versions.tool_policy_json`；AI 对话提取（`favorite.create`）与模型推理依赖 AI 运行时，按部署环境验证。

### 12.4 API 契约

| 资源 | 路由 | B17 实施状态 |
| --- | --- | --- |
| 收藏管理 | `GET /api/v1/life/favorites?category=&visibility=`、`POST /api/v1/life/favorites`、`PUT /api/v1/life/favorites/{id}`、`DELETE /api/v1/life/favorites/{id}` | ✅ 已发布（B15）；`owner_member_id` 默认解析当前成员；`DELETE` 软删除并审计 |
| 对话导入 | `POST /api/v1/life/favorites/import` | ✅ 已发布（B15）；结构化导入 + 来源留痕审计；AI 对话提取依赖 AI 运行时 |
| 翻牌 / 行程 | `POST /api/v1/experts/personal-life-expert/runs` | ✅ 已发布（B16/B17）；`intent=recommend` 只读 L1 返回 Top1-2 建议；`intent=plan` 生成 `calendar_create_event` 动作 |
| 行程确认 | `POST /api/v1/experts/personal-life-expert/runs/{runId}/actions/{actionId}/confirm` | ✅ 已发布（B17）；L1 确认后逐日创建日历事件，复用确认/幂等（`ActionExecutionAudits`，`018` 迁移放宽连接器/设备列为 NULL）/审计链路 |

权限采用 `life.favorite.read` / `life.favorite.write`（B14 已收敛发布），专家运行沿用 `ai.run`；跨家庭与越权访问一律 `404`。

### 12.5 验收

- `017` 迁移可在真实 MySQL 顺序执行；`personal_favorites` 按 `home_id` + `owner_member_id` 过滤，`private` 不泄露给家庭其他成员；✅ 定向测试覆盖可见性/软删/审计/翻牌/行程/确认执行；
- 收藏 CRUD、可见性、软删除与 `favorite_*` 审计通过测试；AI 导入（`favorite.create`）与翻牌（`favorite.recommend`）能经 Expert Run 产出可解释建议；— AI 部分依赖 AI 运行时，按部署环境验证；
- 行程规划生成的日历同步以 Run Action 确认/幂等/审计链路执行，不绕过既有边界；✅ 已落实（B17）；
- 前端字段级契约同步至 `frontend-api-integration.md` 与 `api-implementation.md`；构建 0 errors、0 CS1591。✅

## 13. V2.4 Connector Scope 与 Web 治理

迁移新增 `workspace_connectors.binding_scope`、`owner_user_id` 与 `connector_authorization_sessions`。数据约束保证家庭实例 owner 为空、个人实例 owner 非空且属于同一 active tenant member；创建 Run 时将实例 scope/owner 纳入权限快照，Action 确认和执行前实时复验。现有 `users`、`tenants`、`tenant_members.role` 已满足账户、成员与固定 `owner/admin/member/viewer` 角色，不新增可编辑角色/权限表。

Web 路由由前端版本发布，服务端权限码始终为唯一授权依据；不持久化 API 路由。若后续需要每家庭菜单个性化，新增独立 `web_navigation_preferences`，只允许已发布 `route_key` 的显隐/排序，不保存 URL、权限表达式或脚本。V2.4 API、迁移、服务与 OAuth 安全验证完成前，移动端/Web 只消费现有家庭级 Connector 契约。

**B18 实施状态（2026-08-07）**：迁移 `024_v2.4_connector_scope.mysql.sql`（workspace_connectors 四列 + connector_authorization_sessions + expert_runs.permission_snapshot_json + family_audit_logs CHECK 扩展 + mock_oauth Provider）与 EF 迁移 `AddConnectorScopeAndAuthSessions` 已落地；`IConnectorAuthorizationServices`（发起/服务端回调/状态/撤销，state 哈希 + PKCE 密文引用 + 会话单次使用与 10 分钟过期 + 回调白名单）已发布；新增权限名 `connector.authorize`（owner/admin/member）；`WorkspaceConnectorView` 携带 `BindingScope`/`IsCurrentUserOwner`，personal 实例仅 owner 可见；Run 创建写入权限快照（scope/owner），Action 确认前复验（personal 仅 owner、household 实时复验 grant）；Mock OAuth Provider（`mock_oauth`）供开发/测试端到端验证；`dotnet build` 0 errors/0 CS1591，`dotnet test` 94/94；真实 MySQL 024 顺序迁移与真实 Provider OAuth 按部署环境验证。

### 13.1 专家会话增量（专家对话框）

会话化将专家交互从「单次运行」扩展为「会话（对话框）」：移动端纯对话、遍历询问，PC 端承载维护与运行细节。新增迁移 `conversations`、`conversation_messages`，并扩展 `experts.owner_user_id` 与 `expert_runs.conversation_id`（可空）。`experts.owner_user_id` 为空表示平台基础专家（开发端维护、全家可见），非空表示用户自建专家（PC 用户端维护、仅创建者本人可见）。会话发送消息复用既有 `IExpertRunService` 创建 Run（携带 `conversation_id`），消息历史即会话上下文（运行时拼接，复用 `input_context` 语义），终态后写入 `assistant` 消息并保留 `run_id`；跨用户/跨租户访问一律 404。

API：`GET/POST /conversations`、`GET/PUT/DELETE /conversations/{id}`（软删除+审计）、`GET /conversations/{id}/messages`（游标分页）、`POST /conversations/{id}/messages`（发送）；`GET /experts?scope=basic|mine|all` 区分平台基础专家与本人自建专家。权限：`conversation.read/write`（仅本人会话）与 `expert.mine.read/write`（仅本人自建专家）。验收：迁移、归属隔离、上下文拼接、消息追溯（run_id）、scope 过滤与跨用户 404；字段级契约同步 `frontend-api-integration.md` 与 `api-implementation.md`。
