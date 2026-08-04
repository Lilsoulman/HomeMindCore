-- Apply after 002_expert_workbench_and_tenancy.mysql.sql.
USE `nexus_mind`;

INSERT INTO `experts` (`tenant_id`,`code`,`name`,`category`,`expert_type`,`status`,`description`,`privacy_scope_json`)
VALUES
  (1,'goal-decomposition','目标拆解教练','planning','builtin','active','将目标拆分为可衡量、可执行的步骤。','["todos","plans"]'),
  (1,'weekly-planner','日周计划师','planning','builtin','active','制定符合实际情况的周计划与日计划。','["todos","calendar_events","plans"]'),
  (1,'review-analyst','个人复盘分析师','review','builtin','active','总结进展并给出改进建议。','["todos","calendar_events"]')
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`description`=VALUES(`description`),`status`=VALUES(`status`);

INSERT INTO `expert_versions` (`tenant_id`,`expert_id`,`version`,`status`,`persona`,`methodology`,`prompt_template`,`tool_policy_json`,`output_schema_json`,`estimated_credits`)
SELECT 1, e.id, 1, 'published', e.name, '使用简洁、务实且经用户确认的行动建议。', '返回对用户安全的总结和结构化行动项。', '[]', '{"type":"object"}', 1.0000
FROM `experts` e
WHERE e.tenant_id=1 AND e.code IN ('goal-decomposition','weekly-planner','review-analyst')
  AND NOT EXISTS (SELECT 1 FROM `expert_versions` v WHERE v.expert_id=e.id AND v.version=1);

INSERT INTO `expert_groups` (`tenant_id`,`code`,`name`,`category`,`captain_expert_id`,`status`,`description`)
SELECT 1,'weekly-planning-group','周计划专家团','planning',e.id,'active','由队长专家协调的周计划工作流。'
FROM `experts` e WHERE e.tenant_id=1 AND e.code='weekly-planner'
  AND NOT EXISTS (SELECT 1 FROM `expert_groups` g WHERE g.tenant_id=1 AND g.code='weekly-planning-group');

INSERT INTO `expert_group_versions` (`tenant_id`,`group_id`,`version`,`status`,`orchestration_policy_json`,`output_schema_json`,`estimated_credits`)
SELECT 1,g.id,1,'published','{"mode":"captain-led-dag"}','{"type":"object"}',2.5000
FROM `expert_groups` g WHERE g.tenant_id=1 AND g.code='weekly-planning-group'
  AND NOT EXISTS (SELECT 1 FROM `expert_group_versions` v WHERE v.group_id=g.id AND v.version=1);

INSERT INTO `expert_group_members` (`tenant_id`,`group_version_id`,`expert_version_id`,`role`,`order_no`,`is_required`)
SELECT 1,gv.id,ev.id,CASE WHEN e.code='weekly-planner' THEN 'captain' ELSE 'member' END,CASE WHEN e.code='weekly-planner' THEN 1 ELSE 2 END,1
FROM `expert_group_versions` gv
JOIN `expert_groups` g ON g.id=gv.group_id AND g.code='weekly-planning-group'
JOIN `expert_versions` ev ON ev.tenant_id=1 AND ev.version=1
JOIN `experts` e ON e.id=ev.expert_id AND e.code IN ('weekly-planner','goal-decomposition')
WHERE gv.version=1
  AND NOT EXISTS (SELECT 1 FROM `expert_group_members` m WHERE m.group_version_id=gv.id AND m.expert_version_id=ev.id);
