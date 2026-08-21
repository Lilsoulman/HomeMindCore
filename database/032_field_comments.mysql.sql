-- Apply after 031_xhs_publish_run_source.mysql.sql.
-- 032 存量字段中文备注补齐：为 001-031 迁移创建的全部存量表字段统一补齐 COMMENT。
-- 只执行 ALTER TABLE ... MODIFY COLUMN ... COMMENT，保留原列类型、可空性、默认值、自增与 CHECK，不引入 schema 漂移。
-- 新建迁移（033 起）自带 COMMENT，不在本迁移范围。
USE `nexus_mind`;


-- 表名：系统账户表，保存不敏感的用户资料
ALTER TABLE `users` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '用户主键';
ALTER TABLE `users` MODIFY COLUMN `display_name` varchar(64) NOT NULL DEFAULT 'HomeMind 用户' COMMENT '用户对外展示名称';
ALTER TABLE `users` MODIFY COLUMN `avatar_url` varchar(512) NULL COMMENT '用户头像 URL，可为空';
ALTER TABLE `users` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'active' COMMENT '账户状态：active/disabled/deleted';
ALTER TABLE `users` MODIFY COLUMN `timezone` varchar(64) NOT NULL DEFAULT 'Asia/Shanghai' COMMENT '用户默认时区，使用 IANA 时区标识';
ALTER TABLE `users` MODIFY COLUMN `locale` varchar(16) NOT NULL DEFAULT 'zh-CN' COMMENT '用户偏好语言标签，遵循 BCP 47';
ALTER TABLE `users` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `users` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC），行更新自动刷新';
ALTER TABLE `users` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳，账户禁用而非物理删除';

-- 表名：登录标识表，仅保存不可逆标识摘要与可选密文
ALTER TABLE `user_identities` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '登录标识主键';
ALTER TABLE `user_identities` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '关联用户主键（外键 users.id）';
ALTER TABLE `user_identities` MODIFY COLUMN `provider` varchar(32) NOT NULL COMMENT '认证提供方：wechat/phone/email';
ALTER TABLE `user_identities` MODIFY COLUMN `issuer` varchar(128) NOT NULL COMMENT '颁发方，便于区分同名提供方下的子渠道';
ALTER TABLE `user_identities` MODIFY COLUMN `subject_kind` varchar(32) NOT NULL COMMENT '主体类型，如 phone_number/openid/unionid/email';
ALTER TABLE `user_identities` MODIFY COLUMN `subject_hash` binary(32) NOT NULL COMMENT '主体 SHA-256 摘要（含 pepper），仅存哈希用于检索去重，不返回明文';
ALTER TABLE `user_identities` MODIFY COLUMN `subject_encrypted` blob NULL COMMENT '主体密文，由 KMS 加密，用于重发登录或回执展示';
ALTER TABLE `user_identities` MODIFY COLUMN `is_primary` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否主登录标识，登录时优先使用';
ALTER TABLE `user_identities` MODIFY COLUMN `verified_at` datetime(3) NOT NULL COMMENT '最近一次校验通过时间（UTC）';
ALTER TABLE `user_identities` MODIFY COLUMN `last_used_at` datetime(3) NULL COMMENT '最近一次登录使用时间（UTC）';
ALTER TABLE `user_identities` MODIFY COLUMN `revoked_at` datetime(3) NULL COMMENT '吊销时间（UTC），吊销后禁止使用';
ALTER TABLE `user_identities` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';

-- 表名：密码凭据表，仅保存 PBKDF2 哈希值
ALTER TABLE `password_credentials` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '所属用户主键（外键 users.id）';
ALTER TABLE `password_credentials` MODIFY COLUMN `password_hash` varchar(255) NOT NULL COMMENT 'PBKDF2 哈希字符串（含算法参数与盐），已加密，不存明文密码';
ALTER TABLE `password_credentials` MODIFY COLUMN `password_changed_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '最近一次修改密码的时间（UTC）';
ALTER TABLE `password_credentials` MODIFY COLUMN `failed_attempts` smallint NOT NULL DEFAULT 0 COMMENT '连续登录失败计数，超过阈值后锁定';
ALTER TABLE `password_credentials` MODIFY COLUMN `locked_until` datetime(3) NULL COMMENT '账户锁定到期时间（UTC），解锁前拒绝登录';

-- 表名：登录设备表，绑定用户与设备指纹
ALTER TABLE `auth_devices` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '设备主键';
ALTER TABLE `auth_devices` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '所属用户主键（外键 users.id）';
ALTER TABLE `auth_devices` MODIFY COLUMN `installation_id` varchar(36) NOT NULL COMMENT '客户端安装 ID，用于关联刷新令牌家族';
ALTER TABLE `auth_devices` MODIFY COLUMN `platform` varchar(16) NOT NULL COMMENT '客户端平台：ios/android/h5 等';
ALTER TABLE `auth_devices` MODIFY COLUMN `device_name` varchar(128) NULL COMMENT '设备名称，便于用户在多设备列表中识别';
ALTER TABLE `auth_devices` MODIFY COLUMN `app_version` varchar(32) NULL COMMENT '客户端应用版本字符串';
ALTER TABLE `auth_devices` MODIFY COLUMN `last_seen_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '最近一次活跃时间（UTC），用于列表排序与异常检测';
ALTER TABLE `auth_devices` MODIFY COLUMN `revoked_at` datetime(3) NULL COMMENT '吊销时间（UTC），吊销后必须重新登录';
ALTER TABLE `auth_devices` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';

-- 表名：刷新令牌表，仅保存哈希，支持轮换与家族级撤销
ALTER TABLE `auth_refresh_tokens` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '刷新令牌主键';
ALTER TABLE `auth_refresh_tokens` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '所属用户主键（外键 users.id）';
ALTER TABLE `auth_refresh_tokens` MODIFY COLUMN `device_id` bigint NOT NULL COMMENT '所属登录设备主键（外键 auth_devices.id）';
ALTER TABLE `auth_refresh_tokens` MODIFY COLUMN `family_id` varchar(36) NOT NULL COMMENT '刷新令牌家族标识，家族内任何令牌被冒用将触发整体撤销';
ALTER TABLE `auth_refresh_tokens` MODIFY COLUMN `token_hash` binary(32) NOT NULL COMMENT '刷新令牌 SHA-256 摘要，仅存哈希，不保存明文';
ALTER TABLE `auth_refresh_tokens` MODIFY COLUMN `expires_at` datetime(3) NOT NULL COMMENT '过期时间（UTC），到达后必须重新登录';
ALTER TABLE `auth_refresh_tokens` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `auth_refresh_tokens` MODIFY COLUMN `last_used_at` datetime(3) NULL COMMENT '最近一次使用时间（UTC），用于闲置清理';
ALTER TABLE `auth_refresh_tokens` MODIFY COLUMN `revoked_at` datetime(3) NULL COMMENT '撤销时间（UTC）';
ALTER TABLE `auth_refresh_tokens` MODIFY COLUMN `revoke_reason` varchar(32) NULL COMMENT '撤销原因，便于审计';
ALTER TABLE `auth_refresh_tokens` MODIFY COLUMN `replaced_by_id` bigint NULL COMMENT '轮换后被取代的新刷新令牌主键（自引用外键 auth_refresh_tokens.id）';

-- 表名：验证挑战表，用于登录/注册等一次性验证
ALTER TABLE `auth_verification_challenges` MODIFY COLUMN `id` varchar(36) NOT NULL COMMENT '验证挑战唯一标识（UUID）';
ALTER TABLE `auth_verification_challenges` MODIFY COLUMN `purpose` varchar(32) NOT NULL COMMENT '验证目的，如登录、注册、重置密码等';
ALTER TABLE `auth_verification_challenges` MODIFY COLUMN `channel` varchar(16) NOT NULL COMMENT '验证码发送渠道，如 sms/email 等';
ALTER TABLE `auth_verification_challenges` MODIFY COLUMN `subject_hash` binary(32) NOT NULL COMMENT '验证主体（手机号/邮箱）SHA-256 摘要，仅存哈希';
ALTER TABLE `auth_verification_challenges` MODIFY COLUMN `code_hash` binary(32) NOT NULL COMMENT '验证码 SHA-256 摘要，仅存哈希，不保存明文';
ALTER TABLE `auth_verification_challenges` MODIFY COLUMN `expires_at` datetime(3) NOT NULL COMMENT '挑战过期时间（UTC）';
ALTER TABLE `auth_verification_challenges` MODIFY COLUMN `consumed_at` datetime(3) NULL COMMENT '消费时间（UTC），为空表示未使用';
ALTER TABLE `auth_verification_challenges` MODIFY COLUMN `attempt_count` smallint NOT NULL DEFAULT 0 COMMENT '验证尝试次数，用于防爆破';
ALTER TABLE `auth_verification_challenges` MODIFY COLUMN `request_ip_hash` binary(32) NULL COMMENT '请求来源 IP 的哈希，仅存哈希用于风控';
ALTER TABLE `auth_verification_challenges` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';

-- 表名：认证审计日志表，记录登录/登出等认证事件
ALTER TABLE `auth_audit_logs` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '审计日志主键';
ALTER TABLE `auth_audit_logs` MODIFY COLUMN `user_id` bigint NULL COMMENT '关联用户主键（外键 users.id），匿名事件可为空';
ALTER TABLE `auth_audit_logs` MODIFY COLUMN `event_type` varchar(48) NOT NULL COMMENT '认证事件类型，如登录成功/失败、登出等';
ALTER TABLE `auth_audit_logs` MODIFY COLUMN `provider` varchar(32) NULL COMMENT '认证提供方，如 wechat/phone/email';
ALTER TABLE `auth_audit_logs` MODIFY COLUMN `device_id` bigint NULL COMMENT '关联设备主键（外键 auth_devices.id）';
ALTER TABLE `auth_audit_logs` MODIFY COLUMN `ip_hash` binary(32) NULL COMMENT '来源 IP 的哈希，仅存哈希用于风控';
ALTER TABLE `auth_audit_logs` MODIFY COLUMN `user_agent_hash` binary(32) NULL COMMENT 'User-Agent 哈希，仅存哈希';
ALTER TABLE `auth_audit_logs` MODIFY COLUMN `metadata` json NULL COMMENT '事件附加元数据（JSON），如失败原因与上下文';
ALTER TABLE `auth_audit_logs` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '事件发生时间（UTC）';

-- 表名：用户同意记录表，记录用户对协议版本同意接受
ALTER TABLE `user_consents` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '所属用户主键（外键 users.id）';
ALTER TABLE `user_consents` MODIFY COLUMN `consent_type` varchar(32) NOT NULL COMMENT '同意类型标识，如隐私政策、服务条款等';
ALTER TABLE `user_consents` MODIFY COLUMN `version` varchar(32) NOT NULL COMMENT '所同意的协议版本号';
ALTER TABLE `user_consents` MODIFY COLUMN `accepted_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '同意接受时间（UTC）';

-- 表名：访问令牌撤销表，存储主动登出或风控触发的 JTI 黑名单
ALTER TABLE `auth_access_token_revocations` MODIFY COLUMN `token_id` char(32) NOT NULL COMMENT '被撤销访问令牌的 JTI 标识';
ALTER TABLE `auth_access_token_revocations` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '令牌所属用户主键（外键 users.id）';
ALTER TABLE `auth_access_token_revocations` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '令牌所属租户主键（外键 tenants.id）';
ALTER TABLE `auth_access_token_revocations` MODIFY COLUMN `expires_at` datetime(3) NOT NULL COMMENT '令牌原过期时间（UTC），用于过期清理';
ALTER TABLE `auth_access_token_revocations` MODIFY COLUMN `revoked_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '撤销时间（UTC）';
ALTER TABLE `auth_access_token_revocations` MODIFY COLUMN `revoke_reason` varchar(32) NOT NULL DEFAULT 'logout' COMMENT '撤销原因：logout/password_reset 等';

-- 表名：同步客户端注册表，记录参与增量同步的客户端
ALTER TABLE `sync_clients` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '同步客户端主键';
ALTER TABLE `sync_clients` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '所属用户主键（外键 users.id）';
ALTER TABLE `sync_clients` MODIFY COLUMN `installation_id` varchar(36) NOT NULL COMMENT '客户端安装 ID，唯一标识一个客户端';
ALTER TABLE `sync_clients` MODIFY COLUMN `platform` varchar(16) NOT NULL COMMENT '客户端平台：ios/android/h5 等';
ALTER TABLE `sync_clients` MODIFY COLUMN `last_pulled_version` bigint NOT NULL DEFAULT 0 COMMENT '客户端最近已拉取的变更版本号（同步游标）';
ALTER TABLE `sync_clients` MODIFY COLUMN `last_seen_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '最近活跃时间（UTC）';

-- 表名：同步幂等去重表，记录客户端已应用的变更
ALTER TABLE `sync_mutations` MODIFY COLUMN `client_id` bigint NOT NULL COMMENT '所属同步客户端主键（外键 sync_clients.id）';
ALTER TABLE `sync_mutations` MODIFY COLUMN `mutation_id` varchar(36) NOT NULL COMMENT '客户端生成的变更 UUID，用于重试幂等';
ALTER TABLE `sync_mutations` MODIFY COLUMN `applied_version` bigint NOT NULL COMMENT '该变更在全局变更日志中对应的版本号';
ALTER TABLE `sync_mutations` MODIFY COLUMN `received_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '服务端接收时间（UTC）';

-- 表名：数据变更日志表，提供增量同步游标
ALTER TABLE `sync_change_log` MODIFY COLUMN `sync_version` bigint NOT NULL AUTO_INCREMENT COMMENT '全局单调递增的变更版本号（同步游标）';
ALTER TABLE `sync_change_log` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '变更所属用户主键（外键 users.id）';
ALTER TABLE `sync_change_log` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '变更所属租户主键（外键 tenants.id）';
ALTER TABLE `sync_change_log` MODIFY COLUMN `entity_type` varchar(40) NOT NULL COMMENT '变更实体类型标识';
ALTER TABLE `sync_change_log` MODIFY COLUMN `entity_id` bigint NOT NULL COMMENT '变更实体主键';
ALTER TABLE `sync_change_log` MODIFY COLUMN `operation` varchar(8) NOT NULL COMMENT '变更操作：upsert/delete';
ALTER TABLE `sync_change_log` MODIFY COLUMN `changed_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '变更时间（UTC）';

-- 表名：租户表，承载多家庭或多组织隔离边界
ALTER TABLE `tenants` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '租户主键';
ALTER TABLE `tenants` MODIFY COLUMN `tenant_type` varchar(16) NOT NULL DEFAULT 'personal' COMMENT '租户类型：system/personal/team';
ALTER TABLE `tenants` MODIFY COLUMN `code` varchar(64) NOT NULL COMMENT '租户业务编码，全局唯一（如 user-{id}）';
ALTER TABLE `tenants` MODIFY COLUMN `name` varchar(128) NOT NULL COMMENT '租户对外展示名称';
ALTER TABLE `tenants` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'active' COMMENT '租户状态：active/suspended/deleted';
ALTER TABLE `tenants` MODIFY COLUMN `owner_user_id` bigint NULL COMMENT '租户所有者用户主键（外键 users.id），可为空表示平台维护';
ALTER TABLE `tenants` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `tenants` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC），行更新自动刷新';
ALTER TABLE `tenants` MODIFY COLUMN `row_version` bigint NOT NULL DEFAULT 1 COMMENT '乐观锁版本号，由 EF ConcurrencyToken 强制';

-- 表名：租户成员表，建立用户与租户的多对多关系
ALTER TABLE `tenant_members` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户主键（外键 tenants.id）';
ALTER TABLE `tenant_members` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '成员用户主键（外键 users.id）';
ALTER TABLE `tenant_members` MODIFY COLUMN `role` varchar(16) NOT NULL DEFAULT 'member' COMMENT '成员角色：owner/admin/member/viewer';
ALTER TABLE `tenant_members` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'active' COMMENT '成员状态：active/suspended';
ALTER TABLE `tenant_members` MODIFY COLUMN `joined_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '加入时间（UTC）';
ALTER TABLE `tenant_members` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `tenant_members` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC），行更新自动刷新';
ALTER TABLE `tenant_members` MODIFY COLUMN `row_version` bigint NOT NULL DEFAULT 1 COMMENT '乐观锁版本号，由 EF ConcurrencyToken 强制';

