using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using Yggdrasil.Util;

namespace Kakia.TW.Shared.Network
{
	/// <summary>
	/// Packet reader and writer for Tales Weaver.
	/// </summary>
	public class Packet
	{
		private readonly BufferReaderWriter _buffer;

		// Default encoding for standard strings (Korean Server default)
		private static readonly Encoding EncodingKR = Encoding.GetEncoding("EUC-KR");

		// Encoding for compressed strings (Legacy/JP Server behavior match)
		private static readonly Encoding EncodingSJIS;

		static Packet()
		{
			// Ensure we can handle Shift-JIS and EUC-KR in .NET Core
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			EncodingSJIS = Encoding.GetEncoding("shift_jis");
		}

		/// <summary>
		/// Gets or sets the packet's opcode.
		/// </summary>
		public Op Op { get; set; }

		/// <summary>
		/// Gets the number of elements in the buffer.
		/// </summary>
		public int Length => _buffer.Length;

		/// <summary>
		/// Creates new packet to write to.
		/// </summary>
		/// <param name="op"></param>
		public Packet(Op op)
		{
			this.Op = op;

			_buffer = new BufferReaderWriter();
			_buffer.Endianness = Endianness.BigEndian; // TW is Big Endian

			// Write OpCode (1 byte)
			_buffer.WriteByte((byte)op);
		}

		/// <summary>
		/// Creates packet from buffer to read it.
		/// </summary>
		/// <param name="buffer"></param>
		public Packet(byte[] buffer)
		{
			_buffer = new BufferReaderWriter(buffer);
			_buffer.Endianness = Endianness.BigEndian;

			if (_buffer.Length > 0)
			{
				// Check for TW Header (0xAA)
				// Format: [AA] [Len] [Len] [OpCode] [Payload...]
				if (buffer[0] == 0xAA && buffer.Length >= 4)
				{
					_buffer.ReadByte(); // AA
					_buffer.ReadByte(); // Len
					_buffer.ReadByte(); // Len
					this.Op = (Op)_buffer.ReadByte(); // Op
				}
				else
				{
					// Assume Raw/Decrypted buffer: [OpCode] [Payload...]
					this.Op = (Op)_buffer.ReadByte();
				}
			}
			else
			{
				this.Op = (Op)0;
			}
		}

		/// <summary>
		/// Writes value to packet.
		/// </summary>
		public void PutByte(byte value) => _buffer.WriteByte(value);

		/// <summary>
		/// Writes value to packet as 1 or 0.
		/// </summary>
		public void PutByte(bool value) => _buffer.WriteByte(value ? (byte)1 : (byte)0);

		/// <summary>
		/// Writes value to packet.
		/// </summary>
		public void PutBytes(byte[] value) => _buffer.Write(value);

		/// <summary>
		/// Writes given number of bytes with the value 0 to packet.
		/// </summary>
		public void PutEmpty(int amount)
		{
			for (var i = 0; i < amount; ++i)
				_buffer.WriteByte(0);
		}

		/// <summary>
		/// Writes zeros to align the packet length to a multiple of 4 bytes.
		/// </summary>
		public void Align()
		{
			var remainder = _buffer.Length % 4;
			if (remainder != 0)
				PutEmpty(4 - remainder);
		}

		/// <summary>
		/// Writes value to packet (Big Endian).
		/// </summary>
		public void PutShort(short value) => _buffer.WriteInt16(value);

		/// <summary>
		/// Writes value to packet (Big Endian).
		/// </summary>
		public void PutUShort(ushort value) => _buffer.WriteUInt16(value);

		/// <summary>
		/// Writes value to packet (Big Endian).
		/// </summary>
		public void PutInt(int value) => _buffer.WriteInt32(value);

		/// <summary>
		/// Writes value to packet (Big Endian).
		/// </summary>
		public void PutUInt(uint value) => _buffer.WriteUInt32(value);

