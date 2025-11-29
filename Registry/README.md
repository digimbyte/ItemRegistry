# Asset Registry System

A type-safe, high-performance asset management framework for Unity that provides centralized asset storage, runtime lookup, object pooling, and lifecycle management.

## Overview

The Asset Registry system provides a robust solution for managing game assets through three core components:

- **Registry**: Type-locked ScriptableObject containers for organizing assets by type (Prefabs, Textures, Materials, Meshes, Audio)
- **RegistryManager**: Runtime singleton for fast UID-based lookups across multiple registries with composite key support
- **RegistryIntegration**: Object pooling and lifecycle management layer for instantiation optimization

## Core Components

### Registry (ScriptableObject)

Individual registry instances that store collections of typed assets. Each registry is **locked to a single asset type** to ensure type safety and prevent configuration errors.

**Features:**
- Type-locked asset storage (Prefab, Texture, Material, Mesh, Audio)
- Fast UID-based lookups with dictionary caching
- Tag-based filtering and categorization
- Custom metadata support per item
- Default/fallback asset handling
- Editor validation tools

**Creating a Registry:**
```
Right-click in Project → Create → Core/Registry
```

Configure the registry:
1. Set **Asset Type** (cannot change after adding items)
2. Assign **Default/Fallback Asset** (required, must match asset type)
3. Add **Description** for documentation
4. Populate **Items** list

### ItemEntry

Individual asset entries within a registry.

**Properties:**
- `uid` (string): Unique identifier for the asset (used as path in composite keys)
- `asset` (UnityEngine.Object): Reference to the actual asset
- `tags` (List<string>): Categories/labels for filtering
- `description` (string): Human-readable documentation
- `metadata` (Dictionary<string, string>): Arbitrary key-value data

**Tag Usage Examples:**
- Categorization: `"enemy"`, `"environment"`, `"consumable"`
- Filtering: `"rare"`, `"animated"`, `"interactive"`
- Runtime queries: `registry.GetItemsByTag("hostile")`

**Metadata Usage Examples:**
```csharp
// Gameplay data
item.metadata["damage"] = "50";
item.metadata["rarity"] = "legendary";
item.metadata["biome"] = "desert";

// Asset pipeline info
item.metadata["author"] = "ArtistName";
item.metadata["version"] = "1.2";
item.metadata["polyCount"] = "2400";

// Integration IDs
item.metadata["localizationKey"] = "item_sword_01";
item.metadata["analyticsId"] = "wpn_legendary_001";
```

### RegistryManager (MonoBehaviour Singleton)

Runtime manager that loads and provides fast access to all registered assets across multiple registries.

**Key Features:**
- Singleton pattern with DontDestroyOnLoad
- Named bucket system for organizing registries
- Composite key support: `"<registryName>/<itemUID>"`
- Override layer for runtime asset swapping
- Global tag-based queries across all registries
- Registry load order tracking

**Composite Key Format:**
```
"registryName/path/to/item"
```

**Named Buckets:**
Buckets allow you to organize registries by purpose:
```csharp
// In Inspector, assign registries to buckets:
// - "Enemies" → EnemyPrefabRegistry
// - "Props" → PropPrefabRegistry
// - "Materials" → MaterialRegistry

// Runtime access:
var enemyRegistry = manager.GetRegistryFromBucket("Enemies");
```

### RegistryIntegration (MonoBehaviour)

Lifecycle management layer providing object pooling, instantiation, and memory tracking.

**Features:**
- Object pooling with configurable pool sizes
- Spawn/Despawn API for GameObjects
- Pre-warming support for performance
- Memory tracking and auto-cleanup
- Random asset selection utilities

## Usage Examples

### Basic Asset Lookup

```csharp
using Core.Registry;

// Get reference to manager
var manager = RegistryManager.Instance;

// Lookup with composite key (recommended)
GameObject enemyPrefab = manager.GetPrefabByUID("enemies/goblin_warrior");
Texture icon = manager.GetTextureByUID("ui/icon_sword");
Material mat = manager.GetMaterialByUID("materials/metal_rusty");

// Bare UID lookup (searches all registries, must be unique)
GameObject item = manager.GetPrefabByUID("rare_item_01");

// Generic typed lookup
var audioClip = manager.GetAssetByUID<AudioClip>("audio/sfx_explosion");
```

### Tag-Based Queries

```csharp
// Get all items with specific tag across all registries
List<ItemEntry> hostiles = manager.GetItemsByTag("hostile");
List<ItemEntry> rareItems = manager.GetItemsByTag("rare");

// Get items by tag from specific registry
Registry enemyRegistry = manager.GetRegistry("enemies");
List<ItemEntry> flyingEnemies = enemyRegistry.GetItemsByTag("flying");

// Check if item has tag
ItemEntry item = manager.GetItemByUID("enemies/goblin_warrior");
if (item.HasTag("melee"))
{
    // Handle melee enemy
}
```

