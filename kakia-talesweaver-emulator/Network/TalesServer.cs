using kakia_talesweaver_network;
using System.Collections.Concurrent;

namespace kakia_talesweaver_emulator.Network;

public class TalesServer : SocketServer
{
	public ConcurrentBag<PlayerClient> ConnectedPlayers { get; } = new();

	public TalesServer(string host, int port) : base(host, port)
	{

	}


	public override void OnConnect(SocketClient s)
	{
		var pc = new PlayerClient(s);
		ConnectedPlayers.Add(pc);
	}
}
