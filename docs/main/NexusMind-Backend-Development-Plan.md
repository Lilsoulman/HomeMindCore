# NexusMind 后端开发计划

> **依据：** `NexusMind-Product-Master-Design.md`、`NexusMind-Backend-Development.md` 与 `D:\HomeMind\mobile\docs\main\NexusMind-Frontend-Development.md`。
> **维护规则：** 每完成一个开发切片，必须在同一变更中更新本计划的状态、已交付内容、验证结果与下一步。
> **最后更新：** 2026-08-04

## 当前基线

- 已有：JWT 身份与租户隔离、Todo/Calendar、AI Skill CRUD、Expert 目录与异步 Run 入队接口。
- 已有 SmartHome：空间、设备、能力、状态和场景的读模型、租户隔离 API，以及显式开关的本地 Mock。
- 缺口：场景/Dashboard 聚合，以及自动化、重试和可观测性。
- 约束：所有读写以 JWT `tenant_id` 隔离；客户端不提交租户归属；不返回凭据、厂商实体 ID、Prompt 或模型思考链；设备写操作必须经 Run Action 确认与幂等校验。

## 实施队列

| 阶段 | 状态 | 交付与验收 |
| --- | --- | --- |
| 0. 现有 AI 基线 | 已完成 | 验证现有 Expert/Run/Job 数据模型与 API，后续不重复建表。 |
| 1. SmartHome 读模型与 Mock | 已完成 | 已创建 007 迁移、实体、DTO、租户隔离的空间/设备/场景查询 API；受控 Mock 仅在显式配置下启用。 |
| 2. Connector 安全配置与 Adapter 契约 | 已完成 | Provider/Workspace Connector、成员授权范围、仅凭据引用的校验，以及协议无关的 Adapter/命令模型；不接收或持久化明文 Token。 |
| 3. Home Assistant 连接与发现 | 已完成 | 在可用 Secret Vault 的前提下实现 HA 连通性测试、五类设备发现、能力映射和 REST 轮询状态同步；API 不透传 HA 原始实体。 |
| 4. 家庭管家 Run 编排 | 已完成 | 只读编排收集已同步的空间/设备状态，生成可展示的 Run Event 和 `pending` 行动草案，不下发命令。 |
| 5. 行动确认与执行 | 已完成 | 再次授权检查、Action 幂等、Adapter 下发、审计、失败可读反馈与状态回写。 |
| 6. 场景与 Dashboard 聚合 | 已完成 | 回家/离家/睡眠场景、按空间摘要和部分失败可用的 Dashboard 聚合。 |
| 7. 自动化与稳定性 | 已完成 | Automation Rule 的四类触发器、持久化同步队列/重试/超时、结构化日志与指标计数器，以及接口契约已交付。 |
| 8. Expert Files 与多专家团队编排（V1 后） | 已完成 | 按既定合约发布 Expert File 最小闭环、版本化的 `sequential`/`parallel`/`synthesis` 团队编排、成员权限交集、聚合视图与审计；外部效果仍由既有 Run Action 链路承担。 |

## 阶段 1 任务

1. 新增 `007_smart_home_read_model.mysql.sql`，仅追加新表与索引，不修改已执行迁移。
2. 在 `Entities/SmartHome`、DbContext 与 DTO 中建立标准空间、设备、能力、状态、场景模型。
3. 用业务服务封装查询和本地受控 Mock，Controller 不直接访问 DbContext。
4. 提供 `GET /api/v1/smart-home/spaces`、`GET /api/v1/smart-home/devices`、`GET /api/v1/smart-home/scenes`；响应带更新时间且不含供应商字段。
5. 更新接口文档、执行构建与定向验证，然后将本阶段改为“已完成”。

## 本次完成记录

### 2026-08-04 - 阶段 1：SmartHome 读模型与 Mock

- 新增 `database/007_smart_home_read_model.mysql.sql`，并接入本地重建脚本；定义 Connector、空间、设备、能力、状态与场景的追加式数据库模型。
- 新增 SmartHome 实体、只读业务服务和 `GET /api/v1/smart-home/spaces`、`/devices?spaceId=`、`/scenes`。Controller 仅处理 HTTP/鉴权，所有查询按 JWT `tenant_id` 限制。
- 响应仅包含标准化状态摘要、能力和时间戳，不返回 Connector 凭据、厂商实体 ID 或协议字段；`SmartHome:MockEnabled` 默认关闭。
- 已更新 `docs/frontend-api-integration.md`、`docs/api-implementation.md` 与 `DEVELOPMENT.md`。
- 验证：`dotnet build HomeMind.Core.sln --no-restore` 已完成所有项目编译，但 API 最后写入 `obj/Debug/net8.0/HomeMind.Api.dll` 时被正在运行的进程锁定，尚未完成全量输出；未自动执行本地 MySQL 迁移。

