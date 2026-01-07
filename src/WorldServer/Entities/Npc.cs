using Kakia.TW.Shared.World;
using Kakia.TW.World.Managers;
using Kakia.TW.World.Network;
using Kakia.TW.World.Scripting;
using System;
using System.Threading.Tasks;

namespace Kakia.TW.World.Entities
{
	public delegate Task DialogFunc(Dialog dialog);

	public class Npc : Entity
	{
		public string Name { get; set; }
		public byte Direction { get; set; }

		// The script to run when clicked
		public DialogFunc? Script { get; set; }

		public Npc(string name, uint modelId)
		{
			Name = name;
			ModelId = modelId;
		}

		public override void Update(TimeSpan elapsed) { }
	}

	public class NpcShop
	{
		public string Name { get; set; }
		public List<int> ItemIds { get; } = new();

		public void AddItem(int itemId) => ItemIds.Add(itemId);
	}
}