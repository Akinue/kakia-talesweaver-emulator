using System;
using System.Collections.Generic;
using System.Text;

namespace TWConv
{
	internal class DatFullDecryptor
	{
		public static void Run(string[] args)
		{
			Console.WriteLine("=== DAT Texture Decryptor ===");

			// 1. INPUT: The raw bytes of DA_00000.DAT (from your dump)
			// I've reconstructed the first 256 bytes from your hex dump to extract the key.
			byte[] rawHeader = HexStringToBytes(
				"0266FD673D07851266568999034CBC43F2D5324F1C860B52964D44E62106DE87" +
				"E705655C28E8B7829D9316F1B4B156F9C9AE5F0FE0430B7B5AABCFEEC29DF060" +
				"7701B1FCE6601A0A1EAFB0886F199CE3F54A1C71A5CFA5A3F9E9017C18C7BE00" +
				"A1B579B2005913699455EF3192810B410EEBF877817EB9A65B0D02FB5E9DF155" +
				"EFB0E5B89EE9E39C36B2DC50DE741077DDBF6D2BCE477D23FBD157F156160BF5" +
				"639A41E19B51A224075C40CEA069C7E6FB8A1D9E2FC586A832C1BCCB91F04520" +
				"A6E5EC43322EDB8C1C48247106AE82920A70F5CBF76BC69C1398AFD893D59215" +
				"162027B97C83B3CDAD9081E5A114A1823F2AC120FA79273B1F4F30987E02F746"
			);

			// 2. CONSTRUCT KNOWN PLAINTEXT (Standard DDS Header)
			byte[] plainHeader = new byte[256];

			// Signature "DDS "
			plainHeader[0] = 0x44; plainHeader[1] = 0x44; plainHeader[2] = 0x53; plainHeader[3] = 0x20;
			// Size: 124
			plainHeader[4] = 0x7C;
			// Flags: 0x1007 (Caps | Height | Width | PixelFormat)
			plainHeader[8] = 0x07; plainHeader[9] = 0x10;

			// The rest of the DDS header (offsets 12 to 127) is usually 00s for standard 2D textures, 
			// except for Height/Width/MipMap count.
			// However, Cipher ^ 00 = Key. So for the 00 regions, the Raw Data IS the Key.
			// We will assume the key is the raw data, then apply corrections for the few non-zero header fields.

			// 3. GENERATE KEY (256 Byte Buffer)
			byte[] key = new byte[256];
			for (int i = 0; i < 256; i++)
			{
				// Key = Raw ^ Plain. 
				// Where Plain is 00, Key = Raw.
				key[i] = (byte)(rawHeader[i] ^ plainHeader[i]);
			}

			// 4. DECRYPT FILES
			string folder = args.Length > 0 ? args[0] : @"C:\Nexon\TalesWeaver\DATA\";//AppDomain.CurrentDomain.BaseDirectory;
			var files = Directory.GetFiles(folder, "*.DAT");
			string outDir = Path.Combine(folder, "Decrypted_Textures");
			Directory.CreateDirectory(outDir);

			foreach (var file in files)
			{
				try
				{
					byte[] data = File.ReadAllBytes(file);

					// Apply Key (Repeating every 256 bytes)
					for (int i = 0; i < data.Length; i++)
					{
						// Skip the filename area (0x129) if you want to preserve the name 
						// logic we found earlier, or just overwrite it later.
						if (i >= 0x129 && i < 0x129 + 13)
						{
							// Filename area is special, we skip or manual overwrite
							continue;
						}

						data[i] = (byte)(data[i] ^ key[i % 256]);
					}

					// Fix Filename visually
					byte[] nameBytes = Encoding.ASCII.GetBytes(Path.GetFileName(file));
					Array.Resize(ref nameBytes, 13);
					if (data.Length > 0x129 + 13)
						Array.Copy(nameBytes, 0, data, 0x129, 13);

					// Check if it looks like a DDS now
					if (data.Length > 4 && data[0] == 'D' && data[1] == 'D' && data[2] == 'S')
					{
						string outName = Path.GetFileNameWithoutExtension(file) + ".dds";
						File.WriteAllBytes(Path.Combine(outDir, outName), data);
						Console.WriteLine($"[+] Decrypted {outName}");
					}
					else
					{
						// If it's not DDS, it might be the Model/Sound formats (KTSR/G1M)
						// This key is specifically for the TEXTURE files (DA_00000, etc.)
						Console.WriteLine($"[-] {Path.GetFileName(file)} is not a texture (Magic: {(char)data[0]}{(char)data[1]}..)");
					}
				}
				catch { }
			}
		}

		public static byte[] HexStringToBytes(string hex)
		{
			List<byte> bytes = new List<byte>();
			for (int i = 0; i < hex.Length; i += 2) bytes.Add(Convert.ToByte(hex.Substring(i, 2), 16));
			return bytes.ToArray();
		}
	}
}