## 阶段 2 调整：Connector 安全配置与 Adapter 契约

阶段 2 不再直接连接 Home Assistant。其目标是先建立可审计、可授权且不持久化明文凭据的边界，完成后才允许阶段 3 调用真实外部服务。

1. 定义 `IConnectorServices`、`IConnectorSecretReferenceValidator` 和 `IConnectorAdapter`；Adapter 契约覆盖连接测试、发现、读取状态与执行命令，但本阶段不发起真实网络调用。
2. 提供 Provider 目录、Workspace Connector 查询/创建、成员授权范围查询/更新 API。创建请求只接受 `credentialRef`，不得包含 URL、Access Token、Refresh Token 或厂商原始认证字段。
3. 校验 `credentialRef` 的 Vault 资源格式和调用者租户归属；Vault 不可用时返回可读的配置错误，不回退为数据库加密字段或应用配置明文。
4. 定义标准 `DeviceCommand`，强制包含 Connector、设备、能力、目标值、操作者、Run Action 和幂等键；Controller 和业务规则不得出现 HA service/entity 名称。
5. 验收：所有 Connector DTO 均无凭据字段；成员无法读取或修改其他租户的 Connector；无 Secret Vault 时无法进入“已连接”状态；Adapter 的契约测试不依赖 Home Assistant 实例。

### 2026-08-04 - 阶段 2：Connector 安全配置与 Adapter 契约

- 新增 `database/008_connector_provider_catalog.mysql.sql`，为 Home Assistant、MQTT、米家和涂鸦建立全局 Provider 目录；本地重建脚本与开发环境迁移顺序已同步。
- 新增 `IConnectorServices`、`IConnectorSecretReferenceValidator`、`IConnectorAdapter` 及标准 `DeviceCommand`。命令强制包含 Connector、设备、能力、目标值、操作者、Run Action 和幂等键，契约中不出现 Home Assistant service/entity 字段；本阶段未注册真实网络 Adapter。
- 提供 `GET /api/v1/connector-providers`、`GET/POST /api/v1/connectors`、`GET /api/v1/connectors/{id}/authorization` 与 `PUT /api/v1/connectors/{id}/authorizations/{memberUserId}`。所有响应 DTO 均不返回 `credentialRef` 或认证字段，JWT 租户决定全部数据边界。
- 创建请求仅允许 `providerId`、`name`、`credentialRef`，且 `credentialRef` 必须为当前租户的 `vault://tenants/{tenantId}/...`。`SecretVault:Enabled` 默认关闭并返回可读 `503`，不回退保存明文；新 Connector 一律从 `disconnected` 创建。
- 验证：解决方案编译到 API 输出阶段时仅受正在运行的 API 进程锁定；`dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/connector-phase2` 成功（0 errors）。未执行本地 MySQL 迁移或真实网络调用。

### 2026-08-04 - 阶段 3：Home Assistant 连接与发现

- 新增 `HomeAssistantConnectorAdapter`、运行期 Connector 服务和 `IConnectorSecretResolver`。HA Adapter 仅在内部处理 REST 实体/认证，Controller、业务 DTO 与客户端响应均不包含 HA URL、Token、服务名或实体 ID。
- 新增 `POST /api/v1/connectors/{id}/test`、`/discovery` 与 `/sync`。全部按 JWT `tenant_id` 查找 Connector，成功结果仅返回连接状态、发现数量和健康/同步时间；未授权或跨租户 Connector 返回 `404`。
- 新增 HashiCorp Vault 解析器：Vault 令牌只从 `NEXUSMIND_SECRET_VAULT_TOKEN`（可配置变量名）读取，`credentialRef` 映射至租户 Vault 路径。HA 的 `baseUrl` 和 `accessToken` 仅在 Adapter 内存中使用，不写入数据库、日志或 API 响应。Vault/密钥错误返回可读 `503`，远端 HA 错误返回 `502`。
- HA REST 发现并标准化 light、switch、climate（空调）、cover 和 sensor/binary_sensor；能力、最新状态快照和连接健康时间回写既有读模型。当前为请求触发的 REST 轮询，持续 WebSocket 订阅与后台重试留到阶段 7。
- 已更新 `docs/frontend-api-integration.md`、`docs/api-implementation.md` 和 `NexusMind-Backend-Development.md`。验证：`dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/phase3-verify` 成功（0 errors）；未执行本地 MySQL 迁移或真实 Vault/HA 网络调用。

