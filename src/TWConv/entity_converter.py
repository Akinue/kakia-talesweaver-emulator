#!/usr/bin/env python3
"""
TalesWeaver EntitySpawnPacket Binary to C# Script Converter

Converts saved EntitySpawnPacket binary files into C# scripts compatible with
the Kakia.TW.World.Scripting system.

File Format (saved packets use little-endian):
- Byte 0: Opcode (0x07)
- Byte 1: ActionType (0x00=Spawn, 0x01=Despawn, 0x02=Die)
- Byte 2: SpawnType/EntityType (when ActionType=0x00)
- Bytes 3+: Payload (varies by SpawnType)

SpawnType values (from EntitySpawnPacket.cs):
- 0x00: Unknown/Monster (short format in saved files)
- 0x01: Player/NPC (extended format in saved files)
- 0x02: MonsterNpc
- 0x03: Item
- 0x04: Portal
- 0x05: Reactor
- 0x06: Pet
- 0x07: MonsterNpc2
- 0x08: SummonedCreature
- 0x09: MonsterNpc3
"""

import os
import sys
import struct
import json
from pathlib import Path
from dataclasses import dataclass, field
from typing import List, Dict, Optional, Any
from datetime import datetime


# ============================================================================
# Entity Type Constants (matching EntitySpawnPacket.cs SpawnType enum)
# ============================================================================

class ActionType:
    SPAWN = 0x00
    DESPAWN = 0x01
    DIE = 0x02


class SpawnType:
    UNKNOWN = 0x00
    PLAYER = 0x01
    MONSTER_NPC = 0x02
    ITEM = 0x03
    PORTAL = 0x04
    REACTOR = 0x05
    PET = 0x06
    MONSTER_NPC2 = 0x07
    SUMMONED_CREATURE = 0x08
    MONSTER_NPC3 = 0x09


SPAWN_TYPE_NAMES = {
    SpawnType.UNKNOWN: "Unknown",
    SpawnType.PLAYER: "Player",
    SpawnType.MONSTER_NPC: "MonsterNpc",
    SpawnType.ITEM: "Item",
    SpawnType.PORTAL: "Portal",
    SpawnType.REACTOR: "Reactor",
    SpawnType.PET: "Pet",
    SpawnType.MONSTER_NPC2: "MonsterNpc2",
    SpawnType.SUMMONED_CREATURE: "SummonedCreature",
    SpawnType.MONSTER_NPC3: "MonsterNpc3",
}


# ============================================================================
# Data Classes for Parsed Entities
# ============================================================================

@dataclass
class MapHeader:
    map_id: int
    zone_id: int


@dataclass
class SpawnPosition:
    entity_type: int
    zone_id: int
    x: int
    y: int
    direction: int


@dataclass
class EntityBase:
    """Base class for all parsed entities."""
    filename: str
    opcode: int
    action_type: int
    spawn_type: int
    raw_data: bytes


@dataclass
class MonsterEntity(EntityBase):
    """Monster/Unknown type entity (SpawnType 0x00 in saved files)."""
    object_id: int = 0
    monster_id: int = 0


@dataclass
class NpcEntity(EntityBase):
    """NPC entity (SpawnType 0x01 in saved files, or 0x02 MonsterNpc)."""
    object_id: int = 0
    npc_id: int = 0
    model_id: int = 0
    sprite_id: int = 0
    position_x: int = 0
    position_y: int = 0
    direction: int = 0
    stance: int = 0
    flags: int = 0


@dataclass
class PortalEntity(EntityBase):
    """Portal entity (SpawnType 0x04)."""
    object_id: int = 0
    position_x: int = 0
    position_y: int = 0
    target_map_id: int = 0
    target_portal_id: int = 0


@dataclass
class ReactorEntity(EntityBase):
    """Reactor entity (SpawnType 0x05)."""
    object_id: int = 0
    reactor_id: int = 0
    position_x: int = 0
    position_y: int = 0


@dataclass
class ItemEntity(EntityBase):
    """Dropped item entity (SpawnType 0x03)."""
    owner_id: int = 0
    position_x: int = 0
    position_y: int = 0
    dropped_amount: int = 0


# ============================================================================
# Binary Parsers
# ============================================================================

