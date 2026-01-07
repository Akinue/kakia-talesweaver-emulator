using Kakia.TW.Shared.Network;

namespace Kakia.TW.Login.Network
{
	/// <summary>
	/// Represents a connection to a client.
	/// </summary>
	public class LoginConnection : Connection
	{
		public string Username { get; set; }

		public bool IsAuthenticated { get; set; }

		public uint Seed => this.Framer.Codec.Seed;

		/// <summary>
		/// Called when a packet was send by the client.
		/// </summary>
		/// <param name="packet"></param>
		protected override void OnPacketReceived(Packet packet)
		{
			LoginServer.Instance.PacketHandler.Handle(this, packet);
		}
	}
}
