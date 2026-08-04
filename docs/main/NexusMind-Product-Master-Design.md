# NexusMind Personal AI Operating System 产品总设计

> **文档角色：** 产品与技术的唯一总纲（Source of Truth）  
> **维护规则：** 需求、范围、领域模型或跨端契约发生变化时，先更新本文件；确认后，同一变更必须同步更新本文档指定的前端、后端实施文档。  
> **当前阶段：** V1 MVP 设计基线  
> **最后更新：** 2026-08-04

## 1. 产品定位

**NexusMind 是一个 Personal AI Operating System（个人 AI 操作系统）。** 它理解用户与家庭的目标，连接任务、日程、信息、服务和设备，通过 AI Agent 在用户授权下规划并协调数字世界与现实生活。

智能家居是 NexusMind 的首个高价值 Connector 领域，而非产品边界。传统智能家居解决“在 App 中控制设备”；NexusMind 解决“用户表达生活目标后，由 AI 结合日程、信息、环境和服务，决定何时、为何以及如何编排行动”。它不与米家、鸿蒙智联或 Aqara 争夺设备控制台，而是在其之上提供理解、规划、确认、跨服务协同与可追溯执行。

```text
用户目标
  ↓
AI Agent / Expert（理解、分析、规划）
  ↓
Skill（可复用能力）
  ↓
Connector（外部系统适配）
  ↓
Smart Home、日历、邮件、健康、财务、天气、出行和办公等外部生态
  ↓
现实动作与可追溯结果
```

### 设计原则

1. **目标优先：** 用户表达“想要什么”，而不是被迫学习设备操作。
2. **上下文优先：** 面向用户、家庭成员、时间、任务、日程、空间与习惯呈现能力；空间是重要上下文，不把“设备控制”作为核心心智。
3. **先建议、后执行：** 涉及自动化、设备写入和高影响操作必须可解释、可确认、可撤销。
4. **能力解耦：** Agent Runtime 负责意图、规划、记忆与主动建议；Expert 决定垂直策略，Skill 表示能力，Connector 隔离供应商协议，Device 只是 Smart Home Connector 下的一类真实资源。
5. **隐私与权限默认收敛：** 只读取完成目标所需的数据；所有外部读取、写入和高影响操作均需要明确权限、审计和授权范围。

## 2. V1 目标与范围

### V1 成功标准

证明 AI 能够在用户确认后可靠地完成一个个人或家庭目标，而不只是展示任务、日程或设备列表。

### V1 范围

| 范畴 | V1 交付 |
| --- | --- |
| AI Agent Runtime | 意图理解、计划草案、受控上下文读取、Run 记录与主动建议的 V1 基线 |
| AI Expert | 周计划、目标拆解、复盘与“家庭管家”专家，均通过同一 Skill/Connector 路径执行 |
| Connector | 通用 Connector Framework、Tool、权限与 Mock Connector；第二阶段接入 Home Assistant，并保留其他生态扩展位 |
| Smart Home | 作为首个 Connector 领域，提供回家、离家、睡眠三类 Mock 场景 |
| 用户体验 | Dashboard、专家中心、计划确认、家庭空间、我的；信息架构为个人与家庭共用 |
| 安全机制 | 权限校验、操作确认、执行记录、失败反馈 |

### 暂不纳入 V1

- 米家、涂鸦等厂商 Connector 的正式接入
- 自动执行高影响动作（无需确认的复杂自动化）
- 医疗诊断、跌倒识别等健康结论
- 装修户型图 AI 方案生成
- 微信、企业微信、飞书、钉钉等协作平台 Connector

## 3. 信息架构与核心页面

NexusMind App 采用不超过五项的底部导航；设备和协议细节始终隐藏在家庭空间、场景与 AI 行动之后：

| Tab | 用户价值 | V1 页面重点 |
| --- | --- | --- |
| 首页 Home | 今天的家庭与个人状态入口 | 问候、天气、AI 建议、家庭状态、计划、快捷场景 |
| AI Expert | 选择和运行可解释的专家 | 专家中心、家庭管家详情、运行记录、方案确认 |
| 计划 Plan | 查看待执行与已执行的行动 | AI 生成方案、确认状态、执行结果、失败重试 |
| 家庭 Home+ | 以空间组织家庭状态与场景 | 房间卡片、关键设备状态、场景入口 |
| 我的 Me | 管理账户、家庭成员与连接 | 个人设置、成员、Connector、权限 |

### 设计定位与体验原则

NexusMind 不是小米米家式设备列表、Home Assistant 式控制面板，也不是 ChatGPT 式聊天窗口。核心体验是：**AI 主动理解 → 给出建议与影响说明 → 用户确认 → 自动执行 → 可追溯结果**。

视觉体验以 Calm、Intelligent、Human、Trust、Premium 为关键词：安静不打扰、能感知 AI 的存在、面向家庭成员、每一步透明可控，并保持高品质的家庭空间感。页面优先级遵循“上下文 → 状态 → 一个主行动”，避免同时竞争多个主操作。

### App 设计系统（Flutter 实现规范）

