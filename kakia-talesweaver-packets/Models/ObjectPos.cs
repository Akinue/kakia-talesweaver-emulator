using kakia_talesweaver_utils;

namespace kakia_talesweaver_packets.Models;

public class ObjectPos
{
	public TsPoint Position { get; set; } = new TsPoint();
	public byte Direction { get; set; }

	public byte[] ToBytes()
	{
		using PacketWriter pw = new();
		pw.Write(Position.ToBytes());
		pw.Write(Direction);
		return pw.ToArray();
	}
}
