-- Apply after 009_housekeeper_run_orchestration.mysql.sql.
-- Stores a credential-free audit trail for confirmed SmartHome device actions.
USE `nexus_mind`;

CREATE TABLE `action_execution_audits` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `run_action_id` BIGINT NOT NULL,
  `operator_user_id` BIGINT NOT NULL,
  `workspace_connector_id` BIGINT NOT NULL,
  `device_id` BIGINT NOT NULL,
  `idempotency_key` CHAR(36) NOT NULL,
  `status` VARCHAR(16) NOT NULL,
  `command_json` JSON NOT NULL,
  `result_json` JSON NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_action_execution_idempotency` (`run_action_id`,`idempotency_key`),
  KEY `idx_action_execution_tenant_created` (`tenant_id`,`created_at`),
  CONSTRAINT `fk_action_execution_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_action_execution_action` FOREIGN KEY (`run_action_id`) REFERENCES `expert_run_actions` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_action_execution_operator` FOREIGN KEY (`operator_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_action_execution_connector` FOREIGN KEY (`workspace_connector_id`) REFERENCES `workspace_connectors` (`id`),
  CONSTRAINT `fk_action_execution_device` FOREIGN KEY (`device_id`) REFERENCES `smart_home_devices` (`id`),
  CONSTRAINT `ck_action_execution_status` CHECK (`status` IN ('executing','executed','failed'))
) ENGINE=InnoDB;