		/// <summary>
		/// Writes value to packet (Big Endian).
		/// </summary>
		public void PutLong(long value) => _buffer.WriteInt64(value);

		/// <summary>
		/// Writes value to packet (Big Endian).
		/// </summary>
		public void PutULong(ulong value) => _buffer.WriteUInt64(value);

		/// <summary>
		/// Writes IP to packet.
		/// UPDATED: Reverses bytes to match Legacy PacketWriter behavior.
		/// </summary>
		public void PutInt(IPAddress value)
		{
			var bytes = value.GetAddressBytes();
			// Legacy PacketWriter reverses IP bytes (making them Little Endian effectively)
			Array.Reverse(bytes);
			_buffer.Write(bytes);
		}

		public void PutLpString(string val)
		{
			val ??= "";
			if (val == "" || val[val.Length - 1] != '\0') val += '\0';
			var bytes = Encoding.Latin1.GetBytes(val);
			this.PutByte((byte)bytes.Length);
			this.PutBytes(bytes);
		}

		/// <summary>
		/// Writes string to packet, padding or capping it at the
		/// given length, while also adding a null terminator.
		/// </summary>
		/// <example>
		/// packet.PutString("foobar", 8);
		/// => 66 6F 6F 62 61 72 00 00
		/// 
		/// packet.PutString("foobar", 4);
		/// => 66 6F 6F 00
		/// </example>
		/// <param name="value"></param>
		public void PutString(string value, int length)
		{
			var bytes = EncodingKR.GetBytes(value ?? "");
			var writeLength = Math.Min(bytes.Length, length - 1);
			var remain = length - writeLength;

			_buffer.Write(bytes, 0, writeLength);

			for (var i = 0; i < remain; ++i)
				_buffer.WriteByte(0);
		}

		/// <summary>
		/// Writes string and a null terminator to packet.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="terminate">Whether to put a null-terminator at the end of the string.</param>
		public void PutString(string value, bool terminate = true)
		{
			var bytes = EncodingKR.GetBytes(value ?? "");
			var length = (byte)bytes.Length;

			_buffer.WriteByte(length);
			_buffer.Write(bytes);
			if (terminate)
				_buffer.WriteByte(0);
		}

		/// <summary>
		/// Write uint in little endian.
		/// </summary>
		/// <param name="value"></param>
		public void PutUIntLittleEndian(uint value)
		{
			var bytes = new byte[4];
			BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
			_buffer.Write(bytes);
		}

		/// <summary>
		/// Write ulong in little endian.
		/// </summary>
		public void PutULongLittleEndian(ulong value)
		{
			var bytes = new byte[8];
			BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
			_buffer.Write(bytes);
		}

		/// <summary>
		/// Writes a ZLib compressed string used by TW.
		/// Format: [0x01] [Len:UShort] [CompressedBytes]
		/// UPDATED: Uses Shift-JIS by default to match Legacy PacketWriter.
		/// </summary>
		public void PutCompressedString(string value, Encoding encoding = null)
		{
			// Default to JIS if no encoding provided
			encoding ??= EncodingSJIS;

			// Register provider to ensure Shift-JIS works on .NET Core/.NET 5+
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

			var bytes = encoding.GetBytes(value ?? "");

			using var outMs = new MemoryStream();
			using (var z = new ZLibStream(outMs, CompressionMode.Compress, leaveOpen: true))
			{
				z.Write(bytes, 0, bytes.Length);
			}

			var compressed = outMs.ToArray();

			_buffer.WriteByte(0x01); // Compression Flag
			_buffer.WriteUInt16((ushort)compressed.Length); // Length of compressed data
			_buffer.Write(compressed); // The data
		}

		/// <summary>
		/// Writes bytes to buffer.
		/// </summary>
		/// <param name="val"></param>
		public void PutBin(params byte[] val)
			=> _buffer.Write(val);

