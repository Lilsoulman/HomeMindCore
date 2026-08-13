# NexusMind × Hermes Agent / Hermes Studio 技术融合分析

> 研究基线：`NousResearch/hermes-agent@222465d`、`JPeetz/Hermes-Studio@356b3d5`（2026-08-12）  
> 目标版本：NexusMind V2.5 参考架构  
> 结论定位：参考实现与设计模式提取，不进行 Python/React 整体移植

## 0. 结论摘要与事实校准

Hermes 最值得 NexusMind 借鉴的不是它的技术栈，而是六个运行机制：小而稳定的 HA 工具面、WebSocket 事件过滤、冻结的记忆快照、会话后后台复盘、阻塞式审批恢复、MCP 工具动态发现。NexusMind 已有 `IDeviceAdapter` / `IDeviceDiscovery` / `IDeviceCommandExecutor`、Run Action、L1/L2/L3、确认中心、家庭知识与审计，因此融合应表现为“替换或增强底层实现”，而不是新增平行业务模型。

源码核对发现三项需先校准：

1. 当前 Hermes Studio 是 React 19 + TypeScript + TanStack，不是 Vue；其交互模式可移植到 Flutter，但组件代码不能直接复用。
2. Hermes 的 HA WebSocket 插件会订阅 `state_changed`，将筛选后的变化转成 Agent `MessageEvent`；它是消息平台适配器，不是通用设备状态仓库。NexusMind 应复用其连接、过滤、冷却和重连思想，事件必须先进入 `DeviceSyncService`，不得直接触发业务 Agent。
3. 未在当前源码中发现“每 15 个任务周期固定回顾技能”的常量或状态机。当前实现是每轮可启动后台 review，并以 `.usage.json`、stale/archive 天数和 curator 规则治理技能。因此 NexusMind 不应把“15 次”写成 Hermes 既有事实，可将其设计为自身可配置阈值。

两个仓库均为 MIT License；复制具体代码时仍需保留许可证声明。本文建议主要复用模式并用 .NET/Flutter 重写。

## 第一部分：功能点速览与融合价值

| 功能点 | Hermes 实现方式 | 融合价值 | 为什么值得融合 |
| --- | --- | --- | --- |
| HA 四工具 | REST API + Bearer Token；实体/状态/服务发现和服务调用四个工具 | ★★★★★ | 能以很小工具面覆盖 HA 核心能力，适合先做 NexusMind P0 设备接入 |
| HA 实时事件 | WebSocket 鉴权后订阅 `state_changed`，实体/域白名单、忽略列表、冷却去重 | ★★★★★ | 可补齐 NexusMind N97 本地状态同步与低延迟事件入口 |
| MCP 生命周期 | stdio、Streamable HTTP、SSE；发现注册、Schema 缓存、`list_changed`、重连与工具撤销 | ★★★★★ | 与已确定 HA MCP 路径直接匹配，可升级现有 `IMcpProcessClient` |
| 执行审批卡片 | 阻塞 Agent 工具调用；once/session/always/deny；Gateway 原生端点与聊天命令双路径 | ★★★★★ | UI 模型和阻塞恢复值得借鉴，可直接映射现有确认中心 |
| 冻结记忆快照 | `MEMORY.md`/`USER.md` 有界存储；会话开始注入，期间写入不改变 Prompt | ★★★★★ | 兼顾上下文稳定、缓存命中和跨会话记忆，适合 NexusMind Context Snapshot |
| 后台记忆/技能复盘 | 每轮后 fork review，工具白名单，仅写记忆/技能；支持独立低成本模型 | ★★★★☆ | 能把家庭偏好和成功工作流沉淀从主响应链路中解耦 |
| SQLite 历史与 FTS | 全量会话持久化、FTS5 搜索、故障时本地 spool 恢复 | ★★★★☆ | N97 本地检索与离线恢复有价值，但 MySQL 云端不能照搬 SQLite FTS5 |
| 自动技能治理 | `skill_manage` + SKILL.md；read-before-write、来源保护、安全扫描、usage sidecar、stale/archive | ★★★★☆ | 为 NexusMind Skill 演进提供版本、验证、去重和归档机制 |
| Crew 多 Agent | 角色/persona/model/profile/session；向全部或指定成员并行 dispatch | ★★★☆☆ | 可优化已有专家团队配置，但现有后端编排已更接近业务事实层 |
| Workflow DAG | task/edge、环检测、拓扑分层、同层并行、SSE 节点状态 | ★★★★☆ | 可直接补强 NexusMind 团队编排的可视化和执行图模型 |
| Studio 事件审计 | SQLite append-only Event Store、session seq、SSE Last-Event-ID 重放、TTL/上限 | ★★★★☆ | 可借鉴断线重放；长期合规仍应写 NexusMind `family_audit_logs` |

