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
- 数据库字段中文备注规范：`database/` 下所有迁移的每个字段必须带 `COMMENT '中文备注'`（简体中文、面向业务语义；枚举列注明取值、JSON 列注明结构、凭据列不暴露敏感信息），新建与修改迁移一律遵守；存量字段由 `032` 迁移统一补齐，已执行迁移不修改。

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
| 第六阶段 | 专家会话与自建专家（B20+B21，已全部发布） | `conversations`/`conversation_messages` 迁移与实体、`experts.owner_user_id` 扩展与 `deleted_at`、`expert_runs.conversation_id`、`IConversationService`（会话 CRUD/消息/上下文拼接）、会话与消息 API、`GET /experts?scope=basic\|mine\|all` 过滤与自建专家 CRUD；验收：归属隔离、上下文拼接、消息追溯（run_id）、scope 过滤与跨用户 404 |
| 第七阶段 | 场景工作流（B22，已全部发布） | `scenario_templates`/`scenario_instances` 迁移与实体、Device Resolver 实例化（type+room+capability、缺设备容忍）、场景运行单 Action metadata 承载步骤、确认后逐步执行与 success/partial/failed 汇总、旧场景路由兼容代理；验收：实例化与缺设备容忍、状态计算规则、确认幂等重放、旧 sceneKey 懒启用不中断 |

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

定向验证：`dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/b18-verify` 通过（0 errors）；`dotnet test` 全绿 81/81（新增 `AiConfigServicesTests` 5 项：未配置 → 不可用、默认启用 → 可用、显式禁用 → 不可用、缺省/空字符串 `apiKey` 保留密文）；真实 MySQL `023` 顺序迁移已在本机执行验证；AI 端到端仍按部署环境验证。

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

**B18 实施状态（2026-08-07）**：迁移 `024_v2.4_connector_scope.mysql.sql`（workspace_connectors 四列 + connector_authorization_sessions + expert_runs.permission_snapshot_json + family_audit_logs CHECK 扩展 + mock_oauth Provider）与 EF 迁移 `AddConnectorScopeAndAuthSessions` 已落地；`IConnectorAuthorizationServices`（发起/服务端回调/状态/撤销，state 哈希 + PKCE 密文引用 + 会话单次使用与 10 分钟过期 + 回调白名单）已发布；新增权限名 `connector.authorize`（owner/admin/member）；`WorkspaceConnectorView` 携带 `BindingScope`/`IsCurrentUserOwner`，personal 实例仅 owner 可见；Run 创建写入权限快照（scope/owner），Action 确认前复验（personal 仅 owner、household 实时复验 grant）；Mock OAuth Provider（`mock_oauth`）供开发/测试端到端验证；`dotnet build` 0 errors/0 CS1591，`dotnet test` 94/94；真实 MySQL 024 顺序迁移已在本机执行验证；真实 Provider OAuth 仍按部署环境验证。

### 13.1 专家会话增量（专家对话框）

会话化将专家交互从「单次运行」扩展为「会话（对话框）」：移动端纯对话、遍历询问，PC 端承载维护与运行细节。新增迁移 `conversations`、`conversation_messages`，并扩展 `experts.owner_user_id` 与 `expert_runs.conversation_id`（可空）。`experts.owner_user_id` 为空表示平台基础专家（开发端维护、全家可见），非空表示用户自建专家（PC 用户端维护、仅创建者本人可见）。会话发送消息复用既有 `IExpertRunService` 创建 Run（携带 `conversation_id`），消息历史即会话上下文（运行时拼接，复用 `input_context` 语义），终态后写入 `assistant` 消息并保留 `run_id`；跨用户/跨租户访问一律 404。

API：`GET/POST /conversations`、`GET/PUT/DELETE /conversations/{id}`（软删除+审计）、`GET /conversations/{id}/messages`（游标分页）、`POST /conversations/{id}/messages`（发送）；`GET /experts?scope=basic|mine|all` 区分平台基础专家与本人自建专家。权限：`conversation.read/write`（仅本人会话）与 `expert.mine.read/write`（仅本人自建专家）。验收：迁移、归属隔离、上下文拼接、消息追溯（run_id）、scope 过滤与跨用户 404；字段级契约同步 `frontend-api-integration.md` 与 `api-implementation.md`。

**B20 实施状态（2026-08-07）**：迁移 `026_expert_conversations.mysql.sql`（conversations + conversation_messages + expert_runs.conversation_id + family_audit_logs CHECK 扩展 3 动作/1 目标）与 EF 迁移 `AddConversations` 已落地（EF 迁移仅建表/加列，不含 CHECK，遵循仓库 Surgical Changes 约定，不触碰既有 schema 漂移、不更新模型快照）；`Conversation`/`ConversationMessage` 实体与 DbSet 已发布；`IConversationServices`/`ConversationServices`（会话 CRUD、软删除与 `conversation_*` 审计、消息游标分页、上下文拼接：最近 20 条 + 12000 字符预算，产出 `{"messages":[...]}` 作为 input_json；`RecordUserMessageAsync`/`AppendAssistantMessageAsync` 按 `(conversation_id, run_id)` 幂等）已发布；`AgentRunCreateRequest` 扩展可选 `conversationId`，`AgentRunServices.CreateAsync` 校验会话归属（非本人/已软删 404、同键异会话 409）并落库，`AgentRunView` 类型化取代匿名投影；`AgentRunProcessor` 终态（completed/failed）后向会话追加 assistant 消息（内容取 result_summary/错误消息，取消态不追加），无事件总线、复用既有 Worker 轮询；`ConversationsController` 发布 7 个端点（`conversation.read/write`，路由无 homes 前缀，JWT 推导归属）；权限名 `conversation.read/write`、`expert.mine.read/write` 注册（后者 B21 消费）；错误码 `40903`（会话/自建专家乐观锁）；`dotnet build` 0 errors/0 CS1591，`dotnet test` 全绿 133/133（新增 ConversationServicesTests 16 项 + AgentRunConversationTests 3 项）；真实 MySQL 026 顺序迁移已在本机执行验证；真实 AI 推理端到端会话仍按部署环境验证。

**B21 实施状态（2026-08-07）**：迁移 `027_expert_self_serve.mysql.sql`（`experts.deleted_at` 软删除列；owner_user_id/created_at/updated_at/row_version 自 002 已有，B21 补齐实体映射；无审计 CHECK 扩展，§13.1 仅要求会话审计）与 EF 迁移 `AddExpertDeletedAt` 已落地；`Expert` 实体补 `CreatedAt`/`UpdatedAt`/`DeletedAt`/`RowVersion`（`[ConcurrencyCheck]`）映射与 DbContext 时间戳配置；`ExpertCatalogItemView`/`ExpertDetailView`（Source=basic|mine，不暴露他人 owner）与创建/更新请求 DTO 已发布；`IExpertCatalogServices.ListAsync` 新增 `scope=basic|mine|all`（默认 basic 向后兼容，叠加 active+未删除+租户可见），`GetAsync` 按 all 语义过滤（他人自建/已软删 404）；`IExpertSelfServeServices`/`ExpertSelfServeServices`（创建：字段校验 422/10001、ToolPolicyJson 合法 JSON 422、code=`custom-`+8 位 hex 撞键重生成、Expert{custom/active/owner=本人} + ExpertVersion v1 published、不写审计；更新：归属 404、RowVersion 409/40903、头部字段替换 + 生成 version+1 published 新版本；删除：软删除）；`AgentRunServices.ResolveSourceAsync` 与 `ConversationServices.ResolveExpertAsync` 补 `DeletedAt==null`（已删专家从目录/运行解析/会话发送全链路消失）；`ExpertsController` 新增 `GET /experts?scope=`（默认 basic）、`POST /experts`、`PUT/DELETE /experts/{id}`（`expert.mine.read/write`）；`dotnet build` 0 errors/0 CS1591，`dotnet test` 全绿 141/141（新增 ExpertSelfServeTests 8 项）；真实 MySQL 027 顺序迁移已在本机执行验证，`GET /api/v1/experts` 端到端 200 验证通过；真实 Web 端「我的专家」接入仍按部署环境验证。

## 14. V2.4 B19 Web 治理 API

### 14.1 迁移与实体

`database/025_v2.4_web_governance.mysql.sql`（`-- Apply after 024`）新增：

- `tenant_member_invitations`：`subject_kind`（固定 `phone`）+ `subject_hash`（BINARY(32)，与 `user_identities.subject_hash` 同口径 SHA-256、无 pepper）、`proposed_role`（CHECK `admin`/`member`/`viewer`，不得为 `owner`）、`status`（`pending`/`accepted`/`expired`/`revoked`）、`expires_at`（默认 7 天）、`accepted_user_id`/`accepted_at`/`revoked_at`、`row_version`；`UNIQUE(tenant_id, subject_hash)` 保证同手机号在同一家庭仅一条 pending 邀请。
- `web_navigation_preferences`：`role`（CHECK 四角色）+ `route_key`（`UNIQUE(tenant_id, role, route_key)`）+ `enabled`/`sort_order`/`updated_by_user_id`；`route_key` 白名单由应用层 `NexusWebNavigationKeys` 强制，数据库不做枚举。
- `family_audit_logs` 两 CHECK 扩展：action 增加 `tenant_member_role_changed`/`tenant_member_status_changed`/`tenant_invitation_created`/`tenant_invitation_revoked`/`tenant_invitation_accepted`/`tenant_owner_transferred`/`web_navigation_preference_updated`；target_type 增加 `tenant_member`/`tenant_invitation`/`web_navigation_preference`。

实体 `TenantMemberInvitation`/`WebNavigationPreference` 位于 `HomeMind.Common.Model/Entities/IdentityEntities.cs`；`TenantMember` 增加 `RowVersion`（`[ConcurrencyCheck]`）支撑角色/状态变更乐观锁；`HomeMindDbContext` 注册两个 DbSet。EF 迁移 `AddWebGovernanceTables` 仅新增两表，不触碰已发布 schema。

### 14.2 静态白名单

`HomeMind.Common.Infrastructure/Constants/WebNavigationKeys.cs` 定义 `NexusWebNavigationKeys`：8 个已发布 route_key（`tenant.dashboard`/`tenant.confirmations`/`tenant.steward`/`tenant.knowledge`/`tenant.family`/`tenant.life`/`tenant.connectors`/`tenant.connector.authorize`），`All` 为显示顺序单一真相源，`DefaultSortOrder` 提供默认排序，`IsKnownRouteKey` 供偏好写入校验。白名单是编译期常量，不随 appsettings 变化。

### 14.3 权限

- `PermissionNames.TenantRead = "tenant.read"`：owner/admin/member/viewer 均可读成员/邀请/导航。
- `PermissionNames.TenantMemberManage = "tenant.member.manage"`：owner/admin 专享；`PermissionAuthorizationHandler` 新增 `OwnerAdminOnly` 分支，`member`/`viewer` 直接拒绝。
- 写操作（角色变更、状态停启、邀请创建/撤销、owner 转让、导航偏好）一律写 `family_audit_logs`，actor 为 JWT 当前用户。

### 14.4 业务服务与契约

