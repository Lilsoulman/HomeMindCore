# NexusMind Personal AI Operating System 产品总设计

> **文档角色：** 产品与技术的唯一总纲（Source of Truth）  
> **维护角色：** 你现在的角色是 NexusMind 的产品设计专家，负责维护和更新产品总设计文档。
> **维护规则：** 需求、范围、领域模型或跨端契约发生变化时，先更新本文件；确认后，同一变更必须同步更新本文档指定的前端、后端实施文档。  
> **当前阶段：** V2.4 家庭与个人连接器、Web 治理设计基线（承接 V2.3）。
> **版本语义：** V2.4 = V2.3 + 家庭级/个人级 Connector 边界 + 用户/成员治理 + Web 用户端与开发端 V1；先完成契约和数据迁移，再进入实现。
> **最后更新：** 2026-08-06

## 1. 产品定位

**NexusMind 是一个 Personal AI Operating System（个人 AI 操作系统）。** 它理解用户与家庭的目标，连接任务、日程、信息、服务和设备，通过 AI Agent 在用户授权下规划并协调数字世界与现实生活。

智能家居是 NexusMind 的首个高价值 Connector 领域，而非产品边界。传统智能家居解决“在 App 中控制设备”；NexusMind 解决“用户表达生活目标后，由 AI 结合日程、信息、环境和服务，决定何时、为何以及如何编排行动”。它不与米家、鸿蒙智联或 Aqara 争夺设备控制台，而是在其之上提供理解、规划、确认、跨服务协同与可追溯执行。

**V2.2 的产品承诺是“AI 与你一起管理”，而不是“AI 替你完成”。** AI 可在明确授权范围内处理低风险事项、整理计划并汇报已完成的工作；中高风险事项必须说明依据、影响和可选方案，由家庭成员确认或决定。所有自动化都必须可追溯，符合条件时还应可撤销。

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
| Expert 能力 | 周计划、目标拆解、复盘、“家庭管家”与“个人生活专家”（探店翻牌、行程规划），均通过同一 Skill/Connector 路径执行 |
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
- OCR 第三方截图识别（个人生活专家 MVP 以手工录入与对话提取为主）
- 快速剪辑（生成剪映 .draft 草稿）已排期 V2.5，见 §7.1
- 一键成片渲染（短视频脚本与合成能力）仍暂不纳入

## 3. 信息架构与核心页面

NexusMind App 采用不超过五项的底部导航；设备和协议细节始终隐藏在家庭空间、场景与 AI 行动之后：

| Tab | 用户价值 | V1 页面重点 |
| --- | --- | --- |
| 管家 | 家庭与个人状态入口，呈现 AI 正在处理和需要确认的事项 | 问候、天气、AI 建议、待确认事项、管家动态、快捷入口 |
| 能力 | 专家对话框：与专家围绕项目遍历询问 | 历史对话框列表、新建对话框（选专家/连接器）、运行记录只读入口 |
| 待办 | 查看待执行与已执行的行动 | AI 生成方案、确认状态、执行结果、失败重试 |
| 家庭 | 以空间组织家庭状态与场景 | 房间卡片、关键设备状态、场景入口 |
| 设置 | 管理账户、家庭成员与连接 | 管家偏好、成员、Connector、权限 |

### 设计定位与体验原则

NexusMind 不是小米米家式设备列表、Home Assistant 式控制面板，也不是自由闲聊式聊天窗口。专家交互支持**对话式任务协作**形态：每条对话消息即一次可追溯的 Expert Run，移动端以「对话框（会话）」为载体围绕项目遍历询问，PC 端承载维护与运行细节。核心体验保持：**AI 主动理解 → 给出建议与影响说明 → 用户确认 → 自动执行 → 可追溯结果**。

视觉体验以 Calm、Intelligent、Human、Trust、Premium 为关键词：安静不打扰、能感知 AI 的存在、面向家庭成员、每一步透明可控，并保持高品质的家庭空间感。页面优先级遵循“上下文 → 状态 → 一个主行动”，避免同时竞争多个主操作。

#### 推送聚合策略

推送只传递需要离开当前界面处理的事项，并服务于“知情、确认或处置”，不能成为设备事件日志。服务端按家庭、成员权限、风险等级和目标资源聚合：**同类聚合**将同一设备/指标的重复异常折叠为一条未读事项；**场景聚合**将一次场景或管家运行产生的多个 L1 行动和结果合并为一条摘要；**时间聚合**在 30 分钟窗口内合并低风险事件，高风险或明确待确认事项不延迟；**周期聚合**把非紧急的环境、能耗和设备健康变化纳入早晚摘要。L2/L3 待确认事项、安防风险和成员直接点名的提醒不得被低风险摘要吞没，仍需按权限单独送达。每个聚合结果必须可追溯到来源活动、支持静默/退订偏好并防止跨家庭或跨成员泄露。

**推送优先级与聚合边界：** 推送优先级为 L3 > L2 > 周期摘要 > 场景聚合 > L1 合并。L3 永不聚合、永不延迟、不受静默偏好影响，除非成员在系统设置中完全关闭该类别通知。L2 不聚合、不延迟，但受成员静默偏好影响，例如“夜间模式”下延迟到次日 7:00 汇总。L1 按同类、场景和时间聚合，不影响 L2/L3 的独立送达。

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

### 管家 Dashboard

管家页展示“今天值得关注什么”，而非设备清单。实现入口为 `D:\HomeMind\mobile\lib\pages\dashboard_page.dart`，页面结构为 `SafeArea → Header → AI 主行动 → Family Status → Today Plan → Smart Scene → AI Suggestion`：

- 问候语、日期、天气与通知入口；
- 一条可行动的 AI 今日建议（检测依据、建议、执行/忽略）；
- 家庭安全、环境、照明等关键状态摘要；
- 用户日程与待办；
- 回家、离家、观影、睡眠等横向滑动快捷场景。

AI 建议卡（`NexusAICard`）是管家页核心，不使用聊天气泡；它应有 AI 语义色、克制的状态动效，并可进入对应 Expert。快捷场景卡建议维持约 120 × 100 的紧凑触控尺寸。

### 能力

能力页是“专家中心”，不是通用聊天窗口。每个专家需要明确展示：

- 能力与可读取/执行的权限；
- 需要的输入（家庭成员、作息、设备等）；
- 输出类型（建议、计划、自动化草案）；
- 预计消耗与执行影响；
- 可查看的运行与行动记录。

运行页可以借鉴 Agent 的任务时间线，但绝不展示模型思考过程；只展示可理解的阶段，例如“分析家庭环境”“获取设备状态”“生成优化方案”“等待确认”。运行状态统一为 `queued`、`running`、`completed`、`failed`，行动状态另行表达等待确认、执行中、成功和失败。

专家目录按 `planning`、`review`、`life` 等分类组织；个人生活专家属于新增的 `life` 分类，不新增底部导航 Tab，五 Tab 信息架构保持不变。

### 家庭

家庭按空间呈现，例如客厅、卧室、老人房、厨房。每个空间只展示对当前生活目标重要的状态，如温湿度、主灯、空调、睡眠模式和安全状态。

空间详情的第二层才显示设备与自动化：例如客厅的主灯、空调和观影模式；不得让实体 ID、协议名称或厂商字段成为默认界面内容。

### Plan 与 Connector

Plan 合并 Todo 和 Calendar，以“任务 / 日历”分段呈现当天进度与 AI 生成行动。移动端 Connector 位于“我的 → 连接”或家庭设置中：所有成员可查看已授权的家庭级连接；个人级连接只对绑定成员显示授权、过期和撤销入口。家庭级实例的创建、测试、发现、同步和成员授权不在移动端暴露，统一放入 Web 开发端。任何端均永不展示凭据明文。

### Web 用户端与开发端

Web 是与 Flutter App 共用同一 `/api/v1`、JWT、家庭和权限模型的 PC 入口，不是第二套产品或可绕过后端的管理后台。它分为两个入口：

| 入口 | 主要用户 | 产品职责 | 明确边界 |
| --- | --- | --- | --- |
| Web 用户端 | 所有已登录家庭成员 | 家庭概览、确认中心、管家动态、成员/知识、个人偏好、本人连接与 Run 记录 | 不配置家庭凭据，不查看其他成员个人连接 |
| Web 开发端 | 当前家庭 owner/admin 或部署者 | 家庭级 Connector、成员授权、同步任务、自动化、专家/Skill 的受控管理 | 不跨租户、不显示凭据、不直连 MySQL/HA/MCP SQLite |

前端路由是客户端发布物，不能作为 API 或权限的事实来源。用户端和开发端均按服务端返回的角色与权限显示菜单，服务端始终复验。`HomeMind.CreatorMcp` 的本地 SQLite 缓存只供受控本机 Agent 查询，不是家庭级 Connector、家庭知识库或任何 App/Web 数据源。

### 设置：AI 配置

移动端 AI 配置遵循**单一模型 + 单一 Key**原则，位于「设置」内：