## 第二部分：可直接复用的功能模块

## 模块一：HA 工具集实现（P0）

### Hermes 实现机制

`tools/homeassistant_tool.py` 注册四个 Agent 工具：

| Tool | 参数 | 调用 | 输出策略 |
| --- | --- | --- | --- |
| `ha_list_entities` | `domain?`、`area?` | `GET /api/states` | 只返回 entity_id/state/friendly_name，减少上下文 |
| `ha_get_state` | `entity_id` | `GET /api/states/{entity_id}` | 返回 attributes、last_changed、last_updated |
| `ha_list_services` | `domain?` | `GET /api/services` | 压缩服务描述和字段说明 |
| `ha_call_service` | `domain`、`service`、`entity_id?`、`data?` | `POST /api/services/{domain}/{service}` | success、service、affected_entities |

实现中的可复用安全点：

- `HASS_URL` 与 `HASS_TOKEN` 按 profile secret scope 读取，Bearer Token 不进入工具参数。
- entity_id、domain、service 使用正则校验，先校验再拼 URL，防路径穿越。
- 阻止 `shell_command`、`command_line`、`python_script`、`pyscript`、`hassio`、`rest_command` 等高危域；源码明确指出 HA 没有服务级权限，安全必须由上层承担。
- 10–15 秒请求超时；异常转成 Tool Error，但当前会把底层异常文本拼给模型，NexusMind 应进一步脱敏和结构化。
- `data` 被定义为 JSON 字符串，兼容部分模型但缺乏 schema 约束；NexusMind 不应照搬这一点。

`plugins/platforms/homeassistant/adapter.py` 走 HA WebSocket：连接 `/api/websocket` → 收到 `auth_required` → 发送 access token → 等待 `auth_ok` → `subscribe_events(event_type=state_changed)`。事件通过 `watch_domains`、`watch_entities`、`ignore_entities`、`watch_all` 过滤，并按 entity 设置 cooldown，之后才转换成 Agent 消息。发送通知则改用 REST `persistent_notification/create`，避免 WebSocket 请求竞争。

内置工具与 MCP 是并行工具源：内置 HA 工具由 `HASS_TOKEN` 是否存在决定可用性；MCP 客户端连接外部 Server 后把发现的工具注册进同一 Registry。当前源码没有 HA 专用自动切换策略，若同时启用且名称不同，两套工具会同时暴露给模型。因此“共存/切换”必须由 NexusMind Connector 显式治理。

### NexusMind 融合方案

保持现有业务边界：

```text
Agent Tool（标准化名称）
  → SmartHomeToolRouter（发现/权限/风险/确认）
  → IDeviceDiscovery / IDeviceCommandExecutor
  → HomeAssistantMcpAdapter（默认）或 HomeAssistantRestAdapter（回退）
  → HA MCP Server / Home Assistant
```

第一阶段只向模型暴露四个产品级工具，不暴露原始 HA 工具：

```json
{
  "name": "smart_home.search_resources",
  "description": "搜索当前家庭已授权的空间、设备、场景与能力。只读，不改变设备状态；调用控制工具前必须先使用本工具解析目标。",
  "inputSchema": {
    "type": "object",
    "properties": {
      "workspace_connector_id": { "type": "integer" },
      "query": { "type": "string", "maxLength": 100 },
      "room_id": { "type": "integer" },
      "capability": { "type": "string" },
      "limit": { "type": "integer", "minimum": 1, "maximum": 20, "default": 10 }
    },
    "required": ["workspace_connector_id"],
    "additionalProperties": false
  }
}
```

其余三个为 `smart_home.get_state`、`smart_home.control_device`、`smart_home.run_scene`。模型只能提交 NexusMind `device_id`/`room_id`/标准 capability；`HomeAssistantMcpAdapter` 在本地映射到 entity_id 和 HA service。自动化 CRUD、任意 service 调用、摄像头和媒体暂不开放。

传输选择采用明确主备：

