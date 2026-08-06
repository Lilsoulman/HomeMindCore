-- Apply after 017_v2.3_personal_favorites.mysql.sql.
-- B17: calendar create-event actions have no connector/device dimension;
-- relax the two FK columns so the shared Run Action audit trail can be reused.
USE `nexus_mind`;

ALTER TABLE `action_execution_audits`
  MODIFY `workspace_connector_id` BIGINT NULL,
  MODIFY `device_id` BIGINT NULL;
