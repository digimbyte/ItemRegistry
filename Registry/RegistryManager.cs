using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;

namespace Core.Registry
{
    /// <summary>
    /// Runtime manager for loading and accessing item Registries.
    /// Provides fast lookup by UID across multiple Registries.
    /// Implements caching and pooling for performance.
    /// </summary>
    public class RegistryManager : MonoBehaviour
    {
        [SerializeField]
        [LabelText("Named Registry Buckets")]
        [InfoBox("Assign registries to named buckets - all registries will load on startup")]
        [DictionaryDrawerSettings(KeyLabel = "Bucket Name", ValueLabel = "Registry")]
        private SerializableDictionary<string, Registry> namedBuckets = new SerializableDictionary<string, Registry>();

        // Runtime state
        private Dictionary<string, Registry> loadedRegistries = new Dictionary<string, Registry>();
        private Dictionary<string, ItemEntry> globalItemCache = new Dictionary<string, ItemEntry>();
        private Dictionary<string, Registry> itemToRegistryMap = new Dictionary<string, Registry>();
        private Dictionary<ItemEntry, string> entryToCompositeKey = new Dictionary<ItemEntry, string>();
        private List<string> registryLoadOrder = new List<string>();
        private Dictionary<string, ItemEntry> overrideItemCache = new Dictionary<string, ItemEntry>();

        // Keying strategy: composite key = "<registryName>/<path>"
        private static string NormalizeRegistryName(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim().Trim('/');
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Trim('/').Replace("\\", "/");
        }

        private static string MakeCompositeKey(string registryName, string itemPath)
        {
            var r = NormalizeRegistryName(registryName);
            var p = NormalizePath(itemPath);
            return string.IsNullOrEmpty(r) || string.IsNullOrEmpty(p) ? string.Empty : ($"{r}/{p}");
        }

        private static bool TryParseCompositeKey(string key, out string registryName, out string itemPath)
        {
            registryName = string.Empty;
            itemPath = string.Empty;
            if (string.IsNullOrWhiteSpace(key)) return false;
            var trimmed = key.Trim();
            int slash = trimmed.IndexOf('/');
            if (slash <= 0 || slash == trimmed.Length - 1) return false;
            registryName = NormalizeRegistryName(trimmed.Substring(0, slash));
            itemPath = NormalizePath(trimmed.Substring(slash + 1));
            return !string.IsNullOrEmpty(registryName) && !string.IsNullOrEmpty(itemPath);
        }

        public static RegistryManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeRegistries();
        }

        private void InitializeRegistries()
        {
            if (namedBuckets == null || namedBuckets.Count == 0)
            {
                Debug.LogWarning("[RegistryManager] No named buckets configured");
                return;
            }

            foreach (var kvp in namedBuckets)
            {
                if (kvp.Value != null)
                {
                    RegisterItem(kvp.Value);
                    Debug.Log($"[RegistryManager] Loaded registry from bucket '{kvp.Key}': {kvp.Value.name}");
                }
                else
                {
                    Debug.LogError($"[RegistryManager] Bucket '{kvp.Key}' has null registry!");
                }
            }
        }



        /// <summary>
        /// Register a Registry for use with comprehensive validation.
        /// </summary>
        public void RegisterItem(Registry Registry)
        {
            if (Registry == null)
            {
                Debug.LogError("[RegistryManager] Cannot register null Registry");
                return;
            }

            var regName = NormalizeRegistryName(Registry.name);
            if (string.IsNullOrEmpty(regName))
            {
                Debug.LogError("[RegistryManager] Registry asset has empty name; skipping registration");
                return;
            }

            // Validate registry has items
            if (Registry.ItemCount == 0)
            {
                Debug.LogWarning($"[RegistryManager] Registry '{regName}' has no items but will be registered anyway");
            }

            // Handle duplicate registration
            if (loadedRegistries.ContainsKey(regName))
            {
                if (loadedRegistries[regName] == Registry)
                {
                    Debug.LogWarning($"[RegistryManager] Registry '{regName}' is already registered (same instance)");
                    return;
                }
                Debug.LogWarning($"[RegistryManager] Registry '{regName}' already exists. Unregistering old instance first.");
                UnregisterRegistry(regName);
            }

            loadedRegistries[regName] = Registry;
            registryLoadOrder.Add(regName);
            
            // Build cache entries for this Registry using composite keys: "<registry>/<path>"
            foreach (var item in Registry.GetAllItems())
            {
                if (item == null) continue;
                var path = item.uid; // interpreted as path inside registry
                if (string.IsNullOrEmpty(path)) continue;

                var key = MakeCompositeKey(regName, path);
                if (string.IsNullOrEmpty(key)) continue;

                if (globalItemCache.ContainsKey(key))
                {
                    Debug.LogWarning($"[RegistryManager] Duplicate composite key '{key}' detected; first entry wins");
                    continue;
                }

                globalItemCache[key] = item;
                itemToRegistryMap[key] = Registry;
                entryToCompositeKey[item] = key;
            }

            Debug.Log($"[RegistryManager] Registered Registry '{regName}' [{Registry.AssetType}] with {Registry.ItemCount} items");
        }