-- 表名：家庭成员邀请表，以手机号摘要匹配已验证账户接受
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '邀请主键';
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属家庭（租户）主键（外键 tenants.id）';
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `invited_by_user_id` bigint NOT NULL COMMENT '邀请发起人用户主键（外键 users.id）';
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `subject_kind` varchar(16) NOT NULL DEFAULT 'phone' COMMENT '受邀标识类型，固定为 phone，与 user_identities.subject_kind 对齐';
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `subject_hash` binary(32) NOT NULL COMMENT '手机号 SHA-256 摘要，仅存哈希，与 user_identities.subject_hash 同口径';
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `proposed_role` varchar(16) NOT NULL COMMENT '接受后授予的角色：admin/member/viewer，不得为 owner';
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'pending' COMMENT '状态机：pending/accepted/expired/revoked';
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `expires_at` datetime(3) NOT NULL COMMENT '邀请过期时间（UTC），默认 7 天';
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `accepted_user_id` bigint NULL COMMENT '接受该邀请的用户主键（外键 users.id），pending 时为空';
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `accepted_at` datetime(3) NULL COMMENT '接受时间（UTC），pending 时为空';
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `revoked_at` datetime(3) NULL COMMENT '撤销时间（UTC），pending 时为空';
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC），行更新自动刷新';
ALTER TABLE `tenant_member_invitations` MODIFY COLUMN `row_version` bigint NOT NULL DEFAULT 1 COMMENT '乐观锁版本号';

-- 表名：用户键值设置表，存储用户个性化配置
ALTER TABLE `user_settings` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '所属用户主键（外键 users.id）';
ALTER TABLE `user_settings` MODIFY COLUMN `k` varchar(64) NOT NULL COMMENT '设置项键名';
ALTER TABLE `user_settings` MODIFY COLUMN `v` json NOT NULL COMMENT '设置项值（JSON），支持结构化配置';
ALTER TABLE `user_settings` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC），行更新自动刷新';
ALTER TABLE `user_settings` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳';
ALTER TABLE `user_settings` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '同步版本号，用于增量同步';


-- 表名：待办列表表
ALTER TABLE `todo_lists` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '待办列表主键';
ALTER TABLE `todo_lists` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants 表';
ALTER TABLE `todo_lists` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '列表所有者用户标识，关联 users 表';
ALTER TABLE `todo_lists` MODIFY COLUMN `name` varchar(80) NOT NULL COMMENT '列表名称';
ALTER TABLE `todo_lists` MODIFY COLUMN `color` varchar(16) NULL COMMENT '前端展示色，HEX 颜色字符串';
ALTER TABLE `todo_lists` MODIFY COLUMN `sort_order` int NOT NULL DEFAULT 0 COMMENT '列表排序值，越小越靠前';
ALTER TABLE `todo_lists` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `todo_lists` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `todo_lists` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳，为空表示未删除';
ALTER TABLE `todo_lists` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '同步版本号，供增量同步游标使用';

-- 表名：待办事项表
ALTER TABLE `todos` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '待办主键';
ALTER TABLE `todos` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants 表';
ALTER TABLE `todos` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '创建者用户标识，关联 users 表';
ALTER TABLE `todos` MODIFY COLUMN `list_id` bigint NULL COMMENT '所属待办列表主键，关联 todo_lists 表，为空表示未分组';
ALTER TABLE `todos` MODIFY COLUMN `parent_id` bigint NULL COMMENT '父待办主键，用于子任务，关联 todos 表';
ALTER TABLE `todos` MODIFY COLUMN `seq` int NOT NULL DEFAULT 0 COMMENT '创建顺序号，作为初始排序依据';
ALTER TABLE `todos` MODIFY COLUMN `title` varchar(255) NOT NULL COMMENT '待办标题';
ALTER TABLE `todos` MODIFY COLUMN `description` text NULL COMMENT '待办详细描述，可为空';
ALTER TABLE `todos` MODIFY COLUMN `type` varchar(32) NULL COMMENT '待办类型，如 task（任务）/habit（习惯）等';
ALTER TABLE `todos` MODIFY COLUMN `priority` varchar(16) NULL COMMENT '优先级，用于排序与前端展示';
ALTER TABLE `todos` MODIFY COLUMN `color` varchar(16) NULL COMMENT '前端展示色，HEX 颜色字符串';
ALTER TABLE `todos` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'pending' COMMENT '待办状态，取值 pending（进行中）/completed（已完成）等，默认 pending';
ALTER TABLE `todos` MODIFY COLUMN `due_at` datetime(3) NULL COMMENT '截止时间（UTC），为空表示无截止';
ALTER TABLE `todos` MODIFY COLUMN `remind_at` datetime(3) NULL COMMENT '提醒时间（UTC），为空表示不提醒';
ALTER TABLE `todos` MODIFY COLUMN `completed_at` datetime(3) NULL COMMENT '完成时间（UTC）';
ALTER TABLE `todos` MODIFY COLUMN `pinned` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否置顶，0 否/1 是';
ALTER TABLE `todos` MODIFY COLUMN `sort_order` int NOT NULL DEFAULT 0 COMMENT '列表内排序值，越小越靠前';
ALTER TABLE `todos` MODIFY COLUMN `repeat_rule` varchar(512) NULL COMMENT '重复规则，使用 RFC 5545 RRULE 子集';
ALTER TABLE `todos` MODIFY COLUMN `recurrence_anchor_at` datetime(3) NULL COMMENT '重复序列锚点时间（UTC），用于计算后续实例';
ALTER TABLE `todos` MODIFY COLUMN `recurrence_parent_id` bigint NULL COMMENT '重复序列父待办主键，关联 todos 表';
ALTER TABLE `todos` MODIFY COLUMN `report_ignored` tinyint(1) NOT NULL DEFAULT 0 COMMENT '汇报忽略标记，0 否/1 是，控制是否计入日常汇报';
ALTER TABLE `todos` MODIFY COLUMN `source_type` varchar(32) NULL COMMENT '来源类型，标记由哪类运行创建，为空表示手动创建';
ALTER TABLE `todos` MODIFY COLUMN `source_run_id` bigint NULL COMMENT '来源运行主键，关联 expert_runs 表';
ALTER TABLE `todos` MODIFY COLUMN `source_action_id` bigint NULL COMMENT '来源运行动作主键，关联 expert_run_actions 表';
ALTER TABLE `todos` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `todos` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `todos` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳，为空表示未删除';
ALTER TABLE `todos` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '同步版本号，供增量同步游标使用';

-- 表名：待办子任务表
ALTER TABLE `subtasks` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '子任务主键';
ALTER TABLE `subtasks` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants 表';
ALTER TABLE `subtasks` MODIFY COLUMN `todo_id` bigint NOT NULL COMMENT '所属待办主键，关联 todos 表';
ALTER TABLE `subtasks` MODIFY COLUMN `text` varchar(255) NOT NULL COMMENT '子任务文本内容';
ALTER TABLE `subtasks` MODIFY COLUMN `done` tinyint(1) NOT NULL DEFAULT 0 COMMENT '子任务完成状态，0 未完成/1 已完成';
ALTER TABLE `subtasks` MODIFY COLUMN `seq` int NOT NULL DEFAULT 0 COMMENT '子任务展示顺序';
ALTER TABLE `subtasks` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `subtasks` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `subtasks` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳，为空表示未删除';
ALTER TABLE `subtasks` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '同步版本号，供增量同步游标使用';

-- 表名：待办标签表
ALTER TABLE `todo_tags` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '标签主键';
ALTER TABLE `todo_tags` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants 表';
ALTER TABLE `todo_tags` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '标签所有者用户标识，关联 users 表';
ALTER TABLE `todo_tags` MODIFY COLUMN `name` varchar(64) NOT NULL COMMENT '标签名称，同一用户下唯一';
ALTER TABLE `todo_tags` MODIFY COLUMN `color` varchar(16) NULL COMMENT '前端展示色，HEX 颜色字符串';
ALTER TABLE `todo_tags` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `todo_tags` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `todo_tags` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳，为空表示未删除';
ALTER TABLE `todo_tags` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '同步版本号，供增量同步游标使用';

-- 表名：待办与标签关联表
ALTER TABLE `todo_tag_links` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants 表';
ALTER TABLE `todo_tag_links` MODIFY COLUMN `todo_id` bigint NOT NULL COMMENT '待办主键，关联 todos 表';
ALTER TABLE `todo_tag_links` MODIFY COLUMN `tag_id` bigint NOT NULL COMMENT '标签主键，关联 todo_tags 表';
ALTER TABLE `todo_tag_links` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `todo_tag_links` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `todo_tag_links` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳，为空表示未删除';
ALTER TABLE `todo_tag_links` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '同步版本号，供增量同步游标使用';

-- 表名：待办附件表
ALTER TABLE `attachments` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '附件主键';
ALTER TABLE `attachments` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants 表';
ALTER TABLE `attachments` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '上传者用户标识，关联 users 表';
ALTER TABLE `attachments` MODIFY COLUMN `todo_id` bigint NULL COMMENT '所属待办主键，关联 todos 表，可为空';
ALTER TABLE `attachments` MODIFY COLUMN `name` varchar(255) NOT NULL COMMENT '附件原始文件名';
ALTER TABLE `attachments` MODIFY COLUMN `mime` varchar(127) NOT NULL COMMENT '附件 MIME 类型';
ALTER TABLE `attachments` MODIFY COLUMN `size_bytes` bigint NOT NULL COMMENT '文件大小（字节）';
ALTER TABLE `attachments` MODIFY COLUMN `storage_path` varchar(512) NOT NULL COMMENT '文件存储路径，全局唯一';
ALTER TABLE `attachments` MODIFY COLUMN `content_sha256` binary(32) NOT NULL COMMENT '文件内容 SHA-256 摘要，仅存哈希用于去重与校验';
ALTER TABLE `attachments` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `attachments` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `attachments` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳，为空表示未删除';
ALTER TABLE `attachments` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '同步版本号，供增量同步游标使用';

-- 表名：日历事件表
ALTER TABLE `calendar_events` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '日历事件主键';
ALTER TABLE `calendar_events` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants 表';
ALTER TABLE `calendar_events` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '创建者用户标识，关联 users 表';
ALTER TABLE `calendar_events` MODIFY COLUMN `title` varchar(255) NOT NULL COMMENT '事件标题';
ALTER TABLE `calendar_events` MODIFY COLUMN `description` text NULL COMMENT '事件详细描述';
ALTER TABLE `calendar_events` MODIFY COLUMN `location` varchar(255) NULL COMMENT '事件地点';
ALTER TABLE `calendar_events` MODIFY COLUMN `start_at` datetime(3) NOT NULL COMMENT '开始时间（UTC）';
ALTER TABLE `calendar_events` MODIFY COLUMN `end_at` datetime(3) NULL COMMENT '结束时间（UTC），为空表示无结束';
ALTER TABLE `calendar_events` MODIFY COLUMN `timezone` varchar(64) NULL COMMENT '事件显示时区，使用 IANA 时区标识';
ALTER TABLE `calendar_events` MODIFY COLUMN `all_day` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否全天事件，0 否/1 是';
ALTER TABLE `calendar_events` MODIFY COLUMN `color` varchar(16) NULL COMMENT '前端展示色，HEX 颜色字符串';
ALTER TABLE `calendar_events` MODIFY COLUMN `opacity` decimal(3,2) NULL COMMENT '事件不透明度，取值 0-1';
ALTER TABLE `calendar_events` MODIFY COLUMN `repeat_rule` varchar(1024) NULL COMMENT '重复规则，使用 RFC 5545 RRULE 子集';
ALTER TABLE `calendar_events` MODIFY COLUMN `source_type` varchar(32) NULL COMMENT '来源类型，标记由哪类运行创建，为空表示手动创建';
ALTER TABLE `calendar_events` MODIFY COLUMN `source_run_id` bigint NULL COMMENT '来源运行主键，关联 expert_runs 表';
ALTER TABLE `calendar_events` MODIFY COLUMN `source_action_id` bigint NULL COMMENT '来源运行动作主键，关联 expert_run_actions 表';
ALTER TABLE `calendar_events` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `calendar_events` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `calendar_events` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳，为空表示未删除';
ALTER TABLE `calendar_events` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '同步版本号，供增量同步游标使用';

-- 表名：日历事件重复实例例外表
ALTER TABLE `calendar_event_exceptions` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '例外主键';
ALTER TABLE `calendar_event_exceptions` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants 表';
ALTER TABLE `calendar_event_exceptions` MODIFY COLUMN `event_id` bigint NOT NULL COMMENT '所属日历事件主键，关联 calendar_events 表';
ALTER TABLE `calendar_event_exceptions` MODIFY COLUMN `original_start_at` datetime(3) NOT NULL COMMENT '被覆盖实例的原始开始时间（UTC）';
ALTER TABLE `calendar_event_exceptions` MODIFY COLUMN `override_title` varchar(255) NULL COMMENT '覆盖后的标题，为空表示沿用原事件标题';
ALTER TABLE `calendar_event_exceptions` MODIFY COLUMN `override_start_at` datetime(3) NULL COMMENT '覆盖后的开始时间（UTC）';
ALTER TABLE `calendar_event_exceptions` MODIFY COLUMN `override_end_at` datetime(3) NULL COMMENT '覆盖后的结束时间（UTC）';
ALTER TABLE `calendar_event_exceptions` MODIFY COLUMN `is_cancelled` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否取消该实例，0 否/1 是';
ALTER TABLE `calendar_event_exceptions` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `calendar_event_exceptions` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `calendar_event_exceptions` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳，为空表示未删除';
ALTER TABLE `calendar_event_exceptions` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '同步版本号，供增量同步游标使用';