| 服务 | 位置 | 关键规则 |
| --- | --- | --- |
| `ITenantMemberServices` | `HomeMind.Business.IServices/Identity/` | 角色变更拒绝直接置 owner（422/42202）；状态停启不得停用最后一名 active owner（422）；owner 转让仅 active owner 可发起（403），同事务更新 `tenants.owner_user_id` + 旧 owner 降 `admin` + 新 owner 升 `owner`（422/42201 拒 suspended 受让方）；全部写操作 `row_version` 乐观锁（409/40901） |
| `ITenantMemberInvitationServices` | 同上 | 创建（手机号 E.164 规范化后 SHA-256，7 天过期，同标识 pending 409/40902）；列表按状态过滤、过期按计算语义不写回填；撤销仅 pending（非终态 409）；接受：手机号哈希须匹配当前账户已验证 `user_identities`（未匹配/未验证/吊销统一 404/30001），成功创建 active `tenant_members` 并写 `tenant_invitation_accepted` 审计 |
| `IWebNavigationPreferencesServices` | 同上 | GET 合并白名单 + 当前角色偏好（未持久化默认 enabled=true + 默认 sort_order）；PUT 仅接受 `NexusWebNavigationKeys` 内 route_key（422/42203），upsert 后写 `web_navigation_preference_updated` 审计 |
| `IConnectorServices.ListMyPersonalConnectionsAsync` | `HomeMind.Business.Services/SmartHome/ConnectorServices.cs` | 仅当前用户作为 owner 的 personal 实例 + 最近一次 `ConnectorAuthorizationSession` 状态，不返回凭据引用 |

### 14.5 路由与权限矩阵

| 路由 | 权限 | 说明 |
| --- | --- | --- |
| `GET /api/v1/homes/{homeId}/members` | `tenant.read` | 成员列表（账户资料 + 角色/状态/行版本） |
| `PUT /api/v1/homes/{homeId}/members/{memberUserId}/role` | `tenant.member.manage` | 角色变更；拒置 owner |
| `PUT /api/v1/homes/{homeId}/members/{memberUserId}/status` | `tenant.member.manage` | 停启；最后一名 owner 守恒 |
| `POST /api/v1/homes/{homeId}/owner-transfer` | `tenant.member.manage` | owner 转让（同事务） |
| `GET /api/v1/homes/{homeId}/invitations?status=` | `tenant.read` | 邀请列表 |
| `POST /api/v1/homes/{homeId}/invitations` | `tenant.member.manage` | 创建邀请 |
| `DELETE /api/v1/homes/{homeId}/invitations/{invitationId}` | `tenant.member.manage` | 撤销邀请 |
| `POST /api/v1/invitations/accept` | `tenant.read` | 受邀人接受（家庭由邀请记录推导，不套 homeId） |
| `GET/PUT /api/v1/web/navigation` | `tenant.read` / `tenant.member.manage` | Web 导航偏好读取/写入 |
| `GET /api/v1/connector-authorizations/my` | `connector.authorize` | 我的个人连接汇总 |

`{homeId}` 一律经 `RequireHomeOwner` 校验等于 JWT tenant_id，跨家庭 404+30000；接受路由不依赖 homeId，跨家庭邀请按哈希不匹配 404。

### 14.6 Swagger Tag

新控制器归属：`TenantMembers`/`TenantMemberInvitations`/`TenantMemberInvitationAccept` → `家庭上下文 / 成员受控管理`/`成员邀请`；`WebNavigation` → `Web / 导航偏好`。

### 14.7 验收

- `dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/b19-verify` 通过（0 errors / 0 CS1591）；
- `dotnet test` 全绿 114/114（B19 新增 20 项，覆盖：拒置 owner、乐观锁、最后一名 owner 守恒、转让同事务、suspended 受让方、邀请哈希/唯一/过期/撤销/接受验证、导航白名单/偏好覆盖、个人连接隔离）；
- 真实 MySQL 025 顺序迁移已在本机执行验证；Web 前端接入仍按部署环境验证。

## 15. V2.4 场景工作流（B22）

场景工作流是「场景 = Run 的一种特殊输入」的落地：平台模板 → 家庭实例 → Run 执行。执行引擎保持硬编码（复用 AgentRun / ExpertRunAction / ActionExecutionAudits 确认、幂等、审计与撤销链路），内容配置化（模板与实例存库），第一阶段不新增 Step 表、不新增独立引擎、不改动作状态机。

### 15.1 迁移与实体

`database/028_scenario_workflow.mysql.sql`（`-- Apply after 027`）新增：

- `scenario_templates`：平台级模板（`tenant_id` 固定 1，与平台专家同惯例）、`code` 唯一业务键、`trigger_keywords_json`（语音入口关键词，本阶段仅快照不消费）、`steps_json`（未解析步骤：id/name/device_type/room/capability/value/optional，`room="*"` 不限房间）、状态/软删除/sync_version。
- `scenario_instances`：家庭启用实例，`template_code`、关键词快照、`steps_json`（解析后附加 `device_id`/`step_status`（`ready`/`unavailable`）/`reason`）、`status`（`enabled`/`disabled`）、`created_by_user_id`、`row_version` 乐观锁、软删除。
- 种子：`goodnight`（晚安）/`arrive_home`（回家）/`leave_home`（离家）三个模板，步骤语义对齐既有管家意图；`ON DUPLICATE KEY UPDATE` 幂等重放。

实体 `ScenarioTemplate`/`ScenarioInstance` 位于 `HomeMind.Common.Model/Entities/SmartHome/ScenarioEntities.cs`；`HomeMindDbContext` 注册两个 DbSet，JSON 列 `HasColumnType("json")`。EF 迁移 `AddScenarioWorkflow` 仅建两表（无 CHECK、不更新快照），遵循 Surgical Changes 约定。**既有表零改动**：`expert_runs`/`expert_run_actions` 不加列——`RequestJson` 即定稿的 metadata，`result_json` 承载 Run Result。

### 15.2 服务与执行链路

`IScenarioWorkflowServices`/`ScenarioWorkflowServices`（`HomeMind.Business.IServices|Services/SmartHome/`）：

- **Enable**：Device Resolver 按 `device_type + room + capability` 匹配家庭设备（空间按 `space_type` 匹配，多台取 Id 最小），能力需 `is_writable`；无匹配 → 步骤 `unavailable` + 原因（`no matching device`/`no matching capability`），**启用仍成功**（Enable-time tolerant）；重复启用返回既有实例。
- **Run**：幂等键检查（复用 B17 模式）→ 创建 `AgentRun`（SourceType=`scenario`、Mode=steward、AutoConfirmPolicy=L3Only、权限快照）→ 创建**单个** `ExpertRunAction`（ActionType=`scenario`，RequestJson=`{scenario_id, scenario_name, steps:[...]}`，unavailable 步骤 status=`skipped`）→ `pending_actions`。场景风险 = MAX(步骤风险)，静态基线：lock/camera/security_alarm 或 lock 能力 → L3，其余 L1。
- **Confirm**：复用 B17 骨架（UUID 幂等键、ActionExecutionAudits 重放、权限快照复验、pending→executing）→ 逐步经 `CommandRelayService.ExecuteAsync` 下发设备命令（逐步复验设备在线/能力/连接器可用/用户授权/Provider 支持），**required 步骤失败后继续后续步骤**；执行期跳过 `skipped` 步骤。

### 15.3 状态计算规则

```
skipped（含 unavailable）步骤不参与成功/失败计数
required_failed = required 步骤中 status ∈ {failed, timeout} 的数量
无任何成功步骤                       → Run 结果 failed,   Action = failed
required_failed > 0 且存在成功步骤   → Run 结果 partial,  Action = executed
required_failed == 0（optional 失败不影响）→ Run 结果 success,  Action = executed
```

`AgentRun.Status` 保持生命周期语义（`pending_actions` → `completed`）；`success/partial/failed` 写入 `run.result_json`（`{scenario, status, summary, success_count, failed_count, failed_steps:[{name, reason}]}`），`result_summary` 为中文摘要。**消费方契约**：Push 读 `summary`、Dashboard 读 `status`、反馈读 `failed_steps`；消费方禁止解析 steps 明细 JSON。

### 15.4 API 与权限

`ScenarioController`（`api/v1/smart-home/scenarios`，Swagger Tag `智能家居 / 场景工作流`）：

| 路由 | 权限 |
| --- | --- |
| `GET /scenarios/templates`、`GET /scenarios/instances` | `smart_home.read`（owner/admin/member/viewer） |
| `POST /scenarios/templates/{templateCode}/enable` | `smart_home.write`（新增，owner/admin/member） |
| `POST /scenarios/instances/{instanceId}/disable` | `smart_home.write`（B23，owner/admin/member） |
| `POST /scenarios/instances/{instanceId}/run` | `ai.run` |
| `POST /scenarios/runs/{runId}/actions/{actionId}/confirm` | `ai.run` |

实例状态流转（B23）：`enabled ↔ disabled`。禁用置 `status=disabled`、
`updated_at` 刷新，重复禁用幂等返回 200；禁用只阻止新触发（Run 查询
已含 `Status=enabled`，禁用后自动 404「不存在或未启用」），已创建的
待确认运行不受影响；`EnableAsync` 对已禁用实例恢复 `enabled` 并返回
「场景实例已重新启用。」；不写审计（与 Enable 对称）、不校验
row_version（与既有 Enable/Run 一致）。

旧场景路由兼容代理：`SmartHomeSceneServices.RunAsync(sceneKey)` 经 `SmartHomeSceneDefinitions.TryGetIntent` 校验后**懒启用**对应模板实例（sceneKey 即模板 code）并转调场景运行链路；`HandleSceneCompletedAsync` 发布保留，`automation_rules` 的 sceneKey 动作引用零改动；`scenes`/`scene_actions` 读模型保留（前端已接入契约与 Dashboard `Scenes` 模块不变）。

### 15.5 演进门槛（YAGNI）

`scenario_steps` 表仅在出现运营（哪一步失败最多）/用户（为什么场景常失败）/step SLA 与重试分析需求时新增；拖拽编排仅在出现「用户想自己创建场景」而非「启用场景」需求时启动；语音入口（Expert → Scenario Match → Run）在真实意图消费者接入时复用同一 `RunAsync` 执行器，不做重复实现。

**B22 实施状态（2026-08-07）**：迁移 `028_scenario_workflow.mysql.sql` 与 EF 迁移 `AddScenarioWorkflow` 已落地；`ScenarioTemplate`/`ScenarioInstance` 实体、`IScenarioWorkflowServices`/`ScenarioWorkflowServices`（模板/实例列表、Enable Device Resolver 容忍缺设备、Run 单 Action metadata、Confirm 逐步执行与状态汇总）与 `ScenarioController` 5 条路由已发布；权限 `smart_home.write` 注册（owner/admin/member）；`SmartHomeSceneServices` 兼容代理（懒启用 + scene_completed 发布保留）已发布；`dotnet build` 0 errors/0 CS1591，`dotnet test` 全绿 153/153（新增 ScenarioWorkflowServicesTests 12 项）；真实 MySQL 028 顺序迁移已在本机执行验证（3 模板种子落库）；真实设备执行仍按部署环境验证。

## 16. V2.5 快速剪辑 Skill（Skill 独立执行）

快速剪辑是 SkillExecutor 的首个实现：用户提供素材位置与创作目标和指令，服务端确定性生成剪辑方案，经用户确认后通过剪辑 MCP 在本地主机生成剪映 `.draft` 草稿并登记为生成文件。Skill 独立执行、不绑定 Expert（先例对齐 §15 场景工作流：SourceType=skill 的 AgentRun，复用确认/幂等/审计链路，不新建运行时）。

**V2.7 对话式优化**：Web 端工作台升级为分步对话式引导——素材支持浏览器上传（`clipping_materials` 登记 + ffprobe 元数据）或路径输入；对话经无状态 chat 引导接口推进（只引导不执行）；方案以结构化视图渲染为时间线；支持修订指令重新生成方案（B29-B32）。

**V2.8 演进设计（B35/B36 已发布）**：产品总设计 §7.1 已确认「视频剪辑模块完整设计」——

