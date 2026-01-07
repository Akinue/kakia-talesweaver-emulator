using Kakia.TW.Shared.Network;
using Kakia.TW.Shared.Network.Helpers;
using Kakia.TW.Shared.World;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Managers;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Kakia.TW.World.Network
{
	public static class Send
	{
		public static void Handshake(WorldConnection conn)
		{
			var packet = new Packet(Op.Handshake);
			packet.PutByte(0x00); packet.PutByte(0x02);
			packet.PutUInt(conn.Seed);
			packet.PutInt(0);
			packet.PutCompressedString("World Server");
			conn.Send(packet);
		}

		public static void Connected(WorldConnection conn)
		{
			var packet = new Packet(Op.ConnectedResponse);
			packet.PutByte(0x1B);
			packet.PutBytes(Encoding.ASCII.GetBytes("CONNECTED SERVER\n"));
			conn.Send(packet);
		}

		/// <summary>
		/// InitObjectIdPacket (0x33 subtype 0x01) - Tells client which entity ID they control.
		/// Critical for enabling movement.
		/// </summary>
		public static void InitObjectId(WorldConnection conn, uint objectId)
		{
			var packet = new Packet((Op)0x33);
			packet.PutByte(0x01); // Subtype for InitObjectId
			packet.PutUInt(objectId);
			packet.PutByte(0x00); // Padding
			conn.Send(packet);
		}

		/// <summary>
		/// MapPacket (0x15).
		/// </summary>
		public static void MapChange(WorldConnection conn, ushort mapId, ushort zoneId, bool weather = false, bool pvp = false)
		{
			var packet = new Packet((Op)0x15);
			packet.PutUShort(mapId);
			packet.PutUShort(zoneId);
			packet.PutUShort(0); // MapType

			byte attributes = 0;
			if (weather) attributes |= 0x01;
			if (pvp) attributes |= 0x02;

			packet.PutByte(attributes);
			conn.Send(packet);
		}

		/// <summary>
		/// ChatPacket (0x0D / 13)
		/// </summary>
		public static void Chat(WorldConnection conn, uint charId, string message)
		{
			var packet = new Packet(Op.ChatResponse);
			packet.PutByte(0x00); // Subtype
			packet.PutUInt(charId);
			packet.PutString(message, true);
			conn.Send(packet);
		}

		/// <summary>
		/// Broadcasts chat message to all players on the map.
		/// </summary>
		public static void ChatBroadcast(Map map, uint charId, string message)
		{
			var packet = new Packet(Op.ChatResponse);
			packet.PutByte(0x00); // Subtype
			packet.PutUInt(charId);
			packet.PutString(message, true);

			BroadcastToMap(map, packet, null);
		}

		/// <summary>
		/// Broadcasts movement to all players on the map.
		/// Uses legacy MoveObjectPacket structure (Op 0x0B).
		/// </summary>
		public static void MoveObject(Map map, uint objectId, byte moveType, ushort prevX, ushort prevY, ushort targetX, ushort targetY, byte direction, Player? exclude = null)
		{
			// MoveSpeed: Run = 27, Walk = 15
			byte speed = moveType == 0x01 ? (byte)27 : (byte)15;

			var packet = new Packet(Op.UserPositionResponse); // Legacy MoveObjectPacket opcode
			packet.PutByte(0x00); // Sub ID
			packet.PutUInt(objectId);
			packet.PutByte(moveType);
			packet.PutByte(speed);
			packet.PutUShort(prevX);
			packet.PutUShort(prevY);
			packet.PutUShort(targetX);
			packet.PutUShort(targetY);
			packet.PutByte(direction);

			BroadcastToMap(map, packet, exclude);
		}

		/// <summary>
		/// Legacy EntitySpawnPacket (Op.World = 7)
		/// </summary>
		public static void SpawnItem(WorldConnection conn, GameItem item, short x, short y, int ownerId)
		{
			var packet = new Packet(Op.WorldResponse);
			packet.PutByte((byte)WorldPacketId.Spawn); // 0
			packet.PutByte((byte)SpawnType.Item); // SpawnType.Item

			// Item serialization
			packet.PutInt(item.ItemId);
			packet.PutShort(item.Amount);
			packet.PutShort(item.Durability);

			if (item.IsEquipment)
			{
				packet.PutByte(item.Refine);
				packet.PutByte(item.DataFlags);
				packet.PutByte((byte)item.Stats.Count);
				foreach (var stat in item.Stats)
				{
					packet.PutShort(stat.Type);
					packet.PutInt(stat.Value);
				}
			}

			packet.PutInt(ownerId);
			packet.PutShort(x);
			packet.PutShort(y);
			packet.PutShort(item.Amount); // Dropped Amount

			conn.Send(packet);
		}

		/// <summary>
		/// Spawns an Item on the ground.
		/// Replaces EntitySpawnPacket (Action 0, Type 3)
		/// </summary>
		public static void SpawnItem(WorldConnection conn, GameItem item, Position pos, int ownerId)
		{
			var packet = new Packet(Op.WorldResponse); // 0x07
			packet.PutByte((byte)WorldPacketId.Spawn); // 0x00 (Spawn)
			packet.PutByte((byte)SpawnType.Item); // Type: Item

			packet.PutGameItem(item);

			packet.PutInt(ownerId);
			packet.PutUShort(pos.X);
			packet.PutUShort(pos.Y);
			packet.PutShort(item.Amount); // Dropped Amount

			conn.Send(packet);
		}

		/// <summary>
		/// Spawns a Portal.
		/// Replaces EntitySpawnPacket (Action 0, Type 4)
		/// </summary>
		public static void SpawnPortal(WorldConnection conn, WarpPortal portal)
		{
			var packet = new Packet(Op.WorldResponse);
			packet.PutByte((byte)WorldPacketId.Spawn);
			packet.PutByte((byte)SpawnType.Portal); // Type: Portal

			packet.PutUInt(portal.Id);
			packet.PutUShort(portal.MinPoint.X); // Visual Pos X
			packet.PutUShort(portal.MinPoint.Y); // Visual Pos Y
			packet.PutUShort(portal.DestMapId);
			packet.PutUShort(portal.DestPortalId);

			conn.Send(packet);
		}

		/// <summary>
		/// Spawns an NPC or Monster.
		/// Replaces EntitySpawnPacket (Action 0, Type 2)
		/// </summary>
		public static void SpawnNpc(WorldConnection conn, uint objectId, uint npcId, Position pos, Direction direction)
		{
			var packet = new Packet(Op.WorldResponse);
			packet.PutByte((byte)WorldPacketId.Spawn);
			packet.PutByte((byte)SpawnType.MonsterNpc); // Type: NPC/Monster

			packet.PutUInt(objectId);
			packet.PutUInt(0); // Unk v3

			packet.PutUInt(0); // Unk v30
			packet.PutUInt(npcId);
			packet.PutUShort(pos.X);
			packet.PutUShort(pos.Y);
			packet.PutByte((byte)direction);

			conn.Send(packet);
		}

		public static void SpawnHardcoded(WorldConnection conn, Entity entity)
		{
			var packet = new Packet(Op.WorldResponse);
			packet.PutByte((byte)WorldPacketId.Spawn);
			packet.PutByte((byte)SpawnType.Npc); // Type: NPC/Monster

			packet.PutUInt(entity.ObjectId);
			packet.PutEmptyBin(4);
			// 21 9E 63 01 
			packet.PutUInt(entity.ModelId);
			packet.PutUShort(entity.Position.X);
			packet.PutUShort(entity.Position.Y);
			packet.PutByte((byte)entity.Direction);
			packet.PutByte(0);
			packet.PutByte(10); // 0x0A
			packet.PutLong(-1);
			packet.PutEmptyBin(39);
			packet.PutByte(0);
			packet.PutByte(0);
			packet.PutByte(0);

			conn.Send(packet);
		}

		/// <summary>
		/// Complex SpawnCharacterPacket (Op.CS_MOVEMENT = 0x33, SubOp 0x00 for Spawn)
		/// Note: The name CS_MOVEMENT in Op.cs for 0x33 is confusing as 0x33 is bi-directional.
		/// Server->Client 0x33 is Spawn User.
		/// </summary>
		public static void SpawnUser(WorldConnection conn, WorldCharacter user, bool isSelf)
		{
			// Explicit cast because Op.cs might name 0x33 as CS_MOVEMENT
			var packet = new Packet((Op)0x33);
			packet.PutByte(0x00); // SubOpcode: Spawn User
			packet.PutUInt(user.UserId);
			packet.PutByte((byte)(isSelf ? 1 : 0));

			// Position & Movement
			packet.PutByte((byte)(user.IsGM ? 1 : 0));
			packet.PutByte(0); // Unk
			packet.PutShort(0); // Unk
			packet.PutUShort(user.X);
			packet.PutUShort(user.Y);
			packet.PutByte((byte)user.Direction);
			packet.PutByte(0); // Unk

			// Basic Stats Block
			packet.PutBasicStatsBlock(user);

			// Name
			// Name (Latin1 length prefixed, no null term)
			packet.PutString(user.Name, false);

			packet.PutUInt(user.GuildInfo.GuildId);

			// Health (u64 in legacy, BigEndian)
			packet.PutBytes(BitConverter.GetBytes((ulong)user.CurrentHP).Reverse().ToArray());
			packet.PutBytes(BitConverter.GetBytes((ulong)user.MaxHP).Reverse().ToArray());

			packet.PutGuildTeamInfo(user.GuildInfo);

			// Visuals
			packet.PutByte(0); // Unk3
			packet.PutUShort(0); // Unk2
			packet.PutUShort(user.Outfit);
			packet.PutUInt(0); // Unk4
			packet.PutUInt(user.ModelId);

			// Equipment Bitmask and Data
			packet.PutEquipmentBlock(user.Equipment);

			// Appearance (Simplified)
			packet.PutByte(0); // Unk4
			packet.PutByte(0); // Unk5

			packet.PutAppearanceBlock(user.Appearance);

			packet.PutUInt(0); // Unk6
			packet.PutByte(0); // Conditional Guild Name Flag
			packet.PutUInt(0); // Unk7
			packet.PutUInt(user.TitleId);

			packet.PutUserStateBlock();
			packet.PutFinalStatusBlock(user);

			conn.Send(packet);
		}

		/// <summary>
		/// Removes an entity from the client.
		/// Replaces EntitySpawnPacket (Action 1)
		/// </summary>
		public static void EntityRemove(WorldConnection conn, uint objectId)
		{
			var packet = new Packet(Op.WorldResponse);
			packet.PutByte((byte)WorldPacketId.Despawn); // 0x01
			packet.PutUInt(objectId);
			conn.Send(packet);
		}

		/// <summary>
		/// Sends entity click acknowledgment when player clicks on an NPC/entity.
		/// </summary>
		public static void EntityClickAck(WorldConnection conn, uint entityId)
		{
			// 0x70 packet - acknowledges entity was clicked
			var packet = new Packet(Op.EntityClickAck);
			packet.PutByte(0x00);
			packet.PutUInt(entityId);
			packet.PutUInt(0); // Unknown
			packet.PutByte(0x00);
			conn.Send(packet);
		}

		/// <summary>
		/// Entity focus packet - sent when player clicks an entity.
		/// Likely controls camera focus or entity highlighting.
		/// </summary>
		public static void EntityFocus(WorldConnection conn, uint objectId)
		{
			var packet = new Packet(Op.EntityFocusResponse);
			packet.PutByte(0x02);
			packet.PutUInt(objectId);
			// Padding pattern from legacy: 32 bytes with 0x01 markers
			packet.PutEmptyBin(12);
			packet.PutByte(0x01);
			packet.PutEmptyBin(4);
			packet.PutByte(0x01);
			packet.PutEmptyBin(4);
			packet.PutByte(0x01);
			packet.PutEmptyBin(4);
			packet.PutByte(0x01);
			packet.PutEmptyBin(4);
			conn.Send(packet);
		}

		/// <summary>
		/// Unknown confirmation packet sent during entity interaction.
		/// </summary>
		public static void InteractionConfirm(WorldConnection conn)
		{
			var packet = new Packet(Op.InteractionConfirmResponse);
			packet.PutByte(0x01);
			packet.PutEmptyBin(3);
			conn.Send(packet);
		}

		/// <summary>
		/// Timer/cooldown packet - possibly interaction cooldown.
		/// </summary>
		public static void InteractionTimer(WorldConnection conn, ushort timerMs = 3000)
		{
			var packet = new Packet(Op.InteractionTimerResponse);
			packet.PutByte(0x0B);
			packet.PutEmptyBin(4);
			packet.PutUShort(timerMs);
			packet.PutEmptyBin(8);
			conn.Send(packet);
		}

		/// <summary>
		/// Entity interaction notification - signals start of interaction.
		/// Uses 0x0B opcode with subtype 0x01.
		/// </summary>
		public static void EntityInteraction(WorldConnection conn, uint objectId)
		{
			var packet = new Packet(Op.UserPositionResponse); // 0x0B
			packet.PutByte(0x01);
			packet.PutUInt(objectId);
			conn.Send(packet);
		}

		/// <summary>
		/// Dialog action packet (0x44 0x05 0x05) - triggers animation or state change.
		/// </summary>
		public static void NpcDialogAction(WorldConnection conn, uint objectId1, uint objectId2)
		{
			var packet = new Packet(Op.FriendDialogResponse);
			packet.PutByte(0x05);
			packet.PutByte(0x05);
			packet.PutUInt(objectId1);
			packet.PutUInt(objectId2);
			conn.Send(packet);
		}

		/// <summary>
		/// Dialog init packet (0x44 0x05 0x00) - initializes dialog state.
		/// </summary>
		public static void NpcDialogInit(WorldConnection conn)
		{
			var packet = new Packet(Op.FriendDialogResponse);
			packet.PutByte(0x05);
			packet.PutByte(0x00);
			packet.PutByte(0x00);
			packet.PutByte(0x01);
			packet.PutByte(0x00);
			conn.Send(packet);
		}

		/// <summary>
		/// Sends a dialog message from an NPC with portrait (0x05 dialog).
		/// Visual novel style - shows portrait in bottom left, hides main UI.
		/// </summary>
		/// <param name="conn">The client connection.</param>
		/// <param name="npcId">The NPC entity ID (unused, kept for API compat).</param>
		/// <param name="modelId">The NPC model ID (for portrait display).</param>
		/// <param name="text">The message to display.</param>
		public static void NpcDialog(WorldConnection conn, uint npcId, uint modelId, string text)
		{
			// 0x44 0x05 0x02 = Text dialog with portrait (visual novel style)
			var packet = new Packet(Op.FriendDialogResponse);
			packet.PutByte((byte)DialogActionType.Dialog);       // 0x05
			packet.PutByte((byte)DialogOptionType.HasOptions);   // 0x02

			// Model ID for portrait
			packet.PutUInt(modelId);

			// Unknown
			packet.PutUInt(0);

			// Message (length-prefixed using PutString)
			packet.PutString(text, true);

			packet.PutByte(0x01); // Unknown trailing byte

			conn.Send(packet);
		}

		/// <summary>
		/// Sends a dialog message from an NPC (simplified version without model).
		/// </summary>
		public static void NpcDialog(WorldConnection conn, uint npcId, string text, bool hasNext)
		{
			// Use FriendDialog sub NpcDialog for simple text
			var packet = new Packet(Op.FriendDialogResponse);
			packet.PutByte((byte)FriendDialogActionType.NpcDialog);

			packet.PutUInt(npcId);
			packet.PutByte(1); // Mode: Standard Text
			packet.PutByte(hasNext ? (byte)1 : (byte)0);
			packet.PutString(text, true);

			conn.Send(packet);
		}

		/// <summary>
		/// Sends a selection menu from an NPC with portrait (0x04 dialog).
		/// Uses Big Endian for dialogId/modelId to match working test.
		/// </summary>
		/// <param name="conn">The client connection.</param>
		/// <param name="dialogId">Dialog ID for answer tracking.</param>
		/// <param name="modelId">The NPC model ID (for portrait display).</param>
		/// <param name="message">The message/question to display.</param>
		/// <param name="options">Menu options for the user to select.</param>
		public static void NpcMenuWithPortrait(WorldConnection conn, ulong dialogId, uint modelId, string message, string[] options)
		{
			// 0x44 0x04 0x02 = Dialog select menu (in-game, with portrait and options)
			var packet = new Packet(Op.FriendDialogResponse);
			packet.PutByte((byte)DialogActionType.DialogSelectMenu); // 0x04
			packet.PutByte(0x02); // Has options flag

			// Dialog answer ID (8 bytes, Big Endian - matches working test)
			packet.PutULong(dialogId);

			// Model ID for portrait (4 bytes, Big Endian - matches working test)
			packet.PutUInt(modelId);

			packet.PutByte(0x00); // Unknown

			// Message (length-prefixed using PutString)
			packet.PutString(message, false);

			// Options
			packet.PutByte((byte)options.Length);
			packet.PutByte(0x00); // Unknown

			foreach (var option in options)
			{
				packet.PutString(option);
			}

			conn.Send(packet);
		}

		/// <summary>
		/// Sends a selection menu from an NPC (simplified version).
		/// </summary>
		public static void NpcMenu(WorldConnection conn, uint npcId, string[] options)
		{
			var packet = new Packet(Op.FriendDialogResponse);
			packet.PutByte((byte)FriendDialogActionType.NpcDialog);

			packet.PutUInt(npcId);
			packet.PutByte(2); // Mode: Menu
			packet.PutByte(0); // Padding

			string menuString = string.Join(":", options);
			packet.PutString(menuString, true);

			conn.Send(packet);
		}

		/// <summary>
		/// Closes the NPC dialog window.
		/// </summary>
		public static void NpcDialogClose(WorldConnection conn, uint npcId)
		{
			// 0x44 0x05 0x01 = Close dialog
			var packet = new Packet(Op.FriendDialogResponse);
			packet.PutByte(0x05);
			packet.PutByte(0x01);
			conn.Send(packet);
		}

		/// <summary>
		/// Opens a shop window for the player.
		/// </summary>
		public static void OpenShop(WorldConnection conn, uint npcId, NpcShop shop)
		{
			// TODO: Implement shop packet structure
		}

		/// <summary>
		/// Broadcasts direction update to all players on the map.
		/// </summary>
		public static void DirectionUpdate(Map map, uint objectId, byte direction, Player? exclude = null)
		{
			var packet = new Packet((Op)0x11); // DirectionUpdate is bidirectional
			packet.PutUInt(objectId);
			packet.PutByte(direction);

			BroadcastToMap(map, packet, exclude);
		}

		/// <summary>
		/// Sends attack acknowledgment to the client.
		/// </summary>
		public static void AttackAck(WorldConnection conn)
		{
			// Legacy: 4A 00 00 00 00 00 00
			var packet = new Packet(Op.AttackAck);
			packet.PutBytes(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
			conn.Send(packet);

			// Legacy: 17 00
			var packet2 = new Packet(Op.DialogResponse);
			packet2.PutByte(0x00);
			conn.Send(packet2);
		}

		/// <summary>
		/// Sends attack result to the client.
		/// </summary>
		public static void AttackResult(WorldConnection conn, uint characterId)
		{
			// Legacy: 48 00 [charId:4] FF
			var packet = new Packet(Op.AttackResultResponse);
			packet.PutByte(0x00);
			packet.PutUInt(characterId);
			packet.PutByte(0xFF);
			conn.Send(packet);

			// Legacy: 6C 03 E7 - Using raw opcode as this is a special case
			var packet2 = new Packet((Op)0x6C);
			packet2.PutByte(0x03);
			packet2.PutByte(0xE7);
			conn.Send(packet2);
		}

		/// <summary>
		/// Broadcasts pose update (sit/stand) to all players on the map.
		/// </summary>
		public static void PoseUpdate(Map map, uint objectId, byte pose)
		{
			// Legacy: 33 02 [charId:4] [pose:1] [padding:36]
			var packet = new Packet((Op)0x33);
			packet.PutByte(0x02); // SubOp for pose change
			packet.PutUInt(objectId);
			packet.PutByte(pose);
			packet.PutBytes(new byte[36]); // Padding

			BroadcastToMap(map, packet, null);
		}

		/// <summary>
		/// Broadcasts entity despawn to all players on the map.
		/// </summary>
		public static void EntityDespawn(Map map, uint objectId, Player? exclude = null)
		{
			var packet = new Packet(Op.WorldResponse);
			packet.PutByte((byte)WorldPacketId.Despawn);
			packet.PutUInt(objectId);

			BroadcastToMap(map, packet, exclude);
		}

		public static void StatUpdateHardcoded(WorldConnection conn)
		{
			var packet = new Packet(Op.StatUpdateResponse);

			packet.PutBinFromHex(@"00 00 FD FF 00 1E 84 8D 00 00 00 00 00 00 01 
				00 02 01 00 00 00 00 00 00 00 00 01 27 00 00 00 
				00 00 00 01 27 00 00 00 00 00 00 00 49 00 00 00 
				00 00 00 00 49 00 00 00 00 00 00 04 C5 00 00 00 
				00 00 00 04 C5 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 02 00 02 00 00 00 00 00 00 00 
				CD 00 00 00 00 00 00 00 64 00 00 00 00 00 00 00 
				FA 00 01 00 00 00 00 00 00 27 10 00 00 00 00 00 
				00 01 27 00 00 00 00 00 00 00 00 49 00 00 00 00 
				00 00 04 C5 00 00 00 00 00 00 00 CD 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 02 00 04 
				00 01 00 03 00 03 00 03 00 03 00 02 00 04 00 01 
				00 03 00 03 00 03 00 03 00 01 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 05 00 19 00 
				02 00 08 00 00 00 02 00 01 00 01 00 00 00 00 16 
				1B 00 00 02 6A 00 00 04 D6 00 00 02 67 00 00 04 
				D0 00 00 00 39 00 00 00 15 59 12 12 00 05 00 05 
				00 05 00 05 00 0A 00 05 00 05 00 0A 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 02 BA 
				7D EF 30 00 00 00 00 00 00 98 96 80 00 00 00 00 
				00 00 00 00 00");

			conn.Send(packet);
		}

		/// <summary>
		/// Sends the stats decoded from hardcoded version.
		/// </summary>
		public static void StatUpdate(WorldConnection conn, WorldCharacter user)
		{
			var packet = new Packet(Op.StatUpdateResponse);

			// === HEADER (Payload offset 0-3) ===
			packet.PutByte(0x00);  // [0] SubType
			packet.PutByte(0x00);  // [1] Flags
			packet.PutByte(0xFD);  // [2] unk1
			packet.PutByte(0xFF);  // [3] unk2

			// === CHARACTER ID (Payload offset 4-7, 4 bytes) ===
			packet.PutUInt(user.ModelId); // 00 1E 84 8D 

			// === PADDING (Payload offset 8-13, 6 bytes) ===
			packet.PutEmptyBin(6); // 00 00 00 00 00 00

			packet.PutByte(0x01);                    // [15] levelFlag1 01
			packet.PutByte(0x00);                    // [16] levelFlag2 00
			packet.PutByte((byte)user.Level);        // [17] level 02
			packet.PutByte(0x01);                    // [18] levelSub/class 01

			packet.PutShort(0); // 00 00

			// --- Vitals (8 Bytes Each) ---
			packet.PutULong(user.MaxHP); // 00 00 00 00 00 00 01 27
			packet.PutULong(user.CurrentHP); // 00 00 00 00 00 00 01 27
			packet.PutULong(user.MaxMP); // 00 00 00 00 00 00 00 49
			packet.PutULong(user.CurrentMP); // 00 00 00 00 00 00 00 49
			packet.PutULong(user.MaxSP); // 00 00 00 00 00 00 04 C5
			packet.PutULong(user.CurrentSP); // 00 00 00 00 00 00 04 C5

			packet.PutEmptyBin(32);

			packet.PutShort((short)user.Level); // 00 02
			packet.PutShort((short)user.Level); // 00 02

			packet.PutLong(user.CurrentExp);    // 00 00 00 00 00 00 00 CD
			packet.PutLong(user.NextExp);       // 00 00 00 00 00 00 00 64
			packet.PutLong(user.LimitExp);      // 00 00 00 00 00 00 00 FA

			packet.PutShort((short)user.StatPoints);     // 00 01

			packet.PutLong(10000);  // 00 00 00 00 00 00 27 10
			packet.PutLong(user.CurrentHP);    // 00 00 00 00 00 00 01 27

			packet.PutByte(0); // 00

			packet.PutULong(user.CurrentMP);
			packet.PutULong(user.CurrentSP);
			packet.PutLong(user.CurrentExp);

			packet.PutEmptyBin(16);

			// --- Total Stats (Stat + Bonus) (Shorts) ---
			packet.PutShort((short)user.TotalStab); // 00 02
			packet.PutShort((short)user.TotalHack); // 00 04
			packet.PutShort((short)user.TotalInt);  // 00 01
			packet.PutShort((short)user.TotalDef);  // 00 03
			packet.PutShort((short)user.TotalMR);   // 00 03
			packet.PutShort((short)user.TotalDex);  // 00 03
			packet.PutShort((short)user.TotalAgi);  // 00 03

			// --- Base Stats (Shorts) ---
			packet.PutShort(user.StatStab); // 00 02
			packet.PutShort(user.StatHack); // 00 04
			packet.PutShort(user.StatInt);  // 00 01
			packet.PutShort(user.StatDef);  // 00 03
			packet.PutShort(user.StatMR);   // 00 03
			packet.PutShort(user.StatDex);  // 00 03
			packet.PutShort(user.StatAgi);  // 00 03

			// Doesn't seem like this is stat points
			// (short)user.StatPoints
			// Changing this value seems to cause a shift in the bytes below.
			packet.PutShort(1);    // 00 01

			packet.PutEmptyBin(17); // 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 

			packet.PutShort(user.EquipStabBonus); // 00 05 (Stab / ?)
			packet.PutShort(user.EquipHackBonus); // 00 19 (Hack/ Cut)
			packet.PutShort(user.EquipDexBonus); // 00 02 (Dex/ Hit Correction?)
			packet.PutShort(user.EquipDefBonus); // 00 08 (DEF / Physical Defense)
			packet.PutShort(user.EquipIntBonus); // 00 00 (Int / Magic Attack)
			packet.PutShort(user.EquipMRBonus); // 00 00 (MR / Magic Defense)
			packet.PutShort(user.EquipAgiBonus); // 00 01 (Agi / Agi Correction?)
			packet.PutShort(user.EquipEvasionBonus); // 00 01 (Agi / Avoid Correction?)
			packet.PutShort(0); // Selection Total?
			packet.PutShort(0);

			packet.PutByte(user.WalkSpeed); // 16
			packet.PutByte(user.RunSpeed); // 1B

			packet.PutInt(user.MinAttack); // Primary weapon attack
			packet.PutInt(user.MaxAttack);

			packet.PutInt(user.MinAttack2); // Secondary/alternate weapon attack
			packet.PutInt(user.MaxAttack2);

			packet.PutInt(user.PhysicalDefense); // 00 00 00 39 (57)
			packet.PutInt(user.MagicalDefense); // 00 00 00 15 (21)
			packet.PutByte(user.HitRate); // 59
			packet.PutByte(user.PhysicalEvasion); // 12
			packet.PutByte(user.MagicalEvasion); // 12

			packet.PutShort(user.NoneAttribute); // 00 05 (None)
			packet.PutShort(user.FireAttribute); // 00 05 (Fire)
			packet.PutShort(user.WaterAttribute); // 00 05 (Water)
			packet.PutShort(user.WindAttribute); // 00 05 (Wind)
			packet.PutShort(user.EarthAttribute); // 00 0A (Earth)
			packet.PutShort(user.LightningAttribute); // 00 05 (Thunder)
			packet.PutShort(user.HolyAttribute); // 00 05 (Holy)
			packet.PutShort(user.DarkAttribute); // 00 0A (Dark)

			packet.PutBinFromHex(@"
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
				00 00 02 BA 7D EF 30 00 00 00 00 00 00 98 96 80 00 00 00 00 
				00 00 00 00 00");

			conn.Send(packet);
		}

		/// <summary>
		/// Sends a character effect (level up, max level, pvp countdown, etc.).
		/// </summary>
		public static void CharEffect(WorldConnection conn, uint objectId, byte effect)
		{
			// Effect codes: 0x01 = LevelUp, 0x02 = MaxLevel, etc.
			var packet = new Packet(Op.CharEffectResponse);
			packet.PutUInt(objectId);
			packet.PutByte(effect);
			conn.Send(packet);
		}

		/// <summary>
		/// Helper method to broadcast a packet to all players on a map.
		/// </summary>
		private static void BroadcastToMap(Map map, Packet packet, Player? exclude)
		{
			foreach (var player in map.GetPlayers())
			{
				if (exclude != null && player.ObjectId == exclude.ObjectId) continue;
				player.Connection.Send(packet);
			}
		}
	}
}