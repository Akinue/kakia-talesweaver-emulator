using Kakia.TW.Shared.Configuration.Files;

namespace Kakia.TW.Shared.World
{
	/// <summary>
	/// Weapon types that affect attack formula calculations.
	/// </summary>
	public enum WeaponType : byte
	{
		None = 0,
		Sword = 1,      // STAB-based (Swords)
		Katana = 2,     // HACK-based (Katanas)
		Spear = 3,      // STAB-based (Spears)
		Whip = 4,       // HACK-based (Whips)
		Staff = 5,      // INT-based (Staves)
		Wand = 6,       // INT-based (Wands)
		Blunt = 7,      // DEF/HACK hybrid (Blunt weapons)
	}

	/// <summary>
	/// Calculates combat stats based on TalesWeaver formulas.
	/// All coefficients are configurable via stats.conf.
	/// </summary>
	public static class StatsCalculator
	{
		private static StatsConf? _conf;
		private static readonly Random _random = new();

		/// <summary>
		/// Initializes the calculator with configuration values.
		/// Must be called during server startup.
		/// </summary>
		public static void Initialize(StatsConf conf)
		{
			_conf = conf;
		}

		/// <summary>
		/// Gets the configuration, throwing if not initialized.
		/// </summary>
		private static StatsConf Conf => _conf ?? throw new InvalidOperationException("StatsCalculator not initialized. Call Initialize() first.");

		// =====================================================================
		// WEAPON TYPE DETECTION
		// =====================================================================

		/// <summary>
		/// Determines weapon type from an item ID based on configured ranges.
		/// </summary>
		public static WeaponType GetWeaponTypeFromItemId(int itemId)
		{
			if (_conf == null)
				return WeaponType.Sword; // Default fallback

			if (IsInRange(itemId, Conf.WeaponSwordRange))
				return WeaponType.Sword;
			if (IsInRange(itemId, Conf.WeaponKatanaRange))
				return WeaponType.Katana;
			if (IsInRange(itemId, Conf.WeaponSpearRange))
				return WeaponType.Spear;
			if (IsInRange(itemId, Conf.WeaponWhipRange))
				return WeaponType.Whip;
			if (IsInRange(itemId, Conf.WeaponStaffRange))
				return WeaponType.Staff;
			if (IsInRange(itemId, Conf.WeaponWandRange))
				return WeaponType.Wand;
			if (IsInRange(itemId, Conf.WeaponBluntRange))
				return WeaponType.Blunt;

			return WeaponType.Sword; // Default to sword
		}

		/// <summary>
		/// Gets the weapon type from the character's equipped weapon.
		/// </summary>
		public static WeaponType GetEquippedWeaponType(WorldCharacter character)
		{
			// Weapon slot is typically slot 0 or find first weapon in equipment
			var weapon = character.Equipment?.FirstOrDefault(e => e.Slot == 0 && e.ItemId > 2000);
			if (weapon != null)
				return GetWeaponTypeFromItemId(weapon.ItemId);

			return WeaponType.Sword; // Default
		}

		private static bool IsInRange(int value, (int Start, int End) range)
		{
			return value >= range.Start && value <= range.End;
		}

		// =====================================================================
		// ATTACK CALCULATIONS
		// =====================================================================

		/// <summary>
		/// Calculates minimum physical attack based on weapon type.
		/// STAB weapons: (STAB × 2.10) + (HACK × 1.08) + (EquipBonus × 2.45)
		/// HACK weapons: (STAB × 1.08) + (HACK × 2.10) + (EquipBonus × 2.45)
		/// </summary>
		public static int CalculateMinPhysicalAttack(WorldCharacter c)
		{
			var weaponType = GetEquippedWeaponType(c);
			return CalculateMinPhysicalAttack(c, weaponType);
		}