        // Back-compat wrapper for older API name
        public void RegisterRegistry(Registry registry) => RegisterItem(registry);

        /// <summary>
        /// Unregister an Registry.
        /// </summary>
        public bool UnregisterRegistry(string RegistryName)
        {
            var name = NormalizeRegistryName(RegistryName);
            if (!loadedRegistries.TryGetValue(name, out var Registry))
                return false;

            loadedRegistries.Remove(name);
            registryLoadOrder.Remove(name);

            // Clear cache entries for this Registry
            var itemsToRemove = globalItemCache
                .Where(kvp => itemToRegistryMap.TryGetValue(kvp.Key, out var a) && a == Registry)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var uid in itemsToRemove)
            {
                globalItemCache.Remove(uid);
                itemToRegistryMap.Remove(uid);
            }

            Debug.Log($"[RegistryManager] Unregistered Registry '{RegistryName}'");
            return true;
        }

        /// <summary>
        /// Get a item entry by UID from any loaded Registry.
        /// </summary>
        public ItemEntry GetItemByUID(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return null;

            // Prefer composite keys: "prefix/path"; check overrides first
            if (uid.Contains("/"))
            {
                if (TryParseCompositeKey(uid, out var regName, out var path))
                {
                    var key = MakeCompositeKey(regName, path);
                    if (overrideItemCache.TryGetValue(key, out var ovr))
                        return ovr;
                    if (globalItemCache.TryGetValue(key, out var e))
                        return e;

                    // Fallback: direct lookup within the addressed registry if loaded but not cached yet
                    if (loadedRegistries.TryGetValue(regName, out var reg))
                        return reg.GetItemByUID(path);
                }
                Debug.LogWarning($"[RegistryManager] Item key '{uid}' not found");
                return null;
            }

            // Bare ids are ambiguous across registries. Search all registries and return only if unique
            ItemEntry unique = null;
            foreach (var reg in loadedRegistries.Values)
            {
                var e = reg.GetItemByUID(uid);
                if (e == null) continue;
                if (unique != null)
                {
                    Debug.LogWarning($"[RegistryManager] Ambiguous bare id '{uid}' across registries; use '<registry>/{uid}'");
                    return null;
                }
                unique = e;
            }
            if (unique != null) return unique;

            Debug.LogWarning($"[RegistryManager] Item id '{uid}' not found in any loaded Registry");
            return null;
        }

        /// <summary>
        /// Get the raw asset object by UID from any loaded Registry.
        /// </summary>
        public UnityEngine.Object GetAssetByUID(string uid)
        {
            var entry = GetItemByUID(uid);
            return entry?.asset;
        }

        /// <summary>
        /// Get a prefab by UID from any loaded Registry.
        /// </summary>
        public GameObject GetPrefabByUID(string uid)
        {
            var entry = GetItemByUID(uid);
            return entry?.asset as GameObject;
        }

        /// <summary>
        /// Get a texture by UID from any loaded Registry.
        /// </summary>
        public Texture GetTextureByUID(string uid)
        {
            var entry = GetItemByUID(uid);
            return entry?.asset as Texture;
        }

        /// <summary>
        /// Get a material by UID from any loaded Registry.
        /// </summary>
        public Material GetMaterialByUID(string uid)
        {
            var entry = GetItemByUID(uid);
            return entry?.asset as Material;
        }

        /// <summary>
        /// Get a mesh by UID from any loaded Registry.
        /// </summary>
        public Mesh GetMeshByUID(string uid)
        {
            var entry = GetItemByUID(uid);
            return entry?.asset as Mesh;
        }

        /// <summary>
        /// Get an audio clip by UID from any loaded Registry.
        /// </summary>
        public AudioClip GetAudioByUID(string uid)
        {
            var entry = GetItemByUID(uid);
            return entry?.asset as AudioClip;
        }

        /// <summary>
        /// Get a typed asset by UID with generic type parameter.
        /// </summary>
        public T GetAssetByUID<T>(string uid) where T : UnityEngine.Object
        {
            var entry = GetItemByUID(uid);
            return entry?.asset as T;
        }

        /// <summary>
        /// Get a specific Registry by name.
        /// </summary>
        public Registry GetRegistry(string RegistryName)
        {
            var name = NormalizeRegistryName(RegistryName);
            loadedRegistries.TryGetValue(name, out var Registry);
            return Registry;
        }

        /// <summary>
        /// Get the Registry that contains a specific item UID.
        /// </summary>
        public Registry GetRegistryForItem(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;

            if (uid.Contains("/") && TryParseCompositeKey(uid, out var regName, out var path))
            {
                var key = MakeCompositeKey(regName, path);
                if (itemToRegistryMap.TryGetValue(key, out var reg))
                    return reg;
                loadedRegistries.TryGetValue(regName, out var direct);
                return direct;
            }

            // Bare id path
            Registry found = null;
            foreach (var reg in loadedRegistries.Values)
            {
                if (reg.HasItem(uid))
                {
                    if (found != null) return null; // ambiguous
                    found = reg;
                }
            }
            return found;
        }


        /// <summary>
        /// Get all items matching a specific tag across all Registries.
        /// </summary>
        public List<ItemEntry> GetItemsByTag(string tag)
        {
            var results = new List<ItemEntry>();
            
            foreach (var Registry in loadedRegistries.Values)
            {
                results.AddRange(Registry.GetItemsByTag(tag));
            }

            return results;
        }


        /// <summary>
        /// Get all registries of a specific asset type.
        /// </summary>
        public List<Registry> GetRegistriesByAssetType(RegistryAssetType assetType)
        {
            var results = new List<Registry>();
            
            foreach (var registry in loadedRegistries.Values)
            {
                if (registry.AssetType == assetType)
                    results.Add(registry);
            }

            return results;
        }

        /// <summary>
        /// Get all prefabs across all loaded Registries.
        /// </summary>
        public List<GameObject> GetAllPrefabs()
        {
            var results = new List<GameObject>();
            
            foreach (var Registry in loadedRegistries.Values)
            {
                results.AddRange(Registry.GetAllPrefabs());
            }

            return results;
        }

        /// <summary>
        /// Get all textures across all loaded Registries.
        /// </summary>
        public List<Texture> GetAllTextures()
        {
            var results = new List<Texture>();
            
            foreach (var Registry in loadedRegistries.Values)
            {
                results.AddRange(Registry.GetAllTextures());
            }

            return results;
        }

        /// <summary>
        /// Get all materials across all loaded Registries.
        /// </summary>
        public List<Material> GetAllMaterials()
        {
            var results = new List<Material>();
            
            foreach (var Registry in loadedRegistries.Values)
            {
                results.AddRange(Registry.GetAllMaterials());
            }

            return results;
        }

        /// <summary>
        /// Get all meshes across all loaded Registries.
        /// </summary>
        public List<Mesh> GetAllMeshes()
        {
            var results = new List<Mesh>();
            
            foreach (var Registry in loadedRegistries.Values)
            {
                results.AddRange(Registry.GetAllMeshes());
            }

            return results;
        }

        /// <summary>
        /// Get all audio clips across all loaded Registries.
        /// </summary>
        public List<AudioClip> GetAllAudioClips()
        {
            var results = new List<AudioClip>();
            
            foreach (var Registry in loadedRegistries.Values)
            {
                results.AddRange(Registry.GetAllAudioClips());
            }

            return results;
        }

        /// <summary>
        /// Check if a item UID exists in any loaded Registry.
        /// </summary>
        public bool HasItem(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return false;
            if (uid.Contains("/"))
            {
                if (TryParseCompositeKey(uid, out var regName, out var path))
                {
                    var key = MakeCompositeKey(regName, path);
                    if (overrideItemCache.ContainsKey(key)) return true;
                    return globalItemCache.ContainsKey(key) || (loadedRegistries.TryGetValue(regName, out var reg) && reg.HasItem(path));
                }
                return false;
            }

            foreach (var reg in loadedRegistries.Values)
            {
                if (reg.HasItem(uid)) return true;
            }
            return false;
        }

        /// <summary>
        /// Get all loaded Registries.
        /// </summary>
        public Dictionary<string, Registry> GetAllRegistries()
        {
            return new Dictionary<string, Registry>(loadedRegistries);
        }



        /// <summary>
        /// Override layer: add/replace an item entry for a composite key.
        /// </summary>
        public void AddOverride(string compositeKey, ItemEntry entry)
        {
            if (string.IsNullOrWhiteSpace(compositeKey) || entry == null) return;
            if (!TryParseCompositeKey(compositeKey, out var r, out var p)) return;
            overrideItemCache[MakeCompositeKey(r, p)] = entry;
        }

        public void AddOverrides(Dictionary<string, ItemEntry> map)
        {
            if (map == null) return;
            foreach (var kv in map)
            {
                AddOverride(kv.Key, kv.Value);
            }
        }

        public bool RemoveOverride(string compositeKey)
        {
            if (string.IsNullOrWhiteSpace(compositeKey)) return false;
            if (!TryParseCompositeKey(compositeKey, out var r, out var p)) return false;
            return overrideItemCache.Remove(MakeCompositeKey(r, p));
        }

        public void ClearOverrides() => overrideItemCache.Clear();

        /// <summary>
        /// Get the total number of items across all Registries.
        /// </summary>
        public int GetTotalItemCount()
        {
            return globalItemCache.Count;
        }

        /// <summary>
        /// Compose the composite key for a given entry if it was registered; null if unknown.
        /// </summary>
        public string GetCompositeKeyForItem(ItemEntry entry)
        {
            if (entry == null) return null;
            return entryToCompositeKey.TryGetValue(entry, out var key) ? key : null;
        }

        /// <summary>
        /// Add or update a registry in a named bucket (runtime and editor).
        /// </summary>
        public void AddRegistryToBucket(string bucketName, Registry registry)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                Debug.LogError("[RegistryManager] Cannot add registry to bucket: bucket name is empty");
                return;
            }

            if (registry == null)
            {
                Debug.LogError($"[RegistryManager] Cannot add null registry to bucket '{bucketName}'");
                return;
            }

            if (namedBuckets == null)
                namedBuckets = new SerializableDictionary<string, Registry>();

            namedBuckets[bucketName] = registry;
            RegisterItem(registry);
            Debug.Log($"[RegistryManager] Added registry '{registry.name}' to bucket '{bucketName}'");
        }

        /// <summary>
        /// Remove a registry from a named bucket.
        /// </summary>
        public bool RemoveRegistryFromBucket(string bucketName)
        {
            if (namedBuckets == null || !namedBuckets.ContainsKey(bucketName))
                return false;

            var registry = namedBuckets[bucketName];
            namedBuckets.Remove(bucketName);

            if (registry != null)
                UnregisterRegistry(registry.name);

            Debug.Log($"[RegistryManager] Removed registry from bucket '{bucketName}'");
            return true;
        }

        /// <summary>
        /// Get a registry from a named bucket.
        /// </summary>
        public Registry GetRegistryFromBucket(string bucketName)
        {
            if (namedBuckets == null || !namedBuckets.TryGetValue(bucketName, out var registry))
                return null;
            return registry;
        }

        /// <summary>
        /// Get all bucket names.
        /// </summary>
        public List<string> GetAllBucketNames()
        {
            if (namedBuckets == null)
                return new List<string>();
            return new List<string>(namedBuckets.Keys);
        }

        /// <summary>
        /// Check if a bucket exists.
        /// </summary>
        public bool HasBucket(string bucketName)
        {
            return namedBuckets != null && namedBuckets.ContainsKey(bucketName);
        }


    }
}
