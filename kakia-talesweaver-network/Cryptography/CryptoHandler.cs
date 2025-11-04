using kakia_talesweaver_logging;

namespace kakia_talesweaver_network.Cryptography;

public class CryptoHandler
{
	public uint Seed { get; set; }
	public byte[] Key { get; set; }
	public int SendIndex { get; set; } = 0;
	public bool IsEncrypted { get; set; } = false;

	public CryptoHandler()
	{
		Seed = (uint)Random.Shared.Next();

		try
		{
			Key = TsCrypto.GenKey(Seed);
		}
		catch (Exception ex)
		{
			Logger.Log(ex);
		}
	}

	public byte[]? Decrypt(byte[] Packet)
	{
		if (!IsEncrypted)
			return Packet;
		return TsCrypto.Decrypt(Key, Packet);
	}

	public byte[]? Encrypt(byte[] Packet)
	{
		if (!IsEncrypted)
			return Packet;
		return TsCrypto.Encrypt(Key, Packet, SendIndex);
	}
}
