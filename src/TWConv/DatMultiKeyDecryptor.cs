using System;
using System.Collections.Generic;
using System.Text;

namespace TWConv
{
	internal class DatMultiKeyDecryptor
	{
		// 1. KNOWN MAGICS (Signatures to look for)
		static readonly Dictionary<string, byte[]> KnownMagics = new Dictionary<string, byte[]>
		{
			{ "DDS ", new byte[] { 0x44, 0x44, 0x53, 0x20 } },
			{ "G1M_", new byte[] { 0x47, 0x31, 0x4D, 0x5F } }, // Koei Model
            { "G1T_", new byte[] { 0x47, 0x31, 0x54, 0x5F } }, // Koei Texture
            { "KTSR", new byte[] { 0x4B, 0x54, 0x53, 0x52 } }, // Koei Sound
            { "RIFF", new byte[] { 0x52, 0x49, 0x46, 0x46 } }, // Wav
            { "FSB5", new byte[] { 0x46, 0x53, 0x42, 0x35 } }, // FMOD Audio
            { "OggS", new byte[] { 0x4F, 0x67, 0x67, 0x53 } }, // Ogg Audio
            { "PNG ", new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
			{ "ARC ", new byte[] { 0x41, 0x52, 0x43, 0x00 } },
			{ "LINK", new byte[] { 0x4C, 0x49, 0x4E, 0x4B } },
			{ "G1E_", new byte[] { 0x47, 0x31, 0x45, 0x5F } }, // Koei Effect?
            { "G2A_", new byte[] { 0x47, 0x32, 0x41, 0x5F } }, // Koei Anim?
        };

		public static void Run(string[] args)
		{
			Console.WriteLine("=== DAT Multi-Key Decryptor ===");
			string folder = args.Length > 0 ? args[0] : @"C:\Nexon\TalesWeaver\DATA\";//AppDomain.CurrentDomain.BaseDirectory;
			string outDir = Path.Combine(folder, "Decrypted_Auto");
			Directory.CreateDirectory(outDir);

			var files = Directory.GetFiles(folder, "*.DAT");

			// Dictionary to store derived keys for each unique ciphertext header
			// Key: First 16 bytes of Ciphertext -> Value: Decryption Key (if found)
			var keyCache = new Dictionary<string, byte[]>();

			int successCount = 0;

			foreach (var file in files)
			{
				byte[] data = File.ReadAllBytes(file);
				if (data.Length < 16) continue;

				// 1. Get Cipher Header (Signature)
				string cipherSig = BitConverter.ToString(data, 0, 16);
				byte[] validKey = null;

				// 2. Check Cache
				if (keyCache.ContainsKey(cipherSig))
				{
					validKey = keyCache[cipherSig];
				}
				else
				{
					// 3. Discovery Mode: Try to find a key for this file type
					foreach (var magic in KnownMagics)
					{
						// Calculate potential Key: Key = Cipher ^ Magic
						byte[] tempKey = new byte[4];
						for (int i = 0; i < 4; i++) tempKey[i] = (byte)(data[i] ^ magic.Value[i]);

						// Verify Key: Decrypt next 12 bytes and see if they look sane
						// "Sane" usually means 00s (padding) or small integers (sizes)
						bool looksValid = true;
						int zeros = 0;
						for (int j = 4; j < 16; j++)
						{
							byte dec = (byte)(data[j] ^ tempKey[j % 4]);
							if (dec == 0x00) zeros++;
							// Simple heuristic: If we see high entropy byte > 0xF0 immediately after magic, suspect bad key
							// Unless it's PNG which has standard header
						}

						// Stronger filter: G1M, G1T, DDS usually have 00 00 00 00 at offset 4 or 8
						if (zeros >= 2 || magic.Key == "PNG " || magic.Key == "OggS")
						{
							Console.WriteLine($"[!] Found Key for {Path.GetFileName(file)} -> Magic: {magic.Key}");
							validKey = tempKey;
							keyCache[cipherSig] = validKey;
							break;
						}
					}
				}

				// 4. Decrypt and Save
				if (validKey != null)
				{
					// Repair Filename (0x129)
					byte[] cleanName = Encoding.ASCII.GetBytes(Path.GetFileName(file));
					Array.Resize(ref cleanName, 13);
					cleanName[12] = 0x00;
					if (data.Length > 0x129 + 13)
						Array.Copy(cleanName, 0, data, 0x129, 13);

					// Decrypt Body (Skip filename region)
					for (int i = 0; i < data.Length; i++)
					{
						if (i >= 0x129 && i < 0x129 + 13) continue;
						data[i] ^= validKey[i % 4];
					}

					string outPath = Path.Combine(outDir, Path.GetFileName(file));
					File.WriteAllBytes(outPath, data);
					successCount++;
				}
				else
				{
					Console.WriteLine($"[-] No key found for {Path.GetFileName(file)} (Cipher Start: {data[0]:X2} {data[1]:X2}...)");
				}
			}

			Console.WriteLine($"\nFinished. Decrypted {successCount}/{files.Length} files.");
		}
	}
}