- **四引擎协作架构**：方案生成改为流水线——第 1 层 video-use（转写音频 → LLM 生成 EDL → ffmpeg 执行粗剪，所有任务第一步）；第 2 层 Seedance 2.0（可选，素材缺失空镜头时生成 5-15 秒补充片段，云端 API 约 15 元/条，**默认关闭**、用户确认后启用，与本地优先原则的平衡为待决项）；第 3 层 HyperFrames（片头/片尾/转场/浮动标签/数据卡片等包装动效）；第 4 层 Remotion（可选，多说话人切换/品牌模板等复杂场景）；最终由 jianying-mcp 写入 .draft。已实施的「确定性方案生成 + 剪辑 MCP」（B24/B25）降级为流水线末段，方案确认/幂等/审计链路不变；
- **增量修改机制**：方案审核阶段支持 7 维度修改（片段时长/顺序/风格/标题/节奏/删除/新增素材）与 3 级粒度执行——参数调整（仅改方案数据，秒级）、部分重做（仅重跑相关引擎，如 HyperFrames 重新生成片头）、全量重做（完整流水线）。沿用 B31 `revise` 扩展修改语义，不新增并行端点；每次修改写 `plan_revised` 事件与 `skill_run_revised` 审计；
- **对话状态持久化（clipping_tasks，V2.8+ 切片）**：新增 `clipping_tasks` 会话状态表（BIGINT 主键同现有约定；tenant_id、run_id 可空、status=collecting/generating/reviewing/modifying/rendering/done/failed、materials、goal、current_plan、version_history、draft_path、created_by、软删除），**覆盖 B32「不落库、不新建会话表」决策**——B32 无状态 chat 保留为素材/目标收集前置引导，进入方案生成后由 `clipping_tasks` 承接持久化状态，`POST /api/v1/clipping/chat` 语义升级为携带 `task_id` 引用；`version_history` 记录每次修改（version、plan、change 描述、modified_at）支持版本标记与回退（回退能力待「版本回退/断点恢复」成为明确用户价值时启用）；任务创建/修改/终态写 `family_audit_logs` 审计；
- **风险等级维持 L1 不变**（导出不升 L2，与已注册 `quick-edit`/L1 一致）。

**B35 实施状态（2026-08-13）**：`037` 迁移新建 `clipping_tasks`；chat 首次调用创建并回传 `taskId`，后续仅本人同租户可恢复；新增 `GET /api/v1/clipping/tasks/{taskId}`。quick-edit Run 请求可选 `taskId`，创建方案时绑定 run、持久化 current_plan 与版本 1，revise 追加版本；`SkillRunView` 对已绑定任务输出 `engineStage`、`version`、`versionHistory`。当前仅发布公开 `planning` 状态，**不调用或模拟** video-use/Seedance/HyperFrames/Remotion；真实四引擎调度、阶段事件、部分/全量重做为下一切片。

**B36 实施状态（2026-08-13）**：已新增 `IClippingEngine` 适配器契约及调度服务，所有实现由命名配置注册。受控本地进程统一执行健康探测、超时和 stderr 排空；未配置、健康检查失败或执行失败均将任务置 `failed` 并只写脱敏失败事件，绝不调用 Mock 或占位结果伪造成功。`Seedance` 仅在全局开关、请求 `allowSeedance=true`、成本确认和服务端安全密钥均满足时调用；默认全部引擎关闭。本机复验 `dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/b36-deployment-verify` 为 0 errors，另有 `ScenarioWorkflowServices.cs` 既有 1 条 CS8604 警告；服务测试 246/246 通过；真实 MySQL `036_mindmap_skill`、`037_clipping_tasks` 已按顺序应用，`mindmap` 种子（1 条）与表字段备注已核验。非敏感样片经真实 API 上传已由 `D:\HomeMind\tools\ffmpeg\bin\ffprobe.exe` 提取为 7 秒、1920×1080，素材输入流生命周期、绝对存储目录及 JSON 字符串时长解析均已修复；真实 `jianying-mcp` 已经 UTF-8 无 BOM 客户端生成草稿并登记 7477 字节文件。所有四引擎配置仍保持关闭，须待逐引擎命令/凭据就绪后分别执行健康与阶段事件验收。

**xhs 部署预检（2026-08-13）**：`D:\\HomeMind\\tools\\xhs-mcp` 的 Node 依赖、stdio `initialize` 握手和只读 `xhs_auth_status` 均正常；当前状态为 `logged_out`。真实搜索/详情验证必须在授权用户人工扫码、服务端 Poll 落库 personal Connector 后执行；不自动发起登录，不执行 L2 发布确认或发布工具调用。

**V2.9 剪辑体验重构设计（B37-B39，2026-08-14）**：产品总设计 §7.1.1 已确认「剪辑体验重构」，目标是「丢素材文件夹 → 说一句话 → 拿回可预览视频」，确认/幂等/审计与 L1 风险不变。三个后端切片：

- **B37 粗剪 mp4 产出（ffmpeg 渲染）**：`clipping_tasks.status` 增加 `rendering` 阶段（`generating → reviewing → rendering → done|failed`）；新增 `IClippingRenderService`/`FfmpegRenderService`：按方案时间线（`segments` 顺序/裁剪区间/总时长）生成 ffmpeg concat+trim 命令，输出 mp4 至配置渲染目录（`Clipping:Render:OutputPath`，默认 `data/clipping/rendered/`）；产物经既有 `RegisterGeneratedFileAsync` 登记为生成文件（复用 readToken 下载），`.draft` 保持既有 jianying-mcp 链路不变（降级为进阶选项，由 Web 端呈现）；渲染失败写 `failed` 事件 + 任务失败原因（脱敏）、可重试，绝不伪造成功；首版仅 concat+trim+转码（crf 默认），不引入滤镜/GPU；渲染默认关闭（`Clipping:Render:Enabled=false`），启用依赖 ffmpeg 可执行文件（`D:\HomeMind\tools\ffmpeg\bin\ffmpeg.exe` 已就绪）；`ClippingEngineOptions` 扩展 Render 配置节；无新业务权限（沿用 `ai.run` + `media.read`）；
- **B38 素材自动发现（目录扫描）**：`clipping_materials` 增加 `source_type`（`upload|scan`，路径模式归入 upload 与 B29 一致）与 `directory_key`（路径 SHA-256 去重键；`041` 迁移——039/040 已由 M3 学习记忆库占用，按 B33 避让先例顺延；EF 迁移 `AddClippingMaterialScanColumns` 手写仅加列+唯一索引、不更新快照）；新增 `IClippingMaterialScanServices` + `ClippingMaterialScanWorker`（仿 `ClippingPipelineWorker` 周期执行，默认 60 秒）：按 `Clipping:Scan` 配置节（Enabled/MaxAgeHours 默认 24h/AllowedExtensions 媒体白名单）递归扫描 `Clipping:StoragePath` 第一级用户目录，owner 由目录名推导、租户经 `tenant_members` active 成员行推导；storage_path 精确查重（上传已登记不重复登记）+ directory_key 哈希去重 + 数据库唯一索引兜底并发；ffprobe 提取元数据（失败不阻塞）；目录不可达静默降级；自动登记仅本人可见（沿用 owner 隔离）、不写审计（后台自动行为）；
- **B39 自然语言对话解析（LLM 结构化参数）**：`ClippingChatServices` 增加 LLM 解析分支——仅当 AI 配置启用（`/ai/config` enabled）时生效；一句话（「剪成 30 秒竖屏快节奏带字幕」）→ LLM 解析为结构化参数 `{ target_duration, aspect_ratio, style, subtitle, mood }` → schema 校验（非法值 422）→ 参数写入 `clipping_tasks.goal` 并直接进入方案生成；响应返回「已理解：30 秒 / 竖屏 / 快节奏 / 加字幕」确认卡；解析失败/超时/AI 禁用自动降级为既有 B32 模板问卷（不破坏既有链路）；Prompt 不落日志、不返回。

任务状态为 `generating → reviewing|failed`，阶段事件复用 `RunEvents` 并新增展示安全 payload `{ stage, status, message, occurredAt }`；stage 限 `video_use|seedance|hyperframes|remotion|draft`，status 限 `queued|running|skipped|succeeded|failed`。每个失败均写 `failed` 事件和任务失败原因（脱敏），不得继续写成功事件或登记草稿。`POST /skills/runs/{runId}/revise` 识别参数调整、部分重做、全量重做：参数调整不调引擎；部分重做只排受影响 stage 与下游；全量重做从 `video_use` 开始。执行转入后台队列，查询仍使用 `GET /clipping/tasks/{taskId}` 与既有 `GET /expert-runs/{id}/events` 轮询；本切片不引入 WebSocket。

运行配置新增 `Clipping:Engines`：每个本地引擎必须有 `Enabled`、`CommandFileName`、`Arguments`、`WorkingDirectory`、`TimeoutSeconds`、`HealthCheckArguments` 和 `Version`；启动期健康检查失败则标记 unavailable。默认全部关闭，特别是 `Seedance:Enabled=false`；密钥仅由部署环境安全配置注入。B36 无新业务权限，沿用 `ai.run` + `media.read`。

### 16.1 领域与执行

- **SkillRun**：`POST /api/v1/skills/{skillCode}/runs` 创建 SourceType=skill 的 AgentRun（不关联 Expert，`expert_id` 为空，与 scenario 同惯例），输入参数（素材位置、创作目标和指令）与剪辑方案写入 `RequestJson`/`ResultJson`，复用既有权限快照与 UUID 幂等键；
- **素材登记（B29）**：浏览器上传经 `POST /api/v1/clipping/materials` 落盘服务端素材目录并 ffprobe 提取元数据（时长/分辨率，失败返回 null 不阻塞），上传返回可访问路径供前端回填 `media_location`（B24 契约零改动）；路径模式仅允许配置的素材根目录、越界 403；素材仅本人可见可删，上传/删除写 `media_file_uploaded`/`media_file_deleted` 审计；
- **方案结构化视图（B30）**：方案 Action 视图输出 `segments`/`audio`/`total_duration`（数据取自方案 Action 的 RequestJson，此前仅文本摘要），供 Web 渲染方案时间线；
- **方案修订（B31）**：`POST /api/v1/skills/runs/{runId}/revise` 以新创作指令重新确定性生成方案并替换方案 Action 的 RequestJson（仅 `pending_actions` 且方案未确认，否则 409），更新 ResultSummary/Result，写 `plan_revised` 事件与 `skill_run_revised` 审计；
- **chat 引导（B32）**：`POST /api/v1/clipping/chat` 无状态 context 随请求回传并推进（`collecting_materials → generating_plan → reviewing → done`，非法步进 422），规则式意图匹配（剪辑关键词）与模板回复 + suggestions，只引导不执行，不落库、不新建会话表。
- **方案生成（确定性）**：输入素材位置与创作指令 → 剪辑 MCP（jianying-mcp / capcut-mate，FFmpeg/ffprobe 为后台依赖）解析视频/音频时长与分辨率 → 生成剪辑方案摘要（片段序列/音频/时长）→ 产出 1 个 `draft_generate` Run Action（RiskLevel=L1）→ 方案摘要展示后经用户确认；
- **执行与产物**：Action 确认（复用既有确认/幂等/审计链路）→ `add_video_segment`/`add_audio_segment` 写入 → `export_draft()` 生成 `.draft` → `RegisterGeneratedFileAsync` 登记为生成文件（复用专家文件链路，下载走 10 分钟 readToken）；
- 剪辑 MCP 独立于 CreatorMcp，遵守本地优先原则；项目选型与部署形态为 B24/B25 前置依赖。

### 16.2 迁移 `029`

`database/029_quick_edit_skill.mysql.sql`（`-- Apply after 028`）：

- `skills` 注册 `quick-edit`（category=`media`，输入/输出 schema 声明素材位置与创作指令，`risk_level=L1`）；
- `family_audit_logs` action CHECK 扩展（`skill_run_created`/`skill_action_confirmed`/`skill_draft_registered`）与 target_type CHECK 扩展（`skill_run`/`skill_draft`）；
- `expert_runs` 零改动：SourceType/RequestJson/ResultJson 既有列承载，不新增列（与 §15 同约定）。

### 16.2.1 迁移 `033`/`034`（B29/B31）

