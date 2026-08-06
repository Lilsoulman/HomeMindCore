-- Apply after 016_v2.2_confirmation_batch_records.mysql.sql.
-- B15 personal favorites baseline; extends family_audit_logs CHECK
-- constraints with favorite actions and the personal_favorite target type.
USE `nexus_mind`;

CREATE TABLE `personal_favorites` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `home_id` BIGINT NOT NULL,
  `owner_member_id` BIGINT NOT NULL,
  `category` VARCHAR(32) NOT NULL,
  `name` VARCHAR(128) NOT NULL,
  `detail_json` JSON NULL,
  `visibility` VARCHAR(16) NOT NULL DEFAULT 'private',
  `deleted_at` DATETIME(3) NULL,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `row_version` BIGINT NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `idx_personal_favorites_home_owner_category_updated` (`home_id`,`owner_member_id`,`category`,`updated_at`),
  CONSTRAINT `fk_personal_favorites_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_personal_favorites_owner_member` FOREIGN KEY (`owner_member_id`) REFERENCES `family_members` (`id`),
  CONSTRAINT `ck_personal_favorites_category` CHECK (`category` IN ('restaurant','travel','material')),
  CONSTRAINT `ck_personal_favorites_visibility` CHECK (`visibility` IN ('private','family'))
) ENGINE=InnoDB;

ALTER TABLE `family_audit_logs`
  DROP CHECK `ck_family_audit_action`,
  DROP CHECK `ck_family_audit_target_type`,
  ADD CONSTRAINT `ck_family_audit_action` CHECK (`action` IN ('member_correction','member_terminal_restore','knowledge_write','knowledge_conflict_resolved','decision_record','confirmation_confirm','confirmation_deny','confirmation_batch','activity_undo','favorite_create','favorite_update','favorite_delete','favorite_import')),
  ADD CONSTRAINT `ck_family_audit_target_type` CHECK (`target_type` IN ('family_member','family_knowledge','decision_history','confirmation_item','steward_activity','personal_favorite'));

-- B16 registers the personal life expert (life category) and its first version,
-- declaring the favorite.recommend / trip.plan / favorite.create skills.
INSERT INTO `experts` (`tenant_id`,`code`,`name`,`category`,`expert_type`,`status`,`description`,`privacy_scope_json`)
VALUES (1,'personal-life-expert','个人生活专家','life','builtin','active','探店翻牌与行程规划：结合私藏店铺库、时间、位置、口味与天气给出建议，并把行程同步到日历。','["favorites"]')
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`description`=VALUES(`description`),`status`=VALUES(`status`);

INSERT INTO `expert_versions` (`tenant_id`,`expert_id`,`version`,`status`,`persona`,`methodology`,`prompt_template`,`tool_policy_json`,`output_schema_json`,`estimated_credits`)
SELECT 1, e.id, 1, 'published', '个人生活专家', '优先使用个人私藏库，再结合时间、位置、口味与天气给出可解释建议；行程以天为单位组织并引用来源。', '返回面向用户的推荐与行程安排，不输出提示或思考过程。', '{"skills":["favorite.recommend","trip.plan","favorite.create"]}', '{"type":"object"}', 1.5000
FROM `experts` e
WHERE e.tenant_id=1 AND e.code='personal-life-expert'
  AND NOT EXISTS (SELECT 1 FROM `expert_versions` v WHERE v.expert_id=e.id AND v.version=1);
