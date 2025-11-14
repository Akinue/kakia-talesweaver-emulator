using kakia_talesweaver_packets.Packets;

namespace kakia_talesweaver_packets.Models;

public class MapInfo
{
	private readonly MapPacket _mapPacket;
	public Dictionary<byte, List<byte[]>> Entities = new();
	public List<ObjectPos> SpawnPoints = [];

	public ushort MapId => _mapPacket.MapId;
	public ushort ZoneId => _mapPacket.ZoneId;

	public MapInfo(string path)
	{
		_mapPacket = MapPacket.FromBytes(
			File.ReadAllBytes(
				Path.Combine(path, "map.bin")
			)
		);

		string[] pointsCsv = File.ReadAllLines(Path.Combine(path, "SpawnPos.txt"));
		foreach (var point in pointsCsv)
		{
			var parts = point.Split(',');

			var pos = new ObjectPos
			{
				Position = new TsPoint
				{
					X = ushort.Parse(parts[2]),
					Y = ushort.Parse(parts[3])
				},
				Direction = byte.Parse(parts[4])
			};

			if (SpawnPoints.Any(m =>
				m.Position.X == pos.Position.X &&
				m.Position.Y == pos.Position.Y &&
				m.Direction == pos.Direction))
				continue;

			SpawnPoints.Add(pos);
		}

		if (Directory.Exists(Path.Combine(path, "Spawn")))
		{

			string[] spawns = Directory.GetFiles(
				Path.Combine(path, "Spawn"),
				"*.bin"
			);

			foreach (var spawn in spawns)
			{
				var entityPacket = File.ReadAllBytes(spawn);
				if (!Entities.ContainsKey(entityPacket[2]))
				{
					Entities[entityPacket[2]] = [];
				}

				Entities[entityPacket[2]].Add(entityPacket);
			}
		}
	}

	public MapPacket GetMapPacket()
	{
		return _mapPacket;
	}
}
