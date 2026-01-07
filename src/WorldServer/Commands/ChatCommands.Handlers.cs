using Kakia.TW.Shared.Data;
using Kakia.TW.Shared.World;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Network;
using Yggdrasil.Logging;
using Yggdrasil.Util;
using Yggdrasil.Util.Commands;

namespace Kakia.TW.World.Commands
{
	/// <summary>
	/// Chat command handlers for TalesWeaver.
	/// </summary>
	public partial class ChatCommands
	{
		/// <summary>
		/// Creates a new instance of ChatCommands and registers all commands.
		/// </summary>
		public ChatCommands()
		{
			// Help commands
			this.Add("help", "[command]", "Displays help for available commands.", this.HandleHelp);
			this.Add("commands", "", "Alias for help.", this.HandleHelp);

			// Player information
			this.Add("where", "", "Displays your current position.", this.HandleWhere);
			this.Add("pos", "", "Alias for where.", this.HandleWhere);

			// Warping
			this.Add("warp", "<mapId> <zoneId> [x] [y]", "Warps to the specified map and zone.", this.HandleWarp);
			this.Add("goto", "<mapId> <zoneId> [x] [y]", "Alias for warp.", this.HandleWarp);

			// Map management
			this.Add("listmaps", "", "Shows all loaded map IDs.", this.HandleListMaps);

			// Character refresh
			this.Add("refresh", "", "Respawns your character.", this.HandleRefresh);

			// Effects
			this.Add("chareffect", "<effect id>", "Plays a character effect (1=levelup, 2=maxlevel).", this.HandleCharEffect);
			this.Add("effect", "<effect id>", "Alias for chareffect.", this.HandleCharEffect);

			// Stats
			this.Add("levelup", "[levels]", "Increases your level.", this.HandleLevelUp);
			this.Add("stat", "", "Shows your current stats.", this.HandleStat);
			this.Add("setstat", "<stat> <value>", "Sets a stat value (stab/hack/int/def/mr/dex/agi).", this.HandleSetStat);
			this.Add("addstatpoints", "<points>", "Adds stat points.", this.HandleAddStatPoints);

			// Player management
			this.Add("who", "", "Shows the number of players online.", this.HandleWho);

			// Save
			this.Add("save", "", "Saves your character data.", this.HandleSave);

			// Item/Monster Database Commands
			this.Add("iteminfo", "<id|name>", "Shows item database info.", this.HandleItemInfo);
			this.Add("monsterinfo", "<id|name>", "Shows monster database info.", this.HandleMonsterInfo);
			this.Add("searchitem", "<name>", "Searches items by name.", this.HandleSearchItem);
			this.Add("searchmonster", "<name>", "Searches monsters by name.", this.HandleSearchMonster);
			this.Add("spawnmonster", "<id>", "Spawns a monster at your location.", this.HandleSpawnMonster);

			// Dev
			this.Add("test", "", "", this.HandleTest);
		}

		private CommandResult HandleTest(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var npc = new Npc("TestNPC", 2203235);
			npc.Position = sender.Position;
			npc.Direction = (Direction)RandomProvider.Get().Next(8);
			sender.Instance.AddNpc(npc, true);
			Send.SpawnHardcoded(sender.Connection, npc);

			return CommandResult.Okay;
		}

		/// <summary>
		/// Sends a server message to the player.
		/// </summary>
		private void Msg(Player player, string message)
		{
			if (player.Connection != null)
				Send.Chat(player.Connection, 0, $"[Server] {message}");
		}

		/// <summary>
		/// Displays available commands or help for a specific command.
		/// </summary>
		private CommandResult HandleHelp(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count == 0)
			{
				Msg(sender, "Available commands:");
				foreach (var cmd in _commands.Values.Distinct())
				{
					Msg(sender, $"  >{cmd.Name} {cmd.Usage}");
				}
				return CommandResult.Okay;
			}

			var cmdName = args.Get(0);
			var command = this.GetCommand(cmdName);
			if (command == null)
			{
				Msg(sender, $"Unknown command: {cmdName}");
				return CommandResult.Okay;
			}

