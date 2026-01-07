using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using TWConv;

// Ensure EntityToScriptConverter is accessible

namespace DatBatchDecryptor
{
	class Program
	{
		// CONFIGURATION
		const int HEADER_SIZE = 0x129; // 297 bytes
		const int FILENAME_OFFSET = 0x129;
		const int FILENAME_LENGTH = 13; // "DA_xxxxx.DAT" + 0x00
		const int BODY_OFFSET = 0x136;

		// The Encrypted Header found in your sample (DA_00000.DAT).
		// We use this to verify other files belong to the same archive type.
		static readonly byte[] REFERENCE_HEADER = StringToByteArray(
			"0266FD673D07851266568999034CBC43F2D5324F1C860B52964D44E62106DE87" +
			"E705655C28E8B7829D9316F1B4B156F9C9AE5F0FE0430B7B5AABCFEEC29DF060" +
			"7701B1FCE6601A0A1EAFB0886F199CE3F54A1C71A5CFA5A3F9E9017C18C7BE00" +
			"A1B579B2005913699455EF3192810B410EEBF877817EB9A65B0D02FB5E9DF155" +
			"EFB0E5B89EE9E39C36B2DC50DE741077DDBF6D2BCE477D23FBD157F156160BF5" +
			"639A41E19B51A224075C40CEA069C7E6FB8A1D9E2FC586A832C1BCCB91F04520" +
			"A6E5EC43322EDB8C1C48247106AE82920A70F5CBF76BC69C1398AFD893D59215" +
			"162027B97C83B3CDAD9081E5A114A1823F2AC120FA79273B1F4F30987E02F746" +
			"B50E5BE487C71685BB281D67E9DFFAFEF52C996913711A9CF8EA992C531A4EAB" +
			"983A3092B8BCFA7614"
		);

		// TODO: If you find the Global XOR key for the Header/Body, enter it here.
		// Currently empty, meaning Header and Body are passed through as-is.
		static readonly byte[] GlobalKey = { };

		static void Main(string[] args)
		{
			// Check for entity converter command
			if (args.Length > 0 && args[0].Equals("--entities", StringComparison.OrdinalIgnoreCase))
			{
				EntityToScriptConverter.Run(args.Skip(1).ToArray());
				return;
			}
			EntityToScriptConverter.Run(args.Skip(1).ToArray());

			//TalesWeaverDatDecryptor.Run(args);
			//DatSmartDecryptor.Run(args);
			//DatMultiKeyDecryptor.Run(args);
			//FinalDatDecryptor.Run(args);
			//DatDeepInspector.Run(args);
			//DatKeyCorrelator.Run(args);
			//DatKeyCracker.Run(args);
			return;
			DatHeaderAnalyzer.Run(args);

			Console.WriteLine("=== DAT Folder Decryptor ===");

			string folderPath = args.Length > 0 ? args[0] : @"C:\Nexon\TalesWeaver\DATA\"; //AppDomain.CurrentDomain.BaseDirectory;

			// Allow manual entry if not running from CLI with args
			if (args.Length == 0)
			{
				Console.Write("Enter folder path (press Enter for current dir): ");
				string input = Console.ReadLine();
				if (!string.IsNullOrWhiteSpace(input)) folderPath = input;
			}

			if (!Directory.Exists(folderPath))
			{
				Console.WriteLine("Error: Folder does not exist.");
				return;
			}

			string outDir = Path.Combine(folderPath, "Decrypted");
			Directory.CreateDirectory(outDir);

			var files = Directory.GetFiles(folderPath, "*.DAT");
			Console.WriteLine($"Found {files.Length} .DAT files.");

			int successCount = 0;

			foreach (string file in files)
			{
				try
				{
					ProcessFile(file, outDir);
					successCount++;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"[X] Failed to process {Path.GetFileName(file)}: {ex.Message}");
				}
			}

			Console.WriteLine($"\nDone. Processed {successCount}/{files.Length} files.");
			Console.WriteLine($"Decrypted files saved to: {outDir}");
			Console.ReadKey();
		}

		static void ProcessFile(string filePath, string outDir)
		{
			string fileName = Path.GetFileName(filePath);
			byte[] fileData = File.ReadAllBytes(filePath);

			// 1. Basic Validation
			if (fileData.Length < BODY_OFFSET)
			{
				Console.WriteLine($"[-] Skipping {fileName}: File too small.");
				return;
			}

			// 2. Check Static Header (Optional warning)
			if (!CompareBytes(fileData, REFERENCE_HEADER, 0, REFERENCE_HEADER.Length))
			{
				// Note: Real files might vary slightly if header contains timestamps/sizes, 
				// but based on your dump, they are identical.
				Console.WriteLine($"[!] Warning: {fileName} header differs from reference.");
			}

			// 3. Decrypt Filename Region (The "Known Plaintext" Logic)
			// We force the bytes at 0x129 to match the ASCII filename.
			// This effectively decrypts this region.
			byte[] nameBytes = Encoding.ASCII.GetBytes(fileName);
			// Ensure null terminator
			Array.Resize(ref nameBytes, FILENAME_LENGTH);
			nameBytes[FILENAME_LENGTH - 1] = 0x00;

			// In your analysis, the Ciphertext changed based on filename, so to "Decrypt"
			// we simply write the Clean Filename into that slot. 
			// (If we were generating the Key, we would do: Key = Cipher ^ Name).
			Array.Copy(nameBytes, 0, fileData, FILENAME_OFFSET, FILENAME_LENGTH);

			// 4. Decrypt Body/Header with Global Key (If known)
			if (GlobalKey != null && GlobalKey.Length > 0)
			{
				// Apply Global Key to Header (0 - 0x129)
				XorBlock(fileData, 0, HEADER_SIZE, GlobalKey);

				// Apply Global Key to Body (0x136 - End)
				XorBlock(fileData, BODY_OFFSET, fileData.Length - BODY_OFFSET, GlobalKey);
			}

			// 5. Save
			string outPath = Path.Combine(outDir, fileName);
			File.WriteAllBytes(outPath, fileData);
			Console.WriteLine($"[+] Processed {fileName}");
		}

		// Helper to apply a repeating XOR key
		static void XorBlock(byte[] data, int start, int length, byte[] key)
		{
			for (int i = 0; i < length; i++)
			{
				data[start + i] ^= key[(start + i) % key.Length]; // Align key to file offset or restart? 
																  // Usually global keys align to 0. If it aligns to 0: key[(start + i) % len]
			}
		}

		static bool CompareBytes(byte[] data, byte[] match, int offset, int len)
		{
			if (data.Length < offset + len) return false;
			for (int i = 0; i < len; i++)
			{
				if (data[offset + i] != match[i]) return false;
			}
			return true;
		}

		public static byte[] StringToByteArray(string hex)
		{
			return Enumerable.Range(0, hex.Length)
							 .Where(x => x % 2 == 0)
							 .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
							 .ToArray();
		}
	}
}