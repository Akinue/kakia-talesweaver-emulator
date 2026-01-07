#!/usr/bin/env python3
"""
TalesWeaver Map/Spawn Binary to C# Script Converter

Converts binary map data and spawn files into C# scripts compatible with
the Kakia.TW.World.Scripting system.

Binary formats:
- map.bin: Contains MapId (ushort) and ZoneId (ushort) at bytes 0-3
- SpawnPos.txt: CSV format - type,zoneId,x,y,direction
- Spawn/*.bin: Entity packets - byte[0]=opcode, byte[1]=flag, byte[2]=entityType, bytes[3-6]=entityId(uint32 LE)

Entity types:
- 0x00: Monster/Mob
- 0x04: Warp Portal
- Other: NPCs, items, etc.
"""

import os
import struct
from pathlib import Path
from dataclasses import dataclass
from typing import List, Dict, Optional
import json

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
class EntitySpawn:
    filename: str
    packet_type: int
    flag: int
    entity_type: int
    entity_id: int
    raw_data: bytes

@dataclass
class WarpPortal:
    entity_id: int
    src_x: int = 0
    src_y: int = 0
    dest_map_id: int = 0
    dest_zone_id: int = 0
    dest_x: int = 0
    dest_y: int = 0

def read_map_bin(path: str) -> MapHeader:
    """Read map.bin to extract MapId and ZoneId."""
    with open(path, 'rb') as f:
        data = f.read()
    
    if len(data) < 4:
        raise ValueError(f"map.bin too short: {len(data)} bytes")
    
    map_id = struct.unpack('<H', data[0:2])[0]
    zone_id = struct.unpack('<H', data[2:4])[0]
    
    return MapHeader(map_id=map_id, zone_id=zone_id)

def read_spawn_pos(path: str) -> List[SpawnPosition]:
    """Read SpawnPos.txt CSV file."""
    spawns = []
    with open(path, 'r') as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            parts = line.split(',')
            if len(parts) >= 5:
                spawns.append(SpawnPosition(
                    entity_type=int(parts[0]),
                    zone_id=int(parts[1]),
                    x=int(parts[2]),
                    y=int(parts[3]),
                    direction=int(parts[4])
                ))
    return spawns

def read_entity_spawn(path: str) -> EntitySpawn:
    """Read a spawn/*.bin entity file."""
    filename = os.path.basename(path)
    with open(path, 'rb') as f:
        data = f.read()
    
    if len(data) < 7:
        raise ValueError(f"Entity file too short: {path}")
    
    packet_type = data[0]
    flag = data[1]
    entity_type = data[2]
    entity_id = struct.unpack('<I', data[3:7])[0]
    
    return EntitySpawn(
        filename=filename,
        packet_type=packet_type,
        flag=flag,
        entity_type=entity_type,
        entity_id=entity_id,
        raw_data=data
    )

def parse_warp_portal(data: bytes) -> Optional[WarpPortal]:
    """Parse a warp portal entity packet."""
    if len(data) < 7:
        return None
    
    entity_id = struct.unpack('<I', data[3:7])[0]
    
    # Extended data for warp portal (positions, destinations)
    # Format varies by implementation - this handles common structures
    portal = WarpPortal(entity_id=entity_id)
    
    if len(data) >= 15:
        # Try to extract position and destination data
        try:
            portal.src_x = struct.unpack('<H', data[7:9])[0] if len(data) >= 9 else 0
            portal.src_y = struct.unpack('<H', data[9:11])[0] if len(data) >= 11 else 0
            portal.dest_map_id = struct.unpack('<H', data[11:13])[0] if len(data) >= 13 else 0
            portal.dest_x = struct.unpack('<H', data[13:15])[0] if len(data) >= 15 else 0
            portal.dest_y = struct.unpack('<H', data[15:17])[0] if len(data) >= 17 else 0
        except:
            pass
    
    return portal