- `mode=mcp`：N97 默认路径，调用 HA MCP Server；支持 stdio，同机容器可用受控 Streamable HTTP。
- `mode=rest_fallback`：仅运维降级，仍通过同一 Adapter 契约，不向 Agent 增加第二套 Tool。
- `mode=disabled`：连接器不可用。
- 禁止“两个模式同时注册 Tool”；切换只发生在 Adapter 内部，`Connector Tool` 标识保持稳定。

实时状态由单独的 `IHomeAssistantEventSubscriber` 负责。它不得把每个 `state_changed` 直接推给 Agent，而是先完成：entity 映射 → 白名单过滤 → 去重/冷却 → 写 `DeviceState` → 生成必要的 `RunEvent`/管家动态。只有匹配已启用自动化或异常阈值的事件才进入 Agent/规则引擎。

### 代码设计参考

```csharp
public interface IHomeAssistantToolClient
{
    Task<IReadOnlyList<HaEntitySummary>> ListEntitiesAsync(
        HaEntityQuery query, CancellationToken cancellationToken = default);
    Task<HaEntityState> GetStateAsync(
        string entityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HaServiceDefinition>> ListServicesAsync(
        string? domain, CancellationToken cancellationToken = default);
    Task<HaServiceCallResult> CallServiceAsync(
        HaServiceCall request, CancellationToken cancellationToken = default);
}

public interface IHomeAssistantEventSubscriber
{
    IAsyncEnumerable<HaStateChangedEvent> SubscribeStateChangesAsync(
        HomeAssistantSubscription subscription,
        CancellationToken cancellationToken = default);
}

public sealed record HomeAssistantSubscription(
    IReadOnlySet<string> EntityIds,
    IReadOnlySet<string> Domains,
    TimeSpan Cooldown);

public enum HomeAssistantTransportMode
{
    Mcp,
    RestFallback,
    Disabled
}
```

`HomeAssistantMcpAdapter` 实现现有 `IDeviceAdapter`、`IDeviceDiscovery`、`IDeviceCommandExecutor`，内部组合 `IHomeAssistantToolClient`，而不是让业务层依赖 MCP Tool 名。建议扩展现有 `IMcpProcessClient` 为 transport-neutral `IMcpClientSession`：

```csharp
public interface IMcpClientSession : IAsyncDisposable
{
    Task<McpInitializeResult> InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken = default);
    Task<JsonDocument> CallToolAsync(string name, JsonElement arguments,
        CancellationToken cancellationToken = default);
    event EventHandler<McpToolsChangedEventArgs>? ToolsChanged;
}
```

错误码固定为：`ha_auth_failed`、`ha_entity_not_found`、`ha_service_not_allowed`、`ha_validation_failed`、`ha_timeout`、`ha_disconnected`、`ha_result_unknown`、`mcp_tool_unavailable`。写操作超时返回 `ha_result_unknown`，不得自动重复调用；由 Action 幂等记录和状态回读决定是否重试。

### 数据模型建议

现有 `workspace_connectors`、`smart_home_devices`、`device_states` 可复用，只增加运行配置或独立本地配置，不把 HA Token 入 MySQL：

| 字段 | 说明 |
| --- | --- |
| `transport_mode` | `mcp/rest_fallback/disabled` |
| `credential_ref` | N97 Secret/Vault 引用，不存 Token |
| `mcp_server_name` | 本地配置中的 HA MCP Server 名称 |
| `tool_manifest_hash` | 当前工具清单哈希，用于漂移检测 |
| `last_event_cursor` | 可选，状态订阅恢复点/最后事件时间 |
| `last_health_at/error_code` | 健康与脱敏错误 |

### 潜在风险与适配点

- Hermes 的 area 过滤实际通过 friendly_name/attributes 模糊匹配，不等价于 HA Area Registry；NexusMind 必须经 WebSocket Registry 或 MCP 明确 Tool 建立可靠映射。
- HA Long-Lived Token 权限粗；必须在 NexusMind 继续执行成员、空间、设备、能力和 L1/L2/L3 权限。
- 任意 `data` JSON 会绕过能力 schema；只允许 Adapter 生成白名单字段。
- `state_changed` 高频，必须合并连续数值更新，传感器不应逐条唤醒模型。
- N97 断网时，本地事件仍可入本地队列；恢复云连接后按摘要同步，不上传全量家庭原始状态历史。

