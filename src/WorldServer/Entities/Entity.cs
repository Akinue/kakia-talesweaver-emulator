using Kakia.TW.Shared.World;
using Kakia.TW.World.Managers;
using Kakia.TW.World.Network;
using System;

namespace Kakia.TW.World.Entities
{
	public abstract class Entity
	{
		public uint Id { get; set; }
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