using Kakia.TW.Lobby.Database;
using Kakia.TW.Lobby.Network;
using Kakia.TW.Shared;
using Yggdrasil.Logging;
using Yggdrasil.Network.TCP;
using Yggdrasil.Util;
using Yggdrasil.Util.Commands;

namespace Kakia.TW.Lobby
{
	/// <summary>
	/// Represents the auth server.
	/// </summary>
	public class LobbyServer : Server
	{
		/// <summary>
		/// Global singleton for the auth server.
		/// </summary>
		public static readonly LobbyServer Instance = new();

		private readonly List<TcpConnectionAcceptor<LobbyConnection>> _acceptors = new();

		/// <summary>
		/// Returns a reference to the server's packet handlers.
		/// </summary>
		public PacketHandler PacketHandler { get; } = new PacketHandler();

		/// <summary>
		/// Returns reference to the server's database interface.
		/// </summary>
		public LobbyDb Database { get; } = new LobbyDb();

		/// <summary>
		/// Starts the server.
		/// </summary>
		/// <param name="args"></param>
		public override void Run(string[] args)
		{
			ConsoleUtil.WriteHeader(nameof(Kakia), "Lobby", ConsoleColor.DarkYellow, ConsoleHeader.Title, ConsoleHeader.Subtitle);
			ConsoleUtil.LoadingTitle();

			this.NavigateToRoot();
			this.LoadConf();
			this.LoadLocalization(this.Conf);
			this.InitDatabase(Database, this.Conf);

			var acceptor = new TcpConnectionAcceptor<LobbyConnection>(this.Conf.Lobby.BindIp, this.Conf.Lobby.BindPort);
			acceptor.ConnectionAccepted += OnConnectionAccepted;
			acceptor.Listen();

			_acceptors.Add(acceptor);

			Log.Status("Server ready, listening on {0}.", acceptor.Address);

			ConsoleUtil.RunningTitle();
			new ConsoleCommands().Wait();
		}

		/// <summary>
		/// Called when a new connection to the server was established
		/// by a client.
		/// </summary>
		/// <param name="conn"></param>
		private void OnConnectionAccepted(LobbyConnection conn)
		{
			Log.Info("New connection accepted from '{0}'.", conn.Address);
		}
	}
}
