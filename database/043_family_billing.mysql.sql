-- B43 家庭缴费管家：本地缴费账户到期日历与缴后记录；不保存账户号码或支付凭据。
CREATE TABLE `billing_accounts` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '缴费账户主键',
  `home_id` BIGINT NOT NULL COMMENT '所属家庭主键',
  `created_by_user_id` BIGINT NOT NULL COMMENT '创建建档的用户主键',
  `billing_type` VARCHAR(32) NOT NULL COMMENT '缴费类别：water/electricity/gas/property/mobile/insurance/other',
  `provider` VARCHAR(128) NOT NULL COMMENT '缴费机构展示名称',
  `label` VARCHAR(128) NOT NULL COMMENT '家庭内区分账单的非敏感标签',
  `billing_cycle_months` INT NOT NULL DEFAULT 1 COMMENT '账单周期月数，用于缴后推算到期日',
  `expected_amount` DECIMAL(18,2) NULL COMMENT '预计应缴金额，未知时为空',
  `currency` VARCHAR(8) NOT NULL DEFAULT 'CNY' COMMENT '金额货币代码',
  `next_due_date` DATE NOT NULL COMMENT '当前待缴账单到期日期',
  `source_type` VARCHAR(16) NOT NULL DEFAULT 'manual' COMMENT '建档来源：manual/ocr/finance',
  `source_ref` VARCHAR(256) NULL COMMENT '本地文件或解析批次的脱敏引用',
  `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否继续参与到期提醒',
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间，UTC',
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '最近更新时间，UTC',
  PRIMARY KEY (`id`),
  KEY `idx_billing_accounts_home_due_active` (`home_id`,`next_due_date`,`is_active`),
  CONSTRAINT `fk_billing_accounts_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_billing_accounts_user` FOREIGN KEY (`created_by_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `ck_billing_accounts_type` CHECK (`billing_type` IN ('water','electricity','gas','property','mobile','insurance','other')),
  CONSTRAINT `ck_billing_accounts_cycle` CHECK (`billing_cycle_months` BETWEEN 1 AND 24),
  CONSTRAINT `ck_billing_accounts_expected_amount` CHECK (`expected_amount` IS NULL OR `expected_amount` > 0),
  CONSTRAINT `ck_billing_accounts_source` CHECK (`source_type` IN ('manual','ocr','finance'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='家庭缴费账户和到期日历';

-- B43 家庭缴费完成记录：每个账户同一到期日只能登记一次，并关联财务事实源。
CREATE TABLE `billing_payment_records` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '缴费记录主键',
  `billing_account_id` BIGINT NOT NULL COMMENT '所属缴费账户主键',
  `home_id` BIGINT NOT NULL COMMENT '所属家庭主键',
  `recorded_by_user_id` BIGINT NOT NULL COMMENT '登记缴费的用户主键',
  `due_date` DATE NOT NULL COMMENT '本次账单到期日期',
  `paid_at` DATE NOT NULL COMMENT '实际缴费日期',
  `amount` DECIMAL(18,2) NOT NULL COMMENT '实际缴费金额',
  `currency` VARCHAR(8) NOT NULL DEFAULT 'CNY' COMMENT '金额货币代码',
  `source_type` VARCHAR(16) NOT NULL DEFAULT 'manual' COMMENT '登记来源：manual/ocr/finance',
  `finance_transaction_id` BIGINT NOT NULL COMMENT '关联的家庭财务账单事实主键',
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间，UTC',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_billing_payment_account_due` (`billing_account_id`,`due_date`),
  KEY `idx_billing_payment_home_paid` (`home_id`,`paid_at`),
  CONSTRAINT `fk_billing_payment_account` FOREIGN KEY (`billing_account_id`) REFERENCES `billing_accounts` (`id`),
  CONSTRAINT `fk_billing_payment_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_billing_payment_user` FOREIGN KEY (`recorded_by_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `fk_billing_payment_finance` FOREIGN KEY (`finance_transaction_id`) REFERENCES `finance_transactions` (`id`),
  CONSTRAINT `ck_billing_payment_amount` CHECK (`amount` > 0),
  CONSTRAINT `ck_billing_payment_source` CHECK (`source_type` IN ('manual','ocr','finance'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='家庭缴费完成记录和财务事实关联';

ALTER TABLE `family_audit_logs` DROP CHECK `ck_family_audit_action`;
ALTER TABLE `family_audit_logs` ADD CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record','confirmation_confirm','confirmation_deny','confirmation_batch','activity_undo','favorite_create','favorite_update','favorite_delete','favorite_import','finance_import','billing_account_create','billing_payment_record','connector_authorize_started','connector_authorize_completed','connector_authorize_revoked','tenant_member_role_changed','tenant_member_status_changed','tenant_invitation_created','tenant_invitation_revoked','tenant_invitation_accepted','tenant_owner_transferred','web_navigation_preference_updated','conversation_create','conversation_rename','conversation_delete','skill_run_created','skill_action_confirmed','skill_draft_registered','xhs_note_published','media_file_uploaded','media_file_deleted','skill_run_revised','memory_candidate_accepted','memory_candidate_rejected'));
ALTER TABLE `family_audit_logs` DROP CHECK `ck_family_audit_target_type`;
ALTER TABLE `family_audit_logs` ADD CONSTRAINT `ck_family_audit_target_type` CHECK (`target_type` IN ('family_member','family_knowledge','decision_history','confirmation_item','steward_activity','personal_favorite','finance_transaction','billing_account','billing_payment_record','connector_authorization','tenant_member','tenant_invitation','web_navigation_preference','conversation','skill_run','skill_draft','xhs_note','clipping_material','memory_candidate','learning_memory'));
