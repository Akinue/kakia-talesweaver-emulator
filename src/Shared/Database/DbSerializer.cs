using System.Text.Json;

namespace Kakia.TW.Shared.Database
{
	public static class DbSerializer
	{
		private static readonly JsonSerializerOptions Options = new()
		{
			IncludeFields = true,
			PropertyNameCaseInsensitive = true
		};

		public static byte[] Serialize<T>(T obj)
		{
			if (obj == null) return Array.Empty<byte>();
			return JsonSerializer.SerializeToUtf8Bytes(obj, Options);
		}

		public static T Deserialize<T>(byte[] data) where T : new()
		{
			if (data == null || data.Length == 0) return new T();
			try
			{
				return JsonSerializer.Deserialize<T>(data, Options) ?? new T();
			}
			catch
			{
				return new T();
			}
		}
	}
}