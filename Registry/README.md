# Asset Registry System

A robust and type-safe asset management system for Unity, built with Odin Inspector for enhanced editor experience.

## Features

### Registry (ScriptableObject)
- **Type-Locked Design**: Each registry can ONLY store one asset type (Prefab, Texture, Material, Mesh, or Audio)
- **UID-based Lookup**: Fast dictionary-based asset retrieval by string identifier
- **Type-safe Getters**: Dedicated methods for each asset type with automatic casting
- **Filtering & Querying**: Filter by tags
- **Validation**: Built-in validation tools to check for duplicates, invalid assets, and type mismatches

### RegistryManager (Singleton)
- **Named Buckets**: Organize registries into named buckets for better categorization
  - Example: "Weapons", "Terrain", "UI", "Audio", etc.
  - Add/remove registries to buckets at runtime or in editor
- **Composite Key System**: Reference assets with `registry_name/asset_uid` format
- **Multi-Registry Lookup**: Query across all loaded registries
- **Override Layer**: Runtime asset override system for modding/testing
- **Comprehensive Validation**: Duplicate detection, empty registry warnings, null checks

### Registry Asset Types
```csharp
public enum RegistryAssetType
{
    Prefab,     // GameObject prefabs only
    Texture,    // Texture2D, Texture only
    Material,   // Material assets only
    Mesh,       // Mesh assets only
    Audio       // AudioClip assets only
}
```
**Each registry is locked to ONE type.** You cannot mix asset types in a single registry.

## Usage Examples

### Creating a Registry
1. Right-click in Project window → `Create > Core > Registries > Registry`
2. **FIRST**: Select the Asset Type (Prefab, Texture, Material, Mesh, or Audio)
3. Name the ScriptableObject file appropriately (e.g., "WeaponPrefabs", "TerrainTextures", "UISounds")
   - The file name is used by RegistryManager for identification
4. Add items with unique UIDs and assign assets **of the locked type only**

### Setting up RegistryManager
```csharp
// Add to scene with RegistryManager component
var manager = RegistryManager.Instance;

// Add registries to named buckets (in inspector or runtime)
manager.AddRegistryToBucket("Weapons", weaponsRegistry);
manager.AddRegistryToBucket("Textures", texturesRegistry);

// Or use preload list for automatic loading
```

### Retrieving Assets

#### By UID
```csharp
// Simple UID lookup
var prefab = RegistryManager.Instance.GetPrefabByUID("sword_iron");
var texture = RegistryManager.Instance.GetTextureByUID("grass_01");
var material = RegistryManager.Instance.GetMaterialByUID("concrete");
var mesh = RegistryManager.Instance.GetMeshByUID("wall_segment");
var audio = RegistryManager.Instance.GetAudioByUID("footstep_grass");

// Generic typed lookup
var asset = RegistryManager.Instance.GetAssetByUID<Material>("metal_rusty");
```

#### By Composite Key
```csharp
// Explicit registry targeting
var weapon = RegistryManager.Instance.GetPrefabByUID("weapons/sword_iron");
var tex = RegistryManager.Instance.GetTextureByUID("terrain/grass_01");
```

#### From Named Buckets
```csharp
// Get registry from bucket
var weaponsRegistry = RegistryManager.Instance.GetRegistryFromBucket("Weapons");
var sword = weaponsRegistry.GetPrefabByUID("sword_iron");

// Check if bucket exists
if (RegistryManager.Instance.HasBucket("Weapons"))
{
    // Use weapons registry
}
```

#### Bulk Queries
```csharp
// Get all prefabs across all registries
List<GameObject> allPrefabs = RegistryManager.Instance.GetAllPrefabs();

// Get all textures
List<Texture> allTextures = RegistryManager.Instance.GetAllTextures();

// Get registries by asset type
List<Registry> audioRegistries = RegistryManager.Instance.GetRegistriesByAssetType(RegistryAssetType.Audio);

// Get items by tag
List<ItemEntry> metallicItems = RegistryManager.Instance.GetItemsByTag("metallic");
```

### Runtime Registry Management
```csharp
// Add registry to bucket at runtime
RegistryManager.Instance.AddRegistryToBucket("NewBucket", myRegistry);

// Remove registry from bucket
RegistryManager.Instance.RemoveRegistryFromBucket("OldBucket");

// Get all bucket names
List<string> buckets = RegistryManager.Instance.GetAllBucketNames();

// Reload all registries
RegistryManager.Instance.ReloadFromPreload();
```

### Asset Overrides (Runtime Modding)
```csharp
// Add runtime override
ItemEntry customEntry = new ItemEntry { uid = "custom_asset", asset = myCustomAsset };
RegistryManager.Instance.AddOverride("registry/path", customEntry);

// Remove override
RegistryManager.Instance.RemoveOverride("registry/path");

// Clear all overrides
RegistryManager.Instance.ClearOverrides();
```

## Odin Inspector Features

### Registry Inspector
- Asset preview thumbnails
- Asset type auto-detection display
- Horizontal layout for UID and type
- Grouped properties (Asset, Properties, Physics, Metadata)
- Validation buttons for checking registry integrity

### RegistryManager Inspector
- Dictionary drawer for named buckets (key-value pairs)
- Bucket validation tools
- Cache statistics display
- Debug tools section
- Action buttons for common operations

## Best Practices

1. **Naming Convention**: Use descriptive UIDs like `category_subcategory_name`
   - Example: `weapon_melee_sword_iron`, `texture_terrain_grass_01`

2. **Composite Keys**: For multi-registry projects, use composite keys
   - Example: `weapons/sword_iron`, `textures/grass_01`

3. **Named Buckets**: Organize registries by domain
   - Weapons, Armor, Terrain, Props, Audio, UI, etc.

4. **Validation**: Run validation tools regularly to catch issues early
   - Use "Validate Registry" button in each registry
   - Use "Validate All Buckets" in RegistryManager

5. **Registry Type-Locking**: Each registry can only hold ONE asset type
   - Create separate registries for each type: WeaponPrefabs, TerrainTextures, MetalMaterials, etc.
   - The system will reject assets that don't match the registry's locked type

## Performance Notes

- Registries use dictionary caching for O(1) lookup performance
- Cache is built on first access and invalidated on changes
- Composite keys provide direct registry targeting (faster than global search)
- Override layer checked before main cache for maximum flexibility
