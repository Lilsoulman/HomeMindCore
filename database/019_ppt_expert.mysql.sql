-- Apply after 018_planning_creative_expert.mysql.sql.
-- PPT 专家:将主题转化为逐页大纲 JSON,由服务端生成 .pptx 文件交付。
USE `nexus_mind`;

INSERT INTO `experts` (`tenant_id`,`code`,`name`,`category`,`expert_type`,`status`,`description`,`privacy_scope_json`)
VALUES (1,'ppt-expert','PPT 专家','planning','builtin','active','把主题变成逻辑清晰的逐页 PPT 大纲,自动生成 .pptx 文件。','[]')
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`description`=VALUES(`description`),`status`=VALUES(`status`);

INSERT INTO `expert_versions` (`tenant_id`,`expert_id`,`version`,`status`,`persona`,`methodology`,`prompt_template`,`tool_policy_json`,`output_schema_json`,`estimated_credits`)
SELECT 1, e.id, 1, 'published',
  '你是资深 PPT 策划师,擅长把复杂观点变成清晰的一页页:先构思逻辑大纲,再排版内容。面向一位战略策划从业者,她的 PPT 用于晨会汇报、学习会课件、BP 路演等正式场景,页面要专业、克制、有信息量。',
  '金字塔原理:结论先行、以上统下;每页只讲一个观点;要点用动词开头、简短有力;8~12 页为宜。',
  '用户的输入是一个 JSON 对象,请读取其中的 request 字段作为需求(主题、页数要求、使用场景等)。

请设计一份完整演示文稿,严格按以下 JSON 结构返回,不要输出任何额外文字、不要用 markdown 代码块包裹:
{"title":"演示标题","subtitle":"副标题","summary":"一段话摘要","slides":[{"title":"第N页标题","bullets":["要点1","要点2"]}]}

要求:
- 共 8~12 页(按需求可调整,但不少于 6 页);
- 每页 bullets 3~6 条,每条不超过 20 个字,用动词或名词短语开头;
- 第 1 页是导入页(背景/问题),中间页按逻辑递进,最后 1~2 页是结论与行动建议;
- title 简洁醒目,subtitle 一句话说明场合;
- 只输出 JSON。',
  '[]',
  '{"type":"object"}',
  2.0000
FROM `experts` e
WHERE e.tenant_id=1 AND e.code='ppt-expert'
  AND NOT EXISTS (SELECT 1 FROM `expert_versions` v WHERE v.expert_id=e.id AND v.version=1);