## 模块二：执行审批卡片（P0）

### Hermes 实现机制

Hermes 危险命令检测和审批是同步安全门：Agent 工具线程登记 `_ApprovalEntry` 后等待 `threading.Event`；Gateway 可以并发维护每个 session 的 FIFO 审批队列。用户 `/approve` 或 `/deny` 后设置结果并唤醒原工具调用。支持 `once`、`session`、`always`；永久 allowlist 写入 profile 配置。审批模式另有 `manual`、`smart`、`off`，smart 可由辅助模型先判低风险，但仍保留人工覆盖。并发 session 使用 ContextVar 隔离，避免共享环境变量造成串权。

Studio 的 Approval Card 展示 Agent 名、action/command、可展开 context，提供 Approve Once、This Session、Always Allow、Deny。服务端先尝试 Gateway 原生 approve/deny endpoint，失败再向目标 session 发送聊天命令。浏览器 store 只在内存 + `sessionStorage` 保存卡片状态，主要用于 UI 去重和短暂回执，不是合规审计事实来源。Studio 另有 SQLite Event Store，用单调 seq 和 SSE `Last-Event-ID` 支持断线重放，并提供跨 session audit 查询。

### NexusMind 融合方案

NexusMind 不采用“阻塞 HTTP 请求等待用户”的方式。创建 `ExpertRunAction` 与 `ConfirmationItem` 后，Run 进入 `needs_input`/`pending_actions` 并持久化；Flutter 收到确认卡，确认接口在新请求中恢复执行。这样适配移动网络、服务重启、多人家庭和分钟级等待。

Hermes 的三个批准范围映射如下：

| Hermes | NexusMind | 规则 |
| --- | --- | --- |
| Approve Once | 本次 Action 确认 | L1/L2/L3 均可，仍需当前操作者权限 |
| This Session | 当前 Run 的受限自动确认偏好 | 仅 L1；限定同 Tool、同设备/空间、同参数约束和到期时间 |
| Always Allow | 家庭/成员确认偏好规则 | 只允许 L1；owner/admin 配置；不得覆盖静态/动态风险升级 |
| Deny | Confirmation denied + Action rejected | 记录操作者、原因和时间，不再执行 |

L2/L3 永远不得因 session/always 规则自动批准。Hermes 的 smart approval 可作为“建议风险/说明生成器”，不能成为确认主体。

Flutter `ConfirmationCard` 建议字段：标题、Agent/专家、动作摘要、风险 Badge、目标空间/设备、影响范围、可逆性、参数 Diff、依据摘要、过期时间、`确认一次`、`拒绝`；仅对 L1 且有权限的用户显示“本次运行内自动确认同类动作”，永久规则移到设置页，避免误触。

### 代码设计参考

```csharp
public interface IActionApprovalOrchestrator
{
    Task<ApprovalDecisionView> RequestAsync(
        ActionApprovalRequest request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> ResolveAsync(
        long confirmationId,
        long actorUserId,
        ResolveApprovalRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ActionApprovalRequest(
    long HomeId,
    long RunId,
    long ActionId,
    string ToolName,
    string RiskLevel,
    string TargetScopeJson,
    string ArgumentDigest,
    DateTime ExpiresAt);

public sealed class ApprovalGrant
{
    public long Id { get; set; }
    public long HomeId { get; set; }
    public long GrantedByUserId { get; set; }
    public string Scope { get; set; } = "action"; // action/run/preference
    public string ToolName { get; set; } = "";
    public string ConstraintJson { get; set; } = "{}";
    public string MaxRiskLevel { get; set; } = "L1";
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
```

执行状态机：

```text
planned → pending_approval → approved → executing → executed
                         ↘ denied
                         ↘ expired
approved/executing → failed | result_unknown
```

确认请求必须携带 UUID 幂等键；事务内锁定 Confirmation 与 Action，复验 tenant、成员、角色、设备可用性、最终风险等级和过期状态，再写 `approval_resolved` 审计并投递执行。执行 Worker 使用 outbox/队列异步执行，前端通过现有轮询或后续 SSE/WebSocket 看状态。

### 潜在风险与适配点

