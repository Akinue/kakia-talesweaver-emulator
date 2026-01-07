using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Numerics;

namespace TWConv
{
	/// <summary>
	/// TalesWeaver DAT Decryptor - Ported from Python implementation
	/// Uses stream cipher with S-boxes and key scheduling
	/// </summary>
	internal class TalesWeaverDatDecryptor
	{
		// Base key used for key derivation
		private const string BaseKey = "VS#sg#^$sa2d34";

		public static void Run(string[] args)
		{
			Console.WriteLine("=== TalesWeaver DAT Decryptor v7 (Stream Cipher) ===");

			string folder = args.Length > 0 ? args[0] : @"C:\Nexon\TalesWeaver\DATA\";
			string outDir = Path.Combine(folder, "Decrypted_Tales");
			Directory.CreateDirectory(outDir);

			var files = Directory.GetFiles(folder, "*.DAT");

			int successCount = 0;
			int failCount = 0;

			foreach (var file in files)
			{
				try
				{
					string filename = Path.GetFileName(file);
					byte[] fileContent = File.ReadAllBytes(file);

					if (fileContent.Length < 64)
					{
						Console.WriteLine($"[SKIP] {filename}: File too small");
						continue;
					}

					Console.WriteLine($"\n[Processing] {filename}");

					// Calculate checksums for offsets
					var (offset1, offset2Seed, rawChecksum2) = CalculateChecksums(filename);
					Console.WriteLine($"  Checksum Offset 1: {offset1}, Offset 2 Seed: {offset2Seed}");

					// Generate header key
					string combinedString = filename + BaseKey;
					byte[] headerKey = GenerateHeaderKey(combinedString);
					Console.WriteLine($"  Header Key: {BitConverter.ToString(headerKey).Replace("-", "")}");

					// Create header cipher and decrypt header
					var headerCipher = new StreamCipher(headerKey);

					int currentOffset = offset1;

					// Read and decrypt header fields
					byte[] encDword1 = new byte[4];
					Array.Copy(fileContent, currentOffset, encDword1, 0, 4);
					currentOffset += 4;

					byte[] encByte = new byte[1];
					Array.Copy(fileContent, currentOffset, encByte, 0, 1);
					currentOffset += 1;

					byte[] encDword2 = new byte[4];
					Array.Copy(fileContent, currentOffset, encDword2, 0, 4);
					currentOffset += 4;

					byte[] decDword1Bytes = headerCipher.StreamDecrypt(encDword1, 4);
					byte[] decByteArr = headerCipher.StreamDecrypt(encByte, 1);
					byte[] decDword2Bytes = headerCipher.StreamDecrypt(encDword2, 4);

					uint decDword1 = BitConverter.ToUInt32(decDword1Bytes, 0);
					byte decVersion = decByteArr[0];
					uint decDword2 = BitConverter.ToUInt32(decDword2Bytes, 0);

					Console.WriteLine($"  Header: dword1={decDword1}, version={decVersion}, dword2={decDword2}");

					// Validate header integrity
					if (decDword1 != ((decDword2 + decVersion) & 0xFFFFFFFF))
					{
						Console.WriteLine($"[WARN] {filename}: Header integrity check failed, trying alternate method...");
						// Could try fallback XOR method here
						failCount++;
						continue;
					}

					Console.WriteLine("  Header OK.");

					// Parse file metadata
					uint numChunks = decDword2;
					int metadataStartOffset = currentOffset + offset2Seed;
					int contentKeySeed = (currentOffset - 9) + offset2Seed;

					Console.WriteLine($"  DAT contains {numChunks} files.");

					// Generate content key
					byte[] contentSbox = GenerateContentSbox(combinedString, contentKeySeed);
					byte[] contentKey = new byte[16];
					Array.Copy(contentSbox, 0, contentKey, 0, 16);
					Console.WriteLine($"  Content Key: {BitConverter.ToString(contentKey).Replace("-", "")}");

					var contentCipher = new StreamCipher(contentKey);

					// Create output directory for this DAT file
					string datOutDir = Path.Combine(outDir, Path.GetFileNameWithoutExtension(filename));
					Directory.CreateDirectory(datOutDir);

					// Process each chunk
					for (uint chunkNum = 0; chunkNum < numChunks; chunkNum++)
					{
						try
						{
							// Read filename length
							byte[] encNameLen = new byte[4];
							Array.Copy(fileContent, metadataStartOffset, encNameLen, 0, 4);
							byte[] decNameLen = contentCipher.StreamDecrypt(encNameLen, 4);
							int nameLen = (int)BitConverter.ToUInt32(decNameLen, 0) * 2; // UTF-16
							metadataStartOffset += 4;

							// Read filename
							byte[] encFilename = new byte[nameLen];
							Array.Copy(fileContent, metadataStartOffset, encFilename, 0, nameLen);
							byte[] decFilename = contentCipher.StreamDecrypt(encFilename, nameLen);
							string chunkFilename = Encoding.Unicode.GetString(decFilename).TrimEnd('\0');
							metadataStartOffset += nameLen;

							// Read metadata fields
							byte[] encUnk1 = new byte[4];
							Array.Copy(fileContent, metadataStartOffset, encUnk1, 0, 4);
							uint unk1 = BitConverter.ToUInt32(contentCipher.StreamDecrypt(encUnk1, 4), 0);
							metadataStartOffset += 4;

							byte[] encCryptFlag = new byte[4];
							Array.Copy(fileContent, metadataStartOffset, encCryptFlag, 0, 4);
							uint cryptFlag = BitConverter.ToUInt32(contentCipher.StreamDecrypt(encCryptFlag, 4), 0);
							metadataStartOffset += 4;

							byte[] encUnk2 = new byte[4];
							Array.Copy(fileContent, metadataStartOffset, encUnk2, 0, 4);
							uint unk2 = BitConverter.ToUInt32(contentCipher.StreamDecrypt(encUnk2, 4), 0);
							metadataStartOffset += 4;

							byte[] encUnk3 = new byte[4];
							Array.Copy(fileContent, metadataStartOffset, encUnk3, 0, 4);
							uint unk3 = BitConverter.ToUInt32(contentCipher.StreamDecrypt(encUnk3, 4), 0);
							metadataStartOffset += 4;

							byte[] encUnk4 = new byte[4];
							Array.Copy(fileContent, metadataStartOffset, encUnk4, 0, 4);
							uint unk4 = BitConverter.ToUInt32(contentCipher.StreamDecrypt(encUnk4, 4), 0);
							metadataStartOffset += 4;

							// Read file key
							byte[] encFileKey = new byte[16];
							Array.Copy(fileContent, metadataStartOffset, encFileKey, 0, 16);
							byte[] fileKey = contentCipher.StreamDecrypt(encFileKey, 16);
							metadataStartOffset += 16;

							Console.WriteLine($"    [{chunkNum}] {chunkFilename} (cryptFlag: 0x{cryptFlag:X8})");

							// TODO: Extract and decrypt actual file content using fileKey
							// This would require knowing the file data offsets and sizes
						}
						catch (Exception ex)
						{
							Console.WriteLine($"    [ERR] Chunk {chunkNum}: {ex.Message}");
						}
					}

					successCount++;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"[ERR] {file}: {ex.Message}");
					failCount++;
				}
			}