-- 表名：日历订阅表（iCal 远端源与刷新策略）
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '订阅主键';
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants 表';
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '订阅者用户标识，关联 users 表';
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `name` varchar(128) NOT NULL COMMENT '订阅名称';
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `source_url_encrypted` blob NOT NULL COMMENT '源 URL 加密密文，已加密，API 不返回明文';
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `source_url_hash` binary(32) NOT NULL COMMENT '源 URL 的 SHA-256 摘要，仅存哈希用于去重与审计检索';
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `enabled` tinyint(1) NOT NULL DEFAULT 1 COMMENT '是否启用，0 否/1 是';
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `refresh_interval_min` int NOT NULL DEFAULT 60 COMMENT '刷新间隔（分钟），取值 15-1440';
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `last_fetch_at` datetime(3) NULL COMMENT '最近一次抓取时间（UTC）';
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `last_error` varchar(512) NULL COMMENT '最近一次抓取错误信息';
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳，为空表示未删除';
ALTER TABLE `calendar_subscriptions` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '同步版本号，供增量同步游标使用';

-- 表名：iCal 订阅事件覆盖表
ALTER TABLE `ical_overrides` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '覆盖记录主键';
ALTER TABLE `ical_overrides` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants 表';
ALTER TABLE `ical_overrides` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '操作用户标识，关联 users 表';
ALTER TABLE `ical_overrides` MODIFY COLUMN `subscription_id` bigint NOT NULL COMMENT '所属订阅主键，关联 calendar_subscriptions 表';
ALTER TABLE `ical_overrides` MODIFY COLUMN `source_event_uid` varchar(255) NOT NULL COMMENT '远端 iCal 事件 UID';
ALTER TABLE `ical_overrides` MODIFY COLUMN `recurrence_id` varchar(255) NOT NULL DEFAULT '' COMMENT '远端重复实例 RECURRENCE-ID，空串表示作用于整个事件';
ALTER TABLE `ical_overrides` MODIFY COLUMN `action` varchar(16) NOT NULL COMMENT '覆盖动作，取值 rename（重命名）/hide（隐藏）/reschedule（改期）';
ALTER TABLE `ical_overrides` MODIFY COLUMN `patch` json NULL COMMENT '覆盖字段 JSON，按动作存放新标题或改期后的时间等';
ALTER TABLE `ical_overrides` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `ical_overrides` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `ical_overrides` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳，为空表示未删除';
ALTER TABLE `ical_overrides` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '同步版本号，供增量同步游标使用';

-- 表名：计划表（专家运行结果可保存为一等公民计划）
ALTER TABLE `plans` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '计划主键';
ALTER TABLE `plans` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants 表';
ALTER TABLE `plans` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '所属用户标识，关联 users 表';
ALTER TABLE `plans` MODIFY COLUMN `title` varchar(255) NOT NULL COMMENT '计划标题';
ALTER TABLE `plans` MODIFY COLUMN `description` text NULL COMMENT '计划详细描述';
ALTER TABLE `plans` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'active' COMMENT '计划状态，取值 draft（草稿）/active（进行中）/completed（已完成）/archived（已归档），默认 active';
ALTER TABLE `plans` MODIFY COLUMN `start_at` datetime(3) NULL COMMENT '计划开始时间（UTC）';
ALTER TABLE `plans` MODIFY COLUMN `target_at` datetime(3) NULL COMMENT '计划目标完成时间（UTC）';
ALTER TABLE `plans` MODIFY COLUMN `source_type` varchar(32) NULL COMMENT '来源类型，标记由哪类运行创建，为空表示手动创建';
ALTER TABLE `plans` MODIFY COLUMN `source_run_id` bigint NULL COMMENT '来源运行主键，关联 expert_runs 表';
ALTER TABLE `plans` MODIFY COLUMN `source_action_id` bigint NULL COMMENT '来源运行动作主键，关联 expert_run_actions 表';
ALTER TABLE `plans` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `plans` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `plans` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳，为空表示未删除';
ALTER TABLE `plans` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '同步版本号，供增量同步游标使用';

-- 表名：计划条目表
ALTER TABLE `plan_items` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '计划条目主键';
ALTER TABLE `plan_items` MODIFY COLUMN `plan_id` bigint NOT NULL COMMENT '所属计划主键，关联 plans 表';
ALTER TABLE `plan_items` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants 表';
ALTER TABLE `plan_items` MODIFY COLUMN `item_type` varchar(16) NOT NULL COMMENT '条目类型，取值 note（备注）/todo（待办）/calendar_event（日历事件）';
ALTER TABLE `plan_items` MODIFY COLUMN `title` varchar(255) NOT NULL COMMENT '条目标题';
ALTER TABLE `plan_items` MODIFY COLUMN `todo_id` bigint NULL COMMENT '关联待办主键，关联 todos 表，note 条目为空';
ALTER TABLE `plan_items` MODIFY COLUMN `calendar_event_id` bigint NULL COMMENT '关联日历事件主键，关联 calendar_events 表，note 条目为空';
ALTER TABLE `plan_items` MODIFY COLUMN `sort_order` int NOT NULL DEFAULT 0 COMMENT '条目排序值，越小越靠前';
ALTER TABLE `plan_items` MODIFY COLUMN `metadata_json` json NULL COMMENT '条目附加元数据 JSON，按条目类型存放扩展字段';
ALTER TABLE `plan_items` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `plan_items` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `plan_items` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳，为空表示未删除';
ALTER TABLE `plan_items` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '同步版本号，供增量同步游标使用';

-- 表名：推送订阅表（Web Push 订阅端点与密钥，仅存密文与哈希）
ALTER TABLE `push_subscriptions` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '推送订阅主键';
ALTER TABLE `push_subscriptions` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '所属用户主键（外键 users.id）';
ALTER TABLE `push_subscriptions` MODIFY COLUMN `device_id` bigint NOT NULL COMMENT '关联登录设备主键（外键 auth_devices.id）';
ALTER TABLE `push_subscriptions` MODIFY COLUMN `endpoint_encrypted` blob NOT NULL COMMENT '推送端点地址密文，由密钥服务加密，不存明文';
ALTER TABLE `push_subscriptions` MODIFY COLUMN `endpoint_hash` binary(32) NOT NULL COMMENT '推送端点 SHA-256 哈希，仅存哈希用于去重检索，不返回明文';
ALTER TABLE `push_subscriptions` MODIFY COLUMN `p256dh_encrypted` blob NOT NULL COMMENT 'Web Push p256dh 公钥密文，由密钥服务加密，不存明文';
ALTER TABLE `push_subscriptions` MODIFY COLUMN `auth_encrypted` blob NOT NULL COMMENT 'Web Push auth 密钥密文，由密钥服务加密，不存明文';
ALTER TABLE `push_subscriptions` MODIFY COLUMN `expires_at` datetime(3) NULL COMMENT '订阅过期时间（UTC），过期后需重新订阅';
ALTER TABLE `push_subscriptions` MODIFY COLUMN `last_success_at` datetime(3) NULL COMMENT '最近一次推送成功时间（UTC）';
ALTER TABLE `push_subscriptions` MODIFY COLUMN `failure_count` smallint NOT NULL DEFAULT 0 COMMENT '连续推送失败计数，超过阈值后停用订阅';
ALTER TABLE `push_subscriptions` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `push_subscriptions` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC），行更新自动刷新';

-- 表名：每日知识条目表（供知识管家取用）
ALTER TABLE `knowledge_items` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '知识条目主键';
ALTER TABLE `knowledge_items` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants 表';
ALTER TABLE `knowledge_items` MODIFY COLUMN `category` varchar(32) NOT NULL DEFAULT 'general' COMMENT '知识分类，取值 yunhe_tcm（芸和中医）/management（管理）/general（通用）等，默认 general';
ALTER TABLE `knowledge_items` MODIFY COLUMN `title` varchar(128) NOT NULL COMMENT '知识标题';
ALTER TABLE `knowledge_items` MODIFY COLUMN `content` text NOT NULL COMMENT '知识正文';
ALTER TABLE `knowledge_items` MODIFY COLUMN `source` varchar(128) NULL COMMENT '来源说明，可为空';
ALTER TABLE `knowledge_items` MODIFY COLUMN `is_active` tinyint(1) NOT NULL DEFAULT 1 COMMENT '是否启用，0 否/1 是，停用后不再被知识管家取用';
ALTER TABLE `knowledge_items` MODIFY COLUMN `created_by_user_id` bigint NULL COMMENT '创建者用户标识，关联 users 表，系统预置条目为空';
ALTER TABLE `knowledge_items` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `knowledge_items` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '最近修改时间（UTC）';

USE `nexus_mind`;

-- 表名：用户自定义 AI 技能表
ALTER TABLE `ai_skills` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '技能主键';
ALTER TABLE `ai_skills` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)，从 JWT 派生，客户端不可覆盖';
ALTER TABLE `ai_skills` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '技能创建者用户标识，外键关联 users(id)';
ALTER TABLE `ai_skills` MODIFY COLUMN `name` varchar(128) NOT NULL COMMENT '技能对外展示名称';
ALTER TABLE `ai_skills` MODIFY COLUMN `prompt` text NOT NULL COMMENT '技能系统提示词模板，运行期由智能体运行时使用';
ALTER TABLE `ai_skills` MODIFY COLUMN `scopes` json NOT NULL COMMENT '技能被调用时所需授权范围的 JSON 数组（如 ["calendar.read"]）';
ALTER TABLE `ai_skills` MODIFY COLUMN `is_builtin` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否系统内置技能（1=是 0=否），内置技能禁止用户删除或编辑';
ALTER TABLE `ai_skills` MODIFY COLUMN `is_active` tinyint(1) NOT NULL DEFAULT 1 COMMENT '技能启用状态（1=启用 0=停用）；停用后不再出现在调用候选中';
ALTER TABLE `ai_skills` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '记录创建时间（UTC）';
ALTER TABLE `ai_skills` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '记录最近一次修改时间（UTC）';
ALTER TABLE `ai_skills` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '逻辑删除标记，软删除时填写时间戳（UTC）';
ALTER TABLE `ai_skills` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '行版本号，用于同步冲突检测';

-- 表名：用户 AI 调用配置表（每用户一行，主键即用户标识）
ALTER TABLE `ai_configs` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '配置所属用户标识，主键，外键关联 users(id)';
ALTER TABLE `ai_configs` MODIFY COLUMN `endpoint` varchar(512) NOT NULL COMMENT 'OpenAI 兼容的 API 端点地址，如 https://api.openai.com/v1';
ALTER TABLE `ai_configs` MODIFY COLUMN `api_key_encrypted` blob NOT NULL COMMENT 'API 密钥密文，已加密存储，永不回传客户端；未配置时为空数组';
ALTER TABLE `ai_configs` MODIFY COLUMN `model` varchar(128) NOT NULL COMMENT '默认使用的模型名称，如 gpt-4.1-mini';
ALTER TABLE `ai_configs` MODIFY COLUMN `temperature` decimal(3,2) NOT NULL DEFAULT 0.70 COMMENT '生成温度参数，取值范围 0~1，精确到两位小数';
ALTER TABLE `ai_configs` MODIFY COLUMN `enabled` tinyint(1) NOT NULL DEFAULT 1 COMMENT '是否启用 AI 生成能力（1=启用 0=停用）；停用时 AI 生成与专家运行整体不可用';
ALTER TABLE `ai_configs` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '记录最近一次修改时间（UTC）';
ALTER TABLE `ai_configs` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '逻辑删除标记，软删除时填写时间戳（UTC）';
ALTER TABLE `ai_configs` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 0 COMMENT '行版本号，用于同步冲突检测';

-- 表名：AI 调用日志表
ALTER TABLE `ai_call_logs` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '调用日志主键';
ALTER TABLE `ai_call_logs` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `ai_call_logs` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '发起调用的用户标识，外键关联 users(id)';
ALTER TABLE `ai_call_logs` MODIFY COLUMN `scope` varchar(32) NOT NULL COMMENT '调用场景范围（如 expert/steward 等，取值由业务接入时定义）';
ALTER TABLE `ai_call_logs` MODIFY COLUMN `skill_id` bigint NULL COMMENT '关联的技能主键，外键关联 ai_skills(id)，可空';
ALTER TABLE `ai_call_logs` MODIFY COLUMN `model` varchar(128) NULL COMMENT '本次调用使用的模型名称，可空';
ALTER TABLE `ai_call_logs` MODIFY COLUMN `prompt_tokens` int NULL COMMENT '提示词 token 数，可空';
ALTER TABLE `ai_call_logs` MODIFY COLUMN `completion_tokens` int NULL COMMENT '补全输出 token 数，可空';
ALTER TABLE `ai_call_logs` MODIFY COLUMN `latency_ms` int NULL COMMENT '调用延迟（毫秒），可空';
ALTER TABLE `ai_call_logs` MODIFY COLUMN `status` varchar(16) NOT NULL COMMENT '调用结果状态（如 success/failed 等，取值由业务接入时定义）';
ALTER TABLE `ai_call_logs` MODIFY COLUMN `error_msg` varchar(512) NULL COMMENT '失败时的错误信息，可空';
ALTER TABLE `ai_call_logs` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '调用记录创建时间（UTC）';

-- 表名：专家目录（保存专家模板与所属租户关系）
ALTER TABLE `experts` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '专家主键';
ALTER TABLE `experts` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `experts` MODIFY COLUMN `owner_user_id` bigint NULL COMMENT '专家所有者用户标识，可空表示平台内置，外键关联 users(id)';
ALTER TABLE `experts` MODIFY COLUMN `code` varchar(64) NOT NULL COMMENT '专家业务编码，租户内唯一';
ALTER TABLE `experts` MODIFY COLUMN `name` varchar(128) NOT NULL COMMENT '专家对外展示名称';
ALTER TABLE `experts` MODIFY COLUMN `category` varchar(32) NOT NULL COMMENT '专家分类，如"生活管家""日程协调"等';
ALTER TABLE `experts` MODIFY COLUMN `expert_type` varchar(16) NOT NULL DEFAULT 'builtin' COMMENT '专家类型：builtin（平台内置）/custom（用户自定义）';
ALTER TABLE `experts` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'active' COMMENT '专家状态：draft（草稿）/active（启用）/disabled（停用）/archived（归档）';
ALTER TABLE `experts` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间（UTC），非空表示已删除；已删专家从目录、运行解析与会话发送全部消失';
ALTER TABLE `experts` MODIFY COLUMN `description` text NULL COMMENT '专家的描述信息，Swagger 展示与运行期说明使用';
ALTER TABLE `experts` MODIFY COLUMN `privacy_scope_json` json NULL COMMENT '专家可见的隐私范围 JSON 数组（如 ["smart_home"]），由智能体运行时解析';
ALTER TABLE `experts` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `experts` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '最近更新时间（UTC）';
ALTER TABLE `experts` MODIFY COLUMN `row_version` bigint NOT NULL DEFAULT 1 COMMENT '行版本号，乐观锁比较字段，更新时递增';