移动端以 Flutter Material 3 实现，默认采用 Nexus Dark Glass，并支持跟随系统/手动选择浅色模式。设计 token 的唯一代码来源是 `D:\HomeMind\mobile\lib\core\ui\nexus_theme.dart`：

| 语义 | 深色模式 | 浅色模式 | 用途 |
| --- | --- | --- | --- |
| 页面背景 | `#111216` | `#F6F7F9` | 页面底色 |
| 内容卡片 | `#1C1D22` | `#FFFFFF` | 独立信息面 |
| AI 主色 | `#6366F1` | 由主题统一提供 | AI 建议、分析、主行动 |
| 家庭正常 | `#0B8F55` | 由主题统一提供 | 健康状态、完成状态 |
| 警告 | 由 ColorScheme 统一提供 | 由主题统一提供 | 需关注但非故障状态 |

- 排版层级：页面标题 28、模块标题 18~20、正文/控件 15~16、辅助信息 13~14、Label 12；中文优先系统字体（PingFang SC 回退）；
- 采用 4/8/12/16/24/32/48 的间距体系；默认内容横向边距为 20px，主要区块间距为 24px；
- 独立信息卡统一使用 20px 圆角；按钮、Chip、输入框使用 12px 圆角；
- `NexusSurface` 为默认信息卡，`FilledButton` 为唯一明确主行动，`OutlinedButton` 用于次操作；
- 组件只能消费 `Theme.of(context)`、`NexusTheme`、`NexusLayout` 和 `NexusSurface`，不能在页面内复制颜色/间距 token；
- 动画只服务状态感知：AI 卡片可使用约 2 秒的轻微呼吸效果，状态切换约 300ms，页面切换约 250ms；不添加装饰性动效。

### 首页 Dashboard

首页展示“今天值得关注什么”，而非设备清单。实现入口为 `D:\HomeMind\mobile\lib\pages\dashboard_page.dart`，页面结构为 `SafeArea → Header → AI 主行动 → Family Status → Today Plan → Smart Scene → AI Suggestion`：

- 问候语、日期、天气与通知入口；
- 一条可行动的 AI 今日建议（检测依据、建议、执行/忽略）；
- 家庭安全、环境、照明等关键状态摘要；
- 用户日程与待办；
- 回家、离家、观影、睡眠等横向滑动快捷场景。

AI 建议卡（`NexusAICard`）是首页核心，不使用聊天气泡；它应有 AI 语义色、克制的状态动效，并可进入对应 Expert。快捷场景卡建议维持约 120 × 100 的紧凑触控尺寸。

### AI Expert

AI Expert 是“专家中心”，不是通用聊天窗口。每个专家需要明确展示：

- 能力与可读取/执行的权限；
- 需要的输入（家庭成员、作息、设备等）；
- 输出类型（建议、计划、自动化草案）；
- 预计消耗与执行影响；
- 可查看的运行与行动记录。

运行页可以借鉴 Agent 的任务时间线，但绝不展示模型思考过程；只展示可理解的阶段，例如“分析家庭环境”“获取设备状态”“生成优化方案”“等待确认”。运行状态统一为 `queued`、`running`、`completed`、`failed`，行动状态另行表达等待确认、执行中、成功和失败。

### 家庭 Home+

家庭按空间呈现，例如客厅、卧室、老人房、厨房。每个空间只展示对当前生活目标重要的状态，如温湿度、主灯、空调、睡眠模式和安全状态。

空间详情的第二层才显示设备与自动化：例如客厅的主灯、空调和观影模式；不得让实体 ID、协议名称或厂商字段成为默认界面内容。

### Plan 与 Connector

Plan 合并 Todo 和 Calendar，以“任务 / 日历”分段呈现当天进度与 AI 生成行动。Connector 位于“我的 → 连接”或家庭设置中，呈现 Home Assistant、天气、日历等连接的在线/授权中/异常状态，永不展示凭据明文。

## 4. AI Agent 与 Connector 工作模型

### 专家运行链路

```text
用户请求 / 页面入口
  ↓
创建 Expert Run
  ↓
Planner 生成目标、上下文需求与行动草案
  ↓
Skill Executor 调用只读 Skill 收集数据
  ↓
生成建议与 Run Actions
  ↓
用户确认（写操作必经）
  ↓
Skill Executor 调用 Connector Adapter
  ↓
设备 / 外部服务执行
  ↓
记录结果、审计与 Dashboard 状态刷新
```

### 代表性场景

| 场景 | 用户表达 | AI 行为 | 需确认的动作 |
| --- | --- | --- | --- |
| 睡眠优化 | “我爸最近睡眠不好，优化一下环境” | 分析睡眠数据、天气、日程压力、卧室温湿度与习惯，生成方案 | 创建夜间温度、灯光和提醒规则 |
| 自然语言控制 | “我有点困了” | 根据时间与所在空间匹配睡眠 Skill | 关闭电视、调暗灯光、调整空调 |
| 家庭安心 | “看看爸爸今天怎么样” | 汇总活动与环境状态，仅提示异常 | 发送提醒或创建关注计划 |
| 节能建议 | “帮我省点电” | 分析能耗和设备使用习惯 | 应用空调时段/温度优化方案 |
| 周计划 | “帮我安排下周” | 读取待办、日历与目标，生成可执行计划 | 创建或调整日历事件、任务与提醒 |

