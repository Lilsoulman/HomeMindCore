-- Apply after 007_smart_home_read_model.mysql.sql.
-- This global catalog contains no tenant data or credentials.
USE `nexus_mind`;

INSERT INTO `connector_providers` (`code`, `name`, `provider`, `connector_type`, `status`, `description`)
VALUES
  ('home_assistant', 'Home Assistant', 'home_assistant', 'smart_home', 'active', '通过受控 Adapter 接入家庭设备。'),
  ('mqtt', 'MQTT', 'mqtt', 'smart_home', 'active', '面向本地优先设备消息的兼容入口。'),
  ('xiaomi_cloud', '米家', 'xiaomi', 'smart_home', 'active', '保留标准能力映射扩展位。'),
  ('tuya_cloud', '涂鸦', 'tuya', 'smart_home', 'active', '保留标准能力映射扩展位。')
ON DUPLICATE KEY UPDATE
  `name` = VALUES(`name`),
  `provider` = VALUES(`provider`),
  `connector_type` = VALUES(`connector_type`),
  `status` = VALUES(`status`),
  `description` = VALUES(`description`),
  `deleted_at` = NULL;
