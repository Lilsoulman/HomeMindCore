-- Apply after 028_scenario_workflow.mysql.sql.
-- B24 quick-edit Skill baseline (V2.5):
--   skills  平台级 Skill 目录表（tenant_id 固定 1，与 scenario_templates 同惯例），
--           种子注册 quick-edit（category=media，risk_level=L1，required_permission=media.read）
-- 执行、确认、幂等与审计全部复用 AgentRun / ExpertRunAction / ActionExecutionAudits 链路，
-- 不新增运行表；本迁移仅建 skills 一张表、注册种子并扩展 family_audit_logs 的 CHECK 约束。
USE `nexus_mind`;

CREATE TABLE `skills` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL DEFAULT 1,
  `key` VARCHAR(64) NOT NULL,
  `name` VARCHAR(50) NOT NULL,
  `category` VARCHAR(32) NOT NULL,
  `description` VARCHAR(255) NULL,
  `input_schema_json` JSON NOT NULL,
  `output_schema_json` JSON NULL,
  `required_permission` VARCHAR(64) NOT NULL,
  `risk_level` VARCHAR(8) NOT NULL DEFAULT 'L1',
  `status` VARCHAR(16) NOT NULL DEFAULT 'active',
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `deleted_at` DATETIME(3) NULL,
  `row_version` BIGINT NOT NULL DEFAULT 1,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_skills_key` (`key`),
  CONSTRAINT `fk_skills_tenants` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`)
) ENGINE=InnoDB;

-- B24 种子：快速剪辑（V2.5）。输入 schema 声明素材位置与创作目标和指令，
-- 产物为剪映 .draft 草稿文件（本地可编辑可丢弃，不对外发布）。
INSERT INTO `skills` (`tenant_id`,`key`,`name`,`category`,`description`,`input_schema_json`,`output_schema_json`,`required_permission`,`risk_level`,`status`)
VALUES (1,'quick-edit','快速剪辑','media','把本机/NAS 素材按创作目标和指令生成可编辑的剪映草稿；草稿为本地文件，可继续编辑或渲染。','{"type":"object","required":["media_location"],"properties":{"media_location":{"type":"string","description":"本机/NAS 目录路径或 URI（视频/音频文件或目录）"},"instruction":{"type":"string","description":"创作目标和指令，如时长、画幅比例、配乐、字幕等要求"}}}','{"type":"object","properties":{"draft_path":{"type":"string"},"segments":{"type":"array"}}}','media.read','L1','active')
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`category`=VALUES(`category`),`description`=VALUES(`description`),`input_schema_json`=VALUES(`input_schema_json`),`output_schema_json`=VALUES(`output_schema_json`),`required_permission`=VALUES(`required_permission`),`risk_level`=VALUES(`risk_level`),`status`=VALUES(`status`);

-- B24 扩展 family_audit_logs：SkillRun 创建 / Action 确认 / 草稿登记三个审计动作，
-- 与 skill_run / skill_draft 两个目标类型（B25 复用，无新迁移）。
ALTER TABLE `family_audit_logs`
  DROP CHECK `ck_family_audit_action`,
  DROP CHECK `ck_family_audit_target_type`,
  ADD CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record','confirmation_confirm','confirmation_deny','confirmation_batch','activity_undo','favorite_create','favorite_update','favorite_delete','favorite_import','connector_authorize_started','connector_authorize_completed','connector_authorize_revoked','tenant_member_role_changed','tenant_member_status_changed','tenant_invitation_created','tenant_invitation_revoked','tenant_invitation_accepted','tenant_owner_transferred','web_navigation_preference_updated','conversation_create','conversation_rename','conversation_delete','skill_run_created','skill_action_confirmed','skill_draft_registered')),
  ADD CONSTRAINT `ck_family_audit_target_type` CHECK (`target_type` IN ('family_member','family_knowledge','decision_history','confirmation_item','steward_activity','personal_favorite','connector_authorization','tenant_member','tenant_invitation','web_navigation_preference','conversation','skill_run','skill_draft'));