## 5. NexusMind AI OS 分层架构

```text
用户
  ↓
NexusMind App（Flutter / Web / Voice Device）
  ↓
NexusMind AI OS
  ↓
AI Agent Runtime（意图理解、任务规划、长期记忆、主动建议、多 Agent 协作）
  ↓
Expert Layer（睡眠、家庭能源、老人陪护、儿童学习、旅行、财务、日程等）
  ↓
Skill Engine（创建任务、控制设备、发送消息、生成报告、分析数据、执行流程）
  ↓
Connector Layer（连接、鉴权、Tool、权限、审计、协议适配）
  ↓
外部生态
  ├─ Smart Home: Home Assistant / Matter / MQTT / Zigbee / 厂商 Native API
  ├─ Personal: Calendar / Email / Health / Finance / Weather / Vehicle
  └─ Productivity: Todo / Notion / GitHub / Office
```

本图描述目标架构，不表示 V1 同时交付所有 Expert 或 Connector。现有技术基线保持不变：移动端为 Flutter Material 3（Dart、Provider、GoRouter、Dio，代码位于 `D:\HomeMind\mobile\lib`），服务端为 .NET 8 API + Repository 分层，数据库为 MySQL。后续增加 AI Runtime、Planner、Memory、Skill Executor 和 Connector Adapter；Smart Home 逻辑只能位于 SmartHome Connector，不能成为 Todo、Calendar 或 Agent Runtime 的内置依赖。

## 6. 核心领域与数据边界

NexusMind 的核心不是“AI 调用设备”，而是一个可演进、可授权、可确认、可追溯的行动系统：

```text
User ── belongs to ── Workspace（当前租户/家庭）
                           │
              ┌────────────┴────────────┐
              ↓                         ↓
       Expert + ExpertVersion      Connector Provider
              ↓                         ↓
        Skill + Permission       Workspace Connector
              └────────────┬────────────┘
                           ↓
                      Expert Run
              ┌────────────┼────────────┐
              ↓            ↓            ↓
          Run Event      Result      Run Action
                                           ↓
                              Scene / Smart Home Device
```

本文中的 **Workspace** 是产品概念；V1 持久化时映射到现有的 `tenant`（家庭）。`user_id` 表示发起者与个人授权范围，`tenant_id` 始终表示数据隔离与家庭归属。

### 6.1 AI Domain

| 模型 | 职责 | 关键字段 / 约束 |
| --- | --- | --- |
| Expert | 面向用户的 AI 角色，如家庭管家、周计划专家、睡眠专家 | `code` 唯一、`name`、`category`、`description`、`avatar`、`builtin`、`status` |
| Expert Version | 固化 Expert 的可复现策略 | `expert_id`、`version`、`system_prompt`、`skill_policy`、`output_schema`；历史 Run 必须关联此版本 |
| Skill | 可控、可授权、可观测的原子能力，而非供应商工具 | `code`、`category`、输入/输出 schema、`risk_level` |
| Expert Skill Permission | 限制某 Expert 版本可调用的 Skill | `expert_version_id`、`skill_id`、`max_calls`、`require_confirm`；默认拒绝未声明能力 |
| Expert Run | 一次专家分析或执行会话 | `user_id`、`tenant_id`、`expert_id`、`expert_version_id`、输入上下文、Token 用量、状态、开始/完成时间 |
| Run Step | 运行中的内部可编排步骤 | 仅保存可审计的计划/执行元数据，不作为模型思考链输出 |
| Run Event | 面向 Flutter 时间线的可理解事件 | `run_id`、`sequence`、`type`、`step_id`、`status`、`display_message`、`created_at`；同一 Run 内 sequence 唯一且不可改写 |
| Run Action | AI 建议的可确认变更 | `run_id`、`action_type`、`payload`、`status`、`confirmed_at`、`idempotency_key`、`result` |

Expert Run 的持久化状态使用细粒度状态：`draft`、`queued`、`planning`、`running`、`synthesizing`、`needs_input`、`completed`、`failed`、`cancelled`。Flutter 只映射成适合界面的 `queued`、`running`、`completed`、`failed`，并通过 Run Event 展示可理解阶段。

Run Action 的标准状态为 `pending`（等待确认）、`confirmed`、`rejected`、`executing`、`executed`、`failed`、`cancelled`。任何设备或自动化写操作先创建 Action，不能由 Planner 直接下发。`payload` 保存标准化目标与参数，审计和 API 输出须脱敏。

**禁止**把 Prompt、模型思考过程或原始推理链写入 `run_events` 或返回给客户端；只允许“正在检测客厅环境”“正在生成优化方案”等用户可理解事件。

### 6.2 Connector 与 Smart Home Domain

Connector Domain 服务全部外部生态：`Connector Provider` 与 `Workspace Connector` 为通用模型，Calendar、Email、Health、Finance 和 Smart Home 都遵守同一 Tool、Permission、Run Action 与审计边界。Smart Home Domain 仅扩展设备、空间、场景和自动化等物理世界模型；它是首个 Connector 实现，不是领域模型的默认中心。

