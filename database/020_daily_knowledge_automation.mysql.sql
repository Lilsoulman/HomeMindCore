-- Apply after 019_ppt_expert.mysql.sql.
-- 每日知识管家:知识条目表(芸和中医预置)+ 知识管家专家 + 工作日早晨自动推送规则(时间可由用户修改)。
USE `nexus_mind`;

CREATE TABLE IF NOT EXISTS `knowledge_items` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `category` VARCHAR(32) NOT NULL DEFAULT 'general',
  `title` VARCHAR(128) NOT NULL,
  `content` TEXT NOT NULL,
  `source` VARCHAR(128) NULL,
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  `created_by_user_id` BIGINT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  KEY `idx_knowledge_items_tenant` (`tenant_id`,`is_active`,`category`),
  CONSTRAINT `fk_knowledge_items_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_knowledge_items_creator` FOREIGN KEY (`created_by_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB;

-- 芸和中医养生预置知识(基础占位,后续由用户扩充为"芸和"内部体系数据)。
INSERT INTO `knowledge_items` (`tenant_id`,`category`,`title`,`content`,`source`,`created_by_user_id`)
SELECT 1,'yunhe_tcm','四时养生:春养肝','中医认为春天肝气当令,宜夜卧早起、多户外踏青以疏肝解郁;饮食上少酸多甘(如山药、红枣),忌暴怒伤肝。晨起一杯温水、敲打大腿内侧肝经各 30 下,帮助一天清醒。', '芸和中医基础', NULL
WHERE NOT EXISTS (SELECT 1 FROM `knowledge_items` WHERE `tenant_id`=1 AND `category`='yunhe_tcm' AND `title`='四时养生:春养肝');

INSERT INTO `knowledge_items` (`tenant_id`,`category`,`title`,`content`,`source`,`created_by_user_id`)
SELECT 1,'yunhe_tcm','子午觉与阳气','午时(11:00-13:00)心经当令,小憩 15-30 分钟养心气,比多睡一小时更有用;子时(23:00 前)入睡养胆气,是恢复精力的黄金时间。熬夜后第二天午休补觉,比晚上补觉更符合节律。', '芸和中医基础', NULL
WHERE NOT EXISTS (SELECT 1 FROM `knowledge_items` WHERE `tenant_id`=1 AND `category`='yunhe_tcm' AND `title`='子午觉与阳气');

INSERT INTO `knowledge_items` (`tenant_id`,`category`,`title`,`content`,`source`,`created_by_user_id`)
SELECT 1,'yunhe_tcm','久坐护颈三招','办公久坐每 45 分钟起身活动 3 分钟:1)耸肩-扩胸-放松 10 次;2)缓慢转头画米字 5 圈;3)双手搓热后按揉风池穴(后脑勺发际线凹陷处)1 分钟。这三招能明显缓解颈肩僵硬,比按摩仪更即时。', '芸和中医基础', NULL
WHERE NOT EXISTS (SELECT 1 FROM `knowledge_items` WHERE `tenant_id`=1 AND `category`='yunhe_tcm' AND `title`='久坐护颈三招');

INSERT INTO `experts` (`tenant_id`,`code`,`name`,`category`,`expert_type`,`status`,`description`,`privacy_scope_json`)
VALUES (1,'daily-knowledge-steward','每日知识管家','learning','builtin','active','每天带来一条新知识:优先从本地知识库取用,也可按需自由生成。','[]')
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`description`=VALUES(`description`),`status`=VALUES(`status`);

INSERT INTO `expert_versions` (`tenant_id`,`expert_id`,`version`,`status`,`persona`,`methodology`,`prompt_template`,`tool_policy_json`,`output_schema_json`,`estimated_credits`)
SELECT 1, e.id, 1, 'published',
  '你是家庭知识管家,为一位战略策划从业者每天带来一条新知识。她需要每天有新知识获取,同时用知识服务晨会与学习会。内容要专业但通俗,今天就能用上。',
  '每日一条,宁精勿滥;先给结论,再给为什么,最后给一个可执行的行动提示;内容 300 字以内。',
  '用户的输入是一个 JSON 对象(可能为空)。

- 若输入中包含 knowledgeItem 对象(title 与 content),请以该条目为主题,加工成一张完整的知识卡片;
- 若输入为空,请围绕商业、战略、管理、心理、效率或创意领域自由生成一条当日知识。

严格按以下 JSON 结构返回,不要输出任何额外文字、不要用 markdown 代码块包裹:
{"date":"YYYY-MM-DD","topic":"知识点标题","category":"领域","content":"300 字以内的知识正文","source":"来源或出处","whyItMatters":"为什么对她有价值","actionTip":"今天可以做什么"}

要求:topic 简洁醒目;content 控制在 300 字以内、有干货;actionTip 必须是一条她今天就能做的事。',
  '[]',
  '{"type":"object"}',
  0.8000
FROM `experts` e
WHERE e.tenant_id=1 AND e.code='daily-knowledge-steward'
  AND NOT EXISTS (SELECT 1 FROM `expert_versions` v WHERE v.expert_id=e.id AND v.version=1);

-- 工作日早晨 08:30 知识推送规则(Asia/Shanghai);时间与星期可由用户在设置中修改。
INSERT INTO `automation_rules` (`tenant_id`,`owner_user_id`,`name`,`trigger_type`,`trigger_config_json`,`conditions_json`,`actions_json`,`approval_policy`,`enabled`)
SELECT 1, u.id, '工作日早晨知识推送', 'time_schedule',
  JSON_OBJECT('kind','fixed_time','time','08:30','daysOfWeek',JSON_ARRAY(1,2,3,4,5),'timeZone','Asia/Shanghai'),
  JSON_ARRAY(),
  JSON_ARRAY(JSON_OBJECT('type','agent_run','expertCode','daily-knowledge-steward')),
  'auto_execute', 1
FROM `users` u
WHERE u.id = (SELECT MIN(id) FROM `users`)
  AND NOT EXISTS (SELECT 1 FROM `automation_rules` WHERE `tenant_id`=1 AND `name`='工作日早晨知识推送');
