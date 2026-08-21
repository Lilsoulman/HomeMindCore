# HomeMind 跨端设计与开发计划

## 目标与原则

以同一 API 契约交付移动端、Web 和后端。后端先确定 DTO、权限、状态机和 Swagger；两端只消费版本化 API，不复制业务判断或直接访问数据库。

## 交付分期

| 阶段 | 后端 | 移动端 | Web | 验收 |
| --- | --- | --- | --- | --- |
| P0 基础 | 认证、用户上下文、错误封装、Swagger | 登录、令牌存储、请求层 | 登录、会话恢复、请求层 | 注册→登录→刷新→`me` |
| P1 个人效率 | 日历、待办、收藏、会话 | 今日、日历、待办、收藏 | 列表、筛选、详情编辑 | CRUD、分页、空态与 401 刷新 |
| P2 家庭协同 | 成员、邀请、知识、确认、Dashboard | 家庭首页、提醒、确认 | 成员/邀请/设置管理 | 换角色、跨家庭与确认回写 |
| P3 管家能力 | 财务、缴费、快递、宠物、家庭日程 | 记录、提醒、概览 | 导入、趋势、管理台 | 数据隔离、敏感字段不回传 |
| P4 AI 与连接器 | 专家/运行/技能、设备、场景、自动化 | 对话、运行状态、确认卡 | 专家治理、Connector 配置 | 异步状态、失败呈现、确认后执行 |

P0–P2 是可用基线；P3–P4 按领域独立上线。未通过验收不得以客户端 mock 替代服务端真实状态。

## 页面与 API 归属

| 页面/能力 | 移动端重点 | Web 重点 | 后端资源 |
| --- | --- | --- | --- |
| 登录与个人设置 | 登录、刷新、个人资料 | 登录、AI 配置 | `auth`、`ai/config` |
| 今日与效率 | 今日卡片、日历、待办 | 日历/待办批量管理 | `dashboard`、`calendar`、`todos` |
| 家庭 | 提醒、成员概览、确认 | 成员、邀请、权限治理 | `homes`、`invitations`、`confirmations` |
| 家庭管家 | 快速记录、提醒处理 | 导入、账单/趋势、档案管理 | `finance`、`billing`、`courier`、`pets`、`schedule` |
| AI 与智能家居 | 对话、运行进度、场景执行 | 专家/技能/Connector 治理 | `experts`、`expert-runs`、`skills`、`smart-home` |

## AI 执行协议

AI 的输入是[产品说明](product.md)中的功能卡，输出和执行顺序固定为：

1. 将一个产品场景拆成按依赖排序的 Core、Web、移动端任务；每项必须列出改动目录、API/状态影响、完成标准和验证命令。
2. 先核对当前仓库是否已有对应实现与测试；已有能力标为“验证/修正”，不重复造接口或模型。
3. 按计划逐项写代码：先 Core 契约和测试，再 Web/移动端请求层与页面状态；每完成一项就运行其最小验证。
4. 只有本项验收通过才进入下一项。遇到 API 缺口时回到 Core 任务补契约、权限、迁移和 Swagger，而不是让客户端 mock。
5. 最终只更新受影响的产品卡、计划状态和 API 索引；不要输出替代代码的长篇设计文档。

任务状态使用 `待做`、`进行中`、`验证/修正`、`完成`。计划是 AI 的编码待办，不是历史记录。

## 下一步（当前开发焦点）

> Codex/Agent 收到「按照下一步计划进行开发」时：读取本节，选依赖已满足的首个 `待做` 任务；若该切片尚未展开详细任务表，**先在本文档按下方「P3-F 家庭财务执行计划」的表格格式生成该切片的编码任务表**（ID/状态/依赖/AI 编码任务/改动位置/完成标准与验证），再按表逐项开发、验证、回写。禁止跳过计划直接写代码。

| 顺序 | 切片 | 领域 | 状态 | 说明 |
| --- | --- | --- | --- | --- |
| 0 | HA-1 | Home Assistant 硬件联调准备 | 进行中（H1-H2 完成，等待真实硬件） | 用户硬件即将到货，提前于 P3 队列实施；任务表见下方 P4-HA |
| 1 | B43 | 缴费管家 | 进行中（F1 已完成，其余待做） | 任务表见下方 P3-F；继续 F2-F5 |
| 2 | B44 | 快递管家 | 待做（未展开） | 收到指令后先在本文档展开任务表再开发 |
| 3 | B45 | 宠物管家 | 待做（未展开） | 同上 |
| 4 | B46 | 家庭日程协同 | 待做（未展开） | 同上 |
| 5 | B47 | 家庭回忆管家 | 待做（未展开） | 依赖剪辑四引擎验收 |
| 6 | B48-B49 | 健康画像 | 待做（未展开） | 依赖 OCR 链路 |
| 7 | B50 | 出游管家 | 待做（未展开） | 依赖 MTR-1a/MTR-1b |

