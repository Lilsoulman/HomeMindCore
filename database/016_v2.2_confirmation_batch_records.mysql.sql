-- Apply after 015_v2.2_family_audit_logs.mysql.sql.
-- B12 confirmation center idempotency records; extends family_audit_logs CHECK
-- constraints with confirmation-center and steward-activity audit actions.
USE `nexus_mind`;

CREATE TABLE `confirmation_batch_records` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `home_id` BIGINT NOT NULL,
  `idempotency_key` CHAR(36) NOT NULL,
  `confirmation_ids_json` JSON NOT NULL,
  `result_json` JSON NOT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_confirmation_batch_home_key` (`home_id`,`idempotency_key`),
  KEY `idx_confirmation_batch_home_created` (`home_id`,`created_at`),
  CONSTRAINT `fk_confirmation_batch_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`)
) ENGINE=InnoDB;

ALTER TABLE `family_audit_logs`
  DROP CHECK `ck_family_audit_action`,
  DROP CHECK `ck_family_audit_target_type`,
  ADD CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record','confirmation_confirm','confirmation_deny','confirmation_batch','activity_undo')),
  ADD CONSTRAINT `ck_family_audit_target_type` CHECK (`target_type` IN ('family_member','family_knowledge','decision_history','confirmation_item','steward_activity'));
