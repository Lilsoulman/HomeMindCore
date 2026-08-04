-- Apply after 006_rebuild_nexus_mind.mysql.sql (or migrations 001-005 in an existing development database).
-- This migration only adds the NexusMind SmartHome read model. It does not store connector credentials.
USE `nexus_mind`;

CREATE TABLE `connector_providers` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `code` VARCHAR(64) NOT NULL,
  `name` VARCHAR(128) NOT NULL,
  `provider` VARCHAR(64) NOT NULL,
  `connector_type` VARCHAR(32) NOT NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'active',
  `description` TEXT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `deleted_at` DATETIME(3) NULL,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_connector_provider_code` (`code`),
  CONSTRAINT `ck_connector_provider_status` CHECK (`status` IN ('active','disabled'))
) ENGINE=InnoDB;

CREATE TABLE `workspace_connectors` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `connector_provider_id` BIGINT NOT NULL,
  `name` VARCHAR(128) NOT NULL,
  `credential_ref` VARCHAR(512) NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'disconnected',
  `last_sync_at` DATETIME(3) NULL,
  `last_health_at` DATETIME(3) NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `deleted_at` DATETIME(3) NULL,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  KEY `idx_workspace_connector_tenant` (`tenant_id`,`status`),
  CONSTRAINT `fk_workspace_connector_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_workspace_connector_provider` FOREIGN KEY (`connector_provider_id`) REFERENCES `connector_providers` (`id`),
  CONSTRAINT `ck_workspace_connector_status` CHECK (`status` IN ('authorizing','connected','disconnected','failed','disabled'))
) ENGINE=InnoDB;

CREATE TABLE `user_connector_authorizations` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `user_id` BIGINT NOT NULL,
  `workspace_connector_id` BIGINT NOT NULL,
  `scope_json` JSON NOT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `deleted_at` DATETIME(3) NULL,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_user_connector_authorization` (`user_id`,`workspace_connector_id`),
  CONSTRAINT `fk_connector_authorization_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_connector_authorization_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_connector_authorization_connector` FOREIGN KEY (`workspace_connector_id`) REFERENCES `workspace_connectors` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `smart_home_spaces` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `name` VARCHAR(128) NOT NULL,
  `space_type` VARCHAR(32) NOT NULL,
  `summary` VARCHAR(512) NULL,
  `sort_order` INT NOT NULL DEFAULT 0,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `deleted_at` DATETIME(3) NULL,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  KEY `idx_smart_home_space_tenant` (`tenant_id`,`sort_order`),
  CONSTRAINT `fk_smart_home_space_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`)
) ENGINE=InnoDB;

CREATE TABLE `smart_home_devices` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `workspace_connector_id` BIGINT NULL,
  `space_id` BIGINT NULL,
  `external_id` VARCHAR(255) NULL,
  `name` VARCHAR(128) NOT NULL,
  `device_type` VARCHAR(32) NOT NULL,
  `online_status` VARCHAR(16) NOT NULL DEFAULT 'unknown',
  `state_summary` VARCHAR(512) NULL,
  `last_seen_at` DATETIME(3) NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `deleted_at` DATETIME(3) NULL,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_device_connector_external` (`workspace_connector_id`,`external_id`),
  KEY `idx_smart_home_device_tenant_space` (`tenant_id`,`space_id`,`online_status`),
  CONSTRAINT `fk_smart_home_device_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_smart_home_device_connector` FOREIGN KEY (`workspace_connector_id`) REFERENCES `workspace_connectors` (`id`),
  CONSTRAINT `fk_smart_home_device_space` FOREIGN KEY (`space_id`) REFERENCES `smart_home_spaces` (`id`) ON DELETE SET NULL,
  CONSTRAINT `ck_smart_home_device_online` CHECK (`online_status` IN ('online','offline','unknown'))
) ENGINE=InnoDB;

CREATE TABLE `device_capabilities` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `device_id` BIGINT NOT NULL,
  `capability` VARCHAR(64) NOT NULL,
  `value_schema_json` JSON NOT NULL,
  `permission` VARCHAR(64) NOT NULL,
  `is_writable` TINYINT(1) NOT NULL DEFAULT 0,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `deleted_at` DATETIME(3) NULL,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_device_capability` (`device_id`,`capability`),
  CONSTRAINT `fk_device_capability_device` FOREIGN KEY (`device_id`) REFERENCES `smart_home_devices` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `device_states` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `device_id` BIGINT NOT NULL,
  `state_json` JSON NOT NULL,
  `sampled_at` DATETIME(3) NOT NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  KEY `idx_device_state_latest` (`device_id`,`sampled_at`),
  CONSTRAINT `fk_device_state_device` FOREIGN KEY (`device_id`) REFERENCES `smart_home_devices` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `scenes` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `scene_key` VARCHAR(64) NOT NULL,
  `name` VARCHAR(128) NOT NULL,
  `summary` VARCHAR(512) NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'active',
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `deleted_at` DATETIME(3) NULL,
  `sync_version` BIGINT NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_scene_tenant_key` (`tenant_id`,`scene_key`),
  CONSTRAINT `fk_scene_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `ck_scene_status` CHECK (`status` IN ('active','disabled'))
) ENGINE=InnoDB;

CREATE TABLE `scene_actions` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `scene_id` BIGINT NOT NULL,
  `device_id` BIGINT NOT NULL,
  `capability` VARCHAR(64) NOT NULL,
  `target_value_json` JSON NOT NULL,
  `sort_order` INT NOT NULL DEFAULT 0,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  KEY `idx_scene_action_scene` (`scene_id`,`sort_order`),
  CONSTRAINT `fk_scene_action_scene` FOREIGN KEY (`scene_id`) REFERENCES `scenes` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_scene_action_device` FOREIGN KEY (`device_id`) REFERENCES `smart_home_devices` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB;
