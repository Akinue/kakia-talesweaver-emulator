using Kakia.TW.Shared.Database;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Yggdrasil.Logging;
using Yggdrasil.Network.TCP;
using Yggdrasil.Util;

namespace Kakia.TW.Shared.Network
{
	/// <summary>
	/// Connection base class for the servers.
	/// </summary>
	public abstract class Connection : TcpConnection
	{
		protected readonly TwFramer _framer;

		public string Username { get; set; }
		public uint Seed { get; set; }
		public Account Account { get; set; }

		public TwFramer Framer => _framer;

		/// <summary>
		/// Creates new connection.
		/// </summary>
		protected Connection()
		{
			_framer = new TwFramer(1024);
			_framer.MessageReceived += this.OnMessageReceived;
		}

		/// <summary>
		/// Called when new data was sent from the client.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="length"></param>
		protected override void ReceiveData(byte[] buffer, int length)
		{
			// This logs the raw encrypted wire bytes including headers (0xAA...)
			// You can keep this for low-level debugging or comment it out to reduce noise
			// Log.Debug("< WIRE {0}", Hex.ToString(buffer, 0, length));

			_framer.ReceiveData(buffer, length);
		}

		/// <summary>
		/// Called when a full message was received from the client (Decrypted & Unframed).
		/// </summary>
		/// <param name="buffer">The packet body (OpCode + Data)</param>
		protected virtual void OnMessageReceived(byte[] buffer)
		{
			PaleLogger.Log(false, buffer);

			// --- LOGGING START (MATCHING OLD FORMAT) ---
			ushort pType = 0;
			if (buffer.Length >= 2)
				pType = BitConverter.ToUInt16(buffer, 0); // Read OpCode as Little Endian UShort for display
			else if (buffer.Length > 0)
				pType = buffer[0];

			/**
			Log.Debug("[RECV RAW] ID: 0x{0:X2} | Len: {1}\n{2}",
				buffer[0], // Assuming 1-byte OpCode for ID display
				buffer.Length,
				Hex.ToString(buffer, HexStringOptions.SpaceSeparated | HexStringOptions.SixteenNewLine));
			**/
			// --- LOGGING END ---

			// CRITICAL: Check for handshake acknowledgment BEFORE processing
			// After the server sends the handshake (seed), the client responds with CL_INIT_CONNECT (0x00)
			// This is the first packet from the client and signals crypto should be enabled
			if (!Framer.IsCryptoSet && buffer.Length == 5)
			{
				Log.Debug("Crypto Handshake ACK received (CL_INIT_CONNECT).");
				// Fall through to process the packet normally
			}

			var packet = new Packet(buffer);
			// Convert Network Op (e.g. 0x7E) to Host Op (Enum)
			packet.Op = PacketTable.ToHost((int)packet.Op);
			if (packet.Op == Op.Unknown)
				return;

			this.OnPacketReceived(packet);
		}

		/// <summary>
		/// Called when a packet was received from the client.
		/// </summary>
		/// <param name="packet"></param>
		protected abstract void OnPacketReceived(Packet packet);

		/// <summary>
		/// Sends packet to client.
		/// </summary>
		/// <param name="packet"></param>
		public void Send(Packet packet)
		{
			// 1. Store Internal Op for debugging if needed
			var internalOp = packet.Op;

			// 2. Translate Internal Op (Enum) to Network Op (0x7E, etc.)
			packet.Op = packet.Op;

			// 3. Build the raw payload (OpCode + Body) BEFORE encryption/framing
			var payload = packet.Build();

			// --- LOGGING START (MATCHING OLD FORMAT) ---
			ushort pType = 0;
			if (payload.Length >= 2)
				pType = BitConverter.ToUInt16(payload, 0);
			else if (payload.Length > 0)
				pType = payload[0];

			/**
			Log.Debug("[SEND RAW] Type: {0} (0x{1:X4}) | Len: {2}\n{3}",
				(int)packet.Op, // Decimal Op
				pType,          // Hex Word (Little Endian)
				payload.Length,
				Hex.ToString(payload, HexStringOptions.SpaceSeparated | HexStringOptions.SixteenNewLine));
			**/
			// --- LOGGING END ---

			// 4. Frame (and optionally encrypt) the payload
			// Note: We use the Frame(byte[]) overload since we already built it
			var buffer = _framer.Frame(payload);

			if (_framer.Codec != null && _framer.Codec.IsInitialized && _framer.EncryptOutput)
			{
				var decodedPacket = _framer.Codec.Decrypt(buffer);
				if (decodedPacket == null)
					decodedPacket = buffer;
				PaleLogger.Log(true, decodedPacket);
			}
			else
				PaleLogger.Log(true, buffer);

			// Optional: Packet Table Size Check Logic
			var tableSize = PacketTable.GetSize((int)packet.Op);
			if (tableSize != PacketTable.Dynamic && buffer.Length != tableSize + 3) // +3 for Header
			{
				// Note: Buffer length includes AA Len Len (3 bytes). Table usually defines Body size.
				// This check might need adjustment depending on how your PacketTable is defined.
				// For now, ignoring strictly to focus on logging/sending.
			}

			try
			{
				this.Send(buffer);
			}
			catch (SocketException)
			{
				this.Close();
			}
		}

		/// <summary>
		/// Called when an exception occurred while reading data from
		/// the TCP stream.
		/// </summary>
		/// <param name="ex"></param>
		protected override void OnReceiveException(Exception ex)
		{
			Log.Error("Error while receiving data: {0}", ex);
		}

		/// <summary>
		/// Called when the connection was closed.
		/// </summary>
		/// <param name="type"></param>
		protected override void OnClosed(ConnectionCloseType type)
		{
			switch (type)
			{
				case ConnectionCloseType.Disconnected: Log.Info("Connection from '{0}' was closed by the client.", this.Address); break;
				case ConnectionCloseType.Closed: Log.Info("Connection to '{0}' was closed by the server.", this.Address); break;
				case ConnectionCloseType.Lost: Log.Info("Lost connection from '{0}'.", this.Address); break;
			}
		}

		/// <summary>
		/// Closes the connection after the given amount of seconds.
		/// </summary>
		/// <param name="seconds"></param>
		public void Close(int seconds)
		{
			Task.Delay(seconds * 1000).ContinueWith(_ => this.Close());
		}
	}
}