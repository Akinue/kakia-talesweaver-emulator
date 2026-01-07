// Auto-generated from EntitySpawnPacket binary files
// Generated: 2026-01-05 16:23:51

using Kakia.TW.World.Scripting;

namespace Kakia.TW.World.Scripts.Warps
{
    public class Map3_Zone16640_Warps : NpcScript
    {
        public override void Load()
        {
            // ObjectId: 113987
            SpawnWarp(
                mapId: 3,
                zoneId: 16640,
                x: 29696,
                y: 26624,
                destMapId: 31488,
                destZoneId: 1,
                destX: 100,
                destY: 100
            );

            // ObjectId: 114115
            SpawnWarp(
                mapId: 3,
                zoneId: 16640,
                x: 9217,
                y: 62464,
                destMapId: 11009,
                destZoneId: 1,
                destX: 100,
                destY: 100
            );

        }
    }
}