- Studio 的 sessionStorage 不能承担 NexusMind 审计；MySQL 是确认事实源，本地事件库只做重放缓存。
- 审批上下文必须强制脱敏，尤其 HA Token、URL 查询参数、文件路径、MCP 原始结果。
- “Always” 不能按命令字符串匹配；必须是结构化 Tool + 资源范围 + 参数限制 + 到期/撤销。
- 多家庭同时审批必须绑定 `home_id`、`actor_user_id`、`run_id`、`action_id`，不能只按 session key。
- 用户确认后资源状态可能变化，执行前必须再次复验。

## 模块三：分层记忆系统（P1）

### Hermes 实现机制

Hermes 当前可确认的记忆层次为：

1. `MEMORY.md`：Agent 对环境、流程和经验的声明式记忆，默认 2,200 字符。
2. `USER.md`：用户画像与交互偏好，默认 1,375 字符。
3. SKILL.md：程序性记忆，表达“如何完成一类任务”，按需加载。
4. SQLite 会话历史：全量 transcript 与 FTS5 搜索；上下文压缩会分裂/轮换 session，同时保留持久历史。
5. 单选外部 Memory Provider：`prefetch`、`queue_prefetch`、`sync_turn`、`on_pre_compress`、`on_session_end` 等扩展点。

有界 MemoryStore 在会话开始读取文件并冻结 `_system_prompt_snapshot`。会话内 add/replace/remove 会立即原子落盘，但不修改当前 system prompt，以维持 prefix cache；tool response 返回 live state。超限不会静默裁剪，而是要求模型合并/删除后重试，并限制同一 turn 最多三次失败，防止循环耗尽预算。写入前执行注入/外传模式扫描、文件锁、漂移检测和备份，避免并发或手工编辑导致覆盖。

所谓“记忆冲刷”在当前源码中不是单一函数，而是两个时点：每轮后 `background_review` fork 会复盘对话并写 memory/skills；上下文压缩前外部 provider 收到 `on_pre_compress`，session 结束收到 `on_session_end`。后台 fork 复用主模型缓存或切到低成本模型；仅开放 memory/skill 管理工具，主对话不被打断。Gateway reset 前会显式 drain 后台写队列，避免已接受的记忆在切会话时丢失。

FTS5 用于会话检索，但当前取证未证明一个固定的“任务层→子任务层→情境层”表结构；这种层级更适合作为 NexusMind 自己的 Context Assembly 规则，而不是宣称 Hermes 已有对应实体。

### NexusMind 融合方案

NexusMind 不采用共享 Markdown 文件作为家庭事实源。将 Hermes 模式映射为：

| Hermes 层 | NexusMind 层 | 存储位置 |
| --- | --- | --- |
| USER.md | 成员偏好/交互偏好 | MySQL 个人可见偏好表；必要摘要下发 N97 |
| MEMORY.md | 家庭稳定事实与决策摘要 | `family_knowledge`、`decision_history` |
| SKILL.md | Expert/Skill 版本化程序知识 | `skills` / `ai_skills` / ExpertVersion |
| SQLite history | 会话消息与本地检索副本 | 云端 `conversation_messages` + N97 加密 SQLite FTS |
| external provider | 可插拔家庭知识检索器 | `IMemoryProvider`，默认 LocalHybridMemoryProvider |

每次 Expert Run 创建 `ContextSnapshot`：冻结当前家庭事实、成员偏好、相关决策、设备摘要、Skill/Expert 版本和检索结果。Run 期间新记忆不改变快照；只有用户明确补充且触发 `needs_input` 后创建新 snapshot version，避免上下文漂移和审计困难。

召回采用三层漏斗：

```text
任务层：当前 Run 输入、绑定 Expert/Skill、已选 Connector
  → 子任务层：Planner 当前步骤、相关设备/空间/文件
  → 情境层：家庭事实、成员偏好、近期决策、相关历史摘要
```

先执行强约束过滤（tenant/user/visibility/type/time），再做 MySQL 索引或 N97 SQLite FTS/BM25 关键词召回，最后可选本地 embedding 重排。每类限制条数与字符预算，优先返回摘要和 source id；Agent 必须能追溯来源，不能把召回内容当系统指令。中文检索需验证 tokenizer；SQLite 默认 unicode61 对中文粒度有限，可选 FTS5 trigram/CJK 扩展，或先用 MySQL ngram/业务关键词索引。

