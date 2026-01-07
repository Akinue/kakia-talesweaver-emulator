using Kakia.TW.Shared.Network;
using Kakia.TW.Shared.Network.Crypto;
using Kakia.TW.Shared.Network.Helpers;
using Kakia.TW.Shared.World;
using Yggdrasil.Logging;

namespace Kakia.TW.Lobby.Network
{
	/// <summary>
	/// Packet handler methods.
	/// </summary>
	public class PacketHandler : PacketHandler<LobbyConnection>
	{
		[PacketHandler(Op.Handshake)]
		public void Handshake(LobbyConnection conn, Packet packet)
		{
			// Standard Handshake
			Send.Handshake(conn);
			conn.Framer.IsCryptoSet = true;
			conn.Framer.EncryptOutput = true;
		}

		[PacketHandler(Op.InitConnectRequest)]
		public void InitConnectRequest(LobbyConnection conn, Packet packet)
		{
			// Client sends this after connecting post-redirect, before ReconnectRequest.
			// Just acknowledge it exists to prevent spam/crash.
			Log.Debug("Lobby: Client sent InitConnectRequest (pre-reconnect handshake).");
		}

		[PacketHandler(Op.LoginRequest)]
		public void LoginRequest(LobbyConnection conn, Packet packet)
		{
			packet.GetByte(); // Skip
			string username = packet.GetString(packet.GetByte());

			// 1. Verify Session/User via Database
			int accountId = LobbyServer.Instance.Database.VerifySession(username);

			if (accountId <= 0)
			{
				Log.Warning($"Lobby: Invalid session for '{username}'");
				conn.Close();
				return;
			}

			conn.Username = username;
			conn.Account = LobbyServer.Instance.Database.GetAccountById(accountId);

			// 2. Fetch Character List
			var characters = LobbyServer.Instance.Database.GetCharacterSummaries(accountId);

			Log.Info($"Lobby: '{username}' connected with {characters.Count} characters.");
			Send.CharacterList(conn, characters);
		}

		/// <summary>
		/// Handles reconnection from Login Server (Redirect).
		/// </summary>
		[PacketHandler(Op.ReconnectRequest)]
		public void ClientReconnectRequest(LobbyConnection conn, Packet packet)
		{
			// Packet Structure: [Op:1] [Seed:4] [HWID_Len:1] [HWID:String] [User_Len:1] [Username:String] ...
			uint seed = packet.GetUInt();
			string hwid = packet.GetString(packet.GetByte());
			string id = packet.GetString(packet.GetByte());
			string nexonId = packet.GetString(packet.GetByte());
			string username = packet.GetString(packet.GetByte());

			Log.Info($"Lobby: Client reconnected. User: {username}, Seed: {seed:X8}");

			// Verify user via DB (gets Account ID etc)
			int accountId = LobbyServer.Instance.Database.VerifySession(username);
			if (accountId <= 0)
			{
				Log.Warning($"Lobby: Reconnect failed. Invalid session for '{username}'");
				conn.Close();
				return;
			}

			conn.Username = username;
			conn.Account = LobbyServer.Instance.Database.GetAccountById(accountId);
			conn.Seed = seed;

			conn.Framer.Codec.Initialize(seed);
			conn.Framer.IsCryptoSet = true;
			conn.Framer.EncryptOutput = true;

			// Send the required sequence to initialize the Lobby state on client
			Send.ReconnectSequence(conn);

			// Send Character Select List (0x6B)
			var characters = LobbyServer.Instance.Database.GetCharacterList(accountId);
			Send.CharacterSelectList(conn, characters);
		}

		[PacketHandler(Op.Acknowledge)]
		public void PingRequest(LobbyConnection conn, Packet packet)
		{
			// Keep-alive from client.
			// We can optionally respond Op.Ping (0x02) back to keep connection healthy.
		}