-- 表名：专家版本快照（发布后不可变；运行期引用具体版本）
ALTER TABLE `expert_versions` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '专家版本主键';
ALTER TABLE `expert_versions` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `expert_versions` MODIFY COLUMN `expert_id` bigint NOT NULL COMMENT '所属专家模板主键，外键关联 experts(id)，级联删除';
ALTER TABLE `expert_versions` MODIFY COLUMN `version` int NOT NULL COMMENT '从 1 起的版本号，租户内专家内单调递增';
ALTER TABLE `expert_versions` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'published' COMMENT '版本状态：draft（草稿）/published（已发布）/retired（已退休）';
ALTER TABLE `expert_versions` MODIFY COLUMN `persona` text NOT NULL COMMENT '角色设定，运行期提示词的人设片段';
ALTER TABLE `expert_versions` MODIFY COLUMN `methodology` text NOT NULL COMMENT '方法论说明，影响思考链风格';
ALTER TABLE `expert_versions` MODIFY COLUMN `prompt_template` text NOT NULL COMMENT '完整提示词模板';
ALTER TABLE `expert_versions` MODIFY COLUMN `tool_policy_json` json NULL COMMENT '工具策略 JSON，决定可调用的 Skill / Connector 集合（如 {"skills":["smart-home.read"],"writeActionsRequireConfirmation":true}）';
ALTER TABLE `expert_versions` MODIFY COLUMN `knowledge_profile_json` json NULL COMMENT '专家知识画像配置 JSON，供运行期知识检索使用';
ALTER TABLE `expert_versions` MODIFY COLUMN `output_schema_json` json NULL COMMENT '输出契约 JSON，用于结构化校验';
ALTER TABLE `expert_versions` MODIFY COLUMN `estimated_credits` decimal(18,4) NOT NULL DEFAULT 0.0000 COMMENT '单次运行的预估积分消耗';
ALTER TABLE `expert_versions` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）；发布后不可变';

-- 表名：专家组目录（可作为团队运行模板的根）
ALTER TABLE `expert_groups` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '专家组主键';
ALTER TABLE `expert_groups` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `expert_groups` MODIFY COLUMN `owner_user_id` bigint NULL COMMENT '所有者用户标识，可空表示平台内置，外键关联 users(id)';
ALTER TABLE `expert_groups` MODIFY COLUMN `code` varchar(64) NOT NULL COMMENT '专家组业务编码，租户内唯一';
ALTER TABLE `expert_groups` MODIFY COLUMN `name` varchar(128) NOT NULL COMMENT '专家组对外展示名称';
ALTER TABLE `expert_groups` MODIFY COLUMN `category` varchar(32) NOT NULL COMMENT '专家组分类';
ALTER TABLE `expert_groups` MODIFY COLUMN `captain_expert_id` bigint NOT NULL COMMENT '队长专家主键，承担汇总/裁决角色，外键关联 experts(id)';
ALTER TABLE `expert_groups` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'active' COMMENT '专家组状态，默认 active（启用）';
ALTER TABLE `expert_groups` MODIFY COLUMN `description` text NULL COMMENT '专家组描述';
ALTER TABLE `expert_groups` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `expert_groups` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '最近更新时间（UTC）';
ALTER TABLE `expert_groups` MODIFY COLUMN `row_version` bigint NOT NULL DEFAULT 1 COMMENT '行版本号，乐观锁比较字段，更新时递增';

-- 表名：专家组版本快照（发布后不可变）
ALTER TABLE `expert_group_versions` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '专家组版本主键';
ALTER TABLE `expert_group_versions` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `expert_group_versions` MODIFY COLUMN `group_id` bigint NOT NULL COMMENT '所属专家组主键，外键关联 expert_groups(id)，级联删除';
ALTER TABLE `expert_group_versions` MODIFY COLUMN `version` int NOT NULL COMMENT '从 1 起的版本号，租户内专家组内单调递增';
ALTER TABLE `expert_group_versions` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'published' COMMENT '版本状态：draft（草稿）/published（已发布）/retired（已退休）';
ALTER TABLE `expert_group_versions` MODIFY COLUMN `orchestration_policy_json` json NULL COMMENT '编排策略 JSON，包含成员顺序、并行/串行、合成方式等';
ALTER TABLE `expert_group_versions` MODIFY COLUMN `output_schema_json` json NULL COMMENT '输出契约 JSON，用于结构化校验';
ALTER TABLE `expert_group_versions` MODIFY COLUMN `estimated_credits` decimal(18,4) NOT NULL DEFAULT 0.0000 COMMENT '单次运行的预估积分消耗';
ALTER TABLE `expert_group_versions` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）；发布后不可变';

-- 表名：专家组版本成员表
ALTER TABLE `expert_group_members` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `expert_group_members` MODIFY COLUMN `group_version_id` bigint NOT NULL COMMENT '所属专家组版本主键，外键关联 expert_group_versions(id)，级联删除';
ALTER TABLE `expert_group_members` MODIFY COLUMN `expert_version_id` bigint NOT NULL COMMENT '成员专家版本主键，外键关联 expert_versions(id)，级联删除';
ALTER TABLE `expert_group_members` MODIFY COLUMN `role` varchar(32) NOT NULL COMMENT '成员角色：captain（队长）/member（成员）/reviewer（评审）';
ALTER TABLE `expert_group_members` MODIFY COLUMN `order_no` int NOT NULL DEFAULT 0 COMMENT '成员执行顺序号';
ALTER TABLE `expert_group_members` MODIFY COLUMN `is_required` tinyint(1) NOT NULL DEFAULT 1 COMMENT '是否必需成员（1=是 0=否），可选成员失败可跳过';

-- 表名：AI Agent 运行表（物理表名保持 expert_runs，兼容既有外键）
ALTER TABLE `expert_runs` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '运行主键';
ALTER TABLE `expert_runs` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `expert_runs` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '发起运行的用户标识，外键关联 users(id)';
ALTER TABLE `expert_runs` MODIFY COLUMN `source_type` varchar(16) NOT NULL COMMENT '运行来源：expert（单专家）/group（专家组）/steward（管家）/skill（技能）/scenario（场景）等';
ALTER TABLE `expert_runs` MODIFY COLUMN `expert_version_id` bigint NULL COMMENT '所引用的专家版本主键，单专家运行时使用；source_type=expert 时必须非空，外键关联 expert_versions(id)';
ALTER TABLE `expert_runs` MODIFY COLUMN `group_version_id` bigint NULL COMMENT '所引用的专家组版本主键，团队运行时使用；source_type=group 时必须非空，外键关联 expert_group_versions(id)';
ALTER TABLE `expert_runs` MODIFY COLUMN `request_idempotency_key` varchar(36) NOT NULL COMMENT '请求级幂等键，重复请求复用结果';
ALTER TABLE `expert_runs` MODIFY COLUMN `input_json` json NOT NULL COMMENT '输入负载 JSON，由智能体运行时解析';
ALTER TABLE `expert_runs` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'draft' COMMENT '运行状态：draft（草稿已创建未入队）/queued（已入队待调度）/planning（规划中生成动作草稿）/running（执行中调用工具或动作）/completed（成功完成）/failed（失败终止）/cancelled（被取消）';
ALTER TABLE `expert_runs` MODIFY COLUMN `plan_summary` text NULL COMMENT '运行计划摘要（面向用户的文本），可空';
ALTER TABLE `expert_runs` MODIFY COLUMN `result_json` json NULL COMMENT '结果负载 JSON，由智能体运行时解析';
ALTER TABLE `expert_runs` MODIFY COLUMN `result_summary` text NULL COMMENT '面向用户的结果摘要';
ALTER TABLE `expert_runs` MODIFY COLUMN `estimated_credits` decimal(18,4) NOT NULL DEFAULT 0.0000 COMMENT '预估积分消耗';
ALTER TABLE `expert_runs` MODIFY COLUMN `actual_credits` decimal(18,4) NOT NULL DEFAULT 0.0000 COMMENT '实际扣减积分';
ALTER TABLE `expert_runs` MODIFY COLUMN `cancel_requested_at` datetime(3) NULL COMMENT '取消请求时间戳，存在时表示客户端已请求取消';
ALTER TABLE `expert_runs` MODIFY COLUMN `started_at` datetime(3) NULL COMMENT '实际开始时间（UTC）';
ALTER TABLE `expert_runs` MODIFY COLUMN `finished_at` datetime(3) NULL COMMENT '结束时间（UTC），失败与取消也会写入';
ALTER TABLE `expert_runs` MODIFY COLUMN `conversation_id` bigint NULL COMMENT '所属会话主键，可空表示非会话运行；会话运行终态后据此追加 assistant 消息';
ALTER TABLE `expert_runs` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `expert_runs` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '最近更新时间（UTC）';
ALTER TABLE `expert_runs` MODIFY COLUMN `row_version` bigint NOT NULL DEFAULT 1 COMMENT '行版本号，乐观锁比较字段，更新时递增';
ALTER TABLE `expert_runs` MODIFY COLUMN `auto_confirm_policy` longtext NOT NULL COMMENT '自动确认策略：never（从不自动确认）/L3_only（仅 L3 风险自动执行）/L2_and_above（L2 及以上风险自动执行）';
ALTER TABLE `expert_runs` MODIFY COLUMN `permission_snapshot_json` json NULL COMMENT '运行创建时的权限快照 JSON（scope/owner 与连接器授权摘要），Action 确认与执行前实时复验';
ALTER TABLE `expert_runs` MODIFY COLUMN `mode` longtext NOT NULL COMMENT '运行模式：single（单专家运行）/steward（管家式运行）';

-- 表名：专家运行的待执行动作表（提交后由用户确认或自动执行）
ALTER TABLE `expert_run_actions` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '动作主键';
ALTER TABLE `expert_run_actions` MODIFY COLUMN `run_id` bigint NOT NULL COMMENT '所属运行主键，外键关联 expert_runs(id)，级联删除';
ALTER TABLE `expert_run_actions` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `expert_run_actions` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '触发动作的用户标识，外键关联 users(id)';
ALTER TABLE `expert_run_actions` MODIFY COLUMN `action_type` varchar(16) NOT NULL COMMENT '动作类型：plan（计划）/todos（待办）/calendar_events（日历事件）/smart_home_device（智能家居设备）/calendar_create_event/xhs_publish 等';
ALTER TABLE `expert_run_actions` MODIFY COLUMN `request_idempotency_key` varchar(36) NOT NULL COMMENT '请求级幂等键，避免重复触发同一动作';
ALTER TABLE `expert_run_actions` MODIFY COLUMN `request_json` json NOT NULL COMMENT '动作请求负载 JSON，由智能体运行时解析';
ALTER TABLE `expert_run_actions` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'queued' COMMENT '动作状态：queued（已入队）/processing（处理中）/completed（完成）/failed（失败）/pending（待确认）/confirmed（已确认）/rejected（已拒绝）/executing（执行中）/executed（已执行）/cancelled（已取消）';
ALTER TABLE `expert_run_actions` MODIFY COLUMN `result_json` json NULL COMMENT '动作结果 JSON，可空';
ALTER TABLE `expert_run_actions` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '动作创建时间（UTC）';
ALTER TABLE `expert_run_actions` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '动作最近修改时间（UTC）';

-- 表名：专家运行上下文快照表（引用业务实体或对象的运行期快照）
ALTER TABLE `expert_run_contexts` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '上下文主键';
ALTER TABLE `expert_run_contexts` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `expert_run_contexts` MODIFY COLUMN `run_id` bigint NOT NULL COMMENT '所属运行主键，外键关联 expert_runs(id)，级联删除';
ALTER TABLE `expert_run_contexts` MODIFY COLUMN `context_type` varchar(16) NOT NULL COMMENT '上下文类型：todo（待办）/plan（计划）/calendar_event（日历事件）/attachment（附件）/file（文件）/text（文本）';
ALTER TABLE `expert_run_contexts` MODIFY COLUMN `context_id` bigint NULL COMMENT '上下文源记录主键（如 todo/plan 的 id），可空';
ALTER TABLE `expert_run_contexts` MODIFY COLUMN `snapshot_json` json NULL COMMENT '上下文内容快照 JSON，供运行期引用，可空';
ALTER TABLE `expert_run_contexts` MODIFY COLUMN `object_key` varchar(512) NULL COMMENT '对象存储键（文件类上下文的存储位置），可空';
ALTER TABLE `expert_run_contexts` MODIFY COLUMN `sha256` binary(32) NULL COMMENT '对象内容 SHA-256 摘要，仅存哈希引用，可空';
ALTER TABLE `expert_run_contexts` MODIFY COLUMN `mime_type` varchar(127) NULL COMMENT '对象 MIME 类型，可空';
ALTER TABLE `expert_run_contexts` MODIFY COLUMN `size_bytes` bigint NULL COMMENT '对象字节大小，可空';
ALTER TABLE `expert_run_contexts` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '快照创建时间（UTC）';

