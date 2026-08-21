-- B46 家庭日程协同管家：仅保存证件到期提醒元数据，不保存号码、照片或证件原文。
CREATE TABLE `family_document_deadlines` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '证件到期记录主键',
  `home_id` BIGINT NOT NULL COMMENT '所属家庭主键',
  `holder_user_id` BIGINT NULL COMMENT '持有人家庭账号主键，未关联账号时为空',
  `document_type` VARCHAR(32) NOT NULL COMMENT '证件类型：identity_card/passport/driver_license/residence_permit/other',
  `display_name` VARCHAR(128) NOT NULL COMMENT '家庭内展示名称，不含证件号码或原文',
  `expires_on` DATE NOT NULL COMMENT '证件到期日期',
  `created_by_user_id` BIGINT NOT NULL COMMENT '创建提醒的用户主键',
  `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否继续参与到期提醒',
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间，UTC',
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间，UTC',
  PRIMARY KEY (`id`), KEY `idx_family_document_deadline_home_due` (`home_id`,`expires_on`,`is_active`),
  CONSTRAINT `fk_family_document_deadline_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_family_document_deadline_holder` FOREIGN KEY (`holder_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `fk_family_document_deadline_creator` FOREIGN KEY (`created_by_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `ck_family_document_deadline_type` CHECK (`document_type` IN ('identity_card','passport','driver_license','residence_permit','other'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='家庭证件到期提醒元数据';

ALTER TABLE `family_audit_logs` DROP CHECK `ck_family_audit_action`;
ALTER TABLE `family_audit_logs` ADD CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record','confirmation_confirm','confirmation_deny','confirmation_batch','activity_undo','favorite_create','favorite_update','favorite_delete','favorite_import','finance_import','billing_account_create','billing_payment_record','pet_profile_create','pet_care_event_create','pet_supply_upsert','schedule_document_deadline_create','connector_authorize_started','connector_authorize_completed','connector_authorize_revoked','tenant_member_role_changed','tenant_member_status_changed','tenant_invitation_created','tenant_invitation_revoked','tenant_invitation_accepted','tenant_owner_transferred','web_navigation_preference_updated','conversation_create','conversation_rename','conversation_delete','skill_run_created','skill_action_confirmed','skill_draft_registered','xhs_note_published','media_file_uploaded','media_file_deleted','skill_run_revised','memory_candidate_accepted','memory_candidate_rejected'));
ALTER TABLE `family_audit_logs` DROP CHECK `ck_family_audit_target_type`;
ALTER TABLE `family_audit_logs` ADD CONSTRAINT `ck_family_audit_target_type` CHECK (`target_type` IN ('family_member','family_knowledge','decision_history','confirmation_item','steward_activity','personal_favorite','finance_transaction','billing_account','billing_payment_record','pet_profile','pet_care_event','pet_supply','schedule_document_deadline','connector_authorization','tenant_member','tenant_invitation','web_navigation_preference','conversation','skill_run','skill_draft','xhs_note','clipping_material','memory_candidate','learning_memory'));