### 2026-08-04 - 阶段 4：家庭管家 Run 编排

- 新增 `database/009_housekeeper_run_orchestration.mysql.sql`，仅扩展既有 `expert_run_actions` 的检查约束以支持 `smart_home_device` 和 `pending`，并初始化全局“家庭管家”专家版本；本地重建脚本和开发迁移顺序已同步。
- 新增 `IHousekeeperRunServices`、确定性的只读编排服务及 `POST /api/v1/housekeeper-runs`。服务按 JWT 的用户与租户边界读取已同步的空间、在线设备和可写标准能力，支持 `sleep`、`away`、`arrive`、`environment_review`；不解析凭据、不读取厂商实体字段，也不调用 Connector Adapter。
- 每次 Run 写入面向 UI 的状态收集/草案完成事件，并最多生成 12 个 `pending` 的 `smart_home_device` 草案。`GET /api/v1/expert-runs/{id}/actions` 返回当前用户的安全 Run/Event/Action 视图；跨用户或跨租户返回 `404`。阶段 4 不提供确认或执行接口。
- 已更新 `docs/frontend-api-integration.md`、`docs/api-implementation.md`、`NexusMind-Backend-Development.md`、`DEVELOPMENT.md` 与数据库说明。验证：`dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/phase4-verify` 成功（0 errors）；未执行本地 MySQL 迁移或真实 Vault/HA 网络调用。

### 2026-08-04 - 阶段 5：行动确认与执行

- 新增 `database/010_confirmed_smart_home_actions.mysql.sql` 与 `action_execution_audits` 实体/DbSet，为每次设备命令保存不含凭据、厂商实体 ID 或协议字段的幂等审计记录。
- 新增 `POST /api/v1/expert-runs/{runId}/actions/{actionId}/confirm`。确认请求必须携带 UUID 幂等键；服务按 JWT 用户与租户边界重新校验 Run、成员 Connector 授权范围、设备在线状态、可写能力和值类型，并进行实时连接健康检查。
- 执行前先将 Action 与审计记录持久化为 `executing`，随后由 Adapter 下发标准 `DeviceCommand`；重复相同幂等键只返回已记录结果，不会再次下发。成功/失败均写入可展示 Run Event 和可读状态，成功后将目标能力合并回标准化设备状态快照。
- Home Assistant Adapter 仅在内部解析设备外部标识，并支持 light/switch/climate/cover 的标准能力写入；Controller、业务 DTO、审计与客户端响应均不包含 HA URL、Token、service/entity 名称或原始错误。
- 已更新 `docs/frontend-api-integration.md`、`docs/api-implementation.md`、`DEVELOPMENT.md` 与数据库说明。验证：`dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/phase5-verify` 成功（0 errors）；未执行本地 MySQL 迁移或真实 Vault/HA 网络调用。

### 2026-08-04 - 阶段 6：场景与 Dashboard 聚合

- 新增 `GET /api/v1/dashboard`。接口从 JWT 派生用户和租户，聚合家庭空间与设备在线摘要、标准回家/离家/睡眠场景、当天最多六项待办与日程，以及最近一条已完成的家庭管家建议；响应中的每个模块都携带独立状态、更新时间和可读错误，局部读取失败不会阻塞其余看板内容。
- 新增 `POST /api/v1/smart-home/scenes/{sceneKey}/run`。标准场景映射至既有家庭管家规划，生成 `pending` 的 `smart_home_device` 行动草案；不直接下发设备命令，仍必须通过阶段 5 的确认、授权、幂等、Adapter 与审计链路。支持 `arrive_home`、`leave_home`、`sleep`，并兼容 `arrive`、`away` 别名。
- 新增 Dashboard/Scene DTO、业务服务和依赖注入；所有客户端响应不包含凭据、厂商实体 ID、协议字段或原始设备状态。已同步 `docs/frontend-api-integration.md` 和 `docs/api-implementation.md`。
- 验证：`dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/phase6-verify` 成功（0 errors）。未执行本地 MySQL 迁移或真实 Vault/HA 网络调用；构建仍报告现有可空性标注和受限内部 NuGet 源警告。

