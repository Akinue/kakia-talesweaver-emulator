using Kakia.TW.Shared.Database;
using Kakia.TW.Shared.Database.MySQL;
using Kakia.TW.Shared.World;
using MySqlConnector;
using Yggdrasil.Logging;
using Yggdrasil.Util;

namespace Kakia.TW.World.Database
{
	/// <summary>
	/// World server database interface.
	/// </summary>
	public class WorldDb : Db
	{
		/// <summary>
		/// Gets the selected character name for the given account from the session.
		/// </summary>
		public string? GetSelectedCharacterName(int accountId)
		{
			using var conn = GetConnection();
			using var cmd = new MySqlCommand("SELECT selected_character FROM accounts WHERE accountId = @id", conn);
			cmd.Parameters.AddWithValue("@id", accountId);
			var result = cmd.ExecuteScalar();
			return result as string;
		}

		public WorldCharacter LoadCharacter(int accountId, string charName)
		{
			WorldCharacter charData = null;
			int charId = 0;

			using (var conn = GetConnection())
			{
				// 1. Load Main Character Data
				string sql = @"SELECT * FROM characters WHERE accountId = @acc AND name = @name";
				using (var cmd = new MySqlCommand(sql, conn))
				{
					cmd.Parameters.AddWithValue("@acc", accountId);
					cmd.Parameters.AddWithValue("@name", charName);

					using var reader = cmd.ExecuteReader();
					if (!reader.Read()) return null;

					charId = reader.GetInt32("characterId");

					// Read Map Position (Default to Narvik starter area if null/missing)
					ushort mapId = 6;
					int zoneId = 38656;
					ushort x = 305;
					ushort y = 220;

					if (reader.HasColumn("map_id") && !reader.IsDBNull(reader.GetOrdinal("map_id")))
						mapId = (ushort)reader.GetInt16("map_id");
					if (reader.HasColumn("zone_id") && !reader.IsDBNull(reader.GetOrdinal("zone_id")))
						zoneId = reader.GetInt32("zone_id");
					if (reader.HasColumn("x") && !reader.IsDBNull(reader.GetOrdinal("x")))
						x = (ushort)reader.GetInt16("x");
					if (reader.HasColumn("y") && !reader.IsDBNull(reader.GetOrdinal("y")))
						y = (ushort)reader.GetInt16("y");

					charData = new WorldCharacter
					{
						UserId = (uint)charId,
						Name = reader.GetString("name"),
						ModelId = (uint)reader.GetInt32("char_type"), // Full ModelId (e.g., 2201536)
						TitleId = (uint)reader.GetInt32("title_id"),

						// Progression
						Level = reader.GetInt32("level"),
						CurrentExp = reader.GetInt64("exp"),

						// Vitals
						CurrentHP = (uint)reader.GetInt32("hp"),
						MaxHP = (uint)reader.GetInt32("hp_max"),
						CurrentMP = (uint)reader.GetInt32("mp"),
						MaxMP = (uint)reader.GetInt32("mp_max"),
						CurrentSP = (ulong)reader.GetInt32("sp"),
						MaxSP = (ulong)reader.GetInt32("sp_max"),

						// Primary Stats
						StatStab = reader.GetInt16("stat_stab"),
						StatHack = reader.GetInt16("stat_hack"),
						StatInt = reader.GetInt16("stat_int"),
						StatDef = reader.GetInt16("stat_def"),
						StatMR = reader.GetInt16("stat_mr"),
						StatDex = reader.GetInt16("stat_dex"),
						StatAgi = reader.GetInt16("stat_agi"),

						// Position (includes MapId, ZoneId, X, Y)
						ObjectPos = new ObjectPos
						{
							Position = new WorldPosition(mapId, zoneId, x, y)
						}
					};

					// Deserialize Blob
					var appBytes = (byte[])reader["appearance_data"];
					charData.Appearance = DbSerializer.Deserialize<CharacterAppearance>(appBytes);
				}

				// 2. Load Items (Inventory & Equipment)
				charData.Equipment = LoadItems(conn, charId);
			}

			return charData;
		}

		private List<GameItem> LoadItems(MySqlConnection conn, int charId)
		{
			var items = new List<GameItem>();
			using var cmd = new MySqlCommand("SELECT * FROM items WHERE characterId = @cid", conn);
			cmd.Parameters.AddWithValue("@cid", charId);

			using var reader = cmd.ExecuteReader();
			while (reader.Read())
			{
				var item = new GameItem
				{
					ItemId = reader.GetInt32("itemId"),
					Amount = reader.GetInt16("amount"),
					Durability = reader.GetInt16("durability"),
					Slot = reader.GetInt32("slot"),
					Refine = reader.GetByte("refine"),
					VisualId = (uint)reader.GetInt32("visualId")
				};

				// Load Stats Blob
				if (!reader.IsDBNull(reader.GetOrdinal("stats_data")))
				{
					var blob = (byte[])reader["stats_data"];
					item.Stats = DbSerializer.Deserialize<List<ItemStat>>(blob);
				}

				// Load Magic Props Blob
				if (!reader.IsDBNull(reader.GetOrdinal("magic_props_data")))
				{
					var blob = (byte[])reader["magic_props_data"];
					item.MagicProperties = DbSerializer.Deserialize<List<ItemMagicProperty>>(blob);
				}

				items.Add(item);
			}
			return items;
		}