后台“记忆冲刷”设计为 `MemoryReviewWorker`：Run 完成、上下文压缩前、会话结束三个触发点；输入为脱敏的 Run 摘要、用户纠正、工具结果摘要和现有候选知识。模型只产生 `MemoryCandidate`，不直接写事实表。偏好或低风险事实可按配置自动接受，涉及成员身份、健康、财务、安防和冲突事实必须进入确认或冲突消解。

### 代码设计参考

```csharp
public interface IMemoryProvider
{
    Task<IReadOnlyList<MemoryRecall>> RecallAsync(
        MemoryQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryCandidate>> ReviewAsync(
        MemoryReviewInput input, CancellationToken cancellationToken = default);
}

public interface IContextSnapshotServices
{
    Task<ContextSnapshotView> CreateAsync(
        long runId, ContextSnapshotRequest request,
        CancellationToken cancellationToken = default);
    Task<ContextSnapshotView> GetAsync(
        long runId, int version,
        CancellationToken cancellationToken = default);
}

public sealed record MemoryQuery(
    long HomeId,
    long UserId,
    string Task,
    IReadOnlyList<string> Categories,
    int Limit,
    int CharacterBudget);
```

建议新增：

| 表 | 核心字段 |
| --- | --- |
| `context_snapshots` | `run_id`、`version`、`expert_version_id`、`skill_versions_json`、`knowledge_refs_json`、`preference_refs_json`、`device_state_refs_json`、`content_hash`、`created_at` |
| `memory_candidates` | `home_id`、`owner_user_id?`、`source_run_id`、`kind`、`key`、`proposed_value`、`confidence`、`evidence_refs_json`、`status`、`risk_level`、`expires_at` |
| `memory_recall_audits` | `run_id`、`provider`、`query_hash`、`result_refs_json`、`token/char_budget`、`created_at` |

个人偏好需要独立 visibility；不可把 USER.md 等价成全家庭共享知识。N97 的 SQLite 是本地派生索引，可删除重建，不成为跨端事实源。

### 潜在风险与适配点

- 家庭成员之间存在隐私边界；个人记忆不能默认进入家庭 Prompt。
- 自动复盘可能固化模型误判，必须保存 evidence、confidence、来源 Run 和确认状态。
- Prompt 快照含敏感家庭信息，日志和前端不得回显全文，只展示引用和摘要。
- 外部 Memory Provider 默认关闭；启用前必须展示数据出本地范围。
- 长期保留和删除必须遵循成员退出、家庭解散与个人数据删除规则。

## 模块四：自动技能沉淀与治理（P1）

### Hermes 实现机制

Hermes 把 Skill 定位为程序性记忆。SKILL.md 使用 YAML frontmatter（name、description、version、platform、tags、related skills、required toolsets/tools、环境变量、配置、blueprint）和正文（When to Use、Quick Reference、Pitfalls、Verification）；正文、references、scripts 按渐进披露加载。

`background_review` 每轮可复盘对话，优先更新已加载的 curator-managed Skill，其次更新现有 umbrella、增加 reference/script，最后才创建 class-level 新 Skill。明确排除一次性任务、未解决失败、环境暂态错误和宽泛负面结论。写入路径还有：read-before-write、来源/所有权保护、pinned 保护、可选安全扫描、write approval staging。`.usage.json` 记录 viewed/used/patched 和来源，Curator 按 stale/archive 天数管理并可合并重叠 Skill。

这比“每 15 次固定回顾”更成熟；建议 NexusMind 保留可配置双触发：每次成功 Run 产生候选，累计使用次数或时间窗口触发治理回顾。

### NexusMind 融合方案

- 只从成功或经用户纠正后成功的 Run 生成 Skill Candidate；失败路径不可直接成为可靠技能。
- 复用现有 `skills`/`ai_skills`，新增不可执行的 `skill_candidates` 和版本评测数据；候选经验证后生成新 SkillVersion，不直接覆盖已发布版本。
- 可复用判定至少满足：跨 session 重复出现、输入可参数化、输出可验证、依赖和权限明确、风险级别可静态确定。
- 去重用 `category + normalized intent + required tools + output type` 粗筛，再比较候选摘要/embedding；优先 patch umbrella Skill。
- 推荐阈值：成功使用即时记录；每 15 个已完成 Run 或每 7 天触发一次治理 review（这是 NexusMind 配置，不宣称源自 Hermes 固定实现）。

