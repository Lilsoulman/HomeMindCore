-- Apply after 010_confirmed_smart_home_actions.mysql.sql.
-- The physical table name remains expert_runs for compatibility with existing
-- Todo, Calendar, Action and audit foreign keys. Its domain model is AgentRun.
USE `nexus_mind`;

UPDATE `expert_runs`
SET `status` = CASE `status`
  WHEN 'synthesizing' THEN 'running'
  WHEN 'needs_input' THEN 'failed'
  ELSE `status`
END
WHERE `status` IN ('synthesizing', 'needs_input');

ALTER TABLE `expert_runs`
  DROP CHECK `ck_run_status`;

ALTER TABLE `expert_runs`
  ADD CONSTRAINT `ck_run_status`
  CHECK (`status` IN ('draft', 'queued', 'planning', 'running', 'completed', 'failed', 'cancelled'));
