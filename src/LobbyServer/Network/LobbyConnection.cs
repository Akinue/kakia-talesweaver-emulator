using Kakia.TW.Shared.Network;

namespace Kakia.TW.Lobby.Network
{
	/// <summary>
	/// Represents a connection to a client.
	/// </summary>
	public class LobbyConnection : Connection
	{
		public string SelectedCharacterName { get; set; } = string.Empty;

		/// <summary>
		/// Called when a packet was send by the client.
		/// </summary>
		/// <param name="packet"></param>
		protected override void OnPacketReceived(Packet packet)
		{
			LobbyServer.Instance.PacketHandler.Handle(this, packet);
		}
	}
}
