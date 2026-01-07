using Kakia.TW.Shared.Configuration.Files;

namespace Kakia.TW.Shared.Configuration
{
	/// <summary>
	/// Holds references to all conf files.
	/// </summary>
	public class ConfFiles
	{
		/// <summary>
		/// login.conf
		/// </summary>
		public LoginConf Login { get; } = new();

		/// <summary>
		/// lobby.conf
		/// </summary>
		public LobbyConf Lobby { get; } = new();

		/// <summary>
		/// world.conf
		/// </summary>
		public WorldConf World { get; } = new();

		/// <summary>
		/// commands.conf
		/// </summary>
		public CommandsConf Commands { get; } = new();

		/// <summary>
		/// database.conf
		/// </summary>
		public DatabaseConf Database { get; } = new();

		/// <summary>
		/// localization.conf
		/// </summary>
		public LocalizationConf Localization { get; } = new();

		/// <summary>
		/// version.conf
		/// </summary>
		public VersionConf Version { get; } = new();

		/// <summary>
		/// stats.conf
		/// </summary>
		public StatsConf Stats { get; } = new();

		/// <summary>
		/// Loads all conf files.
		/// </summary>
		public void Load()
		{
			this.Login.Load("system/conf/login.conf");
			this.Lobby.Load("system/conf/lobby.conf");
			this.World.Load("system/conf/world.conf");

			this.Commands.Load("system/conf/commands.conf");
			this.Database.Load("system/conf/database.conf");
			this.Localization.Load("system/conf/localization.conf");
			this.Version.Load("system/conf/version.conf");
			this.Stats.Load("system/conf/stats.conf");
		}
	}
}
