-- Apply after 002_expert_workbench_and_tenancy.mysql.sql.
USE `nexus_mind`;

CREATE TABLE `auth_access_token_revocations` (
  `token_id` CHAR(32) NOT NULL,
  `user_id` BIGINT NOT NULL,
  `tenant_id` BIGINT NOT NULL,
  `expires_at` DATETIME(3) NOT NULL,
  `revoked_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `revoke_reason` VARCHAR(32) NOT NULL DEFAULT 'logout',
  PRIMARY KEY (`token_id`),
  KEY `idx_access_revocation_expiry` (`expires_at`),
  KEY `idx_access_revocation_user` (`user_id`,`revoked_at`),
  CONSTRAINT `fk_access_revocation_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_access_revocation_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB;
