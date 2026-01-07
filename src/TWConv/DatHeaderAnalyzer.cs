using System;
using System.Collections.Generic;
using System.Text;

namespace TWConv
{
	internal class DatHeaderAnalyzer
	{
		public static void Run(params string[] args)
		{
			string folder = args.Length > 0 ? args[0] : @"C:\Nexon\TalesWeaver\DATA\";//AppDomain.CurrentDomain.BaseDirectory;

			string path0 = Path.Combine(folder, "DA_00000.DAT");
			string path2 = Path.Combine(folder, "DA_00002.DAT"); // The first mismatched file

			if (!File.Exists(path0) || !File.Exists(path2))
			{
				Console.WriteLine("Error: Could not find DA_00000.DAT and DA_00002.DAT in folder.");
				return;
			}

			byte[] file0 = File.ReadAllBytes(path0);
			byte[] file2 = File.ReadAllBytes(path2);

			Console.WriteLine("--- Header Analysis (XOR Reveal) ---");
			Console.WriteLine("Assuming Key is Static, we calculate: (File0 ^ File2)");
			Console.WriteLine("If File0 contains nulls (00), this reveals File2's file type.\n");

			int len = Math.Min(64, Math.Min(file0.Length, file2.Length));
			byte[] xorResult = new byte[len];

			Console.WriteLine("OFFSET | HEX                        | ASCII");
			Console.WriteLine("-------|----------------------------|-------");

			for (int i = 0; i < len; i++)
			{
				// XOR the two encrypted files to eliminate the Key
				// Result = Plaintext0 ^ Plaintext2
				xorResult[i] = (byte)(file0[i] ^ file2[i]);
			}

			// Print Hex Dump
			for (int i = 0; i < len; i += 16)
			{
				Console.Write($" {i:X4}  | ");

				// Hex
				for (int j = 0; j < 16; j++)
				{
					if (i + j < len)
						Console.Write($"{xorResult[i + j]:X2} ");
					else
						Console.Write("   ");
				}

				Console.Write("| ");

				// ASCII
				for (int j = 0; j < 16; j++)
				{
					if (i + j < len)
					{
						char c = (char)xorResult[i + j];
						// Sanitize non-printable chars
						if (c < 0x20 || c > 0x7E) c = '.';
						Console.Write(c);
					}
				}
				Console.WriteLine();
			}
		}
	}
}