```csharp
public interface ISkillCurationServices
{
    Task RecordUsageAsync(long skillId, long runId, SkillUsageOutcome outcome,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SkillCandidateView>> ReviewAsync(long homeId,
        SkillReviewWindow window, CancellationToken cancellationToken = default);
    Task<ServiceResult> ValidateAndPublishAsync(long candidateId, long actorUserId,
        CancellationToken cancellationToken = default);
}
```

候选验证至少包括 schema、权限、危险内容扫描、静态依赖、3 个回放样例、幂等/副作用检查和人工审批；后台 reviewer 无权修改平台级 Skill。

## 模块五：MCP 工具集成生命周期（P0）

### Hermes 实现机制

Hermes MCP Client 支持 stdio、Streamable HTTP 和 SSE；连接后 initialize、tools/list 并把工具注册到统一 Registry，tools/call 时路由回具体 Server。支持 `tools/list_changed` 动态刷新、按 server 配置允许/拒绝工具、每 Server 并行调用声明、连接和调用超时、指数退避 + 抖动、停泊后周期复活、HTTP keepalive、stdio 生命周期回收、stderr 独立日志、敏感错误脱敏和环境变量白名单。

Schema Cache 以 server 名 + 连接配置 fingerprint 存储 name/description/inputSchema，使 Studio/Agent 启动时不必立刻拉起所有 stdio 进程；首次真实连接或 list_changed 后写穿更新。连接失效且耗尽恢复预算时应撤销对应工具，避免 Registry 中留下不可调用工具。

### NexusMind 融合方案

在现有 `StdioMcpProcessClient` 之上增加 `McpClientManager`，职责仅限 transport、session、manifest、健康和调用；Connector Adapter 负责把外部 Tool 映射为产品级 Tool。HA MCP 配置存 N97，不由腾讯云直接拉起本地进程。

```csharp
public interface IMcpClientManager
{
    Task<McpServerHandle> EnsureConnectedAsync(string serverName,
        CancellationToken cancellationToken = default);
    Task<McpToolManifest> RefreshManifestAsync(string serverName,
        CancellationToken cancellationToken = default);
    Task<JsonDocument> CallAsync(string serverName, string toolName,
        JsonElement arguments, CancellationToken cancellationToken = default);
}
```

Manifest 必须校验 schema size、工具数量、名称冲突、危险描述、output 上限；动态新增 Tool 默认不可用，需映射到已发布 `Connector Tool` 后才能进入 Agent Surface。对 HA MCP 可缓存底层 manifest，但 Agent 看到的产品 Tool 集保持冻结到 Run snapshot。

## 模块六：Crew 与 Workflow DAG（P2）

### Hermes 实现机制

Studio CrewMember 包含 role（coordinator/executor/reviewer/specialist）、persona、displayName、model、profileName、sessionKey、status。dispatch 向全部或指定成员发任务，每个成员独立 session/model，fire-and-forget 启动，SSE 返回状态。

Workflow 数据模型极简：Task(id/label/prompt/assigneeId/x/y) + Edge(from/to)。客户端添加边前 DFS 防环，用 Kahn 拓扑排序形成 parallel layers；同层并行，层间串行。工作流 JSON 文件持久化，运行状态主要由前端和 SSE 驱动。

### NexusMind 融合方案

NexusMind 已有 Expert Group 和 sequential/parallel/synthesis，后端仍应是编排事实源。只借鉴 Studio 的 DAG 编辑交互与 `topological layers`，不能把运行编排放到 Flutter。

建议把现有团队版本扩展为 `expert_workflow_nodes`、`expert_workflow_edges`；发布版本时校验无环、成员权限交集、每节点模型预算、最大并行数、失败策略、输入输出 schema。每次 Run 冻结 workflow version；节点状态写 Run Step/Event，Flutter 只渲染。

## 第三部分：优先级融合建议

