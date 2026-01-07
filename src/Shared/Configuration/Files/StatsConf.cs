using System.Globalization;
using Yggdrasil.Configuration;

namespace Kakia.TW.Shared.Configuration.Files
{
	/// <summary>
	/// Represents stats.conf - combat formula coefficients.
	/// </summary>
	public class StatsConf : ConfFile
	{
		// Attack Coefficients
		public float AttackStabPrimary { get; set; }
		public float AttackStabSecondary { get; set; }
		public float AttackHackPrimary { get; set; }
		public float AttackHackSecondary { get; set; }
		public float AttackEquipMultiplier { get; set; }
		public float AttackVarianceMin { get; set; }
		public float AttackVarianceMax { get; set; }

		// Magic Attack Coefficients
		public float MagicIntMultiplier { get; set; }
		public float MagicMrMultiplier { get; set; }
		public float MagicEquipIntMultiplier { get; set; }
		public float MagicEquipMrMultiplier { get; set; }

		// Defense Coefficients
		public float DefenseMultiplier { get; set; }

		// Hit Rate / Evasion
		public int HitRateBase { get; set; }
		public float HitRateDexMultiplier { get; set; }
		public int EvasionBase { get; set; }
		public float EvasionAgiMultiplier { get; set; }
		public int MagicEvasionBase { get; set; }
		public float MagicEvasionMrMultiplier { get; set; }

		// Speed
		public int WalkSpeedBase { get; set; }
		public int WalkSpeedAgiDivisor { get; set; }
		public int RunSpeedBase { get; set; }
		public int RunSpeedAgiDivisor { get; set; }

		// Weapon Item ID Ranges
		public (int Start, int End) WeaponSwordRange { get; set; }
		public (int Start, int End) WeaponKatanaRange { get; set; }
		public (int Start, int End) WeaponSpearRange { get; set; }
		public (int Start, int End) WeaponWhipRange { get; set; }
		public (int Start, int End) WeaponStaffRange { get; set; }
		public (int Start, int End) WeaponWandRange { get; set; }
		public (int Start, int End) WeaponBluntRange { get; set; }

		/// <summary>
		/// Loads the conf file and its options from the given path.
		/// </summary>
		public void Load(string filePath)
		{
			this.Include(filePath);

			// Attack Coefficients
			this.AttackStabPrimary = GetFloat("attack_stab_primary", 2.10f);
			this.AttackStabSecondary = GetFloat("attack_stab_secondary", 1.08f);
			this.AttackHackPrimary = GetFloat("attack_hack_primary", 2.10f);
			this.AttackHackSecondary = GetFloat("attack_hack_secondary", 1.08f);
			this.AttackEquipMultiplier = GetFloat("attack_equip_multiplier", 2.45f);
			this.AttackVarianceMin = GetFloat("attack_variance_min", 1.00f);
			this.AttackVarianceMax = GetFloat("attack_variance_max", 1.20f);

			// Magic Attack Coefficients
			this.MagicIntMultiplier = GetFloat("magic_int_multiplier", 2.4f);
			this.MagicMrMultiplier = GetFloat("magic_mr_multiplier", 0.6f);
			this.MagicEquipIntMultiplier = GetFloat("magic_equip_int_multiplier", 6.65f);
			this.MagicEquipMrMultiplier = GetFloat("magic_equip_mr_multiplier", 0.7f);

			// Defense Coefficients
			this.DefenseMultiplier = GetFloat("defense_multiplier", 3.0f);

			// Hit Rate / Evasion
			this.HitRateBase = this.GetInt("hit_rate_base", 50);
			this.HitRateDexMultiplier = GetFloat("hit_rate_dex_multiplier", 0.5f);
			this.EvasionBase = this.GetInt("evasion_base", 5);
			this.EvasionAgiMultiplier = GetFloat("evasion_agi_multiplier", 0.3f);
			this.MagicEvasionBase = this.GetInt("magic_evasion_base", 5);
			this.MagicEvasionMrMultiplier = GetFloat("magic_evasion_mr_multiplier", 0.2f);

			// Speed
			this.WalkSpeedBase = this.GetInt("walk_speed_base", 20);
			this.WalkSpeedAgiDivisor = this.GetInt("walk_speed_agi_divisor", 10);
			this.RunSpeedBase = this.GetInt("run_speed_base", 25);
			this.RunSpeedAgiDivisor = this.GetInt("run_speed_agi_divisor", 8);

			// Weapon Item ID Ranges
			this.WeaponSwordRange = ParseRange("weapon_sword_range", (3000, 3999));
			this.WeaponKatanaRange = ParseRange("weapon_katana_range", (4000, 4999));
			this.WeaponSpearRange = ParseRange("weapon_spear_range", (5000, 5999));
			this.WeaponWhipRange = ParseRange("weapon_whip_range", (6000, 6999));
			this.WeaponStaffRange = ParseRange("weapon_staff_range", (7000, 7999));
			this.WeaponWandRange = ParseRange("weapon_wand_range", (8000, 8999));
			this.WeaponBluntRange = ParseRange("weapon_blunt_range", (9000, 9999));
		}

		/// <summary>
		/// Gets a float value from the configuration.
		/// </summary>
		private float GetFloat(string option, float defaultValue)
		{
			var str = this.GetString(option, null);
			if (string.IsNullOrEmpty(str))
				return defaultValue;

			if (float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
				return result;

			return defaultValue;
		}

		/// <summary>
		/// Parses a range string like "3000-3999" into a tuple.
		/// </summary>
		private (int Start, int End) ParseRange(string option, (int Start, int End) defaultValue)
		{
			var str = this.GetString(option, null);
			if (string.IsNullOrEmpty(str))
				return defaultValue;

			var parts = str.Split('-');
			if (parts.Length == 2 &&
				int.TryParse(parts[0], out var start) &&
				int.TryParse(parts[1], out var end))
			{
				return (start, end);
			}

			return defaultValue;
		}
	}
}
