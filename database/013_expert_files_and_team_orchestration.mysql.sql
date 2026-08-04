-- Apply after 012_automation_and_connector_sync.mysql.sql.
-- Expert Files and versioned multi-expert team orchestration.
-- No file binary, credential, vendor identifier or prompt is stored here.
USE `nexus_mind`;

CREATE TABLE `expert_files` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `owner_user_id` BIGINT NOT NULL,
  `name` VARCHAR(256) NOT NULL,
  `mime_type` VARCHAR(128) NOT NULL,
  `size_bytes` BIGINT NOT NULL,
  `sha256` CHAR(64) NOT NULL,
  `status` VARCHAR(24) NOT NULL DEFAULT 'pending_upload',
  `scan_provider` VARCHAR(64) NULL,
  `scan_completed_at` DATETIME(3) NULL,
  `rejection_reason` VARCHAR(64) NULL,
  `quota_bytes` BIGINT NOT NULL DEFAULT 0,
  `expires_at` DATETIME(3) NULL,
  `soft_deleted_at` DATETIME(3) NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `row_version` BIGINT NOT NULL DEFAULT 1,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  KEY `idx_expert_file_tenant` (`tenant_id`,`status`,`updated_at`),
  KEY `idx_expert_file_owner` (`owner_user_id`,`updated_at`),
  CONSTRAINT `fk_expert_file_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_expert_file_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  CONSTRAINT `ck_expert_file_status` CHECK (`status` IN ('pending_upload','scanning','ready','rejected','deleted'))
) ENGINE=InnoDB;

CREATE TABLE `expert_file_objects` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `expert_file_id` BIGINT NOT NULL,
  `object_key` VARCHAR(512) NOT NULL,
  `size_bytes` BIGINT NOT NULL,
  `offset_bytes` BIGINT NOT NULL DEFAULT 0,
  `uploaded_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  KEY `idx_expert_file_object_file` (`expert_file_id`),
  CONSTRAINT `fk_expert_file_object_file` FOREIGN KEY (`expert_file_id`) REFERENCES `expert_files` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `expert_file_attachments` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `expert_file_id` BIGINT NOT NULL,
  `expert_id` BIGINT NULL,
  `agent_run_id` BIGINT NULL,
  `attached_by_user_id` BIGINT NOT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  KEY `idx_expert_file_attachment_expert` (`expert_id`),
  KEY `idx_expert_file_attachment_run` (`agent_run_id`),
  KEY `idx_expert_file_attachment_tenant` (`tenant_id`,`updated_at`),
  CONSTRAINT `fk_expert_file_attachment_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_expert_file_attachment_file` FOREIGN KEY (`expert_file_id`) REFERENCES `expert_files` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_expert_file_attachment_expert` FOREIGN KEY (`expert_id`) REFERENCES `experts` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_expert_file_attachment_run` FOREIGN KEY (`agent_run_id`) REFERENCES `expert_runs` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_expert_file_attachment_user` FOREIGN KEY (`attached_by_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  CONSTRAINT `ck_expert_file_attachment_target` CHECK ((`expert_id` IS NOT NULL AND `agent_run_id` IS NULL) OR (`expert_id` IS NULL AND `agent_run_id` IS NOT NULL))
) ENGINE=InnoDB;

