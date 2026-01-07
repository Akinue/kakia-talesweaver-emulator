using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TWConv
{
	/// <summary>
	/// Converts EntitySpawnPacket binary files to C# scripts for the WorldServer.
	///
	/// Binary format (saved files, little-endian):
	/// - Byte 0: Opcode (0x07)
	/// - Byte 1: Flag (varies by entity type)
	/// - Byte 2: SpawnType (0=Monster, 2=NPC, 4=Portal, etc.)
	/// - Bytes 3+: Payload (varies by SpawnType)
	///
	/// Filename format: {SpawnType}_{ObjectID}.bin
	/// </summary>
	public static class EntityToScriptConverter
	{
		public static void Run(string[] args)
		{
			string baseDir = args.Length > 0 ? args[0] : ".";
			string outputDir = args.Length > 1 ? args[1] : Path.Combine(baseDir, "Scripts");

			Console.WriteLine("=== Entity to Script Converter ===");
			Console.WriteLine($"Base directory: {Path.GetFullPath(baseDir)}");
			Console.WriteLine($"Output directory: {Path.GetFullPath(outputDir)}");
			Console.WriteLine();

			var mapsDir = Path.Combine(baseDir, "Maps");
			var npcsDir = Path.Combine(baseDir, "NPCs");

			if (!Directory.Exists(mapsDir) && !Directory.Exists(npcsDir))
			{
				Console.WriteLine("Error: No Maps or NPCs directories found.");
				Console.WriteLine("Expected structure:");
				Console.WriteLine("  Maps/MapId_X/ZoneId_Y/map.bin");
				Console.WriteLine("  Maps/MapId_X/ZoneId_Y/SpawnPos.txt");
				Console.WriteLine("  Maps/MapId_X/ZoneId_Y/Spawn/*.bin");
				Console.WriteLine("  NPCs/*.bin");
				return;
			}

			Directory.CreateDirectory(outputDir);
			Directory.CreateDirectory(Path.Combine(outputDir, "Spawns"));
			Directory.CreateDirectory(Path.Combine(outputDir, "Npcs"));
			Directory.CreateDirectory(Path.Combine(outputDir, "Warps"));

			int totalScripts = 0;

			// Process each map
			if (Directory.Exists(mapsDir))
			{
				foreach (var mapFolder in Directory.GetDirectories(mapsDir, "MapId_*"))
				{
					foreach (var zoneFolder in Directory.GetDirectories(mapFolder, "ZoneId_*"))
					{
						totalScripts += ProcessZone(zoneFolder, outputDir);
					}
				}
			}

			// Note: NPCs folder is legacy data - all entity spawning data is in Maps folder
			// Global NPCs processing skipped

			Console.WriteLine();
			Console.WriteLine($"=== Done! Generated {totalScripts} script files ===");
		}

		private static int ProcessZone(string zonePath, string outputDir)
		{
			var mapBinPath = Path.Combine(zonePath, "map.bin");
			if (!File.Exists(mapBinPath))
			{
				Console.WriteLine($"Skipping {zonePath}: No map.bin found");
				return 0;
			}

			// Read map header
			// Format: [0]=PacketType(0x15), [1-2]=MapId(BE), [3-4]=ZoneId(BE), [5-6]=MapType(BE), [7]=Attributes
			var mapData = File.ReadAllBytes(mapBinPath);
			ushort mapId = ReadUInt16BE(mapData, 1);
			ushort zoneId = ReadUInt16BE(mapData, 3);

			Console.WriteLine($"Processing Map {mapId}, Zone {zoneId}...");

			// Parse spawn positions
			var spawnPositions = new List<SpawnPosition>();
			var spawnPosPath = Path.Combine(zonePath, "SpawnPos.txt");
			if (File.Exists(spawnPosPath))
			{
				foreach (var line in File.ReadAllLines(spawnPosPath))
				{
					if (string.IsNullOrWhiteSpace(line)) continue;
					var parts = line.Split(',');
					if (parts.Length >= 5)
					{
						spawnPositions.Add(new SpawnPosition
						{
							EntityType = int.Parse(parts[0]),
							ZoneId = int.Parse(parts[1]),
							X = ushort.Parse(parts[2]),
							Y = ushort.Parse(parts[3]),
							Direction = byte.Parse(parts[4])
						});
					}
				}
			}

			// Parse entity files
			var monsters = new List<MonsterData>();
			var npcs = new List<NpcData>();
			var portals = new List<PortalData>();

			var spawnDir = Path.Combine(zonePath, "Spawn");
			if (Directory.Exists(spawnDir))
			{
				foreach (var file in Directory.GetFiles(spawnDir, "*.bin"))
				{
					var entity = ParseEntityFile(file);
					if (entity == null) continue;

					switch (entity)
					{
						case MonsterData m: monsters.Add(m); break;
						case NpcData n: npcs.Add(n); break;
						case PortalData p: portals.Add(p); break;
					}
				}
			}

			// Load warp portal configs
			var portalConfigs = new Dictionary<uint, WarpConfig>();
			var warpDir = Path.Combine(zonePath, "WarpPortals");
			if (Directory.Exists(warpDir))
			{
				foreach (var file in Directory.GetFiles(warpDir, "*.json"))
				{
					try
					{
						var json = File.ReadAllText(file);
						var config = ParseWarpConfig(json);
						if (config != null)
							portalConfigs[config.Id] = config;
					}
					catch { }
				}
			}

			Console.WriteLine($"  Found: {monsters.Count} monsters, {npcs.Count} NPCs, {portals.Count} portals, {spawnPositions.Count} spawn positions");

			int scriptsGenerated = 0;

			// Generate spawn script (monsters)
			if (monsters.Count > 0 || spawnPositions.Count > 0)
			{
				var script = GenerateSpawnScript(mapId, zoneId, monsters, spawnPositions);
				var path = Path.Combine(outputDir, "Spawns", $"Map{mapId}_Zone{zoneId}_Spawns.cs");
				File.WriteAllText(path, script);
				scriptsGenerated++;
			}

			// Generate NPC script
			if (npcs.Count > 0)
			{
				var script = GenerateNpcScript(mapId, zoneId, npcs);
				var path = Path.Combine(outputDir, "Npcs", $"Map{mapId}_Zone{zoneId}_Npcs.cs");
				File.WriteAllText(path, script);
				scriptsGenerated++;
			}

			// Generate warp script
			if (portals.Count > 0)
			{
				var script = GenerateWarpScript(mapId, zoneId, portals, portalConfigs);
				var path = Path.Combine(outputDir, "Warps", $"Map{mapId}_Zone{zoneId}_Warps.cs");
				File.WriteAllText(path, script);
				scriptsGenerated++;
			}

			return scriptsGenerated;
		}

		private static int ProcessGlobalNpcs(string npcsDir, string outputDir)
		{
			var npcs = new List<NpcData>();

			foreach (var file in Directory.GetFiles(npcsDir, "*.bin"))
			{
				var data = File.ReadAllBytes(file);
				if (data.Length < 7) continue;

				// Global NPCs use different format - parse as extended NPC
				var npc = ParseGlobalNpcFile(data, Path.GetFileNameWithoutExtension(file));
				if (npc != null)
					npcs.Add(npc);
			}

			if (npcs.Count == 0) return 0;

			Console.WriteLine($"Processing {npcs.Count} global NPCs...");

			var script = GenerateGlobalNpcScript(npcs);
			var path = Path.Combine(outputDir, "Npcs", "GlobalNpcs.cs");
			File.WriteAllText(path, script);
			return 1;
		}

		private static object ParseEntityFile(string filePath)
		{
			var data = File.ReadAllBytes(filePath);
			var filename = Path.GetFileNameWithoutExtension(filePath);

			if (data.Length < 7) return null;

			// Byte 1 is ActionType:
			// 0x00 = Spawn (actual entity spawn)
			// 0x01 = Despawn (remove entity, only 7 bytes)
			// 0x02 = Death (monster/player killed)
			// Only process spawn packets (0x00)
			byte actionType = data[1];
			if (actionType != 0x00)
				return null;

			// Format: {SpawnType}_{ObjectID}.bin
			var parts = filename.Split('_');
			if (parts.Length < 2) return null;

			byte spawnType = data[2];

			// Also verify against filename
			if (int.TryParse(parts[0], out int filenameType) && filenameType != spawnType)
			{
				Console.WriteLine($"  Warning: Type mismatch in {filename}: file says {spawnType}, name says {filenameType}");
			}

			switch (spawnType)
			{
				case 0x00: // Monster
					return ParseMonster(data, filename);

				case 0x01: // Extended NPC (has ModelId, positions as LE)
					return ParseExtendedNpc(data, filename);

				case 0x02: // Zone NPC (just ObjectId, no embedded position data)
					return ParseZoneNpc(data, filename);

				case 0x04: // Portal (positions as BE)
					return ParsePortal(data, filename);

				default:
					return null;
			}
		}

		private static MonsterData ParseMonster(byte[] data, string filename)
		{
			// Type 0x00 Monster format (matches template):
			// ObjectId(4 LE) + PosX(2 LE) + PosY(2 LE) + Direction(1)
			// Offsets: 3-6=ObjectId, 7-8=PosX, 9-10=PosY, 11=Direction
			if (data.Length < 7) return null;

			uint objectId = BitConverter.ToUInt32(data, 3);
			ushort posX = 0, posY = 0;
			byte direction = 0;

			if (data.Length >= 11)
			{
				posX = BitConverter.ToUInt16(data, 7);
				posY = BitConverter.ToUInt16(data, 9);
			}
			if (data.Length >= 12)
			{
				direction = data[11];
			}

			return new MonsterData
			{
				ObjectId = objectId,
				MonsterId = objectId,
				PositionX = posX,
				PositionY = posY,
				Direction = direction,
				Filename = filename
			};
		}

		private static NpcData ParseExtendedNpc(byte[] data, string filename)
		{
			// Type 0x01 Extended NPC format (matches template):
			// ObjectId(4 LE) + Padding(5) + ModelId(2 LE) + SpriteId(2 LE) + PosX(2 LE) + PosY(2 LE) + Direction(1)
			// Offsets: 3-6=ObjectId, 7-11=Padding, 12-13=ModelId, 14-15=SpriteId, 16-17=PosX, 18-19=PosY, 20=Direction
			if (data.Length < 21) return null;

			uint objectId = BitConverter.ToUInt32(data, 3);
			ushort modelId = BitConverter.ToUInt16(data, 12);
			ushort posX = BitConverter.ToUInt16(data, 16);
			ushort posY = BitConverter.ToUInt16(data, 18);
			byte direction = data[20];

			return new NpcData
			{
				ObjectId = objectId,
				NpcId = objectId,
				ModelId = modelId,
				PositionX = posX,
				PositionY = posY,
				Direction = direction,
				Filename = filename
			};
		}

		private static NpcData ParseZoneNpc(byte[] data, string filename)
		{
			// Type 0x02 Zone NPC format:
			// ObjectId(4 LE) + Padding(12) + PosX(2 LE) + PosY(2 LE) + Direction(1)
			// Offsets: 3-6=ObjectId, 7-18=Padding, 19-20=PosX(LE), 21-22=PosY(LE), 23=Direction
			if (data.Length < 24) return null;

			uint objectId = BitConverter.ToUInt32(data, 3);
			ushort posX = BitConverter.ToUInt16(data, 19);
			ushort posY = BitConverter.ToUInt16(data, 21);
			byte direction = data[23];

			return new NpcData
			{
				ObjectId = objectId,
				NpcId = objectId,
				ModelId = objectId, // No model ID in packet, use ObjectId
				PositionX = posX,
				PositionY = posY,
				Direction = direction,
				Filename = filename
			};
		}

		private static PortalData ParsePortal(byte[] data, string filename)
		{
			// Portal format: ObjectID(4 LE) + PosX(2 LE) + PosY(2 LE) + TargetMapID(2 LE) + TargetPortalID(2 LE)
			// Note: WorldResponse spawn packets use Little Endian
			if (data.Length < 15) return null;

			return new PortalData
			{
				ObjectId = BitConverter.ToUInt32(data, 3),
				PositionX = BitConverter.ToUInt16(data, 7),
				PositionY = BitConverter.ToUInt16(data, 9),
				TargetMapId = BitConverter.ToUInt16(data, 11),
				TargetPortalId = BitConverter.ToUInt16(data, 13),
				Filename = filename
			};
		}

		private static ushort ReadUInt16BE(byte[] data, int offset)
		{
			return (ushort)((data[offset] << 8) | data[offset + 1]);
		}

		private static NpcData ParseGlobalNpcFile(byte[] data, string filename)
		{
			// Global NPC files (from NPCs/ folder) have extended format (Type 0x01)
			// Format: 07 00 01 [ObjectID:4 LE] [Padding:5] [ModelID:2 LE] [SpriteID:2] [PosX:2 LE] [PosY:2 LE] [Dir:1]
			// Offsets:  0  1  2      3-6          7-11        12-13        14-15       16-17       18-19       20
			if (data.Length < 21) return null;

			// Only process spawn packets (action type 0x00)
			if (data[1] != 0x00) return null;

			uint objectId = BitConverter.ToUInt32(data, 3);
			ushort modelId = BitConverter.ToUInt16(data, 12);
			ushort posX = BitConverter.ToUInt16(data, 16);
			ushort posY = BitConverter.ToUInt16(data, 18);
			byte direction = data.Length >= 21 ? data[20] : (byte)0;

			return new NpcData
			{
				ObjectId = objectId,
				NpcId = objectId,
				ModelId = modelId,
				PositionX = posX,
				PositionY = posY,
				Direction = direction,
				Filename = filename
			};
		}

		private static WarpConfig ParseWarpConfig(string json)
		{
			// Simple JSON parsing for warp configs
			var config = new WarpConfig();

			if (json.Contains("\"Id\""))
			{
				var match = System.Text.RegularExpressions.Regex.Match(json, @"""Id""\s*:\s*(\d+)");
				if (match.Success) config.Id = uint.Parse(match.Groups[1].Value);
			}
			if (json.Contains("\"DestMapId\""))
			{
				var match = System.Text.RegularExpressions.Regex.Match(json, @"""DestMapId""\s*:\s*(\d+)");
				if (match.Success) config.DestMapId = ushort.Parse(match.Groups[1].Value);
			}
			if (json.Contains("\"DestZoneId\""))
			{
				var match = System.Text.RegularExpressions.Regex.Match(json, @"""DestZoneId""\s*:\s*(\d+)");
				if (match.Success) config.DestZoneId = ushort.Parse(match.Groups[1].Value);
			}
			if (json.Contains("\"DestX\""))
			{
				var match = System.Text.RegularExpressions.Regex.Match(json, @"""DestX""\s*:\s*(\d+)");
				if (match.Success) config.DestX = ushort.Parse(match.Groups[1].Value);
			}
			if (json.Contains("\"DestY\""))
			{
				var match = System.Text.RegularExpressions.Regex.Match(json, @"""DestY""\s*:\s*(\d+)");
				if (match.Success) config.DestY = ushort.Parse(match.Groups[1].Value);
			}

			return config.Id > 0 ? config : null;
		}

		#region Script Generators

		private static string GenerateSpawnScript(ushort mapId, ushort zoneId, List<MonsterData> monsters, List<SpawnPosition> positions)
		{
			var sb = new StringBuilder();
			sb.AppendLine("// Auto-generated from EntitySpawnPacket binary files");
			sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			sb.AppendLine();
			sb.AppendLine("using Kakia.TW.World.Scripting;");
			sb.AppendLine();
			sb.AppendLine("namespace Kakia.TW.World.Scripts.Spawns");
			sb.AppendLine("{");
			sb.AppendLine($"    public class Map{mapId}_Zone{zoneId}_Spawns : SpawnScript");
			sb.AppendLine("    {");
			sb.AppendLine("        public override void Load()");
			sb.AppendLine("        {");

			// Generate spawners from monster data (has embedded positions)
			foreach (var monster in monsters)
			{
				// Use position from packet, or fallback to 100,100 if zero
				ushort x = monster.PositionX > 0 ? monster.PositionX : (ushort)100;
				ushort y = monster.PositionY > 0 ? monster.PositionY : (ushort)100;

				sb.AppendLine($"            // ObjectId: {monster.ObjectId}");
				sb.AppendLine($"            AddSpawner(");
				sb.AppendLine($"                modelId: {monster.MonsterId},");
				sb.AppendLine($"                name: \"Monster_{monster.ObjectId}\",");
				sb.AppendLine($"                mapId: {mapId},");
				sb.AppendLine($"                zoneId: {zoneId},");
				sb.AppendLine($"                x: {x},");
				sb.AppendLine($"                y: {y},");
				sb.AppendLine($"                direction: {monster.Direction},");
				sb.AppendLine($"                respawnTime: 30,");
				sb.AppendLine($"                maxHp: 100");
				sb.AppendLine($"            );");
				sb.AppendLine();
			}

			// Also output spawn positions from SpawnPos.txt as reference
			if (positions.Count > 0 && monsters.Count == 0)
			{
				sb.AppendLine("            // Spawn positions from SpawnPos.txt (no monster data found):");
				var uniquePositions = positions
					.GroupBy(p => (p.X, p.Y, p.Direction))
					.Select(g => g.First())
					.ToList();

				foreach (var pos in uniquePositions)
				{
					sb.AppendLine($"            // Position: ({pos.X}, {pos.Y}), Direction: {pos.Direction}");
				}
			}

			sb.AppendLine("        }");
			sb.AppendLine("    }");
			sb.AppendLine("}");

			return sb.ToString();
		}

		private static string GenerateNpcScript(ushort mapId, ushort zoneId, List<NpcData> npcs)
		{
			var sb = new StringBuilder();
			sb.AppendLine("// Auto-generated from EntitySpawnPacket binary files");
			sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			sb.AppendLine();
			sb.AppendLine("using Kakia.TW.World.Scripting;");
			sb.AppendLine();
			sb.AppendLine("namespace Kakia.TW.World.Scripts.Npcs");
			sb.AppendLine("{");
			sb.AppendLine($"    public class Map{mapId}_Zone{zoneId}_Npcs : NpcScript");
			sb.AppendLine("    {");
			sb.AppendLine("        public override void Load()");
			sb.AppendLine("        {");

			foreach (var npc in npcs)
			{
				sb.AppendLine($"            // ObjectId: {npc.ObjectId}");
				sb.AppendLine($"            SpawnNpc(");
				sb.AppendLine($"                modelId: {npc.ModelId},");
				sb.AppendLine($"                name: \"NPC_{npc.ObjectId}\",");
				sb.AppendLine($"                mapId: {mapId},");
				sb.AppendLine($"                zoneId: {zoneId},");
				sb.AppendLine($"                x: {npc.PositionX},");
				sb.AppendLine($"                y: {npc.PositionY},");
				sb.AppendLine($"                direction: {npc.Direction},");
				sb.AppendLine($"                dialogFunc: null");
				sb.AppendLine($"            );");
				sb.AppendLine();
			}

			sb.AppendLine("        }");
			sb.AppendLine("    }");
			sb.AppendLine("}");

			return sb.ToString();
		}

		private static string GenerateWarpScript(ushort mapId, ushort zoneId, List<PortalData> portals, Dictionary<uint, WarpConfig> configs)
		{
			var sb = new StringBuilder();
			sb.AppendLine("// Auto-generated from EntitySpawnPacket binary files");
			sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			sb.AppendLine();
			sb.AppendLine("using Kakia.TW.World.Scripting;");
			sb.AppendLine();
			sb.AppendLine("namespace Kakia.TW.World.Scripts.Warps");
			sb.AppendLine("{");
			sb.AppendLine($"    public class Map{mapId}_Zone{zoneId}_Warps : NpcScript");
			sb.AppendLine("    {");
			sb.AppendLine("        public override void Load()");
			sb.AppendLine("        {");

			foreach (var portal in portals)
			{
				// Check for config override
				configs.TryGetValue(portal.ObjectId, out var config);

				ushort destMapId = config?.DestMapId ?? portal.TargetMapId;
				ushort destZoneId = config?.DestZoneId ?? 1;
				ushort destX = config?.DestX ?? 100;
				ushort destY = config?.DestY ?? 100;

				sb.AppendLine($"            // ObjectId: {portal.ObjectId}");
				sb.AppendLine($"            SpawnWarp(");
				sb.AppendLine($"                mapId: {mapId},");
				sb.AppendLine($"                zoneId: {zoneId},");
				sb.AppendLine($"                x: {portal.PositionX},");
				sb.AppendLine($"                y: {portal.PositionY},");
				sb.AppendLine($"                destMapId: {destMapId},");
				sb.AppendLine($"                destZoneId: {destZoneId},");
				sb.AppendLine($"                destX: {destX},");
				sb.AppendLine($"                destY: {destY}");
				sb.AppendLine($"            );");
				sb.AppendLine();
			}

			sb.AppendLine("        }");
			sb.AppendLine("    }");
			sb.AppendLine("}");

			return sb.ToString();
		}

		private static string GenerateGlobalNpcScript(List<NpcData> npcs)
		{
			var sb = new StringBuilder();
			sb.AppendLine("// Auto-generated from EntitySpawnPacket binary files");
			sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			sb.AppendLine("// Global NPCs - assign mapId/zoneId based on where they should spawn");
			sb.AppendLine();
			sb.AppendLine("using Kakia.TW.World.Scripting;");
			sb.AppendLine();
			sb.AppendLine("namespace Kakia.TW.World.Scripts.Npcs");
			sb.AppendLine("{");
			sb.AppendLine("    public class GlobalNpcs : NpcScript");
			sb.AppendLine("    {");
			sb.AppendLine("        public override void Load()");
			sb.AppendLine("        {");

			foreach (var npc in npcs)
			{
				sb.AppendLine($"            // NPC ID: {npc.NpcId}, Model: {npc.ModelId}");
				sb.AppendLine($"            SpawnNpc(");
				sb.AppendLine($"                modelId: {npc.ModelId},");
				sb.AppendLine($"                name: \"NPC_{npc.NpcId}\",");
				sb.AppendLine($"                mapId: 0,     // TODO: Set correct map");
				sb.AppendLine($"                zoneId: 0,    // TODO: Set correct zone");
				sb.AppendLine($"                x: {npc.PositionX},");
				sb.AppendLine($"                y: {npc.PositionY},");
				sb.AppendLine($"                direction: {npc.Direction},");
				sb.AppendLine($"                dialogFunc: null  // TODO: Add dialog");
				sb.AppendLine($"            );");
				sb.AppendLine();
			}

			sb.AppendLine("        }");
			sb.AppendLine("    }");
			sb.AppendLine("}");

			return sb.ToString();
		}

		#endregion

		#region Data Classes

		private class SpawnPosition
		{
			public int EntityType;
			public int ZoneId;
			public ushort X;
			public ushort Y;
			public byte Direction;
		}

		private class MonsterData
		{
			public uint ObjectId;
			public uint MonsterId;
			public ushort PositionX;
			public ushort PositionY;
			public byte Direction;
			public string Filename;
		}

		private class NpcData
		{
			public uint ObjectId;
			public uint NpcId;
			public uint ModelId;
			public ushort PositionX;
			public ushort PositionY;
			public byte Direction;
			public string Filename;
		}

		private class PortalData
		{
			public uint ObjectId;
			public ushort PositionX;
			public ushort PositionY;
			public ushort TargetMapId;
			public ushort TargetPortalId;
			public string Filename;
		}

		private class WarpConfig
		{
			public uint Id;
			public ushort DestMapId;
			public ushort DestZoneId;
			public ushort DestX;
			public ushort DestY;
		}

		#endregion
	}
}