- `database/033_clipping_materials.mysql.sql`（`-- Apply after 031`）：新建 `clipping_materials` 表（BIGINT 主键同现有约定、tenant_id、owner_user_id、file_name、storage_path、content_type、file_size、duration_seconds、width、height、fps、status、is_deleted、created_at/updated_at）；EF 迁移 `AddClippingMaterials`（仅建表、不更新快照，遵循仓库 Surgical Changes 约定）；
- `database/034_skill_run_revised.mysql.sql`（`-- Apply after 033`）：`family_audit_logs` action CHECK 扩展 `skill_run_revised`（无 EF 迁移，同 B23/B25 先例）。

### 16.3 权限与 API

- 新增权限 `media.read`（owner/admin/member；Skill 目录与运行发起前置）与 `media.write`（owner/admin/member；素材上传/删除）；Skill 运行沿用 `ai.run`；
- `POST /api/v1/skills/{skillCode}/runs`：`ai.run` + `media.read`，请求含 UUID 幂等键与 Skill 输入参数；未知/未启用 Skill → 422，跨租户 → 404；
- `POST /api/v1/skills/runs/{runId}/revise`（B31）：`ai.run` + `media.read`，body `{ instruction, idempotencyKey }`；仅 `pending_actions` 且方案 Action 未确认，否则 409；幂等键重放；
- `POST/GET/DELETE /api/v1/clipping/materials`（B29）：POST 上传与 DELETE 删除用 `media.write`、GET 列表用 `media.read`；上传 multipart（`media_file_uploaded` 审计）落盘服务端素材目录并 ffprobe 提取元数据；路径模式仅允许配置的素材根目录、越界 403；列表/删除仅本人（删除 `media_file_deleted` 审计）；
- `POST /api/v1/clipping/chat`（B32）：`ai.run` + `media.read`，body `{ message, context }`，无状态 context 回传、非法步进 422、规则意图匹配 + 模板回复；
- 运行轮询/取消/重试复用既有 `expert-runs` 契约（`GET /api/v1/expert-runs/{id}`、`/events`、`/cancel`、`/retry`）；
- Action 确认沿用既有确认端点链路（`POST .../actions/{actionId}/confirm`：UUID 幂等键、ActionExecutionAudits 重放、权限快照复验），不新建并行确认机制；
- 生成文件下载复用专家文件 readToken 下载；`GET /api/v1/skills` 目录接口既有已发布（CreatorMcp 已消费），快速剪辑沿用；
- 响应绝不包含 MCP 内部路径、草稿绝对路径、素材目录内容或 Prompt。

### 16.4 切片与验收

| 切片 | 范围 | 最小验收 |
| --- | --- | --- |
| B24 | `029` 迁移、`media.read` 权限、SkillRun 创建 API（SourceType=skill）、确定性方案生成与 `draft_generate` Action | `dotnet build` 0 errors / 0 CS1591；`dotnet test` 全绿（新增 SkillRun 测试：创建/幂等/未知 Skill 422/跨租户 404/方案生成/权限）；真实 MySQL 029 顺序迁移本机验证 |
| B25 | Action 确认 → 剪辑 MCP 写入草稿 → `RegisterGeneratedFileAsync` 登记 → readToken 下载；审计 | `dotnet build` 0 errors / 0 CS1591；`dotnet test` 全绿（确认执行/幂等重放/文件登记/审计）；剪辑 MCP 端到端按部署环境验证（需可访问素材与草稿目录的主机） |
| B29 | `033` 迁移 `clipping_materials` + `media.write` 权限、素材上传/列表/删除（multipart → 素材目录落盘 → ffprobe 元数据 → 审计）、路径模式仅允许素材根目录（越界 403） | `dotnet build` 0 errors / 0 CS1591；`dotnet test` 全绿（上传登记/元数据/越权/路径越界 403/删除）；真实 MySQL 033 顺序迁移本机验证 |
| B30 | 方案 Action 视图输出结构化 `segments`/`audio`/`total_duration`（数据取自方案 RequestJson） | `dotnet build` 0 errors / 0 CS1591；`dotnet test` 全绿（结构化视图测试）；无新迁移 |
| B31 | `POST /skills/runs/{runId}/revise`（`ai.run` + `media.read`）：`pending_actions` 且未确认可修订、替换方案 RequestJson、`plan_revised` 事件、`skill_run_revised` 审计（`034` CHECK 扩展）、幂等重放 | `dotnet build` 0 errors / 0 CS1591；`dotnet test` 全绿（状态机/幂等重放/409/422/404）；真实 MySQL 034 顺序迁移本机验证 |
| B32 | `POST /api/v1/clipping/chat`：无状态 context 校验推进、规则式意图匹配、模板回复 + suggestions；只引导不执行；不落库 | `dotnet build` 0 errors / 0 CS1591；`dotnet test` 全绿（意图匹配/状态机/非法步进 422）；无新迁移 |
| B36 | 四引擎异步调度、受控进程配置与健康门禁、任务/Run 阶段事件，revise 的参数调整/部分重做/全量重做语义；Seedance 默认关闭且逐任务成本确认 | 单元测试覆盖未配置失败不伪成功、Seedance 四重门禁、事件序列、部分/全量重做范围与跨用户 404；真实本地引擎仅在部署环境以样片验证，未通过不标记完成 |
| B37 | `038` 无结构变更迁移（`037` 已含 `rendering`）；`IClippingRenderService`/`FfmpegRenderService`（已确认方案 → ffmpeg trim+转码 → mp4 → `RegisterGeneratedFileAsync` 登记 → readToken 下载）；关联剪辑任务的确认返回 202 并经 Worker 完成 `rendering→done|failed`、写安全事件；验收修复完成事件序号冲突和创建方案误入 B36 队列；`FfmpegRenderService` 直读配置源修复运行时 `Clipping:Render` 解析为关闭；渲染默认关闭，失败不伪造 `.draft` 成功 | `dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/b37-verify` 通过（0 errors）；`dotnet test` 全绿 254/254；**本机全链路验收通过（2026-08-14）**：确认 202 排队 → Worker 实际启动 ffmpeg 渲染（配置直读生效）→ 任务 `done`/Run `completed` → mp4 登记 3430836 字节 → readToken 下载 200 → ffprobe 1920×1080、6.897 秒（60 秒目标被素材全长截断）；事件序号 1-6 连续；登记 `size_bytes` 解析修复；`ExpertFiles:Storage:Enabled` 恢复 `true`（首轮登记失败根因） |
| B38 | 素材自动发现（已完成）：`041` 迁移 `clipping_materials` 增加 `source_type`（默认 upload）/`directory_key`（唯一索引，039/040 被 M3 占用而顺延）；`IClippingMaterialScanServices` + `ClippingMaterialScanWorker`（素材根目录第一级用户目录递归扫描、扩展名白名单、storage_path+directory_key 双重去重、ffprobe 元数据、最近修改时间窗、目录不可达静默降级）；`ClippingMaterialView` 新增 `sourceType` | `dotnet build` 0 errors / 0 CS1591；`dotnet test` 全绿 262/262（新增扫描测试 8 项：登记/去重/元数据/不可达降级/白名单/时间窗/owner 隔离）；真实 MySQL 041 顺序迁移本机验证；**真实目录验收通过（2026-08-14）**：样片放入 ~2s 内自动登记（source_type=scan、7s/1920×1080/30fps 元数据完整），下一轮扫描与历史上传 guid 目录均不重复登记 |
| B39 | 自然语言对话解析：`ClippingChatServices` LLM 解析分支（AI 启用时生效、结构化参数 schema 校验 422、写入 `clipping_tasks.goal` 直入方案生成、响应「已理解」确认卡）；解析失败/AI 禁用降级模板问卷；Prompt 不落日志 | `dotnet build` 0 errors / 0 CS1591；`dotnet test` 全绿（解析成功/非法参数 422/AI 禁用降级/超时降级/参数写入 goal/确认卡响应）；真实 LLM 解析在 AI 配置启用环境验证 |

字段级契约发布后同步 `docs/api-implementation.md` 与 `docs/frontend-api-integration.md`。

**B23 实施状态（2026-08-08）**：`DisableAsync`（接口+实现+`ScenarioController` 第 6 条路由 `POST /scenarios/instances/{instanceId}/disable`，`smart_home.write`）已发布；`EnableAsync` 修正已禁用实例的恢复语义；无新迁移（复用 028 `status` 字段与 `ScenarioInstanceStatus.Disabled` 常量）；`dotnet build` 0 errors/0 CS1591，`dotnet test` 全绿 158/158（新增 ScenarioWorkflowServicesTests 5 项）；真实 MySQL 无需新迁移，设备执行仍按部署环境验证。

**B24 实施状态（2026-08-08）**：`029` 迁移落地——新建平台级 `skills` 目录表（`tenant_id=1`，同 `scenario_templates` 惯例；key 唯一 / category / input_schema_json / output_schema_json / required_permission / risk_level / status）并种子注册 `quick-edit`（category=`media`、`risk_level=L1`、`required_permission=media.read`），`family_audit_logs` CHECK 扩展 `skill_run_created`/`skill_action_confirmed`/`skill_draft_registered` 动作与 `skill_run`/`skill_draft` 目标（一次到位，B25 无新迁移）；EF 迁移 `AddSkillCatalog`（仅建表、无 CHECK、不更新快照）。`media.read` 权限注册（owner/admin/member，viewer 不含）；`SkillCatalog` 实体（表 `skills`，与 `ai_skills` 用户自定义技能语义分离）；`ISkillRunServices`/`SkillRunServices`（SkillRun 创建：解析平台 Skill → 校验输入（`media_location` 必填）→ 确定性方案生成（指令时长提取 1-600 秒、默认 15 秒、单片段方案，蛇形键承载于动作 RequestJson）→ SourceType=skill 的 AgentRun + `draft_generate` Action（L1）+ `skill_run_created` 审计；幂等重放 200、同键异类型 409；`GetAsync` 跨租户/跨用户 404）；`SkillRunsController` 路由 `POST /api/v1/skills/{skillCode}/runs`（`ai.run` + `media.read` 双策略）；`dotnet build` 0 errors/0 CS1591，`dotnet test` 全绿 165/165（新增 SkillRunServicesTests 7 项：创建/方案时长解析/幂等重放/未知 Skill 422/非法输入 422/跨租户 404/审计/同键异类型 409）；真实 MySQL 029 顺序迁移已在本机执行验证（seed 落库 + CHECK 生效）；剪辑 MCP 选型与部署形态仍为 B25 前置依赖。

**B29 实施状态（2026-08-09）**：`033` 迁移新建 `clipping_materials` 素材登记表 + EF 迁移 `AddClippingMaterials`；`media.write` 权限注册（owner/admin/member，viewer 不含）；`IClippingMaterialServices`/`ClippingMaterialServices`（上传：multipart → 配置素材根目录落盘 → ffprobe 提取时长/分辨率（失败 null 不阻塞）→ 返回素材视图含可访问路径 → `media_file_uploaded` 审计；路径模式校验仅允许素材根目录内、越界 403；列表/删除仅本人，删除 `media_file_deleted` 审计）；`ClippingMaterialsController` 三条路由；`dotnet build` 0 errors/0 CS1591、`dotnet test` 全绿（新增上传登记/元数据/越权/路径越界/删除测试）；真实 MySQL 033 顺序迁移已在本机执行验证；ffprobe 按部署环境验证。

**B30 实施状态（2026-08-09）**：`SkillRunActionView` 输出结构化剪辑方案（`segments`/`audio`/`total_duration`，数据取自方案 Action 的 RequestJson，此前仅文本摘要）；Web 端据此渲染方案时间线；无新迁移；`dotnet build`/`dotnet test` 全绿（结构化视图测试）。