- **Key 不落地前端：** API Key 仅由用户输入并提交给 NexusMind 服务端加密保存（`PUT /ai/config`），客户端只持有 `hasApiKey` 布尔状态，任何端均不展示凭据明文。
- **三态交互：** 未配置时为**新增态**（表单可编辑、Key 必填、显示保存）；配置完成后进入**只读态**（仅展示 Endpoint/模型/温度，不显示表单与保存按钮）；调整需进入**编辑态**（表单可编辑、Key 必重新输入、显示保存）。
- **启用/禁用：** 只读态提供启用开关，直接提交服务端持久化；`enabled=false` 时 AI 生成能力（`/ai/generate`、`/ai/chat`、`/ai/stream`）整体不可用，服务端校验并返回 422。

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
| 探店翻牌 | “今天下午去哪吃？” | 结合私藏店铺库、时间、位置、口味与天气，筛选 1-2 家并给出理由 | 存入今日计划（可选） |
| 行程规划 | “帮我规划周末去杭州” | 读取目的地、偏好（拍照/轻松/预算）、天气与私藏库，生成每日行程 | 同步日历、创建待办 |

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
User ── joins through ── TenantMember ── belongs to ── Workspace（当前租户/家庭）
                           │
              ┌────────────┴────────────┐
              ↓                         ↓
       Expert + ExpertVersion      Connector Provider
              ↓                         ↓
        Skill + Permission       Workspace Connector（household / personal）
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

### 6.1.1 账户与成员资格边界

`User`（可登录账户）、`Tenant Member`（账户在家庭内的成员资格）与 `Family Member`（生活上下文中的成员档案）必须分离。`family_members` 可以尚未拥有登录账户，不能作为认证或授权事实；家庭角色只能来自 JWT 对应的 active `tenant_members`。

| 模型 | 职责 | 关键字段 / 约束 |
| --- | --- | --- |
| User / `users` | 每人独立的 NexusMind 账户 | `id`、`display_name`、`status`、`timezone`、`locale`；登录标识与密码凭据分表保存，不把手机号/邮箱明文扩散到业务表 |
| User Identity / `user_identities` | 手机号、邮箱或认证提供方的登录标识 | `user_id`、`provider`、`issuer`、`subject_kind`、`subject_hash`、可选密文、`verified_at`、`revoked_at`；同一提供方主体唯一 |
| Tenant Member / `tenant_members` | 用户加入家庭后的资格、固定角色和启停状态 | `tenant_id`、`user_id`、`role`、`status`、`joined_at`；同一用户在同一家庭唯一，角色仅 `owner`/`admin`/`member`/`viewer`，每个 active 家庭至少一名 owner |
| Tenant Member Invitation / `tenant_member_invitations` | 可撤销的家庭邀请，不预先创建账户（V2.4 B19 已发布） | `id`、`tenant_id`、`invited_by_user_id`、受邀标识哈希/密文、`proposed_role`（不得为 owner）、`status=pending|accepted|expired|revoked`、`expires_at`、`accepted_user_id`；只接受标识匹配的已验证账户；同 `(tenant_id, subject_hash)` 仅一条 pending 邀请 |
| Family Member / `family_members` | 家庭生活上下文和生命周期 | 保留既有字段，可选 `linked_user_id` 仅建立档案到登录账户的关联，不能反向赋予权限 |

用户自己维护账户资料和登录设备/会话；owner/admin 维护当前家庭的邀请、成员资格、固定角色与停用状态，但不能修改他人密码、登录标识或个人 Connector 凭据。owner 转交必须验证目标为 active 成员，在同一事务中更新 `tenants.owner_user_id` 和成员角色；不得停用或移除最后一个 active owner。成员资格、邀请、owner 转交和 Connector 授权均写家庭审计。

### 6.1 AI Domain

| 模型 | 职责 | 关键字段 / 约束 |
| --- | --- | --- |
| Expert | 面向用户的 AI 角色，如家庭管家、周计划专家、睡眠专家 | `code` 唯一、`name`、`category`、`description`、`avatar`、`builtin`、`status`、`owner_user_id`（可空：空=平台基础专家，开发端维护、全家可见；非空=用户自建，PC 用户端维护、仅创建者本人可见） |
| Expert Version | 固化 Expert 的可复现策略 | `expert_id`、`version`、`system_prompt`、`skill_policy`、`output_schema`；历史 Run 必须关联此版本 |
| Skill | 可控、可授权、可观测的原子能力，而非供应商工具 | `code`、`category`、输入/输出 schema、`risk_level` |
| Expert Skill Permission | 限制某 Expert 版本可调用的 Skill | `expert_version_id`、`skill_id`、`max_calls`、`require_confirm`；默认拒绝未声明能力 |
| Expert Run | 一次专家分析或执行会话 | `user_id`、`tenant_id`、`expert_id`、`expert_version_id`、输入上下文、Token 用量、状态、开始/完成时间、`conversation_id`（可空，关联会话） |
| Conversation | 用户围绕某领域创建的对话项目（对话框），绑定专家与连接器 | `tenant_id`、`owner_user_id`、`title`、`expert_id`+`expert_version_id`（可空）、`workspace_connector_id`（可空，单值；多连接器后续演进关联表）、软删除、`updated_at`、`row_version` |
| Conversation Message | 会话内的一条对话消息，一次消息对应一次可追溯的 Expert Run | `conversation_id`、`role`（`user`/`assistant`）、`content`、`run_id`（可空）、`created_at`；按 `conversation_id + id` 游标分页 |
| Run Step | 运行中的内部可编排步骤 | 仅保存可审计的计划/执行元数据，不作为模型思考链输出 |
| Run Event | 面向 Flutter 时间线的可理解事件 | `run_id`、`sequence`、`type`、`step_id`、`status`、`display_message`、`created_at`；同一 Run 内 sequence 唯一且不可改写 |
| Run Action | AI 建议的可确认变更 | `run_id`、`action_type`、`payload`、`status`、`confirmed_at`、`idempotency_key`、`result` |
| Family Member / `family_members` | 家庭成员及其可审计生命周期 | `home_id`、`name`、`relation`、`birthday`、`is_elderly`、`is_child`、`member_status`、`preferences`、`created_by`、软删除 |
| Family Knowledge / `family_knowledge` | 家庭可维护的事实及其来源 | `home_id`、`category`、`key`、`value`、`notes`、`source_member_id`、`confidence_score`、`conflict_resolution_strategy` |
| Personal Favorite / `personal_favorites` | 成员个人的偏好集合（店铺、旅行点、素材），支撑个人生活专家 | `home_id`、`owner_member_id`、`category`（`restaurant`/`travel`/`material`）、`name`、`detail_json`、`visibility`（默认 `private`）；与 `family_knowledge` 区分：知识是家庭共享事实，收藏是个人偏好，默认仅归属成员可见 |

**知识写入权限规则：** 默认所有 `active` 家庭成员可写入；`category=security`（WiFi 密码、门锁密码）仅限家庭管理员写入。家庭管理员可在“设置 → 家庭知识”中调整分类写入权限。AI 可在用户授权后从对话中提取知识并写入；此时 `source_member_id=system_ai`，`confidence_score` 默认为 `0.6`，经用户确认后提升至 `0.9`。当 `conflict_resolution_strategy=authority` 时，权威来源指向 `family_members` 中 `is_primary=true` 且 `member_status=active` 的成员；若无活跃主成员，则降级为 `latest` 策略。

Expert Run 的持久化状态使用细粒度状态：`draft`、`queued`、`planning`、`running`、`synthesizing`、`needs_input`、`completed`、`failed`、`cancelled`。Flutter 只映射成适合界面的 `queued`、`running`、`completed`、`failed`，并通过 Run Event 展示可理解阶段。

对话消息经会话发送时，创建的 Run 必须携带 `conversation_id`；消息历史即会话上下文，运行时按会话加载历史拼接输入上下文（复用 `input_context` 语义），移动端不做本地上下文缓存。

Run Action 的标准状态为 `pending`（等待确认）、`confirmed`、`rejected`、`executing`、`executed`、`failed`、`cancelled`。任何设备或自动化写操作先创建 Action，不能由 Planner 直接下发。`payload` 保存标准化目标与参数，审计和 API 输出须脱敏。

`family_members.member_status` 是家庭成员生命周期事实，固定为 `active`、`away`、`permanently_left`、`deceased`：`active` 与 `away` 可双向切换；两者可转入 `permanently_left` 或 `deceased`；终态不得由普通更新恢复，只允许具备家庭管理权限的更正操作恢复，并留下审计记录。软删除不替代生命周期状态，也不得物理删除已有知识来源、决策或审计关联的成员。`family_knowledge.source_member_id` 必须指向同一家庭的成员或系统主体 `system_ai`，`confidence_score` 为 0 到 1 的可解释置信值，`conflict_resolution_strategy` 仅允许 `latest`、`authority`、`majority`；同 key 写入冲突时保存来源和解决结果，不静默覆盖。

**禁止**把 Prompt、模型思考过程或原始推理链写入 `run_events` 或返回给客户端；只允许“正在检测客厅环境”“正在生成优化方案”等用户可理解事件。

### 6.2 Connector 与 Smart Home Domain

Connector Domain 服务全部外部生态：`Connector Provider` 与 `Workspace Connector` 为通用模型，Calendar、Email、Health、Finance 和 Smart Home 都遵守同一 Tool、Permission、Run Action 与审计边界。Smart Home Domain 仅扩展设备、空间、场景和自动化等物理世界模型；它是首个 Connector 实现，不是领域模型的默认中心。

