using Kakia.TW.Shared.Network;
using Kakia.TW.Shared.World;
using Kakia.TW.World.Managers;
using Kakia.TW.World.Network;
using System;

namespace Kakia.TW.World.Entities
{
	public class Monster : Entity
	{
		public string Name { get; set; }
		public uint MaxHP { get; set; }
		public uint CurrentHP { get; set; }

		// Reference to the Spawner that created this monster (to notify on death)
		public Spawner SourceSpawner { get; set; }

		public bool IsDead => CurrentHP <= 0;

		public Monster(string name, uint modelId)
		{
			Name = name;
			ModelId = modelId;
			MaxHP = 100; // Default, should load from DB/Data
			CurrentHP = 100;
		}

		public override void Update(TimeSpan elapsed)
		{
			if (IsDead) return;

			// TODO: AI Logic (Wander, Aggro check)
			// if (Target == null) Wander();
			// else Attack(Target);
		}

		public void TakeDamage(uint amount, Entity attacker)
		{
			if (IsDead) return;

			if (amount >= CurrentHP)
			{
				CurrentHP = 0;
				Die(attacker);
			}
			else
			{
				CurrentHP -= amount;
			}
		}

		private void Die(Entity killer)
		{
			// 1. Notify Map to broadcast death packet
			Instance?.Broadcast(this, () =>
			{
				var p = new Packet(Op.WorldResponse); // 0x07
				p.PutByte((byte)WorldPacketId.Died); // 0x02
				p.PutUInt(ObjectId);
				p.PutUInt(killer?.ObjectId ?? 0); // Attacker ObjectId
				return p;
			}, includeSelf: true);

			// 2. Remove from Map
			Instance?.Leave(this);

			// 3. Notify Spawner to start respawn timer
			SourceSpawner?.OnMonsterDied();
		}
	}

	public class Warp : Entity
	{
		public uint PortalId { get; set; } // The visual ID of the portal
		public ushort DestMapId { get; set; }
		public ushort DestZoneId { get; set; }
		public ushort DestX { get; set; }
		public ushort DestY { get; set; }

		public Warp()
		{
		}

		public override void Update(TimeSpan elapsed)
		{
			// Warps are static, no update logic needed usually
		}
	}
}