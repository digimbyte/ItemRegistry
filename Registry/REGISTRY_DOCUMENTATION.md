# Tile Atlas System Documentation

## Overview

The Tile Atlas System is a scriptable, modular asset management framework for storing, organizing, and instantiating tile prefabs in OpenSyndicate. It bridges Easy Grid Builder Pro 2 (EGBP2), the WorldGrid system, and your modding tools with a clean, performant interface.

**Key Features:**
- String-based UID lookup for tile prefabs
- Hierarchical organization by category and type
- QR axial coordinate support (UV purged per requirements)
- Runtime object pooling for performance
- Editor tools for auto-building atlases from folder structures
- Integration with WorldGrid for grid-based placement
- Extensible metadata system for custom properties

---

## Architecture

### Core Components

#### 1. **TileAtlas.cs** - The Asset Definition
The main ScriptableObject that stores a collection of tile entries.

**Properties:**
- `atlasName`: Unique identifier for the atlas (e.g., "tiles_industrial")
- `category`: TileCategory enum (Terrain, Building, Structure, Props, WorldObjects, etc.)
- `tileEntries`: List of TileEntry objects (UID → Prefab mappings)

**Key Methods:**
```csharp
// Get by UID
TileEntry entry = atlas.GetTileByUID("floor_metal_01");
GameObject prefab = atlas.GetPrefabByUID("floor_metal_01");

// Filter operations
List<TileEntry> floors = atlas.GetTilesByType(TileType.Floor);
List<TileEntry> industrial = atlas.GetTilesByTag("industrial");

// Validation
atlas.ValidateAtlas(); // Check for duplicates and missing prefabs
```

#### 2. **TileEntry** - Individual Tile Definition
A serializable struct representing one tile in an atlas.

**Fields:**
- `uid`: String identifier (required, must be unique per atlas)
- `prefab`: GameObject prefab to instantiate
- `tileType`: TileType enum (Floor, Wall, Door, etc.)
- `tags`: List of custom tags for filtering
- `scale`: Scale multiplier for the prefab
- `qrCoordinate`: Vector2Int for hex-based coordinate systems (QR axial)
- `isRotatable`: Whether the tile can be rotated
- `maxRotations`: Number of 90° rotation states
- `physicsData`: TilePhysicsData (collision, walkability, friction)
- `metadata`: Custom key-value pairs for extensibility

**Methods:**
```csharp
string desc = entry.GetMetadata("description", "default");
bool hasTag = entry.HasTag("industrial");
```

#### 3. **TileAtlasManager.cs** - Runtime Singleton Manager
Loads and caches all atlases for fast runtime lookup.

**Features:**
- Singleton pattern for global access
- Lazy loading from Resources
- Multi-atlas lookup (returns tile from any loaded atlas)
- Category and tag filtering across all atlases
- Cache validation and statistics

**Usage:**
```csharp
TileAtlasManager manager = TileAtlasManager.Instance;

// Load atlases
manager.LoadAtlasesFromResources(); // Auto-load from Resources/Atlases/Tiles

// Get tiles
TileEntry tile = manager.GetTileByUID("floor_metal_01");
List<TileEntry> allFloors = manager.GetTilesByType(TileType.Floor);
List<TileEntry> building = manager.GetTilesByCategory(TileCategory.Building);

// Check existence
bool exists = manager.HasTile("floor_metal_01");

// Debug
manager.PrintCacheStats();
```

#### 4. **TileAtlasIntegration.cs** - EGBP2/WorldGrid Bridge
Provides instantiation, pooling, and grid integration.

**Features:**
- Spawn tiles at grid positions or world positions
- Object pooling for performance
- Pool pre-warming for predictable performance
- Grid cell filling with automatic GridCellType assignment
- Region filling with random tile selection

