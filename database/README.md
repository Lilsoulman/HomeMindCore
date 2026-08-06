# HomeMind 数据库约定

按顺序执行 `001_mobile_initial_schema.mysql.sql` 至 `014_v2.2_family_and_steward.mysql.sql`，数据库名称为 `nexus_mind`。

本地开发环境需要清空全部数据并重新建库时，执行 `006_rebuild_nexus_mind.mysql.sql`。该脚本会删除 `nexus_mind`，随后按全部迁移重建表和初始化中文专家目录；不得用于生产库。

实体统一以 `HomeMind.Common.Model/Entities` 中的 `[Table]` 与 `[Column]` 映射为准，`HomeMindDbContext` 为唯一的数据访问入口。物理表名保持现有小写蛇形命名，例如 `users`、`auth_refresh_tokens`、`calendar_events`、`expert_runs`；不得仅为 C# 命名而在生产库改表名。

`user_identities.subject_hash` 使用 `BINARY(32)` 保存 SHA-256 原始 32 字节，数据库管理工具按文本显示时会出现乱码，这是正常现象。排查或展示时使用 `HEX(subject_hash)`，例如：`SELECT HEX(subject_hash) FROM user_identities;`，不要把该字段改为可逆明文。

新增表时必须同时完成：

1. 新增顺序号 SQL 迁移，定义主键、外键、索引和 UTC `DATETIME(3)` 字段；
2. 新增实体和显式表名、字段名映射；
3. 在 `HomeMindDbContext` 注册 `DbSet`，组合主键在 `OnModelCreating` 声明；
4. 通过仓储/工作单元或业务服务访问，控制器不得写 SQL。