def parse_map_bin(data: bytes) -> MapHeader:
    """Parse map.bin file."""
    if len(data) < 4:
        raise ValueError(f"map.bin too short: {len(data)} bytes")
    map_id = struct.unpack('<H', data[0:2])[0]
    zone_id = struct.unpack('<H', data[2:4])[0]
    return MapHeader(map_id=map_id, zone_id=zone_id)


def parse_spawn_pos_txt(lines: List[str]) -> List[SpawnPosition]:
    """Parse SpawnPos.txt CSV file."""
    spawns = []
    for line in lines:
        line = line.strip()
        if not line or line.startswith('#'):
            continue
        parts = line.split(',')
        if len(parts) >= 5:
            try:
                spawns.append(SpawnPosition(
                    entity_type=int(parts[0]),
                    zone_id=int(parts[1]),
                    x=int(parts[2]),
                    y=int(parts[3]),
                    direction=int(parts[4])
                ))
            except ValueError:
                continue
    return spawns


def parse_entity_packet(data: bytes, filename: str) -> Optional[EntityBase]:
    """
    Parse an EntitySpawnPacket binary file.

    IMPORTANT: Saved files have a different format than network packets!
    - Byte[0]: Opcode (0x07)
    - Byte[1]: Flags/SubType (NOT ActionType enum!)
    - Byte[2]: EntityType (the key used in MapInfo.Entities dictionary)
    - Byte[3+]: Entity-specific payload (little-endian)

    Entity types in saved files:
    - 0x00: Monster (short format)
    - 0x01: NPC (extended format with position)
    - 0x04: Portal
    """
    if len(data) < 7:
        return None

    opcode = data[0]
    flags = data[1]  # This is NOT ActionType!
    entity_type = data[2]

    base_kwargs = {
        'filename': filename,
        'opcode': opcode,
        'action_type': flags,  # Store flags here for reference
        'spawn_type': entity_type,
        'raw_data': data,
    }

    # Parse based on entity type (byte[2])

    if entity_type == 0x00:
        # Monster: short format (7 bytes)
        # [3-6]: MonsterID (LE uint32)
        monster_id = struct.unpack('<I', data[3:7])[0]
        return MonsterEntity(
            **base_kwargs,
            object_id=monster_id,
            monster_id=monster_id
        )

    elif entity_type == 0x01:
        # NPC: extended format (72 bytes)
        # [3-6]: ObjectID/NpcID
        # [7-11]: Padding
        # [12-13]: ModelID
        # [14-15]: SpriteID
        # [16-17]: PositionX
        # [18-19]: PositionY
        # [20]: Direction
        # [21]: Flags
        if len(data) >= 22:
            object_id = struct.unpack('<I', data[3:7])[0]
            model_id = struct.unpack('<H', data[12:14])[0]
            sprite_id = struct.unpack('<H', data[14:16])[0]
            position_x = struct.unpack('<H', data[16:18])[0]
            position_y = struct.unpack('<H', data[18:20])[0]
            direction = data[20]
            npc_flags = data[21] if len(data) > 21 else 0

            return NpcEntity(
                **base_kwargs,
                object_id=object_id,
                npc_id=object_id,
                model_id=model_id,
                sprite_id=sprite_id,
                position_x=position_x,
                position_y=position_y,
                direction=direction,
                flags=npc_flags
            )

    elif entity_type == 0x02:
        # MonsterNpc (network packet format - uses BIG ENDIAN for most fields!)
        # But ObjectID seems to be stored in LE in saved files (matches filename)
        # ObjectID(4 LE) + NpcID(4) + Unknown_v3(4) + Unknown_v30(4) + PosX(2 BE) + PosY(2 BE) + Stance(1)
        if len(data) >= 24:
            # ObjectID is little-endian (matches filename convention)
            object_id = struct.unpack('<I', data[3:7])[0]
            npc_id = struct.unpack('<I', data[7:11])[0]
            unknown_v3 = struct.unpack('<I', data[11:15])[0]
            unknown_v30 = struct.unpack('<I', data[15:19])[0]
            # Coordinates are BIG-endian (matches EntitySpawnPacket.cs ReadInt16BE)
            position_x = struct.unpack('>H', data[19:21])[0]
            position_y = struct.unpack('>H', data[21:23])[0]
            stance = data[23] if len(data) > 23 else 0

            # Model ID: use unknown_v30 if it looks valid, otherwise use object_id
            # unknown_v30 of 4113047552 (0xf5282000) is probably flags, not model ID
            model_id = object_id  # Use object_id as model ID for type 2

            return NpcEntity(
                **base_kwargs,
                object_id=object_id,
                npc_id=npc_id if npc_id > 0 else object_id,
                model_id=model_id,
                position_x=position_x,
                position_y=position_y,
                stance=stance
            )

    elif entity_type == 0x04:
        # Portal: [3-6] ObjectID, [7-8] PosX, [9-10] PosY, [11-12] TargetMapID, [13-14] TargetPortalID
        if len(data) >= 15:
            object_id = struct.unpack('<I', data[3:7])[0]
            # Use unsigned for coordinates
            position_x = struct.unpack('<H', data[7:9])[0]
            position_y = struct.unpack('<H', data[9:11])[0]
            target_map_id = struct.unpack('<H', data[11:13])[0]
            target_portal_id = struct.unpack('<H', data[13:15])[0]

            return PortalEntity(
                **base_kwargs,
                object_id=object_id,
                position_x=position_x,
                position_y=position_y,
                target_map_id=target_map_id,
                target_portal_id=target_portal_id
            )

    elif entity_type == 0x05:
        # Reactor
        if len(data) >= 15:
            object_id = struct.unpack('<I', data[3:7])[0]
            reactor_id = struct.unpack('<I', data[7:11])[0]
            # Use unsigned for coordinates
            position_x = struct.unpack('<H', data[11:13])[0]
            position_y = struct.unpack('<H', data[13:15])[0]

            return ReactorEntity(
                **base_kwargs,
                object_id=object_id,
                reactor_id=reactor_id,
                position_x=position_x,
                position_y=position_y
            )

    # Short format entities (7 bytes): types 0, 8, 42, 48, etc.
    # These are all monster-like entities with just an ID
    if len(data) == 7:
        object_id = struct.unpack('<I', data[3:7])[0]
        return MonsterEntity(
            **base_kwargs,
            object_id=object_id,
            monster_id=object_id
        )

    # Fallback: try to extract basic info from longer packets
    if len(data) >= 7:
        object_id = struct.unpack('<I', data[3:7])[0]
        # Return as monster by default
        return MonsterEntity(
            **base_kwargs,
            object_id=object_id,
            monster_id=object_id
        )

    return None


