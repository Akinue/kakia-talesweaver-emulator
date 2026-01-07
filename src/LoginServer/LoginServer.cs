using Kakia.TW.Login.Database;
using Kakia.TW.Login.Network;
using Kakia.TW.Shared;
using Yggdrasil.Logging;
using Yggdrasil.Network.TCP;
using Yggdrasil.Util;
using Yggdrasil.Util.Commands;

namespace Kakia.TW.Login
{
	/// <summary>
	/// Represents the login server.
	/// </summary>
	public class LoginServer : Server
	{
		/// <summary>
		/// Global singleton for the login server.
		/// </summary>
		public static readonly LoginServer Instance = new();

		private readonly List<TcpConnectionAcceptor<LoginConnection>> _acceptors = new();

		/// <summary>
		/// Returns a reference to the server's packet handlers.
		/// </summary>
		public PacketHandler PacketHandler { get; } = new PacketHandler();

		/// <summary>
		/// Returns reference to the server's database interface.
		/// </summary>
		public LoginDb Database { get; } = new LoginDb();

		/// <summary>
		/// Starts the server.
		/// </summary>
		/// <param name="args"></param>
		public override void Run(string[] args)
		{
			ConsoleUtil.WriteHeader(nameof(Kakia), "Login", ConsoleColor.DarkYellow, ConsoleHeader.Title, ConsoleHeader.Subtitle);
			ConsoleUtil.LoadingTitle();

			this.NavigateToRoot();
			this.LoadConf();
			this.LoadLocalization(this.Conf);
			this.InitDatabase(Database, this.Conf);
			CheckDatabaseUpdates();

			foreach (var port in this.Conf.Login.BindPorts)
			{
				var acceptor = new TcpConnectionAcceptor<LoginConnection>(this.Conf.Login.BindIp, port);
				acceptor.ConnectionAccepted += OnConnectionAccepted;
				acceptor.Listen();

				_acceptors.Add(acceptor);

				Log.Status("Server ready, listening on {0}.", acceptor.Address);
			}

			ConsoleUtil.RunningTitle();
			new ConsoleCommands().Wait();
		}

		/// <summary>
		/// Called when a new connection to the server was established
		/// by a client.
		/// </summary>
		/// <param name="conn"></param>
		private void OnConnectionAccepted(LoginConnection conn)
		{
			Log.Info("New connection accepted from '{0}'.", conn.Address);

			// Initialize the codec (Generates the Seed and Key)
			conn.Framer.Codec.Initialize();

			// Disable encryption for the handshake packet so the client can read the seed
			conn.Framer.EncryptOutput = false;

			Send.Connected(conn);
		}

		/// <summary>
		/// Checks for potential updates for the database.
		/// </summary>
		private void CheckDatabaseUpdates()
		{
			Log.Info("Checking for updates...");

			// We had an issue with our update names, and to ensure that we
			// don't break everyone's update history, we'll temporarily fix
			// the update names on the fly. This should be removed at some
			// point in the future.
			//this.Database.NormalizeUpdateNames();

			var enumOptions = new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive };
			if (!Directory.Exists("sql/updates/")) return;

			var filePaths = Directory.GetFiles("sql/updates/", "*.sql", enumOptions).OrderBy(a => a);

			foreach (var filePath in filePaths)
			{
				var updateName = Path.GetFileName(filePath);
				var normalizedName = updateName.ToLower().Replace("update-", "update_");

				if (Database.CheckUpdate(normalizedName))
					continue;

				Log.Info("Update '{0}' found, executing...", updateName);
				Database.RunUpdate(normalizedName, File.ReadAllText(filePath));
			}
		}
	}
}
