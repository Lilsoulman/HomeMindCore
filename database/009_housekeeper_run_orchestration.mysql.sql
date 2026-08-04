-- Apply after 008_connector_provider_catalog.mysql.sql.
USE `nexus_mind`;

-- Existing Run Action storage is reused. This only expands its state machine for
-- smart-home drafts; execution remains intentionally out of scope for phase 4.
ALTER TABLE `expert_run_actions`
  DROP CHECK `ck_action_type`,
  DROP CHECK `ck_action_status`;

ALTER TABLE `expert_run_actions`
  ADD CONSTRAINT `ck_action_type` CHECK (`action_type` IN ('plan','todos','calendar_events','smart_home_device')),
  ADD CONSTRAINT `ck_action_status` CHECK (`status` IN ('queued','processing','completed','failed','pending','confirmed','rejected','executing','executed','cancelled'));

INSERT INTO `experts` (`tenant_id`,`code`,`name`,`category`,`expert_type`,`status`,`description`,`privacy_scope_json`)
VALUES (1,'family-housekeeper','家庭管家','smart_home','builtin','active','基于已同步的家庭状态生成需确认的行动建议。','["smart_home"]')
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `description`=VALUES(`description`), `status`=VALUES(`status`);

INSERT INTO `expert_versions` (`tenant_id`,`expert_id`,`version`,`status`,`persona`,`methodology`,`prompt_template`,`tool_policy_json`,`output_schema_json`,`estimated_credits`)
SELECT 1,e.id,1,'published','家庭管家','仅使用已同步的标准化家庭读模型；设备变更只生成待确认草案。','返回面向用户的家庭状态摘要和需确认的行动建议。','{"skills":["smart-home.read"],"writeActionsRequireConfirmation":true}','{"type":"object"}',1.0000
FROM `experts` e
WHERE e.tenant_id=1 AND e.code='family-housekeeper'
  AND NOT EXISTS (SELECT 1 FROM `expert_versions` v WHERE v.expert_id=e.id AND v.version=1);