		[PacketHandler(Op.Heartbeat)]
		public void Heartbeat(LobbyConnection conn, Packet packet)
		{
			// Client sends this periodically - just consume to prevent log spam.
		}

		[PacketHandler(Op.CreateCharacterRequest)]
		public void CreateCharacterRequest(LobbyConnection conn, Packet packet)
		{
			if (conn.Account == null) return;

			// Actual Structure from Log: [Op] [ModelID:4] [NameLen:1] [Name:String]
			uint modelId = packet.GetUInt();
			byte nameLen = packet.GetByte();
			string charName = packet.GetString(nameLen);

			// Client doesn't send appearance block here, implies default.
			var appearance = new CharacterAppearance();

			// ModelID usually maps to CharType. 
			// Assuming strict mapping for now or passing ModelID as CharType.
			int charType = (int)modelId;

			bool success = LobbyServer.Instance.Database.CreateCharacter(conn.Account.Id, charName, charType, appearance);

			if (success)
			{
				Log.Info($"Created character '{charName}' (Model: {modelId}) for account {conn.Account.Id}.");

				Send.CharacterCreationSuccess(conn);

				// Send 0x02 Ack (Loading Done?)
				Send.Ack(conn);

				// Refresh Character Select List (0x6B)
				var characters = LobbyServer.Instance.Database.GetCharacterList(conn.Account.Id);
				Send.CharacterSelectList(conn, characters);
			}
			else
			{
				// Send.SystemMessage(conn, "Failed to create character.");
			}
		}

		[PacketHandler(Op.SelectCharacterRequest)]
		public void SelectCharacterRequest(LobbyConnection conn, Packet packet)
		{
			var charName = packet.GetLpString();
			Log.Info($"User selected character: {charName}. Requesting Security Code.");

			conn.SelectedCharacterName = charName;
			Send.LoginSecurity(conn, CharLoginSecurityType.RequestSecurityCode);
		}

		[PacketHandler(Op.SecurityCodeRequest)]
		public void InputSecurityCodeRequest(LobbyConnection conn, Packet packet)
		{
			var code = (ClientResponseCode)packet.GetByte();

			// If it's just a status update (like LoadingDone 0x05), ignore it.
			if (code != ClientResponseCode.WithMessage)
			{
				Log.Debug($"Client sent status {code} (0x{(byte)code:X2}). Ignoring.");
				return;
			}

			// If code is WithMessage (0x01), the actual security code string follows
			string message = packet.GetLpString();

			Log.Info($"Security Code Received: {message}");

			// TODO: Verify 'message' against secondary password here.

			// Store selected character name for WorldServer handover
			if (!string.IsNullOrEmpty(conn.SelectedCharacterName))
			{
				LobbyServer.Instance.Database.SetSelectedCharacter(conn.Account.Id, conn.SelectedCharacterName);
			}

			// Update session ID for World Server handover
			var account = conn.Account;
			LobbyServer.Instance.Database.UpdateSessionId(ref account);

			Log.Info($"Security check passed. Character: {conn.SelectedCharacterName}. Redirecting to World with Seed: {account.SessionId:X8}");

			// Redirect to World Server
			string worldIp = LobbyServer.Instance.Conf.World.ServerIp;
			int worldPort = LobbyServer.Instance.Conf.World.BindPort;

			Send.WorldRedirect(conn, worldIp, worldPort);
		}

		[PacketHandler(Op.CheckNameRequest)]
		public void CheckNameRequest(LobbyConnection conn, Packet packet)
		{
			var nameToCheck = packet.GetLpString();

			// Check DB
			bool isAvailable = LobbyServer.Instance.Database.CheckNameAvailability(nameToCheck);

			if (isAvailable)
			{
				Send.CheckNameResponse(conn, "NEW_CHARACTER_ID_AVAILABLE");
			}
			else
			{
				Send.CheckNameResponse(conn, "EXIST_CHARACTER_ID");
			}
		}
	}
}
