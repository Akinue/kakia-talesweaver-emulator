#!/usr/bin/env python3
"""
TalesWeaver Map Data Converter - Advanced Version

This tool converts binary map/spawn data into C# scripts compatible with the
Kakia.TW.World.Scripting system.

Features:
- Batch conversion of multiple maps
- Entity type detection and proper script generation
- Support for monster spawners, NPCs, and warp portals
- Configuration file support for entity name mappings
- Handles WarpPortals JSON configs

Usage:
    python batch_converter.py <maps_root_dir> <output_dir>
    python batch_converter.py <maps_root_dir> <output_dir> --config entities.json
"""

import os
import sys
import struct
import json
from pathlib import Path
from dataclasses import dataclass, field
from typing import List, Dict, Optional, Tuple
from datetime import datetime

# ============================================================================
# Data Classes
# ============================================================================

@dataclass
class MapHeader:
    map_id: int
    zone_id: int
    raw_data: bytes = field(default_factory=bytes)

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
    
    # Extended fields (if available)
    x: int = 0
    y: int = 0
    model_id: int = 0      # For NPCs
    sprite_id: int = 0     # For NPCs
    direction: int = 0     # For NPCs

@dataclass
class WarpPortalConfig:
    id: int
    src_x: int = 0
    src_y: int = 0
    width: int = 50
    height: int = 50
    dest_map_id: int = 0
    dest_zone_id: int = 0
    dest_x: int = 0
    dest_y: int = 0

# Entity type constants
ENTITY_TYPE_MONSTER = 0x00
ENTITY_TYPE_NPC = 0x01
ENTITY_TYPE_ITEM = 0x02
ENTITY_TYPE_WARP = 0x04
ENTITY_TYPE_EFFECT = 0x05

ENTITY_TYPE_NAMES = {
    ENTITY_TYPE_MONSTER: "Monster",
    ENTITY_TYPE_NPC: "NPC", 
    ENTITY_TYPE_ITEM: "Item",
    ENTITY_TYPE_WARP: "Warp",
    ENTITY_TYPE_EFFECT: "Effect",
}

# ============================================================================
# Binary Parsers
# ============================================================================

def parse_map_bin(data: bytes) -> MapHeader:
    """Parse map.bin file to extract map/zone IDs."""
    if len(data) < 4:
        raise ValueError(f"map.bin too short: {len(data)} bytes")
    
    map_id = struct.unpack('<H', data[0:2])[0]
    zone_id = struct.unpack('<H', data[2:4])[0]
    
    return MapHeader(map_id=map_id, zone_id=zone_id, raw_data=data)

def parse_spawn_pos_txt(lines: List[str]) -> List[SpawnPosition]:
    """Parse SpawnPos.txt file."""
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

def parse_entity_bin(data: bytes, filename: str) -> EntitySpawn:
    """Parse a Spawn/*.bin entity file."""
    if len(data) < 7:
        raise ValueError(f"Entity file too short: {len(data)} bytes")
    
    packet_type = data[0]
    flag = data[1]
    entity_type = data[2]
    entity_id = struct.unpack('<I', data[3:7])[0]
    
    entity = EntitySpawn(
        filename=filename,
        packet_type=packet_type,
        flag=flag,
        entity_type=entity_type,
        entity_id=entity_id,
        raw_data=data
    )
    
    # Parse based on entity type
    if entity_type == ENTITY_TYPE_NPC and len(data) >= 22:
        # NPC extended format (72 bytes):
        # [12-13] model_id, [14-15] sprite_id, [16-17] x, [18-19] y, [20] direction
        entity.model_id = struct.unpack('<H', data[12:14])[0]
        entity.sprite_id = struct.unpack('<H', data[14:16])[0]
        entity.x = struct.unpack('<H', data[16:18])[0]
        entity.y = struct.unpack('<H', data[18:20])[0]
        entity.direction = data[20]
    elif entity_type == ENTITY_TYPE_WARP and len(data) >= 11:
        # Warp portal - try to extract position
        entity.x = struct.unpack('<H', data[7:9])[0] if len(data) >= 9 else 0
        entity.y = struct.unpack('<H', data[9:11])[0] if len(data) >= 11 else 0
    elif len(data) >= 11:
        # Generic fallback - try common position offsets
        try:
            entity.x = struct.unpack('<H', data[7:9])[0]
            entity.y = struct.unpack('<H', data[9:11])[0]
        except:
            pass
    
    return entity

