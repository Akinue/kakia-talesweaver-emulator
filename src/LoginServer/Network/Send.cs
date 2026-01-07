using Kakia.TW.Shared.Network;
using System.Net;
using System.Text;

namespace Kakia.TW.Login.Network
{
	/// <summary>
	/// Packet senders.
	/// </summary>
	public static class Send
	{

		/// <summary>
		/// Sends the initial connection confirmation packet (0x7E).
		/// Usually sent immediately upon TCP connection.
		/// </summary>
		public static void Connected(LoginConnection conn)
		{
			// Op.LC_CONNECTED (0x7E)
			var packet = new Packet(Op.ConnectedResponse);

			packet.PutByte(0x1B); // Id
			packet.PutBytes(Encoding.ASCII.GetBytes("CONNECTED SERVER\n"));

			conn.Send(packet);
		}

		/// <summary>
		/// Sends the KeySeed (Handshake) packet.
		/// Note: This is usually the very first packet sent on connection.
		/// </summary>
		public static void Handshake(LoginConnection conn, string motd = "")
		{
			var packet = new Packet(Op.Handshake);

			// 1. Id
			packet.PutByte(0x00);

			// 2. SubFlag
			packet.PutByte(0x02);

			// 3. Seed
			packet.PutUInt(conn.Seed);

			// 4. IP Address
			packet.PutInt(IPAddress.Parse(conn.Address.Substring(0, conn.Address.IndexOf(":"))));

			// 5. MOTD
			packet.PutCompressedString(motd);

			conn.Send(packet);
		}

		/// <summary>
		/// Sends the account/character list information after a successful login.
		/// Corresponds to old CharacterListPacket (Log ID 0x50).
		/// </summary>
		public static void LoginSuccess(LoginConnection conn, List<AccountInfo> accounts)
		{
			var packet = new Packet(Op.LoginResponse);

			// Structure: [0x01] [Count] { [ServerId] [Name] [Unk1:4] [Time1:4] [Time2:4] [CharCount] }...
			packet.PutByte(0x01);
			packet.PutByte((byte)accounts.Count);

			foreach (var account in accounts)
			{
				packet.PutByte(account.ServerId);
				packet.PutString(account.Username);
				packet.PutInt(account.Unknown);
				packet.PutInt(account.CreatedAt);
				packet.PutInt(account.LastLogin);
				packet.PutByte(account.CharacterCount);
			}

			conn.Send(packet);
		}

		/// <summary>
		/// Sends the server list to the client.
		/// Response to CL_INIT_CONNECT.
		/// </summary>
		public static void ServerList(LoginConnection conn, List<ServerInfo> servers)
		{
			var packet = new Packet(Op.ServerListResponse);
			packet.PutByte((byte)servers.Count);

			foreach (var server in servers)
			{
				packet.PutByte(server.Id);
				packet.PutInt(server.IP); // PutInt writes Big Endian
				packet.PutUShort(server.Port);
				packet.PutString(server.Name, true);
				packet.PutUShort(server.Load);
			}

			packet.Align();
			conn.Send(packet);
		}

		/// <summary>
		/// Redirects the client to the World/Lobby server.
		/// Corresponds to old ServerConnectPacket (Log ID 0x03).
		/// </summary>
		public static void ServerSelectRedirect(LoginConnection conn, string ip, int port, byte flagUnknown)
		{
			// Op.LC_REDIRECT (0x03)
			var packet = new Packet(Op.ServerRedirect);

			// Structure reconstructed from Debug Log:
			// 03 [IP] [Port] [Flag1] [Seed] [Name:Pascal] [Unk] [Flag2]

			// 1. IP
			byte[] ipBytes = IPAddress.Parse(ip).GetAddressBytes();
			Array.Reverse(ipBytes);
			packet.PutBytes(ipBytes);

			// 2. Port
			packet.PutUShort((ushort)port);

			// 3. Flag1
			packet.PutByte(2);

			// 4. Seed
			packet.PutUInt(conn.Seed);

			// 5. Username (Pascal)
			packet.PutString(conn.Username, false);

			// 6. Unknown
			packet.PutByte(flagUnknown);

			// 7. Flag2
			packet.PutByte(0);

			conn.Send(packet);
		}
	}

	/// <summary>
	/// Data structure for passing server info to the packet generator.
	/// </summary>
	public class ServerInfo
	{
		public byte Id { get; set; }
		public IPAddress IP { get; set; }
		public ushort Port { get; set; }
		public string Name { get; set; } = string.Empty;
		public ushort Load { get; set; }
	}

	/// <summary>
	/// Data structure for passing account info to LoginSuccess packet.
	/// </summary>
	public class AccountInfo
	{
		public byte ServerId { get; set; }
		public string Username { get; set; } = string.Empty;
		public int Unknown { get; set; }
		public int CreatedAt { get; set; }
		public int LastLogin { get; set; }
		public byte CharacterCount { get; set; }
	}
}