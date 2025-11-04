namespace kakia_talesweaver_network;

public class RawPacket
{
	public byte[] Data { get; }
	public RawPacket(byte[] data)
	{
		Data = data;
	}
}