**Usage:**
```csharp
TileAtlasIntegration integration = TileAtlasIntegration.Instance;

// Spawn at grid position
GameObject tile = integration.SpawnTile("floor_metal_01", new Vector3Int(0, 0, 0));

// Spawn at world position
GameObject tile = integration.SpawnTileAtWorldPosition("wall_brick_01", Vector3.zero);

// Fill grid cell
integration.FillGridCell(new Vector3Int(5, 0, 5), "floor_metal_01");

// Fill a region
var region = integration.FillGridRegion(
    new Vector3Int(0, 0, 0),
    new Vector3Int(10, 1, 10),
    TileCategory.Terrain
);

// Object pooling
integration.PreWarmPoolForTile("floor_metal_01", 50);
integration.PreWarmPoolForAtlas("tiles_industrial", 20);

// Despawn (returns to pool or destroys)
integration.DespawnTile(tile);

// Cleanup
integration.ClearPools();
integration.PrintPoolStats();
```

---

## Setup Guide

### Step 1: Create the Atlas Manager

1. In your main game scene, create an empty GameObject called "AtlasManager"
2. Add the `TileAtlasManager` component
3. Configure:
   - **Auto Load Atlases**: true
   - **Atlases Path**: "Atlases/Tiles" (or your preferred path)
   - **Preload Atlases**: Drag specific atlases if you want guaranteed loading

### Step 2: Create the Integration Component

1. Create an empty GameObject called "TileIntegration"
2. Add the `TileAtlasIntegration` component
3. Configure:
   - **Atlas Manager**: Auto-populates from singleton
   - **Tile Parent Transform**: Where spawned tiles will be parented
   - **Use Object Pooling**: true (recommended)
   - **Pool Size**: 100 (adjust based on memory)

### Step 3: Create Your First Atlas

**Option A: Manual Creation**
1. Right-click in Project → Create → OpenSyndicate → Tile Atlas
2. Name it (e.g., "tiles_industrial.asset")
3. Assign a name and category
4. Manually add TileEntry objects with prefabs

**Option B: Auto-Build from Folder**
1. Organize prefabs in a folder structure:
   ```
   Assets/Prefabs/Tiles/
   ├── Floor/
   │   ├── floor_metal_01.prefab
   │   ├── floor_metal_02.prefab
   ├── Wall/
   │   ├── wall_brick_01.prefab
   ```
2. Open Window → OpenSyndicate → Tile Atlas Builder
3. Set scan path to your prefab folder
4. Click "Scan for Prefabs"
5. Click "Create Atlas from Results"

### Step 4: Store Atlases in Resources

Move your atlas files to `Assets/Resources/Atlases/Tiles/` so they can be auto-loaded.

---

## Integration with EGBP2

Easy Grid Builder Pro 2 works by generating grid-based levels. The Tile Atlas System provides the **prefab database** that EGBP2 references.

### Workflow

1. **Define Your Assets in Atlases**
   - Create atlases for each asset category (terrain, buildings, props)
   - Organize with meaningful UIDs

2. **In EGBP2 Editor**
   - When painting with EGBP2, reference tile UIDs
   - Example: Paint a cell with `floor_metal_01` instead of dragging prefabs

3. **Runtime Loading**
   - At game start, TileAtlasManager loads all atlases
   - EGBP2 data includes UID strings instead of direct prefab references
   - TileAtlasIntegration instantiates the correct prefabs

### Example EGBP2 Integration Code

```csharp
public class EGBP2TileMapper : MonoBehaviour
{
    public void PaintCellWithTileUID(Vector3Int gridPos, string tileUID)
    {
        var integration = TileAtlasIntegration.Instance;
        integration.FillGridCell(gridPos, tileUID);
    }

    public void LoadLevelFromUIDs(List<(Vector3Int pos, string uid)> cells)
    {
        var integration = TileAtlasIntegration.Instance;
        
        foreach (var (pos, uid) in cells)
        {
            integration.SpawnTile(uid, pos);
        }
    }
}
```

---

## Integration with WorldGrid

The WorldGrid system manages spatial data, chunk streaming, and grid queries.

### Automatic Integration

TileAtlasIntegration automatically:
- Converts grid positions to world positions via WorldGrid
- Registers spawned tiles with GridCell metadata
- Validates grid bounds before spawning

### Example Usage with WorldGrid

```csharp
public class CityBuilder : MonoBehaviour
{
    void BuildCity()
    {
        var worldGrid = WorldGrid.Instance;
        var integration = TileAtlasIntegration.Instance;
        
        // Fill a city block
        Vector3Int startPos = new Vector3Int(0, 0, 0);
        Vector3Int size = new Vector3Int(32, 2, 32);
        
        // Fill ground layer
        integration.FillGridRegion(startPos, new Vector3Int(size.x, 1, size.z), TileCategory.Terrain);
        
        // Build structures
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                var pos = startPos + new Vector3Int(x * 2, 1, z * 2);
                integration.FillGridCell(pos, "building_residential_01");
            }
        }
    }
}
```

