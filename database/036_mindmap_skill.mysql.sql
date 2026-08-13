-- Apply after 035_xhs_content_creator_expert.mysql.sql.
-- B33 思维导图 Skill：仅登记平台级目录项；运行与审计复用既有 AgentRun/skill_run_created 链路。
USE `nexus_mind`;

INSERT INTO `skills` (`tenant_id`,`key`,`name`,`category`,`description`,`input_schema_json`,`output_schema_json`,`required_permission`,`risk_level`,`status`)
VALUES (1,'mindmap','生成思维导图','productivity','将 markdown 交由浏览器端转换为可交互的思维导图；服务端仅记录运行与展示安全摘要。','{"type":"object","required":["markdown"],"properties":{"markdown":{"type":"string","maxLength":100000,"description":"待转换的 Markdown 文本"}}}','{"type":"object","properties":{"character_count":{"type":"integer"},"first_heading":{"type":["string","null"]}}}','mindmap.read','L1','active')
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`category`=VALUES(`category`),`description`=VALUES(`description`),`input_schema_json`=VALUES(`input_schema_json`),`output_schema_json`=VALUES(`output_schema_json`),`required_permission`=VALUES(`required_permission`),`risk_level`=VALUES(`risk_level`),`status`=VALUES(`status`);
