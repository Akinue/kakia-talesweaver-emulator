using Kakia.TW.Shared.Database.MySQL;
using MySqlConnector;
using System.Globalization;
using Yggdrasil.Logging;
using Yggdrasil.Util;

namespace Kakia.TW.Shared.Database
{
	/// <summary>
	/// Base class for MySQL database interfaces.
	/// </summary>
	public abstract class Db
	{
		private string _connectionString;

		/// <summary>
		/// Returns a valid connection.
		/// </summary>
		public MySqlConnection GetConnection()
		{
			if (_connectionString == null)
				throw new Exception("Database has not been initialized.");

			var result = new MySqlConnection(_connectionString);
			result.Open();
			return result;
		}

		/// <summary>
		/// Sets connection string and calls TestConnection.
		/// </summary>
		/// <param name="host"></param>
		/// <param name="port"></param>
		/// <param name="user"></param>
		/// <param name="pass"></param>
		/// <param name="db"></param>
		public void Init(string host, int port, string user, string pass, string db)
		{
			_connectionString = string.Format("server={0}; port={1}; database={2}; uid={3}; password={4}; pooling=true; min pool size=0; max pool size=100; ConvertZeroDateTime=true", host, port, db, user, pass);
			this.TestConnection();
		}

		/// <summary>
		/// Tests connection, throws on error.
		/// </summary>
		public void TestConnection()
		{
			MySqlConnection conn = null;
			try
			{
				conn = this.GetConnection();
			}
			finally
			{
				conn?.Close();
			}
		}

		/// <summary>
		/// Returns true if an account with the given username exists.
		/// </summary>
		/// <param name="username"></param>
		/// <returns></returns>
		public bool UsernameExists(string username)
		{
			using (var conn = this.GetConnection())
			using (var cmd = new MySqlCommand("SELECT * FROM `accounts` WHERE `username` = @username", conn))
			{
				cmd.AddParameter("@username", username);

				using (var reader = cmd.ExecuteReader())
					return reader.HasRows;
			}
		}

		/// <summary>
		/// Returns an account by its username, or null if no account was
		/// found.
		/// </summary>
		/// <param name="username"></param>
		/// <returns></returns>
		public Account GetAccountByUsername(string username)
		{
			using (var conn = this.GetConnection())
			using (var cmd = new MySqlCommand("SELECT * FROM `accounts` WHERE `username` = @username", conn))
			{
				cmd.AddParameter("@username", username);

				using (var reader = cmd.ExecuteReader())
				{
					if (!reader.Read())
						return null;

					return this.ReadAccount(reader);
				}
			}
		}

		/// <summary>
		/// Returns an account by its account id, or null if no account was
		/// found.
		/// </summary>
		/// <param name="accountId"></param>
		/// <returns></returns>
		public Account GetAccountById(int accountId)
		{
			using (var conn = this.GetConnection())
			using (var cmd = new MySqlCommand("SELECT * FROM `accounts` WHERE `accountId` = @accountId", conn))
			{
				cmd.AddParameter("@accountId", accountId);

				using (var reader = cmd.ExecuteReader())
				{
					if (!reader.Read())
						return null;

					return this.ReadAccount(reader);
				}
			}
		}

		/// <summary>
		/// Updates the account's information in the database.
		/// </summary>
		/// <param name="account"></param>
		public void SaveAccount(Account account)
		{
			using (var conn = this.GetConnection())
			using (var cmd = new UpdateCommand("UPDATE `accounts` SET {0} WHERE `accountId` = @accountId", conn))
			{
				cmd.AddParameter("@accountId", account.Id);
				cmd.Set("authority", account.Authority);
			}

			this.SaveVars("vars_account", account.Id, account.Vars.Perm.GetList());
		}

		/// <summary>
		/// Reads account from reader and returns it.
		/// </summary>
		/// <param name="reader"></param>
		/// <returns></returns>
		private Account ReadAccount(MySqlDataReader reader)
		{
			var account = new Account();

			account.Id = reader.GetInt32("accountId");
			account.Username = reader.GetStringSafe("username");
			account.Password = reader.GetStringSafe("password");
			account.Authority = reader.GetByte("authority");
			account.SessionId = reader.GetInt32("sessionId");

			// Timestamps (optional columns - use defaults if not present)
			account.CreatedAt = reader.TryGetInt32("createdAt");
			account.LastLogin = reader.TryGetInt32("lastLogin");

			account.Vars.Perm.Load(this.GetVars("vars_account", account.Id));

			return account;
		}

