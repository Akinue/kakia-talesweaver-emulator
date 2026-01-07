using System;

namespace Kakia.TW.Shared
{
	/// <summary>
	/// Global accessor for the application settings.
	/// </summary>
	public static class Game
	{
		/// <summary>
		/// The packet version to use.
		/// </summary>
		public static int Version { get; set; } = Versions.JP_25_11_29;

		/// <summary>
		/// Returns the servers current tick time, indicating how many
		/// milliseconds have passed since a certain time.
		/// </summary>
		/// <returns></returns>
		public static int GetTick()
			=> Environment.TickCount;
	}

	/// <summary>
	/// A list of known packet versions.
	/// </summary>
	public static class Versions
	{
		/// <summary>
		/// The packet version for the JP client (2025-11-29).
		/// </summary>
		public const int JP_25_11_29 = 851;

		/// <summary>
		/// The packet version for the KR client (2025-11-29).
		/// </summary>
		public const int KR_25_11_29 = 909;
	}
}
