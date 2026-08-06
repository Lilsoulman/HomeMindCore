-- Apply after 021_video_script_expert.mysql.sql.
-- 周末出行推荐:本地景点库(宁波及周边自然景点为主,匹配"爱自然不爱城市")+ 出行推荐专家。
USE `nexus_mind`;

CREATE TABLE IF NOT EXISTS `attractions` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(128) NOT NULL,
  `city` VARCHAR(64) NOT NULL,
  `category` VARCHAR(32) NOT NULL,
  `duration_hours` DECIMAL(3,1) NOT NULL DEFAULT 4.0,
  `cost_level` TINYINT NOT NULL DEFAULT 2,
  `weather_tag` VARCHAR(32) NULL,
  `tags_json` JSON NULL,
  `latitude` DECIMAL(9,6) NULL,
  `longitude` DECIMAL(9,6) NULL,
  `description` VARCHAR(512) NULL,
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_attractions_name` (`name`),
  KEY `idx_attractions_active` (`is_active`,`category`)
) ENGINE=InnoDB;

INSERT INTO `attractions` (`name`,`city`,`category`,`duration_hours`,`cost_level`,`weather_tag`,`tags_json`,`description`)
VALUES
  ('四明山·白鹿观景台','余姚','自然',5.0,2,'雨后云海概率高','["自然","拍照","云海","自驾"]','四明山高处观景台，雨后云海与日出绝佳，适合摄影与放空。'),
  ('柿林村·丹山赤水','余姚','自然',4.5,2,'四季皆宜','["自然","古村","拍照","柿子季"]','千年古村与赤水丹霞，秋季柿子满枝，山间清溪徒步。'),
  ('四明湖','余姚','自然',3.0,1,'晴天开阔','["自然","湖景","露营","亲子"]','湖面开阔水杉成排，环湖骑行与野餐的好去处。'),
  ('雪窦山·千丈岩','奉化','自然',6.0,3,'山间清凉','["自然","瀑布","登山","弥勒"]','弥勒道场，千丈岩瀑布与妙高台，山林清幽。'),
  ('浙东大峡谷','宁海','自然',6.0,3,'雨后水量大','["自然","峡谷","徒步","玩水"]','峡谷幽深溪流清澈，夏季亲水避暑首选。'),
  ('宁海森林温泉','宁海','自然',5.0,4,'雨天室内','["自然","温泉","放松","秋冬"]','森林环抱的天然温泉，泡汤解乏，适合秋冬周末。'),
  ('松兰山滨海旅游度假区','象山','自然',5.0,3,'晴天开阔','["自然","海滨","拍照","亲子"]','绵长沙滩与礁石海岸，赶海看日落都很治愈。'),
  ('杭州湾湿地公园','慈溪','自然',4.0,2,'候鸟季观鸟','["自然","湿地","观鸟","亲子"]','东亚候鸟迁徙驿站，芦苇荡与观鸟塔，科普又出片。'),
  ('天童森林公园','鄞州','自然',4.0,2,'四季皆宜','["自然","森林","徒步","古寺"]','古木参天的森林公园，紧邻天童禅寺，负氧离子充足。'),
  ('九龙湖旅游度假区','镇海','自然',4.0,2,'晴天开阔','["自然","湖景","徒步","亲子"]','群山环抱的湖湾，环湖步道与游船，安静不拥挤。'),
  ('达蓬山','慈溪','自然',4.5,2,'山间清凉','["自然","湖景","徒步","徐福"]','徐福东渡起航地，山顶观湖看海，可连游仙佛谷。'),
  ('五磊山风景区','慈溪','自然',4.0,2,'山间清凉','["自然","古寺","登山","静谧"]','江南名刹五磊讲寺，山高林密，香火清静。'),
  ('福泉山茶场','东钱湖','自然',3.5,2,'晴天开阔','["自然","茶山","拍照","徒步"]','万亩茶山层叠，山顶风车观景，清晨薄雾最出片。'),
  ('东钱湖环湖绿道','鄞州','自然',4.0,1,'四季皆宜','["自然","湖景","骑行","亲子"]','环湖骑行与徒步绿道，沿途村落与湿地，轻松无压力。'),
  ('九峰山风景区','北仑','自然',5.0,2,'山间清凉','["自然","瀑布","登山","亲子"]','九峰连环瀑布群，溪涧与步道交织，亲子徒步友好。'),
  ('招宝山','镇海','人文',3.0,2,'晴天开阔','["人文","海防","古炮台","拍照"]','镇海口海防要塞，古炮台与招宝阁，带历史故事。'),
  ('慈城古县城','慈溪','人文',4.0,2,'四季皆宜','["人文","古街","老宅","手作"]','千年古县城，孔庙与年糕手作，慢慢逛很有味道。'),
  ('石浦渔港古城','象山','人文',4.0,3,'晴天开阔','["人文","渔港","海鲜","老街"]','六百载渔港古城，渔船码头与海鲜大排档一条街。'),
  ('隐潭','奉化','自然',3.0,1,'雨后水量大','["自然","瀑布","清凉","徒步"]','雪窦山支线的幽深瀑布群，人少清静，夏季凉快。'),
  ('阳明温泉山庄','余姚','自然',5.0,4,'雨天室内','["自然","温泉","放松","秋冬"]','阳明文化主题温泉，泡汤后可在四明山脚散步。')
ON DUPLICATE KEY UPDATE `city`=VALUES(`city`),`category`=VALUES(`category`),`duration_hours`=VALUES(`duration_hours`),
  `cost_level`=VALUES(`cost_level`),`weather_tag`=VALUES(`weather_tag`),`tags_json`=VALUES(`tags_json`),`description`=VALUES(`description`);

INSERT INTO `experts` (`tenant_id`,`code`,`name`,`category`,`expert_type`,`status`,`description`,`privacy_scope_json`)
VALUES (1,'travel-recommender','周末出行推荐专家','life','builtin','active','根据家庭偏好与本地景点库推荐周末自然出行目的地,支持三选一反馈闭环。','[]')
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`description`=VALUES(`description`),`status`=VALUES(`status`);

INSERT INTO `expert_versions` (`tenant_id`,`expert_id`,`version`,`status`,`persona`,`methodology`,`prompt_template`,`tool_policy_json`,`output_schema_json`,`estimated_credits`)
SELECT 1, e.id, 1, 'published',
  '你是周末出行推荐专家,了解一位爱自然、不爱城市的伙伴。她从 App 的出行页面查看推荐列表,你负责为每次推送写一段温暖有画面的推荐语。',
  '推荐语要具体:说清地点、亮点、适合的天气与出发时段;不堆砌形容词;给出 1 条实用小贴士。',
  '用户的输入是一个 JSON 对象,可能为空(定时推送)或包含候选景点信息。请输出一条周末出行推荐文案,严格按以下 JSON 结构返回,不要输出任何额外文字、不要用 markdown 代码块包裹:
{"title":"推荐标题","reason":"推荐理由(100 字以内,有画面感)","tip":"实用小贴士(一句话)"}

要求:title 简洁有吸引力;reason 突出自然体验与当下时节;tip 具体可执行(如出发时间、穿搭、顺路安排)。',
  '[]',
  '{"type":"object"}',
  0.6000
FROM `experts` e
WHERE e.tenant_id=1 AND e.code='travel-recommender'
  AND NOT EXISTS (SELECT 1 FROM `expert_versions` v WHERE v.expert_id=e.id AND v.version=1);
