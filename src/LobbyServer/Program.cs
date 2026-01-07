using Yggdrasil.Logging;
using Yggdrasil.Util;

namespace Kakia.TW.Lobby
{
	internal class Program
	{
		static void Main(string[] args)
		{
			try
			{
				LobbyServer.Instance.Run(args);
			}
			catch (Exception ex)
			{
				Log.Error("While starting server: " + ex);
				ConsoleUtil.Exit(1);
			}
		}
	}
}