| 模型 | 职责 | 关键字段 / 约束 |
| --- | --- | --- |
| Connector Provider | 全局 Connector 类型目录，如 Home Assistant、Google Calendar、Notion | `code` 唯一、`name`、`type`、`provider`、`description`、`status` |
| User / `users` | 独立登录账户，而非家庭成员资料的副本 | `display_name`、`status`、`timezone`、`locale`；手机号/邮箱等身份仅在 `user_identities` 的加密/哈希字段保存 |
| Tenant Member / `tenant_members` | 用户在家庭中的成员关系、固定角色和生命周期 | `tenant_id`、`user_id`、`role`（`owner/admin/member/viewer`）、`status`、`joined_at`；同一用户可加入多个家庭，但每次请求只在 JWT 当前家庭内执行 |
| Workspace Connector | 某家庭实际授权的 Connector 实例；可为家庭共用或成员个人绑定 | `tenant_id`、`connector_provider_id`、`binding_scope`（`household/personal`）、`owner_user_id`（个人必填、家庭为空）、`name`、`status`、`auth_status`、`config`、`credential_ref`、`created_at`、`last_sync_at`、`last_health_at` |
| Connector Tool | Provider 声明的稳定工具契约，如 `turn_on_light`、`set_temperature`、`create_calendar_event` | `connector_provider_id`、`name`、`description`、`input_schema`、`output_schema`、`permission`、`risk_level`、`require_confirm`；同一 Provider 内 `name` 唯一 |
| Workspace Connector Tool | 某已授权实例实际可用的工具及其发现/禁用状态 | `workspace_connector_id`、`connector_tool_id`、`status`、`availability_reason`、`last_checked_at`；不复制密钥或厂商原始字段 |
| Connector Permission Grant | 成员对家庭级连接工具或权限范围的显式许可；不用于共享个人凭据 | `user_id`、`workspace_connector_id`、`connector_tool_id` 可空、`permission`、`effect`、`confirmation_policy`、`granted_at`、`revoked_at` |
| Connector Authorization Session | 一次个人 OAuth 或受控家庭授权的短期服务端会话 | `tenant_id`、`connector_provider_id`、`binding_scope`、`initiator_user_id`、`state_hash`、`pkce_verifier_ref`、`redirect_uri`、`status`、`expires_at`、`completed_at`；不保存明文 code、access token 或 refresh token |
| Smart Home Space | 家庭空间 | `tenant_id`、`name`、`space_type`、`sort_order` |
| Smart Home Device | 规范化后的真实设备或传感器 | `tenant_id`、`workspace_connector_id`、`external_id`、`space_id`、`name`、`device_type`、`online_status`、`zigbee_role`、`battery_level`、`signal_lqi`、`health_status`；连接内 `external_id` 唯一 |
| Device Capability | 设备可读/写的标准能力 | `device_id`、`capability`、`value_schema`、`permission`；例如 `power:boolean`、`brightness:number` |
| Device State | 设备最近一次标准化状态快照 | `device_id`、`state_json`、`updated_at`；必须标记采样时间，不将过期数据描述为实时 |
| Scene / Scene Action | 用户可理解的多个设备动作组合 | Scene 归属家庭；Action 记录 `scene_id`、目标设备、Capability 与目标值 |
| Scenario Template / Scenario Instance（B22 起） | 场景工作流的两级配置化载体：平台模板定义能力步骤，家庭启用后生成解析到具体设备的实例；执行、确认、幂等与审计全部复用 Run Action 链路，不新增执行引擎 | Template：`code` 唯一、`trigger_keywords`、`steps`（`device_type`/`room`/`capability`/`value`/`optional`，`room="*"` 不限房间）；Instance：归属家庭、`template_code`、解析后步骤（`device_id`/`step_status`=ready\|unavailable/`reason`）、`status`=enabled\|disabled（B23 起支持禁用，禁用只阻止新触发、不中断进行中运行，重复启用可恢复）、`row_version`；缺设备不阻塞启用（Enable-time tolerant），执行时跳过 unavailable 步骤；场景风险 = MAX(步骤风险)，门锁/安防类 L3 其余 L1 |
| Automation Rule | 经授权的长期自动化 | `trigger`、`conditions`、`actions`、`approval_policy`、`enabled`；复用 Skill、Connector、Run、Action 模型 |

Connector Provider 与 Workspace Connector 必须分离：前者描述“可接入的产品”，后者描述“当前家庭已授权的实例”。`binding_scope=household` 的实例由 owner/admin 配置并通过 Permission Grant 授予成员；`binding_scope=personal` 的实例必须由当前成员授权，`owner_user_id` 必须是同一家庭的 active `tenant_member`，只允许该成员读取、调用和撤销。个人实例产生的可共享业务结果必须经另一个显式领域动作写入，不能因连接本身自动共享。`status` 描述连接运行健康，`auth_status` 描述授权生命周期（如 `not_connected`、`pending`、`authorized`、`expired`、`revoked`、`failed`），两者不可混用。`config` 是通过 Provider Schema 校验的非敏感配置（如服务地址、区域、同步选项）；访问令牌、客户端密钥和刷新令牌仅由 `credential_ref` 指向 Secret Vault，数据库和 API 不得保存或回传明文。

个人 OAuth 的浏览器只用于跳转和显示结果：授权发起、`state`/PKCE 校验、回调、Token 交换、加密存储、刷新、撤销和审计均由服务端处理。`Connector Authorization Session` 的 `state_hash`、PKCE 引用和过期时间必须单次使用；回调 URL 必须为 Provider 预注册白名单。家庭共读和个人发布是两个不同实例，前者不因成员个人 OAuth 而获得访问权。

`Connector Tool` 是 AI 可见的最小外部能力边界，而不是厂商 API 的镜像。工具用 JSON Schema 定义输入和输出，例如 `turn_on_light({ room })` 与 `set_temperature({ temperature })`；Smart Home 工具仍需在执行时解析为已授权的空间、设备能力和 `Run Action`。工具定义应保持与 MCP Tool 的 `name`、`description`、`inputSchema` 语义兼容，使后续可由 NexusMind 作为 MCP Client 调用外部 Server，或把 Connector 暴露为 MCP Server，而不改变 Expert 或 Skill 的业务语义。

**创作者中心本地 MCP Bridge：** 专家、专家组和技能可通过受现有权限保护的 API 同步至 N97 的本地 SQLite 缓存，再由独立 MCP Server 以只读 Tool 提供给本地 Agent。同步过程必须显式触发并记录成功时间；MCP Server 不得直连生产库，也不得绕过租户和用户权限。专家与技能的提示词属于敏感数据，默认不得写入本地缓存或通过 MCP 返回；只有设备侧显式启用并由调用方显式请求时才可处理。当前 V1 采用 stdio transport；远程访问须另行实现受认证、会话和 Origin 校验保护的 Streamable HTTP transport。

用于 Zigbee 网络拓扑退化诊断时，`zigbee_role` 仅取 `end_device`、`router`、`coordinator`，`battery_level` 为百分比或未知，`signal_lqi` 为协议映射后的链路质量值，`health_status` 仅取 `healthy`、`degraded`、`offline`、`low_battery`。这些是标准化的运行健康数据，不在 Flutter 或通用业务层暴露 Topic、网络地址、厂商实体 ID 或原始协议报文。

### 6.3 Permission Domain

权限控制分为三层：

1. **成员连接与工具授权：** 成员只能使用其在家庭中被授予范围的 Connector、Tool 和 Permission；授权支持显式 `allow` / `deny` 与过期/撤销，默认拒绝。
2. **确认策略：** Permission Grant 或 Tool 定义可以指定 `never`、`always`、`high_risk_only`。发送消息、控制灯等低风险写入至少需要授权；门锁、安防和创建长期自动化必须 `always` 确认，不能由成员或 Expert 覆盖为自动执行。
3. **Expert Skill 授权：** Expert Version 只能调用已声明的 Skill，并受调用次数、风险和确认策略限制；Skill 只能委派给已启用且已授权的 Connector Tool。
4. **Run 权限快照：** 创建 Run 时解析成员、家庭、Connector、Tool、设备能力和 Expert 权限，形成不可变快照。确认与执行前再次校验实时权限、Tool 可用性与资源版本。
5. **固定家庭角色与 Web 路由：** V1 固定 `owner`、`admin`、`member`、`viewer` 四角色，角色含义由服务端策略代码定义，Web 只消费权限结果。用户维护以账户资料、`tenant_members` 成员管理和邀请流程为边界；不提供可任意创建角色或编辑 API 路由的后台。Web 路由随前端版本发布，通过权限码显示/隐藏；若确需家庭级菜单个性化，已发布独立 `web_navigation_preferences`（`tenant_id`、`role`、`route_key`、`enabled`、`sort_order`、`updated_by`）表（V2.4 B19），限制为已发布 `route_key`（`NexusWebNavigationKeys` 静态白名单）的显示偏好，绝不存 API URL、权限规则或可执行脚本；成员角色受控管理（`tenant.member.manage`）、邀请流程与 owner 转让（同事务更新 `tenants.owner_user_id` + 双方角色，最后一名 active owner 守恒）亦随 B19 发布。