**B31 实施状态（2026-08-09）**：`034` 迁移扩展 `family_audit_logs` action CHECK（`skill_run_revised`，无 EF 迁移）；`ISkillRunServices.ReviseAsync`/`SkillRunServices`（仅 `pending_actions` 且方案 Action 未确认可修订，否则 409；新指令重新确定性生成方案并替换方案 Action 的 RequestJson、更新 ResultSummary/Result、`plan_revised` 事件、`skill_run_revised` 审计、幂等键重放）；`SkillRunsController` 新增 `POST /api/v1/skills/runs/{runId}/revise`（`ai.run` + `media.read`，非法幂等键 422/跨租户 404）；真实 MySQL 034 顺序迁移已在本机执行验证。

**B32 实施状态（2026-08-09）**：`IClippingChatServices`/`ClippingChatServices`（无状态 context 随请求回传：`collecting_materials → generating_plan → reviewing → done`，非法步进 422；规则式意图匹配——消息含剪辑关键词即进入快速剪辑引导；模板回复 + suggestions 引导按钮；只引导不执行，不落库）+ `ClippingChatController` 单条路由 `POST /api/v1/clipping/chat`（`ai.run` + `media.read`）；`dotnet build`/`dotnet test` 全绿（意图匹配/状态机/非法上下文）。

**B25 实施状态（2026-08-09）**：`IClippingMcpClient`/`MockClippingMcpClient` 已发布（确定性 Mock：按剪辑方案生成最小剪映草稿 JSON——片段序列/总时长/draft_roaming_id，不访问素材目录、不产生真实文件路径；真实 jianying-mcp / capcut-mate 接入为部署环境验证项）。`ISkillRunServices.ConfirmActionAsync`/`SkillRunServices` 确认执行链路：UUID 幂等键 422 校验 → `ActionExecutionAudits` 同键重放首次结果不重复登记 → 权限快照复验 → 经剪辑 MCP 生成 .draft 内容 → `RegisterGeneratedFileAsync` 登记 `quick_edit_{runId}.draft.json`（application/json，附件到 run，解析 fileId/sizeBytes）→ action `executed`/run `completed`，登记失败 502、action/run `failed`；写 `skill_action_confirmed`（目标 `skill_run`）与 `skill_draft_registered`（目标 `skill_draft`）审计；**无新迁移**。`SkillRunsController` 新增路由 `POST /api/v1/skills/runs/{runId}/actions/{actionId}/confirm`（`ai.run` + `media.read`，非法幂等键 422/动作不存在或非本人 404/已终态换键 409）；下载复用既有 `POST /api/v1/expert-files/{fileId}/read-token`（10 分钟 readToken）。`dotnet build` 0 errors/0 CS1591（唯一警告为存量 ScenarioWorkflowServices 可空性，非本切片引入），`dotnet test` 全绿 170/170（新增 SkillRunServicesTests 5 项：确认执行登记与双审计/幂等重放/422-404-409/登记失败 502 与终态/Mock 草稿结构）；真实 MySQL 无需新迁移；剪辑 MCP 端到端（真实写入素材与剪映草稿目录）按部署环境验证。

**B38 实施状态（2026-08-14）**：`041` 迁移落地——`clipping_materials` 加 `source_type`（`VARCHAR(16) NOT NULL DEFAULT 'upload'`）与 `directory_key`（`VARCHAR(64) NULL`）+ 唯一索引 `uk_clipping_materials_directory_key`；041 避让：039/040 已由 M3 学习记忆库占用，按 B33 避让先例顺延；EF 迁移 `AddClippingMaterialScanColumns` 手写仅加列与索引、不更新快照（`dotnet ef` 自动生成含全库漂移，弃用）。`IClippingMaterialScanServices`/`ClippingMaterialScanServices`：`Clipping:Scan` 配置节直读（Enabled 默认 true/MaxAgeHours 默认 24/AllowedExtensions 媒体白名单），递归扫描 `Clipping:StoragePath` 第一级用户目录，owner 由目录名推导、租户经 `tenant_members` active 成员行推导（users 表无租户列）；storage_path 精确查重 + directory_key 哈希去重 + 唯一索引兜底；ffprobe 失败不阻塞；目录不可达/用户不存在/唯一键冲突均静默跳过；不写审计。`ClippingMaterialScanWorker`（BackgroundService，`Clipping:Scan:IntervalSeconds` 默认 60）注册于 Startup；`ClippingMaterialView` 新增 `sourceType`。`dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/b38-verify` 0 errors / 0 CS1591（34 个警告均为存量）；`dotnet test` 全绿 262/262（新增 ClippingMaterialScanServicesTests 8 项）；真实 MySQL 041 顺序迁移已在本机执行验证（列/默认值/唯一索引/存量行 source_type=upload）；**真实目录验收通过**：样片放入 `materials/1/` 后 ~2s 自动登记（source_type=scan、8067291 字节、7s/1920×1080/30fps、directory_key 64 位），下一轮扫描与历史上传 guid 目录均不重复登记。

## 17. V2.6 小红书个人级 Connector（xhs）

小红书作为个人级 Connector 落地（产品决策：搜索 + 发布；形态匹配产品总设计 §12「内容发布 Provider」方向）。经本地 stdio MCP（xhs-mcp，Puppeteer 驱动）调用，扫码登录、凭据由本机 MCP 进程管理；搜索/详情只读 L1，发布 L2 逐项确认。本地优先部署于开发机，生产按 N97 另行评估。

### 17.1 领域与部署形态

- **Provider**：`xhs`（provider=`xhs_mcp`、connector_type=`social`），`binding_scope=personal` + `owner_user_id`（JWT 推导），成员仅管理本人实例；
- **MCP 客户端**：`IMcpProcessClient`/`StdioMcpProcessClient` 本地 stdio JSON-RPC 客户端（UTF-8 双管道、懒启动+initialize 握手、按 id 关联、超时、进程重建，进程级单例共享）；`IXhsMcpClient`/`XhsMcpClient` 封装 `xhs_auth_status`/`xhs_auth_login`/`xhs_auth_logout`/`xhs_search_note`/`xhs_get_note_detail`/`xhs_publish_content`，只读解析稳健降级（解析失败返回空结果不抛异常，字段映射以部署的 xhs-mcp 版本契约为准，部署验证时校准）；`MockXhsMcpClient` 确定性 Mock（DI 默认注册，`Mcp:Clients:Xhs:Enabled=true` 切换真实，测试与无 node 环境回退）；
- **安全约束**：凭据（cookie/登录态）由本地 MCP 进程管理，不落库、不返回、不记录；`credential_ref` 仅存 `local://xhs-sessions/{uuid}` 会话标识；响应绝不包含 MCP 内部路径、登录态明文或 Prompt。

### 17.2 迁移 `030`

- `connector_providers` 注册 `xhs`（ON DUPLICATE KEY UPDATE 同 024 惯例）；
- `family_audit_logs` CHECK 扩展 `xhs_note_published` 动作与 `xhs_note` 目标（B27 发布消费，一次到位）；
- **无 EF 迁移**（同 B23/B25 先例：无表结构变更不生成 EF 迁移，种子与 CHECK 由 `database/030` 管理）。

### 17.3 授权适配（扫码登录态 ↔ 现有模型，不改表结构）

- **发起**：`StartAuthorizationAsync` xhs 分支跳过回调白名单与 Vault 检查，调用 `xhs_auth_login` 触发扫码，`redirect_uri` 落库占位 `xhs://local-polling`、pkce 为空、`state_hash` 保存一次性标识；响应携带 `QrContent`（`AuthorizationSessionView` 扩展字段）；
- **轮询**：新增 `PollAuthorizationAsync`（路由 `POST /api/v1/connector-authorizations/{id}/poll`）：未登录 202；登录成功后创建/更新 personal `WorkspaceConnector`（`auth_status=connected`、`credential_ref=local://xhs-sessions/{uuid}`）并写 `connector_authorize_completed` 审计；过期/已结束 409，跨租户/非本人 404；
- **撤销**：`RevokeAuthorizationAsync` xhs 分支调用 `xhs_auth_logout`（失败不阻塞状态流转），重复撤销幂等返回既有结果；
- 授权动作复用既有 `connector_authorize_*` 审计动作与目标，无新增。

### 17.4 工具与服务与 API

- `IXhsConnectorServices`/`XhsConnectorServices`：搜索/详情/登录状态执行前统一校验连接器归属（当前租户 + personal + 本人 owner + `auth_status=connected`），未授权统一 404；
- 路由与权限：

| 路由 | 方法 | 权限 | 风险 |
| --- | --- | --- | --- |
| `/api/v1/connector-providers/xhs/notes/search?query=&limit=` | GET | `connector.read` | L1 只读 |
| `/api/v1/connector-providers/xhs/notes/detail?url=` | GET | `connector.read` | L1 只读 |
| `/api/v1/connector-providers/xhs/auth-status` | GET | `connector.read` | L1 只读 |
| `/api/v1/connector-providers/xhs/authorizations` | POST | `connector.authorize` | 发起扫码 |
| `/api/v1/connector-authorizations/{id}/poll` | POST | `connector.authorize` | 轮询登录 |
| `/api/v1/connector-authorizations/{id}` | GET/DELETE | `connector.authorize` | 状态/撤销 |

- 发布（B27）：`xhs_publish_content` 为 L2 对外发布动作，走 ExpertRunAction 确认链路（幂等键 + ActionExecutionAudits 重放 + 权限快照复验），确认后执行并写 `xhs_note_published` 审计；发布参数校验：图文标题≤20 字符、正文≤1000 字、图片≤18，视频恰 1 个文件。

### 17.5 切片与验收

| 切片 | 范围 | 最小验收 |
| --- | --- | --- |
| B26 | `030` 迁移（xhs Provider + 审计 CHECK）、stdio MCP 客户端基础设施、扫码授权（发起/轮询/撤销）、搜索/详情/登录状态 API（L1） | `dotnet build` 0 errors / 0 CS1591（无新增警告）；`dotnet test` 全绿（新增 XhsConnectorServicesTests 7 项 + AuthorizationServicesTests xhs 分支 5 项）；真实 MySQL 030 顺序迁移本机验证；真实 xhs-mcp 扫码与搜索端到端按部署环境验证 |
| B27 | 发布 L2 确认链路 + 真实发布执行 + `xhs_note_published` 审计 | `dotnet build` 0 errors / 0 CS1591；`dotnet test` 全绿（发布创建/确认/幂等/422-404-409/失败 502）；真实发布按部署环境验证 |
| B28 | 剪映真实 MCP 接入：`JianyingMcpClient` 实现 `IClippingMcpClient`（create_draft + 读取草稿字节流，SkillRunServices 零改动），DI 配置驱动可回退 Mock | `dotnet build` 0 errors / 0 CS1591（无新增警告）；`dotnet test` 全绿（既有 190 保留）；本机 jianying-mcp 部署后真实草稿生成端到端按部署环境验证 |

**B26 实施状态（2026-08-09）**：`030` 迁移落地——`connector_providers` 注册 `xhs`（provider=`xhs_mcp`、connector_type=`social`），`family_audit_logs` CHECK 扩展 `xhs_note_published`/`xhs_note`（一次到位，B27 无新迁移）；无 EF 迁移（同 B23/B25 先例）。`IMcpProcessClient`/`StdioMcpProcessClient`/`McpProcessOptions`/`McpClientException` 本地 stdio MCP 客户端发布（JSON-RPC 2.0、UTF-8 双管道、懒启动+握手、超时、进程重建；进程客户端进程级单例共享——Scoped 注册会导致每请求重启 MCP 进程，与「本地助手进程」定位矛盾，故注册为 Singleton，回顾模式记录此偏差）。`IXhsMcpClient`/`XhsMcpClient`/`MockXhsMcpClient` 发布（工具映射见 17.1；DI 默认 Mock，`Mcp:Clients:Xhs:Enabled=true` 切换真实）。扫码授权适配：`StartAuthorizationAsync` xhs 分支（跳过白名单与 Vault、占位 `xhs://local-polling`、pkce 空、返回 `QrContent`）、新增 `PollAuthorizationAsync`（未登录 202/完成落库+审计）、`RevokeAuthorizationAsync` xhs 分支（`xhs_auth_logout` 失败不阻塞、幂等）。`IXhsConnectorServices`/`XhsConnectorServices` 搜索/详情/登录状态（未授权 404、只读 L1）；`XhsController` 三条 GET 路由（`connector.read`）；`ConnectorsController` 新增 poll 路由（`connector.authorize`）。`dotnet build` 0 errors/0 CS1591（21 个警告均为既有存量，无新增），`dotnet test` 全绿 182/182（新增 XhsConnectorServicesTests 7 项 + xhs 授权分支 5 项）；真实 MySQL 030 顺序迁移已在本机执行验证（xhs Provider 落库 + CHECK 接受 `xhs_note_published`/`xhs_note`）；真实 xhs-mcp 扫码授权与搜索端到端按部署环境验证。

