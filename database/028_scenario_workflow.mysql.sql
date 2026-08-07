-- Apply after 027_expert_self_serve.mysql.sql.
-- B22 scenario workflow (第七阶段):
--   scenario_templates  平台级场景模板（tenant_id 固定 1，与平台专家同惯例）
--   scenario_instances  家庭启用后的实例（Device Resolver 解析 device_id / step_status）
-- 执行、确认、幂等与审计全部复用 AgentRun / ExpertRunAction / ActionExecutionAudits，
-- 不新增 Step 表、不修改既有表；本迁移仅建两张表并写入三个种子模板。
USE `nexus_mind`;

CREATE TABLE `scenario_templates` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL DEFAULT 1,
  `code` VARCHAR(64) NOT NULL,
  `name` VARCHAR(50) NOT NULL,
  `summary` VARCHAR(255) NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'active',
  `trigger_keywords_json` JSON NULL,
  `steps_json` JSON NOT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `deleted_at` DATETIME(3) NULL,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_scenario_templates_code` (`code`),
  CONSTRAINT `fk_scenario_templates_tenants` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`)
) ENGINE=InnoDB;

CREATE TABLE `scenario_instances` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `template_code` VARCHAR(64) NOT NULL,
  `name` VARCHAR(50) NOT NULL,
  `trigger_keywords_json` JSON NULL,
  `steps_json` JSON NOT NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'enabled',
  `created_by_user_id` BIGINT NOT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `deleted_at` DATETIME(3) NULL,
  `row_version` BIGINT NOT NULL DEFAULT 1,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  KEY `idx_scenario_instances_tenant` (`tenant_id`),
  CONSTRAINT `fk_scenario_instances_tenants` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`)
) ENGINE=InnoDB;

-- B22 种子模板：步骤语义对齐既有管家意图（睡眠/回家/离家）。
-- room="*" 表示不限房间；步骤 value 为目标值 JSON；optional=true 失败不阻塞场景。
INSERT INTO `scenario_templates` (`tenant_id`,`code`,`name`,`summary`,`status`,`trigger_keywords_json`,`steps_json`)
VALUES (1,'goodnight','晚安','关闭卧室照明并将空调调至睡眠温度。','active',
  '["晚安","我要睡觉了","睡了"]',
  '[{"id":"step_1","name":"关闭卧室灯","device_type":"light","room":"bedroom","capability":"power","value":false,"optional":false},{"id":"step_2","name":"设置卧室空调温度","device_type":"air_conditioner","room":"bedroom","capability":"temperature","value":26,"optional":true}]')
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`summary`=VALUES(`summary`),`status`=VALUES(`status`),`trigger_keywords_json`=VALUES(`trigger_keywords_json`),`steps_json`=VALUES(`steps_json`);

INSERT INTO `scenario_templates` (`tenant_id`,`code`,`name`,`summary`,`status`,`trigger_keywords_json`,`steps_json`)
VALUES (1,'arrive_home','回家','恢复客厅舒适照明。','active',
  '["我回来了","回家"]',
  '[{"id":"step_1","name":"开启客厅灯","device_type":"light","room":"living_room","capability":"power","value":true,"optional":false}]')
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`summary`=VALUES(`summary`),`status`=VALUES(`status`),`trigger_keywords_json`=VALUES(`trigger_keywords_json`),`steps_json`=VALUES(`steps_json`);

INSERT INTO `scenario_templates` (`tenant_id`,`code`,`name`,`summary`,`status`,`trigger_keywords_json`,`steps_json`)
VALUES (1,'leave_home','离家','关闭全部房间的非必要设备。','active',
  '["我出去了","离家","出门"]',
  '[{"id":"step_1","name":"关闭全部灯光","device_type":"light","room":"*","capability":"power","value":false,"optional":false},{"id":"step_2","name":"关闭开关设备","device_type":"switch","room":"*","capability":"power","value":false,"optional":true}]')
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`summary`=VALUES(`summary`),`status`=VALUES(`status`),`trigger_keywords_json`=VALUES(`trigger_keywords_json`),`steps_json`=VALUES(`steps_json`);