高风险能力（门锁、安防、自动化创建等）必须 `require_confirm=true`；低风险只读 Skill 不需要用户逐项确认，但仍需要审计。所有写操作使用 `idempotency_key`，重复确认只能返回既有结果。

### 6.4 V1 数据范围与演进

V1 实现 `Expert`、`ExpertVersion`、`Skill`、`ExpertSkillPermission`、`ExpertRun`、`RunEvent`、`RunAction`、`Conversation`、`ConversationMessage`（专家会话化）、`Connector Provider`、`Workspace Connector`、`Connector Tool`、`Workspace Connector Tool`、`Connector Permission Grant`、`Device`、`DeviceCapability`、`DeviceState`、`Scene` 与 `Personal Favorite`。首批可使用 Mock Connector 验证工具发现、权限、Skill 调用和 Run 记录；Home Assistant 是第二阶段的正式设备接入。`Automation Rule`、Expert DAG/协作组、MCP Server 托管和 Skill Marketplace 进入后续阶段。

数据库命名以现有 SQL 迁移和实体为事实来源；建议采用 `experts`、`expert_versions`、`skills`、`expert_skill_permissions`、`expert_runs`、`run_events`、`run_actions`、`conversations`、`conversation_messages`、`connector_providers`、`workspace_connectors`、`connector_tools`、`workspace_connector_tools`、`connector_permission_grants`、`smart_home_*`、`device_capabilities`、`device_states`、`scenes`、`scene_actions`、`automation_rules`、`personal_favorites`。不得仅为对齐文档修改已上线迁移；实际落地前先核对现有 `AiEntities.cs` 和迁移的重用空间。

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
| 快速剪辑 | 素材位置 + 创作目标和指令 | 剪映 .draft 草稿（登记 Expert File） | `media.read`、`ai.skills.*` |

### Skill 跨端分级

Skill 按**产物形态**分级，决定其承载端（与「附件选择/上传、Action 确认、运行时间线、生成文件下载已移 PC 端」的既有决策同源）：

- **简单 Skill（移动端）**：输出为即时状态/建议/单动作确认，单屏可完成，如读取家庭环境、控制灯光/空调、创建家庭提醒、场景一键执行、探店翻牌与行程规划；
- **复杂 Skill（Web 端）**：输入含文件/路径或多步编排，产物为可下载文件，如快速剪辑（及未来的 PPT/渲染类）；
- 分级判定以产物形态为准，不引入 `mobile_friendly` 等元数据字段（YAGNI，待 Skill 数量增长后再评估）。

### 7.1 快速剪辑 Skill（V2.5）

**产品目标**：把「探店/日常拍摄素材 → 可编辑剪映草稿」的重复劳动用对话完成。产物是剪映 .draft 草稿文件，可在剪映中继续编辑或渲染，**不做一键成片**；草稿为本地文件、可编辑可丢弃、不对外发布。

**用户表达与执行链路**：

```text
用户："帮我剪一下探店视频"（+素材位置 +创作目标和指令）
  ↓
NexusMind Agent（理解意图）
  ↓
匹配「快速剪辑」Skill
  ↓
Skill Executor 调用剪辑 MCP（jianying-mcp / capcut-mate）
  ↓
MCP 调用 FFmpeg(ffprobe) 解析视频/音频时长与分辨率
  ↓
生成剪辑方案（片段序列/音频/时长摘要）→ 用户确认
  ↓
add_video_segment / add_audio_segment 写入 → export_draft()
  ↓
生成 .draft 草稿文件 → RegisterGeneratedFileAsync 登记（复用 Expert File 链路）
  ↓
返回"草稿已生成，打开剪映即可编辑"
```

**Skill 输入契约**：

- `素材位置`：本机/NAS 目录路径或 URI（视频/音频文件或目录）；
- `创作目标和指令`：自然语言，如时长、画幅比例、配乐、字幕等要求。

**剪辑 MCP 工具契约**（jianying-mcp / capcut-mate，FFmpeg 为后台依赖）：

| 工具 | 输入 | 输出 |
| --- | --- | --- |
| `add_video_segment` | video_url | 片段已写入草稿（含 ffprobe 解析的时长/分辨率） |
| `add_audio_segment` | audio_url | 音频已写入草稿 |
| `export_draft` | — | .draft 文件路径 |

**确认与风险**：生成剪辑方案摘要（片段数/音频/时长）后经用户确认再写入草稿；草稿为本地可编辑可丢弃文件、不对外发布，属低风险操作，运行记录与审计留痕。素材读取需 `media.read` 权限（新权限码，与后端设计对齐时确认）。

**跨端契约**：

- 移动端：无入口、不承载该能力（复杂 Skill 不在移动端范围）；能力 Tab 仅保留既有只读运行记录入口；
- Web 端：完整流程——快速剪辑工作台（`/app/media/quick-edit`）：素材位置与创作目标和指令表单 → 创建 Skill Run → 轮询至剪辑方案摘要 → Action 确认 → .draft 生成文件下载（复用 `/app/runs/:id` 现有 readToken 下载能力）；
- 服务端：SkillExecutor 首个实现——Skill 独立执行（`POST /api/v1/skills/{skillCode}/runs`，SourceType=skill 的 AgentRun，不绑定专家，同场景工作流先例）、剪辑 MCP 客户端、`media.read` 权限、产物经 `RegisterGeneratedFileAsync` 登记。

**验收标准**：对话输入素材位置与创作指令 → 返回 .draft 路径；ffprobe 元数据解析正确；草稿可在剪映打开编辑；运行/审计完整。

**待决**（留待后端设计）：jianying-mcp / capcut-mate 具体项目选型与剪映版本兼容；剪辑 MCP 部署形态（需部署于可访问素材目录与剪映草稿目录的主机，符合本地优先原则）——两者为后端 B24/B25 切片的前置依赖。

**B24 实施状态（2026-08-08）**：`029` 迁移新建平台级 `skills` 目录表（tenant_id=1）并注册 `quick-edit`（media / L1 / media.read），`family_audit_logs` CHECK 扩展 `skill_run_created`/`skill_action_confirmed`/`skill_draft_registered` 与 `skill_run`/`skill_draft`；`POST /api/v1/skills/{skillCode}/runs`（`ai.run` + `media.read`）已发布：确定性方案生成（指令时长提取 1-600 秒、默认 15 秒、单片段方案）+ `draft_generate` Action（L1）+ `skill_run_created` 审计；`dotnet build` 0 errors/0 CS1591、`dotnet test` 全绿 165/165、真实 MySQL 029 顺序迁移已在本机验证。剪辑 MCP 选型与部署形态仍为 B25（Action 确认 → 剪辑 MCP 写入草稿 → 生成文件登记 → readToken 下载）前置依赖。

**B25 实施状态（2026-08-09）**：`POST /api/v1/skills/runs/{runId}/actions/{actionId}/confirm`（`ai.run` + `media.read`）已发布：确认 `draft_generate` 动作 → 剪辑 MCP 客户端（当前为确定性 Mock `MockClippingMcpClient`，不访问素材目录、不产生真实文件路径）生成 .draft 草稿内容 → `RegisterGeneratedFileAsync` 登记为 Ready 生成文件（附件到 run）→ 下载复用既有 readToken 端点（10 分钟）；写 `skill_action_confirmed`/`skill_draft_registered` 审计；无新迁移；`dotnet build` 0 errors/0 CS1591、`dotnet test` 全绿 170/170（新增确认执行/幂等重放/错误分支/Mock 草稿 5 项测试）。剪辑 MCP 真实项目选型与部署形态（jianying-mcp / capcut-mate，需部署于可访问素材与剪映草稿目录的主机，符合本地优先原则）为部署环境验证项，不阻塞 B24/B25 切片收口。

## 8. 文档治理与联动

本文件是“为什么做、做什么、跨端如何协作”的总纲，也是控制产品走向的唯一最终产品内容输出。当前产品由一人负责从产品设计、研发设计到落地，因此该维护者同时承担产品决策收敛、跨端拆分和实施状态核对职责；但产品、前端、后端和计划表仍须保持各自明确的内容边界。

### 单人全流程交付链路（V2.3）

每个已确认的产品变更必须按以下链路流转，不能直接从想法跳到前端、后端或代码：

```text
产品总设计（最终产品内容输出）
  ↓ 拆分体验、状态与验收要求
前端总设计
  ↓ 拆分领域、数据、API 与安全要求
后端总设计
  ↓ 更新当前实施快照
前端开发计划表 / 后端开发计划表
  ↓
代码实现、测试与完成状态回写
```

**最终产品内容输出**由本文维护，是每次产品决策的可执行结论，而不是需求讨论的汇总。它必须明确目标用户与问题、目标和非目标、用户体验与人机协同边界、风险等级与确认规则、领域模型与数据约束、跨端契约、验收标准，以及当前版本的范围与待决项。出现冲突时，以本文中最新且已记录的结论为准；Smart Home 仍仅是 Connector，Agent Runtime、Expert、Skill、Connector 和统一设备抽象的既有领域边界不得因下游实现而改变。

前端总设计只承接本文已确认的页面信息架构、交互流程、状态、权限呈现、接口消费和验收；后端总设计只承接已确认的领域模型、存储、API、授权、幂等、审计、Connector 适配和运行时约束。任一端发现实现不可行或需要改变产品语义时，必须回到本文决策并新增变更记录，不能在下游文档中单独改写产品方向。

