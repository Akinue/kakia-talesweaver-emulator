using Kakia.TW.Shared.Network;
using Kakia.TW.Shared.World;
using Kakia.TW.World.Entities;
using Kakia.TW.World.Network;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Kakia.TW.World.Managers
{
	public class Map : IUpdateable
	{
		public ushort MapId { get; }
		public ushort ZoneId { get; }

		// Thread-safe collections for entities
		private readonly ConcurrentDictionary<uint, Player> _players = new();
		private readonly ConcurrentDictionary<uint, ItemEntity> _items = new();
		private readonly ConcurrentDictionary<uint, Npc> _npcs = new();
		private readonly ConcurrentDictionary<uint, Monster> _monsters = new();
		private readonly ConcurrentDictionary<uint, Warp> _warps = new();
		private readonly List<Spawner> _spawners = new();

		// Raw entity packets loaded from .bin files (sent directly to clients)
		private List<byte[]> _rawEntityPackets = new();

		public Map(ushort mapId, ushort zoneId)
		{
			MapId = mapId;
			ZoneId = zoneId;
		}

		/// <summary>
		/// Sets raw entity packets to be sent to players when they enter the map.
		/// These are the original captured packets from the client.
		/// </summary>
		public void SetRawEntityPackets(List<byte[]> packets)
		{
			_rawEntityPackets = packets;
		}

		public void Update(TimeSpan elapsed)
		{
			// 1. Update Players
			foreach (var p in _players.Values)
			{
				p.Update(elapsed);
				CheckWarpCollision(p); // Check if player walked into a portal
			}

			// 2. Update Monsters
			foreach (var m in _monsters.Values) m.Update(elapsed);

			// 3. Update Spawners
			foreach (var s in _spawners) s.Update(elapsed);
		}

		public void Enter(Entity entity)
		{
			if (entity is Player p)
			{
				if (_players.TryAdd(p.Id, p))
				{
					p.Instance = this;
					Send.MapChange(p.Connection, MapId, ZoneId);

					// Spawn existing entities for the player
					foreach (var m in _monsters.Values) Send.SpawnNpc(p.Connection, m.Id, m.ModelId, m.ObjectPos.Position, m.Direction);
					foreach (var w in _warps.Values) Send.SpawnPortal(p.Connection, new WarpPortal { Id = w.Id, MinPoint = w.ObjectPos.Position, DestMapId = w.DestMapId, DestPortalId = 1 });

					// Spawn this player for others
					Broadcast(p, () =>
					{
						var pkt = new Packet((Op)0x33);
						// ... (Reuse spawn logic from Send.SpawnUser)
						return pkt;
					}, includeSelf: false);
				}
			}
			else if (entity is Monster m)
			{
				if (_monsters.TryAdd(m.Id, m))
				{
					Broadcast(m, () =>
					{
						var pkt = new Packet(Op.WorldResponse);
						// Using Send.EntitySpawnNpc logic inline or via helper
						pkt.PutByte((byte)WorldPacketId.Spawn); // 0 Spawn
						pkt.PutByte(0x02); // Type 2: Monster/NPC
						pkt.PutUInt(m.Id);
						pkt.PutUInt(m.ModelId);
						pkt.PutUInt(0); // Unk
						pkt.PutUInt(0); // Unk
						pkt.PutUShort(m.ObjectPos.Position.X);
						pkt.PutUShort(m.ObjectPos.Position.Y);
						pkt.PutByte(m.Direction);
						return pkt;
					}, includeSelf: false);
				}
			}
			else if (entity is Warp w)
			{
				_warps.TryAdd(w.Id, w);
				// Warps are usually static, so we might not broadcast spawn if added during load,
				// but if added dynamically:
				Broadcast(w, () =>
				{
					var pkt = new Packet(Op.WorldResponse);
					pkt.PutByte((byte)WorldPacketId.Spawn);
					pkt.PutByte(0x04); // Type 4: Portal
					pkt.PutUInt(w.Id);
					pkt.PutUShort(w.ObjectPos.Position.X);
					pkt.PutUShort(w.ObjectPos.Position.Y);
					pkt.PutUShort(w.DestMapId);
					pkt.PutUShort(1); // Dest Portal ID (visual only usually)
					return pkt;
				}, includeSelf: false);
			}
		}

		public void Enter(Player player)
		{
			if (_players.TryAdd(player.Id, player))
			{
				player.Instance = this;

				// 1. Send Map Change Packet to Player
				Send.MapChange(player.Connection, MapId, ZoneId);

				// 2. Notify Player about existing entities
				foreach (var existingPlayer in _players.Values)
				{
					if (existingPlayer.Id == player.Id) continue;
					// Send existing player to new player
					Send.SpawnUser(player.Connection, existingPlayer.Data, false);
					// Send new player to existing player
					Send.SpawnUser(existingPlayer.Connection, player.Data, false);
				}

				foreach (var item in _items.Values)
				{
					Send.SpawnItem(player.Connection, item.ItemData, item.ObjectPos.Position, item.OwnerId);
				}

				// Send existing NPCs to new player
				foreach (var npc in _npcs.Values)
				{
					Send.SpawnNpc(player.Connection, npc.Id, npc.ModelId, npc.ObjectPos.Position, npc.Direction);
				}

				// Send existing monsters to new player
				foreach (var monster in _monsters.Values)
				{
					Send.SpawnNpc(player.Connection, monster.Id, monster.ModelId, monster.ObjectPos.Position, monster.Direction);
				}
			}
		}

		public void Leave(Player player)
		{
			if (_players.TryRemove(player.Id, out _))
			{
				player.Instance = null;
				// Broadcast removal to others
				Broadcast(player, () =>
				{
					var p = new Packet(Op.WorldResponse);
					p.PutByte((byte)WorldPacketId.Despawn);
					p.PutUInt(player.Id);
					return p;
				}, includeSelf: false);
			}
		}

		public void Leave(Entity entity)
		{
			if (entity is Monster m)
			{
				_monsters.TryRemove(m.Id, out _);
				// Death packet handled in Monster.Die
			}
			else if (entity is Player p)
			{
				_players.TryRemove(p.Id, out _);
				p.Instance = null;
				// Broadcast vanish
			}
		}

		public void AddSpawner(Spawner spawner)
		{
			_spawners.Add(spawner);
		}

		public void Broadcast(Entity source, Func<Packet> packetBuilder, bool includeSelf = false)
		{
			var packet = packetBuilder();
			// In a real scenario, use QuadTree or Grid for range checks
			foreach (var p in _players.Values)
			{
				if (!includeSelf && p.Id == source.Id) continue;

				// Simple distance check (e.g. 20 tiles)
				if (IsInRange(source.ObjectPos, p.ObjectPos))
				{
					p.Connection.Send(packet);
				}
			}
		}

		private void CheckWarpCollision(Player p)
		{
			foreach (var w in _warps.Values)
			{
				// Simple distance check (Portal is usually 1x1 or 3x3)
				int dist = Math.Abs(p.ObjectPos.Position.X - w.ObjectPos.Position.X) +
						   Math.Abs(p.ObjectPos.Position.Y - w.ObjectPos.Position.Y);

				if (dist <= 1)
				{
					// Trigger Warp
					p.Warp(w.DestMapId, w.DestZoneId, w.DestX, w.DestY);
					break;
				}
			}
		}

		private bool IsInRange(ObjectPos a, ObjectPos b)
		{
			var dx = a.Position.X - b.Position.X;
			var dy = a.Position.Y - b.Position.Y;
			// 20*20 = 400
			return (dx * dx + dy * dy) <= 400;
		}

		public bool TryGetNpc(uint id, out Npc? npc)
		{
			return _npcs.TryGetValue(id, out npc);
		}

		/// <summary>
		/// Gets all players currently on this map.
		/// </summary>
		public IEnumerable<Player> GetPlayers()
		{
			return _players.Values;
		}

		/// <summary>
		/// Gets a player by ID if they are on this map.
		/// </summary>
		public bool TryGetPlayer(uint id, out Player? player)
		{
			return _players.TryGetValue(id, out player);
		}

		/// <summary>
		/// Finds a portal at the given coordinates.
		/// </summary>
		public Warp? FindPortalAt(ushort x, ushort y)
		{
			foreach (var warp in _warps.Values)
			{
				// Simple distance check (portal collision radius of 2 tiles)
				int dx = Math.Abs(x - warp.ObjectPos.Position.X);
				int dy = Math.Abs(y - warp.ObjectPos.Position.Y);

				if (dx <= 2 && dy <= 2)
				{
					return warp;
				}
			}
			return null;
		}

		/// <summary>
		/// Adds a warp portal to the map.
		/// </summary>
		public void AddWarp(Warp warp)
		{
			_warps.TryAdd(warp.Id, warp);
		}

		/// <summary>
		/// Adds an NPC to the map and broadcasts spawn to all players.
		/// </summary>
		public void AddNpc(Npc npc)
		{
			if (_npcs.TryAdd(npc.Id, npc))
			{
				npc.Instance = this;

				// Broadcast NPC spawn to all players on the map
				foreach (var player in _players.Values)
				{
					Send.SpawnNpc(player.Connection, npc.Id, npc.ModelId, npc.ObjectPos.Position, npc.Direction);
				}
			}
		}

		/// <summary>
		/// Removes an NPC from the map and broadcasts despawn to all players.
		/// </summary>
		public void RemoveNpc(uint npcId)
		{
			if (_npcs.TryRemove(npcId, out var npc))
			{
				npc.Instance = null;

				// Broadcast NPC removal to all players
				foreach (var player in _players.Values)
				{
					Send.EntityRemove(player.Connection, npcId);
				}
			}
		}

		/// <summary>
		/// Adds a monster to the map and broadcasts spawn to all players.
		/// </summary>
		public void AddMonster(Monster monster)
		{
			if (_monsters.TryAdd(monster.Id, monster))
			{
				monster.Instance = this;

				// Broadcast monster spawn to all players on the map
				foreach (var player in _players.Values)
				{
					Send.SpawnNpc(player.Connection, monster.Id, monster.ModelId, monster.ObjectPos.Position, monster.Direction);
				}
			}
		}

		/// <summary>
		/// Removes a monster from the map and broadcasts despawn to all players.
		/// </summary>
		public void RemoveMonster(uint monsterId)
		{
			if (_monsters.TryRemove(monsterId, out var monster))
			{
				monster.Instance = null;

				// Broadcast monster removal to all players
				foreach (var player in _players.Values)
				{
					Send.EntityRemove(player.Connection, monsterId);
				}
			}
		}

		/// <summary>
		/// Gets a monster by ID if it exists on this map.
		/// </summary>
		public bool TryGetMonster(uint id, out Monster? monster)
		{
			return _monsters.TryGetValue(id, out monster);
		}
	}
}