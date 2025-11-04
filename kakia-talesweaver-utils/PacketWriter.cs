namespace kakia_talesweaver_utils;

public class PacketWriter : BinaryWriter
{
	public PacketWriter() : base(new MemoryStream())
	{
	}

	public void WritePacket(byte[] packet)
	{
		ushort len = (ushort)packet.Length;
		Write((byte)0xAA);
		Write(len);
		Write(packet);
	}

	public byte[] ToArray()
	{
		return ((MemoryStream)BaseStream).ToArray();
	}

	#region Deal with endianness
	public override void Write(short value)
	{
		var bytes = BitConverter.GetBytes(value);
		if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
		base.Write(bytes);
	}

	public override void Write(ushort value)
	{
		var bytes = BitConverter.GetBytes(value);
		if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
		base.Write(bytes);
	}

	public override void Write(int value)
	{
		var bytes = BitConverter.GetBytes(value);
		if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
		base.Write(bytes);
	}

	public override void Write(uint value)
	{
		var bytes = BitConverter.GetBytes(value);
		if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
		base.Write(bytes);
	}

	public override void Write(long value)
	{
		var bytes = BitConverter.GetBytes(value);
		if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
		base.Write(bytes);
	}

	public override void Write(ulong value)
	{
		var bytes = BitConverter.GetBytes(value);
		if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
		base.Write(bytes);
	}

	public override void Write(float value) => Write(BitConverter.SingleToInt32Bits(value));
	public override void Write(double value) => Write(BitConverter.DoubleToInt64Bits(value));
	#endregion
}
