using Kakia.TW.Shared.World;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Managers;
using Yggdrasil.Logging;
using Yggdrasil.Scripting;

namespace Kakia.TW.World.Scripting
{
	/// <summary>
	/// Base class for NPC scripts that define NPC spawns and dialog behavior.
	/// </summary>
	public abstract class NpcScript : IScript, IDisposable
	{
		private readonly List<Npc> _spawnedNpcs = new();

		/// <summary>
		/// Initializes the script.
		/// </summary>
		public bool Init()
		{
			Load();
			return true;
		}

		/// <summary>
		/// Called when the script is being removed before a reload.
		/// </summary>
		public virtual void Dispose()
		{
			// Remove all NPCs spawned by this script
			foreach (var npc in _spawnedNpcs)
			{
				npc.Instance?.RemoveNpc(npc.Id);
			}
			_spawnedNpcs.Clear();
		}

		/// <summary>
		/// Called when the script is being initialized. Override to spawn NPCs.
		/// </summary>
		public abstract void Load();

		/// <summary>
		/// Spawns an NPC at the specified location.
		/// </summary>
		/// <param name="modelId">The NPC's visual model ID.</param>
		/// <param name="name">The NPC's display name.</param>
		/// <param name="mapId">Map ID to spawn on.</param>
		/// <param name="zoneId">Zone ID to spawn on.</param>
		/// <param name="x">X coordinate.</param>
		/// <param name="y">Y coordinate.</param>
		/// <param name="direction">Facing direction (0-7).</param>
		/// <param name="dialogFunc">Optional dialog function when clicked.</param>
		/// <returns>The spawned NPC entity.</returns>
		protected Npc SpawnNpc(uint modelId, string name, ushort mapId, ushort zoneId, ushort x, ushort y, byte direction = 0, DialogFunc? dialogFunc = null)
		{
			var map = WorldServer.Instance.World.Maps.GetOrCreateMap(mapId, zoneId);
			if (map == null)
			{
				Log.Warning($"NpcScript: Cannot spawn NPC '{name}' - Map {mapId}-{zoneId} not found.");
				return null;
			}

			// Generate unique NPC ID (high range to avoid player ID conflicts)
			uint npcId = GenerateNpcId();

			var npc = new Npc(npcId, name, modelId)
			{
				Direction = direction,
				Script = dialogFunc,
				ObjectPos = new ObjectPos
				{
					Position = new WorldPosition(mapId, zoneId, x, y),
					Direction = direction
				}
			};

			map.AddNpc(npc);
			_spawnedNpcs.Add(npc);

			Log.Debug($"Spawned NPC '{name}' (ID: {npcId}, Model: {modelId}) at {mapId}-{zoneId} ({x},{y})");

			return npc;
		}

		/// <summary>
		/// Spawns a warp portal at the specified location.
		/// </summary>
		protected Warp SpawnWarp(ushort mapId, ushort zoneId, ushort x, ushort y, ushort destMapId, ushort destZoneId, ushort destX, ushort destY)
		{
			var map = WorldServer.Instance.World.Maps.GetOrCreateMap(mapId, zoneId);
			if (map == null)
			{
				Log.Warning($"NpcScript: Cannot spawn warp - Map {mapId}-{zoneId} not found.");
				return null;
			}

			uint warpId = GenerateNpcId();

			var warp = new Warp(warpId)
			{
				DestMapId = destMapId,
				DestZoneId = destZoneId,
				DestX = destX,
				DestY = destY,
				ObjectPos = new ObjectPos
				{
					Position = new WorldPosition(mapId, zoneId, x, y)
				}
			};

			map.AddWarp(warp);

			Log.Debug($"Spawned Warp (ID: {warpId}) at {mapId}-{zoneId} ({x},{y}) -> {destMapId}-{destZoneId} ({destX},{destY})");

			return warp;
		}

		private static uint _npcIdCounter = 0x10000000; // Start at high range
		private static uint GenerateNpcId()
		{
			return Interlocked.Increment(ref _npcIdCounter);
		}
	}
}