| 模型 | 职责 | 关键字段 / 约束 |
| --- | --- | --- |
| Connector Provider | 全局 Connector 类型目录，如 Home Assistant、Google Calendar、Notion | `code` 唯一、`name`、`type`、`provider`、`description`、`status` |
| Workspace Connector | 某家庭实际授权的 Connector 实例，如“余姚家 HA”或“我的 Google 日历” | `tenant_id`、`connector_provider_id`、`name`、`status`、`auth_status`、`config`、`credential_ref`、`created_at`、`last_sync_at`、`last_health_at` |
| Connector Tool | Provider 声明的稳定工具契约，如 `turn_on_light`、`set_temperature`、`create_calendar_event` | `connector_provider_id`、`name`、`description`、`input_schema`、`output_schema`、`permission`、`risk_level`、`require_confirm`；同一 Provider 内 `name` 唯一 |
| Workspace Connector Tool | 某已授权实例实际可用的工具及其发现/禁用状态 | `workspace_connector_id`、`connector_tool_id`、`status`、`availability_reason`、`last_checked_at`；不复制密钥或厂商原始字段 |
| Connector Permission Grant | 成员对某连接工具或权限范围的显式许可 | `user_id`、`workspace_connector_id`、`connector_tool_id` 可空、`permission`、`effect`、`confirmation_policy`、`granted_at`、`revoked_at` |
| Smart Home Space | 家庭空间 | `tenant_id`、`name`、`space_type`、`sort_order` |
| Smart Home Device | 规范化后的真实设备或传感器 | `tenant_id`、`workspace_connector_id`、`external_id`、`space_id`、`name`、`device_type`、`online_status`；连接内 `external_id` 唯一 |
| Device Capability | 设备可读/写的标准能力 | `device_id`、`capability`、`value_schema`、`permission`；例如 `power:boolean`、`brightness:number` |
| Device State | 设备最近一次标准化状态快照 | `device_id`、`state_json`、`updated_at`；必须标记采样时间，不将过期数据描述为实时 |
| Scene / Scene Action | 用户可理解的多个设备动作组合 | Scene 归属家庭；Action 记录 `scene_id`、目标设备、Capability 与目标值 |
| Automation Rule | 经授权的长期自动化 | `trigger`、`conditions`、`actions`、`approval_policy`、`enabled`；复用 Skill、Connector、Run、Action 模型 |

Connector Provider 与 Workspace Connector 必须分离：前者描述“可接入的产品”，后者描述“这个家庭已授权的实例”。`status` 描述连接运行健康，`auth_status` 描述授权生命周期（如 `not_connected`、`pending`、`authorized`、`expired`、`revoked`、`failed`），两者不可混用。`config` 是通过 Provider Schema 校验的非敏感配置（如服务地址、区域、同步选项）；访问令牌、客户端密钥和刷新令牌仅由 `credential_ref` 指向 Secret Vault，数据库和 API 不得保存或回传明文。

`Connector Tool` 是 AI 可见的最小外部能力边界，而不是厂商 API 的镜像。工具用 JSON Schema 定义输入和输出，例如 `turn_on_light({ room })` 与 `set_temperature({ temperature })`；Smart Home 工具仍需在执行时解析为已授权的空间、设备能力和 `Run Action`。工具定义应保持与 MCP Tool 的 `name`、`description`、`inputSchema` 语义兼容，使后续可由 NexusMind 作为 MCP Client 调用外部 Server，或把 Connector 暴露为 MCP Server，而不改变 Expert 或 Skill 的业务语义。

### 6.3 Permission Domain

权限控制分为三层：

1. **成员连接与工具授权：** 成员只能使用其在家庭中被授予范围的 Connector、Tool 和 Permission；授权支持显式 `allow` / `deny` 与过期/撤销，默认拒绝。
2. **确认策略：** Permission Grant 或 Tool 定义可以指定 `never`、`always`、`high_risk_only`。发送消息、控制灯等低风险写入至少需要授权；门锁、安防和创建长期自动化必须 `always` 确认，不能由成员或 Expert 覆盖为自动执行。
3. **Expert Skill 授权：** Expert Version 只能调用已声明的 Skill，并受调用次数、风险和确认策略限制；Skill 只能委派给已启用且已授权的 Connector Tool。
4. **Run 权限快照：** 创建 Run 时解析成员、家庭、Connector、Tool、设备能力和 Expert 权限，形成不可变快照。确认与执行前再次校验实时权限、Tool 可用性与资源版本。

高风险能力（门锁、安防、自动化创建等）必须 `require_confirm=true`；低风险只读 Skill 不需要用户逐项确认，但仍需要审计。所有写操作使用 `idempotency_key`，重复确认只能返回既有结果。

### 6.4 V1 数据范围与演进

