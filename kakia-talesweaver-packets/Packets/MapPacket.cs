using kakia_talesweaver_utils;

namespace kakia_talesweaver_packets.Packets;

public class MapPacket
{
	public ushort MapId { get; set; }
	public ushort ZoneId { get; set; }
	public ushort MapType { get; set; }

    public bool HasWeatherEffects
    {
        get => (Attributes & 0b0000_0001) != 0;
        set
        {
            if (value)
                Attributes |= 0b0000_0001;
            else
                Attributes &= 0b1111_1110;
        }
	}

    public bool PvPEnabled
    {
        get => (Attributes & 0b0000_0010) != 0;
        set
        {
            if (value)
                Attributes |= 0b0000_0010;
            else
                Attributes &= 0b1111_1101;
        }
    }

	/**
     * A bitmask field combining several boolean properties of the map.
     * For example:
     * - Bit 0: Has weather effects
     * - Bit 1: Is a Player-vs-Player (PK) enabled area
     * - etc.
     */
	private byte Attributes { get; set; }


    public byte[] ToBytes()
    {
        using PacketWriter pw = new();
        pw.Write((byte)0x15); // Packet ID for MapPacket
        pw.Write(MapId);
        pw.Write(ZoneId);
        pw.Write(MapType);
        pw.Write(Attributes);
        return pw.ToArray();
	}

    public static MapPacket FromBytes(byte[] data)
    {
        using PacketReader pr = new(data);
        byte packetType = pr.ReadByte();
        if (packetType != 0x15)
            throw new InvalidDataException("Invalid packet type for MapPacket");
        var packet = new MapPacket
        {
            MapId = pr.ReadUInt16BE(),
            ZoneId = pr.ReadUInt16BE(),
            MapType = pr.ReadUInt16BE(),
            Attributes = pr.ReadByte()
        };
        return packet;
	}
}