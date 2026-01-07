using Kakia.TW.Shared.World;
using System.Text;
using System.Linq;

namespace Kakia.TW.Shared.Network.Helpers
{
	public static class PacketCharacterExtensions
	{
		/// <summary>
		/// Writes the character data block specifically for Lobby Selection (0x6B).
		/// Matches the structure from the original emulator's Character.cs.
		/// </summary>
		public static void PutLobbyCharacterListEntry(this Packet packet, WorldCharacter c)
		{
			// 1. Last Login Time
			packet.PutUInt(c.LastLoginTime > 0 ? c.LastLoginTime : (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

			// 2. Creation Time
			packet.PutUInt(c.CreationTime > 0 ? c.CreationTime : (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

			// 3. Unknown (Int + Byte)
			packet.PutInt(0);
			packet.PutByte(0);

			// 4. Model ID (The outer container model)
			if (c.ModelId < 2000000)
				c.ModelId = 2000000;
			packet.PutUInt(c.ModelId);

			packet.PutBinFromHex(@"00 01 00 00 00 00 00 0E 00 0F A0 32 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 01 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 0F A1 6E 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 01 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 0F A1 E7 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 FF 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
						00 00 00 00 00 00 00 00 00 00 00");

			/**
			// 5. THE VISUAL BLOB (Dynamic Generation)
			// Instead of a hardcoded array, we write the actual character data.
			// This matches the internal structure of SpawnCharacterPacket (0x33) body.

			// 5.1 Flags
			packet.PutByte(0); // Unk/SubOpcode equivalent in this context
			packet.PutByte(1); // Flag (Active/IsMe)

			// 5.2 Movement/Position Data
			packet.PutByte(c.IsGM ? (byte)1 : (byte)0); // GM
			packet.PutByte(0); // UnkByte1
			packet.PutShort(0); // UnkShort1
			packet.PutUShort(c.Position.Position.X);
			packet.PutUShort(c.Position.Position.Y);
			packet.PutByte(c.Position.Direction);
			packet.PutByte(0); // Unk

			// 5.3 Basic Stats Block
			packet.PutBasicStatsBlock();

			// 5.4 Name (Inside the blob)
			packet.PutString(c.Name, false);

			// 5.5 Guild / Stats
			packet.PutUInt(c.GuildInfo.GuildId);
			packet.PutLong(c.CurrentHP);
			packet.PutLong(c.MaxHP);

			// 5.6 Guild/Team Info
			packet.PutGuildTeamInfo(c.GuildInfo);

			// 5.7 Visual Header
			packet.PutByte(0); // UnkByte3
			packet.PutUShort(0); // Unk2
			packet.PutUShort(c.Outfit);
			packet.PutUInt(0); // UnkInt4
			packet.PutUInt(c.ModelId); // Model ID repeated inside

			// 5.8 Equipment
			packet.PutEquipmentBlock(c.Equipment);

			// 5.9 Visual Footer
			packet.PutByte(0); // UnkByte4
			packet.PutByte(0); // UnkByte5

			// 5.10 Appearance
			packet.PutAppearanceBlock(c.Appearance);

			packet.PutUInt(0); // UnkInt6

			// 5.11 Conditional Guild Name
			if (!string.IsNullOrEmpty(c.GuildInfo.GuildName))
			{
				packet.PutByte(1);
				packet.PutString(c.GuildInfo.GuildName, false);
			}
			else
			{
				packet.PutByte(0);
			}

			// 5.12 Title & State
			packet.PutUInt(0); // UnkInt7
			packet.PutUInt(c.TitleId);

			packet.PutUserStateBlock();

			// 5.13 Final Status
			packet.PutFinalStatusBlock(c);
			**/
			// 6. Character ID (Footer)
			packet.PutUInt(c.UserId);

			// 7. Name (Length + String, NO null terminator after string bytes based on original ToBytes)
			packet.PutString(c.Name, false);
		}

		/// <summary>
		/// Writes the full character data block used in Lobby Selection (0x6B) and Spawn (0x33).
		/// </summary>
		public static void PutLobbyCharacterData(this Packet packet, WorldCharacter c)
		{
			// 1. User ID
			packet.PutUInt(c.UserId);

			// 2. IsMe / Flag (In Lobby 0x6B this is typically 1)
			packet.PutByte(1);

			// 3. GM Flag & Unknowns (Movement/Spawn header part)
			packet.PutByte(c.IsGM ? (byte)1 : (byte)0);
			packet.PutByte(0); // UnkByte1
			packet.PutShort(0); // UnkShort1

			// 4. Position
			packet.PutUShort(c.ObjectPos.Position.X);
			packet.PutUShort(c.ObjectPos.Position.Y);
			packet.PutByte(c.ObjectPos.Direction);

			packet.PutByte(0); // Unk

			// 5. Basic Stats Block
			packet.PutBasicStatsBlock(c);

			// 6. Name (Length prefixed, Latin1/KSC)
			packet.PutString(c.Name, false);

			// 7. Guild / Stats
			packet.PutUInt(c.GuildInfo.GuildId);
			packet.PutLong(c.CurrentHP); // Often sent as ulong in this specific block
			packet.PutLong(c.MaxHP);

			// 8. Guild/Team Info
			packet.PutGuildTeamInfo(c.GuildInfo);

			// 9. Visual Header
			packet.PutByte(0); // UnkByte3
			packet.PutUShort(0); // Unk2
			packet.PutUShort(c.Outfit);
			packet.PutUInt(0); // UnkInt4
			packet.PutUInt(c.ModelId);

			// 10. Equipment
			packet.PutEquipmentBlock(c.Equipment);

			// 11. Visual Footer
			packet.PutByte(0); // UnkByte4
			packet.PutByte(0); // UnkByte5

			// 12. Appearance
			packet.PutAppearanceBlock(c.Appearance);

			packet.PutUInt(0); // UnkInt6

			// 13. Conditional Guild Name
			// If not empty, write 1 + String, else 0
			if (!string.IsNullOrEmpty(c.GuildInfo.GuildName))
			{
				packet.PutByte(1);
				packet.PutString(c.GuildInfo.GuildName, false);
			}
			else
			{
				packet.PutByte(0);
			}

			// 14. Title & State
			packet.PutUInt(0); // UnkInt7
			packet.PutUInt(c.TitleId);

			packet.PutUserStateBlock();

			// 15. Final Status (HP/MP/SP)
			packet.PutFinalStatusBlock(c);
		}

		/// <summary>
		/// Writes the Basic Stats Block.
		/// </summary>
		public static void PutBasicStatsBlock(this Packet packet, WorldCharacter c)
		{
			packet.PutByte(0); // UnknownFlag1
			packet.PutByte(0); // UnknownFlag2
			packet.PutUShort((ushort)c.Level);
			packet.PutUShort((ushort)c.StatStab);
			packet.PutUShort((ushort)c.StatHack);
			packet.PutUShort((ushort)c.StatInt);
		}

		/// <summary>
		/// Writes the Basic Stats Block with default values.
		/// </summary>
		public static void PutBasicStatsBlock(this Packet packet)
		{
			packet.PutByte(0); // UnknownFlag1
			packet.PutByte(0); // UnknownFlag2
			packet.PutUShort(1); // Level
			packet.PutUShort(1); // Stat1
			packet.PutUShort(1); // Stat2
			packet.PutUShort(1); // Stat3
		}

		/// <summary>
		/// Writes Guild and Team information.
		/// </summary>
		public static void PutGuildTeamInfo(this Packet packet, GuildTeamInfo info)
		{
			packet.PutByte(info.HasGuildInfo);
			if (info.HasGuildInfo)
			{
				packet.PutString(info.GuildName, false); // Length-prefixed, no null term
				packet.PutUInt(info.GuildId);
				packet.PutString(info.TeamName, false);
				packet.PutUShort(info.MarkId);
				packet.PutUShort(info.MarkColor);
			}
		}

		/// <summary>
		/// Writes the complex character appearance block.
		/// </summary>
		public static void PutAppearanceBlock(this Packet packet, CharacterAppearance app)
		{
			packet.PutByte(0); // UnknownAppearanceFlag

			// 9 Visual Slots
			for (int i = 0; i < 9; i++)
			{
				byte type = app.Types[i];
				packet.PutByte(type);

				if (type == 1) // Type Visual
				{
					packet.PutUInt(app.VisualIds[i]);
					packet.PutUInt(app.VisualIds2[i]);
				}
				else // Type Color/Index
				{
					packet.PutByte(app.Colors[i]);
				}
			}

			// 4 Properties
			for (int i = 0; i < 4; i++)
			{
				packet.PutUInt(app.Props[i]); // ID
				packet.PutByte(app.PropTypes[i]); // Type

				if (app.PropTypes[i] == 1)
					packet.PutUInt(app.Props[i]);
				else
					packet.PutByte((byte)app.Props[i]);
			}
		}

		/// <summary>
		/// Writes the User State block.
		/// </summary>
		public static void PutUserStateBlock(this Packet packet)
		{
			packet.PutByte(0); // Flag1
			for (int i = 0; i < 8; i++) packet.PutUInt(0); // StateData
			packet.PutByte(0); // Bool
			packet.PutByte(0); // Flag2
			packet.PutByte(0); // Discarded
			packet.PutByte(0); // Flag3
		}

		/// <summary>
		/// Writes the Final Status block (HP/MP/SP updates).
		/// </summary>
		public static void PutFinalStatusBlock(this Packet packet, WorldCharacter c)
		{
			packet.PutByte(0); // Unk6
			packet.PutByte(0); // UnkBool2
			packet.PutByte(0); // Unk7
			packet.PutUInt(c.CurrentHP);
			packet.PutUInt(c.MaxHP);
			packet.PutUInt(c.CurrentMP);

			// SP is ulong (8 bytes) reversed in legacy logic
			packet.PutBytes(BitConverter.GetBytes(c.CurrentSP).Reverse().ToArray());

			packet.PutByte(0); // Unk8
		}

		/// <summary>
		/// Reads the Character Appearance block from the packet.
		/// Used during Character Creation.
		/// </summary>
		public static CharacterAppearance GetAppearanceBlock(this Packet packet)
		{
			var app = new CharacterAppearance();

			packet.GetByte(); // Unknown Appearance Flag

			// 9 Visual Slots (Hair, Face, etc.)
			for (int i = 0; i < 9; i++)
			{
				byte type = packet.GetByte();
				app.Types[i] = type;

				if (type == 1) // Type Visual (Mesh/Texture IDs)
				{
					app.VisualIds[i] = packet.GetUInt();
					app.VisualIds2[i] = packet.GetUInt();
				}
				else // Type Color/Index
				{
					app.Colors[i] = packet.GetByte();
				}
			}

			// 4 Properties (Rank, Coat of Arms, etc.)
			// Structure: [ID: u32] [Type: u8] [Value: u32/u8]
			for (int i = 0; i < 4; i++)
			{
				// In some versions, the client sends the ID it wants to set.
				// We usually trust the server-side ID logic, but we must read the stream.
				uint propId = packet.GetUInt();

				byte type = packet.GetByte();
				app.PropTypes[i] = type;

				if (type == 1) // Int Value
				{
					// We store the value in Props array. 
					// Note: If you need to store the ID as well, the model needs expansion.
					app.Props[i] = packet.GetUInt();
				}
				else // Byte Value
				{
					app.Props[i] = packet.GetByte();
				}
			}

			return app;
		}
	}
}