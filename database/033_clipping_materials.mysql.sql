-- Apply after 031_xhs_publish_run_source.mysql.sql.
-- B29 快速剪辑素材登记（V2.7 对话式优化）：
--   clipping_materials  素材登记表（浏览器上传落盘或路径模式登记的输入文件，ffprobe 提取元数据）
-- 素材仅本人可见可删；上传返回 storage_path 供前端回填 Skill 输入 media_location（B24 契约零改动）；
-- family_audit_logs 的 CHECK 扩展（media_file_uploaded / media_file_deleted / clipping_material）由 034 一并完成。
USE `nexus_mind`;

CREATE TABLE `clipping_materials` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `tenant_id` BIGINT NOT NULL,
  `owner_user_id` BIGINT NOT NULL,
  `file_name` VARCHAR(255) NOT NULL,
  `storage_path` VARCHAR(1024) NOT NULL,
  `content_type` VARCHAR(128) NULL,
  `file_size` BIGINT NOT NULL DEFAULT 0,
  `duration_seconds` INT NULL,
  `width` INT NULL,
  `height` INT NULL,
  `fps` DOUBLE NULL,
  `status` VARCHAR(16) NOT NULL DEFAULT 'active',
  `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  KEY `idx_clipping_materials_owner` (`owner_user_id`, `is_deleted`),
  CONSTRAINT `fk_clipping_materials_tenants` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`)
) ENGINE=InnoDB;