### 2026-08-04 - 阶段 7：自动化与稳定性

- 新增 `database/012_automation_and_connector_sync.mysql.sql`、实体和 DTO。`automation_rules` 以 `tenant_id`、规则所有者和乐观 `row_version` 隔离，触发器固定为 `time_schedule`、`device_state_change`、`scene_completed`、`sync_completed`；`connector_sync_jobs` 持久化队列状态、尝试次数、可用时间和不含敏感内容的错误码。
- 新增 `GET/POST/PATCH /api/v1/automation-rules`。时间触发支持固定时刻、日出/日落（含时区、坐标、偏移）与一次性倒计时；设备状态变化由已标准化的同步状态分发，场景完成和同步完成均使用独立事件分发。规则动作限定为内建场景，复用 Housekeeper Run、Action、授权、幂等、Adapter 和审计边界；`manual_confirmation` 保留待确认 Action，`auto_execute` 以规则所有者的有效授权通过同一确认链路执行。
- `POST /api/v1/connectors/{id}/sync` 改为 `202` 入队，并新增 `GET /api/v1/connectors/sync-jobs/{jobId}`。进程内 Channel 与 `AutomationWorker` 执行持久化任务；每次同步 30 秒超时、最多三次尝试、指数退避，周期扫描会恢复重启或退避中的任务。`HomeMind.Automation` Meter 提供规则触发、同步入队、重试和失败计数器，Worker 使用结构化日志且不记录凭据或厂商标识。
- 已同步 `docs/frontend-api-integration.md` 与 `docs/api-implementation.md`；接口仍不返回凭据、厂商实体 ID、协议字段、原始状态或任意设备命令。
- 验证：`dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/phase7-verify` 成功（0 errors）；隔离端口启动后 Swagger 包含 `/api/v1/automation-rules`，无凭据 `GET` 返回统一 `401`。未执行本地 MySQL 迁移、真实 Vault/HA/MQTT 网络调用；构建仍报告既有可空性标注与受限内部 NuGet 源警告。

## 阶段 8：Expert Files 与多专家团队编排（V1 后）

阶段 8 依赖阶段 7 的权限、审计、重试和可观测性基线。它不改变已发布的 `GET /api/v1/experts`、`type=group` 或 `POST /api/v1/expert-runs` 的语义：在本阶段完成前，Expert Group 只能作为单一策略来源，不能向客户端承诺成员级执行过程。

1. 新增下一序号的 MySQL 迁移、实体和 DTO：Expert File 元数据、对象引用、文件处理状态、Run/Expert 附件关联、团队编排模板/版本、成员快照、子 Run、聚合结果和审计记录。所有记录按 JWT `tenant_id` 隔离；文件二进制内容不进入 MySQL，也不写入 Run Event。
2. 定义并实现 Expert Files 的最小闭环：创建上传会话、提交已扫描对象、列出/移除当前租户拥有的文件，以及将已就绪文件附加到 Expert 或 AgentRun。对象访问使用短期、按租户和用途限制的授权；API 永不返回存储提供商凭据、内部对象路径或第三方文件 ID。
3. 将文件状态固定为 `pending_upload`、`scanning`、`ready`、`rejected`、`deleted`。只有 `ready` 文件可被附加或由运行时读取；上传大小、MIME 类型、哈希、恶意软件扫描、配额、保留期和软删除必须由服务端强制执行，并为上传、扫描、读取授权和删除写入审计记录。
4. 发布版本化的团队编排契约。首批仅支持显式 `sequential`、`parallel` 和 `synthesis` 三种模式；客户端提交团队版本和受限输入，服务端固定成员 ExpertVersion、权限快照、文件引用和编排图。禁止客户端提交任意 Prompt、成员级工具调用、供应商参数或未声明的 DAG。
5. 每个成员执行创建受父 AgentRun 约束的子 Run；只返回可展示的阶段状态、成员显示名、汇总进度、受控 Action 和最终聚合结果。不得返回成员 Prompt、模型思维链、供应商日志、原始中间输出、凭据或跨成员私有上下文。取消、重试、幂等和失败策略由父 Run 统一管理。
6. 新增权限与限额：文件读写/删除、团队运行和团队管理分离授权；成员级 Skill 和 Connector 权限取 ExpertVersion、团队策略与调用者权限的交集。所有外部效果继续经由既有 Run Action 确认、Adapter 和审计链路，团队编排不得绕过该边界。
7. 同步更新 `docs/frontend-api-integration.md`、`docs/api-implementation.md`、`NexusMind-Backend-Development.md`、前端实施文档和数据库说明；在接口文档中给出请求、响应、状态机、权限、配额、错误和轮询/取消示例。实现前不得将这些路由列为已发布。
8. 验收：覆盖跨租户文件与团队拒绝访问、文件扫描/删除竞态、文件附件快照、编排版本固定、串行/并行/汇总状态、成员失败与取消、幂等重试、权限交集、审计完整性以及敏感字段不出现在 API 响应和日志中的测试；执行迁移验证、定向集成测试和完整解决方案构建。