V1 实现 `Expert`、`ExpertVersion`、`Skill`、`ExpertSkillPermission`、`ExpertRun`、`RunEvent`、`RunAction`、`Connector Provider`、`Workspace Connector`、`Connector Tool`、`Workspace Connector Tool`、`Connector Permission Grant`、`Device`、`DeviceCapability`、`DeviceState` 与 `Scene`。首批可使用 Mock Connector 验证工具发现、权限、Skill 调用和 Run 记录；Home Assistant 是第二阶段的正式设备接入。`Automation Rule`、Expert DAG/协作组、MCP Server 托管和 Skill Marketplace 进入后续阶段。

数据库命名以现有 SQL 迁移和实体为事实来源；建议采用 `experts`、`expert_versions`、`skills`、`expert_skill_permissions`、`expert_runs`、`run_events`、`run_actions`、`connector_providers`、`workspace_connectors`、`connector_tools`、`workspace_connector_tools`、`connector_permission_grants`、`smart_home_*`、`device_capabilities`、`device_states`、`scenes`、`scene_actions`、`automation_rules`。不得仅为对齐文档修改已上线迁移；实际落地前先核对现有 `AiEntities.cs` 和迁移的重用空间。

所有可变表遵循现有数据库约定：软删除、更新时间、同步版本；目录和运行记录使用行版本控制。跨家庭数据严格以 JWT 的 `tenant_id` 隔离，禁止客户端传值覆盖。

## 7. Connector 与 Skill 规范

### Connector

Connector 是 NexusMind AI OS 与外部世界之间的受控执行入口：AI Agent / Expert 负责理解用户目标，Skill 负责选择受控能力，Connector 负责鉴权、调用、审计并屏蔽外部协议。AI、Flutter 页面和业务服务均不得直接调用厂商 API、HA 实体、MQTT Topic 或第三方 SaaS API。

```text
AI Agent / Expert
          ↓
        Skill
          ↓
Connector Service（Tool、Permission、确认、审计）
          ↓
      IConnectorAdapter
  ┌───────┼───────────┬───────────────┐
  ↓       ↓           ↓               ↓
SmartHome Calendar    Productivity     Personal Data
  ↓       ↓           ↓               ↓
HA/Matter/  Google     Notion/GitHub    Health/Finance/Weather
MQTT/API    Calendar   Office           Vehicle
```

Connector Provider 描述“可接入的产品”；Workspace Connector 描述“当前用户/家庭已授权的连接实例”，例如“余姚家”“我的 Google 日历”或“团队 Notion”。它统一记录名称、类型、连接健康、授权范围、能力发现结果与同步时间。当前 V1 先以 Mock Connector 验证完整受控调用链；第二阶段正式支持 Home Assistant。MQTT 与 Zigbee2MQTT 为 SmartHome Connector 的本地优先兼容层；米家、涂鸦和 Matter 只保留 Adapter 契约与数据映射，不承诺 V1 正式接入。

### SmartHome Connector 与 Home Assistant

Home Assistant（HA）是设备抽象层与家庭自动化中间层：它发现、统一和执行设备协议，但不理解用户意图、生活目标或跨服务上下文。NexusMind 必须位于 HA 之上，承担 AI 家庭大脑的职责；HA 仅是 SmartHome Connector 的首选设备执行后端，而不是产品业务中心、用户入口或更高层平台。

```text
用户
  ↓
NexusMind AI Agent（理解疲劳、日程、睡眠、天气与家庭上下文）
  ↓
Sleep Expert → SleepMode Skill → SmartHome Connector
  ↓
Home Assistant（统一设备模型、自动化执行、状态同步）
  ↓
米家 / 华为鸿蒙智联 / Aqara / Zigbee / Matter / MQTT / Wi-Fi 设备
```

V1 与第二阶段优先采用 `NexusMind → SmartHome Connector → Home Assistant → 设备生态`。这利用 HA 对跨厂商设备的统一能力，避免 NexusMind 重复实现 Zigbee、Matter、MQTT 或厂商云协议。只有 HA 无法覆盖且存在明确用户价值时，才为米家、华为或其他生态增加直连 Adapter；该 Adapter 仍必须位于 SmartHome Connector 之后，不能改变 Agent、Expert、Skill 或权限模型。

### Adapter 与设备能力规范

每个厂商或协议均实现同一 Adapter 契约：连接测试、设备发现、读取标准化状态、执行标准化命令、订阅/接收状态变更。Adapter 负责把厂商字段转换为 NexusMind 的 Device Capability，业务层不感知 `prop.power`、`switch_led`、HA Entity ID 或 Zigbee Topic。

```text
米家 prop.power       → capability.power
涂鸦 switch_led       → capability.power
HA light.turn_on      → command(power = true)
Zigbee2MQTT JSON      → DeviceState + Capability
```

设备类型可以用于展示和推荐（灯光、空调、窗帘、传感器、音箱、摄像头），但不能成为硬编码控制逻辑。真实控制基于动态能力，如 `power`、`brightness`、`color_temperature`、`temperature`、`humidity`、`motion`、`open_close` 及其 `value_schema`、权限和当前状态。

### 本地优先与协议边界

