-- Apply after 036_mindmap_skill.mysql.sql.
-- V2.8 B35 剪辑任务持久化：保存 Web P5 所需 task_id、公开状态和方案版本历史。
USE `nexus_mind`;

CREATE TABLE `clipping_tasks` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '剪辑任务主键',
  `tenant_id` BIGINT NOT NULL COMMENT '租户 ID（JWT 隔离）',
  `run_id` BIGINT NULL COMMENT '关联 Skill Run 主键，生成方案前为空',
  `status` VARCHAR(16) NOT NULL DEFAULT 'collecting' COMMENT '任务状态：collecting/generating/reviewing/modifying/rendering/done/failed',
  `materials_json` JSON NOT NULL COMMENT '已收集素材路径数组',
  `goal` VARCHAR(255) NULL COMMENT '用户创作目标',
  `current_plan_json` JSON NULL COMMENT '当前展示安全剪辑方案',
  `version_history_json` JSON NOT NULL COMMENT '方案版本历史数组（版本、方案、修改说明、时间）',
  `engine_stage` VARCHAR(32) NULL COMMENT '公开引擎阶段，不含内部执行细节',
  `created_by_user_id` BIGINT NOT NULL COMMENT '任务创建用户，仅本人可访问',
  `deleted_at` DATETIME(3) NULL COMMENT '软删除时间',
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）',
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）',
  PRIMARY KEY (`id`),
  KEY `idx_clipping_tasks_owner` (`tenant_id`,`created_by_user_id`,`deleted_at`),
  KEY `idx_clipping_tasks_run` (`run_id`),
  CONSTRAINT `fk_clipping_tasks_tenants` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_clipping_tasks_runs` FOREIGN KEY (`run_id`) REFERENCES `expert_runs` (`id`)
) ENGINE=InnoDB COMMENT='V2.8 快速剪辑持久化任务，承载会话状态与版本历史';