			Console.WriteLine($"\n=== Complete ===");
			Console.WriteLine($"Success: {successCount}, Failed: {failCount}");
		}

		/// <summary>
		/// Calculate checksum offsets from filename
		/// </summary>
		private static (int offset1, int offset2Seed, int rawChecksum2) CalculateChecksums(string filename)
		{
			int checksum1 = 0;
			int checksum2 = 0;

			foreach (char c in filename)
			{
				int charVal = (int)c;
				checksum1 += charVal;
				checksum2 += charVal * 3;
			}

			int offset1 = (checksum1 % 312) + 30;
			int offset2Seed = (checksum2 % 212) + 33;

			return (offset1, offset2Seed, checksum2);
		}

		/// <summary>
		/// Generate 16-byte header key from combined string
		/// </summary>
		private static byte[] GenerateHeaderKey(string combinedString)
		{
			byte[] keyBuffer = Encoding.ASCII.GetBytes(combinedString);
			int keyBufferSize = keyBuffer.Length;

			byte[] finalKey = new byte[128];

			for (int i = 0; i < 128; i++)
			{
				int index = i % keyBufferSize;
				byte value = keyBuffer[index];
				finalKey[i] = (byte)((i + value) & 0xFF);
			}

			byte[] result = new byte[16];
			Array.Copy(finalKey, 0, result, 0, 16);
			return result;
		}