- **Home Assistant：** 首选设备抽象与执行后端，不是暴露给用户的控制台；App 只调用 NexusMind API，服务端经 SmartHome Connector 连接 HA。HA 可以统一米家、华为、Aqara、Zigbee、Matter、MQTT 与 Wi-Fi 设备，但不承载用户意图、Expert 策略、Skill 编排或产品权限事实；
- **Zigbee：** 采用 `Zigbee → USB Dongle → Zigbee2MQTT → MQTT → SmartHome Service` 的本地路径，优先保障断网可用、低延迟、隐私和稳定性；
- **MQTT：** 使用标准化内部主题 `nexusmind/home/{homeId}/device/{deviceId}/state` 与 `.../command`；状态消息包含标准化 state，命令消息只接受经过权限校验和幂等处理的 action；
- **米家/华为/涂鸦：** 优先由 HA 统一接入。仅在 HA 无法覆盖且已验证用户价值时，才由受控 Cloud Adapter 访问其云 API，处理 OAuth/Token 刷新、限流、云端延迟和厂商字段转换；不让客户端持有厂商凭据；
- **Node-RED：** 可作为家庭主机的规则执行辅助，不能绕过 NexusMind 的权限、Action 审计和状态模型；长期自动化仍以 `automation_rules` 为系统事实来源。

### 部署与演进

V1 以现有 .NET 服务内的 `Connector` 模块落地，SmartHome 仅是其中的首个子模块，避免过早拆分服务；当连接器调用、实时消息或本地 Agent 规模增长时，再分别演进为独立服务。第二阶段的家庭侧可在 Docker 中运行 Home Assistant、Zigbee2MQTT、MQTT Broker、Node-RED（可选）和 NexusMind Agent；云端保持 ASP.NET Core、MySQL、AI Gateway，并按需要增加 Redis、后台同步 Worker 和 WebSocket 推送。

本地 Agent 只接收最小权限的命令并回报设备状态；云端不可用时，已被明确授权的本地规则可继续运行，但必须在恢复连接后补齐执行记录。高风险控制不得因离线重试而重复执行。商业硬件阶段的 `NexusMind Hub` 是家庭侧部署形态，可运行 NexusMind Agent、Home Assistant Core 与 SmartHome Gateway；它不取代云端 AI OS，也不改变 Connector 的统一契约。

首批 Connector 分类：

- Smart Home：Home Assistant、MQTT、Zigbee2MQTT，后续扩展 Matter、米家、华为、Aqara、涂鸦；
- Personal：Calendar、Todo、Email、Health、Finance、Weather、Vehicle；
- Productivity：Notion、GitHub、Office；
- Future：微信、企业微信、飞书、钉钉。

### Skill

Skill 是 AI 的“手脚”，定义稳定的输入、输出、权限和失败语义。示例：

| Skill | 输入 | 输出 | 所需权限 |
| --- | --- | --- | --- |
| 读取家庭环境 | space / metric | 环境数据与采样时间 | `environment.read` |
| 控制灯光 | device / action | 执行结果 | `light.write` |
| 控制空调 | device / mode / target | 执行结果 | `air.write` |
| 创建家庭提醒 | schedule / content | 提醒 ID | `notification.write` |

## 8. 文档治理与联动

本文件是“为什么做、做什么、跨端如何协作”的总纲。实施细节分别由下列文件维护：

| 文档 | 位置 | 负责内容 |
| --- | --- | --- |
| 产品总设计（本文） | `D:\HomeMind\core\docs\main\NexusMind-Product-Master-Design.md` | 产品范围、领域边界、版本路线、跨端契约 |
| 后端开发设计 | `D:\HomeMind\core\docs\main\NexusMind-Backend-Development.md` | API、模型、数据库、服务分层、Connector 与执行安全 |
| 前端开发设计 | `D:\HomeMind\mobile\docs\main\NexusMind-Frontend-Development.md` | 页面、组件、状态、接口消费、交互与验收 |
| 前端开发规范 | `D:\HomeMind\mobile\docs\DEVELOPMENT_GUIDELINES.md` | Flutter 分层、Provider、路由、异步、安全与质量门禁 |
| UI 样式规范 | `D:\HomeMind\mobile\docs\UI_STYLE_GUIDE.md` | 设计 token、排版、布局、组件状态与响应式规则 |
| 主题实现 | `D:\HomeMind\mobile\lib\core\ui\nexus_theme.dart` | 语义色、20px 内容容器、排版、按钮、输入框和深浅主题 |

### 同步流程（必须执行）

1. **先修改本文：** 记录需求背景、范围、领域模型、页面/API 影响和版本状态。
2. **确认影响面：** 在“变更记录”中标明需要同步的前端、后端文档、开发规范或 UI 规范。
3. **更新实施文档：** 同一次任务内同步对应文档的待办、契约、验收标准、数据模型或 UI 状态。
4. **代码实现时再同步：** 前端变更遵循 Flutter 开发/UI 规范；API 变化还必须遵循后端 `DEVELOPMENT.md` 中的接口文档规则。
5. **关闭变更：** 三份文档的状态都更新为“已同步”后，才视为设计变更完成。

### 变更记录

