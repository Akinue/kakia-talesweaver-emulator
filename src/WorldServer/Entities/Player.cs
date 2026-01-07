using Kakia.TW.Shared.World;
using Kakia.TW.World;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Managers;
using Kakia.TW.World.Network;

namespace Kakia.TW.World.Entities
{
	public class Player : Entity
	{
		public WorldConnection Connection { get; }
		public WorldCharacter Data { get; }

		public Player(WorldConnection conn, WorldCharacter data)
		{
			Connection = conn;
			Data = data;
			Position = new Position(data.X, data.Y);
			Direction = data.Direction;
		}

		public override void Update(TimeSpan elapsed)
		{
			// Regen HP/MP, handle buffs
		}

		public void Warp(ushort mapId, ushort zoneId, ushort x, ushort y)
		{
			// 1. Remove from current map
			Instance?.Leave(this);

			// 2. Get new map
			var newMap = WorldServer.Instance.World.Maps.GetMap(mapId, zoneId);
			if (newMap == null) return; // Error handling

			// 3. Update Position
			Data.MapId = mapId;
			Data.ZoneId = zoneId;
			Data.X = x;
			Data.Y = y;
			Position = new Position(x, y);

			// 4. Enter new map (This triggers MapChange packet and Spawns)
			newMap.Enter(this);

			// 5. Save location to DB
			WorldServer.Instance.Database.SaveCharacterPosition(this.Data);
		}

		public void Save()
		{
			WorldServer.Instance.Database.SaveCharacterFull(this.Data);
		}
	}
}