		/// <summary>
		/// Generate content S-box from combined string and seed
		/// </summary>
		private static byte[] GenerateContentSbox(string combinedString, int seed)
		{
			byte[] keyBuffer = Encoding.ASCII.GetBytes(combinedString);
			byte[] sbox = new byte[128];
			int keyLen = keyBuffer.Length;

			for (int i = 0; i < 128; i++)
			{
				// Handle negative modulo properly
				int index = ((seed - i) % keyLen + keyLen) % keyLen;
				byte charVal = keyBuffer[index];
				sbox[i] = (byte)((i + (i % 3 + 2) * charVal) & 0xFF);
			}

			return sbox;
		}
	}

	/// <summary>
	/// Stream cipher implementation - port from Python
	/// </summary>
	internal class StreamCipher
	{
		private readonly byte[] _key;
		private readonly uint[] _state;
		private readonly List<byte> _keystreamBuffer;

		public StreamCipher(byte[] key)
		{
			if (key.Length != 16)
				throw new ArgumentException("Key must be 16 bytes long.");

			_key = (byte[])key.Clone();
			_state = new uint[256];
			_keystreamBuffer = new List<byte>();

			GenerateKeySchedule();
		}

		private static byte Byte0(uint n) => (byte)(n & 0xFF);
		private static byte Byte1(uint n) => (byte)((n >> 8) & 0xFF);
		private static byte Byte2(uint n) => (byte)((n >> 16) & 0xFF);
		private static byte Byte3(uint n) => (byte)((n >> 24) & 0xFF);

		private static int ToInt8(byte b) => b > 127 ? b - 256 : b;

		/// <summary>
		/// Load 4 bytes as signed big-endian uint32
		/// </summary>
		private uint LoadSignedBigEndian(byte[] keyBytes, int offset = 0)
		{
			int temp = ToInt8(keyBytes[offset]);
			uint val = (uint)temp << 8;
			temp = ToInt8(keyBytes[offset + 1]);
			val |= (uint)temp & 0xFF;
			val = val << 8;
			temp = ToInt8(keyBytes[offset + 2]);
			val |= (uint)temp & 0xFF;
			val = val << 8;
			temp = ToInt8(keyBytes[offset + 3]);
			val |= (uint)temp & 0xFF;
			return val;
		}

		private uint PerformRoundUpdate(uint vTarget, uint vShiftRightSrc, params uint[] xorInputs)
		{
			uint result = (vTarget << 8) ^
						 (vShiftRightSrc >> 8) ^
						 CipherTables.DIV_A[vShiftRightSrc & 0xFF] ^
						 CipherTables.MUL_A[(vTarget >> 24) & 0xFF];

			foreach (uint val in xorInputs)
			{
				result ^= val;
			}

			return result;
		}

		private (uint[] k, uint[] state) InitializeStateFromKey()
		{
			uint[] k = new uint[4];
			for (int i = 0; i < 4; i++)
			{
				k[i] = LoadSignedBigEndian(_key, i * 4);
			}

			uint[] state = new uint[]
			{
				k[0], k[1], k[2], k[3],
				~k[0], ~k[1], ~k[2], ~k[3],
				k[0], k[1], k[2], k[3],
				~k[0], ~k[1], ~k[2], ~k[3]
			};

			return (k, state);
		}

		private void GenerateKeySchedule()
		{
			var (k, state) = InitializeStateFromKey();
			uint[] keyScheduleOutput = new uint[38];

			keyScheduleOutput[0] = k[0];
			keyScheduleOutput[1] = k[1];
			keyScheduleOutput[2] = k[2];

			uint fsmReg1 = 0, fsmReg2 = 0, tempOutput18 = 0;

			for (int i = 0; i < 2; i++)
			{
				uint v8 = (i == 0) ? k[0] : state[0];
				uint v12 = (i == 0) ? ~k[1] : state[13];

				uint v14 = v8 + fsmReg1;
				uint v15 = fsmReg2 + state[10];
				uint v16 = (state[4] >> 8) ^ v14 ^ CipherTables.DIV_A[Byte0(state[4])] ^ CipherTables.MUL_A[Byte3(state[15])];
				tempOutput18 = v15;
				state[15] = v12 ^ fsmReg2 ^ (state[15] << 8) ^ v16;

				uint v17 = CipherTables.S1_T0[Byte0(fsmReg1)] ^ CipherTables.S1_T1[Byte1(fsmReg1)] ^
						  CipherTables.S1_T2[Byte2(fsmReg1)] ^ CipherTables.S1_T3[Byte3(fsmReg1)];
				uint v79 = v17 + state[9];

				state[14] = PerformRoundUpdate(state[14], state[3], state[12], v17, v15 + state[15]);
				uint v18 = v17 + state[9] + state[14];

				uint v19 = CipherTables.S1_T2[Byte2(tempOutput18)] ^ CipherTables.S1_T1[Byte1(tempOutput18)] ^ CipherTables.S1_T0[Byte0(v15)];
				uint v21 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ v19;
				tempOutput18 = v79;

				uint v22 = v21 + state[8];
				state[13] = PerformRoundUpdate(state[13], state[2], v21, v18, state[11]);
				uint v23 = v21 + state[8] + state[13];

				uint v24 = CipherTables.S1_T2[Byte2(tempOutput18)] ^ CipherTables.S1_T1[Byte1(tempOutput18)] ^ CipherTables.S1_T0[Byte0(v79)];
				uint v26 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ v24;
				tempOutput18 = v22;

				state[12] = PerformRoundUpdate(state[12], state[1], v26, v23, state[10]);
				uint v80 = v26 + state[7];

				uint v27 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ CipherTables.S1_T2[Byte2(tempOutput18)] ^
						  CipherTables.S1_T1[Byte1(tempOutput18)] ^ CipherTables.S1_T0[Byte0(v22)];
				tempOutput18 = v80;

				state[11] = PerformRoundUpdate(state[11], state[0], v27, v80 + state[12], state[9]);

				uint v29 = CipherTables.S1_T2[Byte2(tempOutput18)] ^ CipherTables.S1_T0[Byte0(v80)] ^ CipherTables.S1_T1[Byte1(tempOutput18)];
				uint tempSum = v27 + state[6];
				uint v31 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ v29;
				tempOutput18 = tempSum;

				state[10] = PerformRoundUpdate(state[10], state[15], state[8], v31, tempSum + state[11]);
				uint v32 = v31 + state[5];

				uint v33 = CipherTables.S1_T2[Byte2(tempOutput18)] ^ CipherTables.S1_T1[Byte1(tempOutput18)] ^ CipherTables.S1_T0[Byte0(tempSum)];
				uint v36 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ v33;
				tempOutput18 = v32;

				state[9] = PerformRoundUpdate(state[9], state[14], state[7], v36, v32 + state[10]);

				uint v37 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ CipherTables.S1_T2[Byte2(tempOutput18)] ^
						  CipherTables.S1_T1[Byte1(tempOutput18)] ^ CipherTables.S1_T0[Byte0(v32)];
				tempSum = v36 + state[4];
				tempOutput18 = tempSum;

				state[8] = PerformRoundUpdate(state[8], state[13], v37, tempSum + state[9], state[6]);

				uint v39 = CipherTables.S1_T2[Byte2(tempOutput18)] ^ CipherTables.S1_T0[Byte0(tempSum)] ^ CipherTables.S1_T1[Byte1(tempOutput18)];
				tempSum = v37 + state[3];
				uint v42 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ v39;
				tempOutput18 = tempSum;

				state[7] = PerformRoundUpdate(state[7], state[12], state[5], v42, tempSum + state[8]);

				uint v43 = CipherTables.S1_T2[Byte2(tempOutput18)] ^ CipherTables.S1_T1[Byte1(tempOutput18)] ^ CipherTables.S1_T0[Byte0(tempSum)];
				tempSum = v42 + state[2];
				uint v45 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ v43;
				tempOutput18 = tempSum;

				state[6] = PerformRoundUpdate(state[6], state[11], v45, tempSum + state[7], state[4]);
				uint v81 = v45 + state[1];

				uint v46 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ CipherTables.S1_T2[Byte2(tempOutput18)] ^
						  CipherTables.S1_T1[Byte1(tempOutput18)] ^ CipherTables.S1_T0[Byte0(tempSum)];
				tempOutput18 = v81;

				state[5] = PerformRoundUpdate(state[5], state[10], v46, v81 + state[6], state[3]);
				uint v48 = v46 + state[0];

				uint v50 = CipherTables.S1_T2[Byte2(tempOutput18)] ^ CipherTables.S1_T0[Byte0(v81)] ^ CipherTables.S1_T1[Byte1(tempOutput18)];
				uint v52 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ v50;
				tempOutput18 = v48;

				state[4] = PerformRoundUpdate(state[4], state[9], v52, v46 + state[0] + state[5], state[2]);

				uint v53 = CipherTables.S1_T2[Byte2(tempOutput18)] ^ CipherTables.S1_T1[Byte1(tempOutput18)] ^ CipherTables.S1_T0[Byte0(v48)];
				tempSum = v52 + state[15];
				uint v56 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ v53;
				tempOutput18 = tempSum;

				state[3] = PerformRoundUpdate(state[3], state[8], v56, state[1], tempSum + state[4]);
				uint v57 = v56 + state[14];

				uint v58 = CipherTables.S1_T2[Byte2(tempOutput18)] ^ CipherTables.S1_T1[Byte1(tempOutput18)] ^ CipherTables.S1_T0[Byte0(tempSum)];
				uint v60 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ v58;
				tempOutput18 = v57;

				state[2] = PerformRoundUpdate(state[2], state[7], v60, state[0], v57 + state[3]);
				uint v62 = v60 + state[13];

				uint v63 = CipherTables.S1_T2[Byte2(tempOutput18)] ^ CipherTables.S1_T1[Byte1(tempOutput18)] ^ CipherTables.S1_T0[Byte0(v57)];
				uint v66 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ v63;
				tempOutput18 = v62;

				state[1] = PerformRoundUpdate(state[1], state[6], v66, state[15], v62 + state[2]);
				uint v68 = v66 + state[12];

				uint v69 = CipherTables.S1_T2[Byte2(tempOutput18)] ^ CipherTables.S1_T1[Byte1(tempOutput18)] ^ CipherTables.S1_T0[Byte0(v62)];
				uint v72 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ v69;
				tempOutput18 = v68;

				uint v76 = v72 ^ (v68 + state[1]);
				fsmReg1 = v72 + state[11];
				state[0] = PerformRoundUpdate(state[0], state[5], v76, state[14]);

				uint v73 = CipherTables.S1_T2[Byte2(tempOutput18)] ^ CipherTables.S1_T1[Byte1(tempOutput18)] ^ CipherTables.S1_T0[Byte0(v68)];
				fsmReg2 = CipherTables.S1_T3[Byte3(tempOutput18)] ^ v73;
				tempOutput18 = fsmReg1;
			}

			// Copy state to key schedule output
			for (int j = 0; j < 16; j++)
			{
				keyScheduleOutput[j] = state[j];
			}

			keyScheduleOutput[16] = state[0] ^ (state[5] >> 8) ^ CipherTables.DIV_A[Byte0(state[5])] ^ CipherTables.MUL_A[Byte3(state[0])] ^ state[14]; // v76 approximation
			keyScheduleOutput[17] = fsmReg1;
			keyScheduleOutput[18] = tempOutput18;
			keyScheduleOutput[19] = fsmReg2;
			keyScheduleOutput[36] = 16;

			// Copy to main state
			for (int j = 0; j < 37; j++)
			{
				_state[j + 1] = keyScheduleOutput[j];
			}
		}

		private void Sub423450()
		{
			// This is the keystream generation function - ported from Python
			uint v109 = _state[19] + _state[10];
			uint v125 = _state[13] ^ (_state[15] << 8) ^ (_state[4] >> 8) ^ CipherTables.DIV_A[Byte0(_state[4])] ^ CipherTables.MUL_A[Byte3(_state[15])];
			_state[15] = v125;
			uint v2 = _state[18];
			_state[18] = v109;

			uint v3 = CipherTables.S1_T3[Byte3(v2)] ^ CipherTables.S1_T1[Byte1(v2)] ^ CipherTables.S1_T2[Byte2(v2)];
			byte v4 = Byte0(v2);
			uint v5 = _state[14];
			uint v6 = CipherTables.S1_T0[v4] ^ v3;
			uint v7 = CipherTables.MUL_A[Byte3(v5)];
			uint v8 = _state[3];
			_state[20] = v5 ^ v6 ^ (v109 + v125);
			uint v9 = (v8 >> 8) ^ CipherTables.DIV_A[Byte0(v8)] ^ v7;
			byte v10 = Byte2(_state[18]);
			uint v11 = v6 + _state[9];
			uint v126 = _state[12] ^ (v5 << 8) ^ v9;
			_state[14] = v126;
			uint v12 = CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T0[Byte0(v109)] ^ CipherTables.S1_T2[v10];
			byte v13 = Byte1(_state[18]);
			_state[18] = v11;
			uint v14 = CipherTables.S1_T1[v13] ^ v12;
			uint v15 = v11 + v126;
			uint v16 = _state[13];
			_state[21] = v16 ^ v14 ^ v15;

			uint v17 = _state[2];
			uint v18 = _state[11];
			uint v124 = v18 ^ (v16 << 8) ^ (_state[2] >> 8) ^ CipherTables.DIV_A[Byte0(v17)] ^ CipherTables.MUL_A[Byte3(v16)];
			uint v19 = _state[8];
			_state[13] = v124;
			uint v20 = v14 + v19;
			v109 = v20;
			v17 = v6 + _state[9];
			uint v22 = CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T1[Byte1(_state[18])] ^ CipherTables.S1_T0[Byte0(v17)];
			byte v23 = Byte2(_state[18]);
			_state[18] = v20;
			uint v24 = CipherTables.S1_T2[v23] ^ v22;
			uint v25 = v20 + v124;
			uint v26 = _state[12];
			_state[22] = v24 ^ v26 ^ v25;

			uint v27 = (_state[1] >> 8) ^ (v26 << 8) ^ CipherTables.DIV_A[Byte0(_state[1])] ^ CipherTables.MUL_A[Byte3(v26)];
			uint v28 = _state[10];
			uint v29 = v28 ^ v27;
			uint v30 = _state[7];
			_state[12] = v29;
			uint v31 = v24 + v30;
			uint v123 = v29;
			uint v32 = CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T1[Byte1(_state[18])] ^ CipherTables.S1_T0[Byte0(v109)];
			byte v33 = Byte2(_state[18]);
			_state[18] = v31;
			uint v34 = CipherTables.S1_T2[v33] ^ v32;
			uint v35 = v31 + v29;
			uint v36 = _state[0];
			_state[23] = v34 ^ v18 ^ v35;

			uint v37 = _state[9] ^ (v36 >> 8) ^ (v18 << 8) ^ CipherTables.DIV_A[Byte0(v36)] ^ CipherTables.MUL_A[Byte3(v18)];
			uint v38 = _state[6];
			_state[11] = v37;
			uint v122 = v37;
			uint v39 = v34 + v38;
			v109 = v39;
			uint v40 = CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T1[Byte1(_state[18])] ^ CipherTables.S1_T0[Byte0(v31)];
			byte v41 = Byte2(_state[18]);
			_state[18] = v39;
			uint v42 = CipherTables.S1_T2[v41] ^ v40;
			_state[24] = v28 ^ v42 ^ (v39 + v37);

			uint v43 = _state[8] ^ (v28 << 8) ^ (v125 >> 8) ^ CipherTables.DIV_A[Byte0(v125)] ^ CipherTables.MUL_A[Byte3(v28)];
			uint v44 = _state[5];
			_state[10] = v43;
			uint v115 = v43;
			uint v45 = v42 + v44;
			uint v117 = v44;
			uint v47 = CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T1[Byte1(_state[18])] ^ CipherTables.S1_T0[Byte0(v109)];
			byte v48 = Byte2(_state[18]);
			_state[18] = v45;
			uint v49 = _state[9];
			uint v50 = CipherTables.S1_T2[v48] ^ v47;
			_state[25] = v49 ^ v50 ^ (v45 + v115);

			uint v51 = _state[7] ^ (_state[9] << 8) ^ (v126 >> 8) ^ CipherTables.DIV_A[Byte0(v126)] ^ CipherTables.MUL_A[Byte3(v49)];
			_state[9] = v51;
			uint v121 = v51;
			uint v110 = v50 + _state[4];
			uint v52 = CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T1[Byte1(_state[18])] ^ CipherTables.S1_T0[Byte0(v45)];
			byte v53 = Byte2(_state[18]);
			_state[18] = v110;
			uint v54 = _state[8];
			uint v55 = CipherTables.S1_T2[v53] ^ v52;
			_state[26] = v54 ^ v55 ^ (v110 + v121);

			uint v120 = v38 ^ (_state[8] << 8) ^ (v124 >> 8) ^ CipherTables.DIV_A[Byte0(v124)] ^ CipherTables.MUL_A[Byte3(v54)];
			_state[8] = v120;
			uint v56 = v55 + _state[3];
			uint v57 = CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T1[Byte1(_state[18])] ^ CipherTables.S1_T0[Byte0(v110)];
			byte v58 = Byte2(_state[18]);
			_state[18] = v56;
			uint v59 = _state[7];
			uint v60 = CipherTables.S1_T2[v58] ^ v57;
			_state[27] = v60 ^ v59 ^ (v56 + v120);

			uint v61 = v117;
			uint v119 = v117 ^ (_state[7] << 8) ^ (v123 >> 8) ^ CipherTables.DIV_A[Byte0(v123)] ^ CipherTables.MUL_A[Byte3(v59)];
			_state[7] = v119;
			uint v111 = v60 + _state[2];
			uint v62 = CipherTables.S1_T2[Byte2(_state[18])] ^ CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T0[Byte0(v56)] ^ CipherTables.S1_T1[Byte1(_state[18])];
			v56 = v111;
			_state[18] = v111;
			_state[28] = v62 ^ v38 ^ (v111 + v119);

			byte v63 = Byte1(_state[18]);
			uint v118 = _state[4] ^ (v38 << 8) ^ (v122 >> 8) ^ CipherTables.DIV_A[Byte0(v122)] ^ CipherTables.MUL_A[Byte3(v38)];
			_state[6] = v118;
			uint v112 = v62 + _state[1];
			uint v64 = CipherTables.S1_T2[Byte2(_state[18])] ^ CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T0[Byte0(v56)] ^ CipherTables.S1_T1[v63];
			uint v65 = CipherTables.MUL_A[Byte3(v61)];
			_state[18] = v112;
			_state[29] = v64 ^ v61 ^ (v118 + v112);

			uint v66 = _state[3] ^ (v115 >> 8) ^ (v61 << 8) ^ CipherTables.DIV_A[Byte0(v115)] ^ v65;
			_state[5] = v66;
			uint v67 = _state[0];
			uint v116 = v66;
			uint v68 = v64 + _state[0];
			uint v69 = _state[4];
			uint v70 = CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T0[Byte0(v112)] ^ CipherTables.S1_T1[Byte1(_state[18])];
			byte v71 = Byte2(_state[18]);
			_state[18] = v68;
			uint v72 = CipherTables.S1_T2[v71] ^ v70;
			_state[30] = v72 ^ v69 ^ (v68 + v116);

			uint v73 = _state[2] ^ (v121 >> 8) ^ (_state[4] << 8) ^ CipherTables.DIV_A[Byte0(v121)] ^ CipherTables.MUL_A[Byte3(v69)];
			_state[4] = v73;
			uint v75 = v72 + v125;
			uint v76 = CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T0[Byte0(v68)] ^ CipherTables.S1_T1[Byte1(_state[18])];
			byte v77 = Byte2(_state[18]);
			_state[18] = v75;
			uint v78 = CipherTables.S1_T2[v77] ^ v76;
			_state[31] = v78 ^ _state[3] ^ (v75 + v73);

			uint v80 = _state[1] ^ (v120 >> 8) ^ (_state[3] << 8) ^ CipherTables.DIV_A[Byte0(v120)] ^ CipherTables.MUL_A[Byte3(_state[3])];
			byte v81 = Byte1(_state[18]);
			_state[3] = v80;
			byte v82 = Byte0(v75);
			uint v83 = v78 + v126;
			uint v84 = CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T0[v82] ^ CipherTables.S1_T1[v81];
			byte v85 = Byte2(_state[18]);
			_state[18] = v83;
			uint v86 = CipherTables.S1_T2[v85] ^ v84;
			uint v87 = _state[2];
			_state[32] = v86 ^ v87 ^ (v83 + v80);

			uint v88 = v67 ^ (v119 >> 8) ^ (_state[2] << 8) ^ CipherTables.DIV_A[Byte0(v119)] ^ CipherTables.MUL_A[Byte3(v87)];
			uint v113 = v86 + v124;
			byte v89 = Byte1(_state[18]);
			_state[2] = v88;
			uint v90 = CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T0[Byte0(v83)] ^ CipherTables.S1_T1[v89];
			byte v91 = Byte2(_state[18]);
			_state[18] = v113;
			uint v92 = CipherTables.S1_T2[v91] ^ v90;
			uint v93 = _state[1];
			_state[33] = v93 ^ v92 ^ (v113 + v88);

			uint v94 = v125 ^ (_state[1] << 8) ^ (v118 >> 8) ^ CipherTables.DIV_A[Byte0(v118)] ^ CipherTables.MUL_A[Byte3(v93)];
			byte v95 = Byte1(_state[18]);
			_state[1] = v94;
			uint v96 = v92 + v123;
			uint v97 = CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T0[Byte0(v113)] ^ CipherTables.S1_T1[v95];
			byte v98 = Byte2(_state[18]);
			_state[18] = v96;
			uint v99 = CipherTables.S1_T2[v98] ^ v97;
			uint v100 = v67 ^ v99 ^ (v96 + v94);
			uint v101 = v122 + v99;
			_state[34] = v100;
			uint v102 = (_state[0] << 8) ^ (v116 >> 8) ^ CipherTables.DIV_A[Byte0(v116)] ^ CipherTables.MUL_A[Byte3(v67)];
			_state[17] = v101;
			uint v103 = v126 ^ v102;
			byte v104 = Byte1(_state[18]);
			_state[0] = v103;
			uint v105 = CipherTables.S1_T3[Byte3(_state[18])] ^ CipherTables.S1_T0[Byte0(v96)] ^ CipherTables.S1_T1[v104];
			byte v106 = Byte2(_state[18]);
			_state[18] = v101;
			uint v107 = CipherTables.S1_T2[v106] ^ v105;
			_state[19] = v107;
			_state[35] = v107 ^ v125 ^ (v101 + v103);
		}

		private void EnsureKeystream(int length)
		{
			while (_keystreamBuffer.Count < length)
			{
				uint counter = _state[37];
				if (counter == 16)
				{
					Sub423450();
					_state[37] = 0;
					counter = 0;
				}

				uint keystreamWord = _state[counter + 21];
				_state[37] = counter + 1;

				// Add bytes in little-endian order
				_keystreamBuffer.Add((byte)(keystreamWord & 0xFF));
				_keystreamBuffer.Add((byte)((keystreamWord >> 8) & 0xFF));
				_keystreamBuffer.Add((byte)((keystreamWord >> 16) & 0xFF));
				_keystreamBuffer.Add((byte)((keystreamWord >> 24) & 0xFF));
			}
		}

		/// <summary>
		/// Decrypt data using stream subtraction
		/// </summary>
		public byte[] StreamDecrypt(byte[] inputBytes, int length)
		{
			if (length == 0)
				return Array.Empty<byte>();

			EnsureKeystream(length);

			byte[] inputData = new byte[length];
			Array.Copy(inputBytes, 0, inputData, 0, Math.Min(inputBytes.Length, length));

			byte[] keystreamData = new byte[length];
			for (int i = 0; i < length; i++)
			{
				keystreamData[i] = _keystreamBuffer[i];
			}

			// Remove used keystream bytes
			_keystreamBuffer.RemoveRange(0, length);

			// Perform subtraction (input - keystream) mod 2^(length*8)
			// Using BigInteger for arbitrary precision
			BigInteger inputInt = new BigInteger(inputData, isUnsigned: true, isBigEndian: false);
			BigInteger keystreamInt = new BigInteger(keystreamData, isUnsigned: true, isBigEndian: false);

			BigInteger mask = (BigInteger.One << (length * 8)) - 1;
			BigInteger outputInt = (inputInt - keystreamInt) & mask;

			byte[] result = outputInt.ToByteArray(isUnsigned: true, isBigEndian: false);

			// Ensure result is exactly 'length' bytes
			if (result.Length < length)
			{
				byte[] padded = new byte[length];
				Array.Copy(result, 0, padded, 0, result.Length);
				return padded;
			}
			else if (result.Length > length)
			{
				byte[] truncated = new byte[length];
				Array.Copy(result, 0, truncated, 0, length);
				return truncated;
			}

			return result;
		}
	}

	/// <summary>
	/// Cipher lookup tables - PLACEHOLDER: These need to be filled with actual values from twfs_tables.py
	/// </summary>
	internal static class CipherTables
	{
		// MUL_A table - 256 entries of uint32
		public static readonly uint[] MUL_A = new uint[256];

		// DIV_A table - 256 entries of uint32
		public static readonly uint[] DIV_A = new uint[256];

		// S-box tables - 256 entries each of uint32
		public static readonly uint[] S1_T0 = new uint[256];
		public static readonly uint[] S1_T1 = new uint[256];
		public static readonly uint[] S1_T2 = new uint[256];
		public static readonly uint[] S1_T3 = new uint[256];

		static CipherTables()
		{
			// TODO: Initialize tables with actual values from twfs_tables.py
			// For now, these are placeholders that will need to be replaced
			// with the real values for the cipher to work correctly.

			// Example initialization (REPLACE WITH REAL VALUES):
			// MUL_A[0] = 0x00000000;
			// MUL_A[1] = 0xE22E00E2;
			// ... etc
		}
	}
}