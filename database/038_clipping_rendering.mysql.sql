-- Apply after 037_clipping_tasks.mysql.sql.
-- V2.9 B37 粗剪视频产出：clipping_tasks.status 在 037 已包含 rendering，故本次无结构变更。
USE `nexus_mind`;

-- No-op: rendering/done/failed 状态流转由应用服务和既有 VARCHAR 状态列承载。
