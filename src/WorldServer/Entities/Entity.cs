using Kakia.TW.Shared.World;
using Kakia.TW.World.Managers;
using Kakia.TW.World.Network;
using System;

namespace Kakia.TW.World.Entities
{
	public abstract class Entity
	{
		/// <summary>
		/// Runtime object ID assigned by the map. Used in network packets.
		/// Unique within a map, can be reused across different maps.
		/// </summary>
		public uint ObjectId { get; internal set; }

		public uint ModelId { get; set; }

		public ObjectPos ObjectPos { get; set; } = new();
		public Map Instance { get; set; }

		public abstract void Update(TimeSpan elapsed);
	}

	public class ItemEntity : Entity
	{
		public GameItem ItemData { get; set; }
		public int OwnerId { get; set; }
		public DateTime DroppedTime { get; set; }

		public ItemEntity(GameItem item, int ownerId)
		{
			ItemData = item;
			OwnerId = ownerId;
			DroppedTime = DateTime.Now;
		}

		public override void Update(TimeSpan elapsed)
		{
			// Check expiry
		}
	}
}