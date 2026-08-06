-- Apply after 023_ai_config_enabled.mysql.sql.
-- B18 connector scope baseline (V2.4):
--   workspace_connectors gains binding_scope / owner_user_id / auth_status / config;
--   new connector_authorization_sessions table for single-use OAuth server sessions;
--   expert_runs gains permission_snapshot_json for Run permission snapshots;
--   family_audit_logs CHECK extended with connector authorization actions;
--   mock_oauth provider registered for local dev/test verification.
USE `nexus_mind`;

ALTER TABLE `workspace_connectors`
  ADD COLUMN `binding_scope` VARCHAR(16) NOT NULL DEFAULT 'household' AFTER `connector_provider_id`,
  ADD COLUMN `owner_user_id` BIGINT NULL AFTER `binding_scope`,
  ADD COLUMN `auth_status` VARCHAR(16) NOT NULL DEFAULT 'none' AFTER `status`,
  ADD COLUMN `config` JSON NULL AFTER `auth_status`,
  ADD KEY `idx_workspace_connector_owner` (`tenant_id`,`binding_scope`,`owner_user_id`),
  ADD CONSTRAINT `fk_workspace_connector_owner_user` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`),
  ADD CONSTRAINT `ck_workspace_connector_binding_scope` CHECK (`binding_scope` IN ('household','personal')),
  ADD CONSTRAINT `ck_workspace_connector_auth_status` CHECK (`auth_status` IN ('none','authorizing','connected','revoked','failed')),
  ADD CONSTRAINT `ck_workspace_connector_owner_scope` CHECK ((`binding_scope` = 'household' AND `owner_user_id` IS NULL) OR (`binding_scope` = 'personal' AND `owner_user_id` IS NOT NULL));

CREATE TABLE `connector_authorization_sessions` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `connector_provider_id` BIGINT NOT NULL,
  `binding_scope` VARCHAR(16) NOT NULL DEFAULT 'personal',
  `initiator_user_id` BIGINT NOT NULL,
  `state_hash` CHAR(64) NOT NULL,
  `pkce_verifier_ref` VARCHAR(512) NULL,
  `redirect_uri` VARCHAR(512) NOT NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'pending',
  `expires_at` DATETIME(3) NOT NULL,
  `completed_at` DATETIME(3) NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  KEY `idx_connector_auth_session_tenant_status` (`tenant_id`,`status`),
  KEY `idx_connector_auth_session_initiator` (`initiator_user_id`),
  CONSTRAINT `fk_connector_auth_session_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_connector_auth_session_provider` FOREIGN KEY (`connector_provider_id`) REFERENCES `connector_providers` (`id`),
  CONSTRAINT `fk_connector_auth_session_initiator` FOREIGN KEY (`initiator_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `ck_connector_auth_session_scope` CHECK (`binding_scope` IN ('household','personal')),
  CONSTRAINT `ck_connector_auth_session_status` CHECK (`status` IN ('pending','used','expired','revoked','completed','failed'))
) ENGINE=InnoDB;

ALTER TABLE `expert_runs`
  ADD COLUMN `permission_snapshot_json` JSON NULL AFTER `auto_confirm_policy`;

ALTER TABLE `family_audit_logs`
  DROP CHECK `ck_family_audit_action`,
  DROP CHECK `ck_family_audit_target_type`,
  ADD CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record','confirmation_confirm','confirmation_deny','confirmation_batch','activity_undo','favorite_create','favorite_update','favorite_delete','favorite_import','connector_authorize_started','connector_authorize_completed','connector_authorize_revoked')),
  ADD CONSTRAINT `ck_family_audit_target_type` CHECK (`target_type` IN ('family_member','family_knowledge','decision_history','confirmation_item','steward_activity','personal_favorite','connector_authorization'));

INSERT INTO `connector_providers` (`code`, `name`, `provider`, `connector_type`, `status`, `description`)
VALUES ('mock_oauth', 'Mock OAuth（开发验证）', 'mock_oauth', 'calendar', 'active', '本地确定性 OAuth Provider，用于开发与测试环境验证个人授权链路。')
ON DUPLICATE KEY UPDATE
  `name` = VALUES(`name`),
  `provider` = VALUES(`provider`),
  `connector_type` = VALUES(`connector_type`),
  `status` = VALUES(`status`),
  `description` = VALUES(`description`),
  `deleted_at` = NULL;