---

## QR Coordinate Support

For hexagonal or offset grid systems, each TileEntry includes a `qrCoordinate` field storing the axial coordinate (Q, R).

**Usage:**
```csharp
// Accessing QR coordinates
TileEntry entry = atlas.GetTileByUID("hex_tile_01");
Vector2Int qr = entry.qrCoordinate;
int q = qr.x;
int r = qr.y;

// Setting QR coordinates
entry.qrCoordinate = new Vector2Int(5, 3);
```

UV coordinates have been **completely purged** from the system per requirements. All spatial data uses QR axial or Cartesian coordinates exclusively.

---

## Performance Optimization

### Object Pooling

Tile instantiation is expensive. The pooling system reuses GameObjects:

```csharp
var integration = TileAtlasIntegration.Instance;

// Pre-warm pools during loading
integration.PreWarmPoolForAtlas("tiles_industrial", 50); // 50 of each tile

// Spawn uses pooled instances
var tile = integration.SpawnTile("floor_metal_01", gridPos);

// Despawn returns to pool
integration.DespawnTile(tile); // Available for reuse
```

### Caching

- **TileAtlas**: Lazy-loads internal cache on first access
- **TileAtlasManager**: Global cache with O(1) UID lookup
- **TileAtlasIntegration**: Pool caches avoid repeated lookups

### Batch Operations

For large-scale operations, use batch methods:

```csharp
// Good: Single batch operation
var results = integration.FillGridRegion(startPos, size, category);

// Avoid: Multiple spawns in a loop
for (int i = 0; i < 100; i++)
{
    integration.SpawnTile(uid, pos); // Slower
}
```

---

## Editor Tools

### Window → OpenSyndicate → Tile Atlas Builder

Auto-generates atlases from folder structures:

1. Set **Atlas Name** (e.g., "tiles_industrial")
2. Set **Category** (e.g., TileCategory.Building)
3. Set **Scan Path** to your prefab folder
4. Optionally set **Prefab Name Filter** (e.g., "floor_")
5. Enable **Use Directory as Tile Type** to infer types from folder names
6. Click **Scan for Prefabs**
7. Click **Create Atlas from Results**

### Assets Menu

- **Assets → Create → OpenSyndicate → Tile Atlas**: Create new atlas
- **Assets → OpenSyndicate → Atlas → Validate Selected Atlas**: Check atlas for errors
- **Assets → OpenSyndicate → Atlas → Quick Scan Folder**: Open builder window

---

## Data Storage Structure

```
Assets/
├── Atlases/
│   └── Tiles/
│       ├── tiles_industrial.asset
│       ├── tiles_terrain.asset
│       ├── tiles_props.asset
│       └── tiles_water.asset
├── Prefabs/
│   └── Tiles/
│       ├── Floor/
│       │   ├── floor_metal_01.prefab
│       │   └── floor_metal_02.prefab
│       └── Wall/
│           ├── wall_brick_01.prefab
│           └── wall_brick_02.prefab
└── Resources/
    └── Atlases/
        └── Tiles/
            ├── tiles_industrial.asset (copy or reference)
            ├── tiles_terrain.asset
            └── ...
```

---

## Modding Tools Integration

For external modding tools that need to load and reference assets:

```csharp
public class ModdingToolAtlasProvider
{
    public static List<TileEntry> GetAllAvailableTiles()
    {
        var manager = TileAtlasManager.Instance;
        var allTiles = new List<TileEntry>();
        
        foreach (var atlas in manager.GetAllAtlases().Values)
        {
            allTiles.AddRange(atlas.GetAllTiles());
        }
        
        return allTiles;
    }

    public static Dictionary<string, GameObject> GetTilePrefabDatabase()
    {
        var manager = TileAtlasManager.Instance;
        var database = new Dictionary<string, GameObject>();
        
        foreach (var tile in GetAllAvailableTiles())
        {
            database[tile.uid] = tile.prefab;
        }
        
        return database;
    }
}
```

