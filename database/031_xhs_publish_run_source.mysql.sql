-- Apply after 030_xhs_connector.mysql.sql.
-- B27 xhs publish run source (V2.6):
--   expert_runs.ck_run_source（002 迁移）原仅允许 source_type=expert/group，
--   而 B22 场景（scenario）、B24 Skill（skill）已实际使用独立 SourceType——
--   真实 MySQL 下创建该类 Run 会被 CHECK 拒绝（本机先前仅验证迁移执行，未创建真实 Run）。
--   本迁移重建约束：原 expert/group 语义不变，追加 scenario/skill（补 B22/B24 缺口）
--   与 xhs（B27 小红书笔记发布，独立 SourceType 与 skill 幂等键类型互不干扰）。
-- 无 EF 迁移（CHECK 由 SQL 管理，同 030 惯例）。
USE `nexus_mind`;

ALTER TABLE `expert_runs`
  DROP CHECK `ck_run_source`,
  ADD CONSTRAINT `ck_run_source` CHECK (
    (`source_type` = 'expert' AND `expert_version_id` IS NOT NULL AND `group_version_id` IS NULL)
    OR (`source_type` = 'group' AND `group_version_id` IS NOT NULL AND `expert_version_id` IS NULL)
    OR (`source_type` IN ('scenario','skill','xhs') AND `expert_version_id` IS NULL AND `group_version_id` IS NULL)
  );
