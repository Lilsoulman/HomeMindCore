-- B45 宠物管家：档案、疫苗/驱虫日历与用品消耗预测，不自动下单。
CREATE TABLE `pet_profiles` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '宠物档案主键',
  `home_id` BIGINT NOT NULL COMMENT '所属家庭主键',
  `created_by_user_id` BIGINT NOT NULL COMMENT '创建档案的用户主键',
  `name` VARCHAR(64) NOT NULL COMMENT '宠物昵称',
  `species` VARCHAR(32) NOT NULL COMMENT '宠物种类',
  `breed` VARCHAR(64) NULL COMMENT '宠物品种',
  `birth_date` DATE NULL COMMENT '宠物出生日期',
  `notes` VARCHAR(512) NULL COMMENT '宠物档案备注',
  `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否参与家庭提醒',
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间，UTC',
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间，UTC',
  PRIMARY KEY (`id`), KEY `idx_pet_profiles_home_active` (`home_id`,`is_active`),
  CONSTRAINT `fk_pet_profile_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_pet_profile_user` FOREIGN KEY (`created_by_user_id`) REFERENCES `users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='家庭宠物基础档案';

CREATE TABLE `pet_care_events` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '照护日历记录主键',
  `pet_id` BIGINT NOT NULL COMMENT '所属宠物主键',
  `home_id` BIGINT NOT NULL COMMENT '所属家庭主键',
  `care_type` VARCHAR(16) NOT NULL COMMENT '照护类型：vaccine/deworming',
  `title` VARCHAR(128) NOT NULL COMMENT '照护项目名称',
  `due_date` DATE NOT NULL COMMENT '下一次到期日期',
  `completed_at` DATE NULL COMMENT '完成日期',
  `notes` VARCHAR(512) NULL COMMENT '照护备注',
  `created_by_user_id` BIGINT NOT NULL COMMENT '创建记录的用户主键',
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间，UTC',
  PRIMARY KEY (`id`), KEY `idx_pet_care_home_due` (`home_id`,`pet_id`,`due_date`),
  CONSTRAINT `fk_pet_care_pet` FOREIGN KEY (`pet_id`) REFERENCES `pet_profiles` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_pet_care_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_pet_care_user` FOREIGN KEY (`created_by_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `ck_pet_care_type` CHECK (`care_type` IN ('vaccine','deworming'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='宠物疫苗与驱虫照护日历';

CREATE TABLE `pet_supply_records` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '用品库存记录主键',
  `pet_id` BIGINT NOT NULL COMMENT '所属宠物主键',
  `home_id` BIGINT NOT NULL COMMENT '所属家庭主键',
  `item_name` VARCHAR(128) NOT NULL COMMENT '用品名称',
  `quantity` DECIMAL(18,3) NOT NULL COMMENT '当前库存数量',
  `daily_usage` DECIMAL(18,3) NOT NULL COMMENT '日均消耗数量',
  `unit` VARCHAR(16) NOT NULL DEFAULT '份' COMMENT '库存数量单位',
  `source_type` VARCHAR(16) NOT NULL DEFAULT 'manual' COMMENT '库存来源：manual/finance',
  `measured_at` DATE NOT NULL COMMENT '最近一次库存测量日期',
  `created_by_user_id` BIGINT NOT NULL COMMENT '创建记录的用户主键',
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间，UTC',
  PRIMARY KEY (`id`), UNIQUE KEY `uk_pet_supply_item` (`pet_id`,`item_name`), KEY `idx_pet_supply_home` (`home_id`,`pet_id`),
  CONSTRAINT `fk_pet_supply_pet` FOREIGN KEY (`pet_id`) REFERENCES `pet_profiles` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_pet_supply_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_pet_supply_user` FOREIGN KEY (`created_by_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `ck_pet_supply_quantity` CHECK (`quantity` >= 0),
  CONSTRAINT `ck_pet_supply_usage` CHECK (`daily_usage` > 0)
  , CONSTRAINT `ck_pet_supply_source` CHECK (`source_type` IN ('manual','finance'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='宠物用品库存与日均消耗';

ALTER TABLE `family_audit_logs` DROP CHECK `ck_family_audit_action`;
ALTER TABLE `family_audit_logs` ADD CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record','confirmation_confirm','confirmation_deny','confirmation_batch','activity_undo','favorite_create','favorite_update','favorite_delete','favorite_import','finance_import','billing_account_create','billing_payment_record','pet_profile_create','pet_care_event_create','pet_supply_upsert','connector_authorize_started','connector_authorize_completed','connector_authorize_revoked','tenant_member_role_changed','tenant_member_status_changed','tenant_invitation_created','tenant_invitation_revoked','tenant_invitation_accepted','tenant_owner_transferred','web_navigation_preference_updated','conversation_create','conversation_rename','conversation_delete','skill_run_created','skill_action_confirmed','skill_draft_registered','xhs_note_published','media_file_uploaded','media_file_deleted','skill_run_revised','memory_candidate_accepted','memory_candidate_rejected'));
ALTER TABLE `family_audit_logs` DROP CHECK `ck_family_audit_target_type`;
ALTER TABLE `family_audit_logs` ADD CONSTRAINT `ck_family_audit_target_type` CHECK (`target_type` IN ('family_member','family_knowledge','decision_history','confirmation_item','steward_activity','personal_favorite','finance_transaction','billing_account','billing_payment_record','pet_profile','pet_care_event','pet_supply','connector_authorization','tenant_member','tenant_invitation','web_navigation_preference','conversation','skill_run','skill_draft','xhs_note','clipping_material','memory_candidate','learning_memory'));
