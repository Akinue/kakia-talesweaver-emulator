using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Yggdrasil.Util; // For Hex string if needed

namespace Kakia.TW.Shared.Network
{
	public static class PaleLogger
	{
		// Constants expected by PaleTree
		public const int WM_COPYDATA = 0x004A;
		public const int Sign_Send = 0x10101011;
		public const int Sign_Recv = 0x10101012;

		[StructLayout(LayoutKind.Sequential)]
		public struct COPYDATASTRUCT
		{
			public IntPtr dwData;
			public int cbData;
			public IntPtr lpData;
		}

		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

		public static void Log(bool isReceive, byte[] packetData)
		{
			// Find PaleTree by Window Title (default is "PaleTree")
			IntPtr hWnd = FindWindow(null, "PaleTree");

			if (hWnd == IntPtr.Zero)
				return; // PaleTree not running

			try
			{
				int len = packetData.Length;
				IntPtr ptr = Marshal.AllocHGlobal(len);
				Marshal.Copy(packetData, 0, ptr, len);

				COPYDATASTRUCT cds = new COPYDATASTRUCT
				{
					dwData = (IntPtr)(isReceive ? Sign_Recv : Sign_Send),
					cbData = len,
					lpData = ptr
				};

				SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, ref cds);

				Marshal.FreeHGlobal(ptr);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"PaleLogger Error: {ex.Message}");
			}
		}
	}
}