| 日期 | 主题 | 本文变更 | 前端同步 | 后端同步 | 状态 |
| --- | --- | --- | --- | --- | --- |
| 2026-08-04 | V1 总体设计基线 | 建立产品定位、V1 边界和协作机制 | 已建立实施文档 | 已建立实施文档 | 已同步 |
| 2026-08-04 | Flutter UI 设计基线 | 纳入五 Tab、Nexus Dark Glass、Dashboard、Expert Run、Home+ 与 Plan 规范 | 已同步 Flutter 页面/规范/实现路径 | 已同步 API 对应流程要求 | 已同步 |
| 2026-08-04 | Expert + SmartHome 数据模型 | 明确版本化 Expert、Skill 授权、Run 审计、Connector 实例、设备能力/状态与场景边界 | 已同步 Run 事件与领域模型要求 | 已同步表模型、权限、状态与接口约束 | 已同步 |
| 2026-08-04 | SmartHome Connector 技术架构 | 明确 HA 驱动层、Adapter 契约、本地优先 Zigbee/MQTT、厂商云适配及部署演进 | 已确认 App 只消费标准化家庭模型 | 已同步 Adapter、消息与服务边界 | 已同步 |
| 2026-08-04 | 通用 Connector Tool 与权限层 | 增加 Connector Tool、实例可用性、成员 Permission Grant、确认策略与 MCP 兼容契约；当前先完成 Framework + Mock，HA 为第二阶段首个真实接入 | 现有连接页继续展示授权、可用性与确认状态 | 已同步数据模型、服务边界与接口规划 | 已同步 |
| 2026-08-04 | Personal AI OS 定位升级 | 明确 AI Agent Runtime、Expert、Skill、Connector 为产品主架构；Smart Home 降为首个垂直 Connector，定义先 Framework、后 HA、再 NexusMind Hub 的路线 | 个人与家庭共用现有入口，后续按 Connector 扩展 | 已同步通用 Connector 服务边界 | 已同步 |
| 2026-08-04 | M5.4 SmartHome Connector Layer | 明确其作为 M5.2 并列模块，依赖 M5.1 + M5.3，产出 Expert 可调用的家庭设备能力 | 已同步 Home+/Run 界面依赖 | 已同步 Connector 实施顺序 | 已同步 |
| 2026-08-04 | 未来 12 个月路线图 | 定义北极星指标、四阶段战略、月度成果、C 端试点与 To B 验证边界 | 已同步前端阶段交付 | 已同步后端阶段能力演进 | 已同步 |

## 9. 路线图

### 战略目标与北极星指标

NexusMind 的战略路径不是“先卖硬件，再找场景”，而是 **AI 入口 → 用户关系 → 跨领域上下文 → Connector → 受控行动 → 主动智能**。产品从“AI 帮我管理个人和家庭生活”演进为“AI 在数字服务与物理世界之间主动协同”，最终成为可信的个人 AI 操作系统。家庭是首个差异化场景，Smart Home 是它的一个 Connector，而非整个产品。

北极星指标是：**每个活跃 Workspace 每周由 AI 完成的有效任务数。** 有效任务必须已被用户确认或按已授权规则成功执行，且能产生可感知价值，例如周计划、睡眠模式、异常提醒、家庭计划、节能或长辈关怀。设备安装数量、下载量和单纯的设备控制次数不是核心成功指标。

```text
第 0–3 月：AI Agent 基础 + Connector Framework
          ↓
第 4–6 月：Personal + Family AI Assistant，Home Assistant 为首个真实 Connector
          ↓
第 7–9 月：主动式个人与家庭 AI，预测 + 自动化
          ↓
第 10–12 月：Personal AI OS + NexusMind Hub，商业化复制
```

### Phase 0：当前至第 1 月｜产品基础稳定期

目标是从 Todo、Calendar、Expert Demo 升级为 AI Personal Assistant MVP。首页重构为问候、天气/日程/任务状态、AI 建议和计划/专家/家庭入口；完成 Agent Runtime 最小编排、专家目录、详情、Run 执行和结果确认。首批专家为周计划、目标拆解、复盘和家庭管家。

完成 Connector Framework、Tool、权限、授权状态与 Run 记录；用 Todo、Calendar 等既有领域和 SmartHome Mock（客厅灯、空调、窗帘、温湿度）验证 `Flutter → ASP.NET API → Agent / Expert → Skill Engine → Connector → Mock` 的完整调用链，不在此阶段接入真实硬件。

### Phase 1：第 2–3 月｜首个真实 Connector：Smart Home

目标是让用户首次感受到“AI 开始懂我的家”。通过 Home Assistant Connector 接入五类高价值设备：灯、空调、窗帘、门磁、温湿度。交付回家、离家、睡眠三个场景；例如“我要睡觉”经 Sleep Expert 和 SmartHome Skill 生成确认后的场景执行。

本阶段的技术落点为 `M5.4 SmartHome Connector Layer`：设备发现、标准能力模型、Connector 健康、权限、Run Action 确认、幂等执行和审计必须完整，不能以“直接控制设备”替代。

### Phase 2：第 4–6 月｜Personal + Family AI Assistant

