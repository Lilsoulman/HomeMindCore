# HomeMind 文档

本目录只保留当前产品、跨端开发和运行所需的信息。接口实现以 Swagger 和控制器为准；本文档说明稳定边界、协作方式和接入规则，不重复粘贴代码。

| 文档 | 用途 | 读者 |
| --- | --- | --- |
| [产品总设计](product.md) | 定位/卖点、管家矩阵、方向共识、功能卡、原则与角色（全产品唯一产品事实源） | 产品、设计、全体研发 |
| [开发计划](development-plan.md) | 产品设计驱动的 AI 编码顺序、改动位置和验收 | 研发、项目负责人、编码代理 |
| [API 接入](api-integration.md) | 统一契约、端到端流程、接口索引 | 移动端、Web、后端 |
| [家庭财务功能卡](family-finance.md) | 产品边界与执行计划入口 | Core、移动端、Web、编码代理 |
| [运行部署](operations.md) | 本地运行、生产配置与发布检查 | 后端、运维 |
| [创作者中心 MCP](mcp-creator-center.md) | 本地 MCP Bridge 的安全和运行说明 | Connector 开发者 |

三端文档索引：Web 端见 `D:\HomeMind\admin\docs\README.md`，移动端见 `D:\HomeMind\mobile\docs\README.md`。每次沟通确认后的方向调整：先更新产品总设计，再按受影响范围同步三端开发计划与接入文档。

## 信息源优先级

1. 运行中的 Swagger：`/swagger`；请求体、响应 DTO、必填字段以它为准。
2. 后端控制器及其 ViewModel：实现与权限的最终事实来源。
3. 本目录：产品边界、跨端约定、工作计划和接入流程。

新增功能时，先更新产品卡，再在[开发计划](development-plan.md)生成可执行任务并按项编码、验证；接口变化必须同步 Swagger 与 [API 接入](api-integration.md)。不要创建按版本堆叠的“总设计”“实现记录”或静态接口副本。

## Home Assistant 接入（硬件到货前）

HomeMind 以 Home Assistant（HA）作为家庭设备网关；Zigbee2MQTT、ZHA 和其他 HA 集成只向 HA 接入，HomeMind 不直接配对 Zigbee 设备或保存厂商凭据。当前目标拓扑为“Zigbee 设备 → Zigbee2MQTT → HA → HomeMind Connector → 家庭上下文与确认流”。

### 已采用的官方接入方式

- **REST**：以 `Authorization: Bearer <long-lived access token>` 调用 `GET /api/` 做健康检查、`GET /api/states` 做全量发现、`GET /api/states/{entity_id}` 做命令后的状态回读，以及 `POST /api/services/{domain}/{service}` 下发已确认的设备动作。
- **WebSocket**：连接 `ws(s)://<ha-host>/api/websocket` 后依次处理 `auth_required`、`auth`、`auth_ok`，再用 `subscribe_events` 订阅 `state_changed`；只接收白名单实体域或显式实体清单。
- **凭据**：在 HA 为 HomeMind 建立专用用户并创建 Long-Lived Access Token；令牌只写入当前家庭的 Secret Vault 条目，结构为 `baseUrl` 和 `accessToken`。令牌、`credentialRef` 和 HA 原始状态均不返回客户端、不写日志。

官方依据：[REST API](https://developers.home-assistant.io/docs/api/rest/)、[WebSocket API](https://developers.home-assistant.io/docs/api/websocket/)、[认证与长期访问令牌](https://www.home-assistant.io/docs/authentication/)。

### 到货联调清单

1. 在 N100 的 Docker Desktop 中启动 HA、MQTT Broker 与 Zigbee2MQTT；把 EFR32MG21 协调器交给 Zigbee2MQTT，确认设备已在 HA 中生成实体。
2. 在 HA 中按房间/区域命名实体，并先只暴露 `light`、`switch`、`climate`、`cover`、`sensor`、`binary_sensor` 六类实体；摄像头、门锁和告警器默认不接入自动执行范围。
3. 为专用 HA 用户创建长期访问令牌，在 Vault 写入 `vault://tenants/{tenantId}/home-assistant/<instance>`；不要把令牌放进 `appsettings.json`、客户端、数据库或聊天记录。
4. 通过 Swagger 创建 `home_assistant` 家庭共享连接器，先调用 `POST /api/v1/connectors/{id}/test`，成功后调用 `POST /api/v1/connectors/{id}/discovery`；确认设备、能力和健康状态已归一化落库。
5. 先保留 `EventSubscriptionEnabled=false` 完成 REST 联调；验证状态和控制命令后，再开启 WebSocket 订阅并配置 `WatchDomains`、`WatchEntities`、`IgnoreEntities`。

联调失败必须呈现真实失败：`401/403` 表示 HA 令牌或权限问题，`502` 表示 HA 端点不可达或协议不兼容，`503` 表示 Vault 不可用。不得以模拟设备替代真实结果。
