# NexusMind 后端开发设计

> **对应总纲：** `D:\HomeMind\core\docs\main\NexusMind-Product-Master-Design.md`  
> **代码仓库：** `D:\HomeMind\core`  
> **维护要求：** 总纲涉及 API、数据模型、执行策略或 Connector 变化时，必须在同一变更中更新本文；新增 HTTP 接口还必须更新 `docs/frontend-api-integration.md` 与 `docs/api-implementation.md`。

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
| `workspace_connectors` | 租户级已授权 Connector 实例 | tenant_id、connector_provider_id、name、status、auth_status、config、credential_ref、created_at、last_sync_at、last_health_at |
| `connector_tools` | Provider 声明的稳定外部工具契约 | connector_provider_id、name、description、input_schema、output_schema、permission、risk_level、require_confirm |
| `workspace_connector_tools` | 已授权实例实际可用的工具 | workspace_connector_id、connector_tool_id、status、availability_reason、last_checked_at |
| `connector_permission_grants` | 成员对连接/工具权限的显式许可 | user_id、workspace_connector_id、connector_tool_id 可空、permission、effect、confirmation_policy、granted_at、revoked_at |
| `smart_home_spaces` | 家庭空间 | tenant_id、name、space_type、sort_order |
| `smart_home_devices` | 已规范化设备 | tenant_id、workspace_connector_id、space_id、external_id、device_type、name、online_status |
| `device_capabilities` | 设备可读/写能力 | device_id、capability、value_schema、permission |
| `device_states` | 最近设备状态快照 | device_id、state_json、updated_at |
| `skills` | Skill 目录与权限声明 | key、input_schema、output_schema、required_permission |
| `experts` | Expert 目录 | code、name、category、builtin、status |
| `expert_versions` | 可复现的 Expert 策略版本 | expert_id、version、system_prompt、skill_policy、output_schema |
| `expert_skill_permissions` | Expert Version 调用 Skill 的限制 | expert_version_id、skill_id、max_calls、require_confirm |
| `expert_runs` | 一次专家运行 | user_id、tenant_id、expert_id、expert_version_id、status、input_context、token 用量、row_version |
| `run_events` | 客户端可展示的运行时间线 | run_id、sequence、type、step_id、status、display_message、created_at |
| `run_actions` | 运行中的建议/执行项 | run_id、action_type、payload、status、confirmed_at、idempotency_key、result |
| `scenes` / `scene_actions` | 家庭场景及其设备能力动作 | tenant_id、scene_id、device_id、capability、value |
| `automation_rules` | 已确认的长期规则 | tenant_id、trigger、actions、enabled、row_version |

设计和创建迁移前，先核对现有 `AiEntities.cs` 与 002/003 迁移，避免重复创建 Expert/Skill 基础表。新增迁移使用下一个未占用序号；已执行迁移不修改。

### 数据与安全约束

- 所有数据均由 JWT 中的 `tenant_id` 隔离，禁止客户端传入值覆盖；
- Connector Provider 与 Workspace Connector 分离；`status` 表示连接健康，`auth_status` 表示授权生命周期；`config` 仅保存经 Provider Schema 校验的非敏感配置，凭据只以 `credential_ref` 指向密钥服务，响应模型永不回传明文；
- Tool 使用 JSON Schema 描述输入/输出，在 Provider 内按名称唯一；实例只有在 `workspace_connector_tools.status=enabled` 且请求者拥有有效 `connector_permission_grants` 时才能调用，默认拒绝；
- 可变业务表含 `deleted_at`、`updated_at`、`sync_version`；目录及运行记录采用 `row_version`；
- `external_id` 在同一 Workspace Connector 内唯一；写操作使用 `idempotency_key` 防止重复下发；
- 状态历史和执行结果可追溯，敏感输入应脱敏后写入审计。
- Run 必须固定 `expert_version_id` 和已解析的权限快照；Run Event 只保存用户可理解事件，禁止写入 Prompt、思考链或模型日志。

## 5. 服务职责与执行链路

