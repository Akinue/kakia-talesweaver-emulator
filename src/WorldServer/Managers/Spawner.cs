using Kakia.TW.Shared.World;
using Kakia.TW.World.Entities;
using System;

namespace Kakia.TW.World.Managers
{
	public class Spawner : IUpdateable
	{
		private readonly Map _map;
		private readonly string _mobName;
		private readonly uint _mobModelId;
		private readonly ushort _x;
		private readonly ushort _y;
		private readonly byte _direction;
		private readonly int _respawnTimeMs;
		private readonly uint _maxHp;

		private Monster? _currentMob;
		private DateTime _deadTime;
		private bool _isRespawning;
		private bool _stopped;

		public Spawner(Map map, string name, uint modelId, ushort x, ushort y, int respawnTimeMs)
			: this(map, modelId, name, x, y, 0, respawnTimeMs, 100)
		{
		}

		public Spawner(Map map, uint modelId, string name, ushort x, ushort y, byte direction, int respawnTimeSec, uint maxHp)
		{
			_map = map;
			_mobName = name;
			_mobModelId = modelId;
			_x = x;
			_y = y;
			_direction = direction;
			_respawnTimeMs = respawnTimeSec * 1000;
			_maxHp = maxHp;

			Spawn();
		}

		public void Update(TimeSpan elapsed)
		{
			if (_stopped) return;

			// Check if we need to respawn
			if (_isRespawning && _currentMob == null)
			{
				if ((DateTime.Now - _deadTime).TotalMilliseconds >= _respawnTimeMs)
				{
					Spawn();
				}
			}
		}

		private void Spawn()
		{
			_isRespawning = false;

			uint id = WorldServer.Instance.World.GetNextEntityId();
			_currentMob = new Monster(id, _mobName, _mobModelId)
			{
				ObjectPos = new ObjectPos
				{
					Position = new WorldPosition(_map.MapId, _map.ZoneId, _x, _y),
					Direction = _direction
				},
				Direction = _direction,
				MaxHP = _maxHp,
				CurrentHP = _maxHp,
				Instance = _map,
				SourceSpawner = this
			};

			_map.AddMonster(_currentMob);
		}

		public void OnMonsterDied()
		{
			_currentMob = null;
			_isRespawning = true;
			_deadTime = DateTime.Now;
		}

		public void Stop()
		{
			_stopped = true;
			if (_currentMob != null)
			{
				_map.RemoveMonster(_currentMob.Id);
				_currentMob = null;
			}
		}
	}
}