def generate_spawn_script(map_header: MapHeader, spawn_positions: List[SpawnPosition], 
                          entities: Dict[int, List[EntitySpawn]], output_name: str) -> str:
    """Generate a C# SpawnScript from the parsed data."""
    
    # Deduplicate spawn positions
    unique_spawns = []
    seen = set()
    for sp in spawn_positions:
        key = (sp.x, sp.y, sp.direction)
        if key not in seen:
            seen.add(key)
            unique_spawns.append(sp)
    
    # Get monster entities (type 0x00)
    monsters = entities.get(0x00, [])
    
    lines = [
        "using Kakia.TW.World.Scripting;",
        "",
        "namespace Kakia.TW.World.Scripts.Spawns",
        "{",
        f"    /// <summary>",
        f"    /// Auto-generated spawn script for Map {map_header.map_id}, Zone {map_header.zone_id}",
        f"    /// </summary>",
        f"    public class {output_name} : SpawnScript",
        "    {",
        "        public override void Load()",
        "        {",
        f"            const ushort MapId = {map_header.map_id};",
        f"            const ushort ZoneId = {map_header.zone_id};",
        "",
    ]
    
    # Generate spawner calls for each unique position
    for i, sp in enumerate(unique_spawns):
        # If we have monster entity data, use the entity ID as model ID
        model_id = monsters[i % len(monsters)].entity_id if monsters else 1000 + i
        
        lines.append(f"            // Spawn point {i + 1}")
        lines.append(f"            AddSpawner(")
        lines.append(f"                modelId: {model_id},")
        lines.append(f"                name: \"Monster_{i + 1}\",")
        lines.append(f"                mapId: MapId,")
        lines.append(f"                zoneId: ZoneId,")
        lines.append(f"                x: {sp.x},")
        lines.append(f"                y: {sp.y},")
        lines.append(f"                direction: {sp.direction},")
        lines.append(f"                respawnTime: 30,")
        lines.append(f"                maxHp: 100")
        lines.append(f"            );")
        lines.append("")
    
    lines.append("        }")
    lines.append("    }")
    lines.append("}")
    
    return "\n".join(lines)

def generate_npc_script(map_header: MapHeader, entities: Dict[int, List[EntitySpawn]], 
                        output_name: str) -> str:
    """Generate a C# NpcScript from entity data."""
    
    # Get NPC entities (non-monster, non-warp types)
    npc_entities = []
    for entity_type, entity_list in entities.items():
        if entity_type not in [0x00, 0x04]:  # Not monster or warp
            npc_entities.extend(entity_list)
    
    lines = [
        "using Kakia.TW.World.Scripting;",
        "",
        "namespace Kakia.TW.World.Scripts.Npcs",
        "{",
        f"    /// <summary>",
        f"    /// Auto-generated NPC script for Map {map_header.map_id}, Zone {map_header.zone_id}",
        f"    /// </summary>",
        f"    public class {output_name} : NpcScript",
        "    {",
        "        public override void Load()",
        "        {",
        f"            const ushort MapId = {map_header.map_id};",
        f"            const ushort ZoneId = {map_header.zone_id};",
        "",
    ]
    
    for i, entity in enumerate(npc_entities):
        lines.append(f"            // NPC from entity {entity.entity_id}")
        lines.append(f"            SpawnNpc(")
        lines.append(f"                modelId: {entity.entity_id},")
        lines.append(f"                name: \"NPC_{entity.entity_id}\",")
        lines.append(f"                mapId: MapId,")
        lines.append(f"                zoneId: ZoneId,")
        lines.append(f"                x: 100,  // TODO: Set actual position")
        lines.append(f"                y: 100,  // TODO: Set actual position")
        lines.append(f"                direction: 0")
        lines.append(f"            );")
        lines.append("")
    
    lines.append("        }")
    lines.append("    }")
    lines.append("}")
    
    return "\n".join(lines)