# ============================================================================
# Script Generators
# ============================================================================

class ScriptGenerator:
    """Generates C# scripts from parsed entities."""

    def __init__(self, entity_names: Dict[int, str] = None):
        self.entity_names = entity_names or {}

    def get_name(self, entity_id: int, prefix: str = "Entity") -> str:
        if entity_id in self.entity_names:
            return self.entity_names[entity_id]
        return f"{prefix}_{entity_id}"

    def generate_spawn_script(self, map_header: MapHeader,
                              spawn_positions: List[SpawnPosition],
                              monsters: List[MonsterEntity]) -> str:
        """Generate SpawnScript for monsters."""

        # Deduplicate spawn positions
        unique_spawns = []
        seen = set()
        for sp in spawn_positions:
            key = (sp.x, sp.y, sp.direction)
            if key not in seen:
                seen.add(key)
                unique_spawns.append(sp)

        class_name = f"Map{map_header.map_id}_Zone{map_header.zone_id}_Spawns"

        lines = [
            "// Auto-generated from EntitySpawnPacket binary files",
            f"// Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
            "",
            "using Kakia.TW.World.Scripting;",
            "",
            "namespace Kakia.TW.World.Scripts.Spawns",
            "{",
            f"    public class {class_name} : SpawnScript",
            "    {",
            "        public override void Load()",
            "        {",
        ]

        for i, sp in enumerate(unique_spawns):
            monster = monsters[i % len(monsters)] if monsters else None
            model_id = monster.monster_id if monster else 1000 + i
            name = self.get_name(model_id, "Monster")

            lines.extend([
                f"            AddSpawner(",
                f"                modelId: {model_id},",
                f"                name: \"{name}\",",
                f"                mapId: {map_header.map_id},",
                f"                zoneId: {map_header.zone_id},",
                f"                x: {sp.x},",
                f"                y: {sp.y},",
                f"                direction: {sp.direction},",
                f"                respawnTime: 30,",
                f"                maxHp: 100",
                f"            );",
                ""
            ])

        lines.extend(["        }", "    }", "}"])
        return "\n".join(lines)

    def generate_npc_script(self, map_header: MapHeader,
                            npcs: List[NpcEntity]) -> Optional[str]:
        """Generate NpcScript for NPCs."""
        if not npcs:
            return None

        class_name = f"Map{map_header.map_id}_Zone{map_header.zone_id}_Npcs"

        lines = [
            "// Auto-generated from EntitySpawnPacket binary files",
            f"// Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
            "",
            "using Kakia.TW.World.Scripting;",
            "",
            "namespace Kakia.TW.World.Scripts.Npcs",
            "{",
            f"    public class {class_name} : NpcScript",
            "    {",
            "        public override void Load()",
            "        {",
        ]

        for npc in npcs:
            name = self.get_name(npc.npc_id, "NPC")

            lines.extend([
                f"            // ObjectID: {npc.object_id}, NpcID: {npc.npc_id}",
                f"            SpawnNpc(",
                f"                modelId: {npc.model_id},",
                f"                name: \"{name}\",",
                f"                mapId: {map_header.map_id},",
                f"                zoneId: {map_header.zone_id},",
                f"                x: {npc.position_x},",
                f"                y: {npc.position_y},",
                f"                direction: {npc.direction},",
                f"                dialogFunc: null  // TODO: Add dialog",
                f"            );",
                ""
            ])

        lines.extend(["        }", "    }", "}"])
        return "\n".join(lines)

    def generate_warp_script(self, map_header: MapHeader,
                             portals: List[PortalEntity],
                             portal_configs: Dict[int, dict] = None) -> Optional[str]:
        """Generate warp portal script."""
        if not portals:
            return None

        portal_configs = portal_configs or {}
        class_name = f"Map{map_header.map_id}_Zone{map_header.zone_id}_Warps"

        lines = [
            "// Auto-generated from EntitySpawnPacket binary files",
            f"// Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
            "",
            "using Kakia.TW.World.Scripting;",
            "",
            "namespace Kakia.TW.World.Scripts.Warps",
            "{",
            f"    public class {class_name} : NpcScript",
            "    {",
            "        public override void Load()",
            "        {",
        ]

        for portal in portals:
            # Check for JSON config override
            config = portal_configs.get(portal.object_id, {})
            dest_map = config.get('DestMapId', portal.target_map_id)
            dest_zone = config.get('DestZoneId', 1)
            dest_x = config.get('DestX', 100)
            dest_y = config.get('DestY', 100)

            lines.extend([
                f"            // Portal ObjectID: {portal.object_id}",
                f"            SpawnWarp(",
                f"                mapId: {map_header.map_id},",
                f"                zoneId: {map_header.zone_id},",
                f"                x: {portal.position_x},",
                f"                y: {portal.position_y},",
                f"                destMapId: {dest_map},",
                f"                destZoneId: {dest_zone},",
                f"                destX: {dest_x},",
                f"                destY: {dest_y}",
                f"            );",
                ""
            ])

        lines.extend(["        }", "    }", "}"])
        return "\n".join(lines)

    def generate_global_npc_script(self, npcs: List[NpcEntity]) -> str:
        """Generate a script for global NPCs (from NPCs/ folder)."""

        lines = [
            "// Auto-generated from EntitySpawnPacket binary files",
            f"// Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
            "// Global NPCs - assign mapId/zoneId based on where they should spawn",
            "",
            "using Kakia.TW.World.Scripting;",
            "",
            "namespace Kakia.TW.World.Scripts.Npcs",
            "{",
            "    public class GlobalNpcs : NpcScript",
            "    {",
            "        public override void Load()",
            "        {",
        ]

        for npc in npcs:
            name = self.get_name(npc.npc_id, "NPC")

            lines.extend([
                f"            // NPC ID: {npc.npc_id}, Model: {npc.model_id}",
                f"            SpawnNpc(",
                f"                modelId: {npc.model_id},",
                f"                name: \"{name}\",",
                f"                mapId: 0,     // TODO: Set correct map",
                f"                zoneId: 0,    // TODO: Set correct zone",
                f"                x: {npc.position_x},",
                f"                y: {npc.position_y},",
                f"                direction: {npc.direction},",
                f"                dialogFunc: null  // TODO: Add dialog",
                f"            );",
                ""
            ])

        lines.extend(["        }", "    }", "}"])
        return "\n".join(lines)


