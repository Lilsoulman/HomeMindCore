-- Apply after 024_v2.4_connector_scope.mysql.sql.
-- B19 web governance baseline (V2.4):
--   tenant_member_invitations: family member invitations hashed by phone,
--     single pending row per (tenant_id, subject_hash);
--   web_navigation_preferences: role-level web menu visibility/sort
--     with route_key whitelist enforced by application layer;
--   family_audit_logs CHECK extended with 7 new tenant/web actions and
--     3 new target types; tenant_members gains row_version for optimistic lock.
USE `nexus_mind`;

CREATE TABLE `tenant_member_invitations` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `invited_by_user_id` BIGINT NOT NULL,
  `subject_kind` VARCHAR(16) NOT NULL DEFAULT 'phone',
  `subject_hash` BINARY(32) NOT NULL,
  `proposed_role` VARCHAR(16) NOT NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'pending',
  `expires_at` DATETIME(3) NOT NULL,
  `accepted_user_id` BIGINT NULL,
  `accepted_at` DATETIME(3) NULL,
  `revoked_at` DATETIME(3) NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `row_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_invitation_tenant_subject` (`tenant_id`,`subject_hash`),
  KEY `idx_invitation_tenant_status` (`tenant_id`,`status`,`expires_at`),
  KEY `idx_invitation_subject_lookup` (`subject_kind`,`subject_hash`),
  CONSTRAINT `fk_invitation_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_invitation_inviter` FOREIGN KEY (`invited_by_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `fk_invitation_acceptor` FOREIGN KEY (`accepted_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `ck_invitation_subject_kind` CHECK (`subject_kind` IN ('phone')),
  CONSTRAINT `ck_invitation_proposed_role` CHECK (`proposed_role` IN ('admin','member','viewer')),
  CONSTRAINT `ck_invitation_status` CHECK (`status` IN ('pending','accepted','expired','revoked'))
) ENGINE=InnoDB;

CREATE TABLE `web_navigation_preferences` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `role` VARCHAR(16) NOT NULL,
  `route_key` VARCHAR(64) NOT NULL,
  `enabled` TINYINT(1) NOT NULL DEFAULT 1,
  `sort_order` INT NOT NULL DEFAULT 0,
  `updated_by_user_id` BIGINT NOT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_webnav_tenant_role_route` (`tenant_id`,`role`,`route_key`),
  KEY `idx_webnav_tenant_role_order` (`tenant_id`,`role`,`sort_order`),
  CONSTRAINT `fk_webnav_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_webnav_updater` FOREIGN KEY (`updated_by_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `ck_webnav_role` CHECK (`role` IN ('owner','admin','member','viewer'))
) ENGINE=InnoDB;

ALTER TABLE `tenant_members`
  ADD COLUMN `row_version` BIGINT NOT NULL DEFAULT 1 COMMENT '乐观锁版本号';

ALTER TABLE `family_audit_logs`
  DROP CHECK `ck_family_audit_action`,
  DROP CHECK `ck_family_audit_target_type`,
  ADD CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record','confirmation_confirm','confirmation_deny','confirmation_batch','activity_undo','favorite_create','favorite_update','favorite_delete','favorite_import','connector_authorize_started','connector_authorize_completed','connector_authorize_revoked','tenant_member_role_changed','tenant_member_status_changed','tenant_invitation_created','tenant_invitation_revoked','tenant_invitation_accepted','tenant_owner_transferred','web_navigation_preference_updated')),
  ADD CONSTRAINT `ck_family_audit_target_type` CHECK (`target_type` IN ('family_member','family_knowledge','decision_history','confirmation_item','steward_activity','personal_favorite','connector_authorization','tenant_member','tenant_invitation','web_navigation_preference'));
