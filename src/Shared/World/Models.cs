namespace Kakia.TW.Shared.World
{
	/// <summary>
	/// Stat types for equipment bonuses and item stats.
	/// </summary>
	public enum StatType : short
	{
		Stab = 1,           // 突き - Thrust/Stab
		Hack = 2,           // 斬り - Slash/Hack
		Int = 3,            // 魔法攻撃 - Magic Attack
		Def = 4,            // 物理防御 - Physical Defense
		MR = 5,             // 魔法防御 - Magic Defense
		Dex = 6,            // 命中補正 - Hit Correction
		Agi = 7,            // 敏捷補正 - Agility Correction
		Evasion = 8,
	}

	public class WorldPosition
	{
		public ushort MapId { get; set; }
		public int ZoneId { get; set; }
		public ushort X { get; set; }
		public ushort Y { get; set; }

		public WorldPosition() { }
		public WorldPosition(ushort mapId, ushort x, ushort y)
		{
			MapId = mapId;
			X = x;
			Y = y;
		}
		public WorldPosition(ushort mapId, int zoneId, ushort x, ushort y)
		{
			MapId = mapId;
			ZoneId = zoneId;
			X = x;
			Y = y;
		}
	}

	public class ObjectPos
	{
		public WorldPosition Position { get; set; } = new();
		public byte Direction { get; set; }
	}

	public class CharacterSummary
	{
		public byte ServerId { get; set; }
		public string Name { get; set; } = string.Empty;
		public int Level { get; set; } // Unk1 in old code?
		public int Time1 { get; set; } = 0x661da586;
		public int Time2 { get; set; } = 0x66d38b6c;
		public byte CharacterCount { get; set; } // Usually implies slot
	}

	public class GameItem
	{
		public int ItemId { get; set; }
		public short Amount { get; set; }
		public short Durability { get; set; }
		public int Slot { get; set; } = -1; // 0-21 for equipment
		public uint VisualId { get; set; }

		// Equipment specific
		public byte Refine { get; set; }
		public byte DataFlags { get; set; }
		public List<ItemStat> Stats { get; set; } = new();

		// Advanced Magic properties (Legacy ItemMagicProperty)
		public List<ItemMagicProperty> MagicProperties { get; set; } = new();

		public bool IsEquipment => ItemId > 2000;

		/// <summary>
		/// Gets the bonus value for a specific stat type from this item.
		/// </summary>
		public int GetStatBonus(StatType statType)
		{
			return Stats
				.Where(s => s.Type == (short)statType)
				.Sum(s => s.Value);
		}
	}

	public struct ItemStat
	{
		public short Type;
		public int Value;
		public ItemStat(short t, int v) { Type = t; Value = v; }
		public ItemStat(StatType type, int value) { Type = (short)type; Value = value; }
	}

	public class ItemMagicProperty
	{
		public byte PropertyId { get; set; }
		public byte PropertyType { get; set; }
		public uint Value1 { get; set; }
		public uint Value2 { get; set; } // Union-like usage in legacy
		public ushort[] StatValues { get; set; } = Array.Empty<ushort>();
		public uint[] StatParams { get; set; } = Array.Empty<uint>();
		// Simplification: Keeping raw data structures for complex skill props if needed
	}

	public class GuildTeamInfo
	{
		public bool HasGuildInfo { get; set; }
		public string GuildName { get; set; } = string.Empty;
		public uint GuildId { get; set; }
		public string TeamName { get; set; } = string.Empty;
		public ushort MarkId { get; set; } // UnknownShort1
		public ushort MarkColor { get; set; } // UnknownShort2
	}

	public class CharacterAppearance
	{
		// 9 Slots for visuals
		public uint[] VisualIds { get; set; } = new uint[9];
		// In legacy, VisualId2 existed, usually 0
		public uint[] VisualIds2 { get; set; } = new uint[9];
		public byte[] Colors { get; set; } = new byte[9];
		public byte[] Types { get; set; } = new byte[9]; // 1 = Visual, 0 = Color

		// 4 Properties (Rank, etc)
		public uint[] Props { get; set; } = new uint[4];
		public byte[] PropTypes { get; set; } = new byte[4]; // 1 = Int, 0 = Byte
	}

	public class WorldCharacter
	{
		public uint UserId { get; set; }
		public string Name { get; set; } = string.Empty;
		public ObjectPos ObjectPos { get; set; } = new();
		public bool IsGM { get; set; } = true;
		public uint LastLoginTime { get; set; }
		public uint CreationTime { get; set; }

		// Progression
		public int Level { get; set; } = 1;
		public long CurrentExp { get; set; } = 205;
		public long NextExp { get; set; } = 100;
		public long LimitExp { get; set; } = 250;

		// Vitals
		public uint CurrentHP { get; set; } = 300;
		public uint MaxHP { get; set; } = 300;
		public uint CurrentMP { get; set; } = 75;
		public uint MaxMP { get; set; } = 75;
		public ulong CurrentSP { get; set; } = 1300;
		public ulong MaxSP { get; set; } = 1300;

		// ===== Base Stats (Character's own stats) =====
		public short StatStab { get; set; } = 2;
		public short StatHack { get; set; } = 4;
		public short StatInt { get; set; } = 1;
		public short StatDef { get; set; } = 3;
		public short StatMR { get; set; } = 3;
		public short StatDex { get; set; } = 3;
		public short StatAgi { get; set; } = 3;

		// Available stat points to distribute
		public int StatPoints { get; set; } = 9999;

		// ===== Equipment Bonus Stats (Computed from equipped items) =====
		/// <summary>
		/// Calculates the total bonus for a stat type from all equipped items.
		/// </summary>
		private short GetEquipmentBonus(StatType statType)
		{
			if (Equipment == null || Equipment.Count == 0)
				return 0;

			return (short)Equipment.Sum(item => item.GetStatBonus(statType));
		}

		/// <summary>STAB bonus from equipment (突き)</summary>
		public short EquipStabBonus => GetEquipmentBonus(StatType.Stab);

		/// <summary>HACK bonus from equipment (斬り)</summary>
		public short EquipHackBonus => GetEquipmentBonus(StatType.Hack);

		/// <summary>INT bonus from equipment (魔法攻撃)</summary>
		public short EquipIntBonus => GetEquipmentBonus(StatType.Int);

		/// <summary>DEF bonus from equipment (物理防御)</summary>
		public short EquipDefBonus => GetEquipmentBonus(StatType.Def);

		/// <summary>MR bonus from equipment (魔法防御)</summary>
		public short EquipMRBonus => GetEquipmentBonus(StatType.MR);

		/// <summary>DEX bonus from equipment (命中補正)</summary>
		public short EquipDexBonus => GetEquipmentBonus(StatType.Dex);

		/// <summary>AGI bonus from equipment (敏捷補正)</summary>
		public short EquipAgiBonus => GetEquipmentBonus(StatType.Agi);

		/// <summary>Evasion bonus from equipment (回避補正)</summary>
		public short EquipEvasionBonus => GetEquipmentBonus(StatType.Evasion);

		// ===== Total Stats (Base + Equipment) =====
		public int TotalStab => StatStab + EquipStabBonus;
		public int TotalHack => StatHack + EquipHackBonus;
		public int TotalInt => StatInt + EquipIntBonus;
		public int TotalDef => StatDef + EquipDefBonus;
		public int TotalMR => StatMR + EquipMRBonus;
		public int TotalDex => StatDex + EquipDexBonus;
		public int TotalAgi => StatAgi + EquipAgiBonus;

		// Movement (calculated from AGI)
		public byte WalkSpeed => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateWalkSpeed(this)
			: (byte)22;
		public byte RunSpeed => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateRunSpeed(this)
			: (byte)27;

		// Calculated Combat Stats (computed from base stats + equipment)
		public int MinAttack => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMinPhysicalAttack(this)
			: 1;
		public int MaxAttack => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMaxPhysicalAttack(this)
			: 1;
		public int MinAttack2 => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMinPhysicalAttack(this, WeaponType.Katana)
			: 1;
		public int MaxAttack2 => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMaxPhysicalAttack(this, WeaponType.Katana)
			: 1;
		public int MagicAttack => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMagicAttack(this)
			: 1;
		public int PhysicalDefense => StatsCalculator.IsInitialized
			? StatsCalculator.CalculatePhysicalDefense(this)
			: 0;
		public int MagicalDefense => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMagicalDefense(this)
			: 0;
		public byte HitRate => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateHitRate(this)
			: (byte)50;
		public byte PhysicalEvasion => StatsCalculator.IsInitialized
			? StatsCalculator.CalculatePhysicalEvasion(this)
			: (byte)5;
		public byte MagicalEvasion => StatsCalculator.IsInitialized
			? StatsCalculator.CalculateMagicalEvasion(this)
			: (byte)5;

		// Attributes (Elemental resistances/bonuses)
		public short NoneAttribute { get; set; } = 5;
		public short FireAttribute { get; set; } = 5;
		public short WaterAttribute { get; set; } = 5;
		public short WindAttribute { get; set; } = 5;
		public short EarthAttribute { get; set; } = 10;
		public short LightningAttribute { get; set; } = 5;
		public short HolyAttribute { get; set; } = 5;
		public short DarkAttribute { get; set; } = 10;

		// Visuals
		public uint ModelId { get; set; }
		public ushort Outfit { get; set; }
		public uint TitleId { get; set; }

		public GuildTeamInfo GuildInfo { get; set; } = new();
		public List<GameItem> Equipment { get; set; } = new();
		public CharacterAppearance Appearance { get; set; } = new();
	}

	public class WarpPortal
	{
		public uint Id { get; set; }
		public WorldPosition MinPoint { get; set; } = new();
		public WorldPosition MaxPoint { get; set; } = new();
		public ushort DestMapId { get; set; }
		public ushort DestPortalId { get; set; }
	}
}