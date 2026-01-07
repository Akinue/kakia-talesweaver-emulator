using Kakia.TW.World.Events;
using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using Yggdrasil.Logging;

namespace Kakia.TW.World.Managers
{
	public class WorldManager
	{
		private int _nextEntityId = 1_000_000;

		/// <summary>
		/// Returns a reference to a collection of maps in the world.
		/// </summary>
		public MapManager Maps { get; } = new MapManager();

		/// <summary>
		/// The world's heartbeast, which controls timed events.
		/// </summary>
		public Heartbeat Heartbeat { get; } = new Heartbeat();

		/// <summary>
		/// Generates a unique runtime Entity ID in a thread-safe manner.
		/// Used for Monsters, NPCs, Warps, and Item Drops.
		/// </summary>
		public uint GetNextEntityId()
		{
			return (uint)Interlocked.Increment(ref _nextEntityId);
		}

		public bool TryGetPlayer(uint id, out Player player)
		{
			// Search via MapManager or a global dictionary
			return Maps.TryGetPlayer(id, out player);
		}

		/// <summary>
		/// Starts the world's heartbeat if it isn't already running.
		/// </summary>
		public void Start()
		{
			this.Heartbeat.Start();
		}
	}
}