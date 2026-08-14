-- Apply after 040_review_analyst_memory_candidates.mysql.sql.
-- B38 素材自动发现（V2.9 剪辑体验重构）：
--   clipping_materials 加 source_type（素材来源）与 directory_key（路径 SHA-256 去重键）。
-- 041 避让：039/040 已由 M3 学习记忆库（learning_memory / memory_candidates）占用，
-- 按 B33 避让先例（036 避让既有 035）顺序顺延为 041。
-- source_type 默认 'upload' 保证存量上传/路径登记行语义不变；
-- directory_key 仅 scan 自动发现写入（upload 行为 NULL），唯一索引允许多个 NULL 不冲突。
USE `nexus_mind`;

ALTER TABLE `clipping_materials`
  ADD COLUMN `source_type` VARCHAR(16) NOT NULL DEFAULT 'upload' COMMENT '素材来源：upload（浏览器上传或路径登记）/scan（素材根目录自动发现）',
  ADD COLUMN `directory_key` VARCHAR(64) NULL COMMENT '素材路径 SHA-256 去重键（仅 scan 自动发现写入，upload 行为空）',
  ADD UNIQUE KEY `uk_clipping_materials_directory_key` (`directory_key`);