def generate_warp_script(map_header: MapHeader, entities: Dict[int, List[EntitySpawn]],
                         warp_json_dir: Optional[str], output_name: str) -> str:
    """Generate a C# warp script from warp portal entities."""
    
    # Get warp portal entities (type 0x04)
    warp_entities = entities.get(0x04, [])
    
    # Load warp portal JSON configs if available
    warp_configs = {}
    if warp_json_dir and os.path.exists(warp_json_dir):
        for f in os.listdir(warp_json_dir):
            if f.endswith('.json'):
                try:
                    with open(os.path.join(warp_json_dir, f), 'r') as jf:
                        config = json.load(jf)
                        if 'Id' in config:
                            warp_configs[config['Id']] = config
                except:
                    pass
    
    lines = [
        "using Kakia.TW.World.Scripting;",
        "",
        "namespace Kakia.TW.World.Scripts.Warps",
        "{",
        f"    /// <summary>",
        f"    /// Auto-generated warp script for Map {map_header.map_id}, Zone {map_header.zone_id}",
        f"    /// </summary>",
        f"    public class {output_name} : NpcScript",
        "    {",
        "        public override void Load()",
        "        {",
        f"            const ushort MapId = {map_header.map_id};",
        f"            const ushort ZoneId = {map_header.zone_id};",
        "",
    ]
    
    for entity in warp_entities:
        portal = parse_warp_portal(entity.raw_data)
        
        # Check for JSON config override
        config = warp_configs.get(entity.entity_id, {})
        dest_map = config.get('DestMapId', portal.dest_map_id if portal else 1)
        dest_zone = config.get('DestZoneId', portal.dest_zone_id if portal else 1)
        dest_x = config.get('DestX', portal.dest_x if portal else 100)
        dest_y = config.get('DestY', portal.dest_y if portal else 100)
        src_x = config.get('X', portal.src_x if portal else 100)
        src_y = config.get('Y', portal.src_y if portal else 100)
        
        lines.append(f"            // Warp portal {entity.entity_id}")
        lines.append(f"            SpawnWarp(")
        lines.append(f"                mapId: MapId,")
        lines.append(f"                zoneId: ZoneId,")
        lines.append(f"                x: {src_x},")
        lines.append(f"                y: {src_y},")
        lines.append(f"                destMapId: {dest_map},")
        lines.append(f"                destZoneId: {dest_zone},")
        lines.append(f"                destX: {dest_x},")
        lines.append(f"                destY: {dest_y}")
        lines.append(f"            );")
        lines.append("")
    
    lines.append("        }")
    lines.append("    }")
    lines.append("}")
    
    return "\n".join(lines)