**B27 实施状态（2026-08-09）**：`031` 迁移落地——重建 `expert_runs.ck_run_source` CHECK：原 expert/group 语义不变，追加 `scenario`/`skill`（补 B22/B24 真实 MySQL 缺口——本机先前仅验证迁移执行未创建真实 Run，`ck_run_source` 原约束会拒绝这两类）与 `xhs`（B27 发布）；无 EF 迁移。`FamilyAuditActions.XhsNotePublished`/`FamilyAuditTargetTypes.XhsNote` 常量与 `FamilyAuditLogger` 白名单同步（B26 仅迁移侧，代码常量 B27 消费补齐）。`IXhsPublishServices`/`XhsPublishServices` 发布链路：创建（参数校验 type=image/video、标题≤20、正文≤1000、图文≤18 图、视频恰 1 → SourceType=`xhs` 的 AgentRun（ExpertVersionId null、permission_snapshot personal）+ `xhs_publish` ExpertRunAction（RiskLevel=L2）+ 幂等键同键重放 200/同键异类型 409）+ 确认（UUID 幂等键 422 → `ActionExecutionAudits` 同键重放不重复发布 → 权限快照复验 → 经 `XhsMcpClient.PublishAsync` 执行 → action `executed`/run `completed` + `xhs_note_published` 审计（目标 `xhs_note`），失败 502、action/run `failed`）。`XhsController` 新增 `POST /api/v1/connector-providers/xhs/notes/publish` 与 `POST /publish-actions/{actionId}/confirm`（`ai.run` + `connector.write`，非法幂等键 422/动作不存在或非本人 404/已终态换键 409）；`XhsPublishConfirmRequest`/`XhsPublishActionView` DTO。`dotnet build` 0 errors/0 CS1591（唯一警告为存量 CS8604），`dotnet test` 全绿 190/190（新增 XhsPublishServicesTests 8 项：创建 L2 动作/参数校验 422/未授权 404/确认发布+审计/幂等重放不重复发布/422-404-409/失败 502 与终态/同键异类型 409）；真实 MySQL 031 顺序迁移已在本机执行验证（约束含 scenario/skill/xhs 分支，xhs 事务内插入成功）；真实发布按部署环境验证。

**B28 实施状态（2026-08-09）**：`JianyingMcpClient` 发布——实现 `IClippingMcpClient.GenerateDraftAsync`：解析方案 JSON（素材名拼草稿名）→ 调用 `create_draft`（draft_name/width 1920/height 1080/fps 30）→ 按 MCP 返回草稿目录读取 draft.json 字节流返回；草稿路径不可读抛 `McpClientException`（调用方按登记失败 502 处理）；素材片段装配与最终 draft.json 内容以部署的 jianying-mcp 版本工具契约为准（部署验证时校准）。`IClippingMcpClient` DI 改为配置驱动：`Mcp:Clients:Jianying:Enabled=false` 默认回退 `MockClippingMcpClient`（测试与无环境回退），true 时经 `uv --directory <repo>/jianyingdraft run server.py` 真实 stdio 调用（SAVE_PATH/OUTPUT_PATH 由 MCP 进程环境提供）；`appsettings.json` 增 `Mcp:Clients:Jianying`（Enabled/CommandFileName=uv/Arguments/TimeoutSeconds=60）。SkillRunServices 零改动（契约保持返回字节流）。`dotnet build` 0 errors/0 CS1591（22 个警告均为既有存量），`dotnet test` 全绿 190/190（既有全保留）；真实 MySQL 无需新迁移。**本机 jianying-mcp 部署验证受阻**：已克隆 `D:\HomeMind\tools\jianying-mcp`（README 确认启动命令与环境变量）、winget 安装 uv 0.12.2 成功；本机无 Python 3.13 且 `uv python install 3.13`/`uv sync` 因网络限制（releases.astral.sh 与 github.com 下载不可达）挂起，依赖安装无法完成——待网络/环境就绪后执行真实草稿生成端到端。

**B28 部署校准实施状态（2026-08-09）**：jianying-mcp 部署与真实草稿生成端到端已在本机验证完成（网络受限解除）。前置部署：安装 uv 0.12.3（`C:\Users\15953\.local\bin` 入用户 PATH）、`uv sync` 完成依赖安装（自动下载 Python 3.13.15 + 47 包，`.venv` 创建）、部署目录创建 `.env`（`SAVE_PATH=D:/HomeMind/tools/jianying-mcp/draft` 草稿中间数据、`OUTPUT_PATH=剪映草稿目录 com.lveditor.draft`；`.env` 已入该仓库 .gitignore）。**契约校准（以部署的 jianying-mcp 版本为据）**：① 启动命令校准为 `uv run python jianyingdraft/server.py`——原 `uv run mcp` 是 mcp CLI 无子命令（实测 exit 2 仅打印 Usage，不进入 stdio 监听）；`appsettings.json` 增 `WorkingDirectory`（`load_dotenv` 依赖工作目录加载 `.env`）；② `create_draft` 返回 `{draft_id,draft_name,width,height,fps}` 且**不返回路径**——`JianyingMcpClient` 重写为完整装配链路（对齐 §16.1 设计契约）：create_draft → create_track（video 轨 video1）→ add_video_segment（media_location 绝对路径素材、`0s-{时长}s`，`Path.GetFullPath` 归一后端相对路径）→ export_draft（返回 `output_path`，直落剪映草稿箱，产出 `draft_content.json`+`draft_meta_info.json`）→ 读取 draft_content.json 字节留档登记（SkillRunServices 仍零改动，文件名/`application/json` 契约不变）；③ 素材经绝对路径引用（export 不复制素材进草稿），剪映本机可打开、跨机不可用（本地优先产品可接受）；④ 工具返回兼容裸 dict（create_draft）与 `{success,message,data}`（create_track/add_video_segment/export_draft），失败均抛 `McpClientException`（502）。`Mcp:Clients:Jianying` 启用（Enabled=true、CommandFileName=uv、Arguments=run python jianyingdraft/server.py、WorkingDirectory=D:\HomeMind\tools\jianying-mcp、TimeoutSeconds=120）。**追加校准（2026-08-10 端到端验证）**：⑤ `StdioMcpProcessClient` 补发 `notifications/initialized` 通知——MCP 协议生命周期要求，FastMCP（python SDK）收到前 `tools/list` 返回空、`tools/call` 报 -32602（xhs-mcp 的 node SDK 不强制，此前未暴露；jianying 首次 confirm 即失败定位）；⑥ `JianyingMcpClient` 装配前经 `parse_media_info` 探测素材实际时长，`add_video_segment` 的 target 取 min(方案时长, 素材时长)——jianying-mcp 契约硬性约束 target 不得超出素材本身时长（超长返回「参数错误: 素材所占的轨道时长…超出素材本身时长…」），而方案时长来自用户指令（1-600 秒）不感知素材实际时长，探测失败回退方案时长（不阻断装配）。`dotnet build` 0 errors/0 CS1591、`dotnet test` 全绿（SkillRunServicesTests 走 Mock 不受影响）；真实端到端（路径模式登记素材→run→方案→confirm→剪映草稿箱生成 `quick_edit_{素材名}` 草稿（draft_content.json+draft_meta_info.json+material、素材绝对路径引用）→readToken 下载 draft_content.json 与草稿箱逐字节一致→`skill_action_confirmed`/`skill_draft_registered` 双审计落库）已通过。

**xhs 真实 MCP 部署验证实施状态（2026-08-09）**：真实 xhs-mcp 扫码授权/搜索端到端已在本机验证完成。前置部署：`D:\HomeMind\tools\xhs-mcp`（xhs-mcp 0.8.11，package.json overrides 强制 node-fetch@2.7.0 修复 0.8.x 打包产物 ERR_REQUIRE_ESM——CJS 入口 require 了 ESM-only 的 node-fetch v3；Chromium 127 已就绪）；`appsettings.json` 启用 `Mcp:Clients:Xhs`（CommandFileName=node、Arguments=本地 dist 绝对路径 + mcp、WorkingDirectory=tools/xhs-mcp、TimeoutSeconds=180——外部页面加载波动下 90s 不足）。端到端：浏览器人工扫码登录（cookie 由 xhs-mcp 本机持久管理）→ 授权发起（新增幂等检查：`GetAuthStatusAsync` 已登录时跳过 `xhs_auth_login` 浏览器流程，避免每次发起都弹浏览器）→ `PollAuthorizationAsync` 落库 personal 连接器 + 完成审计 → `GET /connector-providers/xhs/notes/search` 返回真实笔记（NoteId/Title/Cover/Author 完整解析）。**StdioMcpProcessClient 四项校准**：① 输入流 `new UTF8Encoding(false)` 无 BOM——`Encoding.UTF8` 默认带 EF BB BF 前缀，node MCP Server 收到后 JSON 解析失败、消息被丢弃、永不响应（根因；Node spawn/msys 管道无 BOM 故正常）；② 调用异常/超时后 kill 进程重建——`WaitAsync` 取消不取消底层 `StreamReader.ReadLineAsync`，残留读占用 stdout 流导致后续调用 "stream is currently in use"；③ stderr 后台排空——`RedirectStandardError=true` 但无人读取，管道缓冲写满致 MCP 进程阻塞死锁；④ `McpProcessOptions` 增加 `WorkingDirectory`。**契约校准**：`XhsMcpClient.SearchNotesAsync` 支持真实返回 `feeds` 数组 + `noteCard{displayTitle,user.nickName,cover.urlDefault}` 嵌套（保留平铺结构兼容）。`dotnet test` 全绿 214/214（幂等发起授权断言同步更新）。真实发布端到端（L2 确认链路）与 jianying 部署验证仍按部署环境待验证。

## 18. V2.7 思维导图 Skill（Skill 独立执行，零转换依赖）

思维导图 Skill 输入为 markdown 文本，产物为浏览器端交互视图与可导出文件。转换是确定性纯函数，由 Web 端 markmap-lib 执行，服务端零新增运行时依赖（否决 node 子进程：部署耦合、进程管理与 Windows 引号处理；否决 C# 重实现：偏离 markmap 语义、维护成本高）。服务端只负责 Skill 目录、权限、Run 记录与审计。

### 18.1 领域与执行

- **SkillRun**：`POST /api/v1/skills/mindmap/runs` 创建 SourceType=skill 的 AgentRun（不关联 Expert），输入 `markdown` 文本（≤ 100,000 字符，超限 422）写入 RequestJson；**同步完成**（无外部依赖、无 Action、无确认——L1 只读），ResultSummary 输出摘要（`characterCount` + 首个一级标题）；复用既有权限快照与 UUID 幂等键（幂等重放 200、同键异请求 409）；
- **审计**：写 `skill_run_created`（目标 `skill_run`，复用 029 CHECK，无新动作码）；
- **不落文件**：`.md` 文件由浏览器 FileReader 本地读取为文本；服务端不新增文件上传、不登记生成文件、不调用 MCP。

### 18.2 迁移 `035`

