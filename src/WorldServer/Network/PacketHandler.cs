using Kakia.TW.Shared.Network;
using Kakia.TW.Shared.World;
using Kakia.TW.World.Scripting;
using System;
using System.Threading.Tasks;
using Yggdrasil.Logging;

namespace Kakia.TW.World.Network
{
	public class PacketHandler : PacketHandler<WorldConnection>
	{
		[PacketHandler(Op.Handshake)]
		public void Handshake(WorldConnection conn, Packet packet)
		{
			Send.Handshake(conn);
			conn.Framer.IsCryptoSet = true;
			conn.Framer.EncryptOutput = true;
		}

		[PacketHandler(Op.InitConnectRequest)]
		public void InitConnectRequest(WorldConnection conn, Packet packet)
		{
			// Client sends this after connecting post-redirect, before ReconnectRequest.
			// Just acknowledge it exists to prevent spam/crash.
			Log.Debug("World: Client sent InitConnectRequest (pre-reconnect handshake).");
		}

		[PacketHandler(Op.Heartbeat)] // 0x24
		public void Heartbeat(WorldConnection conn, Packet packet)
		{
			// Client sends this periodically.
			// Server can acknowledge it (Op.Acknowledge or Pong) or just ignore it 
			// as the TCP layer handles keep-alive.
			// Handling it here stops the "No handler" warning log.
		}

		[PacketHandler(Op.DebugSourceLineRequest)] // 0x7C (Debug Source Line from Client)
		public void DebugSourceLine(WorldConnection conn, Packet packet)
		{
			// Clients sends 0x7C with a byte (01) or string sometimes for debug info.
			// Just consume it to clear logs.
		}

		/// <summary>
		/// Handles reconnection from Lobby Server (Redirect).
		/// </summary>
		[PacketHandler(Op.ReconnectRequest)]
		public void ClientReconnectRequest(WorldConnection conn, Packet packet)
		{
			// Packet Structure: [Op:1] [Seed:4] [HWID_Len:1] [HWID:String] [User_Len:1] [Username:String] ...
			uint seed = packet.GetUInt();
			string hwid = packet.GetString(packet.GetByte());
			string id = packet.GetString(packet.GetByte());
			string nexonId = packet.GetString(packet.GetByte());
			string username = packet.GetString(packet.GetByte());

			Log.Info($"Lobby: Client reconnected. User: {username}, Seed: {seed:X8}");

			// 1. Verify user via DB (gets Account ID etc)
			int accountId = WorldServer.Instance.Database.VerifySession(username);
			if (accountId <= 0)
			{
				Log.Warning($"Lobby: Reconnect failed. Invalid session for '{username}'");
				conn.Close();
				return;
			}

			conn.Username = username;
			conn.Account = WorldServer.Instance.Database.GetAccountById(accountId);
			conn.Seed = seed;

			conn.Framer.Codec.Initialize(seed);
			conn.Framer.IsCryptoSet = true;
			conn.Framer.EncryptOutput = true;

			// 2. Get selected character name from session
			string? selectedCharName = WorldServer.Instance.Database.GetSelectedCharacterName(accountId);
			if (string.IsNullOrEmpty(selectedCharName))
			{
				Log.Error($"World: No character selected for account '{username}'.");
				conn.Close();
				return;
			}

			// 3. Load User Data from Database
			var user = WorldServer.Instance.Database.LoadCharacter(accountId, selectedCharName);

			if (user == null)
			{
				Log.Error($"World: Failed to load character '{selectedCharName}' for '{username}'.");
				conn.Close();
				return;
			}

			// Assign player to connection
			conn.Player = new Player(conn, user);

			// 4. Send World Entry Packets
			Send.Connected(conn); // 0x7E

			// Use the map position from the database (includes MapId, ZoneId, X, Y)
			ushort mapId = user.ObjectPos.Position.MapId;
			ushort zoneId = (ushort)user.ObjectPos.Position.ZoneId;

			// Default to Narvik starter area if not set
			if (mapId == 0 || zoneId == 0)
			{
				mapId = 6;
				zoneId = 38656;
				user.ObjectPos.Position.X = 305;
				user.ObjectPos.Position.Y = 220;
			}

			Log.Info($"World: Spawning '{selectedCharName}' at Map {mapId}-{zoneId} ({user.ObjectPos.Position.X},{user.ObjectPos.Position.Y})");

			Send.MapChange(conn, mapId, zoneId); // 0x15 Map Packet

			// 5. Spawn the user (0x33 subtype 0x00)
			Send.SpawnUser(conn, user, isSelf: true);

			// 6. Send InitObjectId (0x33 subtype 0x01) - Critical for enabling movement
			Send.InitObjectId(conn, user.UserId);


			//Send.StatUpdateFull(conn, user);
			//Send.StatUpdateHardcoded(conn);
			//Send.StatUpdateDecoded(conn, user);
			//Send.StatUpdateFull(conn, user);
			Send.StatUpdate(conn, user);

			// 7. Add Player to Map Manager
			var map = WorldServer.Instance.World.Maps.GetOrCreateMap(mapId, zoneId);
			if (map != null)
			{
				map.Enter(conn.Player);
			}
			else
			{
				Log.Warning($"Map {mapId}-{zoneId} not found. Player floating in void.");
			}
		}

