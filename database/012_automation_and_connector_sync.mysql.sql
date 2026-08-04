-- Apply after 011_agent_runtime_architecture.mysql.sql.
-- Durable automation rules and connector sync work. No credential or provider entity data is stored here.
USE `nexus_mind`;

CREATE TABLE `automation_rules` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `owner_user_id` BIGINT NOT NULL,
  `name` VARCHAR(128) NOT NULL,
  `trigger_type` VARCHAR(32) NOT NULL,
  `trigger_config_json` JSON NOT NULL,
  `conditions_json` JSON NOT NULL,
  `actions_json` JSON NOT NULL,
  `approval_policy` VARCHAR(32) NOT NULL DEFAULT 'manual_confirmation',
  `enabled` TINYINT(1) NOT NULL DEFAULT 1,
  `last_triggered_at` DATETIME(3) NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `row_version` BIGINT NOT NULL DEFAULT 1,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  KEY `idx_automation_rule_due` (`enabled`,`trigger_type`,`last_triggered_at`),
  KEY `idx_automation_rule_tenant` (`tenant_id`,`updated_at`),
  CONSTRAINT `fk_automation_rule_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_automation_rule_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  CONSTRAINT `ck_automation_rule_trigger` CHECK (`trigger_type` IN ('time_schedule','device_state_change','scene_completed','sync_completed')),
  CONSTRAINT `ck_automation_rule_approval` CHECK (`approval_policy` IN ('manual_confirmation','auto_execute'))
) ENGINE=InnoDB;

CREATE TABLE `connector_sync_jobs` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `workspace_connector_id` BIGINT NOT NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'queued',
  `reason` VARCHAR(32) NOT NULL DEFAULT 'manual',
  `attempt_no` INT NOT NULL DEFAULT 0,
  `available_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `started_at` DATETIME(3) NULL,
  `completed_at` DATETIME(3) NULL,
  `last_error_code` VARCHAR(64) NULL,
  `idempotency_key` CHAR(36) NOT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_connector_sync_job_idempotency` (`tenant_id`,`idempotency_key`),
  KEY `idx_connector_sync_job_dequeue` (`status`,`available_at`),
  CONSTRAINT `fk_connector_sync_job_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_connector_sync_job_connector` FOREIGN KEY (`workspace_connector_id`) REFERENCES `workspace_connectors` (`id`) ON DELETE CASCADE,
  CONSTRAINT `ck_connector_sync_job_status` CHECK (`status` IN ('queued','running','completed','failed'))
) ENGINE=InnoDB;
