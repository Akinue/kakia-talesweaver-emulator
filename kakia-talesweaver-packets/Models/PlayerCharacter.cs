using kakia_talesweaver_packets.Packets;

namespace kakia_talesweaver_packets.Models;
public class PlayerCharacter
{
	public uint Id { get; set; }
	public string Name { get; set; }
	public TsPoint Position { get; set; } = new TsPoint();
	public byte Direction { get; set; }


	public SpawnCharacterPacket SpawnCharacterPacket;
}
