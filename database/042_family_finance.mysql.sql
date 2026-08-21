-- B41-B42 家庭财务账单：本地解析导入、按内容哈希去重，并为周报聚合提供事实源。
CREATE TABLE `finance_transactions` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '账单条目主键',
  `home_id` BIGINT NOT NULL COMMENT '所属家庭主键',
  `created_by_user_id` BIGINT NOT NULL COMMENT '导入操作用户主键',
  `transaction_date` DATE NOT NULL COMMENT '消费发生日期',
  `merchant` VARCHAR(256) NOT NULL COMMENT '商户或收款方名称',
  `amount` DECIMAL(18,2) NOT NULL COMMENT '消费金额，支出为正数',
  `currency` VARCHAR(8) NOT NULL DEFAULT 'CNY' COMMENT '货币代码',
  `category` VARCHAR(64) NOT NULL DEFAULT '其他' COMMENT '账单分类',
  `source_type` VARCHAR(16) NOT NULL DEFAULT 'csv' COMMENT '来源类型：csv/ocr/manual',
  `source_ref` VARCHAR(256) NULL COMMENT '来源文件或外部流水去重引用',
  `content_hash` CHAR(64) NOT NULL COMMENT '归一化账单行哈希，仅用于去重',
  `notes` VARCHAR(512) NULL COMMENT '备注或解析说明',
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_finance_home_content_hash` (`home_id`,`content_hash`),
  KEY `idx_finance_home_date` (`home_id`,`transaction_date`),
  CONSTRAINT `fk_finance_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_finance_user` FOREIGN KEY (`created_by_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `ck_finance_amount` CHECK (`amount` > 0),
  CONSTRAINT `ck_finance_source_type` CHECK (`source_type` IN ('csv','ocr','manual'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='家庭财务账单事实表';

ALTER TABLE `family_audit_logs` DROP CHECK `ck_family_audit_action`;
ALTER TABLE `family_audit_logs` ADD CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record','confirmation_confirm','confirmation_deny','confirmation_batch','activity_undo','favorite_create','favorite_update','favorite_delete','favorite_import','finance_import','connector_authorize_started','connector_authorize_completed','connector_authorize_revoked','tenant_member_role_changed','tenant_member_status_changed','tenant_invitation_created','tenant_invitation_revoked','tenant_invitation_accepted','tenant_owner_transferred','web_navigation_preference_updated','conversation_create','conversation_rename','conversation_delete','skill_run_created','skill_action_confirmed','skill_draft_registered','xhs_note_published','media_file_uploaded','media_file_deleted','skill_run_revised','memory_candidate_accepted','memory_candidate_rejected'));
ALTER TABLE `family_audit_logs` DROP CHECK `ck_family_audit_target_type`;
ALTER TABLE `family_audit_logs` ADD CONSTRAINT `ck_family_audit_target_type` CHECK (`target_type` IN ('family_member','family_knowledge','decision_history','confirmation_item','steward_activity','personal_favorite','finance_transaction','connector_authorization','tenant_member','tenant_invitation','web_navigation_preference','conversation','skill_run','skill_draft','xhs_note','clipping_material','memory_candidate','learning_memory'));
