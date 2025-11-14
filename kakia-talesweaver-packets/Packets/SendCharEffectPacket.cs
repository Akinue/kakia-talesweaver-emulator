using kakia_talesweaver_utils;
using System.Net.Sockets;

namespace kakia_talesweaver_packets.Packets;

public class SendCharEffectPacket
{
	public uint ObjectId { get; set; }
	public CharEffect Effect { get; set; }

	public byte[] ToBytes()
	{
		using PacketWriter pw = new();
		pw.Write((byte)0x1A);
		pw.Write(ObjectId);
		pw.Write((byte)Effect);
		return pw.ToArray();
	}
}


public enum CharEffect: byte
{
	None,
	Unknown1,
	Unknown2,
	Unknown3,
	Teleport
}