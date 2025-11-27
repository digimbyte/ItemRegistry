# Item Registry - Unity Package

A robust and type-safe asset management system for Unity, built with Odin Inspector for enhanced editor experience.

## Installation

### Via Unity Package Manager (Git URL)

1. Open Unity Package Manager (Window → Package Manager)
2. Click the `+` button → "Add package from git URL..."
3. Enter: `https://github.com/digimbyte/ItemRegistry.git`

### Requirements

- Unity 2021.3 or later
- Odin Inspector 4.0.0 or later (Asset Store)

## Features

### Registry (ScriptableObject)
- **Type-Locked Design**: Each registry can ONLY store one asset type (Prefab, Texture, Material, Mesh, or Audio)
- **UID-based Lookup**: Fast dictionary-based asset retrieval by string identifier
- **Type-safe Getters**: Dedicated methods for each asset type with automatic casting
- **Filtering & Querying**: Filter by tags
- **Validation**: Built-in validation tools to check for duplicates, invalid assets, and type mismatches

### RegistryManager (Singleton)
- **Named Buckets**: Organize registries into named buckets for better categorization
- **Composite Key System**: Reference assets with `registry_name/asset_uid` format
- **Multi-Registry Lookup**: Query across all loaded registries
- **Override Layer**: Runtime asset override system for modding/testing
- **Comprehensive Validation**: Duplicate detection, empty registry warnings, null checks

## Quick Start

### Creating a Registry
1. Right-click in Project window → `Create > Core > Registries > Registry`
2. Select the Asset Type (Prefab, Texture, Material, Mesh, or Audio)
3. Name the ScriptableObject file appropriately (e.g., "WeaponPrefabs", "TerrainTextures")
4. Add items with unique UIDs and assign assets of the locked type

### Basic Usage
```csharp
// Get assets by UID
var prefab = RegistryManager.Instance.GetPrefabByUID("sword_iron");
var texture = RegistryManager.Instance.GetTextureByUID("grass_01");
var material = RegistryManager.Instance.GetMaterialByUID("concrete");

// Use composite keys for explicit registry targeting
var weapon = RegistryManager.Instance.GetPrefabByUID("weapons/sword_iron");

// Query by tags
List<ItemEntry> metallicItems = RegistryManager.Instance.GetItemsByTag("metallic");
```

## Documentation

For complete documentation, see [README.md](README.md) and [REGISTRY_DOCUMENTATION.md](REGISTRY_DOCUMENTATION.md) included in the package.

## License

[Add your license here]

## Contributing

[Add contribution guidelines if applicable]
