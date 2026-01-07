using System;
using System.Collections.Generic;
using System.Text;

namespace TWConv
{
	internal class DatKeyCracker
	{
		public static void Run(string[] args)
		{
			string folder = args.Length > 0 ? args[0] : @"C:\Nexon\TalesWeaver\DATA\";//AppDomain.CurrentDomain.BaseDirectory;
			string targetFile = Path.Combine(folder, "DA_00004.DAT"); // Try DA_00002 if 0 is boring

			if (!File.Exists(targetFile))
			{
				Console.WriteLine("Please copy this tool into the folder with DA_00000.DAT");
				return;
			}

			byte[] cipher = File.ReadAllBytes(targetFile);

			// Common Magic Signatures for games (Koei, generic, etc.)
			var signatures = new Dictionary<string, byte[]>
			{
				{ "G1M_ (Koei Model)",  new byte[] { 0x47, 0x31, 0x4D, 0x5F } },
				{ "G1T_ (Koei Texture)",new byte[] { 0x47, 0x31, 0x54, 0x5F } },
				{ "KTSR (Koei Sound)",  new byte[] { 0x4B, 0x54, 0x53, 0x52 } },
				{ "DDS (Texture)",      new byte[] { 0x44, 0x44, 0x53, 0x20 } },
				{ "RIFF (Audio)",       new byte[] { 0x52, 0x49, 0x46, 0x46 } },
				{ "FSB5 (Audio)",       new byte[] { 0x46, 0x53, 0x42, 0x35 } },
				{ "OggS (Audio)",       new byte[] { 0x4F, 0x67, 0x67, 0x53 } },
				{ "PNG (Image)",        new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
				{ "Empty (Zeros)",      new byte[] { 0x00, 0x00, 0x00, 0x00 } }
			};

			Console.WriteLine($"Analyzing {Path.GetFileName(targetFile)}...");
			Console.WriteLine($"First 4 Bytes Cipher: {BitConverter.ToString(cipher, 0, 4)}");
			Console.WriteLine("------------------------------------------------");

			foreach (var sig in signatures)
			{
				// Calculate potential Key based on signature
				// Key = Cipher ^ Signature
				byte[] key = new byte[4];
				for (int i = 0; i < 4; i++) key[i] = (byte)(cipher[i] ^ sig.Value[i]);

				Console.WriteLine($"Testing Sig: {sig.Key}");
				Console.WriteLine($"  -> Potential Key: {BitConverter.ToString(key)}");

				// Preview Decryption with this Key
				Console.Write("  -> Preview: ");
				for (int i = 0; i < 16; i++)
				{
					byte b = (byte)(cipher[i] ^ key[i % 4]); // Assume 4-byte repeating key
					char c = (b >= 0x20 && b <= 0x7E) ? (char)b : '.';
					Console.Write(c);
				}
				Console.WriteLine("\n");
			}
		}
	}
}
