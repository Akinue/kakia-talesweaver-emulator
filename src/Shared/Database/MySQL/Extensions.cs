using System;
using MySqlConnector;

namespace Kakia.TW.Shared.Database.MySQL
{
	/// <summary>
	/// Extensions for MySqlDataReader.
	/// </summary>
	public static class MySqlDataReaderExtension
	{
		/// <summary>
		/// Returns true if value at index is null.
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		private static bool IsDBNull(this MySqlDataReader reader, string index)
		{
			return reader.IsDBNull(reader.GetOrdinal(index));
		}

		/// <summary>
		/// Same as GetString, except for a is null check. Returns null if NULL.
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		public static string GetStringSafe(this MySqlDataReader reader, string index)
		{
			if (IsDBNull(reader, index))
				return null;
			else
				return reader.GetString(index);
		}

		/// <summary>
		/// Returns DateTime of the index or DateTime.MinValue if value is null.
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		public static DateTime GetDateTimeSafe(this MySqlDataReader reader, string index)
		{
			return reader[index] as DateTime? ?? DateTime.MinValue;
		}

		/// <summary>
		/// Returns true if the reader has a column with the given name.
		/// </summary>
		public static bool HasColumn(this MySqlDataReader reader, string columnName)
		{
			for (int i = 0; i < reader.FieldCount; i++)
			{
				if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Returns int value if column exists, or default value if column doesn't exist or is null.
		/// </summary>
		public static int TryGetInt32(this MySqlDataReader reader, string column, int defaultValue = 0)
		{
			if (!reader.HasColumn(column) || IsDBNull(reader, column))
				return defaultValue;

			return reader.GetInt32(column);
		}
	}

	/// <summary>
	/// Extensions for MySqlCommand.
	/// </summary>
	public static class MySqlCommandExtensions
	{
		/// <summary>
		/// Shortcut for Parameters.AddWithValue, for consistency with
		/// the simplified commands.
		/// </summary>
		/// <param name="cmd"></param>
		/// <param name="name"></param>
		/// <param name="value"></param>
		public static void AddParameter(this MySqlCommand cmd, string name, object value)
		{
			cmd.Parameters.AddWithValue(name, value);
		}
	}
}
