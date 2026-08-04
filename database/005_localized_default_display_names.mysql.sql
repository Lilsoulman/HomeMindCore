-- 将历史默认展示名统一为中文；仅影响系统生成的英文默认值，不修改用户自定义名称。
USE `nexus_mind`;

UPDATE `users`
SET `display_name` = 'HomeMind 用户'
WHERE `display_name` = 'HomeMind user';

UPDATE `tenants`
SET `name` = CONCAT('个人空间 ', `owner_user_id`)
WHERE `tenant_type` = 'personal'
  AND `owner_user_id` IS NOT NULL
  AND `name` LIKE 'Personal workspace %';
