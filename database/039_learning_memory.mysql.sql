-- Apply after 038_clipping_rendering.mysql.sql.
-- M2/M3：候选审核、个人偏好事实与学习记忆展示投影。
USE `nexus_mind`;

CREATE TABLE `memory_candidates` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '候选主键',
  `home_id` BIGINT NOT NULL COMMENT '所属家庭主键',
  `owner_user_id` BIGINT NULL COMMENT '个人候选归属用户，家庭候选为空',
  `source_run_id` BIGINT NULL COMMENT '来源专家运行主键',
  `kind` VARCHAR(32) NOT NULL COMMENT '候选类型 preference/fact/decision',
  `visibility` VARCHAR(16) NOT NULL COMMENT '可见性 personal/family',
  `memory_key` VARCHAR(256) NOT NULL COMMENT '候选键',
  `proposed_value` TEXT NOT NULL COMMENT '建议写入的值',
  `display_summary` VARCHAR(512) NOT NULL COMMENT '脱敏展示摘要',
  `category` VARCHAR(32) NULL COMMENT '家庭知识分类',
  `confidence` DECIMAL(4,3) NOT NULL COMMENT '置信度，范围 0 到 1',
  `evidence_refs_json` JSON NULL COMMENT '服务端证据引用，不向客户端回显',
  `risk_level` VARCHAR(4) NOT NULL COMMENT '风险等级 L1/L2/L3',
  `status` VARCHAR(16) NOT NULL COMMENT '候选状态 pending/accepted/rejected/expired',
  `resolved_by_user_id` BIGINT NULL COMMENT '解决候选的用户主键',
  `resolved_at` DATETIME(6) NULL COMMENT '解决时间',
  `expires_at` DATETIME(6) NULL COMMENT '候选过期时间',
  `created_at` DATETIME(6) NOT NULL COMMENT '创建时间',
  `updated_at` DATETIME(6) NOT NULL COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_memory_candidates_run_visibility_key` (`source_run_id`,`visibility`,`memory_key`),
  KEY `idx_memory_candidates_home_status` (`home_id`,`status`,`created_at`),
  KEY `idx_memory_candidates_personal` (`home_id`,`owner_user_id`,`status`,`created_at`),
  CONSTRAINT `fk_memory_candidates_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_memory_candidates_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_memory_candidates_source_run` FOREIGN KEY (`source_run_id`) REFERENCES `expert_runs` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_memory_candidates_resolver` FOREIGN KEY (`resolved_by_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL,
  CONSTRAINT `ck_memory_candidates_kind` CHECK (`kind` IN ('preference','fact','decision')),
  CONSTRAINT `ck_memory_candidates_visibility` CHECK (`visibility` IN ('personal','family')),
  CONSTRAINT `ck_memory_candidates_scope` CHECK ((`visibility` = 'personal' AND `owner_user_id` IS NOT NULL) OR (`visibility` = 'family' AND `owner_user_id` IS NULL)),
  CONSTRAINT `ck_memory_candidates_confidence` CHECK (`confidence` >= 0 AND `confidence` <= 1),
  CONSTRAINT `ck_memory_candidates_risk` CHECK (`risk_level` IN ('L1','L2','L3')),
  CONSTRAINT `ck_memory_candidates_status` CHECK (`status` IN ('pending','accepted','rejected','expired'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='AI 后台复盘生成的待审核记忆候选';

CREATE TABLE `memory_review_receipts` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'Memory review receipt primary key',
  `source_run_id` BIGINT NOT NULL COMMENT 'Reviewed Run primary key',
  `candidate_count` INT NOT NULL COMMENT 'Number of pending candidates created',
  `reviewed_at` DATETIME(6) NOT NULL COMMENT 'Review timestamp',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_memory_review_receipts_run` (`source_run_id`),
  CONSTRAINT `fk_memory_review_receipts_source_run` FOREIGN KEY (`source_run_id`) REFERENCES `expert_runs` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Completed Run memory review receipts';

CREATE TABLE `personal_memory_preferences` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '个人偏好事实主键',
  `home_id` BIGINT NOT NULL COMMENT '所属家庭主键',
  `owner_user_id` BIGINT NOT NULL COMMENT '归属用户主键',
  `preference_key` VARCHAR(256) NOT NULL COMMENT '偏好键',
  `preference_value` TEXT NOT NULL COMMENT '偏好值',
  `display_summary` VARCHAR(512) NOT NULL COMMENT '脱敏展示摘要',
  `status` VARCHAR(16) NOT NULL DEFAULT 'active' COMMENT '状态 active/archived/expired',
  `created_at` DATETIME(6) NOT NULL COMMENT '创建时间',
  `updated_at` DATETIME(6) NOT NULL COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_personal_memory_preferences_owner` (`home_id`,`owner_user_id`,`status`,`updated_at`),
  CONSTRAINT `fk_personal_memory_preferences_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_personal_memory_preferences_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `ck_personal_memory_preferences_status` CHECK (`status` IN ('active','archived','expired'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='成员个人可召回偏好事实';

CREATE TABLE `learning_memory_records` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '学习记忆投影主键',
  `home_id` BIGINT NOT NULL COMMENT '所属家庭主键',
  `owner_user_id` BIGINT NULL COMMENT '个人记忆归属用户，家庭记忆为空',
  `candidate_id` BIGINT NOT NULL COMMENT '来源候选主键，同一候选唯一',
  `target_type` VARCHAR(32) NOT NULL COMMENT '事实源类型 family_knowledge/personal_preference',
  `target_id` BIGINT NOT NULL COMMENT '事实源主键',
  `kind` VARCHAR(32) NOT NULL COMMENT '记忆类型 preference/fact/decision',
  `visibility` VARCHAR(16) NOT NULL COMMENT '可见性 personal/family',
  `display_summary` VARCHAR(512) NOT NULL COMMENT '脱敏展示摘要',
  `stability` DECIMAL(4,3) NOT NULL COMMENT '稳定性，范围 0 到 1',
  `status` VARCHAR(16) NOT NULL DEFAULT 'active' COMMENT '状态 active/archived/expired',
  `source_run_id` BIGINT NULL COMMENT '来源运行主键',
  `learned_at` DATETIME(6) NOT NULL COMMENT '学习时间',
  `expires_at` DATETIME(6) NULL COMMENT '过期时间',
  `archived_at` DATETIME(6) NULL COMMENT '归档时间',
  `created_at` DATETIME(6) NOT NULL COMMENT '创建时间',
  `updated_at` DATETIME(6) NOT NULL COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_learning_memory_records_candidate` (`candidate_id`),
  KEY `idx_learning_memory_records_visible` (`home_id`,`owner_user_id`,`status`,`learned_at`),
  CONSTRAINT `fk_learning_memory_records_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_learning_memory_records_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_learning_memory_records_candidate` FOREIGN KEY (`candidate_id`) REFERENCES `memory_candidates` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_learning_memory_records_source_run` FOREIGN KEY (`source_run_id`) REFERENCES `expert_runs` (`id`) ON DELETE SET NULL,
  CONSTRAINT `ck_learning_memory_records_target` CHECK (`target_type` IN ('family_knowledge','personal_preference')),
  CONSTRAINT `ck_learning_memory_records_kind` CHECK (`kind` IN ('preference','fact','decision')),
  CONSTRAINT `ck_learning_memory_records_visibility` CHECK (`visibility` IN ('personal','family')),
  CONSTRAINT `ck_learning_memory_records_scope` CHECK ((`visibility` = 'personal' AND `owner_user_id` IS NOT NULL) OR (`visibility` = 'family' AND `owner_user_id` IS NULL)),
  CONSTRAINT `ck_learning_memory_records_stability` CHECK (`stability` >= 0 AND `stability` <= 1),
  CONSTRAINT `ck_learning_memory_records_status` CHECK (`status` IN ('active','archived','expired'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='已接受学习记忆的脱敏展示投影';

ALTER TABLE `family_audit_logs`
  DROP CHECK `ck_family_audit_action`,
  DROP CHECK `ck_family_audit_target_type`,
  ADD CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record','confirmation_confirm','confirmation_deny','confirmation_batch','activity_undo','favorite_create','favorite_update','favorite_delete','favorite_import','connector_authorize_started','connector_authorize_completed','connector_authorize_revoked','tenant_member_role_changed','tenant_member_status_changed','tenant_invitation_created','tenant_invitation_revoked','tenant_invitation_accepted','tenant_owner_transferred','web_navigation_preference_updated','conversation_create','conversation_rename','conversation_delete','skill_run_created','skill_action_confirmed','skill_draft_registered','xhs_note_published','media_file_uploaded','media_file_deleted','skill_run_revised','memory_candidate_accepted','memory_candidate_rejected')),
  ADD CONSTRAINT `ck_family_audit_target_type` CHECK (`target_type` IN ('family_member','family_knowledge','decision_history','confirmation_item','steward_activity','personal_favorite','connector_authorization','tenant_member','tenant_invitation','web_navigation_preference','conversation','skill_run','skill_draft','xhs_note','clipping_material','memory_candidate','learning_memory'));