这是首个可收费版本。App 形成首页、计划、专家、家庭、我的五栏；Dashboard 聚合个人任务/日程、家庭健康、天气、AI 建议和快捷场景，家庭中心展示“我的家”、空气/温度/安全与设备摘要。Calendar、Todo、Weather 等 Connector 与 Smart Home 使用同一权限、确认和审计链路。

商业验证以清晰的 C 端套餐和年服务为原则：基础场景、标准家庭服务、老人安心增值服务及 AI/云同步/远程访问/OTA 年费。具体价格、主机和安装成本必须先完成单位经济模型和试点验证，不在产品设计中承诺固定售价。

### Phase 3：第 7–9 月｜主动式个人与家庭 AI

从“用户说打开空调”升级为“结合室温、在家状态、时间和习惯，建议开启舒适模式”。新增 Family Context Engine，统一时间、天气、成员、设备、习惯和日程上下文；在明确授权下生成预测建议和自动化。

长辈关怀是差异化重点：以每日家庭状态报告、异常停留提醒和环境趋势呈现，不将其包装为医疗结论或“跌倒检测”。节能、观影等场景在用户信任与确认策略成熟后扩大自动化覆盖。

### Phase 4：第 10–12 月｜Personal AI OS、Hub 与商业复制

Expert 与 Connector 深度融合，推出健康、节能、家庭管家、旅行、财务等跨数字生活和物理家庭的专家。例如根据孩子上课、天气降温与采购需求，生成儿童房温度建议、待办和出门提醒，但所有高影响动作仍遵循权限与确认策略。此阶段验证 `NexusMind Hub`，在家庭侧运行 NexusMind Agent、Home Assistant Core 与 SmartHome Gateway，以 Hub 作为部署和体验入口，不把它变为业务中心。

第 10 月起验证 To B：`NexusMind Host` 面向民宿提供入住欢迎、节能、异常提醒和远程管理；`NexusMind Builder` 面向装修渠道提供智能方案、设计支持、安装标准。To B 在小规模付费试点通过后才扩展渠道。

### 十二个月里程碑

> 下表的“第 N 月”是产品节奏，不等同于 `PROJECT_PLAN.md` 中的工程里程碑编号（如 M5.4）。

| 时间 | 版本主题 | 核心成果 |
| --- | --- | --- |
| 第 1 月 | NexusMind AI Assistant | Agent/Expert 闭环、AI 优先 Dashboard、Connector Framework + Mock |
| 第 2 月 | Connector Foundation | Tool、权限、授权状态、日程/任务上下文与标准化 Mock 能力 |
| 第 3 月 | Connector MVP | Home Assistant 接入、五类设备、回家/离家/睡眠 |
| 第 4 月 | Personal + Family 1.0 | 五栏信息架构、首批家庭试点与套餐验证 |
| 第 5 月 | AI Assistant | Expert + Connector 联动、运行记录、确认与审计体验完善 |
| 第 6 月 | 老人安心版 | 家庭状态报告、异常停留提醒、环境监测 |
| 第 7 月 | 主动 AI | 基于跨领域上下文的建议与授权自动化 |
| 第 8 月 | 家庭状态模型 | Family Context Engine、习惯与设备状态融合 |
| 第 9 月 | 民宿方案 | Host 小规模 To B 验证 |
| 第 10 月 | Builder 方案 | 装修渠道智能方案与安装标准试点 |
| 第 11 月 | Personal AI OS | 多 Expert 协同、个人 AI、服务 Connector 与 SmartHome 融合 |
| 第 12 月 | NexusMind 2.0 + Hub | 可复制交付、Hub 试点、运营指标和商业化规模验证 |

### 工程里程碑对齐

`M5.4 SmartHome Connector Layer` 是通用 Connector Framework 的首个垂直实现，也是 M5.2 的并列模块。它依赖 `M5.1 Backend` 提供的身份、家庭租户、迁移和部署基线，以及 `M5.3 AI + Expert 执行` 提供的 Skill Engine、Expert Run、权限与确认 Action 语义；它不得反向定义 Agent Runtime、Expert 或其他 Connector 的边界。

它的唯一输出是：**AI Expert 可以通过受控 Skill 调用家庭设备能力。** 当前首批交付为 Mock Connector、工具目录、设备发现的模拟数据、标准能力模型、家庭空间/场景以及 Run Action 的确认/幂等执行/审计；第二阶段将以相同契约实现 Home Assistant Connector。米家、涂鸦和 Matter 仅保留 Adapter 扩展位。M5.4 不等同于“设备控制 API”，也不替代 M5.2 的同步、附件、iCal 和 Push。

## 10. 当前待决事项

- 确定 Phase 1 后进入 Connector Catalog 的优先级、OAuth/授权方式、数据最小化范围与用户价值；
- 第二阶段实施前明确 Home Assistant 的认证方式、实体发现和设备能力标准化范围；
- 定义家庭、空间、成员与现有租户模型的对应关系；
- 确定 AI Runtime 的模型供应商、成本预算、上下文隔离和降级策略；
- 确定 Home+ 与 Plan 首版页面的最小数据结构和交互原型；
- 为高风险动作定义确认级别、幂等键、超时和失败重试规则。