		public static int CalculateMinPhysicalAttack(WorldCharacter c, WeaponType weaponType)
		{
			float attack;

			switch (weaponType)
			{
				case WeaponType.Sword:
				case WeaponType.Spear:
					// STAB-based
					attack = (c.TotalStab * Conf.AttackStabPrimary) +
							 (c.TotalHack * Conf.AttackStabSecondary) +
							 (c.EquipStabBonus * Conf.AttackEquipMultiplier);
					break;

				case WeaponType.Katana:
				case WeaponType.Whip:
					// HACK-based
					attack = (c.TotalStab * Conf.AttackHackSecondary) +
							 (c.TotalHack * Conf.AttackHackPrimary) +
							 (c.EquipHackBonus * Conf.AttackEquipMultiplier);
					break;

				case WeaponType.Staff:
				case WeaponType.Wand:
					// Magic weapons use magic attack
					return CalculateMagicAttack(c);

				case WeaponType.Blunt:
					// Hybrid DEF/HACK
					attack = (c.TotalDef * Conf.AttackStabSecondary) +
							 (c.TotalHack * Conf.AttackHackPrimary) +
							 (c.EquipDefBonus * Conf.AttackEquipMultiplier);
					break;

				default:
					// Default to STAB-based
					attack = (c.TotalStab * Conf.AttackStabPrimary) +
							 (c.TotalHack * Conf.AttackStabSecondary) +
							 (c.EquipStabBonus * Conf.AttackEquipMultiplier);
					break;
			}

			return Math.Max(1, (int)attack);
		}

		/// <summary>
		/// Calculates maximum physical attack (MinAttack × variance).
		/// </summary>
		public static int CalculateMaxPhysicalAttack(WorldCharacter c)
		{
			var minAttack = CalculateMinPhysicalAttack(c);
			return (int)(minAttack * Conf.AttackVarianceMax);
		}

		public static int CalculateMaxPhysicalAttack(WorldCharacter c, WeaponType weaponType)
		{
			var minAttack = CalculateMinPhysicalAttack(c, weaponType);
			return (int)(minAttack * Conf.AttackVarianceMax);
		}

		/// <summary>
		/// Calculates actual attack damage with random variance.
		/// </summary>
		public static int CalculateRandomAttack(WorldCharacter c)
		{
			var minAttack = CalculateMinPhysicalAttack(c);
			var variance = Conf.AttackVarianceMin +
						   (_random.NextDouble() * (Conf.AttackVarianceMax - Conf.AttackVarianceMin));
			return Math.Max(1, (int)(minAttack * variance));
		}

		/// <summary>
		/// Calculates magic attack power.
		/// MagicAttack = (INT × 2.4) + (MR × 0.6) + (EquipINT × 6.65) + (EquipMR × 0.7)
		/// </summary>
		public static int CalculateMagicAttack(WorldCharacter c)
		{
			var magicAttack = (c.TotalInt * Conf.MagicIntMultiplier) +
							  (c.TotalMR * Conf.MagicMrMultiplier) +
							  (c.EquipIntBonus * Conf.MagicEquipIntMultiplier) +
							  (c.EquipMRBonus * Conf.MagicEquipMrMultiplier);

			return Math.Max(1, (int)magicAttack);
		}

		// =====================================================================
		// DEFENSE CALCULATIONS
		// =====================================================================

		/// <summary>
		/// Calculates physical defense.
		/// PhysicalDefense = TotalDEF × 3.0
		/// </summary>
		public static int CalculatePhysicalDefense(WorldCharacter c)
		{
			return Math.Max(0, (int)(c.TotalDef * Conf.DefenseMultiplier));
		}

		/// <summary>
		/// Calculates magical defense.
		/// MagicalDefense = TotalMR × 3.0
		/// </summary>
		public static int CalculateMagicalDefense(WorldCharacter c)
		{
			return Math.Max(0, (int)(c.TotalMR * Conf.DefenseMultiplier));
		}

		// =====================================================================
		// HIT RATE / EVASION
		// =====================================================================