开发计划表是持续更新的实施快照，不是设计变更档案。前端和后端计划表均只保留当前范围内的“已完成”和“下一步”，以及完成所需的最小验收信息；每次调整应替换过时条目、重排优先级并回写完成状态，不累计叠加历史任务，不维护变更记录，也不以计划表内容覆盖产品总设计或前后端总设计。

实施细节分别由下列文件维护：

| 文档 | 位置 | 负责内容 |
| --- | --- | --- |
| 产品总设计（本文） | `D:\HomeMind\core\docs\main\NexusMind-Product-Master-Design.md` | 产品范围、领域边界、版本路线、跨端契约 |
| 后端总设计 | `D:\HomeMind\core\docs\main\NexusMind-Backend-Development.md` | API、模型、数据库、服务分层、Connector 与执行安全 |
| 前端总设计 | `D:\HomeMind\mobile\docs\main\NexusMind-Frontend-Development.md` | 页面、组件、状态、接口消费、交互与验收 |
| 后端开发计划表 | `D:\HomeMind\core\docs\main\NexusMind-Backend-Development-Plan.md` | 当前已完成、下一步、优先级和最小验收；不记录变更历史 |
| 前端开发计划表 | 前端仓库内对应计划文件 | 当前已完成、下一步、优先级和最小验收；不记录变更历史 |
| 前端开发规范 | `D:\HomeMind\mobile\docs\DEVELOPMENT_GUIDELINES.md` | Flutter 分层、Provider、路由、异步、安全与质量门禁 |
| UI 样式规范 | `D:\HomeMind\mobile\docs\UI_STYLE_GUIDE.md` | 设计 token、排版、布局、组件状态与响应式规则 |
| 主题实现 | `D:\HomeMind\mobile\lib\core\ui\nexus_theme.dart` | 语义色、20px 内容容器、排版、按钮、输入框和深浅主题 |
| API 实现状态 | `D:\HomeMind\core\docs\api-implementation.md` | 已发布路由、运行时配置门禁和服务端安全约束 |
| 前端 API 契约 | `D:\HomeMind\core\docs\frontend-api-integration.md` | 字段级请求/响应、错误、幂等与前端消费规则 |

### 同步流程（必须执行）

1. **先修改本文：** 记录需求背景、最终产品内容、范围、领域模型、页面/API 影响和版本状态；所有新增修改均进入 V2.3，且先写入变更记录。
2. **确认影响面：** 在“变更记录”中标明需要同步的前端总设计、后端总设计、开发规范、UI 规范与计划表。
3. **拆分总设计：** 将已确认结论分别写入前端总设计和后端总设计；下游文档不得自行改变产品目标、风险等级、授权语义或领域边界。
4. **刷新计划快照：** 根据两份总设计替换前端、后端开发计划表中过时内容，只更新“已完成”和“下一步”，不新增计划表变更记录。
5. **严格按计划实施：** 后续前后端开发必须按对应开发计划的当前切片、依赖、优先级和最小验收执行；未经计划调整，不得跳过切片、改变实施顺序或以手工代码修改替代计划项。前端变更遵循 Flutter 开发/UI 规范；API 变化还必须遵循后端 `DEVELOPMENT.md` 中的接口文档规则。
6. **实施结果回写：** 每次实施完成、部分完成、受阻或验证失败后，先更新对应计划表的状态、下一步、验收结果和未验证依赖；随后在同一变更中同步调整受影响的前后端总设计及本文的产品约束、技术设计和实施状态。
7. **处理手工调整与回归：** 手工调整代码后出现 Bug、测试不通过或契约偏差时，也必须先调整对应开发计划，再同步回写相关总设计和本文；不得只修复代码而不更新计划与设计。若需要改变产品范围、领域语义、风险规则或跨端契约，必须先在本文确认决策。
8. **关闭变更：** 产品总设计、受影响的前后端总设计和对应计划快照均已对齐后，才视为设计变更完成。

### 变更记录

