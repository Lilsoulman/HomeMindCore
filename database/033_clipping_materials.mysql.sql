-- Apply after 031_xhs_publish_run_source.mysql.sql.
-- B29 快速剪辑素材登记（V2.7 对话式优化）：
--   clipping_materials  素材登记表（浏览器上传落盘或路径模式登记的输入文件，ffprobe 提取元数据）
-- 素材仅本人可见可删；上传返回 storage_path 供前端回填 Skill 输入 media_location（B24 契约零改动）；
-- family_audit_logs 的 CHECK 扩展（media_file_uploaded / media_file_deleted / clipping_material）由 034 一并完成。
USE `nexus_mind`;

CREATE TABLE `clipping_materials` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '素材主键',
  `tenant_id` BIGINT NOT NULL COMMENT '租户 ID（JWT 隔离，禁止客户端覆盖）',
  `owner_user_id` BIGINT NOT NULL COMMENT '素材归属用户（仅本人可见可删）',
  `file_name` VARCHAR(255) NOT NULL COMMENT '素材文件名（浏览器上传原名或路径模式文件名）',
  `storage_path` VARCHAR(1024) NOT NULL COMMENT '服务端素材目录内的存储路径（供前端回填 media_location）',
  `content_type` VARCHAR(128) NULL COMMENT '素材 MIME 类型',
  `file_size` BIGINT NOT NULL DEFAULT 0 COMMENT '文件大小（字节）',
  `duration_seconds` INT NULL COMMENT '时长（秒），ffprobe 提取，提取失败为 NULL',
  `width` INT NULL COMMENT '视频宽度（像素），ffprobe 提取，失败为 NULL',
  `height` INT NULL COMMENT '视频高度（像素），ffprobe 提取，失败为 NULL',
  `fps` DOUBLE NULL COMMENT '视频帧率，ffprobe 提取，失败为 NULL',
  `status` VARCHAR(16) NOT NULL DEFAULT 'active' COMMENT '素材状态：active（可用）',
  `is_deleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '软删除标记：0 未删除/1 已删除',
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）',
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）',
  PRIMARY KEY (`id`),
  KEY `idx_clipping_materials_owner` (`owner_user_id`, `is_deleted`),
  CONSTRAINT `fk_clipping_materials_tenants` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`)
) ENGINE=InnoDB;
