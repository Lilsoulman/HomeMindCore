-- Apply after 039_learning_memory.mysql.sql.
-- Publish a new immutable version for the built-in review analyst; do not mutate prior versions.
USE `nexus_mind`;

INSERT INTO `expert_versions`
  (`tenant_id`,`expert_id`,`version`,`status`,`persona`,`methodology`,`prompt_template`,`tool_policy_json`,`output_schema_json`,`estimated_credits`)
SELECT
  current_version.`tenant_id`, current_version.`expert_id`, current_version.`version` + 1, 'published',
  current_version.`persona`, current_version.`methodology`, current_version.`prompt_template`, current_version.`tool_policy_json`,
  JSON_MERGE_PATCH(
    COALESCE(current_version.`output_schema_json`, JSON_OBJECT()),
    JSON_OBJECT('type', 'object', 'properties', JSON_OBJECT('memoryCandidates', JSON_OBJECT('type', 'array', 'maxItems', 10)))
  ),
  current_version.`estimated_credits`
FROM `experts` expert
JOIN `expert_versions` current_version ON current_version.`expert_id` = expert.`id`
WHERE expert.`tenant_id` = 1
  AND expert.`code` = 'review-analyst'
  AND expert.`status` = 'active'
  AND current_version.`status` = 'published'
  AND current_version.`id` = (
    SELECT latest_version.`id`
    FROM `expert_versions` latest_version
    WHERE latest_version.`expert_id` = expert.`id` AND latest_version.`status` = 'published'
    ORDER BY latest_version.`version` DESC
    LIMIT 1
  )
  AND NOT EXISTS (
    SELECT 1
    FROM `expert_versions` memory_enabled
    WHERE memory_enabled.`expert_id` = expert.`id`
      AND JSON_CONTAINS_PATH(COALESCE(memory_enabled.`output_schema_json`, JSON_OBJECT()), 'one', '$.properties.memoryCandidates')
  );
