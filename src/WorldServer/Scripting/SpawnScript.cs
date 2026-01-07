using Kakia.TW.Shared.World;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Managers;
using Yggdrasil.Logging;
using Yggdrasil.Scripting;

namespace Kakia.TW.World.Scripting
{
	/// <summary>
	/// Base class for monster/creature spawn scripts.
	/// </summary>
	public abstract class SpawnScript : IScript, IDisposable
	{
		private readonly List<Spawner> _spawners = new();

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
			foreach (var spawner in _spawners)
			{
				WorldServer.Instance.World.Heartbeat.Remove(spawner);
				spawner.Stop();
			}
			_spawners.Clear();
		}

		/// <summary>
		/// Called when the script is being initialized. Override to define spawns.
		/// </summary>
		public abstract void Load();

		/// <summary>
		/// Creates a monster spawner at the specified location.
		/// </summary>
		/// <param name="modelId">Monster model/sprite ID.</param>
		/// <param name="name">Monster display name.</param>
		/// <param name="mapId">Map ID.</param>
		/// <param name="zoneId">Zone ID.</param>
		/// <param name="x">X coordinate.</param>
		/// <param name="y">Y coordinate.</param>
		/// <param name="direction">Facing direction.</param>
		/// <param name="respawnTime">Time in seconds before respawn after death.</param>
		/// <param name="maxHp">Monster max HP.</param>
		/// <returns>The created spawner.</returns>
		protected Spawner AddSpawner(uint modelId, string name, ushort mapId, ushort zoneId, ushort x, ushort y, byte direction = 0, int respawnTime = 30, uint maxHp = 100)
		{
			var map = WorldServer.Instance.World.Maps.GetOrCreateMap(mapId, zoneId);
			if (map == null)
			{
				Log.Warning($"SpawnScript: Cannot create spawner - Map {mapId}-{zoneId} not found.");
				return null;
			}

			var spawner = new Spawner(map, modelId, name, x, y, direction, respawnTime, maxHp);
			_spawners.Add(spawner);

			// Register with heartbeat for respawn timing
			WorldServer.Instance.World.Heartbeat.Add(spawner);

			Log.Debug($"Created spawner for '{name}' (Model: {modelId}) at {mapId}-{zoneId} ({x},{y}), respawn: {respawnTime}s");

			return spawner;
		}

		/// <summary>
		/// Spawns a monster immediately (without using a spawner for respawns).
		/// </summary>
		protected Monster SpawnMonster(uint modelId, string name, ushort mapId, ushort zoneId, ushort x, ushort y, byte direction = 0, uint maxHp = 100)
		{
			var map = WorldServer.Instance.World.Maps.GetOrCreateMap(mapId, zoneId);
			if (map == null)
			{
				Log.Warning($"SpawnScript: Cannot spawn monster - Map {mapId}-{zoneId} not found.");
				return null;
			}

			uint mobId = GenerateMobId();

			var monster = new Monster(mobId, name, modelId)
			{
				Direction = direction,
				MaxHP = maxHp,
				CurrentHP = maxHp,
				ObjectPos = new ObjectPos
				{
					Position = new WorldPosition(mapId, zoneId, x, y),
					Direction = direction
				}
			};

			map.AddMonster(monster);

			Log.Debug($"Spawned monster '{name}' (ID: {mobId}, Model: {modelId}) at {mapId}-{zoneId} ({x},{y})");

			return monster;
		}

		private static uint _mobIdCounter = 0x20000000; // Different range than NPCs
		private static uint GenerateMobId()
		{
			return Interlocked.Increment(ref _mobIdCounter);
		}
	}
}