def parse_warp_portal_json(data: dict) -> WarpPortalConfig:
    """Parse WarpPortal JSON config."""
    return WarpPortalConfig(
        id=data.get('Id', 0),
        src_x=data.get('X', 0),
        src_y=data.get('Y', 0),
        width=data.get('Width', 50),
        height=data.get('Height', 50),
        dest_map_id=data.get('DestMapId', 0),
        dest_zone_id=data.get('DestZoneId', 1),
        dest_x=data.get('DestX', 0),
        dest_y=data.get('DestY', 0)
    )

# ============================================================================
# Script Generators
# ============================================================================

class ScriptGenerator:
    """Generates C# scripts from parsed map data."""
    
    def __init__(self, entity_names: Dict[int, str] = None):
        self.entity_names = entity_names or {}
        self.generated_files = []
    
    def get_entity_name(self, entity_id: int, default_prefix: str = "Entity") -> str:
        """Look up entity name or generate default."""
        if entity_id in self.entity_names:
            return self.entity_names[entity_id]
        return f"{default_prefix}_{entity_id}"
    
    def generate_spawn_script(self, map_header: MapHeader, 
                              spawn_positions: List[SpawnPosition],
                              entities: Dict[int, List[EntitySpawn]]) -> str:
        """Generate SpawnScript for monsters."""
        
        # Deduplicate spawn positions
        unique_spawns = []
        seen = set()
        for sp in spawn_positions:
            key = (sp.x, sp.y, sp.direction)
            if key not in seen:
                seen.add(key)
                unique_spawns.append(sp)
        
        # Get monster entities
        monsters = entities.get(ENTITY_TYPE_MONSTER, [])
        
        class_name = f"Map{map_header.map_id}_Zone{map_header.zone_id}_Spawns"
        
        lines = [
            "// Auto-generated by TalesWeaver Map Converter",
            f"// Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
            "",
            "using Kakia.TW.World.Scripting;",
            "",
            "namespace Kakia.TW.World.Scripts.Spawns",
            "{",
            "    /// <summary>",
            f"    /// Monster spawns for Map {map_header.map_id}, Zone {map_header.zone_id}",
            "    /// </summary>",
            f"    public class {class_name} : SpawnScript",
            "    {",
            "        public override void Load()",
            "        {",
        ]
        
        if not unique_spawns:
            lines.append("            // No spawn positions defined")
        else:
            for i, sp in enumerate(unique_spawns):
                # Assign monster entity if available
                if monsters:
                    monster = monsters[i % len(monsters)]
                    model_id = monster.entity_id
                    name = self.get_entity_name(model_id, "Monster")
                else:
                    model_id = 1000 + i
                    name = f"Monster_{i + 1}"
                
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
        
        lines.extend([
            "        }",
            "    }",
            "}"
        ])
        
        return "\n".join(lines)
    
    def generate_npc_script(self, map_header: MapHeader,
                            entities: Dict[int, List[EntitySpawn]]) -> Optional[str]:
        """Generate NpcScript for NPCs."""
        
        npc_entities = entities.get(ENTITY_TYPE_NPC, [])
        if not npc_entities:
            return None
        
        class_name = f"Map{map_header.map_id}_Zone{map_header.zone_id}_Npcs"
        
        lines = [
            "// Auto-generated by TalesWeaver Map Converter",
            f"// Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
            "",
            "using Kakia.TW.World.Scripting;",
            "",
            "namespace Kakia.TW.World.Scripts.Npcs",
            "{",
            "    /// <summary>",
            f"    /// NPCs for Map {map_header.map_id}, Zone {map_header.zone_id}",
            "    /// </summary>",
            f"    public class {class_name} : NpcScript",
            "    {",
            "        public override void Load()",
            "        {",
        ]
        
        for entity in npc_entities:
            name = self.get_entity_name(entity.entity_id, "NPC")
            # Use model_id from extended NPC data if available, otherwise fall back to entity_id
            model_id = entity.model_id if entity.model_id > 0 else entity.entity_id
            x = entity.x if entity.x > 0 else 100
            y = entity.y if entity.y > 0 else 100
            direction = entity.direction
            
            lines.extend([
                f"            // NPC ID: {entity.entity_id}",
                f"            SpawnNpc(",
                f"                modelId: {model_id},",
                f"                name: \"{name}\",",
                f"                mapId: {map_header.map_id},",
                f"                zoneId: {map_header.zone_id},",
                f"                x: {x},",
                f"                y: {y},",
                f"                direction: {direction},",
                f"                dialogFunc: null  // TODO: Add dialog handler",
                f"            );",
                ""
            ])
        
        lines.extend([
            "        }",
            "    }",
            "}"
        ])
        
        return "\n".join(lines)
    
    def generate_warp_script(self, map_header: MapHeader,
                             entities: Dict[int, List[EntitySpawn]],
                             warp_configs: Dict[int, WarpPortalConfig]) -> Optional[str]:
        """Generate warp portal script."""
        
        warp_entities = entities.get(ENTITY_TYPE_WARP, [])
        if not warp_entities and not warp_configs:
            return None
        
        class_name = f"Map{map_header.map_id}_Zone{map_header.zone_id}_Warps"
        
        lines = [
            "// Auto-generated by TalesWeaver Map Converter",
            f"// Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
            "",
            "using Kakia.TW.World.Scripting;",
            "",
            "namespace Kakia.TW.World.Scripts.Warps",
            "{",
            "    /// <summary>",
            f"    /// Warp portals for Map {map_header.map_id}, Zone {map_header.zone_id}",
            "    /// </summary>",
            f"    public class {class_name} : NpcScript",
            "    {",
            "        public override void Load()",
            "        {",
        ]
        
        # Process warp entities with config overrides
        processed_ids = set()
        
        for entity in warp_entities:
            config = warp_configs.get(entity.entity_id)
            processed_ids.add(entity.entity_id)
            
            if config:
                lines.extend([
                    f"            // Warp {entity.entity_id} (configured)",
                    f"            SpawnWarp(",
                    f"                mapId: {map_header.map_id},",
                    f"                zoneId: {map_header.zone_id},",
                    f"                x: {config.src_x},",
                    f"                y: {config.src_y},",
                    f"                destMapId: {config.dest_map_id},",
                    f"                destZoneId: {config.dest_zone_id},",
                    f"                destX: {config.dest_x},",
                    f"                destY: {config.dest_y}",
                    f"            );",
                    ""
                ])
            else:
                lines.extend([
                    f"            // Warp {entity.entity_id} (needs configuration)",
                    f"            SpawnWarp(",
                    f"                mapId: {map_header.map_id},",
                    f"                zoneId: {map_header.zone_id},",
                    f"                x: {entity.x if entity.x > 0 else 100},",
                    f"                y: {entity.y if entity.y > 0 else 100},",
                    f"                destMapId: 1,    // TODO: Configure destination",
                    f"                destZoneId: 1,",
                    f"                destX: 100,",
                    f"                destY: 100",
                    f"            );",
                    ""
                ])
        
        # Add any configs that weren't matched to entities
        for warp_id, config in warp_configs.items():
            if warp_id not in processed_ids:
                lines.extend([
                    f"            // Warp {warp_id} (from config only)",
                    f"            SpawnWarp(",
                    f"                mapId: {map_header.map_id},",
                    f"                zoneId: {map_header.zone_id},",
                    f"                x: {config.src_x},",
                    f"                y: {config.src_y},",
                    f"                destMapId: {config.dest_map_id},",
                    f"                destZoneId: {config.dest_zone_id},",
                    f"                destX: {config.dest_x},",
                    f"                destY: {config.dest_y}",
                    f"            );",
                    ""
                ])
        
        lines.extend([
            "        }",
            "    }",
            "}"
        ])
        
        return "\n".join(lines)

