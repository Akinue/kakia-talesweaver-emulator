using Yggdrasil.Logging;
using Yggdrasil.Util;

namespace Kakia.TW.Login
{
	internal class Program
	{
		static void Main(string[] args)
		{
			try
			{
				LoginServer.Instance.Run(args);
			}
			catch (Exception ex)
			{
				Log.Error("While starting server: " + ex);
				ConsoleUtil.Exit(1);
			}
		}
	}
}
