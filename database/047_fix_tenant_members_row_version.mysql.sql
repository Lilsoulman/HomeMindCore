-- Apply after 046_family_schedule_coordination.mysql.sql.
-- 修复 025 迁移遗漏的租户成员乐观锁字段；可安全重复执行。
USE `nexus_mind`;

SET @tenant_members_row_version_exists := (
  SELECT COUNT(*)
  FROM information_schema.columns
  WHERE table_schema = DATABASE()
    AND table_name = 'tenant_members'
    AND column_name = 'row_version'
);
SET @tenant_members_row_version_sql := IF(
  @tenant_members_row_version_exists = 0,
  'ALTER TABLE `tenant_members` ADD COLUMN `row_version` BIGINT NOT NULL DEFAULT 1 COMMENT ''乐观锁版本号''',
  'SELECT 1'
);
PREPARE tenant_members_row_version_stmt FROM @tenant_members_row_version_sql;
EXECUTE tenant_members_row_version_stmt;
DEALLOCATE PREPARE tenant_members_row_version_stmt;