		/// <summary>
		/// Returns all variables for the given id from the table.
		/// </summary>
		/// <param name="tableName"></param>
		/// <param name="id"></param>
		/// <returns></returns>
		public IDictionary<string, object> GetVars(string tableName, long id)
		{
			using var conn = this.GetConnection();
			using var cmd = new MySqlCommand("SELECT * FROM `" + tableName + "` WHERE ownerId = @ownerId", conn);
			cmd.AddParameter("@ownerId", id);

			var vars = new Dictionary<string, object>();

			using (var reader = cmd.ExecuteReader())
			{
				while (reader.Read())
				{
					var name = reader.GetString("name");
					var type = reader.GetString("type");
					var val = reader.GetStringSafe("value");

					if (val == null)
						continue;

					switch (type)
					{
						case "1": vars[name] = byte.Parse(val); break;
						case "2": vars[name] = short.Parse(val); break;
						case "4": vars[name] = int.Parse(val); break;
						case "8": vars[name] = long.Parse(val); break;
						case "f": vars[name] = float.Parse(val, CultureInfo.InvariantCulture); break;
						case "d": vars[name] = double.Parse(val, CultureInfo.InvariantCulture); break;
						case "b": vars[name] = bool.Parse(val); break;
						case "s": vars[name] = val; break;
						case "B": vars[name] = Convert.FromBase64String(val); break;

						default:
							Log.Warning("Db.LoadVars: Unknown variable type '{0}'.", type);
							continue;
					}
				}
			}

			return vars;
		}

		/// <summary>
		/// Saves all variables to the table.
		/// </summary>
		/// <param name="tableName"></param>
		/// <param name="id"></param>
		/// <param name="vars"></param>
		public void SaveVars(string tableName, long id, IDictionary<string, object> vars)
		{
			using (var conn = this.GetConnection())
			using (var transaction = conn.BeginTransaction())
			{
				using (var cmd = new MySqlCommand("DELETE FROM `" + tableName + "` WHERE ownerId = @ownerId", conn, transaction))
				{
					cmd.AddParameter("@ownerId", id);
					cmd.ExecuteNonQuery();
				}

				foreach (var var in vars)
				{
					if (var.Value == null)
						continue;

					// Get type
					string type;
					if (var.Value is sbyte) type = "1";
					else if (var.Value is short) type = "2";
					else if (var.Value is int) type = "4";
					else if (var.Value is long) type = "8";
					else if (var.Value is float) type = "f";
					else if (var.Value is double) type = "d";
					else if (var.Value is string) type = "s";
					else if (var.Value is bool) type = "b";
					else if (var.Value is byte[]) type = "B";
					else
					{
						Log.Warning("Db.SaveVars: Skipping variable '{0}', as it's using the unsupported type '{1}'.", var.Key, var.Value.GetType());
						continue;
					}

					// Get value
					var val = string.Empty;
					if (type == "f")
					{
						val = ((float)var.Value).ToString(CultureInfo.InvariantCulture);
					}
					else if (type == "d")
					{
						val = ((double)var.Value).ToString(CultureInfo.InvariantCulture);
					}
					else if (type == "B")
					{
						val = Convert.ToBase64String((byte[])var.Value);
					}
					else
					{
						val = var.Value.ToString();
					}

					// Make sure value isn't too big for the mediumtext field
					if (val.Length > ushort.MaxValue)
					{
						Log.Warning("Db.SaveVars: Skipping variable '{0}', as it's too large.", var.Key);
						continue;
					}

					// Save
					using (var cmd = new InsertCommand("INSERT INTO `" + tableName + "` {0}", conn, transaction))
					{
						cmd.Set("ownerId", id);
						cmd.Set("name", var.Key);
						cmd.Set("type", type);
						cmd.Set("value", val);

						cmd.Execute();
					}
				}

				transaction.Commit();
			}
		}

		/// <summary>
		/// Generates a random session id, assigns it to the account,
		/// and updates the database.
		/// </summary>
		/// <param name="account"></param>
		public void UpdateSessionId(ref Account account)
		{
			var sessionId = RandomProvider.Get().Next();

			using (var conn = this.GetConnection())
			using (var cmd = new UpdateCommand("UPDATE `accounts` SET {0} WHERE `accountId` = @accountId", conn))
			{
				cmd.AddParameter("@accountId", account.Id);
				cmd.Set("sessionId", sessionId);

				cmd.Execute();
			}

			account.SessionId = sessionId;
		}

		/// <summary>
		/// Verifies the existence of a user session by checking the account ID associated with the specified username.
		/// </summary>
		/// <remarks>This method queries the database to retrieve the account ID for the given username. If no
		/// matching account is found, the method returns 0.</remarks>
		/// <param name="username">The username to verify. This value cannot be null or empty.</param>
		/// <returns>The account ID associated with the specified username if the user exists; otherwise, 0.</returns>
		public int VerifySession(string username)
		{
			using var conn = GetConnection();
			using var cmd = new MySqlCommand("SELECT accountId FROM accounts WHERE username = @u", conn);
			cmd.Parameters.AddWithValue("@u", username);
			object result = cmd.ExecuteScalar();
			return result != null ? Convert.ToInt32(result) : 0;
		}
	}
}
