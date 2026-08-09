-- Apply after 033_clipping_materials.mysql.sql.
-- B31 方案修订（V2.7 对话式优化）：
--   family_audit_logs action CHECK 扩展 skill_run_revised（目标复用既有 skill_run，无需扩展 target_type）
-- 无表结构变更（无 EF 迁移，同 B23/B25 先例：种子与 CHECK 由 SQL 管理）。
USE `nexus_mind`;

ALTER TABLE `family_audit_logs`
  DROP CHECK `ck_family_audit_action`,
  ADD CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record','confirmation_confirm','confirmation_deny','confirmation_batch','activity_undo','favorite_create','favorite_update','favorite_delete','favorite_import','connector_authorize_started','connector_authorize_completed','connector_authorize_revoked','tenant_member_role_changed','tenant_member_status_changed','tenant_invitation_created','tenant_invitation_revoked','tenant_invitation_accepted','tenant_owner_transferred','web_navigation_preference_updated','conversation_create','conversation_rename','conversation_delete','skill_run_created','skill_action_confirmed','skill_draft_registered','xhs_note_published','media_file_uploaded','media_file_deleted','skill_run_revised'));