			Msg(sender, $">{command.Name} {command.Usage}");
			Msg(sender, $"  {command.Description}");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Displays the player's current position.
		/// </summary>
		private CommandResult HandleWhere(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var mapId = target.Data.MapId;
			var zoneId = target.Data.ZoneId;
			var x = target.Position.X;
			var y = target.Position.Y;
			var dir = target.Direction;

			Msg(sender, $"Map: {mapId}-{zoneId}, Position: ({x}, {y}), Direction: {dir}");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Warps the player to a specified location.
		/// </summary>
		private CommandResult HandleWarp(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 2)
			{
				Msg(sender, "Usage: >warp <mapId> <zoneId> [x] [y]");
				return CommandResult.InvalidArgument;
			}

			if (!ushort.TryParse(args.Get(0), out var mapId))
			{
				Msg(sender, "Invalid map ID.");
				return CommandResult.InvalidArgument;
			}

			if (!ushort.TryParse(args.Get(1), out var zoneId))
			{
				Msg(sender, "Invalid zone ID.");
				return CommandResult.InvalidArgument;
			}

			// Default coordinates
			ushort x = 100, y = 100;

			if (args.Count >= 3 && ushort.TryParse(args.Get(2), out var parsedX))
				x = parsedX;

			if (args.Count >= 4 && ushort.TryParse(args.Get(3), out var parsedY))
				y = parsedY;

			Log.Info($"Player {target.Data.Name} warping to Map {mapId}-{zoneId} ({x}, {y})");
			target.Warp(mapId, zoneId, x, y);
			Msg(sender, $"Warped to Map {mapId}-{zoneId} at ({x}, {y})");

			return CommandResult.Okay;
		}

		/// <summary>
		/// Shows all loaded map IDs.
		/// </summary>
		private CommandResult HandleListMaps(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var mapList = string.Join(", ", WorldServer.Instance.World.Maps.GetLoadedMapIds());
			Msg(sender, $"Loaded Maps: {mapList}");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Respawns the player's character.
		/// </summary>
		private CommandResult HandleRefresh(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (target.Connection != null)
			{
				Send.SpawnUser(target.Connection, target.Data, isSelf: true);
				Msg(sender, "Character refreshed.");
			}
			return CommandResult.Okay;
		}

		/// <summary>
		/// Plays a character effect.
		/// </summary>
		private CommandResult HandleCharEffect(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1 || !byte.TryParse(args.Get(0), out var effectId))
			{
				Msg(sender, "Usage: >chareffect <effect id>");
				Msg(sender, "  1 = Level Up");
				Msg(sender, "  2 = Max Level");
				return CommandResult.InvalidArgument;
			}

			if (target.Connection != null)
			{
				Send.CharEffect(target.Connection, target.ObjectId, effectId);
				Msg(sender, $"Played effect {effectId} on {target.Data.Name}.");
			}
			return CommandResult.Okay;
		}

		/// <summary>
		/// Increases the player's level.
		/// </summary>
		private CommandResult HandleLevelUp(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var levels = 1;
			if (args.Count >= 1 && int.TryParse(args.Get(0), out var parsedLevels))
				levels = parsedLevels;

			var oldLevel = target.Data.Level;
			target.Data.Level = (byte)Math.Min(255, target.Data.Level + levels);

			Msg(sender, $"Level changed from {oldLevel} to {target.Data.Level}.");

			// Send stat update and level up effect
			if (target.Connection != null)
			{
				Send.StatUpdate(target.Connection, target.Data);
				Send.CharEffect(target.Connection, target.ObjectId, 1); // Level up effect
			}

			return CommandResult.Okay;
		}

		/// <summary>
		/// Shows the player's current stats.
		/// </summary>
		private CommandResult HandleStat(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var d = target.Data;
			Msg(sender, $"Level: {d.Level}, HP: {d.CurrentHP}/{d.MaxHP}, MP: {d.CurrentMP}/{d.MaxMP}");
			Msg(sender, $"Stab: {d.StatStab}, Hack: {d.StatHack}, Int: {d.StatInt}");
			Msg(sender, $"Def: {d.StatDef}, MR: {d.StatMR}, Dex: {d.StatDex}, Agi: {d.StatAgi}");
			Msg(sender, $"Stat Points: {d.StatPoints}");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Sets a specific stat value.
		/// </summary>
		private CommandResult HandleSetStat(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 2)
			{
				Msg(sender, "Usage: >setstat <stat> <value>");
				Msg(sender, "Stats: stab, hack, int, def, mr, dex, agi");
				return CommandResult.InvalidArgument;
			}

			var statName = args.Get(0).ToLowerInvariant();
			if (!int.TryParse(args.Get(1), out var value))
			{
				Msg(sender, "Invalid value.");
				return CommandResult.InvalidArgument;
			}

			var d = target.Data;
			switch (statName)
			{
				case "stab": d.StatStab = (short)value; break;
				case "hack": d.StatHack = (short)value; break;
				case "int": d.StatInt = (short)value; break;
				case "def": d.StatDef = (short)value; break;
				case "mr": d.StatMR = (short)value; break;
				case "dex": d.StatDex = (short)value; break;
				case "agi": d.StatAgi = (short)value; break;
				default:
					Msg(sender, $"Unknown stat: {statName}");
					return CommandResult.InvalidArgument;
			}

			Msg(sender, $"Set {statName} to {value}.");

			if (target.Connection != null)
				Send.StatUpdate(target.Connection, target.Data);

			return CommandResult.Okay;
		}