```text
ExpertsController
  → IExpertRunService（创建运行、返回建议）
  → IPlannerService（生成行动草案）
  → ISkillExecutor（权限校验、参数校验、分发）
  → IConnectorService（解析实例、Tool、授权和审计）
  → IConnectorAdapter（协议无关接口）
  → HomeAssistantConnectorAdapter（HA REST/WebSocket 实现）
```

`IConnectorService` 是对 Skill 的统一入口：列出可用 Tool、校验 JSON Schema 与 Permission、应用确认策略、创建/关联 Run Action，并把请求委派给 Adapter。`IConnectorAdapter` 至少应定义连接测试、工具发现、设备发现、读取状态、执行工具/命令四类能力。业务服务依赖这些接口以及标准化能力模型，不依赖 MQTT Topic、Zigbee 实体名或厂商 JSON 格式。

```csharp
public interface IConnectorAdapter
{
    string ProviderCode { get; }
    Task<IReadOnlyList<ConnectorToolDefinition>> DiscoverToolsAsync(WorkspaceConnector connector);
    Task<ConnectorExecutionResult> ExecuteAsync(ConnectorToolCall call);
}
```

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
| Expert Run | `POST /experts/{key}/runs`、`POST /housekeeper-runs`、`GET /expert-runs/{id}`、`GET /expert-runs/{id}/events`、`GET /expert-runs/{id}/actions` | 创建家庭管家分析、查看进度、事件和待确认方案 |
| Run Action | `POST /expert-runs/{id}/actions/{actionId}/confirm` | 明确确认待执行动作 |
| Automation | `GET/POST/PATCH /automation-rules` | 管理已确认自动化 |

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

阶段 8（Expert Files 与多专家团队编排）新增 8 张表 `expert_files`、`expert_file_objects`、`expert_file_attachments`、`team_run_templates`、`team_run_template_versions`、`team_runs`、`team_run_members`、`team_run_audits`，全部按 `tenant_id` 隔离并使用 UTC `DATETIME(3)`、乐观 `row_version`、状态/模式检查约束。文件二进制不进入数据库；对象存储由 `IExpertFileStorage` 抽象隔离，`LocalExpertFileStorage` 作为受控本地实现，`ExpertFiles:Storage:Enabled=false` 时返回可读 `503`。扫描走 `IExpertFileScanner`，默认仅做扩展名、MIME、大小、SHA-256 校验；状态固定为 `pending_upload | scanning | ready | rejected | deleted`，仅 `ready` 文件可被附加或被运行时读取。

团队编排仅支持显式 `sequential`、`parallel`、`synthesis` 三种模式；首个发布的 `teamVersion=1`；客户端提交时必须显式声明成员 `expertVersionId` 与文件 `fileIds`；服务端在创建时将模板冻结到 `team_run_template_versions`，并把每个成员的权限交集写入 `team_run_members`。`team_run_create`、`team_run_cancel`、`team_run_retry` 与成员、合成失败均落入 `team_run_audits`；`HomeMind.Automation` Meter 新增 `team_runs_triggered_total`、`team_run_members_failed_total`、`team_run_synthesis_failed_total` 计数器。所有响应 DTO 不返回存储凭据、内部对象路径、第三方文件 ID、成员 Prompt、模型思维链、供应商日志或原始中间输出；跨租户与跨用户的资源一律返回 `404`。外部效果仍由既有 `POST /api/v1/expert-runs/{id}/actions/{actionId}/confirm` 链路承担，团队编排不得绕过该边界。

## 9. 与前端的联动检查

前端目录与实施细节在 `D:\HomeMind\mobile\docs\main\NexusMind-Frontend-Development.md`；开发/UI 规则在 `D:\HomeMind\mobile\docs\DEVELOPMENT_GUIDELINES.md` 和 `D:\HomeMind\mobile\docs\UI_STYLE_GUIDE.md`。每次后端设计变更至少确认：接口是否已定义、加载/空/错误/确认状态是否可呈现、字段是否包含权限与最后更新时间、是否需要客户端刷新或轮询运行状态。
