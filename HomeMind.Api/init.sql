CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    ALTER DATABASE CHARACTER SET utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `action_execution_audits` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `run_action_id` bigint NOT NULL,
        `operator_user_id` bigint NOT NULL,
        `workspace_connector_id` bigint NOT NULL,
        `device_id` bigint NOT NULL,
        `idempotency_key` longtext CHARACTER SET utf8mb4 NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `command_json` json NOT NULL,
        `result_json` json NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        CONSTRAINT `PK_action_execution_audits` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `ai_call_logs` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_ai_call_logs` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `ai_configs` (
        `user_id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_ai_configs` PRIMARY KEY (`user_id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `ai_skills` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `user_id` bigint NOT NULL,
        `name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `prompt` longtext CHARACTER SET utf8mb4 NOT NULL,
        `scopes` json NOT NULL,
        `is_builtin` tinyint(1) NOT NULL,
        `is_active` tinyint(1) NOT NULL,
        `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
        `deleted_at` datetime(6) NULL,
        CONSTRAINT `PK_ai_skills` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `attachments` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_attachments` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `auth_access_token_revocations` (
        `token_id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `user_id` bigint NOT NULL,
        `tenant_id` bigint NOT NULL,
        `expires_at` datetime(6) NOT NULL,
        `revoked_at` datetime(6) NOT NULL,
        `revoke_reason` longtext CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_auth_access_token_revocations` PRIMARY KEY (`token_id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `auth_audit_logs` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_auth_audit_logs` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `auth_devices` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `user_id` bigint NOT NULL,
        `installation_id` longtext CHARACTER SET utf8mb4 NOT NULL,
        `platform` longtext CHARACTER SET utf8mb4 NOT NULL,
        `device_name` longtext CHARACTER SET utf8mb4 NULL,
        `app_version` longtext CHARACTER SET utf8mb4 NULL,
        `last_seen_at` datetime(6) NOT NULL,
        `revoked_at` datetime(6) NULL,
        `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        CONSTRAINT `PK_auth_devices` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `auth_refresh_tokens` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `user_id` bigint NOT NULL,
        `device_id` bigint NOT NULL,
        `family_id` longtext CHARACTER SET utf8mb4 NOT NULL,
        `token_hash` longblob NOT NULL,
        `expires_at` datetime(6) NOT NULL,
        `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        `last_used_at` datetime(6) NULL,
        `revoked_at` datetime(6) NULL,
        `revoke_reason` longtext CHARACTER SET utf8mb4 NULL,
        `replaced_by_id` bigint NULL,
        CONSTRAINT `PK_auth_refresh_tokens` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `auth_verification_challenges` (
        `id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_auth_verification_challenges` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `automation_rules` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `owner_user_id` bigint NOT NULL,
        `name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `trigger_type` longtext CHARACTER SET utf8mb4 NOT NULL,
        `trigger_config_json` json NOT NULL,
        `conditions_json` json NOT NULL,
        `actions_json` json NOT NULL,
        `approval_policy` longtext CHARACTER SET utf8mb4 NOT NULL,
        `enabled` tinyint(1) NOT NULL,
        `last_triggered_at` datetime(6) NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        `row_version` bigint NOT NULL,
        `sync_version` bigint NOT NULL,
        CONSTRAINT `PK_automation_rules` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `calendar_event_exceptions` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_calendar_event_exceptions` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `calendar_events` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `user_id` bigint NOT NULL,
        `title` longtext CHARACTER SET utf8mb4 NOT NULL,
        `description` longtext CHARACTER SET utf8mb4 NULL,
        `location` longtext CHARACTER SET utf8mb4 NULL,
        `start_at` datetime(6) NOT NULL,
        `end_at` datetime(6) NULL,
        `timezone` longtext CHARACTER SET utf8mb4 NULL,
        `all_day` tinyint(1) NOT NULL,
        `color` longtext CHARACTER SET utf8mb4 NULL,
        `opacity` decimal(65,30) NULL,
        `repeat_rule` longtext CHARACTER SET utf8mb4 NULL,
        `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
        `deleted_at` datetime(6) NULL,
        CONSTRAINT `PK_calendar_events` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `calendar_subscriptions` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `user_id` bigint NOT NULL,
        `name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `source_url_encrypted` longblob NOT NULL,
        `source_url_hash` longblob NOT NULL,
        `enabled` tinyint(1) NOT NULL,
        `refresh_interval_min` int NOT NULL,
        `last_fetch_at` datetime(6) NULL,
        `last_error` longtext CHARACTER SET utf8mb4 NULL,
        `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
        `deleted_at` datetime(6) NULL,
        CONSTRAINT `PK_calendar_subscriptions` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `connector_providers` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `code` longtext CHARACTER SET utf8mb4 NOT NULL,
        `name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `provider` longtext CHARACTER SET utf8mb4 NOT NULL,
        `connector_type` longtext CHARACTER SET utf8mb4 NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `description` longtext CHARACTER SET utf8mb4 NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        `deleted_at` datetime(6) NULL,
        `sync_version` bigint NOT NULL,
        CONSTRAINT `PK_connector_providers` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `connector_sync_jobs` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `workspace_connector_id` bigint NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `reason` longtext CHARACTER SET utf8mb4 NOT NULL,
        `attempt_no` int NOT NULL,
        `available_at` datetime(6) NOT NULL,
        `started_at` datetime(6) NULL,
        `completed_at` datetime(6) NULL,
        `last_error_code` longtext CHARACTER SET utf8mb4 NULL,
        `idempotency_key` longtext CHARACTER SET utf8mb4 NOT NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        `sync_version` bigint NOT NULL,
        CONSTRAINT `PK_connector_sync_jobs` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `credit_ledger` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_credit_ledger` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `device_capabilities` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `device_id` bigint NOT NULL,
        `capability` longtext CHARACTER SET utf8mb4 NOT NULL,
        `value_schema_json` json NOT NULL,
        `permission` longtext CHARACTER SET utf8mb4 NOT NULL,
        `is_writable` tinyint(1) NOT NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        `deleted_at` datetime(6) NULL,
        `sync_version` bigint NOT NULL,
        CONSTRAINT `PK_device_capabilities` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `device_states` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `device_id` bigint NOT NULL,
        `state_json` json NOT NULL,
        `sampled_at` datetime(6) NOT NULL,
        `created_at` datetime(6) NOT NULL,
        CONSTRAINT `PK_device_states` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `expert_file_attachments` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `expert_file_id` bigint NOT NULL,
        `expert_id` bigint NULL,
        `agent_run_id` bigint NULL,
        `attached_by_user_id` bigint NOT NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        CONSTRAINT `PK_expert_file_attachments` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `expert_file_objects` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `expert_file_id` bigint NOT NULL,
        `object_key` longtext CHARACTER SET utf8mb4 NOT NULL,
        `size_bytes` bigint NOT NULL,
        `offset_bytes` bigint NOT NULL,
        `uploaded_at` datetime(6) NOT NULL,
        CONSTRAINT `PK_expert_file_objects` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `expert_files` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `owner_user_id` bigint NOT NULL,
        `name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `mime_type` longtext CHARACTER SET utf8mb4 NOT NULL,
        `size_bytes` bigint NOT NULL,
        `sha256` longtext CHARACTER SET utf8mb4 NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `scan_provider` longtext CHARACTER SET utf8mb4 NULL,
        `scan_completed_at` datetime(6) NULL,
        `rejection_reason` longtext CHARACTER SET utf8mb4 NULL,
        `quota_bytes` bigint NOT NULL,
        `expires_at` datetime(6) NULL,
        `soft_deleted_at` datetime(6) NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        `row_version` bigint NOT NULL,
        `sync_version` bigint NOT NULL,
        CONSTRAINT `PK_expert_files` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `expert_group_members` (
        `group_version_id` bigint NOT NULL,
        `expert_version_id` bigint NOT NULL,
        CONSTRAINT `PK_expert_group_members` PRIMARY KEY (`group_version_id`, `expert_version_id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `expert_group_versions` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `group_id` bigint NOT NULL,
        `version` int NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `orchestration_policy_json` json NULL,
        `output_schema_json` json NULL,
        `estimated_credits` decimal(65,30) NOT NULL,
        CONSTRAINT `PK_expert_group_versions` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `expert_groups` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `owner_user_id` bigint NULL,
        `code` longtext CHARACTER SET utf8mb4 NOT NULL,
        `name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `category` longtext CHARACTER SET utf8mb4 NOT NULL,
        `captain_expert_id` bigint NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `description` longtext CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_expert_groups` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `expert_jobs` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `run_id` bigint NOT NULL,
        `job_type` longtext CHARACTER SET utf8mb4 NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `idempotency_key` longtext CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_expert_jobs` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `expert_run_actions` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `run_id` bigint NOT NULL,
        `tenant_id` bigint NOT NULL,
        `user_id` bigint NOT NULL,
        `action_type` longtext CHARACTER SET utf8mb4 NOT NULL,
        `request_idempotency_key` longtext CHARACTER SET utf8mb4 NOT NULL,
        `request_json` json NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `result_json` json NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        CONSTRAINT `PK_expert_run_actions` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `expert_run_contexts` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_expert_run_contexts` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `expert_runs` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `user_id` bigint NOT NULL,
        `source_type` longtext CHARACTER SET utf8mb4 NOT NULL,
        `expert_version_id` bigint NULL,
        `group_version_id` bigint NULL,
        `request_idempotency_key` longtext CHARACTER SET utf8mb4 NOT NULL,
        `input_json` json NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `result_json` json NULL,
        `result_summary` longtext CHARACTER SET utf8mb4 NULL,
        `estimated_credits` decimal(65,30) NOT NULL,
        `actual_credits` decimal(65,30) NOT NULL,
        `cancel_requested_at` datetime(6) NULL,
        `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        `started_at` datetime(6) NULL,
        `finished_at` datetime(6) NULL,
        CONSTRAINT `PK_expert_runs` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `expert_versions` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `expert_id` bigint NOT NULL,
        `version` int NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `persona` longtext CHARACTER SET utf8mb4 NOT NULL,
        `methodology` longtext CHARACTER SET utf8mb4 NOT NULL,
        `prompt_template` longtext CHARACTER SET utf8mb4 NOT NULL,
        `tool_policy_json` json NULL,
        `output_schema_json` json NULL,
        `estimated_credits` decimal(65,30) NOT NULL,
        CONSTRAINT `PK_expert_versions` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `experts` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `owner_user_id` bigint NULL,
        `code` longtext CHARACTER SET utf8mb4 NOT NULL,
        `name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `category` longtext CHARACTER SET utf8mb4 NOT NULL,
        `expert_type` longtext CHARACTER SET utf8mb4 NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `description` longtext CHARACTER SET utf8mb4 NULL,
        `privacy_scope_json` json NULL,
        CONSTRAINT `PK_experts` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `ical_overrides` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_ical_overrides` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `password_credentials` (
        `user_id` bigint NOT NULL AUTO_INCREMENT,
        `password_hash` longtext CHARACTER SET utf8mb4 NOT NULL,
        `password_changed_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        `failed_attempts` smallint NOT NULL,
        `locked_until` datetime(6) NULL,
        CONSTRAINT `PK_password_credentials` PRIMARY KEY (`user_id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `plan_items` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_plan_items` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `plans` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_plans` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `push_subscriptions` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_push_subscriptions` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `run_artifacts` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_run_artifacts` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `run_events` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `run_id` bigint NOT NULL,
        `sequence` int NOT NULL,
        `event_type` longtext CHARACTER SET utf8mb4 NOT NULL,
        `display_payload_json` json NOT NULL,
        `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        CONSTRAINT `PK_run_events` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `run_step_dependencies` (
        `step_id` bigint NOT NULL,
        `depends_on_step_id` bigint NOT NULL,
        CONSTRAINT `PK_run_step_dependencies` PRIMARY KEY (`step_id`, `depends_on_step_id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `run_step_usage` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_run_step_usage` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `run_steps` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_run_steps` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `scene_actions` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `scene_id` bigint NOT NULL,
        `device_id` bigint NOT NULL,
        `capability` longtext CHARACTER SET utf8mb4 NOT NULL,
        `target_value_json` json NOT NULL,
        `sort_order` int NOT NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        CONSTRAINT `PK_scene_actions` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `scenes` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `scene_key` longtext CHARACTER SET utf8mb4 NOT NULL,
        `name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `summary` longtext CHARACTER SET utf8mb4 NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        `deleted_at` datetime(6) NULL,
        `sync_version` bigint NOT NULL,
        CONSTRAINT `PK_scenes` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `smart_home_devices` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `workspace_connector_id` bigint NULL,
        `space_id` bigint NULL,
        `external_id` longtext CHARACTER SET utf8mb4 NULL,
        `name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `device_type` longtext CHARACTER SET utf8mb4 NOT NULL,
        `online_status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `state_summary` longtext CHARACTER SET utf8mb4 NULL,
        `last_seen_at` datetime(6) NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        `deleted_at` datetime(6) NULL,
        `sync_version` bigint NOT NULL,
        CONSTRAINT `PK_smart_home_devices` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `smart_home_spaces` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `space_type` longtext CHARACTER SET utf8mb4 NOT NULL,
        `summary` longtext CHARACTER SET utf8mb4 NULL,
        `sort_order` int NOT NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        `deleted_at` datetime(6) NULL,
        `sync_version` bigint NOT NULL,
        CONSTRAINT `PK_smart_home_spaces` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `subtasks` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `todo_id` bigint NOT NULL,
        `text` longtext CHARACTER SET utf8mb4 NOT NULL,
        `done` tinyint(1) NOT NULL,
        `seq` int NOT NULL,
        `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
        `deleted_at` datetime(6) NULL,
        CONSTRAINT `PK_subtasks` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `sync_change_log` (
        `sync_version` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_sync_change_log` PRIMARY KEY (`sync_version`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `sync_clients` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_sync_clients` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `sync_mutations` (
        `client_id` bigint NOT NULL,
        `mutation_id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_sync_mutations` PRIMARY KEY (`client_id`, `mutation_id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `team_run_audits` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `actor_user_id` bigint NULL,
        `team_run_id` bigint NULL,
        `expert_file_id` bigint NULL,
        `team_run_member_id` bigint NULL,
        `action` longtext CHARACTER SET utf8mb4 NOT NULL,
        `result` longtext CHARACTER SET utf8mb4 NOT NULL,
        `error_code` longtext CHARACTER SET utf8mb4 NULL,
        `payload_json` json NULL,
        `created_at` datetime(6) NOT NULL,
        CONSTRAINT `PK_team_run_audits` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `team_run_members` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `team_run_id` bigint NOT NULL,
        `expert_version_id` bigint NOT NULL,
        `child_agent_run_id` bigint NULL,
        `display_name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `stage_order` int NOT NULL,
        `permission_intersection_json` json NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `last_error_code` longtext CHARACTER SET utf8mb4 NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        CONSTRAINT `PK_team_run_members` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `team_run_template_versions` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `team_run_template_id` bigint NOT NULL,
        `tenant_id` bigint NOT NULL,
        `version` int NOT NULL,
        `members_json` json NOT NULL,
        `file_refs_json` json NOT NULL,
        `permission_intersections_json` json NOT NULL,
        `graph_json` json NOT NULL,
        `created_at` datetime(6) NOT NULL,
        CONSTRAINT `PK_team_run_template_versions` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `team_run_templates` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `owner_user_id` bigint NOT NULL,
        `name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `team_version` int NOT NULL,
        `mode` longtext CHARACTER SET utf8mb4 NOT NULL,
        `graph_json` json NOT NULL,
        `approval_policy` longtext CHARACTER SET utf8mb4 NOT NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        `row_version` bigint NOT NULL,
        `sync_version` bigint NOT NULL,
        CONSTRAINT `PK_team_run_templates` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `team_runs` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `parent_agent_run_id` bigint NOT NULL,
        `team_run_template_id` bigint NOT NULL,
        `team_run_template_version_id` bigint NOT NULL,
        `team_version` int NOT NULL,
        `mode` longtext CHARACTER SET utf8mb4 NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `synthesis_result_json` json NULL,
        `last_error_code` longtext CHARACTER SET utf8mb4 NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        `row_version` bigint NOT NULL,
        `sync_version` bigint NOT NULL,
        CONSTRAINT `PK_team_runs` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `tenant_members` (
        `tenant_id` bigint NOT NULL,
        `user_id` bigint NOT NULL,
        `role` longtext CHARACTER SET utf8mb4 NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `joined_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
        CONSTRAINT `PK_tenant_members` PRIMARY KEY (`tenant_id`, `user_id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `tenants` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_type` longtext CHARACTER SET utf8mb4 NOT NULL,
        `code` longtext CHARACTER SET utf8mb4 NOT NULL,
        `name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `owner_user_id` bigint NULL,
        `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
        CONSTRAINT `PK_tenants` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `todo_lists` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_todo_lists` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `todo_tag_links` (
        `todo_id` bigint NOT NULL,
        `tag_id` bigint NOT NULL,
        CONSTRAINT `PK_todo_tag_links` PRIMARY KEY (`todo_id`, `tag_id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `todo_tags` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        CONSTRAINT `PK_todo_tags` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `todos` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `user_id` bigint NOT NULL,
        `list_id` bigint NULL,
        `parent_id` bigint NULL,
        `title` longtext CHARACTER SET utf8mb4 NOT NULL,
        `description` longtext CHARACTER SET utf8mb4 NULL,
        `type` longtext CHARACTER SET utf8mb4 NULL,
        `priority` longtext CHARACTER SET utf8mb4 NULL,
        `color` longtext CHARACTER SET utf8mb4 NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `due_at` datetime(6) NULL,
        `remind_at` datetime(6) NULL,
        `completed_at` datetime(6) NULL,
        `pinned` tinyint(1) NOT NULL,
        `sort_order` int NOT NULL,
        `repeat_rule` longtext CHARACTER SET utf8mb4 NULL,
        `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
        `deleted_at` datetime(6) NULL,
        CONSTRAINT `PK_todos` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `user_connector_authorizations` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `user_id` bigint NOT NULL,
        `workspace_connector_id` bigint NOT NULL,
        `scope_json` json NOT NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        `deleted_at` datetime(6) NULL,
        `sync_version` bigint NOT NULL,
        CONSTRAINT `PK_user_connector_authorizations` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `user_consents` (
        `user_id` bigint NOT NULL,
        `consent_type` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `version` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_user_consents` PRIMARY KEY (`user_id`, `consent_type`, `version`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `user_expert_preferences` (
        `tenant_id` bigint NOT NULL,
        `user_id` bigint NOT NULL,
        `expert_id` bigint NOT NULL,
        CONSTRAINT `PK_user_expert_preferences` PRIMARY KEY (`tenant_id`, `user_id`, `expert_id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `user_identities` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `user_id` bigint NOT NULL,
        `provider` longtext CHARACTER SET utf8mb4 NOT NULL,
        `issuer` longtext CHARACTER SET utf8mb4 NOT NULL,
        `subject_kind` longtext CHARACTER SET utf8mb4 NOT NULL,
        `subject_hash` longblob NOT NULL,
        `subject_encrypted` longblob NULL,
        `is_primary` tinyint(1) NOT NULL,
        `verified_at` datetime(6) NOT NULL,
        `last_used_at` datetime(6) NULL,
        `revoked_at` datetime(6) NULL,
        `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        CONSTRAINT `PK_user_identities` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `user_settings` (
        `user_id` bigint NOT NULL,
        `k` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_user_settings` PRIMARY KEY (`user_id`, `k`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `users` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `display_name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `avatar_url` longtext CHARACTER SET utf8mb4 NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `timezone` longtext CHARACTER SET utf8mb4 NOT NULL,
        `locale` longtext CHARACTER SET utf8mb4 NOT NULL,
        `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
        `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
        `deleted_at` datetime(6) NULL,
        CONSTRAINT `PK_users` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    CREATE TABLE `workspace_connectors` (
        `id` bigint NOT NULL AUTO_INCREMENT,
        `tenant_id` bigint NOT NULL,
        `connector_provider_id` bigint NOT NULL,
        `name` longtext CHARACTER SET utf8mb4 NOT NULL,
        `credential_ref` longtext CHARACTER SET utf8mb4 NULL,
        `status` longtext CHARACTER SET utf8mb4 NOT NULL,
        `last_sync_at` datetime(6) NULL,
        `last_health_at` datetime(6) NULL,
        `created_at` datetime(6) NOT NULL,
        `updated_at` datetime(6) NOT NULL,
        `deleted_at` datetime(6) NULL,
        `sync_version` bigint NOT NULL,
        CONSTRAINT `PK_workspace_connectors` PRIMARY KEY (`id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260804144324_InitialCreate') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260804144324_InitialCreate', '8.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