# ============================================================================
# Map Processor
# ============================================================================

def find_maps_and_npcs(base_dir: str = ".") -> tuple:
    """
    Find Maps and NPCs folders using the standard directory structure.

    Expected structure:
        Maps/MapId_{id}/ZoneId_{zoneId}/
            map.bin
            SpawnPos.txt
            Spawn/*.bin
        NPCs/
            {npcId}.bin

    Returns: (list of map directories, list of NPC files)
    """
    maps_dir = os.path.join(base_dir, "Maps")
    npcs_dir = os.path.join(base_dir, "NPCs")

    map_dirs = []
    npc_files = []

    # Find all map directories
    if os.path.exists(maps_dir):
        for map_folder in os.listdir(maps_dir):
            if not map_folder.startswith("MapId_"):
                continue
            map_path = os.path.join(maps_dir, map_folder)
            if not os.path.isdir(map_path):
                continue

            # Look for zone subdirectories
            for zone_folder in os.listdir(map_path):
                if not zone_folder.startswith("ZoneId_"):
                    continue
                zone_path = os.path.join(map_path, zone_folder)
                if os.path.isdir(zone_path) and os.path.exists(os.path.join(zone_path, "map.bin")):
                    map_dirs.append(zone_path)

    # Find all NPC files
    if os.path.exists(npcs_dir):
        for filename in os.listdir(npcs_dir):
            if filename.endswith('.bin'):
                npc_files.append(os.path.join(npcs_dir, filename))

    return map_dirs, npc_files


