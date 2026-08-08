-- Apply after 029_quick_edit_skill.mysql.sql.
-- B26 xhs Connector baseline (V2.6 小红书个人级 Connector):
--   connector_providers 注册 xhs（小红书）个人级 Connector Provider（provider=xhs_mcp，connector_type=social）；
--   family_audit_logs CHECK 扩展 xhs_note_published 动作与 xhs_note 目标（B27 发布消费，一次到位）；
-- 无表结构变更：扫码登录态复用 connector_authorization_sessions（state/initiator_user_id），
-- 凭据由本地 MCP 进程（xhs-mcp）管理，credential_ref 仅存 local:// 会话标识，不落 cookie 明文。
USE `nexus_mind`;

-- B26 种子：小红书个人级 Connector。登录为 Puppeteer 扫码登录（非 OAuth code 交换），
-- 工具经本地 stdio MCP（xhs-mcp）调用；搜索只读 L1，发布 L2。
INSERT INTO `connector_providers` (`code`, `name`, `provider`, `connector_type`, `status`, `description`)
VALUES ('xhs', '小红书', 'xhs_mcp', 'social', 'active', '小红书个人级 Connector：经本地 stdio MCP（xhs-mcp）搜索笔记、查看详情与发布图文/视频笔记；扫码登录，凭据由本机 MCP 进程管理，不落库明文。')
ON DUPLICATE KEY UPDATE
  `name` = VALUES(`name`),
  `provider` = VALUES(`provider`),
  `connector_type` = VALUES(`connector_type`),
  `status` = VALUES(`status`),
  `description` = VALUES(`description`),
  `deleted_at` = NULL;

-- B26 扩展 family_audit_logs：小红书笔记发布审计动作与目标类型（B27 消费，无新迁移）。
ALTER TABLE `family_audit_logs`
  DROP CHECK `ck_family_audit_action`,
  DROP CHECK `ck_family_audit_target_type`,
  ADD CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record','confirmation_confirm','confirmation_deny','confirmation_batch','activity_undo','favorite_create','favorite_update','favorite_delete','favorite_import','connector_authorize_started','connector_authorize_completed','connector_authorize_revoked','tenant_member_role_changed','tenant_member_status_changed','tenant_invitation_created','tenant_invitation_revoked','tenant_invitation_accepted','tenant_owner_transferred','web_navigation_preference_updated','conversation_create','conversation_rename','conversation_delete','skill_run_created','skill_action_confirmed','skill_draft_registered','xhs_note_published')),
  ADD CONSTRAINT `ck_family_audit_target_type` CHECK (`target_type` IN ('family_member','family_knowledge','decision_history','confirmation_item','steward_activity','personal_favorite','connector_authorization','tenant_member','tenant_invitation','web_navigation_preference','conversation','skill_run','skill_draft','xhs_note'));