`database/035_mindmap_skill.mysql.sql`（`-- Apply after 034`；无 EF 迁移、无表结构变更，同 B23/B25 先例）：

- `skills` 注册 `mindmap`（category=`productivity`，input_schema 声明 `markdown` 必填、≤100000 字符，`risk_level=L1`，`required_permission=mindmap.read`）；
- 权限注册 `mindmap.read`（owner/admin/member，viewer 不含，同 `media.read` 惯例）。

### 18.3 权限与 API

- `POST /api/v1/skills/mindmap/runs`：`ai.run` + `mindmap.read`；请求含 UUID 幂等键与 `{ markdown }`；未知/未启用 Skill → 422，markdown 缺失或超限 → 422，跨租户 → 404；
- 运行查询/事件/取消/重试复用既有 `expert-runs` 契约（`GET /api/v1/expert-runs/{id}`、`/events`）；`GET /api/v1/skills` 目录接口既有已发布；
- 响应与日志绝不包含 Prompt；markdown 内容仅存 Run RequestJson（家庭租户隔离），Response 摘要仅含字符数与一级标题。

### 18.4 切片与验收

| 切片 | 范围 | 最小验收 |
| --- | --- | --- |
| B33 | `036` 迁移（因既有 `035_xhs_content_creator_expert` 占号，顺序避让）注册 mindmap + `mindmap.read` 权限、`POST /api/v1/skills/mindmap/runs`（SourceType=skill、同步 completed、摘要生成、`skill_run_created` 审计、幂等重放/422/404） | `dotnet test --filter FullyQualifiedName~MindmapRunServicesTests` 通过 4/4；真实 MySQL 036 顺序迁移待部署环境验证 |

## 19. V2.7 Skill 目录查看（scope 视图）

产品决策：用户端只能查看本人用户级技能，开发端可查看全部（平台级目录 + 租户内成员技能）。现状说明：`GET /api/v1/skills` 历史语义为用户级 `ai_skills` 列表（AiSkillServices，`ai.skills.read`，租户+用户过滤）；平台级 `skills` 目录此前仅服务内部按 key 查询（SkillRunServices），**无列表接口**——B24 实施状态中「`GET /api/v1/skills` 目录接口既有已发布」的表述与实际不符，本设计修正。为不破坏既有消费方（CreatorMcp 默认不带 scope），扩展 scope 参数并保持默认行为不变。

### 19.1 API 与视图

- `GET /api/v1/skills?scope=mine|platform|all`（默认 `mine`）：
  - `mine`：本人用户级技能（现状行为不变，视图含 Prompt——仅本人可读）；
  - `platform`：平台级 Skill 目录（`skills` 表 tenant_id=1、status=active、未删），返回 key/name/category/description/risk_level/required_permission/input_schema_json/status（非敏感）；
  - `all`：平台级目录 + 当前租户全部成员的技能摘要（id/name/is_active/成员名/created_at/updated_at），**不含 Prompt 明文**（提示词属敏感数据，仅本人可读）；
- 权限：`mine` 沿用 `ai.skills.read`（所有成员）；`platform`/`all` 服务端校验当前家庭角色为 owner/admin（`ai.skills.read` + `tenant_members.role ∈ {owner, admin}`），member/viewer 即使持有 `ai.read` 也 403——**平台级 Skill 目录不得被成员查询**；跨租户 404；
- 实现：AiSkillServices 增加 `ListPlatformAsync`/`ListAllAsync`（平台目录直查 `SkillCatalogs`；all 视图在租户内关联 `ai_skills` 与成员名，输出脱敏 DTO，不含 Prompt）。

### 19.2 切片与验收

| 切片 | 范围 | 最小验收 |
| --- | --- | --- |
| B34 | `GET /api/v1/skills` 已扩展 scope（默认 mine 行为不变/platform/all）、平台目录与成员技能脱敏视图（all 不含 Prompt）、角色校验（platform/all 仅 owner/admin，member/viewer 持 `ai.skills.read` 也 403） | `dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/b34-verify` 0 errors；`dotnet test --filter FullyQualifiedName~AiSkillCatalogServicesTests` 通过 3/3；无新迁移 |

## 20. V2.5 HA MCP、审批与分层记忆参考架构

详细源码分析见 `NexusMind-Hermes-MCP-Fusion-Analysis.md`。本节只定义后续实现边界；当前没有发布新 API、迁移或代码。

### 20.1 HA MCP Adapter 与事件同步

- 新增 transport-neutral `IMcpClientSession`/`IMcpClientManager`：initialize、tools/list、tools/call、Tool Manifest 缓存与哈希、tools/list_changed、超时、指数退避、停泊/恢复、脱敏错误；现有 `StdioMcpProcessClient` 作为 stdio transport 实现继续复用，N97 默认 stdio；
- `HomeAssistantMcpAdapter` 继续实现现有 `IDeviceAdapter`、`IDeviceDiscovery`、`IDeviceCommandExecutor`，业务服务不感知 MCP Tool 名；运行模式仅取 `mcp/rest_fallback/disabled`，主备切换不改变上层 Tool；
- Agent Tool 固定为 `smart_home.search_resources/get_state/control_device/run_scene`；输入只接受 `workspace_connector_id` 和 NexusMind `room_id/device_id/capability`，Adapter 内部映射 entity_id/service，禁止任意 service 与自由 JSON；
- 新增 `IHomeAssistantEventSubscriber`，HA WebSocket `state_changed` 经 entity/domain 白名单、ignore、cooldown、去重后进入 `DeviceSyncService`；断线重连必须恢复订阅，高频传感器事件不逐条唤醒 Agent；
- 错误码基线：`ha_auth_failed`、`ha_entity_not_found`、`ha_service_not_allowed`、`ha_validation_failed`、`ha_timeout`、`ha_disconnected`、`ha_result_unknown`、`mcp_tool_unavailable`。写操作超时进入 `result_unknown`，不得盲重试。

### 20.2 审批 Grant 与异步恢复

- 复用 `ExpertRunAction`、`ConfirmationItem`、`ActionExecutionAudits`、`family_audit_logs`；创建确认后 Run 持久化为等待输入，确认 API 新请求恢复执行，不保持阻塞 HTTP/线程；
- `Approve once` 为 Action 确认；run-scoped/preference-scoped Grant 只允许 L1，约束至少含 home/user/tool/resource/argument constraint/expiry/revocation；L2/L3 永远逐项确认；
- Confirmation View 后续增加 tool、目标空间/设备、参数 diff、影响、可逆性、依据摘要和过期时间；任何展示先脱敏；
- 执行前事务内复验 tenant、成员权限、设备状态、最终风险、过期与幂等；执行 Worker 与 outbox 在实现切片决定，不以 Flutter 本地状态作为事实。

### 20.3 Context Snapshot 与 Memory Candidate

- 新增 `IContextSnapshotServices`；每个 Run 冻结家庭知识、个人偏好、决策历史、设备摘要、Expert/Skill 版本及召回引用，运行中记忆写入不修改既有版本；
- 建议表 `context_snapshots`：`run_id/version/expert_version_id/skill_versions_json/knowledge_refs_json/preference_refs_json/device_state_refs_json/content_hash/created_at`；
- 已实现 `MemoryReviewWorker`：仅消费已完成 Run 的显式 `memoryCandidates` 结构化结果，服务端重建 Run 证据引用并创建 `pending` 候选；`memory_review_receipts` 记录空结果和已处理 Run，重启后不重复扫描或生成候选；不从 Prompt、会话正文或自由文本摘要推断记忆，失败不影响主 Run。上下文压缩和会话结束触发仍待后续接入；
- Expert 版本以 `output_schema_json.properties.memoryCandidates` 显式选择加入候选契约；运行期仅对已选择版本附加候选格式与敏感信息禁止规则。自建 Expert 的创建/更新 API 可写入该输出契约；未选择的既有 Expert 保持原有输出，不产生候选；
- `040_review_analyst_memory_candidates.mysql.sql` 为内置 `review-analyst` 新建不可变的已发布版本并声明该字段；Run 解析始终选择最新已发布版本，因此部署迁移后新的复盘 Run 可产生待审核候选，旧 Run 仍固定在原版本；
- USER 类个人偏好和家庭知识严格隔离；N97 SQLite FTS 只做可重建的本地派生索引，MySQL 保持事实源；中文 tokenizer、删除传播和本地加密为实施前置；
- 后台 review 可使用本地低成本模型，但敏感/冲突事实、成员身份、健康/财务/安防和平台 Skill 不得自动写入或覆盖。

### 20.3.1 学习记忆库（M3，已实现，真实 MySQL 待部署验证）

M3 为 Web `/app/memories` 提供“已接受、可召回的学习记忆”只读视图；它不把 `memory_candidates` 的审核页搬到新路由，也不把家庭知识、个人偏好、完整对话、Prompt 或 N97 SQLite FTS 当作客户端资源。M2 仍是候选产生与受控写入链路；M3 仅在候选被接受并成功写入事实源后建立可追溯的展示投影。

- 新增 `learning_memory_records` 投影表：`id`、`home_id`、`owner_user_id?`、`candidate_id`（唯一）、`target_type`（`family_knowledge|personal_preference|decision_history`）、`target_id`、`kind`（`preference|fact|decision`）、`visibility`（`personal|family`）、`display_summary`、`stability`、`status`（`active|archived|expired`）、`source_run_id?`、`source_conversation_id?`、`evidence_ref_count`、`restricted_reference_count`、`resolved_at`、`expires_at?`、`archived_at?`、`created_at`、`updated_at`。它只保存用于产品展示的脱敏摘要和来源引用，不复制候选原始证据、对话正文或模型内容。
- `MemoryCandidate` 被接受、编辑后接受或按配置自动接受时，与目标事实源写入同一事务创建/更新投影；目标事实被删除、过期、被覆盖或不再对当前成员可见时，同步归档或过滤投影。候选被拒绝、过期或 review 失败不创建记录；同一 `candidate_id` 的重复处理只能重放既有结果。
- 新增 `ILearningMemoryServices`，所有查询先由 JWT 推导 `home_id`/`user_id`，再执行 `visibility`、owner、`family.read` 与目标事实源可见性过滤。`memory.read` 只授予可读取本人个人记忆及既有家庭可见范围的角色；跨家庭、跨成员个人记忆或已删除目标统一 `404`。来源包含无权个人引用时仅返回 `restrictedReferenceCount`，不返回引用 ID、摘要或成员信息。
- 已发布只读 API：`GET /api/v1/memories?scope=all|personal|family&kind=&status=&query=&cursor=&limit=`（游标分页）与 `GET /api/v1/memories/{id}`。列表/详情仅返回 `LearningMemoryView`：`id`、`summary`、`kind`、`visibility`、`stability`、`status`、`learnedAt`、`expiresAt`、`sourceReferences`、`restrictedReferenceCount`、`resolutionSummary`；不提供任何 M3 写接口。当前来源仅含可见 Run 引用；Conversation 与受限引用计数扩展仍待后续切片。

### 20.4 切片与最小验收

