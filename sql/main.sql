CREATE DATABASE IF NOT EXISTS `kakia_tw` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
USE `kakia_tw`;

-- --------------------------------------------------------
-- Accounts Table
-- --------------------------------------------------------
CREATE TABLE `accounts` (
  `accountId` int(11) NOT NULL AUTO_INCREMENT,
  `username` varchar(64) NOT NULL,
  `password` varchar(128) NOT NULL,
  `authority` tinyint(4) NOT NULL DEFAULT 0 COMMENT '0: User, 1: GM, 99: Admin',
  `sessionId` int(11) NOT NULL DEFAULT 0 COMMENT 'Used for handover between Login/Lobby/World',
  `selected_character` varchar(24) DEFAULT NULL COMMENT 'Currently selected character name for world entry',
  `isBanned` tinyint(1) NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `last_login` timestamp NULL DEFAULT NULL,
  PRIMARY KEY (`accountId`),
  UNIQUE KEY `username` (`username`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------
-- Characters Table
-- --------------------------------------------------------
CREATE TABLE `characters` (
  `characterId` int(11) NOT NULL AUTO_INCREMENT,
  `accountId` int(11) NOT NULL,
  `slot` tinyint(4) NOT NULL COMMENT 'Slot index 0-2',
  `name` varchar(24) NOT NULL,
  
  -- Visuals
  `char_type` int(11) NOT NULL COMMENT 'ModelId: 2201536 (Lucian), 2201537 (Boris), etc.',
  `title_id` int(11) NOT NULL DEFAULT 0,
  `appearance_data` blob COMMENT 'Binary serialized CharacterAppearance object',

  -- Position (Default: Narvik starter area)
  `map_id` smallint(6) NOT NULL DEFAULT 6,
  `zone_id` int(11) NOT NULL DEFAULT 38656,
  `x` smallint(6) NOT NULL DEFAULT 305,
  `y` smallint(6) NOT NULL DEFAULT 220,
  
  -- Progression
  `level` int(11) NOT NULL DEFAULT 1,
  `exp` bigint(20) NOT NULL DEFAULT 0,
  
  -- Vitals
  `hp` int(11) NOT NULL DEFAULT 100,
  `hp_max` int(11) NOT NULL DEFAULT 100,
  `mp` int(11) NOT NULL DEFAULT 50,
  `mp_max` int(11) NOT NULL DEFAULT 50,
  `sp` int(11) NOT NULL DEFAULT 5000,
  `sp_max` int(11) NOT NULL DEFAULT 5000,
  
  -- Primary Stats (TalesWeaver specific)
  `stat_stab` int(11) NOT NULL DEFAULT 1,
  `stat_hack` int(11) NOT NULL DEFAULT 1,
  `stat_int` int(11) NOT NULL DEFAULT 1,
  `stat_def` int(11) NOT NULL DEFAULT 1,
  `stat_mr` int(11) NOT NULL DEFAULT 1,
  `stat_dex` int(11) NOT NULL DEFAULT 1,
  `stat_agi` int(11) NOT NULL DEFAULT 1,
  
  -- Social
  `guild_id` int(11) NOT NULL DEFAULT 0,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  
  PRIMARY KEY (`characterId`),
  UNIQUE KEY `name` (`name`),
  KEY `accountId` (`accountId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------
-- Inventory / Items Table
-- --------------------------------------------------------
CREATE TABLE `items` (
  `dbId` bigint(20) NOT NULL AUTO_INCREMENT,
  `characterId` int(11) NOT NULL,
  
  `itemId` int(11) NOT NULL COMMENT 'Prototype ID',
  `amount` smallint(6) NOT NULL DEFAULT 1,
  `durability` smallint(6) NOT NULL DEFAULT 0,
  
  -- Equipment Specific
  `slot` int(11) NOT NULL DEFAULT -1 COMMENT '-1: Inventory, 0-21: Equipped',
  `refine` tinyint(4) NOT NULL DEFAULT 0,
  `visualId` int(11) NOT NULL DEFAULT 0,
  
  -- Dynamic Data
  `stats_data` blob COMMENT 'Binary serialized list of ItemStat',
  `magic_props_data` blob COMMENT 'Binary serialized list of ItemMagicProperty',
  
  PRIMARY KEY (`dbId`),
  KEY `characterId` (`characterId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------
-- Skills Table
-- --------------------------------------------------------
CREATE TABLE `skills` (
  `characterId` int(11) NOT NULL,
  `skillId` int(11) NOT NULL,
  `level` tinyint(4) NOT NULL DEFAULT 1,
  `exp` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`characterId`,`skillId`),
  CONSTRAINT `skills_ibfk_1` FOREIGN KEY (`characterId`) REFERENCES `characters` (`characterId`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------
-- Variable Tables (Quest Flags, Server Settings)
-- --------------------------------------------------------
CREATE TABLE `vars_account` (
  `varId` bigint(20) NOT NULL AUTO_INCREMENT,
  `ownerId` int(11) NOT NULL,
  `name` varchar(128) NOT NULL,
  `type` char(2) NOT NULL,
  `value` mediumtext NOT NULL,
  PRIMARY KEY (`varId`),
  KEY `ownerId` (`ownerId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE `vars_character` (
  `varId` bigint(20) NOT NULL AUTO_INCREMENT,
  `ownerId` int(11) NOT NULL,
  `name` varchar(128) NOT NULL,
  `type` char(2) NOT NULL,
  `value` mediumtext NOT NULL,
  PRIMARY KEY (`varId`),
  KEY `ownerId` (`ownerId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- --------------------------------------------------------
-- Migration/Update Tracking
-- --------------------------------------------------------
CREATE TABLE IF NOT EXISTS `updates` (
  `path` varchar(255) NOT NULL,
  `applied_at` timestamp NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`path`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- --------------------------------------------------------
-- Constraints (Foreign Keys)
-- --------------------------------------------------------
ALTER TABLE `characters`
  ADD CONSTRAINT `characters_ibfk_1` FOREIGN KEY (`accountId`) REFERENCES `accounts` (`accountId`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `items`
  ADD CONSTRAINT `items_ibfk_1` FOREIGN KEY (`characterId`) REFERENCES `characters` (`characterId`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `vars_account`
  ADD CONSTRAINT `vars_account_ibfk_1` FOREIGN KEY (`ownerId`) REFERENCES `accounts` (`accountId`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `vars_character`
  ADD CONSTRAINT `vars_character_ibfk_1` FOREIGN KEY (`ownerId`) REFERENCES `characters` (`characterId`) ON DELETE CASCADE ON UPDATE CASCADE;

-- --------------------------------------------------------
-- Initial Data
-- --------------------------------------------------------
INSERT INTO `updates` (`path`) VALUES ('main.sql');