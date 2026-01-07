using Kakia.TW.Shared.Util;

namespace Kakia.TW.Shared.Database
{
	/// <summary>
	/// Represents an account from the database.
	/// </summary>
	public class Account
	{
		/// <summary>
		/// Gets or sets the account's id.
		/// </summary>
		public int Id { get; set; } = int.MaxValue;

		/// <summary>
		/// Gets or sets the account's temporary session id.
		/// </summary>
		public int SessionId { get; set; } = -1;

		/// <summary>
		/// Gets or sets the account's username.
		/// </summary>
		public string Username { get; set; } = "guest";

		/// <summary>
		/// Gets or sets the account's password.
		/// </summary>
		public string Password { get; set; } = "guest";

		/// <summary>
		/// Gets or sets the account's authority level.
		/// </summary>
		public int Authority { get; set; }

		/// <summary>
		/// Gets the account's banned state.
		/// </summary>
		public bool IsBanned { get; set; }

		/// <summary>
		/// Gets or sets the account creation timestamp (Unix time).
		/// </summary>
		public int CreatedAt { get; set; }

		/// <summary>
		/// Gets or sets the last login timestamp (Unix time).
		/// </summary>
		public int LastLogin { get; set; }

		/// <summary>
		/// Returns the account's variable container.
		/// </summary>
		public VariableContainer Vars { get; } = new();
	}
}