-- 表名：智能体运行期作业队列表（承载规划、确认、重试等任务）
ALTER TABLE `expert_jobs` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '作业主键';
ALTER TABLE `expert_jobs` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `expert_jobs` MODIFY COLUMN `run_id` bigint NOT NULL COMMENT '所属运行主键，外键关联 expert_runs(id)，级联删除';
ALTER TABLE `expert_jobs` MODIFY COLUMN `step_id` bigint NULL COMMENT '关联的运行步骤主键，可空，外键关联 run_steps(id)';
ALTER TABLE `expert_jobs` MODIFY COLUMN `job_type` varchar(16) NOT NULL COMMENT '作业类型：plan（规划）/execute（执行）/synthesize（合成）/retry（重试）';
ALTER TABLE `expert_jobs` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'queued' COMMENT '作业状态：queued（已入队）/leased（已租出处理中）/completed（完成）/failed（失败）/cancelled（取消）';
ALTER TABLE `expert_jobs` MODIFY COLUMN `idempotency_key` varchar(36) NOT NULL COMMENT '作业幂等键，用于去重';
ALTER TABLE `expert_jobs` MODIFY COLUMN `available_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '作业可被拉取的最早时间（UTC），支持延迟执行';
ALTER TABLE `expert_jobs` MODIFY COLUMN `leased_until` datetime(3) NULL COMMENT '作业租约到期时间（UTC），非空表示已被 worker 租出';
ALTER TABLE `expert_jobs` MODIFY COLUMN `attempt_no` int NOT NULL DEFAULT 0 COMMENT '已尝试次数';
ALTER TABLE `expert_jobs` MODIFY COLUMN `last_error_code` varchar(64) NULL COMMENT '最近一次失败的错误码，可空';
ALTER TABLE `expert_jobs` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '作业创建时间（UTC）';
ALTER TABLE `expert_jobs` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '最近更新时间（UTC）';

-- 表名：运行事件表（审计派生；不含提示与模型原始输出）
ALTER TABLE `run_events` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '事件主键';
ALTER TABLE `run_events` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `run_events` MODIFY COLUMN `run_id` bigint NOT NULL COMMENT '所属运行主键，外键关联 expert_runs(id)，级联删除';
ALTER TABLE `run_events` MODIFY COLUMN `step_id` bigint NULL COMMENT '关联步骤主键，可空，外键关联 run_steps(id)，级联置空';
ALTER TABLE `run_events` MODIFY COLUMN `sequence` int NOT NULL COMMENT '事件序号，运行内单调递增（与 run_id 联合唯一）';
ALTER TABLE `run_events` MODIFY COLUMN `event_type` varchar(32) NOT NULL COMMENT '事件类型：如 plan_ready/plan_revised/action_confirmed/action_executed/action_failed/pending_actions/context_collected/running/completed 等（开放集合）';
ALTER TABLE `run_events` MODIFY COLUMN `display_payload_json` json NOT NULL COMMENT '展示安全的负载 JSON，不含提示词与模型原始输出';
ALTER TABLE `run_events` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '事件创建时间（UTC）';

-- 表名：运行产物表（记录运行生成的输出对象）
ALTER TABLE `run_artifacts` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '产物主键';
ALTER TABLE `run_artifacts` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `run_artifacts` MODIFY COLUMN `run_id` bigint NOT NULL COMMENT '所属运行主键，外键关联 expert_runs(id)，级联删除';
ALTER TABLE `run_artifacts` MODIFY COLUMN `step_id` bigint NULL COMMENT '关联步骤主键，可空，外键关联 run_steps(id)，级联置空';
ALTER TABLE `run_artifacts` MODIFY COLUMN `object_key` varchar(512) NOT NULL COMMENT '对象存储键，全局唯一';
ALTER TABLE `run_artifacts` MODIFY COLUMN `sha256` binary(32) NOT NULL COMMENT '对象内容 SHA-256 摘要，仅存哈希引用';
ALTER TABLE `run_artifacts` MODIFY COLUMN `mime_type` varchar(127) NOT NULL COMMENT '产物 MIME 类型';
ALTER TABLE `run_artifacts` MODIFY COLUMN `size_bytes` bigint NOT NULL COMMENT '产物字节大小';
ALTER TABLE `run_artifacts` MODIFY COLUMN `metadata_json` json NULL COMMENT '产物元数据 JSON，结构语义由业务接入定义，可空';
ALTER TABLE `run_artifacts` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '产物创建时间（UTC）';

-- 表名：运行步骤表（运行内的执行单元）
ALTER TABLE `run_steps` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '步骤主键';
ALTER TABLE `run_steps` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `run_steps` MODIFY COLUMN `run_id` bigint NOT NULL COMMENT '所属运行主键，外键关联 expert_runs(id)，级联删除';
ALTER TABLE `run_steps` MODIFY COLUMN `parent_step_id` bigint NULL COMMENT '父步骤主键，可空，外键关联 run_steps(id)';
ALTER TABLE `run_steps` MODIFY COLUMN `expert_version_id` bigint NOT NULL COMMENT '执行该步骤的专家版本主键，外键关联 expert_versions(id)';
ALTER TABLE `run_steps` MODIFY COLUMN `step_type` varchar(16) NOT NULL COMMENT '步骤类型：plan（规划）/execute（执行）/synthesize（合成）/review（评审）';
ALTER TABLE `run_steps` MODIFY COLUMN `title` varchar(255) NOT NULL COMMENT '步骤标题，面向用户展示';
ALTER TABLE `run_steps` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'waiting' COMMENT '步骤状态：waiting（等待）/queued（已入队）/running（运行中）/completed（完成）/failed（失败）/cancelled（取消）/needs_input（需要输入）';
ALTER TABLE `run_steps` MODIFY COLUMN `input_json` json NULL COMMENT '步骤输入 JSON，可空';
ALTER TABLE `run_steps` MODIFY COLUMN `output_json` json NULL COMMENT '步骤输出 JSON，可空';
ALTER TABLE `run_steps` MODIFY COLUMN `display_summary` text NULL COMMENT '面向用户的步骤摘要，可空';
ALTER TABLE `run_steps` MODIFY COLUMN `attempt_no` int NOT NULL DEFAULT 0 COMMENT '已尝试次数';
ALTER TABLE `run_steps` MODIFY COLUMN `started_at` datetime(3) NULL COMMENT '实际开始时间（UTC）';
ALTER TABLE `run_steps` MODIFY COLUMN `finished_at` datetime(3) NULL COMMENT '结束时间（UTC）';
ALTER TABLE `run_steps` MODIFY COLUMN `error_code` varchar(64) NULL COMMENT '最近一次失败的错误码，可空';
ALTER TABLE `run_steps` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '步骤创建时间（UTC）';
ALTER TABLE `run_steps` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '最近更新时间（UTC）';
ALTER TABLE `run_steps` MODIFY COLUMN `row_version` bigint NOT NULL DEFAULT 1 COMMENT '行版本号，乐观锁比较字段，更新时递增';

-- 表名：运行步骤依赖表（步骤间前置关系）
ALTER TABLE `run_step_dependencies` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `run_step_dependencies` MODIFY COLUMN `step_id` bigint NOT NULL COMMENT '依赖方步骤主键，外键关联 run_steps(id)，级联删除';
ALTER TABLE `run_step_dependencies` MODIFY COLUMN `depends_on_step_id` bigint NOT NULL COMMENT '被依赖步骤主键，外键关联 run_steps(id)，级联删除；约束不允许与 step_id 相同（禁止自依赖）';

-- 表名：运行步骤 AI 调用用量表（token 与积分计量）
ALTER TABLE `run_step_usage` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '用量记录主键';
ALTER TABLE `run_step_usage` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，外键关联 tenants(id)';
ALTER TABLE `run_step_usage` MODIFY COLUMN `run_id` bigint NOT NULL COMMENT '所属运行主键，外键关联 expert_runs(id)，级联删除';
ALTER TABLE `run_step_usage` MODIFY COLUMN `step_id` bigint NULL COMMENT '关联步骤主键，可空，外键关联 run_steps(id)，级联置空';
ALTER TABLE `run_step_usage` MODIFY COLUMN `provider` varchar(32) NOT NULL COMMENT '模型提供方名称（如 openai 等）';
ALTER TABLE `run_step_usage` MODIFY COLUMN `model` varchar(128) NULL COMMENT '使用的模型名称，可空';
ALTER TABLE `run_step_usage` MODIFY COLUMN `request_id_hash` binary(32) NULL COMMENT '请求标识的 SHA-256 摘要，仅存哈希引用，可空';
ALTER TABLE `run_step_usage` MODIFY COLUMN `input_tokens` int NOT NULL DEFAULT 0 COMMENT '输入 token 数';
ALTER TABLE `run_step_usage` MODIFY COLUMN `output_tokens` int NOT NULL DEFAULT 0 COMMENT '输出 token 数';
ALTER TABLE `run_step_usage` MODIFY COLUMN `credits` decimal(18,4) NOT NULL DEFAULT 0.0000 COMMENT '本次调用折算积分';
ALTER TABLE `run_step_usage` MODIFY COLUMN `latency_ms` int NULL COMMENT '调用延迟（毫秒），可空';
ALTER TABLE `run_step_usage` MODIFY COLUMN `status` varchar(16) NOT NULL COMMENT '调用结果状态（如 success/failed 等，取值由业务接入时定义）';
ALTER TABLE `run_step_usage` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '用量记录创建时间（UTC）';


-- 表名：连接器提供方目录（登记可被租户接入的厂商类型）
ALTER TABLE `connector_providers` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '提供方主键';
ALTER TABLE `connector_providers` MODIFY COLUMN `code` varchar(64) NOT NULL COMMENT '提供方业务编码，全局唯一';
ALTER TABLE `connector_providers` MODIFY COLUMN `name` varchar(128) NOT NULL COMMENT '提供方对外展示名';
ALTER TABLE `connector_providers` MODIFY COLUMN `provider` varchar(64) NOT NULL COMMENT '底层实现供应商（如 home_assistant/mqtt 等）';
ALTER TABLE `connector_providers` MODIFY COLUMN `connector_type` varchar(32) NOT NULL COMMENT '连接器类型，如 smart_home/calendar 等';
ALTER TABLE `connector_providers` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'active' COMMENT '提供方状态：active 启用/disabled 停用';
ALTER TABLE `connector_providers` MODIFY COLUMN `description` text NULL COMMENT '提供方描述';
ALTER TABLE `connector_providers` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `connector_providers` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `connector_providers` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳';
ALTER TABLE `connector_providers` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 1 COMMENT '同步版本号，用于增量同步';

-- 表名：工作区连接器实例（租户实际接入的连接器实例）
ALTER TABLE `workspace_connectors` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '连接器实例主键';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants.id';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `connector_provider_id` bigint NOT NULL COMMENT '所用提供方主键，关联 connector_providers.id';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `binding_scope` varchar(16) NOT NULL DEFAULT 'household' COMMENT '绑定范围：household 家庭共享实例/personal 成员个人实例';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `owner_user_id` bigint NULL COMMENT '个人实例所有者用户主键，家庭实例必须为空，关联 users.id';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `name` varchar(128) NOT NULL COMMENT '租户侧自定义的连接器名称';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `credential_ref` varchar(512) NULL COMMENT '凭据引用（仅存密钥服务引用，不落明文，由 Vault 托管）';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'disconnected' COMMENT '连接器状态：authorizing/connected/disconnected/failed/disabled';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `auth_status` varchar(16) NOT NULL DEFAULT 'none' COMMENT '授权生命周期状态：none/authorizing/connected/revoked/failed';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `config` json NULL COMMENT '非敏感配置 JSON，仅保存经 Provider Schema 校验的非敏感配置，凭据由 Vault 托管';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `last_sync_at` datetime(3) NULL COMMENT '最近一次同步时间（UTC）';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `last_health_at` datetime(3) NULL COMMENT '最近一次健康探测时间（UTC）';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳';
ALTER TABLE `workspace_connectors` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 1 COMMENT '同步版本号，用于增量同步';

-- 表名：连接器同步任务（承载后台重试与重排队）
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '同步任务主键';
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants.id';
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `workspace_connector_id` bigint NOT NULL COMMENT '所属工作区连接器主键，关联 workspace_connectors.id';
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'queued' COMMENT '任务状态：queued/running/completed/failed';
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `reason` varchar(32) NOT NULL DEFAULT 'manual' COMMENT '任务触发原因，如 manual 手动/scheduled 定时等';
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `attempt_no` int NOT NULL DEFAULT 0 COMMENT '当前重试次数（首次为 1）';
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `available_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '任务可被拉取的最早时间（UTC）';
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `started_at` datetime(3) NULL COMMENT '实际开始时间（UTC）';
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `completed_at` datetime(3) NULL COMMENT '完成时间（UTC），包括失败完成';
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `last_error_code` varchar(64) NULL COMMENT '最近一次失败的错误码';
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `idempotency_key` varchar(36) NOT NULL COMMENT '幂等键，避免重复入队，租户内唯一';
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `connector_sync_jobs` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 1 COMMENT '同步版本号，用于增量同步';

-- 表名：连接器授权会话（承载个人 OAuth 或受控家庭授权的短期一次性服务端会话，不保存授权 code 与令牌）
ALTER TABLE `connector_authorization_sessions` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '会话主键';
ALTER TABLE `connector_authorization_sessions` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants.id';
ALTER TABLE `connector_authorization_sessions` MODIFY COLUMN `connector_provider_id` bigint NOT NULL COMMENT '所授权连接器提供方主键，关联 connector_providers.id';
ALTER TABLE `connector_authorization_sessions` MODIFY COLUMN `binding_scope` varchar(16) NOT NULL DEFAULT 'personal' COMMENT '绑定范围：household/personal，当前实现固定为 personal';
ALTER TABLE `connector_authorization_sessions` MODIFY COLUMN `initiator_user_id` bigint NOT NULL COMMENT '发起授权的用户主键，关联 users.id';
ALTER TABLE `connector_authorization_sessions` MODIFY COLUMN `state_hash` char(64) NOT NULL COMMENT '一次性 state 的 SHA-256 十六进制哈希，回调时校验、使用后失效，仅存哈希引用';
ALTER TABLE `connector_authorization_sessions` MODIFY COLUMN `pkce_verifier_ref` varchar(512) NULL COMMENT 'PKCE 校验器引用（仅存密钥服务引用，不落明文，由 Vault 托管）';
ALTER TABLE `connector_authorization_sessions` MODIFY COLUMN `redirect_uri` varchar(512) NOT NULL COMMENT '回调跳转地址，必须命中 Provider 预注册白名单';
ALTER TABLE `connector_authorization_sessions` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'pending' COMMENT '会话状态：pending/used/expired/revoked/completed/failed';
ALTER TABLE `connector_authorization_sessions` MODIFY COLUMN `expires_at` datetime(3) NOT NULL COMMENT '会话过期时间（UTC），过期后回调拒绝';
ALTER TABLE `connector_authorization_sessions` MODIFY COLUMN `completed_at` datetime(3) NULL COMMENT '会话完成时间（UTC），成功回调或撤销时写入';
ALTER TABLE `connector_authorization_sessions` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `connector_authorization_sessions` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';

-- 表名：用户对连接器的范围授权
ALTER TABLE `user_connector_authorizations` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '授权主键';
ALTER TABLE `user_connector_authorizations` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants.id';
ALTER TABLE `user_connector_authorizations` MODIFY COLUMN `user_id` bigint NOT NULL COMMENT '被授权用户主键，关联 users.id';
ALTER TABLE `user_connector_authorizations` MODIFY COLUMN `workspace_connector_id` bigint NOT NULL COMMENT '工作区连接器主键，关联 workspace_connectors.id';
ALTER TABLE `user_connector_authorizations` MODIFY COLUMN `scope_json` json NOT NULL COMMENT '授权范围 JSON 字符串（权限列表），需与设备能力 permission 匹配';
ALTER TABLE `user_connector_authorizations` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `user_connector_authorizations` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `user_connector_authorizations` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳';
ALTER TABLE `user_connector_authorizations` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 1 COMMENT '同步版本号，用于增量同步';

-- 表名：智能家居空间（如客厅、卧室）的归一化视图
ALTER TABLE `smart_home_spaces` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '空间主键';
ALTER TABLE `smart_home_spaces` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants.id';
ALTER TABLE `smart_home_spaces` MODIFY COLUMN `name` varchar(128) NOT NULL COMMENT '空间名称';
ALTER TABLE `smart_home_spaces` MODIFY COLUMN `space_type` varchar(32) NOT NULL COMMENT '空间类型，例如 living_room/bedroom 等';
ALTER TABLE `smart_home_spaces` MODIFY COLUMN `summary` varchar(512) NULL COMMENT '空间摘要，便于前端列表展示';
ALTER TABLE `smart_home_spaces` MODIFY COLUMN `sort_order` int NOT NULL DEFAULT 0 COMMENT '前端排序值，越小越靠前';
ALTER TABLE `smart_home_spaces` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `smart_home_spaces` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `smart_home_spaces` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳';
ALTER TABLE `smart_home_spaces` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 1 COMMENT '同步版本号，用于增量同步';