		/// <summary>
		/// Adds stat points to the player.
		/// </summary>
		private CommandResult HandleAddStatPoints(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1 || !int.TryParse(args.Get(0), out var points))
			{
				Msg(sender, "Usage: >addstatpoints <points>");
				return CommandResult.InvalidArgument;
			}

			target.Data.StatPoints += points;
			Msg(sender, $"Added {points} stat points. Total: {target.Data.StatPoints}");

			if (target.Connection != null)
				Send.StatUpdate(target.Connection, target.Data);

			return CommandResult.Okay;
		}

		/// <summary>
		/// Shows the number of players online.
		/// </summary>
		private CommandResult HandleWho(Player sender, Player target, string message, string commandName, Arguments args)
		{
			var count = WorldServer.Instance.World.GetCharacterCount();
			Msg(sender, $"Players online: {count}");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Saves the player's character data.
		/// </summary>
		private CommandResult HandleSave(Player sender, Player target, string message, string commandName, Arguments args)
		{
			target.Save();
			Msg(sender, "Character saved.");
			return CommandResult.Okay;
		}

		/// <summary>
		/// Shows item database information.
		/// </summary>
		private CommandResult HandleItemInfo(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1)
			{
				Msg(sender, "Usage: >iteminfo <id|name>");
				return CommandResult.InvalidArgument;
			}

			var query = args.Get(0);
			ItemData? item = null;

			// Try parsing as ID first
			if (int.TryParse(query, out var id))
				item = WorldServer.Instance.ItemDb.GetById(id);

			// Otherwise search by name
			item ??= WorldServer.Instance.ItemDb.GetByName(query);

			if (item == null)
			{
				Msg(sender, $"Item not found: {query}");
				return CommandResult.Okay;
			}

			Msg(sender, $"=== {item.Name} (ID: {item.Id}) ===");
			Msg(sender, $"Type: {item.Type}, Slot: {item.Slot}");
			Msg(sender, $"Price: {item.Price}, Stack: {item.MaxStack}");

			if (item.IsEquipment)
			{
				if (item.MinDamage > 0 || item.MaxDamage > 0)
					Msg(sender, $"Damage: {item.MinDamage}-{item.MaxDamage}");
				if (item.Defense > 0)
					Msg(sender, $"Defense: {item.Defense}");
				if (item.MagicDefense > 0)
					Msg(sender, $"Magic Defense: {item.MagicDefense}");
				if (item.BonusStats.Count > 0)
				{
					var stats = string.Join(", ", item.BonusStats.Select(s => $"{s.Key}+{s.Value}"));
					Msg(sender, $"Bonus: {stats}");
				}
			}

			if (item.LevelRequirement > 0)
				Msg(sender, $"Level Req: {item.LevelRequirement}");

			if (!string.IsNullOrEmpty(item.Description))
				Msg(sender, $"Desc: {item.Description}");

			if (item.DropSources.Count > 0)
			{
				var drops = string.Join(", ", item.DropSources.Select(d =>
				{
					var monster = WorldServer.Instance.MonsterDb.GetById(d.MonsterId);
					return monster != null ? $"{monster.Name} ({d.Rate}%)" : $"#{d.MonsterId} ({d.Rate}%)";
				}));
				Msg(sender, $"Drops from: {drops}");
			}

			return CommandResult.Okay;
		}

		/// <summary>
		/// Shows monster database information.
		/// </summary>
		private CommandResult HandleMonsterInfo(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1)
			{
				Msg(sender, "Usage: >monsterinfo <id|name>");
				return CommandResult.InvalidArgument;
			}

			var query = args.Get(0);
			MonsterData? monster = null;

			// Try parsing as ID first
			if (int.TryParse(query, out var id))
				monster = WorldServer.Instance.MonsterDb.GetById(id);

