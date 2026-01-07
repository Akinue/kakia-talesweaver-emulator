using Kakia.TW.Shared.World;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Managers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kakia.TW.World.Scripting
{
	public static partial class Shortcuts
	{
		public static void AddSpawner(string mapKey, string mobName, uint modelId, int count, ushort x, ushort y, int respawnMs)
		{
			// MapKey parsing logic (string -> ID)
			// For now assuming mapKey is "1_1" style or we look up ID
			var map = WorldServer.Instance.World.Maps.GetMap(1, 1);

			if (map != null)
			{
				for (int i = 0; i < count; i++)
				{
					// Scatter logic could go here (x + rand, y + rand)
					var spawner = new Spawner(map, mobName, modelId, x, y, respawnMs);
					map.AddSpawner(spawner);
				}
			}
		}

		public static void AddWarp(string mapKey, ushort x, ushort y, ushort destMapId, ushort destX, ushort destY)
		{
			var map = WorldServer.Instance.World.Maps.GetMap(1, 1);
			if (map != null)
			{
				uint id = WorldServer.Instance.World.GetNextEntityId();
				var warp = new Warp(id)
				{
					ObjectPos = new ObjectPos { Position = new WorldPosition(destMapId, x, y) },
					DestMapId = destMapId,
					DestZoneId = 1, // Default zone usually
					DestX = destX,
					DestY = destY
				};
				map.Enter(warp);
			}
		}
	}
}