def convert_map_directory(input_dir: str, output_dir: str):
    """Convert all data in a map directory to C# scripts."""
    
    map_bin_path = os.path.join(input_dir, "map.bin")
    spawn_pos_path = os.path.join(input_dir, "SpawnPos.txt")
    spawn_dir = os.path.join(input_dir, "Spawn")
    warp_json_dir = os.path.join(input_dir, "WarpPortals")
    
    # Read map header
    if not os.path.exists(map_bin_path):
        print(f"Error: map.bin not found in {input_dir}")
        return
    
    map_header = read_map_bin(map_bin_path)
    print(f"Map: {map_header.map_id}, Zone: {map_header.zone_id}")
    
    # Read spawn positions
    spawn_positions = []
    if os.path.exists(spawn_pos_path):
        spawn_positions = read_spawn_pos(spawn_pos_path)
        print(f"Loaded {len(spawn_positions)} spawn positions")
    
    # Read entity spawn files
    entities: Dict[int, List[EntitySpawn]] = {}
    if os.path.exists(spawn_dir):
        for filename in os.listdir(spawn_dir):
            if filename.endswith('.bin'):
                filepath = os.path.join(spawn_dir, filename)
                try:
                    entity = read_entity_spawn(filepath)
                    if entity.entity_type not in entities:
                        entities[entity.entity_type] = []
                    entities[entity.entity_type].append(entity)
                except Exception as e:
                    print(f"Warning: Failed to parse {filename}: {e}")
    
    print(f"Loaded entities by type: {dict((k, len(v)) for k, v in entities.items())}")
    
    # Create output directory
    os.makedirs(output_dir, exist_ok=True)
    
    # Generate script name from map/zone
    base_name = f"Map{map_header.map_id}_Zone{map_header.zone_id}"
    
    # Generate spawn script
    if spawn_positions:
        spawn_script = generate_spawn_script(map_header, spawn_positions, entities, f"{base_name}_Spawns")
        spawn_path = os.path.join(output_dir, f"{base_name}_Spawns.cs")
        with open(spawn_path, 'w') as f:
            f.write(spawn_script)
        print(f"Generated: {spawn_path}")
    
    # Generate warp script
    if 0x04 in entities:
        warp_script = generate_warp_script(map_header, entities, warp_json_dir, f"{base_name}_Warps")
        warp_path = os.path.join(output_dir, f"{base_name}_Warps.cs")
        with open(warp_path, 'w') as f:
            f.write(warp_script)
        print(f"Generated: {warp_path}")
    
    # Generate NPC script
    npc_types = [t for t in entities.keys() if t not in [0x00, 0x04]]
    if npc_types:
        npc_script = generate_npc_script(map_header, entities, f"{base_name}_Npcs")
        npc_path = os.path.join(output_dir, f"{base_name}_Npcs.cs")
        with open(npc_path, 'w') as f:
            f.write(npc_script)
        print(f"Generated: {npc_path}")

def main():
    import sys
    
    if len(sys.argv) < 2:
        print("Usage: python map_converter.py <input_dir> [output_dir]")
        print("       python map_converter.py --single <map.bin> <SpawnPos.txt> [spawn.bin...] [output_dir]")
        sys.exit(1)
    
    if sys.argv[1] == "--single":
        # Single file mode for the uploaded files
        if len(sys.argv) < 4:
            print("Usage: python map_converter.py --single <map.bin> <SpawnPos.txt> [spawn.bin...] [output_dir]")
            sys.exit(1)
        
        map_bin = sys.argv[2]
        spawn_pos = sys.argv[3]
        
        # Find spawn bins and output dir
        spawn_bins = []
        output_dir = "./output"
        
        for arg in sys.argv[4:]:
            if arg.endswith('.bin'):
                spawn_bins.append(arg)
            else:
                output_dir = arg
        
        # Read data
        map_header = read_map_bin(map_bin)
        spawn_positions = read_spawn_pos(spawn_pos) if os.path.exists(spawn_pos) else []
        
        entities: Dict[int, List[EntitySpawn]] = {}
        for spawn_bin in spawn_bins:
            entity = read_entity_spawn(spawn_bin)
            if entity.entity_type not in entities:
                entities[entity.entity_type] = []
            entities[entity.entity_type].append(entity)
        
        os.makedirs(output_dir, exist_ok=True)
        base_name = f"Map{map_header.map_id}_Zone{map_header.zone_id}"
        
        # Generate scripts
        if spawn_positions:
            spawn_script = generate_spawn_script(map_header, spawn_positions, entities, f"{base_name}_Spawns")
            spawn_path = os.path.join(output_dir, f"{base_name}_Spawns.cs")
            with open(spawn_path, 'w') as f:
                f.write(spawn_script)
            print(f"Generated: {spawn_path}")
        
        print(f"\nMap ID: {map_header.map_id}")
        print(f"Zone ID: {map_header.zone_id}")
        print(f"Spawn positions: {len(spawn_positions)}")
        print(f"Entity types: {list(entities.keys())}")
        
    else:
        # Directory mode
        input_dir = sys.argv[1]
        output_dir = sys.argv[2] if len(sys.argv) > 2 else "./output"
        convert_map_directory(input_dir, output_dir)

if __name__ == "__main__":
    main()
