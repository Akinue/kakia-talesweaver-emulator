using System;
using System.Collections.Generic;
using System.Text;

namespace TWConv
{
	internal class FinalDatDecryptor
	{
		// A standard DDS Header (First 32 bytes are usually predictable)
		// Magic: DDS_
		// Size: 124 (0x7C)
		// Flags: Caps | Height | Width | PixelFormat (0x1007 or similar)
		// Since we only need to ID the other files, 4-8 bytes of Key is often enough, 
		// but we will try to recover as much as possible.
		static readonly byte[] DDS_TEMPLATE = new byte[]
		{
			0x44, 0x44, 0x53, 0x20, // 'DDS '
            0x7C, 0x00, 0x00, 0x00, // Size = 124
            0x07, 0x10, 0x00, 0x00, // Flags (Common)
            0x00, 0x00, 0x00, 0x00  // Height (Variable, but often 00 in upper bytes)
        };

		public static void Run(string[] args)
		{
			Console.WriteLine("=== DAT Final Decryptor ===");
			string folder = args.Length > 0 ? args[0] : @"C:\Nexon\TalesWeaver\DATA\";//AppDomain.CurrentDomain.BaseDirectory;

			string keyFile = Path.Combine(folder, "DA_00000.DAT");
			if (!File.Exists(keyFile))
			{
				Console.WriteLine("Error: DA_00000.DAT is required to derive the key.");
				return;
			}

			// 1. Recover Global Key from DA_00000
			byte[] cipher0 = File.ReadAllBytes(keyFile);
			byte[] globalKey = new byte[DDS_TEMPLATE.Length];

			Console.WriteLine("[*] Deriving Global Key from DA_00000.DAT...");
			for (int i = 0; i < DDS_TEMPLATE.Length; i++)
			{
				// Key = Cipher ^ Plain(DDS)
				globalKey[i] = (byte)(cipher0[i] ^ DDS_TEMPLATE[i]);
			}
			Console.WriteLine($"    Key recovered (Hex): {BitConverter.ToString(globalKey)}");

			// 2. Process Files
			string outDir = Path.Combine(folder, "Decrypted_Headers");
			Directory.CreateDirectory(outDir);

			var files = Directory.GetFiles(folder, "*.DAT");
			foreach (var file in files)
			{
				try
				{
					DecryptFileHeader(file, globalKey, outDir);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error processing {Path.GetFileName(file)}: {ex.Message}");
				}
			}
		}

		static void DecryptFileHeader(string filePath, byte[] key, string outDir)
		{
			string fileName = Path.GetFileName(filePath);
			byte[] data = File.ReadAllBytes(filePath);

			// A. Decrypt The Filename (0x129)
			// We just overwrite it with the clean name because we know what it should be.
			byte[] cleanName = Encoding.ASCII.GetBytes(fileName);
			Array.Resize(ref cleanName, 13);
			cleanName[12] = 0x00; // Null terminator
			Array.Copy(cleanName, 0, data, 0x129, 13);

			// B. Decrypt The Header (First N bytes) using the Derived Key
			int decryptLen = Math.Min(data.Length, key.Length);
			for (int i = 0; i < decryptLen; i++)
			{
				// Only decrypt the header area, leave filename area (0x129) alone as we just fixed it
				if (i >= 0x129 && i < 0x129 + 13) continue;

				data[i] = (byte)(data[i] ^ key[i]);
			}

			// C. Save
			string outPath = Path.Combine(outDir, fileName);
			File.WriteAllBytes(outPath, data);

			// D. Check Magic
			string magic = Encoding.ASCII.GetString(data, 0, 4);
			// Cleanup non-printable for display
			string displayMagic = "";
			foreach (char c in magic) displayMagic += (char.IsLetterOrDigit(c) || char.IsPunctuation(c)) ? c : '.';

			Console.WriteLine($"[+] {fileName} -> Header Magic: {displayMagic}");
		}
	}
}