# ============================================================================
# Map Directory Processor
# ============================================================================

def process_map_directory(map_dir: str, output_dir: str, 
                          entity_names: Dict[int, str] = None) -> List[str]:
    """Process a single map directory and generate scripts."""
    
    generated = []
    generator = ScriptGenerator(entity_names)
    
    map_bin_path = os.path.join(map_dir, "map.bin")
    spawn_pos_path = os.path.join(map_dir, "SpawnPos.txt")
    spawn_dir = os.path.join(map_dir, "Spawn")
    warp_dir = os.path.join(map_dir, "WarpPortals")
    
    # Read map header
    if not os.path.exists(map_bin_path):
        print(f"  Warning: No map.bin found")
        return generated
    
    with open(map_bin_path, 'rb') as f:
        map_header = parse_map_bin(f.read())
    
    print(f"  Map ID: {map_header.map_id}, Zone ID: {map_header.zone_id}")
    
    # Read spawn positions
    spawn_positions = []
    if os.path.exists(spawn_pos_path):
        with open(spawn_pos_path, 'r') as f:
            spawn_positions = parse_spawn_pos_txt(f.readlines())
        print(f"  Spawn positions: {len(spawn_positions)}")
    
    # Read entity spawns
    entities: Dict[int, List[EntitySpawn]] = {}
    if os.path.exists(spawn_dir):
        for filename in os.listdir(spawn_dir):
            if filename.endswith('.bin'):
                filepath = os.path.join(spawn_dir, filename)
                try:
                    with open(filepath, 'rb') as f:
                        entity = parse_entity_bin(f.read(), filename)
                    if entity.entity_type not in entities:
                        entities[entity.entity_type] = []
                    entities[entity.entity_type].append(entity)
                except Exception as e:
                    print(f"  Warning: Failed to parse {filename}: {e}")
    
    entity_summary = {ENTITY_TYPE_NAMES.get(k, f"Type{k}"): len(v) for k, v in entities.items()}
    print(f"  Entities: {entity_summary}")
    
    # Read warp portal configs
    warp_configs: Dict[int, WarpPortalConfig] = {}
    if os.path.exists(warp_dir):
        for filename in os.listdir(warp_dir):
            if filename.endswith('.json'):
                filepath = os.path.join(warp_dir, filename)
                try:
                    with open(filepath, 'r') as f:
                        config = parse_warp_portal_json(json.load(f))
                    warp_configs[config.id] = config
                except Exception as e:
                    print(f"  Warning: Failed to parse {filename}: {e}")
    
    if warp_configs:
        print(f"  Warp configs: {len(warp_configs)}")
    
    # Create output subdirectories
    spawns_dir = os.path.join(output_dir, "Spawns")
    npcs_dir = os.path.join(output_dir, "Npcs")
    warps_dir = os.path.join(output_dir, "Warps")
    
    os.makedirs(spawns_dir, exist_ok=True)
    os.makedirs(npcs_dir, exist_ok=True)
    os.makedirs(warps_dir, exist_ok=True)
    
    base_name = f"Map{map_header.map_id}_Zone{map_header.zone_id}"
    
    # Generate spawn script
    if spawn_positions or ENTITY_TYPE_MONSTER in entities:
        script = generator.generate_spawn_script(map_header, spawn_positions, entities)
        path = os.path.join(spawns_dir, f"{base_name}_Spawns.cs")
        with open(path, 'w') as f:
            f.write(script)
        generated.append(path)
    
    # Generate NPC script
    npc_script = generator.generate_npc_script(map_header, entities)
    if npc_script:
        path = os.path.join(npcs_dir, f"{base_name}_Npcs.cs")
        with open(path, 'w') as f:
            f.write(npc_script)
        generated.append(path)
    
    # Generate warp script
    warp_script = generator.generate_warp_script(map_header, entities, warp_configs)
    if warp_script:
        path = os.path.join(warps_dir, f"{base_name}_Warps.cs")
        with open(path, 'w') as f:
            f.write(warp_script)
        generated.append(path)
    
    return generated

