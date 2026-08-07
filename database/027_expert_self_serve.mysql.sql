-- Apply after 026_expert_conversations.mysql.sql.
-- B21 self-serve experts (V2.4, 第六阶段):
--   experts.deleted_at for soft-delete of user-owned experts.
--   experts.owner_user_id / created_at / updated_at / row_version already exist (002);
--   no family_audit_logs CHECK extension in this slice (session audit only per design §13.1).
USE `nexus_mind`;

ALTER TABLE `experts`
  ADD COLUMN `deleted_at` DATETIME(3) NULL AFTER `status`;