		public void SaveCharacterPosition(WorldCharacter c)
		{
			using var conn = GetConnection();
			using var cmd = new MySqlCommand("UPDATE characters SET map_id = @map, zone_id = @zone, x = @x, y = @y WHERE characterId = @id", conn);
			cmd.Parameters.AddWithValue("@map", c.ObjectPos.Position.MapId);
			cmd.Parameters.AddWithValue("@zone", c.ObjectPos.Position.ZoneId);
			cmd.Parameters.AddWithValue("@x", c.ObjectPos.Position.X);
			cmd.Parameters.AddWithValue("@y", c.ObjectPos.Position.Y);
			cmd.Parameters.AddWithValue("@id", c.UserId);
			cmd.ExecuteNonQuery();
		}

		public void SaveCharacterFull(WorldCharacter c)
		{
			using var conn = GetConnection();
			using var trans = conn.BeginTransaction();

			try
			{
				// 1. Update Character Stats
				string updateSql = @"UPDATE characters SET 
                                     hp=@hp, mp=@mp, sp=@sp, level=@lvl, exp=@exp,
                                     stat_stab=@stab, stat_hack=@hack, stat_int=@int, stat_def=@def, stat_mr=@mr, stat_dex=@dex, stat_agi=@agi
                                     WHERE characterId=@id";

				using (var cmd = new MySqlCommand(updateSql, conn, trans))
				{
					cmd.Parameters.AddWithValue("@hp", c.CurrentHP);
					cmd.Parameters.AddWithValue("@mp", c.CurrentMP);
					cmd.Parameters.AddWithValue("@sp", c.CurrentSP);
					cmd.Parameters.AddWithValue("@lvl", c.Level);
					cmd.Parameters.AddWithValue("@exp", c.CurrentExp);

					// Primary Stats
					cmd.Parameters.AddWithValue("@stab", c.StatStab);
					cmd.Parameters.AddWithValue("@hack", c.StatHack);
					cmd.Parameters.AddWithValue("@int", c.StatInt);
					cmd.Parameters.AddWithValue("@def", c.StatDef);
					cmd.Parameters.AddWithValue("@mr", c.StatMR);
					cmd.Parameters.AddWithValue("@dex", c.StatDex);
					cmd.Parameters.AddWithValue("@agi", c.StatAgi);

					cmd.Parameters.AddWithValue("@id", c.UserId);
					cmd.ExecuteNonQuery();
				}

				// 2. Save Items (Basic strategy: Delete all owned items, Re-insert)
				// In a production server, you would want to diff updates to prevent ID thrashing,
				// but for this implementation, a flush-and-insert is reliable.
				using (var delCmd = new MySqlCommand("DELETE FROM items WHERE characterId = @id", conn, trans))
				{
					delCmd.Parameters.AddWithValue("@id", c.UserId);
					delCmd.ExecuteNonQuery();
				}

				if (c.Equipment.Count > 0)
				{
					string insertItemSql = @"INSERT INTO items 
                        (characterId, itemId, amount, durability, slot, refine, visualId, stats_data, magic_props_data)
                        VALUES 
                        (@cid, @iid, @amt, @dur, @slot, @ref, @vis, @stats, @magic)";

					using (var insCmd = new MySqlCommand(insertItemSql, conn, trans))
					{
						// Prepare parameters once
						var pCid = insCmd.Parameters.Add("@cid", MySqlDbType.Int32);
						var pIid = insCmd.Parameters.Add("@iid", MySqlDbType.Int32);
						var pAmt = insCmd.Parameters.Add("@amt", MySqlDbType.Int16);
						var pDur = insCmd.Parameters.Add("@dur", MySqlDbType.Int16);
						var pSlot = insCmd.Parameters.Add("@slot", MySqlDbType.Int32);
						var pRef = insCmd.Parameters.Add("@ref", MySqlDbType.Byte);
						var pVis = insCmd.Parameters.Add("@vis", MySqlDbType.Int32);
						var pStats = insCmd.Parameters.Add("@stats", MySqlDbType.Blob);
						var pMagic = insCmd.Parameters.Add("@magic", MySqlDbType.Blob);

						foreach (var item in c.Equipment)
						{
							pCid.Value = c.UserId;
							pIid.Value = item.ItemId;
							pAmt.Value = item.Amount;
							pDur.Value = item.Durability;
							pSlot.Value = item.Slot;
							pRef.Value = item.Refine;
							pVis.Value = item.VisualId;
							pStats.Value = DbSerializer.Serialize(item.Stats);
							pMagic.Value = DbSerializer.Serialize(item.MagicProperties);

							insCmd.ExecuteNonQuery();
						}
					}
				}

				trans.Commit();
			}
			catch
			{
				trans.Rollback();
				throw;
			}
		}
	}
}
