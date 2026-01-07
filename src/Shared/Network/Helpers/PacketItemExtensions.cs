using Kakia.TW.Shared.World;

namespace Kakia.TW.Shared.Network.Helpers
{
	public static class PacketItemExtensions
	{
		/// <summary>
		/// Writes a single item (Inventory/Drop style).
		/// Matches Legacy GameItem logic.
		/// </summary>
		public static void PutGameItem(this Packet packet, GameItem item)
		{
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
		}

		/// <summary>
		/// Writes the Equipment Bitmask and Data block for Character Spawn.
		/// Matches Legacy SpawnCharacterPacket._pack_equipment
		/// </summary>
		public static void PutEquipmentBlock(this Packet packet, List<GameItem> equipment)
		{
			uint mask = 0;
			var slots = new GameItem[22];

			foreach (var item in equipment)
			{
				if (item.Slot >= 0 && item.Slot < 22)
				{
					mask |= (1u << item.Slot);
					slots[item.Slot] = item;
				}
			}

			packet.PutInt((int)mask);

			for (int i = 0; i < 22; i++)
			{
				if (slots[i] != null)
				{
					var item = slots[i];
					packet.PutUInt((uint)item.ItemId);
					packet.PutUInt(item.VisualId);

					// Stat Blocks (Legacy had 2 blocks)
					// Currently sending empty stats for spawn visual
					packet.PutByte(0); // No stats block 1
					packet.PutByte(0); // No stats block 2

					packet.PutShort(item.Durability);

					// Magic Properties
					packet.PutByte((byte)item.MagicProperties.Count);
					foreach (var prop in item.MagicProperties)
					{
						packet.PutByte(prop.PropertyId);
						packet.PutByte(prop.PropertyType);
						packet.PutUInt(prop.Value1);

						// Switch based on type (Simplified from legacy)
						if (prop.PropertyType == 1 || prop.PropertyType >= 4)
						{
							packet.PutUInt(prop.Value2);
						}
						else if (prop.PropertyType == 2 && prop.StatValues != null)
						{
							foreach (var stat in prop.StatValues)
								packet.PutUShort(stat);
						}
					}
				}
			}
		}
	}
}