CREATE TABLE `team_run_templates` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `owner_user_id` BIGINT NOT NULL,
  `name` VARCHAR(128) NOT NULL,
  `team_version` INT NOT NULL,
  `mode` VARCHAR(16) NOT NULL,
  `graph_json` JSON NOT NULL,
  `approval_policy` VARCHAR(32) NOT NULL DEFAULT 'manual_confirmation',
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `row_version` BIGINT NOT NULL DEFAULT 1,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  KEY `idx_team_run_template_tenant` (`tenant_id`,`updated_at`),
  CONSTRAINT `fk_team_run_template_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_team_run_template_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  CONSTRAINT `ck_team_run_template_mode` CHECK (`mode` IN ('sequential','parallel','synthesis')),
  CONSTRAINT `ck_team_run_template_approval` CHECK (`approval_policy` IN ('manual_confirmation','auto_execute'))
) ENGINE=InnoDB;

CREATE TABLE `team_run_template_versions` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `team_run_template_id` BIGINT NOT NULL,
  `tenant_id` BIGINT NOT NULL,
  `version` INT NOT NULL,
  `members_json` JSON NOT NULL,
  `file_refs_json` JSON NOT NULL,
  `permission_intersections_json` JSON NOT NULL,
  `graph_json` JSON NOT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_team_run_template_version` (`team_run_template_id`,`version`),
  KEY `idx_team_run_template_version_tenant` (`tenant_id`,`created_at`),
  CONSTRAINT `fk_team_run_template_version_template` FOREIGN KEY (`team_run_template_id`) REFERENCES `team_run_templates` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_team_run_template_version_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`)
) ENGINE=InnoDB;

CREATE TABLE `team_runs` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `parent_agent_run_id` BIGINT NOT NULL,
  `team_run_template_id` BIGINT NOT NULL,
  `team_run_template_version_id` BIGINT NOT NULL,
  `team_version` INT NOT NULL,
  `mode` VARCHAR(16) NOT NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'pending',
  `synthesis_result_json` JSON NULL,
  `last_error_code` VARCHAR(64) NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `row_version` BIGINT NOT NULL DEFAULT 1,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  KEY `idx_team_run_tenant` (`tenant_id`,`status`,`updated_at`),
  KEY `idx_team_run_parent` (`parent_agent_run_id`),
  CONSTRAINT `fk_team_run_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_team_run_parent` FOREIGN KEY (`parent_agent_run_id`) REFERENCES `expert_runs` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_team_run_template` FOREIGN KEY (`team_run_template_id`) REFERENCES `team_run_templates` (`id`),
  CONSTRAINT `fk_team_run_template_version` FOREIGN KEY (`team_run_template_version_id`) REFERENCES `team_run_template_versions` (`id`),
  CONSTRAINT `ck_team_run_mode` CHECK (`mode` IN ('sequential','parallel','synthesis')),
  CONSTRAINT `ck_team_run_status` CHECK (`status` IN ('pending','running','completed','failed','cancelled'))
) ENGINE=InnoDB;

CREATE TABLE `team_run_members` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `team_run_id` BIGINT NOT NULL,
  `expert_version_id` BIGINT NOT NULL,
  `child_agent_run_id` BIGINT NULL,
  `display_name` VARCHAR(128) NOT NULL,
  `stage_order` INT NOT NULL,
  `permission_intersection_json` JSON NOT NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'pending',
  `last_error_code` VARCHAR(64) NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  KEY `idx_team_run_member_team_run` (`team_run_id`,`stage_order`),
  KEY `idx_team_run_member_tenant` (`tenant_id`,`status`),
  CONSTRAINT `fk_team_run_member_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_team_run_member_team_run` FOREIGN KEY (`team_run_id`) REFERENCES `team_runs` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_team_run_member_expert_version` FOREIGN KEY (`expert_version_id`) REFERENCES `expert_versions` (`id`),
  CONSTRAINT `fk_team_run_member_child_run` FOREIGN KEY (`child_agent_run_id`) REFERENCES `expert_runs` (`id`) ON DELETE SET NULL,
  CONSTRAINT `ck_team_run_member_status` CHECK (`status` IN ('pending','running','completed','failed','cancelled','skipped'))
) ENGINE=InnoDB;

CREATE TABLE `team_run_audits` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `actor_user_id` BIGINT NULL,
  `team_run_id` BIGINT NULL,
  `expert_file_id` BIGINT NULL,
  `team_run_member_id` BIGINT NULL,
  `action` VARCHAR(32) NOT NULL,
  `result` VARCHAR(16) NOT NULL,
  `error_code` VARCHAR(64) NULL,
  `payload_json` JSON NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  KEY `idx_team_run_audit_tenant` (`tenant_id`,`created_at`),
  KEY `idx_team_run_audit_team_run` (`team_run_id`),
  KEY `idx_team_run_audit_file` (`expert_file_id`),
  CONSTRAINT `fk_team_run_audit_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_team_run_audit_team_run` FOREIGN KEY (`team_run_id`) REFERENCES `team_runs` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_team_run_audit_file` FOREIGN KEY (`expert_file_id`) REFERENCES `expert_files` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_team_run_audit_member` FOREIGN KEY (`team_run_member_id`) REFERENCES `team_run_members` (`id`) ON DELETE SET NULL,
  CONSTRAINT `ck_team_run_audit_result` CHECK (`result` IN ('success','failure')),
  CONSTRAINT `ck_team_run_audit_action` CHECK (`action` IN ('file_upload_session','file_object_commit','file_scan','file_read','file_delete','file_attach','team_run_create','team_run_member_start','team_run_member_complete','team_run_member_fail','team_run_synthesis','team_run_cancel','team_run_retry'))
) ENGINE=InnoDB;