			// Otherwise search by name
			monster ??= WorldServer.Instance.MonsterDb.GetByName(query);

			if (monster == null)
			{
				Msg(sender, $"Monster not found: {query}");
				return CommandResult.Okay;
			}

			Msg(sender, $"=== {monster.Name} (ID: {monster.Id}) ===");
			Msg(sender, $"Level: {monster.Level}, Type: {monster.Type}");
			Msg(sender, $"HP: {monster.MaxHP}, MP: {monster.MaxMP}");
			Msg(sender, $"ATK: {monster.Attack}, DEF: {monster.Defense}");
			Msg(sender, $"MATK: {monster.MagicAttack}, MDEF: {monster.MagicDefense}");
			Msg(sender, $"EXP: {monster.Experience}, Gold: {monster.Gold}");
			Msg(sender, $"Behavior: {monster.Behavior}, Aggro: {monster.AggroRange}");
			Msg(sender, $"Respawn: {monster.RespawnTimeMs / 1000}s");

			if (monster.DropTable.Count > 0)
			{
				Msg(sender, "--- Drop Table ---");
				foreach (var drop in monster.DropTable.Take(5))
				{
					var item = WorldServer.Instance.ItemDb.GetById(drop.ItemId);
					var name = item?.Name ?? $"#{drop.ItemId}";
					Msg(sender, $"  {name}: {drop.DropRate}% (x{drop.MinAmount}-{drop.MaxAmount})");
				}
				if (monster.DropTable.Count > 5)
					Msg(sender, $"  ...and {monster.DropTable.Count - 5} more");
			}

			return CommandResult.Okay;
		}

		/// <summary>
		/// Searches items by name.
		/// </summary>
		private CommandResult HandleSearchItem(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1)
			{
				Msg(sender, "Usage: >searchitem <name>");
				return CommandResult.InvalidArgument;
			}

			var query = string.Join(" ", args.GetAll());
			var results = WorldServer.Instance.ItemDb.Search(query, 10).ToList();

			if (results.Count == 0)
			{
				Msg(sender, $"No items found matching: {query}");
				return CommandResult.Okay;
			}

			Msg(sender, $"Found {results.Count} items:");
			foreach (var item in results)
			{
				Msg(sender, $"  [{item.Id}] {item.Name} ({item.Type})");
			}

			return CommandResult.Okay;
		}

		/// <summary>
		/// Searches monsters by name.
		/// </summary>
		private CommandResult HandleSearchMonster(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1)
			{
				Msg(sender, "Usage: >searchmonster <name>");
				return CommandResult.InvalidArgument;
			}

			var query = string.Join(" ", args.GetAll());
			var results = WorldServer.Instance.MonsterDb.Search(query, 10).ToList();

			if (results.Count == 0)
			{
				Msg(sender, $"No monsters found matching: {query}");
				return CommandResult.Okay;
			}

			Msg(sender, $"Found {results.Count} monsters:");
			foreach (var monster in results)
			{
				Msg(sender, $"  [{monster.Id}] {monster.Name} (Lv.{monster.Level})");
			}

			return CommandResult.Okay;
		}

		/// <summary>
		/// Spawns a monster at the player's location.
		/// </summary>
		private CommandResult HandleSpawnMonster(Player sender, Player target, string message, string commandName, Arguments args)
		{
			if (args.Count < 1)
			{
				Msg(sender, "Usage: >spawnmonster <id>");
				return CommandResult.InvalidArgument;
			}

			if (!int.TryParse(args.Get(0), out var monsterId))
			{
				Msg(sender, "Invalid monster ID.");
				return CommandResult.InvalidArgument;
			}

			var monsterData = WorldServer.Instance.MonsterDb.GetById(monsterId);
			if (monsterData == null)
			{
				Msg(sender, $"Monster ID {monsterId} not found in database.");
				return CommandResult.Okay;
			}

			if (target.Instance == null)
			{
				Msg(sender, "Player is not on a map.");
				return CommandResult.Fail;
			}

			// Map will assign ObjectId via RegisterEntity
			var monster = new Monster(monsterData.Name, (uint)monsterData.ModelId)
			{
				MaxHP = (uint)monsterData.MaxHP,
				CurrentHP = (uint)monsterData.MaxHP,
				Position = target.Position,
				Direction = target.Direction
			};

			target.Instance.AddMonster(monster);
			Msg(sender, $"Spawned {monsterData.Name} (ObjectId: {monster.ObjectId}) at your location.");

			return CommandResult.Okay;
		}
	}
}