def process_all_maps(base_dir: str, output_dir: str, entity_names: Dict[int, str] = None):
    """
    Process all maps and NPCs using the standard directory structure.
    """
    map_dirs, npc_files = find_maps_and_npcs(base_dir)

    if not map_dirs and not npc_files:
        print("No Maps or NPCs folders found!")
        print()
        print("Expected structure:")
        print("  Maps/MapId_X/ZoneId_Y/map.bin")
        print("  Maps/MapId_X/ZoneId_Y/SpawnPos.txt")
        print("  Maps/MapId_X/ZoneId_Y/Spawn/*.bin")
        print("  NPCs/*.bin")
        return []

    print(f"Found {len(map_dirs)} map(s) and {len(npc_files)} global NPC file(s)")
    print()

    # Parse all global NPCs first
    global_npcs: List[NpcEntity] = []
    for npc_path in npc_files:
        with open(npc_path, 'rb') as f:
            data = f.read()
        entity = parse_entity_packet(data, os.path.basename(npc_path))
        if isinstance(entity, NpcEntity):
            global_npcs.append(entity)

    if global_npcs:
        print(f"Parsed {len(global_npcs)} global NPCs")

    generated = []
    generator = ScriptGenerator(entity_names)

    # Process each map
    for map_dir in sorted(map_dirs):
        rel_path = os.path.relpath(map_dir, base_dir)
        print(f"\nProcessing: {rel_path}")

        # Read map header
        map_bin_path = os.path.join(map_dir, "map.bin")
        with open(map_bin_path, 'rb') as f:
            map_header = parse_map_bin(f.read())

        print(f"  Map ID: {map_header.map_id}, Zone ID: {map_header.zone_id}")

        # Read spawn positions
        spawn_positions = []
        spawn_pos_path = os.path.join(map_dir, "SpawnPos.txt")
        if os.path.exists(spawn_pos_path):
            with open(spawn_pos_path, 'r') as f:
                spawn_positions = parse_spawn_pos_txt(f.readlines())
            print(f"  Spawn positions: {len(spawn_positions)}")

        # Read entity files from Spawn folder
        monsters: List[MonsterEntity] = []
        map_npcs: List[NpcEntity] = []
        portals: List[PortalEntity] = []

        spawn_dir = os.path.join(map_dir, "Spawn")
        if os.path.exists(spawn_dir):
            for filename in os.listdir(spawn_dir):
                if not filename.endswith('.bin'):
                    continue

                filepath = os.path.join(spawn_dir, filename)
                with open(filepath, 'rb') as f:
                    data = f.read()

                entity = parse_entity_packet(data, filename)
                if entity is None:
                    continue

                if isinstance(entity, MonsterEntity):
                    monsters.append(entity)
                elif isinstance(entity, NpcEntity):
                    map_npcs.append(entity)
                elif isinstance(entity, PortalEntity):
                    portals.append(entity)

        print(f"  Monsters: {len(monsters)}, Map NPCs: {len(map_npcs)}, Portals: {len(portals)}")

        # Load portal configs
        portal_configs = {}
        warp_dir = os.path.join(map_dir, "WarpPortals")
        if os.path.exists(warp_dir):
            for filename in os.listdir(warp_dir):
                if filename.endswith('.json'):
                    with open(os.path.join(warp_dir, filename), 'r') as f:
                        config = json.load(f)
                        if 'Id' in config:
                            portal_configs[config['Id']] = config

        # Create output directories
        os.makedirs(os.path.join(output_dir, "Spawns"), exist_ok=True)
        os.makedirs(os.path.join(output_dir, "Npcs"), exist_ok=True)
        os.makedirs(os.path.join(output_dir, "Warps"), exist_ok=True)

        base_name = f"Map{map_header.map_id}_Zone{map_header.zone_id}"

        # Generate spawn script
        if spawn_positions or monsters:
            script = generator.generate_spawn_script(map_header, spawn_positions, monsters)
            path = os.path.join(output_dir, "Spawns", f"{base_name}_Spawns.cs")
            with open(path, 'w') as f:
                f.write(script)
            generated.append(path)

        # Generate NPC script (combine map NPCs with global NPCs for this map)
        # For now, we'll create separate scripts; global NPCs go in their own file
        if map_npcs:
            script = generator.generate_npc_script(map_header, map_npcs)
            if script:
                path = os.path.join(output_dir, "Npcs", f"{base_name}_Npcs.cs")
                with open(path, 'w') as f:
                    f.write(script)
                generated.append(path)

        # Generate warp script
        if portals:
            script = generator.generate_warp_script(map_header, portals, portal_configs)
            if script:
                path = os.path.join(output_dir, "Warps", f"{base_name}_Warps.cs")
                with open(path, 'w') as f:
                    f.write(script)
                generated.append(path)

    # Generate global NPCs script
    if global_npcs:
        os.makedirs(os.path.join(output_dir, "Npcs"), exist_ok=True)
        script = generator.generate_global_npc_script(global_npcs)
        path = os.path.join(output_dir, "Npcs", "GlobalNpcs.cs")
        with open(path, 'w') as f:
            f.write(script)
        generated.append(path)
        print(f"\nGenerated global NPC script with {len(global_npcs)} NPCs")

    return generated


