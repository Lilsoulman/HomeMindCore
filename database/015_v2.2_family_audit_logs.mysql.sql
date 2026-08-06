-- Apply after 014_v2.2_family_and_steward.mysql.sql.
-- B11 family context audit log; reuses FKs to tenants, users, expert_runs.
USE `nexus_mind`;

CREATE TABLE `family_audit_logs` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `home_id` BIGINT NOT NULL,
  `actor_user_id` BIGINT NULL,
  `action` VARCHAR(32) NOT NULL,
  `target_type` VARCHAR(32) NOT NULL,
  `target_id` BIGINT NULL,
  `before_json` JSON NULL,
  `after_json` JSON NULL,
  `reason` VARCHAR(512) NULL,
  `related_run_id` BIGINT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  KEY `idx_family_audit_home_created` (`home_id`, `created_at`),
  KEY `idx_family_audit_target` (`home_id`, `target_type`, `target_id`),
  CONSTRAINT `fk_family_audit_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_family_audit_actor` FOREIGN KEY (`actor_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_family_audit_related_run` FOREIGN KEY (`related_run_id`) REFERENCES `expert_runs` (`id`) ON DELETE SET NULL,
  CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record')),
  CONSTRAINT `ck_family_audit_target_type` CHECK (`target_type` IN ('family_member','family_knowledge','decision_history'))
) ENGINE=InnoDB;