		/// <summary>
		/// Calculates hit rate (accuracy).
		/// HitRate = 50 + (TotalDEX × 0.5) + EquipDexBonus
		/// </summary>
		public static byte CalculateHitRate(WorldCharacter c)
		{
			var hitRate = Conf.HitRateBase +
						  (c.TotalDex * Conf.HitRateDexMultiplier) +
						  c.EquipDexBonus;

			return (byte)Math.Clamp((int)hitRate, 1, 255);
		}

		/// <summary>
		/// Calculates physical evasion.
		/// PhysicalEvasion = 5 + (TotalAGI × 0.3) + EquipAgiBonus
		/// </summary>
		public static byte CalculatePhysicalEvasion(WorldCharacter c)
		{
			var evasion = Conf.EvasionBase +
						  (c.TotalAgi * Conf.EvasionAgiMultiplier) +
						  c.EquipAgiBonus +
						  c.EquipEvasionBonus;

			return (byte)Math.Clamp((int)evasion, 0, 255);
		}

		/// <summary>
		/// Calculates magical evasion.
		/// MagicalEvasion = 5 + (TotalMR × 0.2)
		/// </summary>
		public static byte CalculateMagicalEvasion(WorldCharacter c)
		{
			var evasion = Conf.MagicEvasionBase +
						  (c.TotalMR * Conf.MagicEvasionMrMultiplier);

			return (byte)Math.Clamp((int)evasion, 0, 255);
		}

		// =====================================================================
		// SPEED CALCULATIONS
		// =====================================================================

		/// <summary>
		/// Calculates walk speed.
		/// WalkSpeed = 20 + (AGI / 10)
		/// </summary>
		public static byte CalculateWalkSpeed(WorldCharacter c)
		{
			var divisor = Conf.WalkSpeedAgiDivisor > 0 ? Conf.WalkSpeedAgiDivisor : 10;
			var speed = Conf.WalkSpeedBase + (c.TotalAgi / divisor);
			return (byte)Math.Clamp(speed, 1, 255);
		}

		/// <summary>
		/// Calculates run speed.
		/// RunSpeed = 25 + (AGI / 8)
		/// </summary>
		public static byte CalculateRunSpeed(WorldCharacter c)
		{
			var divisor = Conf.RunSpeedAgiDivisor > 0 ? Conf.RunSpeedAgiDivisor : 8;
			var speed = Conf.RunSpeedBase + (c.TotalAgi / divisor);
			return (byte)Math.Clamp(speed, 1, 255);
		}

		// =====================================================================
		// UTILITY METHODS
		// =====================================================================

		/// <summary>
		/// Recalculates all combat stats for a character.
		/// Call this after equipment changes or stat point allocation.
		/// </summary>
		public static void RecalculateAll(WorldCharacter c)
		{
			// Combat stats are now calculated on-the-fly via properties
			// This method can be used if we need to cache values in the future
			// or trigger stat update packets

			// Currently a no-op since properties calculate dynamically
		}

		/// <summary>
		/// Calculates damage dealt from attacker to defender.
		/// Damage = (Attack - Defense) × SkillMultiplier
		/// </summary>
		public static int CalculateDamage(int attack, int defense, float skillMultiplier = 1.0f)
		{
			var baseDamage = attack - defense;
			var finalDamage = (int)(baseDamage * skillMultiplier);
			return Math.Max(1, finalDamage); // Minimum 1 damage
		}

		/// <summary>
		/// Checks if an attack hits based on hit rate and evasion.
		/// </summary>
		public static bool CheckHit(byte hitRate, byte evasion)
		{
			var hitChance = Math.Clamp(hitRate - evasion + 50, 5, 95); // 5-95% range
			return _random.Next(100) < hitChance;
		}

		/// <summary>
		/// Returns whether the calculator has been initialized.
		/// </summary>
		public static bool IsInitialized => _conf != null;
	}
}