| 优先级 | 功能 | 什么时候做 | 实现方式 | 前置条件 |
| --- | --- | --- | --- | --- |
| P0 | HA MCP Adapter | V2.5 第一切片 | `HomeAssistantMcpAdapter` 实现现有三接口；四个产品级 Tool；MCP/REST 主备 | N97 HA MCP 部署、Token Secret、entity 映射 |
| P0 | HA state_changed Gateway | 与 HA Adapter 同期 | 独立 subscriber → filter/cooldown → DeviceSyncService | HA WebSocket 或 MCP subscription 契约、事件压测 |
| P0 | MCP Client Manager | HA Adapter 前置 | stdio 优先；initialize/list/call、manifest cache、重连和工具撤销 | 扩展 `IMcpProcessClient`、配置/Secret 规范 |
| P0 | 审批卡片增强 | HA 写控制前 | 复用 ConfirmationItem/Run Action；Flutter 展示目标/影响/diff；异步恢复 | Action 与 Confirmation 关联、推送/轮询 |
| P0 | L1 Approval Grant | 审批卡后续切片 | run-scoped/preference-scoped 结构规则；L2/L3 禁止自动确认 | 风险计算统一入口、撤销与过期 |
| P1 | Context Snapshot | HA 基线稳定后 | Run 创建时冻结知识、偏好、设备、Skill/Expert 版本 | 明确个人/家庭 visibility、字符预算 |
| P1 | Memory Review Worker | Snapshot 后 | 完成 Run/压缩前/会话终止产生 Candidate | 候选表、确认策略、本地模型 |
| P1 | 本地会话检索 | N97 数据面完成后 | 加密 SQLite FTS 派生索引 + MySQL 引用事实 | 中文 tokenizer、同步/删除协议 |
| P1 | Skill Candidate/Curator | 记忆候选链路稳定后 | 成功 Run 产生候选，15 Run/7 天治理 review，版本化发布 | Usage 记录、回放 Eval、安全扫描 |
| P2 | Workflow DAG UI | 多专家使用量验证后 | Flutter/Web 编辑，后端 topo 校验与执行 | Expert Group 版本模型、Run Step |
| P2 | 外部 Memory Provider | 明确用户需求后 | `IMemoryProvider` 单选插件，本地默认 | 数据出境同意、删除/导出、可用性降级 |

## 建议的 V2.5 最小开发切片

1. **H1：MCP 会话基线**：transport-neutral session、initialize/list/call、manifest hash、超时/重连/脱敏错误；Mock MCP 测试。
2. **H2：HA MCP 只读接入**：发现、标准化实体/空间/能力、状态读取；禁止原始 entity_id 进入 API/Agent。
3. **H3：HA 实时同步**：WebSocket state_changed、过滤/冷却、DeviceState 更新、断线重订阅。
4. **H4：受控设备写入**：control/run_scene 生成 Run Action；L1/L2/L3 计算、确认、幂等、结果回读。
5. **H5：审批体验增强**：Flutter 卡片、once/本 Run L1 grant、拒绝原因、过期、断线恢复。
6. **M1：Context Snapshot**：冻结家庭知识/偏好/设备摘要与来源引用。
7. **M2：Memory Candidate**：Run 后后台复盘、候选确认和写入 `family_knowledge`/个人偏好。

## 验收重点

- N97 上 HA Token 不进入 MySQL、日志、MCP Tool 参数或模型上下文。
- Agent 只能看到稳定的 NexusMind Tool，MCP Server 增删工具不自动扩权。
- HA 断连能重连并恢复订阅；写请求超时不会盲重试或产生双重副作用。
- L2/L3 不受 session/always grant 影响；任何确认都能定位操作者、动作参数摘要和执行结果。
- Run 使用冻结 Context Snapshot；会话中记忆更新不悄然改变已开始 Run 的行为。
- 自动记忆/技能只形成 Candidate；敏感事实、冲突事实和平台 Skill 不被后台模型直接改写。

## 源码证据索引

- Hermes HA Tool：`tools/homeassistant_tool.py`
- Hermes HA WebSocket Adapter：`plugins/platforms/homeassistant/adapter.py`
- Hermes MCP Client / Schema Cache：`tools/mcp_tool.py`、`tools/mcp_schema_cache.py`
- Hermes Memory：`tools/memory_tool.py`、`agent/memory_manager.py`、`agent/memory_provider.py`
- Hermes 后台复盘：`agent/background_review.py`
- Hermes Skill 管理/治理：`tools/skill_manager_tool.py`、`tools/skill_usage.py`、`tools/skill_linter.py`
- Hermes 审批：`tools/approval.py`、`tools/write_approval.py`
- Studio Approval：`src/screens/chat/components/approval-card.tsx`、`src/lib/approvals-store.ts`
- Studio Event Store：`src/server/event-store.ts`
- Studio Crew / Workflow：`src/lib/crews-api.ts`、`src/types/workflow.ts`、`src/screens/crews/components/workflow-builder.tsx`