| 日期 | 主题 | 本文变更 | 前端同步 | 后端同步 | 状态 |
| --- | --- | --- | --- | --- | --- |
| 2026-08-09 | V2.5 B25 剪辑执行与文件登记 | 快速剪辑确认执行链路落地：`POST /api/v1/skills/runs/{runId}/actions/{actionId}/confirm`（`ai.run` + `media.read`）确认 `draft_generate` 动作 → 剪辑 MCP 客户端（确定性 Mock `MockClippingMcpClient`，不访问素材目录、不产生真实文件路径）生成 .draft 草稿内容 → `RegisterGeneratedFileAsync` 登记为 Ready 生成文件（附件到 run）→ 下载复用既有 readToken 端点（10 分钟）；`skill_action_confirmed`/`skill_draft_registered` 审计；无新迁移；剪辑 MCP 真实项目选型与部署形态（jianying-mcp/capcut-mate，需可访问素材与剪映草稿目录的主机）转为部署环境验证项，不阻塞 B24/B25 收口 | 待同步 Web 端文档（快速剪辑工作台确认与下载流程接入 7.8 契约） | 已同步后端总设计（§16 B25 实施状态）与开发计划（B25 已完成，V2.5 收口） | 已同步 |
| 2026-08-09 | V2.6 小红书笔记发布（B27） | 发布 L2 确认链路落地：`031` 迁移重建 `expert_runs.ck_run_source` CHECK（追加 scenario/skill/xhs，补 B22/B24 真实库缺口）；`IXhsPublishServices`/`XhsPublishServices`（参数校验 → SourceType=xhs 的 Run + `xhs_publish` Action L2 → 确认经本地 MCP `xhs_publish_content` 发布 → `xhs_note_published` 审计）；`POST notes/publish` 与 `POST publish-actions/{actionId}/confirm`（`ai.run` + `connector.write`）；幂等重放不重复发布 | 待同步 Web 端文档（发布确认接入 10.1 契约） | 已同步后端总设计（§17 B27 实施状态）与开发计划（B27 已完成，B28 下一步） | 已同步 |
| 2026-08-09 | V2.6 小红书个人级 Connector（B26 授权与搜索） | 小红书作为个人级 Connector 落地（搜索+发布，经本地 stdio MCP xhs-mcp 调用，Puppeteer 扫码登录、凭据本机管理）：`030` 迁移注册 xhs Provider 与 `xhs_note_published`/`xhs_note` 审计 CHECK；本地 MCP 客户端基础设施（`IMcpProcessClient`/`StdioMcpProcessClient` + `IXhsMcpClient`/`XhsMcpClient`/Mock）；扫码登录态适配现有授权模型（发起扫码/轮询/撤销，不改表结构、不落 cookie 明文、`credential_ref` 仅存 `local://xhs-sessions/{uuid}`）；搜索/详情只读 L1 API 与登录状态；发布（L2）按 B27 排期 | 待同步 Web 端文档（小红书授权扫码/搜索接入 10.1 契约） | 已同步后端总设计（§17 B26 实施状态）与开发计划（B26 已完成，B27 下一步） | 已同步 |
| 2026-08-08 | V2.5 B24 快速剪辑 Skill 基线 | `029` 迁移新建平台级 `skills` 目录表（key/category/input_schema/output_schema/required_permission/risk_level，tenant_id=1）并注册 `quick-edit`（media/L1/media.read）；`media.read` 权限（owner/admin/member）；`POST /api/v1/skills/{skillCode}/runs` 发布（SourceType=skill、确定性方案生成 + `draft_generate` Action L1、`skill_run_created` 审计）；剪辑 MCP 选型与部署形态仍为 B25 前置依赖 | 待同步 Web 端文档（快速剪辑工作台接入 7.7 契约） | 已同步后端总设计（§16 B24 实施状态）与开发计划（B24 已完成，B25 下一步） | 已同步 |
| 2026-08-08 | V2.5 快速剪辑跨端修订 | 复杂 Skill（快速剪辑）整体归 Web 端完整流程（工作台表单/方案确认/草稿下载），移动端无入口、仅保留简单 Skill；新增「Skill 跨端分级」（按产物形态判定，不引入 mobile_friendly 元数据）；Skill 独立执行端点 `POST /api/v1/skills/{skillCode}/runs`（SourceType=skill，同场景工作流先例，不绑定专家）与 `media.read` 权限；剪辑 MCP 选型与部署形态为后端 B24/B25 前置依赖 | 已同步前端总设计（移动端无新增、显式边界）与 Web 端文档（快速剪辑工作台页面） | 已同步后端总设计（§16）与开发计划（B24/B25） | 已同步 |
| 2026-08-08 | V2.5 快速剪辑 Skill | 落地「快速剪辑」Skill：素材位置 + 创作目标和指令 → jianying-mcp/capcut-mate 调 FFmpeg 解析 → 生成剪映 .draft 草稿并登记 Expert File；产物可编辑不对外发布、低风险、方案确认后生成；跨端契约：移动端对话、web 端目录/运行/下载复用；§2 短视频生成拆为「快速剪辑已排期 + 一键成片仍不纳入」；剪辑 MCP 独立于 CreatorMcp（遵守 §12.2-5） | 待同步前端总设计（运行详情文件下载复用，无新页面） | 待同步后端总设计（SkillExecutor 实现、剪辑 MCP 客户端、media.read） | 待同步 |
| 2026-08-08 | V2.3 B23 场景实例禁用 | 场景实例 `status=enabled\|disabled` 语义落地：禁用只阻止新触发（Run 404）、不中断进行中运行、重复启用恢复；纯后端能力，不改领域模型与风险规则 | 已同步前端总设计（场景实例卡片「禁用/启用」入口） | 已同步后端总设计（§15 实例状态流转）与开发计划（B23） | 已同步 |
| 2026-08-07 | V2.4 B22 场景工作流 | 落地「场景 = Run 的一种特殊输入」决策：`ScenarioTemplate`/`ScenarioInstance` 两级模型（平台模板 → 家庭实例），执行引擎硬编码（复用 Run Action 确认/幂等/审计链路）、内容配置化（实例化 Device Resolver 容忍缺设备）、步骤上下文承载于运行动作 metadata、不新增 Step 表与独立引擎；场景风险取步骤 MAX；旧场景路由保留为兼容代理；拖拽编排与步骤表按演进门槛（运营/用户/step SLA 需求）触发 | 待同步前端总设计（场景 Tab 模板化「一键启用」入口） | 已同步后端总设计（§15）与开发计划（B22） | 已同步 |
| 2026-08-07 | V2.4 B19 Web 治理 API | 落地 `tenant_member_invitations`（手机号哈希邀请、owner 转让）与 `web_navigation_preferences`（角色粒度菜单偏好）+ 成员/角色受控管理 + 邀请流程 + 我的个人连接汇总；固定 4 角色不变；菜单偏好仅接受已发布 `route_key` 显隐/排序 | 待同步前端总设计 | 已同步后端总设计 | 已同步 |
| 2026-08-07 | V2.4 B20 专家会话 | 会话/消息迁移 `026`、`IConversationServices`（CRUD/游标分页/上下文拼接/幂等）、`conversation.read/write` 权限、`expert-runs` 携带 `conversationId`、终态自动追加 assistant 消息、会话 7 端点发布；`expert.mine.read/write` 权限预注册（B21 消费） | 已同步前端总设计（会话列表/新建对话框/纯对话/轮询追加） | 已同步后端总设计（§13.1 实施状态） | 已同步 |
| 2026-08-07 | V2.4 B21 自建专家 | `experts.deleted_at` 软删除（`027`）、`GET /experts?scope=basic\|mine\|all` 来源过滤与类型化目录视图、自建专家 CRUD（创建自动生成 `custom-` 编码与 v1 版本、更新生成 version+1、软删除全链路消失）、`expert.mine.read/write` 权限消费；第六阶段「专家会话与自建专家」全部落地 | 已同步前端总设计（选专家基础/自建分组展示并标识来源） | 已同步后端总设计（§13.1 实施状态、阶段表） | 已同步 |
| 2026-08-07 | 专家会话化：移动端纯对话 + PC 端维护 + 用户自建专家 | 专家交互新增「会话（对话框）」形态：移动端纯对话（历史对话框列表 + 新建对话框、选专家/连接器、多轮上下文由后端承载），附件/Action 确认/时间线/下载移 PC 端；PC 用户端新增「我的专家」（自建/维护，仅本人可见）；新增 Conversation/Message 领域模型与 `experts.owner_user_id`、`expert_runs.conversation_id`；会话/消息 API 与 `scope=basic\|mine\|all` 契约；飞书/钉钉等 Connector 仍属暂不纳入范围，连接器可用性受 Provider 发布节奏约束 | 已同步前端总设计与 Web 端文档 | 已同步后端总设计 | 已同步 |
| 2026-08-06 | AI 配置启用开关与移动端交互 | 新增「设置：AI 配置」产品决策：单一模型 + 单一 Key、Key 仅服务端加密保存、三态交互（新增/只读/编辑）、只读态启用/禁用开关；`AiConfig` 增加 `enabled` 字段，禁用时 AI 生成能力整体不可用 | 已同步前端 AI 配置页三态改造 | 已同步后端开发计划 B18 | 已同步 |
| 2026-08-06 | V2.3 个人生活专家前置 | 修订 V2.3 版本语义：新增产品能力“个人生活专家”（探店翻牌、行程规划）；新增 `personal_favorites` 领域模型与 `life` 专家分类；OCR 截图识别与短视频生成暂不纳入 V1 | 已同步前端总设计与前端开发计划表（life 分类、翻牌/行程/收藏页面与 P5b/P5c 交付项） | 已同步后端开发设计与后端开发计划（B15-B17 已完成） | 已同步 |
| 2026-08-05 | V2.3 计划执行与回写规范 | 明确后续开发严格按计划执行；实施、手工调整、Bug 与测试回归均须先更新计划，再回写总设计 | 按受影响范围同步 | 已同步后端开发设计与后端开发计划 | 已同步 |
| 2026-08-05 | V2.3 单人全流程产品治理 | 明确 V2.3 不新增产品功能；统一五 Tab 命名，补充风险判定、家庭知识写入、管家动态与技术审计关系、推送优先级，并将 Phase 0 计划替换为进入 Phase 1 的门槛；新增“产品总设计 → 前端/后端总设计 → 开发计划表”的拆分链路，并规定计划表只维护当前已完成与下一步、不记录变更历史 | 待同步前端总设计与前端开发计划表 | 已同步后端总设计与后端开发计划表 | 后端已同步，前端待同步 |
| 2026-08-06 | V2.2 风险与运营契约补充 | 增加家庭成员状态机、知识冲突字段、Zigbee 拓扑健康、推送聚合、进入 Phase 1 的 Go/No-Go 标准、L1 批量确认和续费待决项 | 已同步 API 消费与 L1 批量确认限制 | 已同步状态/字段约束、批量确认契约与实施队列 | 已同步 |
| 2026-08-06 | V2.2 人机协同家庭管家 | 定义“AI 与你一起管理”、五 Tab 新语义、三级风险确认、家庭/知识/管家动态模型和设备健康边界 | 已同步页面、数据层、组件、测试与 F12-F16 计划 | 已同步迁移、服务、API、权限设计与 B9-B14 队列 | 已同步 |
| 2026-08-04 | V1 总体设计基线 | 建立产品定位、V1 边界和协作机制 | 已建立实施文档 | 已建立实施文档 | 已同步 |
| 2026-08-04 | Flutter UI 设计基线 | 纳入五 Tab、Nexus Dark Glass、Dashboard、Expert Run、家庭与待办规范 | 已同步 Flutter 页面/规范/实现路径 | 已同步 API 对应流程要求 | 已同步 |
| 2026-08-04 | Expert + SmartHome 数据模型 | 明确版本化 Expert、Skill 授权、Run 审计、Connector 实例、设备能力/状态与场景边界 | 已同步 Run 事件与领域模型要求 | 已同步表模型、权限、状态与接口约束 | 已同步 |
| 2026-08-04 | SmartHome Connector 技术架构 | 明确 HA 驱动层、Adapter 契约、本地优先 Zigbee/MQTT、厂商云适配及部署演进 | 已确认 App 只消费标准化家庭模型 | 已同步 Adapter、消息与服务边界 | 已同步 |
| 2026-08-04 | 通用 Connector Tool 与权限层 | 增加 Connector Tool、实例可用性、成员 Permission Grant、确认策略与 MCP 兼容契约；当前先完成 Framework + Mock，HA 为第二阶段首个真实接入 | 现有连接页继续展示授权、可用性与确认状态 | 已同步数据模型、服务边界与接口规划 | 已同步 |
| 2026-08-04 | Personal AI OS 定位升级 | 明确 AI Agent Runtime、Expert、Skill、Connector 为产品主架构；Smart Home 降为首个垂直 Connector，定义先 Framework、后 HA、再 NexusMind Hub 的路线 | 个人与家庭共用现有入口，后续按 Connector 扩展 | 已同步通用 Connector 服务边界 | 已同步 |
| 2026-08-04 | M5.4 SmartHome Connector Layer | 明确其作为 M5.2 并列模块，依赖 M5.1 + M5.3，产出 Expert 可调用的家庭设备能力 | 已同步家庭/Run 界面依赖 | 已同步 Connector 实施顺序 | 已同步 |
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

目标是从 Todo、Calendar、Expert Demo 升级为 AI Personal Assistant MVP。管家页重构为问候、天气/日程/任务状态、AI 建议和待办/能力/家庭入口；完成 Agent Runtime 最小编排、专家目录、详情、Run 执行和结果确认。首批专家为周计划、目标拆解、复盘和家庭管家。

完成 Connector Framework、Tool、权限、授权状态与 Run 记录；用 Todo、Calendar 等既有领域和 SmartHome Mock（客厅灯、空调、窗帘、温湿度）验证 `Flutter → ASP.NET API → Agent / Expert → Skill Engine → Connector → Mock` 的完整调用链，不在此阶段接入真实硬件。

#### 进入 Phase 1 的 Go / No-Go 标准

**Go：** 全链路能从 Flutter 创建 Run、生成可解释 Action、完成授权后的确认/审计，并在 Mock 不可用时安全降级；JWT 家庭隔离、幂等、脱敏和关键测试全部通过。**No-Go：** 任一跨家庭访问、重复执行、凭据/厂商字段泄露、L2/L3 绕过逐项确认，或核心场景只能依赖真实硬件才能演示时，停止进入 Phase 1，先修复并复审。

