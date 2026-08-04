-- 警告：本脚本会删除 nexus_mind 中的全部数据与表，仅用于本地开发库重建。
-- 请在仓库根目录执行：mysql -u root -p < database/006_rebuild_nexus_mind.mysql.sql

DROP DATABASE IF EXISTS `nexus_mind`;
CREATE DATABASE `nexus_mind` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE `nexus_mind`;

-- 以下 SOURCE 指令由 MySQL 命令行客户端解析，顺序不可调整。
SOURCE database/001_mobile_initial_schema.mysql.sql;
SOURCE database/002_expert_workbench_and_tenancy.mysql.sql;
SOURCE database/003_builtin_expert_catalog.mysql.sql;
SOURCE database/004_access_token_revocations.mysql.sql;
SOURCE database/005_localized_default_display_names.mysql.sql;
SOURCE database/007_smart_home_read_model.mysql.sql;
SOURCE database/008_connector_provider_catalog.mysql.sql;
SOURCE database/009_housekeeper_run_orchestration.mysql.sql;
SOURCE database/010_confirmed_smart_home_actions.mysql.sql;
SOURCE database/011_agent_runtime_architecture.mysql.sql;
SOURCE database/012_automation_and_connector_sync.mysql.sql;
SOURCE database/013_expert_files_and_team_orchestration.mysql.sql;