def batch_convert(maps_root: str, output_dir: str, entity_config: str = None):
    """Batch convert all map directories."""
    
    # Load entity name mappings if provided
    entity_names = {}
    if entity_config and os.path.exists(entity_config):
        with open(entity_config, 'r') as f:
            raw_names = json.load(f)
            # Convert string keys to int keys
            entity_names = {int(k): v for k, v in raw_names.items()}
        print(f"Loaded {len(entity_names)} entity name mappings")
    
    all_generated = []
    
    # Check if this is a single map directory or root containing multiple maps
    if os.path.exists(os.path.join(maps_root, "map.bin")):
        # Single map directory
        print(f"Processing: {maps_root}")
        generated = process_map_directory(maps_root, output_dir, entity_names)
        all_generated.extend(generated)
    else:
        # Root directory with multiple maps
        for item in sorted(os.listdir(maps_root)):
            map_dir = os.path.join(maps_root, item)
            if os.path.isdir(map_dir) and os.path.exists(os.path.join(map_dir, "map.bin")):
                print(f"\nProcessing: {item}")
                generated = process_map_directory(map_dir, output_dir, entity_names)
                all_generated.extend(generated)
    
    return all_generated

# ============================================================================
# Main
# ============================================================================

def main():
    if len(sys.argv) < 3:
        print("TalesWeaver Map Data Converter")
        print()
        print("Usage:")
        print("  python batch_converter.py <maps_dir> <output_dir> [--config entities.json]")
        print()
        print("Arguments:")
        print("  maps_dir    - Directory containing map data (or root with multiple map dirs)")
        print("  output_dir  - Output directory for generated C# scripts")
        print("  --config    - Optional JSON file mapping entity IDs to names")
        print()
        print("Expected map directory structure:")
        print("  map_dir/")
        print("  ├── map.bin           # Map header (required)")
        print("  ├── SpawnPos.txt      # Spawn positions (optional)")
        print("  ├── Spawn/            # Entity spawn packets (optional)")
        print("  │   ├── 0_12345.bin")
        print("  │   └── ...")
        print("  └── WarpPortals/      # Warp portal configs (optional)")
        print("      ├── portal1.json")
        print("      └── ...")
        sys.exit(1)
    
    maps_dir = sys.argv[1]
    output_dir = sys.argv[2]
    
    entity_config = None
    if "--config" in sys.argv:
        idx = sys.argv.index("--config")
        if idx + 1 < len(sys.argv):
            entity_config = sys.argv[idx + 1]
    
    if not os.path.exists(maps_dir):
        print(f"Error: Maps directory not found: {maps_dir}")
        sys.exit(1)
    
    os.makedirs(output_dir, exist_ok=True)
    
    print("=" * 60)
    print("TalesWeaver Map Data Converter")
    print("=" * 60)
    print(f"Input:  {maps_dir}")
    print(f"Output: {output_dir}")
    print()
    
    generated = batch_convert(maps_dir, output_dir, entity_config)
    
    print()
    print("=" * 60)
    print(f"Generated {len(generated)} script files:")
    for path in generated:
        print(f"  {os.path.relpath(path, output_dir)}")

if __name__ == "__main__":
    main()