### Phase 1：第 2–3 月｜首个真实 Connector：Smart Home

目标是让用户首次感受到“AI 开始懂我的家”。通过 Home Assistant Connector 接入五类高价值设备：灯、空调、窗帘、门磁、温湿度。交付回家、离家、睡眠三个场景；例如“我要睡觉”经 Sleep Expert 和 SmartHome Skill 生成确认后的场景执行。

本阶段的技术落点为 `M5.4 SmartHome Connector Layer`：设备发现、标准能力模型、Connector 健康、权限、Run Action 确认、幂等执行和审计必须完整，不能以“直接控制设备”替代。

### Phase 2：第 4–6 月｜Personal + Family AI Assistant

这是首个可收费版本。App 形成管家、能力、待办、家庭、设置五栏；Dashboard 聚合个人任务/日程、家庭健康、天气、AI 建议和快捷场景，家庭中心展示“我的家”、空气/温度/安全与设备摘要。Calendar、Todo、Weather 等 Connector 与 Smart Home 使用同一权限、确认和审计链路。

商业验证以清晰的 C 端套餐和年服务为原则：基础场景、标准家庭服务、老人安心增值服务及 AI/云同步/远程访问/OTA 年费。具体价格、主机和安装成本必须先完成单位经济模型和试点验证，不在产品设计中承诺固定售价。

进入定价页设计前，必须定义年费续费钩子：权益状态、续费周期、宽限期、到期后的云端能力降级、续费提醒和支付结果回调；本地已授权的安全控制不得因订阅变化失去必要的人工控制能力。

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

它的唯一输出是：**Expert 可以通过受控 Skill 调用家庭设备能力。** 当前首批交付为 Mock Connector、工具目录、设备发现的模拟数据、标准能力模型、家庭空间/场景以及 Run Action 的确认/幂等执行/审计；第二阶段将以相同契约实现 Home Assistant Connector。米家、涂鸦和 Matter 仅保留 Adapter 扩展位。M5.4 不等同于“设备控制 API”，也不替代 M5.2 的同步、附件、iCal 和 Push。

## 10. V2.2 人机协同家庭管家定义

### 10.1 五 Tab 与核心体验

V2.2 将五个 Tab 从 AI 能力展示调整为人机协同入口。底部文案依次为“管家 / 能力 / 待办 / 家庭 / 设置”；Todo 与 Calendar 仍是待办的子能力，不能因导航调整丢失。

| Tab | V2.2 定位 | 页面首要内容 |
| --- | --- | --- |
| 管家 | 管家工作台：AI 做了什么、正在做什么、需要成员确认什么 | 待确认事项、管家动态、家庭概览、快捷入口 |
| 能力 | 管家能力中心：能交给 AI 管什么、已托管什么 | 已托管能力、可托管目录、托管开关、风险说明 |
| 待办 | 待办与日程：AI 整理的计划和需要确认的事项 | 待确认 / 任务 / 日历三段，按风险分组 |
| 家庭 | 家庭状态：空间状态和设备健康 | 空间摘要、离线/低电量/弱信号异常、动态入口 |
| 设置 | 家庭设置：成员、知识、偏好和连接 | 家庭成员、家庭知识库、管家偏好、连接管理 |

Dashboard 固定按 `Header → 待确认事项（优先）→ 管家动态（已执行）→ 家庭概览 → 快捷入口` 组织。管家消息必须展示风险、执行状态、依据和影响范围；不得将模型思考过程、供应商字段或设备实体 ID 暴露给用户。

### 10.2 风险与确认模型

| 等级 | 策略 | UI 表达 | 约束 |
| --- | --- | --- | --- |
| L1 低风险 | 可由已授权管家自动处理，或由成员批量确认 | 绿色，“已自动处理” | 仅可对 L1 执行批量确认 |
| L2 中风险 | 必须逐项确认 | 黄色，“建议确认” | 展示影响范围与建议操作 |
| L3 高风险 | 必须由成员逐项决定 | 红色，“需要你决定” | 不得自动执行或批量确认 |

**风险等级判定规则：**

**静态基线：** 由 Connector Tool 定义默认风险等级（`risk_level`）。Expert 不可覆盖 Tool 的默认等级，但可在 Expert Skill Permission 中声明更严格的等级（L1 → L2，不能反向降低）。

**用户覆盖：** 成员可在“设置 → 管家偏好”中为特定 Tool 或类别设置风险等级偏好，仅对本家庭成员生效。用户覆盖只能提高风险等级（L2 → L3），不能降低（L3 → L2）。

**上下文动态调整：** Runtime 可根据当前上下文（火警、离家模式、夜间）临时提升风险等级，但不得降低。上下文调整不持久化，仅对单次确认有效。

**最终等级：** 实际确认时取静态基线、用户偏好和上下文动态三者中的**最高等级**。

确认项必须具备标题、描述、影响范围、建议操作、状态、创建/过期时间和风险等级。确认、拒绝和批量确认均应可审计；确认请求使用幂等键，重复提交不得造成重复副作用。可逆的已执行动作以管家动态的撤销入口呈现，实际撤销前仍需进行实时权限与资源状态校验。

### 10.3 V2.2 家庭与管家领域模型

V2.2 在现有 Expert Run、Run Event、Run Action 与 Smart Home 模型之上新增以下家庭协同模型。所有记录由 JWT `tenant_id`（家庭）隔离，客户端不能指定或覆盖归属。

| 模型 / 表 | 用途 | 必要字段与约束 |
| --- | --- | --- |
| Family Member / `family_members` | 家庭成员与生命周期 | `home_id`、`name`、`relation`、`birthday`、`is_elderly`、`is_child`、`member_status`、`preferences`、软删除；状态机见 6.1 |
| Family Knowledge / `family_knowledge` | 家庭可维护事实，如物业、WiFi、维修和保险 | `category`、`key`、`value`、`notes`、`source_member_id`、`confidence_score`、`conflict_resolution_strategy` |
| Decision History / `decision_history` | 保留重要家庭决策的依据与替代方案 | 场景、决策、依据、替代方案、做出者、时间 |
| Steward Activity / `steward_activities` | 面向用户的管家动态，提供区别于 Run Event 的产品视角 | `run_id`、`sensing/planning/executing/reporting`、风险、状态、结果摘要、可撤销标识 |
| Confirmation Item / `confirmation_items` | 待确认的协同事项 | 活动关联、L1/L2/L3、影响、建议、确认/拒绝信息、过期时间 |
| Family Audit Log / `family_audit_logs` | 家庭域合规审计；与管家动态分离，面向长期合规与排障 | `home_id`、`actor_user_id`、`action`（`member_correction/member_terminal_restore/knowledge_write/knowledge_conflict_resolved/decision_record`）、`target_type`（`family_member/family_knowledge/decision_history`）、`target_id`、`before_json/after_json`、`reason`、`related_run_id`（与运行/确认链路同源） |

`smart_home_devices` 增加 `zigbee_role`、`battery_level`、`signal_lqi`、`health_status`，以支持家庭状态中的异常提醒；`expert_runs` 增加 `mode`（`single` / `steward`）和 `auto_confirm_policy`。B9 已提供它们及家庭/管家五表的数据库与实体基线；B10 已将设备健康和托管字段接入运行时；B11 已发布成员/知识/决策三领域服务并引入 `family_audit_logs` 作为与管家动态分离的合规审计载体，知识同 `knowledge_key` 在事务内按 `latest`/`authority`/`majority` 留痕。B12 已发布管家动态与确认中心 API：动态列表/详情/撤销（仅 `undoable=true` 已完成活动，写 `activity_undo` 审计）、确认项列表过滤、单项确认/拒绝（幂等键格式校验 + 状态去重重放）、L1 批量确认（`016` 迁移新增 `confirmation_batch_records` 幂等表，单事务预验证后原子确认，同键同集仅重放首次结果）；确认/拒绝/批量确认/撤销均写 `family_audit_logs`（`016` 同步扩展审计 CHECK）并生成可展示的管家动态；Dashboard 新增 `pendingConfirmations`/`stewardActivities` 模块，`homeSummary` 对应 `Home` 模块，`quickActions` 为前端静态入口。B13 已将设备边界收敛为 `IDeviceAdapter`/`IDeviceDiscovery`/`IDeviceCommandExecutor` 三契约 + `DeviceSyncService`/`CommandRelayService` 桥接，业务层不再感知 HA 实现；B14 已收敛家庭/管家权限（`family.read/write`、`steward.activity.read`、`confirmation.read/write`、`life.favorite.read/write`）并发布单设备健康详情（`GET /api/v1/smart-home/devices/{id}/health`），修复 B10/B11 遗留测试。这些字段只描述标准化设备健康和托管策略，不能把协议或厂商数据泄漏到业务层和 Flutter。

**Steward Activity 与 Run Event 的关系：** Run Event 是技术层审计记录，面向系统可靠性和问题排查，不可修改、不可删除。Steward Activity 是产品层管家动态，面向用户理解，可聚合、可摘要、可撤销。每次 Run Event 写入时可选择是否同步生成对应的 Steward Activity，默认生成 L2/L3 的 Activity，L1 可聚合后生成。Steward Activity 必须关联 `run_id`，确保用户可追溯到原始运行记录。