def main():
    print("=" * 60)
    print("EntitySpawnPacket to C# Script Converter")
    print("=" * 60)
    print()

    # Parse arguments
    base_dir = "."
    output_dir = "./Scripts"
    entity_names = {}

    args = sys.argv[1:]
    i = 0
    while i < len(args):
        if args[i] == "--names" and i + 1 < len(args):
            with open(args[i + 1], 'r') as f:
                raw = json.load(f)
                entity_names = {int(k): v for k, v in raw.items()}
            i += 2
        elif args[i] == "--output" and i + 1 < len(args):
            output_dir = args[i + 1]
            i += 2
        elif args[i] == "--help" or args[i] == "-h":
            print("Usage: python entity_converter.py [options] [base_dir]")
            print()
            print("Options:")
            print("  --output DIR     Output directory (default: ./Scripts)")
            print("  --names FILE     JSON file mapping entity IDs to names")
            print("  --help, -h       Show this help message")
            print()
            print("If no base_dir is specified, uses current directory.")
            print()
            print("Expected directory structure:")
            print("  Maps/MapId_X/ZoneId_Y/")
            print("      map.bin")
            print("      SpawnPos.txt")
            print("      Spawn/*.bin")
            print("  NPCs/")
            print("      {npcId}.bin")
            sys.exit(0)
        elif not args[i].startswith("-"):
            base_dir = args[i]
            i += 1
        else:
            i += 1

    print(f"Base directory: {os.path.abspath(base_dir)}")
    print(f"Output directory: {os.path.abspath(output_dir)}")
    if entity_names:
        print(f"Entity names: {len(entity_names)} mappings loaded")
    print()

    os.makedirs(output_dir, exist_ok=True)

    generated = process_all_maps(base_dir, output_dir, entity_names)

    print()
    print("=" * 60)
    print(f"Generated {len(generated)} files:")
    for path in generated:
        print(f"  {os.path.relpath(path, output_dir)}")


if __name__ == "__main__":
    main()