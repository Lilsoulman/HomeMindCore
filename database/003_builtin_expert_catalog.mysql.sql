-- Apply after 002_expert_workbench_and_tenancy.mysql.sql.
USE `nexus_mind`;

INSERT INTO `experts` (`tenant_id`,`code`,`name`,`category`,`expert_type`,`status`,`description`,`privacy_scope_json`)
VALUES
  (1,'goal-decomposition','Goal decomposition coach','planning','builtin','active','Turns goals into measurable steps.','["todos","plans"]'),
  (1,'weekly-planner','Daily and weekly planner','planning','builtin','active','Builds realistic weekly plans.','["todos","calendar_events","plans"]'),
  (1,'review-analyst','Personal review analyst','review','builtin','active','Summarizes progress and improvement opportunities.','["todos","calendar_events"]')
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`description`=VALUES(`description`),`status`=VALUES(`status`);

INSERT INTO `expert_versions` (`tenant_id`,`expert_id`,`version`,`status`,`persona`,`methodology`,`prompt_template`,`tool_policy_json`,`output_schema_json`,`estimated_credits`)
SELECT 1, e.id, 1, 'published', e.name, 'Use concise, practical, user-approved actions.', 'Return a user-safe summary and structured action items.', '[]', '{"type":"object"}', 1.0000
FROM `experts` e
WHERE e.tenant_id=1 AND e.code IN ('goal-decomposition','weekly-planner','review-analyst')
  AND NOT EXISTS (SELECT 1 FROM `expert_versions` v WHERE v.expert_id=e.id AND v.version=1);

INSERT INTO `expert_groups` (`tenant_id`,`code`,`name`,`category`,`captain_expert_id`,`status`,`description`)
SELECT 1,'weekly-planning-group','Weekly planning group','planning',e.id,'active','A captain-led weekly planning workflow.'
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
