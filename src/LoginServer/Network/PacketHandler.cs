using Kakia.TW.Shared.Network;
using Yggdrasil.Logging;
using Yggdrasil.Security.Hashing;

namespace Kakia.TW.Login.Network
{
	/// <summary>
	/// Packet handler methods.
	/// </summary>
	public class PacketHandler : PacketHandler<LoginConnection>
	{
		[PacketHandler(Op.Handshake)]
		public void Handshake(LoginConnection conn, Packet packet)
		{
			// Send Server List (Loaded from Config in a real scenario, hardcoded for now)
			Send.ServerList(conn, [new ServerInfo {
				Id = 26,
				IP = System.Net.IPAddress.Parse(LoginServer.Instance.Conf.Login.ServerIp),
				Port = (ushort)LoginServer.Instance.Conf.Login.BindPorts[0],
				Name = "Kakia Tales",
				Load = 0
			}]);

			Send.Handshake(conn, "Welcome to Kakia TalesWeaver!");
			conn.Framer.IsCryptoSet = true;
			conn.Framer.EncryptOutput = true;
		}

		[PacketHandler(Op.InitConnectRequest)]
		public void InitConnectRequest(LoginConnection conn, Packet packet)
		{
			// This is just an ACK. We don't need to do anything specific logic-wise,
			// but we must acknowledge the packet exists so we don't crash.
			Log.Debug("Client Initialized Connection (Handshake ACK).");
		}

		[PacketHandler(Op.Heartbeat)]
		public void Heartbeat(LoginConnection conn, Packet packet)
		{
			// Client sends this periodically - just consume to prevent log spam.
		}


		/// <summary>
		/// Login request (0x66).
		/// </summary>
		[PacketHandler(Op.LoginRequest)]
		public void Login(LoginConnection conn, Packet packet)
		{
			packet.GetByte(); // Skip 0x00

			string username = packet.GetString(packet.GetByte());
			string password = packet.GetString(packet.GetByte());

			var db = LoginServer.Instance.Database;
			var account = db.GetAccountByUsername(username);

			// 1. Account Auto-Creation Logic
			if (account == null)
			{
				var allowAutoRegister = LoginServer.Instance.Conf.Login.AllowAccountCreation;

				// Create new account
				var hasSuffix = username.StartsWith("new//", StringComparison.OrdinalIgnoreCase) ||
								 username.StartsWith("new__", StringComparison.OrdinalIgnoreCase);

				if (!allowAutoRegister || !hasSuffix)
				{
					Log.Info($"Login failed: Account '{username}' not found.");
					// Send.LoginFailed(conn, "User not found."); 
					conn.Close();
					return;
				}

				// Strip suffix to get actual username
				string realUsername = username["new__".Length..];

				if (db.UsernameExists(realUsername))
				{
					Log.Info($"Registration failed: Username '{realUsername}' already exists.");
					conn.Close();
					return;
				}

				// Hash the password
				string hashedPassword = BCrypt.HashPassword(password, BCrypt.GenerateSalt());

				// Create the account
				account = db.CreateAccount(realUsername, hashedPassword, 0);
				Log.Info($"New account created: {realUsername}");
			}

			// 2. Verification
			if (account.IsBanned)
			{
				Log.Info($"Banned user '{account.Username}' attempted login.");
				conn.Close();
				return;
			}

			// Verify Password using BCrypt
			var validPassword = false;
			try
			{
				validPassword = BCrypt.CheckPassword(password, account.Password);
			}
			catch
			{
				// Handle legacy passwords or bad hashes
				validPassword = false;
			}

			if (!validPassword)
			{
				Log.Info($"Login failed: Incorrect password for '{account.Username}'.");
				// Send.LoginFailed(conn, "Incorrect password.");
				conn.Close();
				return;
			}

			// 3. Success Logic
			db.UpdateSessionId(ref account);

			conn.Username = account.Username;
			conn.IsAuthenticated = true;

			Log.Info($"User '{account.Username}' logged in (ID: {account.Id}).");

			Send.LoginSuccess(conn, [new AccountInfo
			{
				ServerId = 26,
				Username = account.Username,
				Unknown = 0,
				CreatedAt = account.CreatedAt,
				LastLogin = account.LastLogin,
				CharacterCount = db.GetCharacterCount(account.Id)
			}]);
		}

		/// <summary>
		/// Server Select / World Transfer (0x67).
		/// </summary>
		[PacketHandler(Op.ServerSelectRequest)]
		public void ServerSelect(LoginConnection conn, Packet packet)
		{
			byte serverId = packet.GetByte();
			byte channelId = packet.GetByte();
			string reqUser = packet.GetString(packet.GetByte());

			if (reqUser != conn.Username || !conn.IsAuthenticated)
			{
				conn.Close();
				return;
			}

			Log.Info($"Account '{conn.Username}' selected server {serverId}.");

			// Redirect to Lobby Server
			string lobbyIp = LoginServer.Instance.Conf.Lobby.ServerIp;
			int lobbyPort = LoginServer.Instance.Conf.Lobby.BindPort;

			Send.ServerSelectRedirect(conn, lobbyIp, lobbyPort, 0x2D);
		}
	}
}
