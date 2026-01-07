# TalesWeaver Binary Templates

Binary templates for 010 Editor / PaleTree packet analysis.

## Naming Convention

Templates follow the same naming convention as `src/Shared/Network/Op.cs`:

- **Client -> Server**: `PascalCase` + `Request` suffix (e.g., `LoginRequest.bt`)
- **Server -> Client**: `PascalCase` + `Response` suffix (e.g., `LoginResponse.bt`)
- **Acknowledgments**: `PascalCase` + `Ack` suffix (e.g., `AttackAck.bt`)
- **Bidirectional**: `PascalCase` without suffix (e.g., `Handshake.bt`)

## Directory Structure

```
docs/bt/
├── inc/                          # Include files
│   ├── common.bt                 # Main include (loads all others)
│   ├── config.bt                 # Configuration constants
│   ├── enums.bt                  # Op codes and enumerations
│   ├── headers.bt                # Packet header structure
│   ├── LpString.bt               # Length-prefixed string type
│   └── structs.bt                # Common data structures
│
├── Handshake.bt                  # 0x00 - Bidirectional handshake
├── LoginRequest.bt               # 0x66 - Client login request
├── LoginResponse.bt              # 0x50 - Server login response
├── ServerListResponse.bt         # 0x56 - Server list
├── ServerSelectRequest.bt        # 0x67 - Server/channel selection
├── ServerRedirect.bt             # 0x03 - Redirect to another server
├── ReconnectRequest.bt           # 0x10 - Reconnect after redirect
├── CharacterSelectListResponse.bt # 0x6B - Character list
├── CheckNameRequest.bt           # 0x28 - Check name availability
├── CreateCharacterRequest.bt     # 0x2C - Create character
├── CreateCharacterResponse.bt    # 0x7C - Character creation result
├── SelectCharacterRequest.bt     # 0x2B - Select character
├── LoginSecurityResponse.bt      # 0x3C - Security code prompt
├── ConnectedResponse.bt          # 0x7E - World connected
├── MapChangeResponse.bt          # 0x15 - Map change notification
├── WorldResponse.bt              # 0x07 - Entity spawn/despawn
├── SpawnUser.bt                  # 0x33:0x00 - Player spawn
├── MovementRequest.bt            # 0x33 - Movement request
├── UserPositionResponse.bt       # 0x0B - Position update broadcast
├── DirectionUpdateRequest.bt     # 0x11 - Direction change
├── ChatRequest.bt                # 0x0E - Chat message (C->S)
├── ChatResponse.bt               # 0x0D - Chat message (S->C)
├── EntityClickRequest.bt         # 0x43 - Click on entity
├── EntityClickAck.bt             # 0x70 - Entity click acknowledgment
├── FriendDialogResponse.bt       # 0x44 - NPC dialog display
├── NpcDialogAnswerRequest.bt     # 0x6C - Dialog response
├── SetPoseRequest.bt             # 0x32 - Sit/stand toggle
├── AttackRequest.bt              # 0x13 - Attack initiation
├── AttackAck.bt                  # 0x4A - Attack acknowledgment
├── AttackResultResponse.bt       # 0x48 - Attack result
└── CharEffectResponse.bt         # 0x5C - Character effects
```

## Usage

1. Open a packet dump in 010 Editor
2. Run the appropriate template based on the packet opcode
3. Templates will parse the packet structure and display field values

### With PaleTree

PaleTree can auto-detect packet types and apply templates. Configure the template directory in PaleTree settings.

## Common Structures

### TwString
Length-prefixed string: `[length:1][chars:length]`

### Position
2D coordinate: `[x:2][y:2]`

### TwHeader
Auto-detecting packet header that handles both framed (0xAA prefix) and unframed packets.

## Notes

- Some packets use Big Endian for specific fields (noted in templates)
- Spawn packets (0x33) use subOp byte to differentiate between spawn types
- WorldResponse (0x07) uses action + type bytes to determine entity type
