using Yggdrasil.Configuration;

namespace Kakia.TW.Shared.Configuration.Files
{
	/// <summary>
	/// Represents zone.conf.
	/// </summary>
	public class WorldConf : ConfFile
	{
		public string BindIp { get; set; }
		public int BindPort { get; set; }
		public string ServerIp { get; set; }

		/// <summary>
		/// Loads the conf file and its options from the given path.
		/// </summary>
		public void Load(string filePath)
		{
			this.Require(filePath);

			this.BindIp = this.GetString("zone_bind_ip", "0.0.0.0");
			this.BindPort = this.GetInt("zone_bind_port", 20002);
			this.ServerIp = this.GetString("zone_server_ip", "127.0.0.1");
		}
	}
}
