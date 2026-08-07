-- Apply after 025_v2.4_web_governance.mysql.sql.
-- B20 expert conversations (V2.4, 第六阶段):
--   conversations: owner-scoped conversation project binding expert + connector
--     (connector is metadata only in this slice, multi-connector later);
--   conversation_messages: user/assistant messages, unique (conversation_id, run_id)
--     as idempotency guarantee for run-traceable messages;
--   expert_runs.conversation_id: nullable link from a run to its conversation;
--   family_audit_logs CHECK extended with 3 conversation actions and 1 target type.
USE `nexus_mind`;

CREATE TABLE `conversations` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `owner_user_id` BIGINT NOT NULL,
  `title` VARCHAR(64) NOT NULL,
  `expert_id` BIGINT NULL,
  `expert_version_id` BIGINT NULL,
  `workspace_connector_id` BIGINT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `deleted_at` DATETIME(3) NULL,
  `row_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  KEY `idx_conversation_owner` (`tenant_id`,`owner_user_id`,`deleted_at`,`updated_at`),
  CONSTRAINT `fk_conversation_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_conversation_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `fk_conversation_expert` FOREIGN KEY (`expert_id`) REFERENCES `experts` (`id`),
  CONSTRAINT `fk_conversation_expert_version` FOREIGN KEY (`expert_version_id`) REFERENCES `expert_versions` (`id`),
  CONSTRAINT `fk_conversation_connector` FOREIGN KEY (`workspace_connector_id`) REFERENCES `workspace_connectors` (`id`),
  CONSTRAINT `ck_conversation_expert` CHECK ((`expert_id` IS NULL AND `expert_version_id` IS NULL) OR (`expert_id` IS NOT NULL AND `expert_version_id` IS NOT NULL))
) ENGINE=InnoDB;

CREATE TABLE `conversation_messages` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `conversation_id` BIGINT NOT NULL,
  `role` VARCHAR(16) NOT NULL,
  `content` TEXT NOT NULL,
  `run_id` BIGINT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_convmsg_conversation_run` (`conversation_id`,`run_id`),
  KEY `idx_convmsg_conversation_id` (`conversation_id`,`id`),
  CONSTRAINT `fk_convmsg_conversation` FOREIGN KEY (`conversation_id`) REFERENCES `conversations` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_convmsg_run` FOREIGN KEY (`run_id`) REFERENCES `expert_runs` (`id`),
  CONSTRAINT `ck_convmsg_role` CHECK (`role` IN ('user','assistant'))
) ENGINE=InnoDB;

ALTER TABLE `expert_runs`
  ADD COLUMN `conversation_id` BIGINT NULL AFTER `finished_at`,
  ADD KEY `idx_run_conversation` (`conversation_id`,`id`),
  ADD CONSTRAINT `fk_run_conversation` FOREIGN KEY (`conversation_id`) REFERENCES `conversations` (`id`);

ALTER TABLE `family_audit_logs`
  DROP CHECK `ck_family_audit_action`,
  DROP CHECK `ck_family_audit_target_type`,
  ADD CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record','confirmation_confirm','confirmation_deny','confirmation_batch','activity_undo','favorite_create','favorite_update','favorite_delete','favorite_import','connector_authorize_started','connector_authorize_completed','connector_authorize_revoked','tenant_member_role_changed','tenant_member_status_changed','tenant_invitation_created','tenant_invitation_revoked','tenant_invitation_accepted','tenant_owner_transferred','web_navigation_preference_updated','conversation_create','conversation_rename','conversation_delete')),
  ADD CONSTRAINT `ck_family_audit_target_type` CHECK (`target_type` IN ('family_member','family_knowledge','decision_history','confirmation_item','steward_activity','personal_favorite','connector_authorization','tenant_member','tenant_invitation','web_navigation_preference','conversation'));