-- 表名：智能家居设备归一化实体（兼容多种底层协议）
ALTER TABLE `smart_home_devices` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '设备主键';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants.id';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `workspace_connector_id` bigint NULL COMMENT '所属工作区连接器主键，平台设备可为空，关联 workspace_connectors.id';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `space_id` bigint NULL COMMENT '所属空间主键，可为空表示未分配空间，关联 smart_home_spaces.id';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `external_id` varchar(255) NULL COMMENT '底层厂商实体 ID，不对外返回';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `name` varchar(128) NOT NULL COMMENT '设备对外展示名';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `device_type` varchar(32) NOT NULL COMMENT '设备类型，例如 light/switch/sensor 等';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `online_status` varchar(16) NOT NULL DEFAULT 'unknown' COMMENT '在线状态：online/offline/unknown';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `state_summary` varchar(512) NULL COMMENT '状态摘要，便于列表展示';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `last_seen_at` datetime(3) NULL COMMENT '最近一次上报时间（UTC）';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 1 COMMENT '同步版本号，用于增量同步';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `battery_level` tinyint unsigned NULL COMMENT '电池电量百分比 0-100，无电池设备为空';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `health_status` varchar(16) NOT NULL COMMENT '设备健康状态，参见 DeviceHealthStatus';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `signal_lqi` int NULL COMMENT '信号 LQI 值，数值越大信号越好';
ALTER TABLE `smart_home_devices` MODIFY COLUMN `zigbee_role` varchar(16) NULL COMMENT '归一化 Zigbee 角色，例如 router/end_device 等';

-- 表名：设备能力声明（决定可调用与可读取的字段）
ALTER TABLE `device_capabilities` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '能力主键';
ALTER TABLE `device_capabilities` MODIFY COLUMN `device_id` bigint NOT NULL COMMENT '所属设备主键，关联 smart_home_devices.id';
ALTER TABLE `device_capabilities` MODIFY COLUMN `capability` varchar(64) NOT NULL COMMENT '能力名，例如 on_off/brightness 等';
ALTER TABLE `device_capabilities` MODIFY COLUMN `value_schema_json` json NOT NULL COMMENT '能力取值 JSON Schema 字符串';
ALTER TABLE `device_capabilities` MODIFY COLUMN `permission` varchar(64) NOT NULL COMMENT '所需权限名，需与用户授权范围匹配';
ALTER TABLE `device_capabilities` MODIFY COLUMN `is_writable` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否可写（0 只读/1 可写）';
ALTER TABLE `device_capabilities` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `device_capabilities` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `device_capabilities` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳';
ALTER TABLE `device_capabilities` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 1 COMMENT '同步版本号，用于增量同步';

-- 表名：设备状态采样（按时间倒序保留最近若干条）
ALTER TABLE `device_states` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '状态主键';
ALTER TABLE `device_states` MODIFY COLUMN `device_id` bigint NOT NULL COMMENT '所属设备主键，关联 smart_home_devices.id';
ALTER TABLE `device_states` MODIFY COLUMN `state_json` json NOT NULL COMMENT '设备状态 JSON 字符串（各能力当前取值）';
ALTER TABLE `device_states` MODIFY COLUMN `sampled_at` datetime(3) NOT NULL COMMENT '采样时间（UTC）';
ALTER TABLE `device_states` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '入库时间（UTC）';

-- 表名：场景定义（由若干设备动作组合）
ALTER TABLE `scenes` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '场景主键';
ALTER TABLE `scenes` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants.id';
ALTER TABLE `scenes` MODIFY COLUMN `scene_key` varchar(64) NOT NULL COMMENT '场景业务键，用于路由与快捷方式，租户内唯一';
ALTER TABLE `scenes` MODIFY COLUMN `name` varchar(128) NOT NULL COMMENT '场景对外展示名';
ALTER TABLE `scenes` MODIFY COLUMN `summary` varchar(512) NULL COMMENT '场景摘要';
ALTER TABLE `scenes` MODIFY COLUMN `status` varchar(16) NOT NULL DEFAULT 'active' COMMENT '场景状态：active/disabled';
ALTER TABLE `scenes` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `scenes` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `scenes` MODIFY COLUMN `deleted_at` datetime(3) NULL COMMENT '软删除时间戳';
ALTER TABLE `scenes` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 1 COMMENT '同步版本号，用于增量同步';

-- 表名：场景动作（描述对设备能力的赋值）
ALTER TABLE `scene_actions` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '动作主键';
ALTER TABLE `scene_actions` MODIFY COLUMN `scene_id` bigint NOT NULL COMMENT '所属场景主键，关联 scenes.id';
ALTER TABLE `scene_actions` MODIFY COLUMN `device_id` bigint NOT NULL COMMENT '目标设备主键，关联 smart_home_devices.id';
ALTER TABLE `scene_actions` MODIFY COLUMN `capability` varchar(64) NOT NULL COMMENT '目标能力名';
ALTER TABLE `scene_actions` MODIFY COLUMN `target_value_json` json NOT NULL COMMENT '目标值 JSON 字符串';
ALTER TABLE `scene_actions` MODIFY COLUMN `sort_order` int NOT NULL DEFAULT 0 COMMENT '执行顺序，数值小的先执行';
ALTER TABLE `scene_actions` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `scene_actions` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';

-- 表名：自动化规则（租户隔离的长期运行触发器）
ALTER TABLE `automation_rules` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '规则主键';
ALTER TABLE `automation_rules` MODIFY COLUMN `tenant_id` bigint NOT NULL COMMENT '所属租户标识，关联 tenants.id';
ALTER TABLE `automation_rules` MODIFY COLUMN `owner_user_id` bigint NOT NULL COMMENT '规则所有者用户标识，关联 users.id';
ALTER TABLE `automation_rules` MODIFY COLUMN `name` varchar(128) NOT NULL COMMENT '规则名称';
ALTER TABLE `automation_rules` MODIFY COLUMN `trigger_type` varchar(32) NOT NULL COMMENT '触发类型：time_schedule/device_state_change/scene_completed/sync_completed';
ALTER TABLE `automation_rules` MODIFY COLUMN `trigger_config_json` json NOT NULL COMMENT '触发配置 JSON 字符串（如定时表达式、设备状态条件）';
ALTER TABLE `automation_rules` MODIFY COLUMN `conditions_json` json NOT NULL COMMENT '额外条件 JSON 数组';
ALTER TABLE `automation_rules` MODIFY COLUMN `actions_json` json NOT NULL COMMENT '动作列表 JSON 数组，限制为内置场景键';
ALTER TABLE `automation_rules` MODIFY COLUMN `approval_policy` varchar(32) NOT NULL DEFAULT 'manual_confirmation' COMMENT '审批策略：manual_confirmation 人工确认/auto_execute 自动执行';
ALTER TABLE `automation_rules` MODIFY COLUMN `enabled` tinyint(1) NOT NULL DEFAULT 1 COMMENT '是否启用规则（0 停用/1 启用）';
ALTER TABLE `automation_rules` MODIFY COLUMN `last_triggered_at` datetime(3) NULL COMMENT '最近一次触发时间（UTC）';
ALTER TABLE `automation_rules` MODIFY COLUMN `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `automation_rules` MODIFY COLUMN `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `automation_rules` MODIFY COLUMN `row_version` bigint NOT NULL DEFAULT 1 COMMENT '乐观锁版本号';
ALTER TABLE `automation_rules` MODIFY COLUMN `sync_version` bigint NOT NULL DEFAULT 1 COMMENT '同步版本号，用于增量同步';

-- 表名：家庭成员（归属家庭，是家庭上下文的核心维度）
ALTER TABLE `family_members` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '成员主键';
ALTER TABLE `family_members` MODIFY COLUMN `home_id` bigint NOT NULL COMMENT '所属家庭主键，关联 tenants.id';
ALTER TABLE `family_members` MODIFY COLUMN `name` longtext NOT NULL COMMENT '成员显示名';
ALTER TABLE `family_members` MODIFY COLUMN `relation` longtext NOT NULL COMMENT '与户主关系，例如 self/spouse/child 等';
ALTER TABLE `family_members` MODIFY COLUMN `birthday` date NULL COMMENT '生日，可空表示未知';
ALTER TABLE `family_members` MODIFY COLUMN `is_elderly` tinyint(1) NOT NULL COMMENT '是否标记为老人，影响健康建议与通知策略';
ALTER TABLE `family_members` MODIFY COLUMN `is_child` tinyint(1) NOT NULL COMMENT '是否标记为儿童，影响自动化与权限';
ALTER TABLE `family_members` MODIFY COLUMN `is_primary` tinyint(1) NOT NULL COMMENT '是否家庭主用户';
ALTER TABLE `family_members` MODIFY COLUMN `member_status` varchar(24) NOT NULL DEFAULT 'active' COMMENT '成员生命周期状态：active 在册/away 短期离开/permanently_left 永久离开/deceased 已故';
ALTER TABLE `family_members` MODIFY COLUMN `preferences_json` json NULL COMMENT '成员偏好 JSON，由管家与建议系统解析';
ALTER TABLE `family_members` MODIFY COLUMN `created_by_user_id` bigint NOT NULL COMMENT '创建成员的用户标识，关联 users.id';
ALTER TABLE `family_members` MODIFY COLUMN `terminal_corrected_by_user_id` bigint NULL COMMENT '终态更正操作者用户标识，仅在终态变更时填写';
ALTER TABLE `family_members` MODIFY COLUMN `terminal_correction_reason` longtext NULL COMMENT '终态更正原因，可审计';
ALTER TABLE `family_members` MODIFY COLUMN `terminal_corrected_at` datetime(6) NULL COMMENT '终态更正时间（UTC）';
ALTER TABLE `family_members` MODIFY COLUMN `deleted_at` datetime(6) NULL COMMENT '软删除时间戳';
ALTER TABLE `family_members` MODIFY COLUMN `created_at` datetime(6) NOT NULL COMMENT '创建时间（UTC）';
ALTER TABLE `family_members` MODIFY COLUMN `updated_at` datetime(6) NOT NULL COMMENT '更新时间（UTC）';
ALTER TABLE `family_members` MODIFY COLUMN `row_version` bigint NOT NULL COMMENT '乐观锁版本号';
ALTER TABLE `family_members` MODIFY COLUMN `sync_version` bigint NOT NULL COMMENT '同步版本号，用于增量同步';

-- 表名：家庭知识（按 key 写入并保留来源与冲突解决结果）
ALTER TABLE `family_knowledge` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '知识主键';
ALTER TABLE `family_knowledge` MODIFY COLUMN `home_id` bigint NOT NULL COMMENT '所属家庭主键，关联 tenants.id';
ALTER TABLE `family_knowledge` MODIFY COLUMN `category` varchar(32) NOT NULL COMMENT '知识分类：property/wifi/repair/cleaning/insurance/other';
ALTER TABLE `family_knowledge` MODIFY COLUMN `knowledge_key` varchar(256) NOT NULL COMMENT '知识键，同家庭内用于去重与冲突合并';
ALTER TABLE `family_knowledge` MODIFY COLUMN `knowledge_value` longtext NOT NULL COMMENT '知识值';
ALTER TABLE `family_knowledge` MODIFY COLUMN `notes` longtext NULL COMMENT '补充说明';
ALTER TABLE `family_knowledge` MODIFY COLUMN `source_type` longtext NOT NULL COMMENT '来源类型：member 家庭成员主动写入/system_ai 系统 AI 推断';
ALTER TABLE `family_knowledge` MODIFY COLUMN `source_member_id` bigint NULL COMMENT '来源成员主键，系统 AI 来源时为空，关联 family_members.id';
ALTER TABLE `family_knowledge` MODIFY COLUMN `confidence_score` decimal(4,3) NOT NULL COMMENT '置信度，范围 0-1';
ALTER TABLE `family_knowledge` MODIFY COLUMN `conflict_resolution_strategy` longtext NOT NULL COMMENT '冲突解决策略：latest 以最近写入为准/authority 以权威来源为准/majority 以多数来源为准';
ALTER TABLE `family_knowledge` MODIFY COLUMN `resolution_summary` longtext NULL COMMENT '冲突解决结果摘要';
ALTER TABLE `family_knowledge` MODIFY COLUMN `created_by_user_id` bigint NULL COMMENT '创建知识条目的用户标识，系统来源可为空';
ALTER TABLE `family_knowledge` MODIFY COLUMN `deleted_at` datetime(6) NULL COMMENT '软删除时间戳';
ALTER TABLE `family_knowledge` MODIFY COLUMN `created_at` datetime(6) NOT NULL COMMENT '创建时间（UTC）';
ALTER TABLE `family_knowledge` MODIFY COLUMN `updated_at` datetime(6) NOT NULL COMMENT '更新时间（UTC）';
ALTER TABLE `family_knowledge` MODIFY COLUMN `row_version` bigint NOT NULL COMMENT '乐观锁版本号';
ALTER TABLE `family_knowledge` MODIFY COLUMN `sync_version` bigint NOT NULL COMMENT '同步版本号，用于增量同步';

-- 表名：家庭决策历史（保留可追溯的决策与理由）
ALTER TABLE `decision_history` MODIFY COLUMN `id` bigint NOT NULL AUTO_INCREMENT COMMENT '决策主键';
ALTER TABLE `decision_history` MODIFY COLUMN `home_id` bigint NOT NULL COMMENT '所属家庭主键，关联 tenants.id';
ALTER TABLE `decision_history` MODIFY COLUMN `scenario` longtext NOT NULL COMMENT '决策场景，如 晚餐安排/出行计划 等';
ALTER TABLE `decision_history` MODIFY COLUMN `decision_made` longtext NOT NULL COMMENT '所做决策内容';
ALTER TABLE `decision_history` MODIFY COLUMN `rationale` longtext NULL COMMENT '决策理由说明';
ALTER TABLE `decision_history` MODIFY COLUMN `alternatives_json` json NULL COMMENT '备选方案 JSON 数组';
ALTER TABLE `decision_history` MODIFY COLUMN `made_by_member_id` bigint NULL COMMENT '决策者关联的家庭成员主键，关联 family_members.id';
ALTER TABLE `decision_history` MODIFY COLUMN `made_by_user_id` bigint NULL COMMENT '决策者用户标识，系统决策可为空';
ALTER TABLE `decision_history` MODIFY COLUMN `decided_at` datetime(6) NOT NULL COMMENT '决策时间（UTC）';
ALTER TABLE `decision_history` MODIFY COLUMN `deleted_at` datetime(6) NULL COMMENT '软删除时间戳';
ALTER TABLE `decision_history` MODIFY COLUMN `created_at` datetime(6) NOT NULL COMMENT '创建时间（UTC）';
ALTER TABLE `decision_history` MODIFY COLUMN `updated_at` datetime(6) NOT NULL COMMENT '更新时间（UTC）';
ALTER TABLE `decision_history` MODIFY COLUMN `row_version` bigint NOT NULL COMMENT '乐观锁版本号';
ALTER TABLE `decision_history` MODIFY COLUMN `sync_version` bigint NOT NULL COMMENT '同步版本号，用于增量同步';