---

## Validation and Debugging

### Validate Individual Atlas

```csharp
TileAtlas atlas = Resources.Load<TileAtlas>("Atlases/Tiles/tiles_industrial");
// In Inspector: Click "Validate Atlas" button
// Logs: duplicate UIDs, missing prefabs, warnings
```

### Debug Manager Cache

```csharp
TileAtlasManager.Instance.PrintCacheStats();
// Output:
// [TileAtlasManager] Cache Statistics:
//   Loaded Atlases: 3
//   Total Cached Tiles: 145
//   Atlases:
//     - tiles_industrial: 50 tiles
//     - tiles_terrain: 60 tiles
//     - tiles_props: 35 tiles
```

### Debug Pooling

```csharp
TileAtlasIntegration.Instance.PrintPoolStats();
// Output:
// [TileAtlasIntegration] Pool Statistics:
//   Pooling Enabled: true
//   Pool Count: 12
//   Total Pooled Instances: 487
//     - floor_metal_01: 45 pooled
//     - wall_brick_01: 32 pooled
//     ...
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "Tile UID not found" | Check UID spelling, ensure atlas is registered with manager |
| Slow spawning | Enable pooling, pre-warm pools during load |
| WorldGrid.Instance not found | Add WorldGrid to scene before TileAtlasIntegration |
| Atlas not auto-loading | Move atlas file to Resources/Atlases/Tiles/ |
| Duplicate UID warnings | Use ValidateAtlas() to find duplicates |
| Pool exhausted | Increase Pool Size or enable unlimited pooling |

---

## Best Practices

1. **Naming Conventions**
   - UIDs: `{category}_{type}_{variant}` (e.g., "floor_metal_01")
   - Atlas names: `tiles_{theme}` (e.g., "tiles_industrial")

2. **Organization**
   - One atlas per category or theme
   - Group related prefabs in folders
   - Use consistent naming patterns

3. **Performance**
   - Pre-warm pools for frequently-used tiles
   - Use batch operations instead of individual spawns
   - Cache tile lookups when possible

4. **Extensibility**
   - Use metadata for custom properties
   - Create editor tools for domain-specific workflows
   - Extend TileEntry for additional data

---

## API Reference

### TileAtlas
```csharp
public TileEntry GetTileByUID(string uid)
public GameObject GetPrefabByUID(string uid)
public List<TileEntry> GetTilesByType(TileType tileType)
public List<TileEntry> GetTilesByTag(string tag)
public bool HasTile(string uid)
public void AddTile(TileEntry entry)
public bool RemoveTile(string uid)
public List<TileEntry> GetAllTiles()
public int TileCount { get; }
```

### TileAtlasManager
```csharp
public void LoadAtlasesFromResources()
public void RegisterAtlas(TileAtlas atlas)
public bool UnregisterAtlas(string atlasName)
public TileEntry GetTileByUID(string uid)
public GameObject GetPrefabByUID(string uid)
public TileAtlas GetAtlas(string atlasName)
public TileAtlas GetAtlasForTile(string uid)
public List<TileEntry> GetTilesByType(TileType tileType)
public List<TileEntry> GetTilesByTag(string tag)
public List<TileEntry> GetTilesByCategory(TileCategory category)
public bool HasTile(string uid)
public Dictionary<string, TileAtlas> GetAllAtlases()
public int GetTotalTileCount()
```

### TileAtlasIntegration
```csharp
public GameObject SpawnTile(string tileUID, Vector3Int gridPosition, Quaternion rotation = default)
public GameObject SpawnTileAtWorldPosition(string tileUID, Vector3 worldPosition, Quaternion rotation = default)
public void DespawnTile(GameObject tileInstance)
public void FillGridCell(Vector3Int gridPosition, string tileUID, Quaternion rotation = default)
public List<GameObject> FillGridRegion(Vector3Int startPos, Vector3Int size, TileCategory category)
public TileEntry GetRandomTileFromAtlas(string atlasName)
public void PreWarmPoolForTile(string tileUID, int count)
public void PreWarmPoolForAtlas(string atlasName, int countPerTile)
public void ClearPools()
```

---

## Version History

- **1.0** (2025-11-13): Initial release with core atlas, manager, and integration systems
