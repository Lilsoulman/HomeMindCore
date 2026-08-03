-- Apply after 001_mobile_initial_schema.mysql.sql.
-- MySQL 8.0+, all DATETIME(3) timestamps are UTC.
-- This migration is safe for a fresh baseline and for existing user data. It
-- adds tenant columns as nullable, creates one personal tenant per user,
-- backfills data, then makes columns NOT NULL and adds foreign keys.
USE `nexus_mind`;

CREATE TABLE `tenants` (
  `id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_type` VARCHAR(16) NOT NULL DEFAULT 'personal', `code` VARCHAR(64) NOT NULL,
  `name` VARCHAR(128) NOT NULL, `status` VARCHAR(16) NOT NULL DEFAULT 'active', `owner_user_id` BIGINT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3), `row_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`), UNIQUE KEY `uk_tenant_code` (`code`), CONSTRAINT `fk_tenant_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `ck_tenant_type` CHECK (`tenant_type` IN ('system','personal','team')), CONSTRAINT `ck_tenant_status` CHECK (`status` IN ('active','suspended','deleted'))
) ENGINE=InnoDB;
CREATE TABLE `tenant_members` (
  `tenant_id` BIGINT NOT NULL, `user_id` BIGINT NOT NULL, `role` VARCHAR(16) NOT NULL DEFAULT 'member', `status` VARCHAR(16) NOT NULL DEFAULT 'active',
  `joined_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`tenant_id`,`user_id`), KEY `idx_member_user` (`user_id`,`status`), CONSTRAINT `fk_member_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`) ON DELETE CASCADE, CONSTRAINT `fk_member_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  CONSTRAINT `ck_member_role` CHECK (`role` IN ('owner','admin','member','viewer'))
) ENGINE=InnoDB;
-- Seed once before creating built-in catalog rows. Application bootstrap creates
-- one personal tenant plus owner membership for every newly registered user.
INSERT INTO `tenants` (`id`,`tenant_type`,`code`,`name`,`status`) VALUES (1,'system','system','HomeMind System','active') ON DUPLICATE KEY UPDATE `name`=VALUES(`name`);

-- Legacy-safe tenancy bootstrap. `user-{id}` is an internal, stable code; it
-- must not be exposed as a user-selectable tenant identifier.
INSERT INTO `tenants` (`tenant_type`,`code`,`name`,`status`,`owner_user_id`)
SELECT 'personal', CONCAT('user-', `u`.`id`), CONCAT('Personal workspace ', `u`.`id`), 'active', `u`.`id`
FROM `users` `u`
LEFT JOIN `tenants` `t` ON `t`.`code` = CONCAT('user-', `u`.`id`)
WHERE `t`.`id` IS NULL;
INSERT INTO `tenant_members` (`tenant_id`,`user_id`,`role`,`status`)
SELECT `t`.`id`, `u`.`id`, 'owner', 'active'
FROM `users` `u`
INNER JOIN `tenants` `t` ON `t`.`code` = CONCAT('user-', `u`.`id`)
LEFT JOIN `tenant_members` `m` ON `m`.`tenant_id` = `t`.`id` AND `m`.`user_id` = `u`.`id`
WHERE `m`.`user_id` IS NULL;

-- Tenant scope is derived from the access token, never accepted from the client.
ALTER TABLE `todo_lists` ADD COLUMN `tenant_id` BIGINT NULL AFTER `id`, ADD KEY `idx_list_tenant` (`tenant_id`);
ALTER TABLE `todos` ADD COLUMN `tenant_id` BIGINT NULL AFTER `id`, ADD COLUMN `source_type` VARCHAR(32) NULL AFTER `report_ignored`, ADD COLUMN `source_run_id` BIGINT NULL AFTER `source_type`, ADD COLUMN `source_action_id` BIGINT NULL AFTER `source_run_id`, ADD KEY `idx_todo_tenant_updated` (`tenant_id`,`updated_at`), ADD KEY `idx_todo_source_run` (`source_run_id`);
ALTER TABLE `subtasks` ADD COLUMN `tenant_id` BIGINT NULL AFTER `id`, ADD KEY `idx_subtask_tenant` (`tenant_id`);
-- `fk_tag_user` originally reuses `uk_tag_name` for its user_id index, so an
-- explicit replacement must exist before the legacy unique key is removed.
ALTER TABLE `todo_tags` ADD COLUMN `tenant_id` BIGINT NULL AFTER `id`, ADD KEY `idx_tag_user` (`user_id`), DROP INDEX `uk_tag_name`, ADD UNIQUE KEY `uk_tag_tenant_user_name` (`tenant_id`,`user_id`,`name`), ADD KEY `idx_tag_tenant` (`tenant_id`);
ALTER TABLE `todo_tag_links` ADD COLUMN `tenant_id` BIGINT NULL FIRST, ADD KEY `idx_tag_link_tenant` (`tenant_id`);
ALTER TABLE `attachments` ADD COLUMN `tenant_id` BIGINT NULL AFTER `id`, ADD KEY `idx_attachment_tenant` (`tenant_id`);
ALTER TABLE `calendar_events` ADD COLUMN `tenant_id` BIGINT NULL AFTER `id`, ADD COLUMN `source_type` VARCHAR(32) NULL AFTER `repeat_rule`, ADD COLUMN `source_run_id` BIGINT NULL AFTER `source_type`, ADD COLUMN `source_action_id` BIGINT NULL AFTER `source_run_id`, ADD KEY `idx_event_tenant_start` (`tenant_id`,`start_at`), ADD KEY `idx_event_source_run` (`source_run_id`);
ALTER TABLE `calendar_event_exceptions` ADD COLUMN `tenant_id` BIGINT NULL AFTER `id`, ADD KEY `idx_exception_tenant` (`tenant_id`);
-- `fk_subscription_user` originally reuses `uk_subscription_url`; retain a
-- dedicated user_id index before replacing the legacy unique key.
ALTER TABLE `calendar_subscriptions` ADD COLUMN `tenant_id` BIGINT NULL AFTER `id`, ADD KEY `idx_subscription_user` (`user_id`), DROP INDEX `uk_subscription_url`, ADD UNIQUE KEY `uk_subscription_tenant_user_url` (`tenant_id`,`user_id`,`source_url_hash`), ADD KEY `idx_subscription_tenant` (`tenant_id`);
ALTER TABLE `ical_overrides` ADD COLUMN `tenant_id` BIGINT NULL AFTER `id`, ADD KEY `idx_ical_override_tenant` (`tenant_id`);
ALTER TABLE `ai_skills` ADD COLUMN `tenant_id` BIGINT NULL AFTER `id`, ADD KEY `idx_skill_tenant` (`tenant_id`);
ALTER TABLE `ai_call_logs` ADD COLUMN `tenant_id` BIGINT NULL AFTER `id`, ADD KEY `idx_ailog_tenant_time` (`tenant_id`,`created_at`);
ALTER TABLE `sync_change_log` ADD COLUMN `tenant_id` BIGINT NULL AFTER `user_id`, ADD KEY `idx_change_tenant_cursor` (`tenant_id`,`sync_version`);

-- Backfill direct user-owned rows first, then rows which inherit their tenant
-- from a parent record. This completes before any tenant foreign key exists.
UPDATE `todo_lists` `x` INNER JOIN `tenants` `t` ON `t`.`code`=CONCAT('user-',`x`.`user_id`) SET `x`.`tenant_id`=`t`.`id` WHERE `x`.`tenant_id` IS NULL;
UPDATE `todos` `x` INNER JOIN `tenants` `t` ON `t`.`code`=CONCAT('user-',`x`.`user_id`) SET `x`.`tenant_id`=`t`.`id` WHERE `x`.`tenant_id` IS NULL;
UPDATE `subtasks` `x` INNER JOIN `todos` `p` ON `p`.`id`=`x`.`todo_id` SET `x`.`tenant_id`=`p`.`tenant_id` WHERE `x`.`tenant_id` IS NULL;
UPDATE `todo_tags` `x` INNER JOIN `tenants` `t` ON `t`.`code`=CONCAT('user-',`x`.`user_id`) SET `x`.`tenant_id`=`t`.`id` WHERE `x`.`tenant_id` IS NULL;
UPDATE `todo_tag_links` `x` INNER JOIN `todos` `p` ON `p`.`id`=`x`.`todo_id` SET `x`.`tenant_id`=`p`.`tenant_id` WHERE `x`.`tenant_id` IS NULL;
UPDATE `attachments` `x` INNER JOIN `tenants` `t` ON `t`.`code`=CONCAT('user-',`x`.`user_id`) SET `x`.`tenant_id`=`t`.`id` WHERE `x`.`tenant_id` IS NULL;
UPDATE `calendar_events` `x` INNER JOIN `tenants` `t` ON `t`.`code`=CONCAT('user-',`x`.`user_id`) SET `x`.`tenant_id`=`t`.`id` WHERE `x`.`tenant_id` IS NULL;
UPDATE `calendar_event_exceptions` `x` INNER JOIN `calendar_events` `p` ON `p`.`id`=`x`.`event_id` SET `x`.`tenant_id`=`p`.`tenant_id` WHERE `x`.`tenant_id` IS NULL;
UPDATE `calendar_subscriptions` `x` INNER JOIN `tenants` `t` ON `t`.`code`=CONCAT('user-',`x`.`user_id`) SET `x`.`tenant_id`=`t`.`id` WHERE `x`.`tenant_id` IS NULL;
UPDATE `ical_overrides` `x` INNER JOIN `tenants` `t` ON `t`.`code`=CONCAT('user-',`x`.`user_id`) SET `x`.`tenant_id`=`t`.`id` WHERE `x`.`tenant_id` IS NULL;
UPDATE `ai_skills` `x` INNER JOIN `tenants` `t` ON `t`.`code`=CONCAT('user-',`x`.`user_id`) SET `x`.`tenant_id`=`t`.`id` WHERE `x`.`tenant_id` IS NULL;
UPDATE `ai_call_logs` `x` INNER JOIN `tenants` `t` ON `t`.`code`=CONCAT('user-',`x`.`user_id`) SET `x`.`tenant_id`=`t`.`id` WHERE `x`.`tenant_id` IS NULL;
UPDATE `sync_change_log` `x` INNER JOIN `tenants` `t` ON `t`.`code`=CONCAT('user-',`x`.`user_id`) SET `x`.`tenant_id`=`t`.`id` WHERE `x`.`tenant_id` IS NULL;

ALTER TABLE `todo_lists` MODIFY COLUMN `tenant_id` BIGINT NOT NULL, ADD CONSTRAINT `fk_list_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`);
ALTER TABLE `todos` MODIFY COLUMN `tenant_id` BIGINT NOT NULL, ADD CONSTRAINT `fk_todo_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`);
ALTER TABLE `subtasks` MODIFY COLUMN `tenant_id` BIGINT NOT NULL, ADD CONSTRAINT `fk_subtask_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`);
ALTER TABLE `todo_tags` MODIFY COLUMN `tenant_id` BIGINT NOT NULL, ADD CONSTRAINT `fk_tag_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`);
ALTER TABLE `todo_tag_links` MODIFY COLUMN `tenant_id` BIGINT NOT NULL, ADD CONSTRAINT `fk_tag_link_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`);
ALTER TABLE `attachments` MODIFY COLUMN `tenant_id` BIGINT NOT NULL, ADD CONSTRAINT `fk_attachment_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`);
ALTER TABLE `calendar_events` MODIFY COLUMN `tenant_id` BIGINT NOT NULL, ADD CONSTRAINT `fk_event_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`);
ALTER TABLE `calendar_event_exceptions` MODIFY COLUMN `tenant_id` BIGINT NOT NULL, ADD CONSTRAINT `fk_exception_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`);
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `tenant_id` BIGINT NOT NULL, ADD CONSTRAINT `fk_subscription_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`);
ALTER TABLE `ical_overrides` MODIFY COLUMN `tenant_id` BIGINT NOT NULL, ADD CONSTRAINT `fk_ical_override_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`);
ALTER TABLE `ai_skills` MODIFY COLUMN `tenant_id` BIGINT NOT NULL, ADD CONSTRAINT `fk_skill_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`);
ALTER TABLE `ai_call_logs` MODIFY COLUMN `tenant_id` BIGINT NOT NULL, ADD CONSTRAINT `fk_ailog_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`);
ALTER TABLE `sync_change_log` MODIFY COLUMN `tenant_id` BIGINT NOT NULL, ADD CONSTRAINT `fk_change_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`);

-- Plans are first-class because an expert result can be saved as a plan before
-- its recommended actions are accepted as todos or calendar events.
CREATE TABLE `plans` (
  `id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_id` BIGINT NOT NULL, `user_id` BIGINT NOT NULL, `title` VARCHAR(255) NOT NULL, `description` TEXT NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'active', `start_at` DATETIME(3) NULL, `target_at` DATETIME(3) NULL,
  `source_type` VARCHAR(32) NULL, `source_run_id` BIGINT NULL, `source_action_id` BIGINT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3), `deleted_at` DATETIME(3) NULL, `sync_version` BIGINT NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`), KEY `idx_plan_tenant_updated` (`tenant_id`,`updated_at`), KEY `idx_plan_user` (`user_id`,`status`), KEY `idx_plan_source_run` (`source_run_id`), CONSTRAINT `fk_plan_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_plan_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  CONSTRAINT `ck_plan_status` CHECK (`status` IN ('draft','active','completed','archived'))
) ENGINE=InnoDB;
CREATE TABLE `plan_items` (
  `id` BIGINT NOT NULL AUTO_INCREMENT, `plan_id` BIGINT NOT NULL, `tenant_id` BIGINT NOT NULL, `item_type` VARCHAR(16) NOT NULL, `title` VARCHAR(255) NOT NULL,
  `todo_id` BIGINT NULL, `calendar_event_id` BIGINT NULL, `sort_order` INT NOT NULL DEFAULT 0, `metadata_json` JSON NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3), `deleted_at` DATETIME(3) NULL, `sync_version` BIGINT NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`), KEY `idx_plan_item_plan` (`plan_id`,`sort_order`), CONSTRAINT `fk_plan_item_plan` FOREIGN KEY (`plan_id`) REFERENCES `plans` (`id`) ON DELETE CASCADE, CONSTRAINT `fk_plan_item_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_plan_item_todo` FOREIGN KEY (`todo_id`) REFERENCES `todos` (`id`), CONSTRAINT `fk_plan_item_event` FOREIGN KEY (`calendar_event_id`) REFERENCES `calendar_events` (`id`),
  CONSTRAINT `ck_plan_item_type` CHECK (`item_type` IN ('note','todo','calendar_event'))
) ENGINE=InnoDB;