		[PacketHandler(Op.ChatRequest)] // 0x0E
		public void Chat(WorldConnection conn, Packet packet)
		{
			// Packet structure: [SubOp:1] [CharId:4 - optional?] [MsgLen:1] [Msg:N]
			byte subType = packet.GetByte();
			string message = packet.GetString(packet.GetByte());

			if (conn.Player == null) return;

			Log.Info($"Chat [{conn.Username}]: {message}");

			// Handle chat commands
			if (message.StartsWith("@"))
			{
				HandleChatCommand(conn, message);
				return;
			}

			// Broadcast chat to map
			if (conn.Player.Instance != null)
			{
				Send.ChatBroadcast(conn.Player.Instance, conn.Player.Id, message);
			}
		}

		private void HandleChatCommand(WorldConnection conn, string message)
		{
			var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var command = parts[0].ToLowerInvariant();

			switch (command)
			{
				case "@warp":
					if (parts.Length >= 2)
					{
						// Format: @warp mapId-zoneId or @warp mapId zoneId
						var mapParts = parts[1].Split('-');
						if (mapParts.Length >= 2 &&
							ushort.TryParse(mapParts[0], out ushort mapId) &&
							ushort.TryParse(mapParts[1], out ushort zoneId))
						{
							// Default position or specified
							ushort x = 100, y = 100;
							if (parts.Length >= 4)
							{
								ushort.TryParse(parts[2], out x);
								ushort.TryParse(parts[3], out y);
							}

							conn.Player?.Warp(mapId, zoneId, x, y);
							Log.Info($"Player {conn.Username} warped to {mapId}-{zoneId} ({x},{y})");
						}
					}
					break;

				case "@listmaps":
					var mapList = string.Join(", ", WorldServer.Instance.World.Maps.GetLoadedMapIds());
					Send.Chat(conn, 0, $"Maps: {mapList}");
					break;

				case "@levelup":
					if (conn.Player != null)
					{
						Send.CharEffect(conn, conn.Player.Id, 0x01); // LevelUp effect
					}
					break;

				case "@maxlevel":
					if (conn.Player != null)
					{
						Send.CharEffect(conn, conn.Player.Id, 0x02); // MaxLevel effect
					}
					break;

				case "@refresh":
					if (conn.Player != null)
					{
						Send.SpawnUser(conn, conn.Player.Data, isSelf: true);
					}
					break;

				case "@pos":
					if (conn.Player != null)
					{
						var pos = conn.Player.ObjectPos;
						Send.Chat(conn, 0, $"Position: ({pos.Position.X}, {pos.Position.Y}) Dir: {pos.Direction}");
					}
					break;

				default:
					Send.Chat(conn, 0, $"Unknown command: {command}");
					break;
			}
		}

