using kakia_talesweaver_logging;
using kakia_talesweaver_network.Cryptography;
using kakia_talesweaver_utils;
using kakia_talesweaver_utils.Extensions;
using System.Net;
using System.Net.Sockets;

namespace kakia_talesweaver_network;

public class SocketClient
{
	public Func<RawPacket, Task>? PacketReceived;

	public int Id { get; set; }
	public bool IsAlive { get; set; }
	private Socket _socket { get; set; }
	private byte[] _buffer { get; set; }
	private int _position = 0;
	private CryptoHandler _cryptoHandler = new();

	public SocketClient(Socket socket)
	{
		IsAlive = true;
		_socket = socket;
		_buffer = new byte[1024 * 10];
		Id = socket.Handle.ToInt32();
	}

	public async Task BeginRead()
	{
		try
		{
			var seg = new ArraySegment<byte>(_buffer, _position, _buffer.Length - _position);
			var len = await _socket.ReceiveAsync(seg);
			if (len <= 0)
			{
				IsAlive = false;
				Logger.Log($"{GetIP()} disconnected.");
				return;
			}
			await HandleData(len);
		}
		catch (Exception ex)
		{
			Logger.Log(ex);
			IsAlive = false;
		}
	}

	private async Task HandleData(int len)
	{
		try
		{			
			var packets = RawPacket.ParsePackets(_buffer[_position..len], _cryptoHandler);
			foreach (var packet in packets)
				await PacketReceived!.Invoke(packet);
			
		}
		catch (Exception ex)
		{
			Logger.Log(ex);
		}
		await BeginRead();
	}

	public async Task Send(byte[] packet)
	{
		if (_cryptoHandler.IsEncrypted)
			packet = _cryptoHandler.Encrypt(packet)!;

		using PacketWriter pw = new();
		pw.WritePacket(packet);

		var temp = pw.ToArray();
		Logger.Log($"Sending {temp.Length} bytes to {GetIP()}:{Environment.NewLine}{temp.ToFormatedHexString()}");

		await _socket.SendAsync(pw.ToArray());
	}

	public string GetIP()
	{
		if (!_socket.Connected)
			return string.Empty;
		var ip = _socket.RemoteEndPoint as IPEndPoint;
		return ip.Address.ToString();
	}
}