### Metadata Access

```csharp
ItemEntry weapon = manager.GetItemByUID("weapons/legendary_sword");

// Get metadata with default fallback
string rarity = weapon.GetMetadata("rarity", "common");
int damage = int.Parse(weapon.GetMetadata("damage", "10"));
float dropChance = float.Parse(weapon.GetMetadata("dropChance", "0.01"));

// Check for metadata key
if (weapon.metadata.ContainsKey("elementalType"))
{
    string element = weapon.metadata["elementalType"];
    // Apply elemental effects
}
```

### Object Pooling

```csharp
using Core.Registry;

var integration = RegistryIntegration.Instance;

// Spawn object from pool
GameObject enemy = integration.Spawn(
    "enemies/goblin_warrior", 
    spawnPosition, 
    Quaternion.identity
);

// Return object to pool when done
integration.Despawn(enemy);

// Pre-warm pools for better performance
integration.PreWarmPool("enemies/goblin_warrior", 20);
integration.PreWarmPoolForRegistry("enemies", 10); // Pre-warm all items in registry

// Query pool stats
int pooled = integration.GetPooledInstanceCount("enemies/goblin_warrior");
int active = integration.GetActiveInstanceCount("enemies/goblin_warrior");
int totalPools = integration.GetPoolCount();

// Clear pools
integration.ClearPool("enemies/goblin_warrior"); // Clear specific
integration.ClearAllPools(); // Clear all
```

### Runtime Overrides

```csharp
// Override an asset at runtime (e.g., for modding, seasonal events)
var customEntry = new ItemEntry
{
    uid = "override_goblin",
    asset = customGoblinPrefab,
    tags = new List<string> { "enemy", "custom" },
    description = "Custom modded goblin variant"
};

manager.AddOverride("enemies/goblin_warrior", customEntry);

// Now lookups for "enemies/goblin_warrior" return the override
GameObject customEnemy = manager.GetPrefabByUID("enemies/goblin_warrior");

// Remove override
manager.RemoveOverride("enemies/goblin_warrior");

// Clear all overrides
manager.ClearOverrides();
```

### Registry Type Filtering

```csharp
// Get all registries of specific type
List<Registry> prefabRegistries = manager.GetRegistriesByAssetType(RegistryAssetType.Prefab);
List<Registry> materialRegistries = manager.GetRegistriesByAssetType(RegistryAssetType.Material);

// Iterate through all prefab registries
foreach (var registry in prefabRegistries)
{
    Debug.Log($"Prefab Registry: {registry.name} ({registry.ItemCount} items)");
}

// Get all assets of type across all registries
List<GameObject> allPrefabs = manager.GetAllPrefabs();
List<Texture> allTextures = manager.GetAllTextures();
List<Material> allMaterials = manager.GetAllMaterials();
```

### Advanced Lookups

```csharp
// Check if item exists
bool hasItem = manager.HasItem("enemies/goblin_warrior");

// Get the registry that contains an item
Registry ownerRegistry = manager.GetRegistryForItem("enemies/goblin_warrior");

// Get specific registry
Registry enemyRegistry = manager.GetRegistry("enemies");
if (enemyRegistry != null)
{
    Debug.Log($"Enemy Registry: {enemyRegistry.Description}");
    Debug.Log($"Total enemies: {enemyRegistry.ItemCount}");
    Debug.Log($"Asset type: {enemyRegistry.AssetType}");
}

// Get composite key for an entry
string compositeKey = manager.GetCompositeKeyForItem(itemEntry);
// Returns: "registryName/itemUID"

// Get total item count across all registries
int totalItems = manager.GetTotalItemCount();
```

## Setup Instructions

### 1. Create Registry ScriptableObjects

Create registries for different asset types:

```
Create → Core/Registry
```

Configure each registry:
- **Name**: Descriptive name (e.g., "EnemyPrefabs", "UIMaterials")
- **Asset Type**: Select from Prefab, Texture, Material, Mesh, Audio
- **Default Asset**: Assign fallback asset matching the type
- **Description**: Document the registry's purpose

### 2. Add Items to Registries

For each registry, populate the Items list:
1. Click `+` to add new ItemEntry
2. Set **UID** (unique identifier, used as path)
3. Assign **Asset** reference
4. Add **Tags** for categorization
5. Write **Description** for documentation
6. Add **Metadata** key-value pairs as needed

### 3. Set Up RegistryManager

Create a GameObject in your first scene:
```
GameObject → Create Empty → "RegistryManager"
Add Component → Registry Manager (Core.Registry)
```