		[PacketHandler(Op.MovementRequest)] // 0x33
		public void MovementRequest(WorldConnection conn, Packet packet)
		{
			// Packet: [Flag:1] [Type:1] [X:2] [Y:2] [Dir:1 (Optional)]
			byte flag = packet.GetByte();

			if (flag == 0x00) // Initial Request
			{
				byte moveType = packet.GetByte();
				ushort x = packet.GetUShort();
				ushort y = packet.GetUShort();

				// Fix: Check if Direction byte exists before reading
				byte dir = conn.Player?.ObjectPos.Direction ?? 0;
				if (packet.Length > 7)
				{
					dir = packet.GetByte();
				}

				// Update Server State
				if (conn.Player != null)
				{
					var previousX = conn.Player.ObjectPos.Position.X;
					var previousY = conn.Player.ObjectPos.Position.Y;

					conn.Player.ObjectPos.Position.X = x;
					conn.Player.ObjectPos.Position.Y = y;
					conn.Player.ObjectPos.Direction = dir;
					conn.Player.Data.ObjectPos = conn.Player.ObjectPos;

					// Broadcast movement to other players
					if (conn.Player.Instance != null)
					{
						Send.MoveObject(conn.Player.Instance, conn.Player.Id, moveType, previousX, previousY, x, y, dir);
					}
				}
			}
			else if (flag == 0x01) // Continuation/Update
			{
				// Position sync during movement
				if (conn.Player != null && packet.Length >= 5)
				{
					ushort x = packet.GetUShort();
					ushort y = packet.GetUShort();

					conn.Player.ObjectPos.Position.X = x;
					conn.Player.ObjectPos.Position.Y = y;
					conn.Player.Data.ObjectPos = conn.Player.ObjectPos;

					// Check for portal collision after movement update
					CheckPortalCollision(conn);
				}
			}
		}

		private void CheckPortalCollision(WorldConnection conn)
		{
			if (conn.Player?.Instance == null) return;

			var portal = conn.Player.Instance.FindPortalAt(
				conn.Player.ObjectPos.Position.X,
				conn.Player.ObjectPos.Position.Y
			);

			if (portal != null)
			{
				Log.Info($"Player {conn.Username} touched portal {portal.Id} -> Map {portal.DestMapId}-{portal.DestZoneId}");
				conn.Player.Warp(portal.DestMapId, portal.DestZoneId, portal.DestX, portal.DestY);
			}
		}

		// 0x43 - CS_CLICKED_ENTITY
		[PacketHandler(Op.EntityClickRequest)]
		public void ClickedEntityRequest(WorldConnection conn, Packet packet)
		{
			uint entityId = packet.GetUInt();

			if (conn.Player?.Instance == null) return;

			// Send click acknowledgment
			Send.EntityClickAck(conn, entityId);

			// Check if it's an NPC with a dialog script
			if (conn.Player.Instance.TryGetNpc(entityId, out var npc) && npc?.Script != null)
			{
				Log.Debug($"Player {conn.Username} clicked NPC '{npc.Name}' (ID: {entityId})");

				// Start Dialog
				var dialog = new Dialog(conn, npc);
				conn.CurrentDialog = dialog;

				// Run script async (fire and forget from handler perspective)
				_ = Task.Run(async () =>
				{
					try
					{
						await npc.Script(dialog);
					}
					catch (Exception ex)
					{
						Log.Error($"NPC Script error: {ex.Message}");
						dialog.Close();
					}
				});
			}
			// Check if it's a monster (for targeting/combat)
			else if (conn.Player.Instance.TryGetMonster(entityId, out var monster))
			{
				Log.Debug($"Player {conn.Username} clicked Monster '{monster.Name}' (ID: {entityId})");
				// TODO: Handle monster targeting/combat
			}
			else
			{
				Log.Debug($"Player {conn.Username} clicked unknown entity (ID: {entityId})");
			}
		}

		[PacketHandler(Op.NpcDialogAnswerRequest)]
		public void NpcDialogAnswerRequest(WorldConnection conn, Packet packet)
		{
			if (conn.CurrentDialog == null) return;

			// Packet structure from legacy:
			// [flag1:1] [flag2:1] [dialogId:8 BE] [padding:2?] [selectedOption:1]
			byte flag1 = packet.GetByte();
			byte flag2 = packet.GetByte();

			// flag2 == 5 means the dialog window was closed
			if (flag2 == 5)
			{
				Log.Debug("Dialog closed by player");
				conn.CurrentDialog.Close();
				return;
			}

			// For "Next" button presses (simple message dialogs)
			if (flag2 == 0 || flag1 == 0)
			{
				conn.CurrentDialog.Resume("next");
				return;
			}

			// For menu selections, read the dialog ID and selected option
			if (packet.Length >= 8)
			{
				// Dialog ID (8 bytes, Big Endian) - used to track which dialog we're responding to
				ulong dialogId = packet.GetULongBE();

				// Skip 2 bytes padding and read option index
				if (packet.Length >= 2)
				{
					packet.GetUShort(); // padding
					byte selectedOption = packet.GetByte();

					Log.Debug($"Dialog answer: DialogId={dialogId}, Option={selectedOption}");
					conn.CurrentDialog.Resume(selectedOption.ToString());
				}
				else
				{
					conn.CurrentDialog.Resume("0");
				}
			}
			else
			{
				// Fallback for simple Next responses
				conn.CurrentDialog.Resume("next");
			}
		}