### 10.4 V2.2 设备适配边界

上层业务逻辑只依赖统一设备抽象，不能依赖 Home Assistant 的具体实现。Connector 层以 `IDeviceAdapter`、`IDeviceDiscovery`、`IDeviceCommandExecutor` 为稳定边界；Home Assistant 适配器实现该边界，直连 Zigbee、米家云和涂鸦云是后续适配器，不改变家庭、风险、确认或审计语义。状态同步和命令转发分别由桥接服务负责，所有命令仍通过确认、授权、幂等和审计链路。

### 10.5 V2.2 跨端契约

- Dashboard 聚合返回 `pendingConfirmations`、`stewardActivities`、`homeSummary` 和 `quickActions`，待确认事项在任何部分失败时仍应优先可见；
- 家庭成员、家庭知识库、管家动态和确认事项均使用家庭作用域路由；知识库支持按分类过滤，并根据冲突消解策略处理同 key；
- 设备列表返回标准化的 Zigbee 角色、电量、LQI 和健康状态，并提供设备健康查询；
- 确认 API 支持 `POST /api/v1/homes/{homeId}/confirmations/batch-confirm` 的 L1 批量确认：请求携带同一家庭的 `confirmationIds` 与 UUID `idempotencyKey`，服务端预校验每项均为未过期 `pending` L1 后原子确认；L2/L3、跨家庭、已终态或过期事项必须拒绝，不能部分绕过；管家动态支持游标分页、详情和可逆操作的撤销；
- 前端以 `ConfirmationCard`、`RiskBadge`、`StewardTimelineTile` 和 `NexusSurface.warning/danger` 表达该契约；风险色为 L1 `#0B8F55`、L2 `#F59E0B`、L3 `#EF4444`；
- 专家会话契约：`GET/POST /api/v1/conversations`、`GET/PUT/DELETE /api/v1/conversations/{id}`（列表/新建/重命名/软删除+审计）；`GET /api/v1/conversations/{id}/messages`（游标分页）与 `POST /api/v1/conversations/{id}/messages`（发送 → 创建关联会话的 Expert Run → 终态追加 assistant 消息，消息携带 `run_id` 可追溯）；`GET /api/v1/experts?scope=basic|mine|all` 区分平台基础专家与本人自建专家（`scope=mine` 仅返回 `owner_user_id=本人`，不泄露他人）；连接器选择复用 `GET /api/v1/connectors` 已授权实例，可用性受 Provider 发布节奏约束；会话读写 `conversation.read/write`、自建专家 `expert.mine.read/write`，跨用户/跨租户一律 404。
- **代码注释规范（自 B11 起强制执行）：** 所有 `public`/`internal` 实体类、ViewModel/DTO、Controller Action、Service 方法必须在声明前给出中文 `<summary>` 注释；Swagger 字段可通过 `[Description("中文")]` 或 `<remarks>` 补充描述。构建开启 `<GenerateDocumentationFile>true</GenerateDocumentationFile>` 并严格对待 `CS1591`（缺少 XML 注释的公开成员）。新增源码不得以 `#pragma warning disable CS1591` 等方式抑制。存量代码（V1/V2.1/V2.2）须在对应切片内一次性补齐。

### 10.6 V2.2 实施优先级

| 优先级 | 范围 | 交付 |
| --- | --- | --- |
| P0 | 后端迁移 | 新增家庭/管家五表，扩展设备健康和 Expert Run 托管字段（B9 已完成） |
| P1 | 后端 API | 家庭成员与知识库 CRUD、确认中心、Dashboard 聚合契约（B11/B12 已完成） |
| P2 | 领域边界 | 设备 Adapter 抽象、家庭/管家 Repository 与 DTO（B13 已完成；B14 权限收敛与单设备健康详情已完成） |
| P3 | 前端页面 | 管家工作台、确认中心、家庭成员和知识库页面（前端 P2-P4 进行中，后端契约已发布） |
| P4 | 前端交互 | 能力托管、设备异常、风险组件与主题（前端 P5 进行中） |
| P5 | 质量门禁 | 新增页面 Widget 测试和既有 Dashboard/能力页回归测试（前端 P6） |

V2.3 个人生活专家后端切片（B15 收藏基线、B16 注册与翻牌、B17 行程规划与日历同步）已全部完成；前端 P5b（收藏管理）与 P5c（翻牌与行程）交付项已列入前端开发计划表。

## 11. 当前待决事项

- 确定 Phase 1 后进入 Connector Catalog 的优先级、OAuth/授权方式、数据最小化范围与用户价值；
- 第二阶段实施前明确 Home Assistant 的认证方式、实体发现和设备能力标准化范围；
- 定义家庭、空间、成员与现有租户模型的对应关系；
- 确定 AI Runtime 的模型供应商、成本预算、上下文隔离和降级策略；
- 确定家庭与待办首版页面的最小数据结构和交互原型；
- 为高风险动作定义确认级别、幂等键、超时和失败重试规则。
- 确定家庭成员生命周期状态机的管理权限、终态更正流程和数据保留期限；
- 定义年费续费钩子，包括权益、宽限期、到期降级、提醒和支付回调边界；
- 确定推送聚合策略的具体窗口、摘要频率、成员偏好与 L2/L3 升级规则；
- 确定个人生活专家的 OCR 截图识别能力进入后续版本的具体排期与第三方依赖（短视频生成已排期 V2.5 快速剪辑，见 §7.1）。
- 确定可画、飞书、钉钉等 Productivity/Future Connector Provider 的接入排期（影响专家对话框的可选连接器列表）。（小红书已作为首个内容发布类个人级 Connector 落地：B26 授权与搜索、B27 发布已完成，见 §12。）

## 12. V2.4 家庭与个人连接器、Web 治理

V2.4 固化“每个成员独立登录、家庭共享、个人授权隔离”，并新增 PC Web 用户端与开发端。它不改变 AI 先建议后执行、Run Action、确认、幂等和审计边界。

| 范围 | V2.4 交付 |
| --- | --- |
| 账户与成员 | 复用 `users`、`user_identities`、`tenants`、`tenant_members`；提供当前成员资料、成员/角色查看与 owner/admin 受控管理。邀请/加入流程需先发布 API。 |
| 家庭级 Connector | Web 开发端创建、测试、发现、同步、健康查看和成员 Permission Grant；移动端/Web 用户端仅显示已授权状态与能力。 |
| 个人级 Connector | 增加 `binding_scope`、`owner_user_id`、OAuth 授权会话和服务端回调；首批从已确认的邮箱、日历或内容发布 Provider 中选择，成员仅管理本人授权。小红书（xhs）已作为首个内容发布类个人级 Connector 落地（B26 授权与搜索、B27 发布）：扫码登录（非 OAuth），经本地 stdio MCP 调用，凭据由本机 MCP 进程管理，`credential_ref` 仅存 `local://xhs-sessions/{uuid}` 会话标识。 |
| Web | Vue 2 + Element UI 的用户端和开发端，共用 API/JWT；路由和菜单按服务端权限码 + 已发布 `route_key` 显隐/排序，开发端仅 owner/admin；成员/邀请/owner 转让/Web 导航偏好随 B19 已发布。 |
| 角色/路由治理 | 固定四角色，不新建可编辑 RBAC 或 API 路由表；菜单偏好已发布（`web_navigation_preferences`，角色粒度），仅管理已发布 Web `route_key`（`NexusWebNavigationKeys` 8 个）的显隐与排序，owner/admin 写入。 |

### 12.1 跨端 API 规则

- 现有家庭级 Connector 路由继续使用 `/api/v1/connector-providers`、`/connectors`、`/connectors/{id}/test|discovery|sync|authorization`；响应增加 `bindingScope`。个人实例只向 owner 返回本人归属摘要，不得向其他成员暴露 `owner_user_id`。
- 个人 OAuth 新增授权发起、服务端 callback、状态、撤销路由。前端不能接收、保存或记录授权 code、access token 或 refresh token；新增路由先同步 API 文档的字段、错误、幂等和回调契约。
- 移动端仅提供“我的连接”的个人授权/撤销和家庭连接状态；Web 用户端提供同等用户能力；Web 开发端承载家庭级配置与成员授权。

### 12.2 实施门禁

1. 先完成产品、迁移、实体、服务、权限快照、OAuth 安全审计与 API 文档，再实现 Flutter 或 Vue HTTP Repository；
2. 迁移验证 `personal` 实例 owner 属于同一 active `tenant_member`，`household` 实例 owner 为空，跨家庭或跨成员访问统一返回 404；
3. 个人 OAuth 断开后立即撤销 `credential_ref` 可用性、取消刷新任务并写审计；历史业务数据按 Provider 的数据保留策略处理；
4. Web 与移动端测试覆盖 owner/admin/member/viewer、个人与家庭实例隔离、授权过期/撤销、路由权限、L1/L2/L3 确认和凭据不泄露；成员/邀请/owner 转让（B19）测试覆盖最后一名 active owner 守恒、跨家庭 404、乐观锁 409、导航偏好白名单拒绝；
5. `HomeMind.CreatorMcp` 始终独立于产品 Connector 目录和 App/Web 数据流。
