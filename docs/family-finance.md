# 家庭财务功能卡

产品需求见[产品说明](product.md#家庭财务产品卡)，AI 编码顺序、改动位置和验收命令见[开发计划 P3-F](development-plan.md#p3-f家庭财务执行计划)。本页只保留开发时需要快速确认的边界：

- Core 的事实来源是本地解析账单与已完成缴费登记；不接入银行、支付平台或自动付款。
- 所有 API 都以 JWT 的家庭归属校验 `{homeId}`，并要求 `finance.read` 或 `finance.write`；`viewer` 只读。
- 请求 JSON 是 camelCase；成功响应使用 `Code`、`Msg`、`Data`，且 `Data` 字段为 PascalCase。
- 本地 OCR 只提交结构化字段；不保存或传输原始账单、票据、账户号码和凭据。
- 财务建议、缴费提醒只生成确认中心 L1 卡片；不代表已执行，也不是系统推送。

接口字段与示例以 Swagger 和 `HomeMind.Api/Controllers/Finance` 为准；跨端请求层规则见[API 接入](api-integration.md)。
