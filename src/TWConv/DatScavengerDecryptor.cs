using System;
using System.Collections.Generic;
using System.Text;

namespace TWConv
{
	internal class DatScavengerDecryptor
	{
		// EXPANDED DICTIONARY: Covers Models, Textures, Animations, Effects
		static readonly Dictionary<string, byte[]> KnownMagics = new Dictionary<string, byte[]>
		{
			{ "DDS ", new byte[] { 0x44, 0x44, 0x53, 0x20 } },
			{ "KTSR", new byte[] { 0x4B, 0x54, 0x53, 0x52 } }, // Sound
            { "RIFF", new byte[] { 0x52, 0x49, 0x46, 0x46 } }, // Audio
            { "G1M_", new byte[] { 0x47, 0x31, 0x4D, 0x5F } }, // Model
            { "G1T_", new byte[] { 0x47, 0x31, 0x54, 0x5F } }, // Texture Container
            { "G1A_", new byte[] { 0x47, 0x31, 0x41, 0x5F } }, // Animation
            { "G1E_", new byte[] { 0x47, 0x31, 0x45, 0x5F } }, // Effect
            { "G2A_", new byte[] { 0x47, 0x32, 0x41, 0x5F } }, // Animation v2
            { "G1F_", new byte[] { 0x47, 0x31, 0x46, 0x5F } }, // Font/Frame?
            { "LNK ", new byte[] { 0x4C, 0x4E, 0x4B, 0x20 } }, // Link
            { "LINK", new byte[] { 0x4C, 0x49, 0x4E, 0x4B } }, // Link
            { "COLL", new byte[] { 0x43, 0x4F, 0x4C, 0x4C } }, // Collision
            { "DB__", new byte[] { 0x44, 0x42, 0x00, 0x00 } }, // Database
            { "FSB5", new byte[] { 0x46, 0x53, 0x42, 0x35 } }  // Audio
        };

		public static void Run(string[] args)
		{
			Console.WriteLine("=== DAT Scavenger Decryptor (Final) ===");
			string folder = args.Length > 0 ? args[0] : @"C:\Nexon\TalesWeaver\DATA\";//AppDomain.CurrentDomain.BaseDirectory;
			string outDir = Path.Combine(folder, "Decrypted_Full");
			Directory.CreateDirectory(outDir);

			var files = Directory.GetFiles(folder, "*.DAT");

			// 1. Initialize Key Ring with known good keys
			List<byte[]> MasterKeys = new List<byte[]>();

			// Attempt to load Key A (DDS)
			if (File.Exists(Path.Combine(folder, "DA_00000.DAT")))
			{
				byte[] ka = DeriveKey(File.ReadAllBytes(Path.Combine(folder, "DA_00000.DAT")), KnownMagics["DDS "]);
				if (ka != null) MasterKeys.Add(ka);
			}

			// Attempt to load Key B (KTSR/RIFF) - Likely Zero/Null Key
			// We just synthesize a null key because we saw many RIFFs work with it
			MasterKeys.Add(new byte[] { 0x00, 0x00, 0x00, 0x00 });

			Console.WriteLine($"[Init] Loaded {MasterKeys.Count} base keys.");
			Console.WriteLine("---------------------------------------------");

			int successCount = 0;
			int totalFiles = 0;

			foreach (var file in files)
			{
				totalFiles++;
				byte[] data = File.ReadAllBytes(file);
				if (data.Length < 16) continue;

				byte[] validKey = null;
				string detectedMagic = "????";

				// Phase 1: Try Existing Keys
				foreach (var key in MasterKeys)
				{
					string m = CheckMagic(data, key);
					if (m != "????")
					{
						validKey = key;
						detectedMagic = m;
						break;
					}
				}

				// Phase 2: Scavenge (If no existing key worked)
				if (validKey == null)
				{
					foreach (var magicEntry in KnownMagics)
					{
						// Assume this file is this Magic Type, calculate the key
						byte[] newKey = DeriveKey(data, magicEntry.Value);

						// Validate the key by checking bytes 4-12
						// Valid Koei files usually have 0x00 padding or small ints here
						if (ValidateDeep(data, newKey))
						{
							Console.WriteLine($"[!!!] NEW KEY DISCOVERED from {Path.GetFileName(file)} ({magicEntry.Key})");
							Console.WriteLine($"      Key: {BitConverter.ToString(newKey)}");

							validKey = newKey;
							detectedMagic = magicEntry.Key;
							MasterKeys.Add(newKey); // Add to keyring for future files
							break;
						}
					}
				}

				// Phase 3: Decrypt
				if (validKey != null)
				{
					// Repair Filename
					byte[] nameBytes = Encoding.ASCII.GetBytes(Path.GetFileName(file));
					Array.Resize(ref nameBytes, 13);
					nameBytes[12] = 0;
					if (data.Length > 0x129 + 13)
						Array.Copy(nameBytes, 0, data, 0x129, 13);

					// XOR Body
					for (int i = 0; i < data.Length; i++)
					{
						if (i >= 0x129 && i < 0x129 + 13) continue;
						data[i] ^= validKey[i % 4];
					}

					string outPath = Path.Combine(outDir, Path.GetFileName(file));
					File.WriteAllBytes(outPath, data);
					Console.WriteLine($"[OK] {Path.GetFileName(file)} -> {detectedMagic}");
					successCount++;
				}
				else
				{
					Console.WriteLine($"[FAIL] {Path.GetFileName(file)} - Unknown Format");
				}
			}

			Console.WriteLine("\n---------------------------------------------");
			Console.WriteLine($"Decryption Complete. Success: {successCount} / {totalFiles}");
			Console.WriteLine($"Files saved to: {outDir}");
		}

		static byte[] DeriveKey(byte[] cipher, byte[] plain)
		{
			if (cipher.Length < 4) return null;
			byte[] k = new byte[4];
			for (int i = 0; i < 4; i++) k[i] = (byte)(cipher[i] ^ plain[i]);
			return k;
		}

		static string CheckMagic(byte[] data, byte[] key)
		{
			byte[] dec = new byte[4];
			for (int i = 0; i < 4; i++) dec[i] = (byte)(data[i] ^ key[i % 4]);

			foreach (var kvp in KnownMagics)
			{
				if (dec.SequenceEqual(kvp.Value))
				{
					// Secondary check: Header should be sane
					if (ValidateDeep(data, key)) return kvp.Key;
				}
			}
			return "????";
		}

		static bool ValidateDeep(byte[] data, byte[] key)
		{
			// Decrypt 16 bytes
			byte[] dec = new byte[16];
			for (int i = 0; i < 16; i++) dec[i] = (byte)(data[i] ^ key[i % 4]);

			// Heuristic 1: RIFF/FSB5 doesn't always have zeros, but G1M/G1T/DDS DO.
			// If it's a known textual magic (RIFF), we are lenient.
			string head = Encoding.ASCII.GetString(dec, 0, 4);
			if (head == "RIFF" || head == "FSB5" || head == "OggS") return true;

			// Heuristic 2: For G1M/G1T/G1A, we expect bytes 4,5,6,7 to be a Size or Count.
			// In Koei games, these are usually Little Endian integers.
			// If it's a small file, the upper bytes (6,7) should be 00.
			if (dec[6] == 0x00 && dec[7] == 0x00) return true;
			if (dec[4] == 0x00 && dec[5] == 0x00) return true; // Sometimes size is at 8, padding at 4

			// Heuristic 3: Check for high entropy "garbage". If bytes 4-16 look random (>0xF0 often), fail.
			// This is loose, but helps avoid false positives.

			return false;
		}
	}
}