-- ============================================================
-- 032 迁移（第 E 片）：为 16 张表补充列中文备注（ALTER ... MODIFY COLUMN ... COMMENT）
-- 类型/可空/默认值/EXTRA 严格照抄当前库结构（.build/tables/*.tsv）
-- 语义来源：HomeMind.Common.Model/Entities 实体类注释 + database/*.mysql.sql 迁移注释
-- ============================================================

-- 表名：scenario_templates 平台级场景模板，由平台定义能力模板；家庭启用后生成实例，不直接执行
ALTER TABLE `scenario_templates` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '模板主键';
ALTER TABLE `scenario_templates` MODIFY COLUMN `tenant_id` BIGINT NOT NULL DEFAULT 1 COMMENT '模板归属租户，平台模板固定为 1，与平台专家同惯例（外键：tenants.id）';
ALTER TABLE `scenario_templates` MODIFY COLUMN `code` VARCHAR(64) NOT NULL COMMENT '模板业务键，全局唯一，如 goodnight / arrive_home / leave_home';
ALTER TABLE `scenario_templates` MODIFY COLUMN `name` VARCHAR(50) NOT NULL COMMENT '模板展示名';
ALTER TABLE `scenario_templates` MODIFY COLUMN `summary` VARCHAR(255) NULL COMMENT '模板摘要';
ALTER TABLE `scenario_templates` MODIFY COLUMN `status` VARCHAR(16) NOT NULL DEFAULT 'active' COMMENT '模板状态：active（可被家庭启用）/inactive（已停用，不再允许新启用）';
ALTER TABLE `scenario_templates` MODIFY COLUMN `trigger_keywords_json` JSON NULL COMMENT '触发关键词 JSON 数组，语音入口按关键词确定性匹配';
ALTER TABLE `scenario_templates` MODIFY COLUMN `steps_json` JSON NOT NULL COMMENT '模板步骤 JSON 数组，步骤未解析设备：id/name/device_type/room/capability/value/optional';
ALTER TABLE `scenario_templates` MODIFY COLUMN `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `scenario_templates` MODIFY COLUMN `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `scenario_templates` MODIFY COLUMN `deleted_at` DATETIME(3) NULL COMMENT '软删除时间戳，非空表示已删除';
ALTER TABLE `scenario_templates` MODIFY COLUMN `sync_version` BIGINT NOT NULL DEFAULT 1 COMMENT '同步版本号，用于同步冲突检测';

-- 表名：scenario_instances 家庭启用的场景实例，步骤经 Device Resolver 解析为具体设备并记录可用性
ALTER TABLE `scenario_instances` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '实例主键';
ALTER TABLE `scenario_instances` MODIFY COLUMN `tenant_id` BIGINT NOT NULL COMMENT '所属租户标识（外键：tenants.id）';
ALTER TABLE `scenario_instances` MODIFY COLUMN `template_code` VARCHAR(64) NOT NULL COMMENT '来源模板业务键（scenario_templates.code）';
ALTER TABLE `scenario_instances` MODIFY COLUMN `name` VARCHAR(50) NOT NULL COMMENT '实例展示名，启用时取自模板';
ALTER TABLE `scenario_instances` MODIFY COLUMN `trigger_keywords_json` JSON NULL COMMENT '触发关键词快照 JSON 数组，启用时从模板复制';
ALTER TABLE `scenario_instances` MODIFY COLUMN `steps_json` JSON NOT NULL COMMENT '解析后步骤 JSON 数组：模板字段之外附加 device_id/step_status/reason；step_status 取 ready（可执行）/unavailable（无匹配设备，执行时跳过）';
ALTER TABLE `scenario_instances` MODIFY COLUMN `status` VARCHAR(16) NOT NULL DEFAULT 'enabled' COMMENT '实例状态：enabled（已启用，可运行）/disabled（已停用）';
ALTER TABLE `scenario_instances` MODIFY COLUMN `created_by_user_id` BIGINT NOT NULL COMMENT '启用实例的用户标识';
ALTER TABLE `scenario_instances` MODIFY COLUMN `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `scenario_instances` MODIFY COLUMN `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `scenario_instances` MODIFY COLUMN `deleted_at` DATETIME(3) NULL COMMENT '软删除时间戳，非空表示已删除';
ALTER TABLE `scenario_instances` MODIFY COLUMN `row_version` BIGINT NOT NULL DEFAULT 1 COMMENT '乐观锁版本号';
ALTER TABLE `scenario_instances` MODIFY COLUMN `sync_version` BIGINT NOT NULL DEFAULT 1 COMMENT '同步版本号，用于同步冲突检测';

-- 表名：steward_activities 管家活动记录，关联运行并向用户呈现的执行流
ALTER TABLE `steward_activities` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '活动主键';
ALTER TABLE `steward_activities` MODIFY COLUMN `home_id` BIGINT NOT NULL COMMENT '所属家庭主键（外键：tenants.id）';
ALTER TABLE `steward_activities` MODIFY COLUMN `run_id` BIGINT NULL COMMENT '关联的 AgentRun（expert_runs）主键，可为空表示非运行期活动';
ALTER TABLE `steward_activities` MODIFY COLUMN `category` LONGTEXT NOT NULL COMMENT '活动分类：sensing（感知）/planning（规划）/executing（执行）/reporting（汇报）';
ALTER TABLE `steward_activities` MODIFY COLUMN `title` LONGTEXT NOT NULL COMMENT '活动标题';
ALTER TABLE `steward_activities` MODIFY COLUMN `description` LONGTEXT NULL COMMENT '活动描述';
ALTER TABLE `steward_activities` MODIFY COLUMN `risk_level` LONGTEXT NOT NULL COMMENT '风险等级：L1（低风险，允许批量确认）/L2（中风险，单项确认）/L3（高风险，强制实时复核）';
ALTER TABLE `steward_activities` MODIFY COLUMN `status` LONGTEXT NOT NULL COMMENT '活动状态：pending（等待确认）/confirmed（已确认）/executing（执行中）/completed（成功完成）/failed（执行失败）/cancelled（已取消）';
ALTER TABLE `steward_activities` MODIFY COLUMN `result_summary` LONGTEXT NULL COMMENT '结果摘要';
ALTER TABLE `steward_activities` MODIFY COLUMN `undoable` TINYINT(1) NOT NULL COMMENT '是否可被撤销：0 否/1 是';
ALTER TABLE `steward_activities` MODIFY COLUMN `undone_at` DATETIME(6) NULL COMMENT '撤销时间（UTC）';
ALTER TABLE `steward_activities` MODIFY COLUMN `created_at` DATETIME(6) NOT NULL COMMENT '创建时间（UTC）';
ALTER TABLE `steward_activities` MODIFY COLUMN `updated_at` DATETIME(6) NOT NULL COMMENT '更新时间（UTC）';
ALTER TABLE `steward_activities` MODIFY COLUMN `row_version` BIGINT NOT NULL COMMENT '乐观锁版本号';
ALTER TABLE `steward_activities` MODIFY COLUMN `sync_version` BIGINT NOT NULL COMMENT '同步版本号，用于同步冲突检测';

-- 表名：confirmation_items 确认项实体，向用户呈现的待确认动作
ALTER TABLE `confirmation_items` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '确认项主键';
ALTER TABLE `confirmation_items` MODIFY COLUMN `home_id` BIGINT NOT NULL COMMENT '所属家庭主键（外键：tenants.id）';
ALTER TABLE `confirmation_items` MODIFY COLUMN `activity_id` BIGINT NULL COMMENT '关联的管家活动主键，可为空表示由系统直接生成（外键：steward_activities.id）';
ALTER TABLE `confirmation_items` MODIFY COLUMN `risk_level` LONGTEXT NOT NULL COMMENT '风险等级：L1（低风险，允许批量确认）/L2（中风险，单项确认并附加影响说明）/L3（高风险，单项确认并强制实时复核）';
ALTER TABLE `confirmation_items` MODIFY COLUMN `title` LONGTEXT NOT NULL COMMENT '确认项标题';
ALTER TABLE `confirmation_items` MODIFY COLUMN `description` LONGTEXT NULL COMMENT '确认项描述';
ALTER TABLE `confirmation_items` MODIFY COLUMN `impact_summary` LONGTEXT NULL COMMENT '影响摘要';
ALTER TABLE `confirmation_items` MODIFY COLUMN `suggested_action` LONGTEXT NULL COMMENT '建议动作文案';
ALTER TABLE `confirmation_items` MODIFY COLUMN `status` LONGTEXT NOT NULL COMMENT '确认项状态：pending（等待处理）/confirmed（已确认）/denied（已拒绝）/expired（已过期）/cancelled（已取消）';
ALTER TABLE `confirmation_items` MODIFY COLUMN `expires_at` DATETIME(6) NULL COMMENT '到期时间（UTC），到期后系统将自动取消';
ALTER TABLE `confirmation_items` MODIFY COLUMN `confirmed_by_user_id` BIGINT NULL COMMENT '确认操作用户标识（外键：users.id）';
ALTER TABLE `confirmation_items` MODIFY COLUMN `confirmed_at` DATETIME(6) NULL COMMENT '确认时间（UTC）';
ALTER TABLE `confirmation_items` MODIFY COLUMN `denied_by_user_id` BIGINT NULL COMMENT '拒绝操作用户标识（外键：users.id）';
ALTER TABLE `confirmation_items` MODIFY COLUMN `denied_at` DATETIME(6) NULL COMMENT '拒绝时间（UTC）';
ALTER TABLE `confirmation_items` MODIFY COLUMN `denial_reason` LONGTEXT NULL COMMENT '拒绝原因，便于审计';
ALTER TABLE `confirmation_items` MODIFY COLUMN `expired_at` DATETIME(6) NULL COMMENT '过期时间戳，由系统按策略回填';
ALTER TABLE `confirmation_items` MODIFY COLUMN `created_at` DATETIME(6) NOT NULL COMMENT '创建时间（UTC）';
ALTER TABLE `confirmation_items` MODIFY COLUMN `updated_at` DATETIME(6) NOT NULL COMMENT '更新时间（UTC）';
ALTER TABLE `confirmation_items` MODIFY COLUMN `row_version` BIGINT NOT NULL COMMENT '乐观锁版本号';
ALTER TABLE `confirmation_items` MODIFY COLUMN `sync_version` BIGINT NOT NULL COMMENT '同步版本号，用于同步冲突检测';

-- 表名：confirmation_batch_records L1 批量确认幂等记录，以 (home_id, idempotency_key) 唯一保存首次请求的确认项集合与结果，供重复请求重放
ALTER TABLE `confirmation_batch_records` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '幂等记录主键';
ALTER TABLE `confirmation_batch_records` MODIFY COLUMN `home_id` BIGINT NOT NULL COMMENT '所属家庭主键（外键：tenants.id）';
ALTER TABLE `confirmation_batch_records` MODIFY COLUMN `idempotency_key` VARCHAR(255) NOT NULL COMMENT '幂等键（UUID），重复请求必须复用首次请求的键，(home_id, idempotency_key) 唯一';
ALTER TABLE `confirmation_batch_records` MODIFY COLUMN `confirmation_ids_json` JSON NOT NULL COMMENT '首次请求的确认项 ID 数组 JSON，用于同键比对集合是否一致';
ALTER TABLE `confirmation_batch_records` MODIFY COLUMN `result_json` JSON NOT NULL COMMENT '首次确认的结果视图 JSON，重复请求直接重放该结果';
ALTER TABLE `confirmation_batch_records` MODIFY COLUMN `created_at` DATETIME(6) NOT NULL COMMENT '创建时间（UTC）';

-- 表名：family_audit_logs 家庭域审计日志，与管家动态、运行事件分离，专门承载 Family 域可审计动作
ALTER TABLE `family_audit_logs` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '审计主键';
ALTER TABLE `family_audit_logs` MODIFY COLUMN `home_id` BIGINT NOT NULL COMMENT '所属家庭主键（外键：tenants.id）';
ALTER TABLE `family_audit_logs` MODIFY COLUMN `actor_user_id` BIGINT NULL COMMENT '执行操作的用户标识，系统行为可为空（外键：users.id）';
ALTER TABLE `family_audit_logs` MODIFY COLUMN `action` LONGTEXT NOT NULL COMMENT '审计动作（FamilyAuditActions 前缀分组）：member_correction/member_terminal_restore、knowledge_write/knowledge_conflict_resolved、decision_record、confirmation_confirm/confirmation_deny/confirmation_batch、activity_undo、favorite_*、connector_authorize_*、tenant_member_role_changed/tenant_member_status_changed、tenant_invitation_created/revoked/accepted、tenant_owner_transferred、web_navigation_preference_updated、conversation_create/rename/delete、skill_run_created/skill_action_confirmed/skill_draft_registered/skill_run_revised、media_file_uploaded/media_file_deleted、xhs_note_published';
ALTER TABLE `family_audit_logs` MODIFY COLUMN `target_type` LONGTEXT NOT NULL COMMENT '审计目标类型（FamilyAuditTargetTypes）：family_member/family_knowledge/decision_history/confirmation_item/steward_activity/personal_favorite/connector_authorization/tenant_member/tenant_invitation/web_navigation_preference/conversation/skill_run/skill_draft/xhs_note/clipping_material';
ALTER TABLE `family_audit_logs` MODIFY COLUMN `target_id` BIGINT NULL COMMENT '审计目标主键，可空（例如新增主键尚未回填）';
ALTER TABLE `family_audit_logs` MODIFY COLUMN `before_json` JSON NULL COMMENT '操作前状态 JSON 序列化结果';
ALTER TABLE `family_audit_logs` MODIFY COLUMN `after_json` JSON NULL COMMENT '操作后状态 JSON 序列化结果';
ALTER TABLE `family_audit_logs` MODIFY COLUMN `reason` LONGTEXT NULL COMMENT '操作原因，可空';
ALTER TABLE `family_audit_logs` MODIFY COLUMN `related_run_id` BIGINT NULL COMMENT '关联的专家运行主键，可空；与运行/确认链路同源关联（外键：expert_runs.id）';
ALTER TABLE `family_audit_logs` MODIFY COLUMN `created_at` DATETIME(6) NOT NULL COMMENT '审计时间（UTC）';

-- 表名：action_execution_audits 动作执行审计，记录每次确认/调度尝试（无凭据审计轨迹）
ALTER TABLE `action_execution_audits` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '审计主键';
ALTER TABLE `action_execution_audits` MODIFY COLUMN `tenant_id` BIGINT NOT NULL COMMENT '所属租户标识（外键：tenants.id）';
ALTER TABLE `action_execution_audits` MODIFY COLUMN `run_action_id` BIGINT NOT NULL COMMENT '关联的动作主键（外键：expert_run_actions.id）';
ALTER TABLE `action_execution_audits` MODIFY COLUMN `operator_user_id` BIGINT NOT NULL COMMENT '实际执行动作的用户标识（含自动策略归属，外键：users.id）';
ALTER TABLE `action_execution_audits` MODIFY COLUMN `workspace_connector_id` BIGINT NULL COMMENT '调度所用工作区连接器主键；非设备类动作（如日历同步）为空（外键：workspace_connectors.id）';
ALTER TABLE `action_execution_audits` MODIFY COLUMN `device_id` BIGINT NULL COMMENT '目标设备主键；非设备类动作（如日历同步）为空（外键：smart_home_devices.id）';
ALTER TABLE `action_execution_audits` MODIFY COLUMN `idempotency_key` LONGTEXT NOT NULL COMMENT '幂等键，确认/取消/重试均会复用，(run_action_id, idempotency_key) 唯一';
ALTER TABLE `action_execution_audits` MODIFY COLUMN `status` LONGTEXT NOT NULL COMMENT '审计状态：executing（执行中）/executed（已执行）/failed（失败）';
ALTER TABLE `action_execution_audits` MODIFY COLUMN `command_json` JSON NOT NULL COMMENT '调度命令 JSON（展示安全版本，已脱敏不含凭据），结构依动作类型而定，如设备开/关与参数';
ALTER TABLE `action_execution_audits` MODIFY COLUMN `result_json` JSON NULL COMMENT '执行结果 JSON，成功返回设备/连接器执行结果，失败含错误信息';
ALTER TABLE `action_execution_audits` MODIFY COLUMN `created_at` DATETIME(6) NOT NULL COMMENT '创建时间（UTC）';
ALTER TABLE `action_execution_audits` MODIFY COLUMN `updated_at` DATETIME(6) NOT NULL COMMENT '更新时间（UTC）';

-- 表名：personal_favorites 个人偏好收藏实体，支撑个人生活专家的探店翻牌与行程规划；默认仅归属成员本人可见
ALTER TABLE `personal_favorites` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '收藏主键';
ALTER TABLE `personal_favorites` MODIFY COLUMN `home_id` BIGINT NOT NULL COMMENT '所属家庭主键，由 JWT 推导，客户端不可覆盖（外键：tenants.id）';
ALTER TABLE `personal_favorites` MODIFY COLUMN `owner_member_id` BIGINT NOT NULL COMMENT '归属家庭成员主键，默认取当前 JWT 成员（外键：family_members.id）';
ALTER TABLE `personal_favorites` MODIFY COLUMN `category` VARCHAR(32) NOT NULL COMMENT '收藏分类：restaurant（店铺，支撑探店翻牌）/travel（旅行地点，支撑行程规划）/material（短视频素材，预留）';
ALTER TABLE `personal_favorites` MODIFY COLUMN `name` VARCHAR(128) NOT NULL COMMENT '店铺/地点/素材名称，列表展示用';
ALTER TABLE `personal_favorites` MODIFY COLUMN `detail_json` JSON NULL COMMENT '结构化扩展信息 JSON：cuisine/address/lat/lng/tags/note/source';
ALTER TABLE `personal_favorites` MODIFY COLUMN `visibility` VARCHAR(16) NOT NULL DEFAULT 'private' COMMENT '可见性：private（仅本人可读写）/family（家庭内可读，写仍限本人或家庭管理员）';
ALTER TABLE `personal_favorites` MODIFY COLUMN `deleted_at` DATETIME(3) NULL COMMENT '软删除时间戳';
ALTER TABLE `personal_favorites` MODIFY COLUMN `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `personal_favorites` MODIFY COLUMN `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
ALTER TABLE `personal_favorites` MODIFY COLUMN `row_version` BIGINT NOT NULL DEFAULT 0 COMMENT '乐观锁版本号';

-- 表名：attractions 周末出行景点库，支撑出行推荐专家；种子数据本地维护，天气标签手动更新
ALTER TABLE `attractions` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '景点主键';
ALTER TABLE `attractions` MODIFY COLUMN `name` VARCHAR(128) NOT NULL COMMENT '景点名称，全局唯一';
ALTER TABLE `attractions` MODIFY COLUMN `city` VARCHAR(64) NOT NULL COMMENT '所在城市/区域';
ALTER TABLE `attractions` MODIFY COLUMN `category` VARCHAR(32) NOT NULL COMMENT '分类：自然/人文/亲子/美食/商圈';
ALTER TABLE `attractions` MODIFY COLUMN `duration_hours` DECIMAL(3,1) NOT NULL DEFAULT 4.0 COMMENT '建议游玩时长（小时）';
ALTER TABLE `attractions` MODIFY COLUMN `cost_level` TINYINT NOT NULL DEFAULT 2 COMMENT '消费档位 1~5，数字越大消费越高';
ALTER TABLE `attractions` MODIFY COLUMN `weather_tag` VARCHAR(32) NULL COMMENT '手动维护的天气标签，如"晴天开阔""雨天室内"';
ALTER TABLE `attractions` MODIFY COLUMN `tags_json` JSON NULL COMMENT '兴趣标签 JSON 数组，如 ["拍照","亲子","爬山"]';
ALTER TABLE `attractions` MODIFY COLUMN `latitude` DECIMAL(9,6) NULL COMMENT '纬度，预留';
ALTER TABLE `attractions` MODIFY COLUMN `longitude` DECIMAL(9,6) NULL COMMENT '经度，预留';
ALTER TABLE `attractions` MODIFY COLUMN `description` VARCHAR(512) NULL COMMENT '一句话简介';
ALTER TABLE `attractions` MODIFY COLUMN `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否可被推荐：0 否/1 是';
ALTER TABLE `attractions` MODIFY COLUMN `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `attractions` MODIFY COLUMN `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';

-- 表名：credit_ledger 积分流水账，记录运行相关的积分估算/预占/扣费/退款/调整
ALTER TABLE `credit_ledger` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '流水主键';
ALTER TABLE `credit_ledger` MODIFY COLUMN `tenant_id` BIGINT NOT NULL COMMENT '所属租户标识（外键：tenants.id）';
ALTER TABLE `credit_ledger` MODIFY COLUMN `user_id` BIGINT NOT NULL COMMENT '归属用户标识（外键：users.id）';
ALTER TABLE `credit_ledger` MODIFY COLUMN `run_id` BIGINT NULL COMMENT '关联的运行主键，可空（外键：expert_runs.id）';
ALTER TABLE `credit_ledger` MODIFY COLUMN `entry_type` VARCHAR(16) NOT NULL COMMENT '账目类型：estimate（预估）/hold（预占）/charge（扣费）/refund（退款）/adjustment（调整）';
ALTER TABLE `credit_ledger` MODIFY COLUMN `amount` DECIMAL(18,4) NOT NULL COMMENT '金额，正负号表示增减方向';
ALTER TABLE `credit_ledger` MODIFY COLUMN `idempotency_key` VARCHAR(36) NOT NULL COMMENT '幂等键（UUID），(tenant_id, idempotency_key) 唯一防重复入账';
ALTER TABLE `credit_ledger` MODIFY COLUMN `metadata_json` JSON NULL COMMENT '附加元数据 JSON，如关联步骤与计费明细';
ALTER TABLE `credit_ledger` MODIFY COLUMN `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '入账时间（UTC）';

-- 表名：skills 平台级 Skill 目录表（tenant_id 固定 1，与 scenario_templates 同惯例），种子注册 quick-edit（category=media，risk_level=L1）
ALTER TABLE `skills` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'Skill 主键';
ALTER TABLE `skills` MODIFY COLUMN `tenant_id` BIGINT NOT NULL DEFAULT 1 COMMENT '所属租户标识，平台级固定 1（外键：tenants.id）';
ALTER TABLE `skills` MODIFY COLUMN `key` VARCHAR(64) NOT NULL COMMENT 'Skill 业务键，全局唯一，路由 skillCode 即此字段';
ALTER TABLE `skills` MODIFY COLUMN `name` VARCHAR(50) NOT NULL COMMENT 'Skill 对外展示名称';
ALTER TABLE `skills` MODIFY COLUMN `category` VARCHAR(32) NOT NULL COMMENT 'Skill 分类，如 media';
ALTER TABLE `skills` MODIFY COLUMN `description` VARCHAR(255) NULL COMMENT 'Skill 描述，Swagger 展示与运行期说明使用';
ALTER TABLE `skills` MODIFY COLUMN `input_schema_json` JSON NOT NULL COMMENT '输入契约 JSON Schema，运行创建时校验输入参数';
ALTER TABLE `skills` MODIFY COLUMN `output_schema_json` JSON NULL COMMENT '输出契约 JSON Schema，可空';
ALTER TABLE `skills` MODIFY COLUMN `required_permission` VARCHAR(64) NOT NULL COMMENT '调用该 Skill 所需的最小权限，如 media.read';
ALTER TABLE `skills` MODIFY COLUMN `risk_level` VARCHAR(8) NOT NULL DEFAULT 'L1' COMMENT '风险等级：L1（低风险，允许批量确认）/L2（中风险）/L3（高风险）；快速剪辑为 L1';
ALTER TABLE `skills` MODIFY COLUMN `status` VARCHAR(16) NOT NULL DEFAULT 'active' COMMENT 'Skill 状态：active（启用，可发起运行）/inactive（停用，运行发起返回 422）';
ALTER TABLE `skills` MODIFY COLUMN `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '记录创建时间（UTC）';
ALTER TABLE `skills` MODIFY COLUMN `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '记录最近一次修改时间（UTC）';
ALTER TABLE `skills` MODIFY COLUMN `deleted_at` DATETIME(3) NULL COMMENT '软删除时间（UTC），非空表示已删除；已删除 Skill 不再可发起运行';
ALTER TABLE `skills` MODIFY COLUMN `row_version` BIGINT NOT NULL DEFAULT 1 COMMENT '行版本号，乐观锁比较字段';
ALTER TABLE `skills` MODIFY COLUMN `sync_version` BIGINT NOT NULL DEFAULT 1 COMMENT '行版本号，用于同步冲突检测';

-- 表名：conversation_messages 会话内对话消息；user 消息发送时落库，assistant 消息在 Run 终态后落库，均保留 run_id 供追溯
ALTER TABLE `conversation_messages` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '消息主键';
ALTER TABLE `conversation_messages` MODIFY COLUMN `conversation_id` BIGINT NOT NULL COMMENT '所属会话主键（外键：conversations.id）';
ALTER TABLE `conversation_messages` MODIFY COLUMN `role` VARCHAR(16) NOT NULL COMMENT '消息角色：user/assistant';
ALTER TABLE `conversation_messages` MODIFY COLUMN `content` TEXT NOT NULL COMMENT '消息内容；不包含 Prompt 或模型思考链';
ALTER TABLE `conversation_messages` MODIFY COLUMN `run_id` BIGINT NULL COMMENT '关联的 Agent 运行主键，可空表示尚未追溯（如历史导入）；同会话内唯一（外键：expert_runs.id）';
ALTER TABLE `conversation_messages` MODIFY COLUMN `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '消息创建时间（UTC）';

-- 表名：conversations 专家会话，用户围绕某领域创建的对话框，绑定专家与连接器（连接器本阶段仅作元数据落库）
ALTER TABLE `conversations` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '会话主键';
ALTER TABLE `conversations` MODIFY COLUMN `tenant_id` BIGINT NOT NULL COMMENT '所属租户标识，会话为个人资源，与 owner 共同隔离（外键：tenants.id）';
ALTER TABLE `conversations` MODIFY COLUMN `owner_user_id` BIGINT NOT NULL COMMENT '会话所有者用户标识；仅本人可读写，跨用户/跨租户一律 404（外键：users.id）';
ALTER TABLE `conversations` MODIFY COLUMN `title` VARCHAR(64) NOT NULL COMMENT '会话标题';
ALTER TABLE `conversations` MODIFY COLUMN `expert_id` BIGINT NULL COMMENT '绑定的专家主键，可空表示尚未选择专家（外键：experts.id）';
ALTER TABLE `conversations` MODIFY COLUMN `expert_version_id` BIGINT NULL COMMENT '绑定的专家版本主键，与专家同空或同非空（外键：expert_versions.id）';
ALTER TABLE `conversations` MODIFY COLUMN `workspace_connector_id` BIGINT NULL COMMENT '绑定的连接器实例主键（单值），本阶段仅元数据，多连接器后续演进（外键：workspace_connectors.id）';
ALTER TABLE `conversations` MODIFY COLUMN `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `conversations` MODIFY COLUMN `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '最近更新时间（UTC）';
ALTER TABLE `conversations` MODIFY COLUMN `deleted_at` DATETIME(3) NULL COMMENT '软删除时间（UTC），非空表示已删除';
ALTER TABLE `conversations` MODIFY COLUMN `row_version` BIGINT NOT NULL DEFAULT 1 COMMENT '行版本号，乐观锁比较字段，更新时递增';

-- 表名：web_navigation_preferences Web 导航偏好，角色粒度的 route_key 显隐与排序，route_key 须命中后端静态白名单
ALTER TABLE `web_navigation_preferences` MODIFY COLUMN `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '偏好主键';
ALTER TABLE `web_navigation_preferences` MODIFY COLUMN `tenant_id` BIGINT NOT NULL COMMENT '所属家庭（租户）主键（外键：tenants.id）';
ALTER TABLE `web_navigation_preferences` MODIFY COLUMN `role` VARCHAR(16) NOT NULL COMMENT '适用角色：owner/admin/member/viewer，固定枚举';
ALTER TABLE `web_navigation_preferences` MODIFY COLUMN `route_key` VARCHAR(64) NOT NULL COMMENT '已发布的 route_key，由 NexusWebNavigationKeys.All 静态白名单校验';
ALTER TABLE `web_navigation_preferences` MODIFY COLUMN `enabled` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否在菜单中显示：0 隐藏/1 显示';
ALTER TABLE `web_navigation_preferences` MODIFY COLUMN `sort_order` INT NOT NULL DEFAULT 0 COMMENT '显示顺序；值越小越靠前';
ALTER TABLE `web_navigation_preferences` MODIFY COLUMN `updated_by_user_id` BIGINT NOT NULL COMMENT '最近一次写入者用户主键（外键：users.id）';
ALTER TABLE `web_navigation_preferences` MODIFY COLUMN `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `web_navigation_preferences` MODIFY COLUMN `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';

-- 表名：user_expert_preferences 用户-专家交互偏好（复合主键），记录收藏与最近使用
ALTER TABLE `user_expert_preferences` MODIFY COLUMN `tenant_id` BIGINT NOT NULL COMMENT '所属租户标识（复合主键之一，外键：tenants.id）';
ALTER TABLE `user_expert_preferences` MODIFY COLUMN `user_id` BIGINT NOT NULL COMMENT '用户标识（复合主键之一，外键：users.id）';
ALTER TABLE `user_expert_preferences` MODIFY COLUMN `expert_id` BIGINT NOT NULL COMMENT '专家标识（复合主键之一，外键：experts.id）';
ALTER TABLE `user_expert_preferences` MODIFY COLUMN `is_favorite` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否收藏为常用专家：0 否/1 是';
ALTER TABLE `user_expert_preferences` MODIFY COLUMN `last_used_at` DATETIME(3) NULL COMMENT '最近使用时间（UTC）';
ALTER TABLE `user_expert_preferences` MODIFY COLUMN `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间（UTC）';
ALTER TABLE `user_expert_preferences` MODIFY COLUMN `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间（UTC）';