Configure named buckets in the inspector:
- **Bucket Name**: Logical grouping (e.g., "Enemies", "Environment", "UI")
- **Registry**: Assign corresponding Registry ScriptableObject

The manager will automatically load all buckets on Awake and persist across scenes.

### 4. (Optional) Set Up RegistryIntegration

For object pooling and spawning features:

```
GameObject → Create Empty → "RegistryIntegration"
Add Component → Registry Integration (Core.Registry)
```

Configure settings:
- **Registry Manager**: Auto-assigns to RegistryManager.Instance
- **Spawned Objects Parent**: Transform for spawned objects (optional)
- **Use Object Pooling**: Enable/disable pooling
- **Max Pool Size Per Asset**: Max pooled instances per asset
- **Enable Memory Tracking**: Track active/pooled counts
- **Auto-Cleanup Threshold**: Trigger cleanup when exceeded

## Best Practices

### UID Naming Conventions

Use hierarchical paths for UIDs:
```
"enemies/melee/goblin_warrior"
"enemies/ranged/goblin_archer"
"props/furniture/chair_wooden"
"materials/terrain/grass_dry"
"audio/music/combat_theme_01"
```

**Benefits:**
- Logical organization
- Easier to search/filter
- Composite keys work naturally: `"enemies/melee/goblin_warrior"`

### Registry Organization

**Separate by asset type:**
- ✅ Good: One registry per asset type (PrefabRegistry, TextureRegistry)
- ❌ Bad: Mixing prefabs and materials in one registry

**Separate by context:**
- ✅ Good: Separate registries for different systems (EnemyPrefabs, PlayerPrefabs)
- ✅ Good: Environment assets separate from character assets

### Tag Strategy

Use consistent, reusable tags:
```
// Functionality tags
"animated", "interactive", "physics", "damageable"

// Category tags
"enemy", "prop", "weapon", "consumable"

// Quality/tier tags
"common", "rare", "legendary"

// Context tags
"tutorial", "endgame", "seasonal"
```

### Metadata Guidelines

Store **data** in metadata, not logic:
```csharp
// ✅ Good: Store values
metadata["damage"] = "50";
metadata["cooldown"] = "2.5";

// ❌ Bad: Store code/logic
metadata["onHitEffect"] = "ApplyBurn()"; // Don't do this
```

Use consistent key naming:
```csharp
// Use camelCase or snake_case consistently
metadata["damageAmount"] = "50";
metadata["attackSpeed"] = "1.5";
```

### Performance Tips

**Pre-warm pools for frequently spawned objects:**
```csharp
void Start()
{
    integration.PreWarmPool("enemies/goblin_warrior", 50);
    integration.PreWarmPool("vfx/hit_spark", 100);
}
```

**Use composite keys for direct lookups:**
```csharp
// ✅ Fast: Direct registry lookup
var item = manager.GetItemByUID("enemies/goblin_warrior");

// ❌ Slower: Searches all registries
var item = manager.GetItemByUID("goblin_warrior");
```

**Cache frequently accessed assets:**
```csharp
// ✅ Good: Cache in Awake/Start
private GameObject goblinPrefab;

void Awake()
{
    goblinPrefab = manager.GetPrefabByUID("enemies/goblin_warrior");
}

// ❌ Bad: Lookup every frame
void Update()
{
    var goblin = manager.GetPrefabByUID("enemies/goblin_warrior");
}
```

## API Reference

### Registry

| Method | Description |
|--------|-------------|
| `GetItemByUID(string uid)` | Get ItemEntry by UID |
| `GetAssetByUID(string uid)` | Get raw asset Object by UID |
| `GetPrefabByUID(string uid)` | Get GameObject prefab by UID |
| `GetTextureByUID(string uid)` | Get Texture by UID |
| `GetMaterialByUID(string uid)` | Get Material by UID |
| `GetMeshByUID(string uid)` | Get Mesh by UID |
| `GetAudioByUID(string uid)` | Get AudioClip by UID |
| `GetAssetByUID<T>(string uid)` | Get typed asset by UID |
| `GetItemsByTag(string tag)` | Get all items with tag |
| `GetAllItems()` | Get all items in registry |
| `HasItem(string uid)` | Check if item exists |
| `AddItem(ItemEntry entry)` | Add item to registry |
| `RemoveItem(string uid)` | Remove item from registry |

### RegistryManager

