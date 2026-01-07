using Kakia.TW.World.Entities;
using Kakia.TW.World.Network;
using System.Threading.Tasks;

namespace Kakia.TW.World.Scripting
{
	public class Dialog
	{
		private readonly WorldConnection _conn;
		private readonly Npc _npc;
		private ulong _dialogId = 1;
		private bool _visualNovelInitialized = false;

		// TaskCompletionSource handles the "Wait for Packet" logic
		private TaskCompletionSource<string>? _inputTcs;

		/// <summary>
		/// The NPC this dialog belongs to.
		/// </summary>
		public Npc Npc => _npc;

		/// <summary>
		/// The player connection.
		/// </summary>
		public WorldConnection Connection => _conn;

		public Dialog(WorldConnection conn, Npc npc)
		{
			_conn = conn;
			_npc = npc;
		}

		/// <summary>
		/// Sends a message with NPC portrait (visual novel style).
		/// Uses 0x05 dialog - portrait in bottom left, hides UI.
		/// Client handles pacing - no server-side wait needed.
		/// </summary>
		public void Message(string text)
		{
			// Initialize visual novel mode if this is the first 0x05 dialog
			if (!_visualNovelInitialized)
			{
				Send.NpcDialogInit(_conn);
				_visualNovelInitialized = true;
			}

			// Send dialog with portrait using NPC's ObjectId and model ID
			Send.NpcDialog(_conn, _npc.ObjectId, _npc.ModelId, text);
		}

		/// <summary>
		/// Alias for Message.
		/// </summary>
		public void Msg(string text) => Message(text);

		/// <summary>
		/// Shows a menu with NPC portrait and returns the selected index (0-based).
		/// </summary>
		public async Task<int> Select(params string[] options)
		{
			return await Select("", options);
		}

		/// <summary>
		/// Shows a menu with a message and NPC portrait, returns selected index (0-based).
		/// </summary>
		public async Task<int> Select(string message, params string[] options)
		{
			// Send menu with portrait
			Send.NpcMenuWithPortrait(_conn, _dialogId++, _npc.ModelId, message, options);

			string response = await WaitForInput();
			if (int.TryParse(response, out int result))
				return result;
			return 0;
		}

		/// <summary>
		/// Alias for Select - shows a menu and returns the selected index (0-based).
		/// </summary>
		public Task<int> Menu(params string[] options) => Select(options);

		/// <summary>
		/// Shows a menu with a message and returns the selected index.
		/// </summary>
		public Task<int> Menu(string message, params string[] options) => Select(message, options);

		/// <summary>
		/// Closes the NPC dialog window.
		/// For visual novel mode (0x05), sends 44 05 01 to end the sequence.
		/// </summary>
		public void Close()
		{
			// Only send close if visual novel was initialized
			if (_visualNovelInitialized)
			{
				Send.NpcDialogClose(_conn, _npc.ObjectId);
				_visualNovelInitialized = false; // Reset for potential reuse
			}
		}

		/// <summary>
		/// Ends the dialog session entirely.
		/// </summary>
		public void End()
		{
			Close();
			_conn.CurrentDialog = null;
		}

		/// <summary>
		/// Called by PacketHandler when 0x6C arrives.
		/// </summary>
		public void Resume(string response)
		{
			_inputTcs?.TrySetResult(response);
		}

		private Task<string> WaitForInput()
		{
			_inputTcs = new TaskCompletionSource<string>();
			return _inputTcs.Task;
		}

		/// <summary>
		/// Opens a shop window for the player.
		/// </summary>
		public void OpenShop(NpcShop shop)
		{
			// TODO: Implement shop packet
			Close();
		}
	}
}