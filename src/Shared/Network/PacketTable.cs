using System;
using System.Collections.Generic;
using System.Linq;
using Yggdrasil.Logging;

namespace Kakia.TW.Shared.Network
{
	/// <summary>
	/// Manages a list of packet opcodes and their lengths.
	/// </summary>
	public static partial class PacketTable
	{
		/// <summary>
		/// Numeric value indicating a dynamic packet size.
		/// </summary>
		public const int Dynamic = -1;

		private static readonly List<PacketTableEntry> Entries = new();
		private static readonly Dictionary<int, int> Sizes = new();
		private static readonly Dictionary<int, string> Names = new();
		private static readonly Dictionary<int, Op> NetworkToHost = new();
		private static readonly Dictionary<Op, int> HostToNetwork = new();

		/// <summary>
		/// Loads the packet definitions.
		/// </summary>
		public static void Load()
		{
			// Load Tales Weaver Packet Definitions
			LoadTalesWeaver();

			BuildLists();
		}

		/// <summary>
		/// Adds a new packet to the table.
		/// </summary>
		/// <param name="op">The op code the packet will be know as internally.</param>
		/// <param name="opNetwork">The op code that is sent/received by the client.</param>
		/// <param name="size">The size of the packet (-1 for dynamic).</param>
		private static void Register(Op op, int opNetwork, int size)
		{
			var newEntry = new PacketTableEntry(op, opNetwork, size);

			if (Entries.Any(a => a.Op == op))
				throw new ArgumentException($"Op {op} was already added.");

			// For TW, we don't need the complex shifting logic RO uses.
			// We just register the direct mapping.
			Entries.Add(newEntry);
		}

		/// <summary>
		/// Builds quick access lists for the packet table.
		/// </summary>
		private static void BuildLists()
		{
			foreach (var entry in Entries)
			{
				NetworkToHost[entry.OpNetwork] = entry.Op;
				HostToNetwork[entry.Op] = entry.OpNetwork;
				Names[entry.OpNetwork] = entry.Op.ToString();
				Sizes[entry.OpNetwork] = entry.Size;
			}
		}

		/// <summary>
		/// Returns the network op for the given op.
		/// </summary>
		public static int ToNetwork(Op op)
		{
			if (!HostToNetwork.TryGetValue(op, out var opNetwork))
				throw new ArgumentException($"Op {op} not found.");

			return opNetwork;
		}

		/// <summary>
		/// Returns the op for the given network op.
		/// </summary>
		public static Op ToHost(int opNetwork)
		{
			if (!NetworkToHost.TryGetValue(opNetwork, out var op))
			{
				// Return a default or throw based on preference. 
				// Throwing helps catch unknown packets during dev.
				//throw new ArgumentException($"Op '0x{opNetwork:X2}' not found.");
				Log.Warning($"Op '0x{opNetwork:X2}' not found.");
				return Op.Unknown;
			}

			return op;
		}

		/// <summary>
		/// Returns the size of packets with the given opcode. If size is
		/// -1 (PacketTable.Dynamic), the packet's size is dynamic.
		/// </summary>
		public static int GetSize(int opNetwork)
		{
			if (!Sizes.TryGetValue(opNetwork, out var size))
				throw new ArgumentException($"No size found for op '0x{opNetwork:X2}'.");

			return size;
		}

		/// <summary>
		/// Returns the name of the given opcode.
		/// </summary>
		public static string GetName(int opNetwork)
		{
			if (!Names.TryGetValue(opNetwork, out var name))
				return "?";

			return name;
		}

		public class PacketTableEntry
		{
			public Op Op { get; }
			public int OpNetwork { get; set; }
			public int Size { get; set; }

			public PacketTableEntry(Op op, int opNetwork, int size)
			{
				this.Op = op;
				this.OpNetwork = opNetwork;
				this.Size = size;
			}
		}
	}
}