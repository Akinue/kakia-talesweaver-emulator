using Kakia.TW.Shared.Network.Crypto;
using Yggdrasil.Network.Framing;

namespace Kakia.TW.Shared.Network
{
	public class TwFramer : IMessageFramer
	{
		private readonly byte[] _headerBuffer;
		private byte[] _messageBuffer;
		private int _bytesReceived;
		private readonly int _headerLength;

		public bool IsCryptoSet { get; set; } = false;

		/// <summary>
		/// Maximum size of messages.
		/// </summary>
		public int MaxMessageSize { get; }

		/// <summary>
		/// The Codec used for encrypting and decrypting packets.
		/// If this is null or not initialized, packets are treated as plain bytes.
		/// </summary>
		public TwCodec Codec { get; set; }

		/// <summary>
		/// Controls whether outgoing packets are encrypted. 
		/// Useful for sending the initial Handshake before enabling encryption.
		/// </summary>
		public bool EncryptOutput { get; set; } = true;

		/// <summary>
		/// Called every time ReceiveData got a full message.
		/// </summary>
		public event Action<byte[]> MessageReceived;

		/// <summary>
		/// Creates new instance.
		/// </summary>
		/// <param name="maxMessageSize">Maximum size of messages</param>
		public TwFramer(int maxMessageSize)
		{
			MaxMessageSize = maxMessageSize;

			// TW Header is: [0xAA] [LenMSB] [LenLSB]
			// Length field is Big Endian.
			_headerBuffer = new byte[3];
			_headerLength = 3;

			// Initialize Codec (Session should likely set Seed later)
			Codec = new TwCodec();
		}

		/// <summary>
		/// Wraps message in frame.
		/// Used for raw byte arrays.
		/// </summary>
		public byte[] Frame(byte[] message)
		{
			// 1. Encrypt Payload if codec is ready
			byte[] payload = message;

			// Encrypt ONLY if Codec is initialized AND Encryption is explicitly enabled
			if (Codec != null && Codec.IsInitialized && EncryptOutput)
			{
				// Encrypt adds the Sequence byte (SendIndex) to the front
				payload = Codec.Encrypt(message);
			}

			// 2. Calculate Length
			// The length field in TW protocol includes the Sequence byte + Body.
			// Since Codec.Encrypt prepends the Sequence byte, payload.Length is correct.
			// If unencrypted, ensure the caller included necessary headers or handle accordingly.
			int length = payload.Length;

			// 3. Build Frame: [0xAA] [LenHigh] [LenLow] [Payload...]
			byte[] buffer = new byte[3 + length];
			buffer[0] = 0xAA;
			buffer[1] = (byte)(length >> 8 & 0xFF);
			buffer[2] = (byte)(length & 0xFF);

			Buffer.BlockCopy(payload, 0, buffer, 3, length);

			return buffer;
		}

		/// <summary>
		/// Wraps packet body in frame.
		/// Used for structured Packet objects.
		/// </summary>
		public byte[] Frame(Packet packet)
		{
			// Build the raw body from the packet
			var body = packet.Build();
			return Frame(body);
		}

		/// <summary>
		/// Receives data and calls MessageReceived every time a full message
		/// has arrived.
		/// </summary>
		/// <param name="data">Buffer to read from.</param>
		/// <param name="length">Length of actual information in data.</param>
		public void ReceiveData(byte[] data, int length)
		{
			var bytesAvailable = length;
			if (bytesAvailable == 0)
				return;

			for (var i = 0; i < bytesAvailable;)
			{
				if (_messageBuffer == null)
				{
					// Fill header buffer
					_headerBuffer[_bytesReceived] = data[i];
					_bytesReceived += 1;
					i += 1;

					// Once we have the full header (3 bytes)
					if (_bytesReceived == _headerLength)
					{
						// Validate Magic Byte
						if (_headerBuffer[0] != 0xAA)
						{
							// In a real scenario, we might want to throw or disconnect.
							// For now, reset and hope for alignment or throw exception.
							throw new InvalidMessageSizeException("Invalid Header Magic Byte: " + _headerBuffer[0].ToString("X2"));
						}

						// Read Length (Big Endian)
						// This length typically includes the Sequence byte + Body
						var bodySize = _headerBuffer[1] << 8 | _headerBuffer[2];

						// Total frame size = Header(3) + Body(bodySize)
						var totalSize = _headerLength + bodySize;

						if (bodySize < 0 || totalSize > MaxMessageSize)
							throw new InvalidMessageSizeException("Invalid size (" + bodySize + ").");

						_messageBuffer = new byte[totalSize];

						// Copy the header into the message buffer so the packet remains complete
						// (Crypto might need the sequence byte which follows immediately)
						Buffer.BlockCopy(_headerBuffer, 0, _messageBuffer, 0, _headerLength);

						// _bytesReceived is currently 3 (header length), which matches the offset in _messageBuffer
					}
				}

				if (_messageBuffer != null)
				{
					// Copy the rest of the packet to the message buffer
					var read = Math.Min(_messageBuffer.Length - _bytesReceived, bytesAvailable - i);
					Buffer.BlockCopy(data, i, _messageBuffer, _bytesReceived, read);

					_bytesReceived += read;
					i += read;

					// Once we have received the full packet
					if (_bytesReceived == _messageBuffer.Length)
					{
						byte[] finalPacket;

						// Check if crypto is initialized AND the client has acknowledged it
						bool shouldDecrypt = Codec != null && Codec.IsInitialized && IsCryptoSet;

						if (shouldDecrypt)
						{
							// Decrypt returns the body (stripped of headers and sequence byte)
							finalPacket = Codec.Decrypt(_messageBuffer);
							if (finalPacket == null)
							{
								// Handle decryption failure (log or disconnect)
								_messageBuffer = null;
								_bytesReceived = 0;
								return;
							}
						}
						else
						{
							// If not encrypted, strip the header (AA Len Len) manually so Packet receives just the Body
							finalPacket = new byte[_messageBuffer.Length - _headerLength];
							Buffer.BlockCopy(_messageBuffer, _headerLength, finalPacket, 0, finalPacket.Length);
						}

						MessageReceived?.Invoke(finalPacket);

						_messageBuffer = null;
						_bytesReceived = 0;
					}
				}
			}
		}
	}
}