| Method | Description |
|--------|-------------|
| `RegisterItem(Registry registry, string bucket)` | Register a registry |
| `UnregisterRegistry(string name)` | Unregister a registry |
| `GetItemByUID(string uid)` | Get ItemEntry by UID (composite or bare) |
| `GetAssetByUID(string uid)` | Get raw asset by UID |
| `GetPrefabByUID(string uid)` | Get GameObject by UID |
| `GetTextureByUID(string uid)` | Get Texture by UID |
| `GetMaterialByUID(string uid)` | Get Material by UID |
| `GetMeshByUID(string uid)` | Get Mesh by UID |
| `GetAudioByUID(string uid)` | Get AudioClip by UID |
| `GetAssetByUID<T>(string uid)` | Get typed asset by UID |
| `GetRegistry(string name)` | Get registry by name |
| `GetRegistryForItem(string uid)` | Get registry containing item |
| `GetItemsByTag(string tag)` | Get items by tag (all registries) |
| `GetRegistriesByAssetType(type)` | Get all registries of type |
| `GetAllPrefabs()` | Get all prefabs (all registries) |
| `GetAllTextures()` | Get all textures (all registries) |
| `GetAllMaterials()` | Get all materials (all registries) |
| `GetAllMeshes()` | Get all meshes (all registries) |
| `GetAllAudioClips()` | Get all audio clips (all registries) |
| `HasItem(string uid)` | Check if item exists |
| `AddOverride(string key, ItemEntry)` | Add runtime override |
| `RemoveOverride(string key)` | Remove override |
| `ClearOverrides()` | Clear all overrides |
| `GetCompositeKeyForItem(entry)` | Get composite key for entry |
| `GetTotalItemCount()` | Get total items across registries |
| `AddRegistryToBucket(name, registry)` | Add registry to bucket |
| `RemoveRegistryFromBucket(name)` | Remove from bucket |
| `GetRegistryFromBucket(name)` | Get registry from bucket |
| `GetAllBucketNames()` | Get all bucket names |
| `HasBucket(string name)` | Check if bucket exists |

### RegistryIntegration

| Method | Description |
|--------|-------------|
| `Spawn(uid, position, rotation)` | Spawn/get pooled GameObject |
| `Despawn(GameObject instance)` | Despawn/return to pool |
| `GetRandomAsset<T>(registryName)` | Get random asset from registry |
| `PreWarmPool(uid, count)` | Pre-instantiate pooled objects |
| `PreWarmPoolForRegistry(name, count)` | Pre-warm all items in registry |
| `ClearAllPools()` | Destroy all pooled objects |
| `ClearPool(string uid)` | Clear specific asset pool |
| `GetActiveInstanceCount(uid)` | Get active instance count |
| `GetPooledInstanceCount(uid)` | Get pooled instance count |
| `GetTotalPooledCount()` | Get total pooled objects |
| `GetPoolCount()` | Get number of unique pools |

## Architecture Notes

### Type Safety

The system enforces type safety at multiple levels:
- Registry asset type is locked after first item is added
- Editor validation prevents wrong asset types
- Runtime type checks with helpful error messages
- Default/fallback assets must match registry type

### Performance Optimizations

- **Dictionary caching**: Fast O(1) UID lookups
- **Lazy initialization**: Caches built on first access
- **Object pooling**: Eliminates instantiation overhead
- **Memory tracking**: Optional profiling without overhead

### Extensibility

Easy to extend for custom needs:
- Add new `RegistryAssetType` enum values
- Extend `ItemEntry` with custom fields
- Create custom manager methods for specific queries
- Implement custom pooling strategies in RegistryIntegration

## Troubleshooting

### "Item UID not found" warnings

**Cause**: Requested UID doesn't exist in any loaded registry

**Solutions:**
- Verify UID spelling matches exactly (case-sensitive)
- Use composite key format: `"registryName/itemUID"`
- Check registry is loaded in RegistryManager buckets
- Use `HasItem()` before `GetItemByUID()` for safety

### "Ambiguous bare id" warnings

**Cause**: Same UID exists in multiple registries

**Solution**: Use composite key format: `"registryName/itemUID"`

### "Asset type mismatch" errors

**Cause**: Trying to add wrong asset type to registry

**Solutions:**
- Check registry's Asset Type setting
- Ensure default asset matches registry type
- Use correct type-specific getter methods

### Objects not pooling

**Cause**: Pooling disabled or pool size exceeded

**Solutions:**
- Verify "Use Object Pooling" is enabled in RegistryIntegration
- Increase "Max Pool Size Per Asset"
- Call `Despawn()` instead of `Destroy()`

### Performance issues

**Solutions:**
- Pre-warm pools for frequently spawned objects
- Use composite keys for direct lookups
- Cache frequently accessed assets
- Enable auto-cleanup with appropriate threshold
- Reduce max pool sizes if memory is constrained

## License

This Asset Registry system is part of the project's internal tools and follows the project's licensing terms.

## Version History

- **Current**: Complete registry system with pooling, metadata, tags, and composite key support
- Features: Type-locked registries, runtime overrides, memory tracking, named buckets
