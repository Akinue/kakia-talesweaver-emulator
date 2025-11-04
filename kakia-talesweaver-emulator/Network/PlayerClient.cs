using kakia_talesweaver_logging;
using kakia_talesweaver_network;
using kakia_talesweaver_packets.Packets;

namespace kakia_talesweaver_emulator.Network;

public class PlayerClient
{
	private SocketClient _socketClient;

	public PlayerClient(SocketClient socketClient)
	{
		_socketClient = socketClient;
		_socketClient.PacketReceived += PacketRecieved;
		Logger.Log($"Player connected: {_socketClient.GetIP()}", LogLevel.Information);

		Send(new ConnectedPacket().ToBytes(), CancellationToken.None).Wait();
		_ = _socketClient.BeginRead();
	}


	public async Task PacketRecieved(RawPacket packet)
	{
		/*
		PacketHandler handler = kakia_lime_odyssey_network.Handler.PacketHandlers.GetHandlerFor(packet.PacketId);
		//Logger.Log($"Recieved [{packet.PacketId}]", LogLevel.Debug);
		if (handler != null)
		{
			try
			{
				handler.HandlePacket(this, packet);
				return;
			}
			catch (Exception e)
			{
				Logger.Log(e);
			}
		}
		else
		{
			Logger.Log($"NOT IMPLEMENTED [{packet.PacketId}]", LogLevel.Warning);
			//Logger.LogPck(packet.Payload);
		}
		*/

		Logger.Log($"Recieved packet of length {packet.Data.Length}", LogLevel.Debug);
	}

	public async Task<bool> Send(byte[] packet, CancellationToken token)
	{
		//Logger.Log($"Sending [{((PacketType)BitConverter.ToUInt16(packet, 0))}]", LogLevel.Debug);
		await _socketClient!.Send(packet);
		return true;
	}
}