-- Catalog versions are immutable after publication. Built-ins belong to tenant 1;
-- tenant custom catalog rows are future-ready but are not exposed in M6.1/M6.2.
CREATE TABLE `experts` (
  `id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_id` BIGINT NOT NULL, `owner_user_id` BIGINT NULL, `code` VARCHAR(64) NOT NULL, `name` VARCHAR(128) NOT NULL, `category` VARCHAR(32) NOT NULL,
  `expert_type` VARCHAR(16) NOT NULL DEFAULT 'builtin', `status` VARCHAR(16) NOT NULL DEFAULT 'active', `description` TEXT NULL, `privacy_scope_json` JSON NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3), `row_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`), UNIQUE KEY `uk_expert_tenant_code` (`tenant_id`,`code`), KEY `idx_expert_catalog` (`tenant_id`,`status`,`category`), CONSTRAINT `fk_expert_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_expert_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `ck_expert_type` CHECK (`expert_type` IN ('builtin','custom')), CONSTRAINT `ck_expert_status` CHECK (`status` IN ('draft','active','disabled','archived'))
) ENGINE=InnoDB;
CREATE TABLE `expert_versions` (
  `id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_id` BIGINT NOT NULL, `expert_id` BIGINT NOT NULL, `version` INT NOT NULL, `status` VARCHAR(16) NOT NULL DEFAULT 'published',
  `persona` TEXT NOT NULL, `methodology` TEXT NOT NULL, `prompt_template` TEXT NOT NULL, `tool_policy_json` JSON NULL, `knowledge_profile_json` JSON NULL, `output_schema_json` JSON NULL,
  `estimated_credits` DECIMAL(18,4) NOT NULL DEFAULT 0, `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`), UNIQUE KEY `uk_expert_version` (`expert_id`,`version`), KEY `idx_expert_version_tenant` (`tenant_id`), CONSTRAINT `fk_expert_version_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_expert_version_expert` FOREIGN KEY (`expert_id`) REFERENCES `experts` (`id`) ON DELETE CASCADE, CONSTRAINT `ck_expert_version_status` CHECK (`status` IN ('draft','published','retired'))
) ENGINE=InnoDB;
CREATE TABLE `expert_groups` (
  `id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_id` BIGINT NOT NULL, `owner_user_id` BIGINT NULL, `code` VARCHAR(64) NOT NULL, `name` VARCHAR(128) NOT NULL, `category` VARCHAR(32) NOT NULL,
  `captain_expert_id` BIGINT NOT NULL, `status` VARCHAR(16) NOT NULL DEFAULT 'active', `description` TEXT NULL, `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3), `row_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`), UNIQUE KEY `uk_group_tenant_code` (`tenant_id`,`code`), KEY `idx_group_catalog` (`tenant_id`,`status`,`category`), CONSTRAINT `fk_group_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_group_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`), CONSTRAINT `fk_group_captain` FOREIGN KEY (`captain_expert_id`) REFERENCES `experts` (`id`)
) ENGINE=InnoDB;
CREATE TABLE `expert_group_versions` (`id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_id` BIGINT NOT NULL, `group_id` BIGINT NOT NULL, `version` INT NOT NULL, `status` VARCHAR(16) NOT NULL DEFAULT 'published', `orchestration_policy_json` JSON NULL, `output_schema_json` JSON NULL, `estimated_credits` DECIMAL(18,4) NOT NULL DEFAULT 0, `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), PRIMARY KEY (`id`), UNIQUE KEY `uk_group_version` (`group_id`,`version`), KEY `idx_group_version_tenant` (`tenant_id`), CONSTRAINT `fk_group_version_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_group_version_group` FOREIGN KEY (`group_id`) REFERENCES `expert_groups` (`id`) ON DELETE CASCADE, CONSTRAINT `ck_group_version_status` CHECK (`status` IN ('draft','published','retired'))) ENGINE=InnoDB;
CREATE TABLE `expert_group_members` (`tenant_id` BIGINT NOT NULL, `group_version_id` BIGINT NOT NULL, `expert_version_id` BIGINT NOT NULL, `role` VARCHAR(32) NOT NULL, `order_no` INT NOT NULL DEFAULT 0, `is_required` TINYINT(1) NOT NULL DEFAULT 1, PRIMARY KEY (`group_version_id`,`expert_version_id`), KEY `idx_group_member_tenant` (`tenant_id`), CONSTRAINT `fk_group_member_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_group_member_version` FOREIGN KEY (`group_version_id`) REFERENCES `expert_group_versions` (`id`) ON DELETE CASCADE, CONSTRAINT `fk_group_member_expert_version` FOREIGN KEY (`expert_version_id`) REFERENCES `expert_versions` (`id`) ON DELETE CASCADE, CONSTRAINT `ck_group_member_role` CHECK (`role` IN ('captain','member','reviewer'))) ENGINE=InnoDB;
CREATE TABLE `user_expert_preferences` (`tenant_id` BIGINT NOT NULL, `user_id` BIGINT NOT NULL, `expert_id` BIGINT NOT NULL, `is_favorite` TINYINT(1) NOT NULL DEFAULT 0, `last_used_at` DATETIME(3) NULL, `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3), PRIMARY KEY (`tenant_id`,`user_id`,`expert_id`), CONSTRAINT `fk_preference_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_preference_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE, CONSTRAINT `fk_preference_expert` FOREIGN KEY (`expert_id`) REFERENCES `experts` (`id`) ON DELETE CASCADE) ENGINE=InnoDB;

CREATE TABLE `expert_runs` (
  `id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_id` BIGINT NOT NULL, `user_id` BIGINT NOT NULL, `source_type` VARCHAR(16) NOT NULL, `expert_version_id` BIGINT NULL, `group_version_id` BIGINT NULL,
  `request_idempotency_key` CHAR(36) NOT NULL, `input_json` JSON NOT NULL, `status` VARCHAR(16) NOT NULL DEFAULT 'draft', `plan_summary` TEXT NULL, `result_json` JSON NULL, `result_summary` TEXT NULL,
  `estimated_credits` DECIMAL(18,4) NOT NULL DEFAULT 0, `actual_credits` DECIMAL(18,4) NOT NULL DEFAULT 0, `cancel_requested_at` DATETIME(3) NULL, `started_at` DATETIME(3) NULL, `finished_at` DATETIME(3) NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3), `row_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`), UNIQUE KEY `uk_run_idempotency` (`tenant_id`,`user_id`,`request_idempotency_key`), KEY `idx_run_tenant_status` (`tenant_id`,`status`,`created_at`), KEY `idx_run_user` (`user_id`,`created_at`),
  CONSTRAINT `fk_run_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_run_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE, CONSTRAINT `fk_run_expert_version` FOREIGN KEY (`expert_version_id`) REFERENCES `expert_versions` (`id`), CONSTRAINT `fk_run_group_version` FOREIGN KEY (`group_version_id`) REFERENCES `expert_group_versions` (`id`),
  CONSTRAINT `ck_run_source` CHECK ((`source_type`='expert' AND `expert_version_id` IS NOT NULL AND `group_version_id` IS NULL) OR (`source_type`='group' AND `group_version_id` IS NOT NULL AND `expert_version_id` IS NULL)),
  CONSTRAINT `ck_run_status` CHECK (`status` IN ('draft','queued','planning','running','synthesizing','completed','failed','cancelled','needs_input'))
) ENGINE=InnoDB;
CREATE TABLE `expert_run_contexts` (`id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_id` BIGINT NOT NULL, `run_id` BIGINT NOT NULL, `context_type` VARCHAR(16) NOT NULL, `context_id` BIGINT NULL, `snapshot_json` JSON NULL, `object_key` VARCHAR(512) NULL, `sha256` BINARY(32) NULL, `mime_type` VARCHAR(127) NULL, `size_bytes` BIGINT NULL, `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), PRIMARY KEY (`id`), KEY `idx_context_tenant_run` (`tenant_id`,`run_id`), CONSTRAINT `fk_context_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_context_run` FOREIGN KEY (`run_id`) REFERENCES `expert_runs` (`id`) ON DELETE CASCADE, CONSTRAINT `ck_context_type` CHECK (`context_type` IN ('todo','plan','calendar_event','attachment','file','text'))) ENGINE=InnoDB;
CREATE TABLE `run_steps` (`id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_id` BIGINT NOT NULL, `run_id` BIGINT NOT NULL, `parent_step_id` BIGINT NULL, `expert_version_id` BIGINT NOT NULL, `step_type` VARCHAR(16) NOT NULL, `title` VARCHAR(255) NOT NULL, `status` VARCHAR(16) NOT NULL DEFAULT 'waiting', `input_json` JSON NULL, `output_json` JSON NULL, `display_summary` TEXT NULL, `attempt_no` INT NOT NULL DEFAULT 0, `started_at` DATETIME(3) NULL, `finished_at` DATETIME(3) NULL, `error_code` VARCHAR(64) NULL, `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3), `row_version` BIGINT NOT NULL DEFAULT 1, PRIMARY KEY (`id`), KEY `idx_step_tenant_run_status` (`tenant_id`,`run_id`,`status`,`created_at`), CONSTRAINT `fk_step_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_step_run` FOREIGN KEY (`run_id`) REFERENCES `expert_runs` (`id`) ON DELETE CASCADE, CONSTRAINT `fk_step_parent` FOREIGN KEY (`parent_step_id`) REFERENCES `run_steps` (`id`), CONSTRAINT `fk_step_expert_version` FOREIGN KEY (`expert_version_id`) REFERENCES `expert_versions` (`id`), CONSTRAINT `ck_step_type` CHECK (`step_type` IN ('plan','execute','synthesize','review')), CONSTRAINT `ck_step_status` CHECK (`status` IN ('waiting','queued','running','completed','failed','cancelled','needs_input'))) ENGINE=InnoDB;
CREATE TABLE `run_step_dependencies` (`tenant_id` BIGINT NOT NULL, `step_id` BIGINT NOT NULL, `depends_on_step_id` BIGINT NOT NULL, PRIMARY KEY (`step_id`,`depends_on_step_id`), KEY `idx_dependency_tenant` (`tenant_id`), CONSTRAINT `fk_dependency_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_dependency_step` FOREIGN KEY (`step_id`) REFERENCES `run_steps` (`id`) ON DELETE CASCADE, CONSTRAINT `fk_dependency_parent` FOREIGN KEY (`depends_on_step_id`) REFERENCES `run_steps` (`id`) ON DELETE CASCADE, CONSTRAINT `ck_dependency_not_self` CHECK (`step_id` <> `depends_on_step_id`)) ENGINE=InnoDB;
CREATE TABLE `expert_jobs` (`id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_id` BIGINT NOT NULL, `run_id` BIGINT NOT NULL, `step_id` BIGINT NULL, `job_type` VARCHAR(16) NOT NULL, `status` VARCHAR(16) NOT NULL DEFAULT 'queued', `idempotency_key` CHAR(36) NOT NULL, `available_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), `leased_until` DATETIME(3) NULL, `attempt_no` INT NOT NULL DEFAULT 0, `last_error_code` VARCHAR(64) NULL, `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3), PRIMARY KEY (`id`), UNIQUE KEY `uk_job_idempotency` (`tenant_id`,`idempotency_key`), KEY `idx_job_dequeue` (`status`,`available_at`), CONSTRAINT `fk_job_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_job_run` FOREIGN KEY (`run_id`) REFERENCES `expert_runs` (`id`) ON DELETE CASCADE, CONSTRAINT `fk_job_step` FOREIGN KEY (`step_id`) REFERENCES `run_steps` (`id`) ON DELETE CASCADE, CONSTRAINT `ck_job_type` CHECK (`job_type` IN ('plan','execute','synthesize','retry')), CONSTRAINT `ck_job_status` CHECK (`status` IN ('queued','leased','completed','failed','cancelled'))) ENGINE=InnoDB;
CREATE TABLE `run_events` (`id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_id` BIGINT NOT NULL, `run_id` BIGINT NOT NULL, `step_id` BIGINT NULL, `sequence` INT NOT NULL, `event_type` VARCHAR(32) NOT NULL, `display_payload_json` JSON NOT NULL, `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), PRIMARY KEY (`id`), UNIQUE KEY `uk_event_sequence` (`run_id`,`sequence`), KEY `idx_event_tenant_run` (`tenant_id`,`run_id`,`sequence`), CONSTRAINT `fk_run_event_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_event_run` FOREIGN KEY (`run_id`) REFERENCES `expert_runs` (`id`) ON DELETE CASCADE, CONSTRAINT `fk_event_step` FOREIGN KEY (`step_id`) REFERENCES `run_steps` (`id`) ON DELETE SET NULL) ENGINE=InnoDB;
CREATE TABLE `run_artifacts` (`id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_id` BIGINT NOT NULL, `run_id` BIGINT NOT NULL, `step_id` BIGINT NULL, `object_key` VARCHAR(512) NOT NULL, `sha256` BINARY(32) NOT NULL, `mime_type` VARCHAR(127) NOT NULL, `size_bytes` BIGINT NOT NULL, `metadata_json` JSON NULL, `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), PRIMARY KEY (`id`), UNIQUE KEY `uk_artifact_object` (`object_key`), KEY `idx_artifact_tenant_run` (`tenant_id`,`run_id`), CONSTRAINT `fk_artifact_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_artifact_run` FOREIGN KEY (`run_id`) REFERENCES `expert_runs` (`id`) ON DELETE CASCADE, CONSTRAINT `fk_artifact_step` FOREIGN KEY (`step_id`) REFERENCES `run_steps` (`id`) ON DELETE SET NULL) ENGINE=InnoDB;
CREATE TABLE `run_step_usage` (`id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_id` BIGINT NOT NULL, `run_id` BIGINT NOT NULL, `step_id` BIGINT NULL, `provider` VARCHAR(32) NOT NULL, `model` VARCHAR(128) NULL, `request_id_hash` BINARY(32) NULL, `input_tokens` INT NOT NULL DEFAULT 0, `output_tokens` INT NOT NULL DEFAULT 0, `credits` DECIMAL(18,4) NOT NULL DEFAULT 0, `latency_ms` INT NULL, `status` VARCHAR(16) NOT NULL, `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), PRIMARY KEY (`id`), KEY `idx_usage_tenant_run` (`tenant_id`,`run_id`,`created_at`), CONSTRAINT `fk_usage_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_usage_run` FOREIGN KEY (`run_id`) REFERENCES `expert_runs` (`id`) ON DELETE CASCADE, CONSTRAINT `fk_usage_step` FOREIGN KEY (`step_id`) REFERENCES `run_steps` (`id`) ON DELETE SET NULL) ENGINE=InnoDB;
CREATE TABLE `credit_ledger` (`id` BIGINT NOT NULL AUTO_INCREMENT, `tenant_id` BIGINT NOT NULL, `user_id` BIGINT NOT NULL, `run_id` BIGINT NULL, `entry_type` VARCHAR(16) NOT NULL, `amount` DECIMAL(18,4) NOT NULL, `idempotency_key` CHAR(36) NOT NULL, `metadata_json` JSON NULL, `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), PRIMARY KEY (`id`), UNIQUE KEY `uk_credit_idempotency` (`tenant_id`,`idempotency_key`), KEY `idx_credit_user` (`user_id`,`created_at`), CONSTRAINT `fk_credit_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_credit_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE, CONSTRAINT `fk_credit_run` FOREIGN KEY (`run_id`) REFERENCES `expert_runs` (`id`) ON DELETE SET NULL, CONSTRAINT `ck_credit_type` CHECK (`entry_type` IN ('estimate','hold','charge','refund','adjustment'))) ENGINE=InnoDB;
CREATE TABLE `expert_run_actions` (`id` BIGINT NOT NULL AUTO_INCREMENT, `run_id` BIGINT NOT NULL, `tenant_id` BIGINT NOT NULL, `user_id` BIGINT NOT NULL, `action_type` VARCHAR(16) NOT NULL, `request_idempotency_key` CHAR(36) NOT NULL, `request_json` JSON NOT NULL, `status` VARCHAR(16) NOT NULL DEFAULT 'queued', `result_json` JSON NULL, `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3), `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3), PRIMARY KEY (`id`), UNIQUE KEY `uk_run_action_idempotency` (`run_id`,`request_idempotency_key`), CONSTRAINT `fk_action_run` FOREIGN KEY (`run_id`) REFERENCES `expert_runs` (`id`) ON DELETE CASCADE, CONSTRAINT `fk_action_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`), CONSTRAINT `fk_action_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE, CONSTRAINT `ck_action_type` CHECK (`action_type` IN ('plan','todos','calendar_events')), CONSTRAINT `ck_action_status` CHECK (`status` IN ('queued','processing','completed','failed'))) ENGINE=InnoDB;

ALTER TABLE `todos` ADD CONSTRAINT `fk_todo_source_run` FOREIGN KEY (`source_run_id`) REFERENCES `expert_runs` (`id`) ON DELETE SET NULL, ADD CONSTRAINT `fk_todo_source_action` FOREIGN KEY (`source_action_id`) REFERENCES `expert_run_actions` (`id`) ON DELETE SET NULL;
ALTER TABLE `calendar_events` ADD CONSTRAINT `fk_event_source_run` FOREIGN KEY (`source_run_id`) REFERENCES `expert_runs` (`id`) ON DELETE SET NULL, ADD CONSTRAINT `fk_event_source_action` FOREIGN KEY (`source_action_id`) REFERENCES `expert_run_actions` (`id`) ON DELETE SET NULL;
ALTER TABLE `plans` ADD CONSTRAINT `fk_plan_source_run` FOREIGN KEY (`source_run_id`) REFERENCES `expert_runs` (`id`) ON DELETE SET NULL, ADD CONSTRAINT `fk_plan_source_action` FOREIGN KEY (`source_action_id`) REFERENCES `expert_run_actions` (`id`) ON DELETE SET NULL;