		[PacketHandler(Op.StatIncreaseRequest)] // 0x0A
		public void StatIncrease(WorldConnection conn, Packet packet)
		{
			// Packet structure: [StatType:1]
			// StatType: 1=Stab, 2=Hack, 3=Int, 4=Def, 5=MR, 6=Dex, 7=Agi
			var statType = (StatType)packet.GetByte();

			if (conn.Player == null) return;

			var data = conn.Player.Data;

			// Check if player has stat points available
			if (data.StatPoints <= 0)
			{
				Log.Debug($"Player {conn.Username} tried to increase stat but has no stat points");
				return;
			}

			// Increase the appropriate stat and get the new value
			int newValue;
			switch (statType)
			{
				case StatType.Stab: newValue = ++data.StatStab; break;
				case StatType.Hack: newValue = ++data.StatHack; break;
				case StatType.Int: newValue = ++data.StatInt; break;
				case StatType.Def: newValue = ++data.StatDef; break;
				case StatType.MR: newValue = ++data.StatMR; break;
				case StatType.Dex: newValue = ++data.StatDex; break;
				case StatType.Agi: newValue = ++data.StatAgi; break;
				default:
					Log.Warning($"Unknown stat type: {statType}");
					return;
			}

			data.StatPoints--;

			Log.Debug($"Player {conn.Username} increased stat {statType} to {newValue}, remaining points: {data.StatPoints}");

			// Send stat update packet to client
			Send.StatUpdate(conn, conn.Player.Data);
		}

		[PacketHandler(Op.DirectionUpdateRequest)] // 0x11
		public void UpdateDirection(WorldConnection conn, Packet packet)
		{
			byte direction = packet.GetByte();

			if (conn.Player == null) return;

			conn.Player.ObjectPos.Direction = direction;
			conn.Player.Data.ObjectPos.Direction = direction;

			// Broadcast to other players on the map
			if (conn.Player.Instance != null)
			{
				Send.DirectionUpdate(conn.Player.Instance, conn.Player.Id, direction, conn.Player);
			}
		}

		[PacketHandler(Op.AttackRequest)] // 0x13
		public void Attack(WorldConnection conn, Packet packet)
		{
			if (conn.Player == null) return;

			// Basic attack acknowledgment
			// Legacy: 4A 00 00 00 00 00 00, 17 00, then attack result

			Send.AttackAck(conn);
			Send.AttackResult(conn, conn.Player.Id);
		}

		[PacketHandler(Op.SetPoseRequest)] // 0x32
		public void SetPose(WorldConnection conn, Packet packet)
		{
			byte pose = packet.GetByte(); // 0 = stand, 1 = sit

			if (conn.Player == null) return;

			Log.Debug($"Player {conn.Username} changed pose to {(pose == 0 ? "stand" : "sit")}");

			// Broadcast pose change to all players including self
			if (conn.Player.Instance != null)
			{
				Send.PoseUpdate(conn.Player.Instance, conn.Player.Id, pose);
			}
		}

		[PacketHandler(Op.CharacterInfoUpdateRequest)] // 0x2A
		public void UpdateCharacterInfo(WorldConnection conn, Packet packet)
		{
			// This is sent when character is leaving the map/logging out
			if (conn.Player == null) return;

			Log.Info($"Saving character data for {conn.Username}");

			// Save character to database
			conn.Player.Save();

			// Broadcast despawn to other players
			if (conn.Player.Instance != null)
			{
				Send.EntityDespawn(conn.Player.Instance, conn.Player.Id, conn.Player);
			}
		}

		// Stub Handlers for logs to prevent warnings
		[PacketHandler(Op.TriggerRequest, Op.UiActionRequest, Op.Unknown39Request, Op.Unknown45Request, Op.Unknown51Request, Op.Unknown55Request, Op.Unknown5FRequest, Op.Unknown60Request)]
		public void IgnoredPackets(WorldConnection conn, Packet packet)
		{
			// These packets are currently ignored to prevent console spam
		}
	}
}