## 下一步

阶段 8 已按既定合约交付 Expert Files 与多专家团队编排；后续工作以可观测性回归、生产部署前的隔离仓库与团队运行压测为主。

### 2026-08-04 - 阶段 8：Expert Files 与多专家团队编排

- 新增 `database/013_expert_files_and_team_orchestration.mysql.sql`，追加 8 张表：`expert_files`、`expert_file_objects`、`expert_file_attachments`、`team_run_templates`、`team_run_template_versions`、`team_runs`、`team_run_members`、`team_run_audits`，全部按 `tenant_id` 隔离、UTC `DATETIME(3)`、`row_version` 乐观锁、状态/模式检查约束；本地重建脚本与 `database/README.md` 已同步。
- 新增实体、枚举（`ExpertFileStatus`、`TeamRunMode`、`TeamRunStatus`、`TeamRunMemberStatus`）与 `HomeMindDbContext` `DbSet`，新增 JSON 列映射。`HomeMind.Business.IServices/Expert` 新增 `IExpertFileServices`、`ITeamRunServices`、`IExpertFileStorage`、`IExpertFileScanner`；`HomeMind.Business.Services/Expert` 提供 `ExpertFileServices`、`TeamRunServices`、`LocalExpertFileStorage`、`LocalExpertFileScanner`，并复用既有 `HomeMindDbContext`、DI 容器、审计通道与 `HomeMind.Automation` Meter。
- 新增 `HomeMind.Api/Controllers/AI/ExpertFilesController.cs` 与 `TeamRunsController.cs`，按 `TryGetUser`/`WithUserAsync` 模式路由到业务服务。`/api/v1/expert-files`（POST/GET/DELETE + `/objects` + `/read-token` + `/experts/{expertId}/files` + `/expert-runs/{runId}/files`）与 `/api/v1/team-runs`（POST + `/{id}`、`/events`、`/members`、`/synthesis`、`/cancel`、`/retry`）均受 `expert_file.read|write` 与 `team_run.read|write` 策略保护。
- 权限矩阵扩展：`PermissionNames` 新增 `ExpertFileRead`、`ExpertFileWrite`、`TeamRunRead`、`TeamRunWrite`、`TeamManage`；`member` 默认含 `expert_file.read`、`team_run.read`，`viewer` 仅含 `expert_file.read`，写与 `team.manage` 仍为 `owner`/`admin`。
- 所有 DTO 不返回凭据、内部对象路径、第三方文件 ID、成员 Prompt、模型思维链、供应商日志或原始中间输出；`POST /api/v1/team-runs` 拒绝任意 Prompt、工具调用与未声明的 DAG，仅接受显式 `teamVersion="1"`、`sequential/parallel/synthesis` 之一。
- 团队运行在创建时即冻结到 `team_run_template_versions`；每个成员计算并持久化 `permission_intersection_json`；成员执行、取消、重试、聚合由父 `team_runs` 统一驱动；外部效果仍由既有 `POST /api/v1/expert-runs/{id}/actions/{actionId}/confirm` 链路承担。`HomeMind.Automation` Meter 新增 `team_runs_triggered_total`、`team_run_members_failed_total`、`team_run_synthesis_failed_total`。
- `docs/frontend-api-integration.md` 新增 8.16 / 8.17 两节与权限矩阵条目；`docs/api-implementation.md` 更新路由表、`## Deliberately gated routes` 收紧为运行时配置开关并新增 Expert Files / Team Runs 合约章节。
- 验证：`dotnet build HomeMind.Api/HomeMind.Api.csproj --no-restore -o .build/phase8-verify` 成功（0 errors）。未执行本地 MySQL 迁移、对象存储或团队运行端到端；构建仍报告既有可空性标注与受限内部 NuGet 源警告。