		/// <summary>
		/// Writes bytes parsed from given hex string to buffer.
		/// </summary>
		/// <param name="hex"></param>
		public void PutBinFromHex(string hex)
		{
			if (hex == null)
				throw new ArgumentNullException(nameof(hex));

			var bytes = Hex.ToByteArray(hex);
			this.PutBin(bytes);
		}

		/// <summary>
		/// Writes the given amount of bytes to the buffer.
		/// </summary>
		/// <param name="amount"></param>
		public void PutEmptyBin(int amount)
		{
			if (amount <= 0)
				return;

			this.PutBin(new byte[amount]);
		}

		/// <summary>
		/// Adds the given number of empty bytes to the packet.
		/// </summary>
		/// <remarks>
		/// Effectively the same as PutEmptyBin, but specifically for known
		/// gaps in packets.
		/// </remarks>
		/// <param name="amount"></param>
		public void PutGap(int amount)
			=> this.PutEmptyBin(amount);

		/// <summary>
		/// Reads a byte from packet and returns it.
		/// </summary>
		/// <returns></returns>
		public byte GetByte()
			=> _buffer.ReadByte();

		/// <summary>
		/// Reads the given number of bytes from the packet and returns them.
		/// </summary>
		/// <param name="length"></param>
		/// <returns></returns>
		public byte[] GetBytes(int length)
			=> _buffer.Read(length);

		/// <summary>
		/// Reads a short from packet and returns it.
		/// </summary>
		/// <returns></returns>
		public short GetShort()
			=> _buffer.ReadInt16();

		public ushort GetUShort()
			=> _buffer.ReadUInt16();

		/// <summary>
		/// Reads an int from packet and returns it.
		/// </summary>
		/// <returns></returns>
		public int GetInt()
			=> _buffer.ReadInt32();

		public uint GetUInt()
			=> _buffer.ReadUInt32();

		public long GetLong()
			=> _buffer.ReadInt64();

		public ulong GetULong()
			=> _buffer.ReadUInt64();

		/// <summary>
		/// Reads 8 bytes as Big Endian unsigned long.
		/// </summary>
		public ulong GetULongBE()
		{
			var bytes = _buffer.Read(8);
			if (BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			return BitConverter.ToUInt64(bytes, 0);
		}

		/// <summary>
		/// Reads given number of bytes from packet and returns them
		/// as a string.
		/// </summary>
		/// <returns></returns>
		public string GetLpString()
		{
			var length = GetByte();
			var bytes = _buffer.Read(length);
			var len = Array.IndexOf(bytes, (byte)0);

			if (len == -1)
				len = bytes.Length;

			var result = EncodingKR.GetString(bytes, 0, len);

			return result;
		}

		/// <summary>
		/// Reads given number of bytes from packet and returns them
		/// as a string.
		/// </summary>
		/// <returns></returns>
		public string GetString(int length)
		{
			var bytes = _buffer.Read(length);
			var len = Array.IndexOf(bytes, (byte)0);

			if (len == -1)
				len = bytes.Length;

			var result = EncodingKR.GetString(bytes, 0, len);

			return result;
		}

		/// <summary>
		/// Returns a buffer containing the packet's body, without opcode.
		/// </summary>
		/// <returns></returns>
		public byte[] Build()
		{
			// The buffer contains [Op] [Body].
			// We usually want to send the whole thing (Op included) to the Framer.
			// However, Connection.Send calls _framer.Frame(packet). 
			// If Framer expects just the body (excluding op), we strip it.
			// But Packet constructor wrote Op. 
			// Let's assume Build() returns the FULL packet content (Op + Body)
			// effectively treating the "Op" as the first byte of data.

			return _buffer.Copy();
		}

		/// <summary>
		/// Returns a string representation of the packet.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
			=> Hex.ToString(_buffer.Copy(), HexStringOptions.SpaceSeparated | HexStringOptions.SixteenNewLine);
	}
}
