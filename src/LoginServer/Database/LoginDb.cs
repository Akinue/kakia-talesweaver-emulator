using Kakia.TW.Shared.Database;
using Kakia.TW.Shared.Database.MySQL;
using MySqlConnector;
using Yggdrasil.Logging;
using Yggdrasil.Util;

namespace Kakia.TW.Login.Database
{
	/// <summary>
	/// Login server database interface.
	/// </summary>
	public class LoginDb : Db
	{
		/// <summary>
		/// Normalizes/Updates the file names in the update db.
		/// </summary>
		/// <remarks>
		/// Temporary fix, since we had some issues with the update names.
		/// </remarks>
		/// <returns></returns>
		public void NormalizeUpdateNames()
		{
			using (var conn = this.GetConnection())
			using (var mc = new MySqlCommand("UPDATE `updates` SET `path` = REPLACE(LOWER(`path`), \"update-\", \"update_\")", conn))
			{
				mc.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Returns true if the update with the given name was already applied.
		/// </summary>
		/// <param name="updateName"></param>
		/// <returns></returns>
		public bool CheckUpdate(string updateName)
		{
			using (var conn = this.GetConnection())
			using (var mc = new MySqlCommand("SELECT * FROM `updates` WHERE `path` = @path", conn))
			{
				mc.Parameters.AddWithValue("@path", updateName);

				using (var reader = mc.ExecuteReader())
					return reader.Read();
			}
		}

		/// <summary>
		/// Executes SQL update.
		/// </summary>
		/// <param name="updateName"></param>
		/// <param name="query"></param>
		public void RunUpdate(string updateName, string query)
		{
			try
			{
				using (var conn = this.GetConnection())
				{
					// Run update
					using (var cmd = new MySqlCommand(query, conn))
						cmd.ExecuteNonQuery();

					// Log update
					using (var cmd = new InsertCommand("INSERT INTO `updates` {0}", conn))
					{
						cmd.Set("path", updateName);
						cmd.Execute();
					}

					Log.Info("Successfully applied '{0}'.", updateName);
				}
			}
			catch (Exception ex)
			{
				Log.Error("RunUpdate: Failed to run '{0}': {1}", updateName, ex.Message);
				ConsoleUtil.Exit(1);
			}
		}

		/// <summary>
		/// Returns the number of characters for the given account.
		/// </summary>
		/// <param name="accountId"></param>
		/// <returns></returns>
		public byte GetCharacterCount(int accountId)
		{
			using var conn = this.GetConnection();
			using var cmd = new MySqlCommand("SELECT COUNT(*) FROM characters WHERE accountId = @id", conn);
			cmd.Parameters.AddWithValue("@id", accountId);

			var result = cmd.ExecuteScalar();
			return result != null ? Convert.ToByte(result) : (byte)0;
		}

		/// <summary>
		/// Creates new account.
		/// </summary>
		/// <param name="username"></param>
		/// <param name="password"></param>
		/// <param name="authority"></param>
		/// <returns></returns>
		public Account CreateAccount(string username, string password, int authority)
		{
			var account = new Account();

			account.Username = username;
			account.Password = password;
			account.Authority = authority;

			using (var conn = this.GetConnection())
			using (var cmd = new InsertCommand("INSERT INTO `accounts` {0}", conn))
			{
				cmd.Set("username", account.Username);
				cmd.Set("password", account.Password);
				cmd.Set("authority", account.Authority);
				cmd.Set("sessionId", 0);

				cmd.Execute();
				account.Id = (int)cmd.LastId;
			}

			return account;
		}
	}
}