| 切片 | 范围 | 最小验收 |
| --- | --- | --- |
| H1 | MCP Session/Manager、Manifest cache、Mock Server | initialize/list/call、缓存失效、重连、工具撤销、脱敏、并发测试 |
| H2 | HA MCP 只读发现/状态与标准化映射 | 已完成：`HomeAssistantMcpAdapter` 内部仅调用 `ha_list_entities`/`ha_get_state`，映射到既有 `DiscoveredDevice`/`AdapterDeviceState`；缺失工具拒绝为 `mcp_tool_unavailable`，写入一律 `mcp_read_only`；运行模式 `mcp/rest_fallback/disabled`，默认 REST 回退；定向 Mock 测试全绿 |
| H3 | WebSocket state_changed 同步 | 已完成：`IHomeAssistantEventSubscriber` 经 Vault 密钥完成鉴权与订阅；域/实体白名单、ignore、冷却和去重后，仅调用 `DeviceSyncService.ApplyStateChangedAsync` 写 `DeviceState` 并触发既有自动化回调；后台宿主异常后退避重订阅，默认关闭以保持现有 REST/MCP 行为。定向状态幂等测试 5/5 通过，真实 HA WebSocket/Vault 联调待部署环境验证 |
| H4 | control_device/run_scene 受控写入 | 已完成：复用既有 Run Action、确认中心与 UUID 执行审计；MCP Adapter 仅调用固定 `ha_control_device`，本地校验映射设备、能力和值白名单，禁止任意 service / 自由 JSON；成功后以 `ha_get_state` 回读并统一经 `DeviceSyncService` 写状态和触发自动化；写入或回读超时为 `result_unknown` / `ha_result_unknown`，不得自动重试。真实 HA MCP 写入联调待部署环境验证 |
| H5 | Confirmation 结构化上下文与 L1 Grant | once/run preference、到期/撤销、L2/L3 不命中、审计与跨家庭隔离 |
| M1 | context_snapshots | 创建/版本/哈希/引用隔离，Run 中记忆变化不改变当前 snapshot |
| M2 | memory_candidates + review worker | evidence/confidence、敏感事实待确认、后台失败不影响主 Run、删除/租户隔离 |
| M3 | learning_memory_records + 只读查询 | M2 接受候选与事实源写入同事务生成投影；个人/家庭隔离、来源脱敏、游标分页、目标删除/失效同步归档；Web `/app/memories` 仅在 API、`memory.read` 与 `route_key` 发布后开放 |

## 21. 美团生活服务个人级 Connector（规划，未发布 API）

本节落实产品总设计 §7.3。`meituan-travel`、`meituan-paotui` 和美团分销推广/领券 Skill 均只能经 `MeituanLifeConnector` 进入产品；官方 CLI、MCP Server 或 API 是 Adapter 的可替换实现，不能成为 Agent、Web 或移动端的直接依赖。以下均为拟议设计，不代表已经取得美团接口许可、MCP 服务、代下单或代付能力。

### 21.1 连接、凭据与部署边界

- 每位成员创建独立的 `binding_scope=personal` 连接实例，所有查询、Run 与确认均从 JWT 推导 owner 和 tenant；其他成员一律按资源不存在处理，且响应不暴露 owner、Token 或美团账户标识。
- 旅行开发者 Token 仅允许通过 HTTPS 一次性提交到服务端，立即写入本地开发机的受控密钥存储；`workspace_connectors.config`、Run 参数/事件、审计详情、日志和 DTO 只能保存不可逆状态或 `credential_ref`。更新 Token 覆盖旧引用，撤销连接同时吊销引用；浏览器、LLM 上下文和子进程命令行均不得接收明文 Token。迁移 N100 时将密钥引用迁入 N100 的受控密钥存储，不改变客户端契约。
- 首期 `MtTravelCliAdapter` 由本地开发机的受控 worker 调用 `mttravel`；可执行文件、工作目录、环境变量白名单、标准输入/输出长度、1--2 分钟超时、取消与 stderr 脱敏必须由服务端固定。调用参数只可来自已校验的结构化请求，禁止拼接用户自然语言为 shell 命令。MTR-1a 验收后原样迁移至 N100；未来美团正式 MCP/API 可替换该 Adapter，不改变上层 Tool 契约。
- 美团账户登录、手机号、短信验证码、地址簿、支付和平台风控页面不进入 NexusMind。本系统只返回经许可的跳转目标，由用户主动打开美团 App 完成最终预订/支付；不得伪造交易完成状态。

本地开发机在 MTR-1a 验收前，按美团官方 Skill 文档安装 CLI 与旅行 Skill（命令须以美团当前文档为准）：

```bash
npm i -g mtskills-cli
mtskills i meituan-travel
```

这两条命令只在本地开发机的受控开发账户执行，用于准备 `MtTravelCliAdapter` 的运行依赖；它们不是 Web 前端能力，也不得由浏览器或 Agent 触发。美团 Token 仍由旅行连接页一次性提交给 NexusMind 服务端，再写入受控本机密钥存储；不得要求家庭成员在命令行中录入或向命令行参数传递 Token。MTR-1a 验收通过后，再由 N100 的受控部署账户重复安装并完成迁移验收。

### 21.2 旅行 MVP 的 Tool、Run 与事实数据

`travel.search` 是第一个可排期 Tool：接收城市、日期、人数、预算和偏好，创建异步个人 Run，结果保留“AI 家庭上下文摘要”和“美团原始供给详情”两个区块。原始价格、评分、距离、库存、费用与时效不得被模型改写、估算或与摘要混合。Adapter 失败、超时、授权失效和不可解析响应都以脱敏错误终止 Run，不降级伪造成功结果。

`travel.plan` 只生成行程、待办与日历 Action，复用既有 Run 的 L1 确认、幂等和审计语义；预订不创建 NexusMind 订单。日历与个人偏好仅使用用户明确确认的最小信息，完整地址、订单详情和任何支付信息均不写入家庭知识或学习记忆。

拟议而**尚未发布**的旅行 API 如下；在真实 Token、平台许可和部署验收前不得写入 Swagger 或面向客户端开放：

| 路由 | 用途 | 权限 / 约束 |
| --- | --- | --- |
| `GET /api/v1/connector-providers/meituan-travel/connection` | 返回本人连接的已配置状态、更新时间与可用性 | `connector.read`；不返回 Token、账户或 `credential_ref` |
| `PUT /api/v1/connector-providers/meituan-travel/connection` | 一次性接收 Token 并建立/更新个人连接 | `connector.authorize`；请求不可写审计明细，提交后服务端不回显 |
| `DELETE /api/v1/connector-providers/meituan-travel/connection` | 撤销本人连接和密钥引用 | `connector.authorize`；幂等并写脱敏审计 |
| `POST /api/v1/connector-providers/meituan-travel/runs` | 创建 `travel.search` 异步 Run | `ai.run` + `connector.read`；校验结构化参数、个人归属、取消/超时和结果脱敏 |

### 21.3 跑腿与领券的后置边界

- `errand.quote` 只能在平台账户授权、地址最小化、POI/费用来源和删除策略获批后提供，费用预览为 L2；`errand.create_order` 至少 L3，提交前必须比对预览与请求的参数哈希，超过 100 元再次显式确认，支付仍交给美团 App。订单状态仅做只读同步。
- `coupon.claim`、`coupon.history`、`coupon.reminder` 不进入当前开发。它们先完成分销资格、平台条款、隐私/营销独立评审、首次协议、用户主动订阅与退订、设备/账号清除路径；不得利用家庭敏感上下文或主动营销诱导消费。

### 21.4 切片、依赖与最小验收

| 切片 | 后端范围 | 前置依赖与最小验收 |
| --- | --- | --- |
| MTR-1a | 本地 Provider 注册、个人连接状态、受控 Token 引用、`MtTravelCliAdapter`、异步 `travel.search` Run 与脱敏结果 | 真实 Token 与美团许可确认；个人隔离、Token 不回显、超时/取消、原始数据不被摘要改写的测试及本地实机查询验收 |
| MTR-1b | N100 迁移与家庭部署验证 | MTR-1a 完成；在 N100 重新安装官方 CLI/Skill、迁移密钥引用和受控配置，验证重启恢复、健康检查、资源占用、局域网与外网失败降级；不改变 API/Tool/Web 契约 |
| MTR-2 | 家庭周末出游 Expert、日历/待办 L1 Action、个人偏好候选与外跳 | MTR-1a 完成；若面向家庭正式使用则还需 MTR-1b；确认幂等、外跳仅用户点击、未确认信息不入记忆的端到端验证 |
| MTP-1 | 跑腿费用预览的 Adapter 与 L2 确认设计 | 平台账户/地址簿许可、POI/价格展示规则与数据删除设计均已书面确认；未满足前不开发 |
| MTP-2 | 参数冻结的 L3 订单创建与只读订单状态 | MTP-1 完成；订单取消/售后/支付外跳与高额二次确认验收 |
| MTC-1 | 领券合规评审，不实现业务接口 | 分销资格、用户协议、隐私/营销审查、退订和清除机制均通过；否则长期保持未排期 |

## 22. 管家功能矩阵（手脚 2，规划，2026-08-14 确认）

产品总设计 §1.1 手脚 2 从单一「家庭财务 Agent」扩展为 **8 个管家功能矩阵**（财务/缴费/快递/宠物/日程协同/回忆/健康画像/出游），每个都是完整闭环（用户表达 → AI 主动行为 → 确认动作），互不重复验证"主动"的侧面。本节只定义跨切片复用的架构约束与数据边界；每个功能的具体迁移、服务、API 与验收在对应切片（B41 起，见开发计划）实施时展开。

### 22.1 跨切片复用资产（已有，不重造）

| 复用资产 | 已落地切片 | 管家功能消费方式 |
| --- | --- | --- |
| 确认中心（`pendingConfirmations`/L1 批量确认/幂等/审计） | B12 | 所有管家建议卡（省钱建议/缴费提醒/快递改投/断粮提醒/复查提醒）统一走确认中心，不新建第二套确认链路 |
| 文件上传与登记链路 | 剪辑 B29/B37 已跑通 | 账单 CSV/截图、缴费单、体检报告 PDF 的上传复用；生成文件登记复用 readToken 下载 |
| OCR 能力 | 财务 B41 首用 | 缴费单/体检报告 OCR 复用同一解析管线，按文档类型做模板化字段抽取 |
| 知识库 / 成员体系 | B9/B11 | 偏好沉淀（已拒绝项不再提醒）写知识库；成员级隔离沿用 owner 隔离 |
| Connector 框架 | B13+ | 快递100 MCP 作为新 Connector Provider 注册，遵循个人连接隔离与 Token 引用化 |
| 剪辑产物链路 | B37-B40 | 回忆管家的"家庭回忆"短视频复用渲染/登记/下载链路，不新起引擎 |

### 22.2 数据敏感分级与边界

- **健康数据（最高敏感）**：健康画像数据本地存储优先、不上云推理；任何场景不把报告原文/指标落 Prompt 或共享面；管家只做「记录、提醒、趋势提示」，**不做诊断**（合规红线）；安诊儿/医院报告由用户主动导入（截图/PDF），系统不反向抓取任何医疗平台。
- **财务数据（高敏感）**：账单 CSV 本地解析优先；仅把分析结论（类别聚合、建议项）写入共享面，原始交易明细不进入家庭知识之外的共享面。
- **快递/宠物/日程（中低敏感）**：沿用现有成员隔离与审计；快递运单号等同 personal Connector 凭据处理（引用化，不回显）。

### 22.3 通用能力需求（跨功能收敛）

1. **上下文聚合**：财务/缴费/宠物消耗共享「成员-支出-日期」数据面；日程协同复用成员日历（已有）；健康画像与日历联动（复查/疫苗到期进日历）。
2. **提醒分级**：所有到期提醒沿用产品 §3 推送聚合策略分级（L3>L2>周期摘要），未实现推送通道前以确认中心/对话内消息形式呈现。
3. **去重**：支付宝+微信同笔交易去重为财务主要技术难点，MVP 允许"疑似重复"人工确认，不自动合并。
4. **健康画像数据模型**（自建、本地）：基础档案（血型/过敏史/慢病史/手术史）+ 健康日历（疫苗/复查/体检/用药到期）+ 报告库（OCR 结构化指标 + 历次趋势），按成员隔离。

### 22.4 切片与验收

见开发计划「管家功能矩阵排期（B41 起）」：B41-B42 财务 → B43 缴费 → B44 快递 → B45 宠物 → B46 日程协同 → B47 回忆 → B48-B49 健康画像 → B50 出游（MTR-2）。执行顺序按依赖与数据源可得性推进，不跳跃；每个切片完成后按「执行与回写规则」同步本节与 `docs/api-implementation.md`。
