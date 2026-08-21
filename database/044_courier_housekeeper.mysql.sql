-- B44 快递管家：个人运单状态投影和异常建议，不保存完整运单号或第三方凭据。
CREATE TABLE `courier_shipments` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '运单主键',
  `home_id` BIGINT NOT NULL COMMENT '所属家庭主键',
  `owner_user_id` BIGINT NOT NULL COMMENT '个人运单所有者用户主键',
  `tracking_number_hash` CHAR(64) NOT NULL COMMENT '完整运单号不可逆哈希',
  `tracking_number_masked` VARCHAR(32) NOT NULL COMMENT '运单脱敏尾号',
  `carrier` VARCHAR(64) NULL COMMENT '承运商展示名称',
  `label` VARCHAR(128) NULL COMMENT '用户自定义包裹标签',
  `is_fresh_food` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否为生鲜包裹',
  `expected_delivery_at` DATETIME(3) NULL COMMENT '预计送达时间',
  `latest_status` VARCHAR(32) NOT NULL DEFAULT 'unknown' COMMENT '最近状态：unknown/in_transit/out_for_delivery/delivered/exception',
  `latest_description` VARCHAR(512) NULL COMMENT '最近状态描述',
  `latest_location` VARCHAR(128) NULL COMMENT '最近状态地点',
  `latest_event_at` DATETIME(3) NULL COMMENT '最近物流事件时间',
  `last_checked_at` DATETIME(3) NULL COMMENT '最近查询时间',
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '创建时间，UTC',
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) COMMENT '更新时间，UTC',
  PRIMARY KEY (`id`), UNIQUE KEY `uk_courier_home_owner_tracking` (`home_id`,`owner_user_id`,`tracking_number_hash`),
  KEY `idx_courier_home_owner` (`home_id`,`owner_user_id`),
  CONSTRAINT `fk_courier_shipment_home` FOREIGN KEY (`home_id`) REFERENCES `tenants` (`id`),
  CONSTRAINT `fk_courier_shipment_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`),
  CONSTRAINT `ck_courier_status` CHECK (`latest_status` IN ('unknown','in_transit','out_for_delivery','delivered','exception'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='个人快递运单状态投影';

-- B44 快递状态事件：用于状态流和异常判定。
CREATE TABLE `courier_shipment_events` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '事件主键',
  `shipment_id` BIGINT NOT NULL COMMENT '所属运单主键',
  `status` VARCHAR(32) NOT NULL COMMENT '事件状态编码',
  `description` VARCHAR(512) NOT NULL COMMENT '物流状态描述',
  `location` VARCHAR(128) NULL COMMENT '物流地点',
  `occurred_at` DATETIME(3) NOT NULL COMMENT '事件发生时间，UTC',
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) COMMENT '写入时间，UTC',
  PRIMARY KEY (`id`), UNIQUE KEY `uk_courier_event` (`shipment_id`,`status`,`occurred_at`),
  CONSTRAINT `fk_courier_event_shipment` FOREIGN KEY (`shipment_id`) REFERENCES `courier_shipments` (`id`) ON DELETE CASCADE,
  CONSTRAINT `ck_courier_event_status` CHECK (`status` IN ('unknown','in_transit','out_for_delivery','delivered','exception'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='个人快递状态事件流';

-- B44 注册快递100 官方 MCP Connector Provider。
INSERT INTO `connector_providers` (`code`,`name`,`provider`,`connector_type`,`status`,`description`,`created_at`,`updated_at`)
VALUES ('kuaidi100','快递100','kuaidi100_mcp','courier','active','快递状态查询与异常发现（个人连接）',CURRENT_TIMESTAMP(3),CURRENT_TIMESTAMP(3))
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`provider`=VALUES(`provider`),`connector_type`=VALUES(`connector_type`),`status`=VALUES(`status`),`description`=VALUES(`description`),`updated_at`=VALUES(`updated_at`);
