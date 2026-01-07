using System;
using System.Collections.Generic;
using System.Text;

namespace TWConv
{
	internal class DatDeepInspector
	{
		public static void Run(string[] args)
		{
			string folder = args.Length > 0 ? args[0] : @"C:\Nexon\TalesWeaver\DATA\";//AppDomain.CurrentDomain.BaseDirectory;
			string targetPath = Path.Combine(folder, "DA_00002.DAT"); // Using File 2 (Real Asset)

			if (!File.Exists(targetPath))
			{
				Console.WriteLine("Error: DA_00002.DAT not found.");
				return;
			}

			byte[] cipher = File.ReadAllBytes(targetPath);
			Console.WriteLine($"Analyzing {Path.GetFileName(targetPath)}...");
			Console.WriteLine($"Cipher Header: {BitConverter.ToString(cipher, 0, 8)}...");
			Console.WriteLine("----------------------------------------------------------------");

			// Common headers to test
			var magics = new Dictionary<string, byte[]>
			{
				{ "G1M_ (Model)",   new byte[] { 0x47, 0x31, 0x4D, 0x5F } },
				{ "G1T_ (Texture)", new byte[] { 0x47, 0x31, 0x54, 0x5F } },
				{ "KTSR (Sound)",   new byte[] { 0x4B, 0x54, 0x53, 0x52 } },
				{ "DDS  (Texture)", new byte[] { 0x44, 0x44, 0x53, 0x20 } },
				{ "RIFF (Audio)",   new byte[] { 0x52, 0x49, 0x46, 0x46 } },
				{ "FSB5 (Audio)",   new byte[] { 0x46, 0x53, 0x42, 0x35 } },
				{ "OggS (Audio)",   new byte[] { 0x4F, 0x67, 0x67, 0x53 } },
				{ "PNG  (Image)",   new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
                // Common Archive types
                { "ARC (Archive)",  new byte[] { 0x41, 0x52, 0x43, 0x00 } },
				{ "LINK (Archive)", new byte[] { 0x4C, 0x49, 0x4E, 0x4B } },
			};

			foreach (var m in magics)
			{
				// 1. Derive Key from the first 4 bytes
				byte[] key = new byte[4];
				for (int i = 0; i < 4; i++) key[i] = (byte)(cipher[i] ^ m.Value[i]);

				// 2. Decrypt next 32 bytes using this key
				byte[] preview = new byte[32];
				for (int i = 0; i < 32; i++)
				{
					preview[i] = (byte)(cipher[i] ^ key[i % 4]);
				}

				Console.WriteLine($"Assume Magic: {m.Key}");
				Console.WriteLine($"Derived Key : {BitConverter.ToString(key)}");
				Console.Write("Decrypted   : ");

				// Print Hex
				for (int i = 0; i < 16; i++) Console.Write($"{preview[i]:X2} ");
				Console.WriteLine();

				// Print ASCII
				Console.Write("ASCII View  : ");
				for (int i = 0; i < 32; i++)
				{
					char c = (char)preview[i];
					if (c < 0x20 || c > 0x7E) c = '.';
					Console.Write(c);
				}
				Console.WriteLine("\n");
			}
		}
	}
}
