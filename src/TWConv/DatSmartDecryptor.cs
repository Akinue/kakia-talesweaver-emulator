using System;
using System.Collections.Generic;
using System.Text;

namespace TWConv
{
	internal class DatSmartDecryptor
	{
		// Headers we trust to define a Key
		static readonly Dictionary<string, byte[]> KnownMagics = new Dictionary<string, byte[]>
		{
			{ "DDS ", new byte[] { 0x44, 0x44, 0x53, 0x20 } },
			{ "KTSR", new byte[] { 0x4B, 0x54, 0x53, 0x52 } }, // Sound
            { "G1M_", new byte[] { 0x47, 0x31, 0x4D, 0x5F } }, // Model
            { "G1T_", new byte[] { 0x47, 0x31, 0x54, 0x5F } }, // Texture
            { "G1A_", new byte[] { 0x47, 0x31, 0x41, 0x5F } }, // Animation
            { "G1E_", new byte[] { 0x47, 0x31, 0x45, 0x5F } }, // Effect
            { "RIFF", new byte[] { 0x52, 0x49, 0x46, 0x46 } }, // Wav
            { "FSB5", new byte[] { 0x46, 0x53, 0x42, 0x35 } }, // Fmod
        };

		public static void Run(string[] args)
		{
			Console.WriteLine("=== DAT Smart Decryptor ===");
			string folder = args.Length > 0 ? args[0] : @"C:\Nexon\TalesWeaver\DATA\";//AppDomain.CurrentDomain.BaseDirectory;
			string outDir = Path.Combine(folder, "Decrypted_Final");
			Directory.CreateDirectory(outDir);

			var files = Directory.GetFiles(folder, "*.DAT");

			// 1. Harvest Keys from "Anchor Files"
			// We know DA_00000 is DDS, and DF_00244 is KTSR.
			List<byte[]> MasterKeys = new List<byte[]>();

			Console.WriteLine("[Phase 1] Harvesting Keys from known files...");

			// Harvest Key A (DDS)
			string fileA = Path.Combine(folder, "DA_00000.DAT");
			if (File.Exists(fileA))
			{
				byte[] keyA = DeriveKey(File.ReadAllBytes(fileA), KnownMagics["DDS "]);
				if (keyA != null)
				{
					MasterKeys.Add(keyA);
					Console.WriteLine($"[+] Harvested Key A (DDS) from DA_00000");
				}
			}

			// Harvest Key B (KTSR)
			string fileB = Path.Combine(folder, "DF_00244.DAT");
			if (File.Exists(fileB))
			{
				byte[] keyB = DeriveKey(File.ReadAllBytes(fileB), KnownMagics["KTSR"]);
				if (keyB != null)
				{
					MasterKeys.Add(keyB);
					Console.WriteLine($"[+] Harvested Key B (KTSR) from DF_00244");
				}
			}

			// Fallback: If DF_00244 is missing, try to find ANY KTSR
			if (MasterKeys.Count < 2)
			{
				foreach (var f in files)
				{
					byte[] data = File.ReadAllBytes(f);
					if (data.Length < 16) continue;
					byte[] derived = DeriveKey(data, KnownMagics["KTSR"]);
					// Verify: KTSR usually has 00 00 00 00 at offset 4 or 8 or 12
					if (CheckQuality(data, derived, "KTSR"))
					{
						MasterKeys.Add(derived);
						Console.WriteLine($"[+] Harvested Key B (KTSR) from {Path.GetFileName(f)}");
						break;
					}
				}
			}

			Console.WriteLine($"Total Master Keys: {MasterKeys.Count}");
			Console.WriteLine("---------------------------------------------");

			// 2. Decrypt All Files
			int successCount = 0;
			foreach (var file in files)
			{
				byte[] data = File.ReadAllBytes(file);
				if (data.Length < 16) continue;

				byte[] bestKey = null;
				string bestMagic = "????";

				// Try all Master Keys
				foreach (var key in MasterKeys)
				{
					string magic = DecryptAndCheck(data, key);
					if (magic != "????")
					{
						bestKey = key;
						bestMagic = magic;
						break;
					}
				}

				// If no Master Key worked, try brute forcing a NEW key
				if (bestKey == null)
				{
					// Try finding G1M, G1T, etc.
					foreach (var kvp in KnownMagics)
					{
						byte[] potentialKey = DeriveKey(data, kvp.Value);
						if (CheckQuality(data, potentialKey, kvp.Key))
						{
							bestKey = potentialKey;
							bestMagic = kvp.Key;
							// Optionally add to MasterKeys?
							break;
						}
					}
				}

				// 3. Save Result
				if (bestKey != null)
				{
					// Repair Filename (0x129)
					byte[] nameBytes = Encoding.ASCII.GetBytes(Path.GetFileName(file));
					Array.Resize(ref nameBytes, 13);
					nameBytes[12] = 0;
					if (data.Length > 0x129 + 13)
						Array.Copy(nameBytes, 0, data, 0x129, 13);

					// Decrypt Body
					for (int i = 0; i < data.Length; i++)
					{
						if (i >= 0x129 && i < 0x129 + 13) continue; // Skip filename
						data[i] ^= bestKey[i % 4];
					}

					File.WriteAllBytes(Path.Combine(outDir, Path.GetFileName(file)), data);
					Console.WriteLine($"[OK] {Path.GetFileName(file)} -> {bestMagic}");
					successCount++;
				}
				else
				{
					// If failed, print what the header looks like with Key A (DDS) just for debugging
					if (MasterKeys.Count > 0)
					{
						byte[] preview = new byte[4];
						for (int i = 0; i < 4; i++) preview[i] = (byte)(data[i] ^ MasterKeys[0][i]);
						string pStr = Encoding.ASCII.GetString(preview.Select(b => (b < 32 || b > 126) ? (byte)46 : b).ToArray());
						Console.WriteLine($"[FAIL] {Path.GetFileName(file)} (With Key A: {pStr})");
					}
				}
			}
			Console.WriteLine($"\nCompleted. {successCount}/{files.Length} files decrypted.");
		}

		static byte[] DeriveKey(byte[] cipher, byte[] plain)
		{
			if (cipher.Length < 4) return null;
			byte[] k = new byte[4];
			for (int i = 0; i < 4; i++) k[i] = (byte)(cipher[i] ^ plain[i]);
			return k;
		}

		// Check if a key produces a valid looking header (Zeros or ASCII)
		static bool CheckQuality(byte[] cipher, byte[] key, string magicName)
		{
			// Decrypt first 16 bytes
			byte[] dec = new byte[16];
			for (int i = 0; i < 16; i++) dec[i] = (byte)(cipher[i] ^ key[i % 4]);

			// KTSR/G1M/G1T/DDS usually have 0x00 padding in the first 16 bytes
			int zeros = 0;
			for (int i = 4; i < 16; i++) if (dec[i] == 0x00) zeros++;

			// Strict check
			if (zeros >= 2) return true;

			// RIFF check (byte 8 should be 'W' or similar, not rigorous)
			if (magicName == "RIFF" && dec[8] > 0x40) return true;

			return false;
		}

		static string DecryptAndCheck(byte[] data, byte[] key)
		{
			byte[] dec = new byte[4];
			for (int i = 0; i < 4; i++) dec[i] = (byte)(data[i] ^ key[i % 4]);

			// Check if matches known magic
			foreach (var kvp in KnownMagics)
			{
				if (dec.SequenceEqual(kvp.Value))
				{
					// Double check quality to avoid false positives (like random collision)
					if (CheckQuality(data, key, kvp.Key)) return kvp.Key;
				}
			}
			return "????";
		}
	}
}