## P3-F：家庭财务执行计划

产品输入：[家庭财务产品卡](product.md#家庭财务产品卡)。Core 现有的财务、缴费控制器和服务已在工作区中，首先执行 F1 的契约核对；不要重建相同接口。

| ID | 状态 | 依赖 | AI 编码任务 | 改动位置 | 完成标准与验证 |
| --- | --- | --- | --- | --- | --- |
| F1 | 完成 | P0 认证、家庭权限 | 核对账单/缴费实体、迁移、服务、控制器、`finance.read/write` 与 `{homeId}` 租户校验；补任何缺失的服务测试或 Swagger 注释 | `HomeMind.Common.Model`、`HomeMind.Business.*`、`HomeMind.Api`、`database` | CSV 重复导入不重复写入；缴费记录关联一条财务流水；跨家庭 `403`、重复缴费 `409`；运行 `dotnet test --filter FullyQualifiedName~FinanceServicesTests\|FullyQualifiedName~BillingServicesTests` |
| F2 | 待做 | F1 | 固化财务 API 客户端：Bearer、一次刷新重放、`Code/Msg/Data` 解包、PascalCase 响应类型和统一错误映射 | Web/移动端的 API client 与共享类型目录 | 所有财务请求从会话取得 `homeId`；`401/403/409/422/5xx` 可被页面状态消费；以 Swagger 或集成测试验证请求/响应 |
| F3 | 待做 | F2 | 实现 Web 财务工作台：本地 CSV 预览与导入、日期/分类流水、汇总、缴费账户、已缴登记、提醒入口和年度趋势 | Web 财务页面、状态管理、组件测试 | 导入后刷新流水和汇总；写操作禁用重复点击；`409` 刷新账户；不展示敏感数据或自动支付按钮；运行 Web 的定向测试与生产构建 |
| F4 | 待做 | F2 | 实现移动端快速财务流：安全会话、CSV 文件预览、本地 OCR 结构化建档/登记、提醒与确认中心跳转、离线草稿 | 移动端财务页、存储、网络层、组件/端到端测试 | 不上传图片/原始票据；超时后先刷新再允许重试；离线仅保存草稿且恢复网络后需用户确认；运行移动端定向测试与构建 |
| F5 | 待做 | F3、F4 | 以同一家庭的 member 与 viewer 做端到端回归，补缺失自动化测试并修正发现的问题 | 对应三端测试目录与最小必要代码 | member 完整走通；viewer 只读；跨家庭拒绝；重复导入/缴费正确；建议和提醒不重复创建确认卡 |

### F1 已知 API 契约

- 根路径为 `/api/v1/homes/{homeId}`：`finance/transactions/import`、`finance/transactions`、`finance/summary`、`billing/accounts`、`billing/accounts/{accountId}/payments`、`billing/reminders`、`billing/trend`。
- 导入请求是 JSON 内的 CSV 文本，不是文件上传；缴费接口只登记已完成付款。完整字段以 Swagger 和控制器/ViewModel 为准。
- `summary` 与 `reminders` 会幂等创建确认中心 L1 卡片，客户端只展示或跳转确认中心；不得执行付款。

## P4-HA：Home Assistant Connector 提前执行计划

产品输入：[产品总设计中的硬件策略](product.md#硬件策略感知层优先)。本切片只打通“HA 是设备网关”的安全连接、发现、状态同步与确认后控制；不直接管理 Zigbee 配网、不保存厂商令牌、不把门锁/摄像头纳入自动执行。

| ID | 状态 | 依赖 | AI 编码任务 | 改动位置 | 完成标准与验证 |
| --- | --- | --- | --- | --- | --- |
| H1 | 完成 | 无 | 依据 HA 官方 REST、WebSocket 与认证文档核对既有 Connector：健康检查、全量状态发现、服务调用、`state_changed` 订阅和 Vault 凭据边界；把到货联调步骤写入文档 | `docs/README.md`、`docs/product.md`、本表 | 使用官方协议路径；明确 Long-Lived Access Token 仅存 Vault、WebSocket 先关闭后开启；不新增客户端 mock |
| H2 | 完成 | H1、现有 SmartHome 读模型 | 已补齐 REST Adapter 单设备状态回读与命令后的回读；事件订阅已统一写入标准化状态，并修正连接器引用的租户/连接器主键顺序；已添加定向测试 | `HomeMind.Business.Services/Connectors/Adapters/HomeAssistantAdapter.cs`、`HomeMind.Business.Services/Connectors/Bridge/HomeAssistantEventSubscriber.cs`、`HomeMind.Business.Services.Tests` | `GET /api/states/{entity_id}` 能返回归一化状态；成功命令尽力回读但不伪造状态；订阅状态与轮询同一 JSON 形状；已通过 `dotnet test --filter FullyQualifiedName~HomeAssistant\|FullyQualifiedName~DeviceSync` |
| H3 | 待做 | H2、真实 HA 与 Zigbee2MQTT 可用 | 以真实 HA 实体注册表核对区域、设备和实体关系；只同步受支持域，记录未映射实体数；补齐 HA 区域到 HomeMind 空间的归一化策略 | Adapter、设备同步服务、必要的测试与迁移 | 真实 HA 中不同区域的灯、插座、温湿度、门窗磁均能发现；同一实体重复发现不重复建档；未支持域明确跳过 |
| H4 | 待做 | H2、H3 | 启用 WebSocket 事件订阅的可观测性与断线重订阅：鉴权失败、服务重启、事件去重、白名单过滤、关闭配置均可验证 | Event Worker、Subscriber、测试、运行文档 | HA 重启后自动恢复；相同状态不重复触发自动化；`IgnoreEntities` 优先；关闭开关时不建长连接且 REST 同步仍可用 |
| H5 | 待做 | H3、H4、实际硬件 | 执行到货验收：协调器、Zigbee2MQTT、HA、Vault、Connector、发现、状态变更、确认后控制和权限隔离完整走通 | 运行环境、Swagger、定向回归记录 | 用真实设备通过 test → discovery → 状态变化 → 确认后服务调用；跨家庭 `404`、无权限 `403`、HA/Vault 不可用 `502/503`；不向任何响应或日志泄露令牌 |

### H1 协议与启用顺序

1. HA REST 默认端口通常为 `8123`（以实际 HA 安装配置为准），先以 `GET /api/` 校验 Bearer Token，再通过 `GET /api/states` 发现受支持实体。
2. 控制仅使用 `POST /api/services/{domain}/{service}`，且必须复用 HomeMind 的确认、授权、幂等和审计链路；HA 服务调用成功不等于状态已更新，因此 H2 必须做状态回读。
3. WebSocket 连接路径为 `/api/websocket`，协议顺序固定为 `auth_required → auth → auth_ok → subscribe_events(state_changed)`；任何鉴权或订阅失败必须断开并按 Worker 退避重试。
4. 先以 REST 完成真实设备发现和控制，之后才把 `EventSubscriptionEnabled` 打开；实体白名单优先于域白名单，排除列表优先级最高。

## 每个功能的交付顺序

1. 从产品卡提取用户目标、数据归属、风险等级、成功/失败体验，并创建上表格式的编码任务。
2. Core 定义或核对请求/响应 DTO、权限、幂等规则、状态转换，并在 Swagger 可调用。
3. 移动端与 Web 共用接口类型或从 OpenAPI 生成类型；先完成请求层与状态机，再做页面。
4. 三端用同一测试账号完成正常、无权限、无数据、网络失败和重复提交验证。
5. 将通过的任务标为完成；仅同步本目录中受影响的产品卡、计划和 API 索引。

## 设计约束

- 时间统一提交 ISO 8601 UTC；展示层按设备时区格式化。
- 列表使用服务端的 `limit`、`cursor` 和过滤参数，不在客户端假设全量数据。
- 可产生副作用的按钮在请求期间禁用；具备 `idempotencyKey` 的接口必须为一次用户意图复用同一 UUID。
- 运行类资源以服务端 `status` 为准：创建后轮询详情/事件，终态停止轮询。
- 两端均实现错误映射：401 刷新令牌后仅重放一次；403 提示无权限；409 刷新资源后提示冲突；422 展示字段/业务错误；5xx 提示稍后重试。

## 上线门禁

- Swagger、权限策略、DTO 和数据库迁移一致。
- 每个写接口都具备服务端授权和家庭/用户归属校验。
- Web 与移动端均覆盖加载、空、错误、无权限和离线恢复界面。
- 外部 Connector、MCP、渲染或数据库不可用时，页面明确显示失败，不将任务标为成功。
