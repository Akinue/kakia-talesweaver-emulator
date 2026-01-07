// Auto-generated from EntitySpawnPacket binary files
// Generated: 2026-01-05 16:23:51

using Kakia.TW.World.Scripting;

namespace Kakia.TW.World.Scripts.Warps
{
    public class Map3_Zone24320_Warps : NpcScript
    {
        public override void Load()
        {
            // ObjectId: 64367
            SpawnWarp(
                mapId: 3,
                zoneId: 24320,
                x: 40960,
                y: 54272,
                destMapId: 42752,
                destZoneId: 1,
                destX: 100,
                destY: 100
            );

            // ObjectId: 64431
            SpawnWarp(
                mapId: 3,
                zoneId: 24320,
                x: 24577,
                y: 48128,
                destMapId: 26369,
                destZoneId: 1,
                destX: 100,
                destY: 100
            );

        }
    }
}
