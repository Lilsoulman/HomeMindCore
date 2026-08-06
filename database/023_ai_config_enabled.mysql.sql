-- Apply after 022_travel_recommendation.mysql.sql.
-- AI 配置启用开关:B18 个人配置三态（新增/只读/编辑）落地；默认启用，向后兼容已有 ai_configs 行。
USE `nexus_mind`;

ALTER TABLE `ai_configs`
  ADD COLUMN `enabled` TINYINT(1) NOT NULL DEFAULT 1 AFTER `temperature`;
