using System;
using System.Collections.Generic;
using System.Text;

namespace TWConv
{
	internal class DatKeyCorrelator
	{
		// Common Magic Headers
		static readonly Dictionary<string, byte[]> KnownMagics = new Dictionary<string, byte[]>
		{
			{ "DDS ", new byte[] { 0x44, 0x44, 0x53, 0x20 } }, // Texture
            { "RIFF", new byte[] { 0x52, 0x49, 0x46, 0x46 } }, // Audio (Wav)
            { "FSB5", new byte[] { 0x46, 0x53, 0x42, 0x35 } }, // Audio (Fmod)
            { "OggS", new byte[] { 0x4F, 0x67, 0x67, 0x53 } }, // Audio (Ogg)
            { "G1M_", new byte[] { 0x47, 0x31, 0x4D, 0x5F } }, // Koei Model
            { "G1T_", new byte[] { 0x47, 0x31, 0x54, 0x5F } }, // Koei Texture
            { "KTSR", new byte[] { 0x4B, 0x54, 0x53, 0x52 } }, // Koei Sound
            { "PNG ", new byte[] { 0x89, 0x50, 0x4E, 0x47 } }, // Image
            { "KTSS", new byte[] { 0x4B, 0x54, 0x53, 0x53 } }, // Koei Sound Stream
            { "KTSL", new byte[] { 0x4B, 0x54, 0x53, 0x4C } }, // Koei Sound Loop
        };

		public static void Run(string[] args)
		{
			string folder = args.Length > 0 ? args[0] : @"C:\Nexon\TalesWeaver\DATA\";//AppDomain.CurrentDomain.BaseDirectory;
			var files = Directory.GetFiles(folder, "*.DAT");

			if (files.Length == 0)
			{
				Console.WriteLine("No DAT files found.");
				return;
			}

			Console.WriteLine($"Loaded {files.Length} files. Starting Correlation Attack...");
			Console.WriteLine("Trying to find a Global Key that works across multiple files...\n");

			// Cache the first 4 bytes of every file to speed up processing
			var fileHeaders = new List<Tuple<string, byte[]>>();
			foreach (var f in files)
			{
				byte[] h = new byte[4];
				using (var fs = File.OpenRead(f)) fs.Read(h, 0, 4);
				fileHeaders.Add(new Tuple<string, byte[]>(Path.GetFileName(f), h));
			}

			// The Brute Force Loop
			// For every file...
			foreach (var sourceFile in fileHeaders)
			{
				// For every possible magic type it *could* be...
				foreach (var magic in KnownMagics)
				{
					// Calculate the Candidate Key
					byte[] candidateKey = new byte[4];
					for (int i = 0; i < 4; i++) candidateKey[i] = (byte)(sourceFile.Item2[i] ^ magic.Value[i]);

					// Now check this Key against ALL other files
					int score = 0;
					List<string> matches = new List<string>();

					foreach (var targetFile in fileHeaders)
					{
						if (targetFile.Item1 == sourceFile.Item1) continue;

						// Decrypt target header with candidate key
						byte[] decrypted = new byte[4];
						for (int i = 0; i < 4; i++) decrypted[i] = (byte)(targetFile.Item2[i] ^ candidateKey[i]);

						// Check if decrypted header matches ANY known magic
						foreach (var m in KnownMagics)
						{
							if (decrypted.SequenceEqual(m.Value))
							{
								score++;
								matches.Add($"{targetFile.Item1} -> {m.Key}");
								break;
							}
						}
					}

					// If we found a key that works for at least 3 other files, it's probably THE key
					if (score > 2)
					{
						Console.WriteLine("!!! POTENTIAL KEY FOUND !!!");
						Console.WriteLine($"Source: Assumed {sourceFile.Item1} is {magic.Key}");
						Console.WriteLine($"Key Bytes: {BitConverter.ToString(candidateKey)}");
						Console.WriteLine($"Score: {score} other files validated.");
						Console.WriteLine("Matches:");
						foreach (var m in matches.Take(5)) Console.WriteLine("  " + m);
						Console.WriteLine("...");
						Console.WriteLine("------------------------------------------------");

						// We assume the first valid high-score key is correct and stop to save time
						// You can remove this 'return' to see all possibilities
						return;
					}
				}
			}

			Console.WriteLine("Analysis complete. If no key was found, the files might use a custom header or non-standard magic.");
		}
	}
}
