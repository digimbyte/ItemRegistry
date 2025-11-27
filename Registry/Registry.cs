using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting;
using UnityEngine.Scripting.APIUpdating;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System;

namespace Core.Registry
{
    /// <summary>
    /// Asset Registry that manages a collection of asset entries organized by UID/path.
    /// Each registry is TYPE-LOCKED to only store one type of asset (prefab, texture, material, mesh, or audio).
    /// </summary>
    [CreateAssetMenu(fileName = "NewRegistry", menuName = "Core/Registry")]
    public class Registry : ScriptableObject
    {
        [SerializeField]
        [LabelText("Asset Type")]
        [InfoBox("Type of assets this registry will store - CANNOT BE CHANGED after adding items")]
        [OnValueChanged("OnAssetTypeChanged")]
        private RegistryAssetType assetType = RegistryAssetType.Prefab;

        [SerializeField]
        [LabelText("Default/Fallback Asset")]
        [InfoBox("REQUIRED: Asset to return when requested UID is not found. Must match the registry's asset type.")]
        [ValidateInput("@defaultAsset != null", "Default asset must be assigned")]
        [AssetsOnly]
        [PreviewField(55)]
        private UnityEngine.Object defaultAsset;

        [SerializeField]
        [LabelText("Description")]
        [TextArea(2, 4)]
        private string description = "Registry for managing asset references.";

        [SerializeField]
        [LabelText("Items")]
        [FormerlySerializedAs("tileEntries")]
        [ListDrawerSettings(ShowIndexLabels = false, DraggableItems = true)]
        private List<ItemEntry> itemEntries = new List<ItemEntry>();

        // Runtime lookup cache
        [NonSerialized]
        private Dictionary<string, ItemEntry> itemCache;

        [NonSerialized]
        private bool isCacheValid = false;

        public string Description => description;
        public int ItemCount => itemEntries.Count;
        public RegistryAssetType AssetType => assetType;
        public UnityEngine.Object DefaultAsset => defaultAsset;

        /// <summary>
        /// Get an item entry by its UID. Returns null if not found.
        /// </summary>
        public ItemEntry GetItemByUID(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return null;

            BuildCache();

            if (itemCache.TryGetValue(uid, out ItemEntry entry))
                return entry;

            Debug.LogWarning($"[{assetType}Registry] Item UID '{uid}' not found, returning default asset");
            return null;
        }

        /// <summary>
        /// Get the raw asset object by its UID. Returns default asset if not found.
        /// </summary>
        public UnityEngine.Object GetAssetByUID(string uid)
        {
            ItemEntry entry = GetItemByUID(uid);
            return entry?.asset ?? defaultAsset;
        }

        /// <summary>
        /// Get a prefab by its UID. Returns default prefab if not found.
        /// </summary>
        public GameObject GetPrefabByUID(string uid)
        {
            ItemEntry entry = GetItemByUID(uid);
            return (entry?.asset as GameObject) ?? (defaultAsset as GameObject);
        }

        /// <summary>
        /// Get a texture by its UID. Returns default texture if not found.
        /// </summary>
        public Texture GetTextureByUID(string uid)
        {
            ItemEntry entry = GetItemByUID(uid);
            return (entry?.asset as Texture) ?? (defaultAsset as Texture);
        }

        /// <summary>
        /// Get a material by its UID. Returns default material if not found.
        /// </summary>
        public Material GetMaterialByUID(string uid)
        {
            ItemEntry entry = GetItemByUID(uid);
            return (entry?.asset as Material) ?? (defaultAsset as Material);
        }

        /// <summary>
        /// Get a mesh by its UID. Returns default mesh if not found.
        /// </summary>
        public Mesh GetMeshByUID(string uid)
        {
            ItemEntry entry = GetItemByUID(uid);
            return (entry?.asset as Mesh) ?? (defaultAsset as Mesh);
        }

        /// <summary>
        /// Get an audio clip by its UID. Returns default audio if not found.
        /// </summary>
        public AudioClip GetAudioByUID(string uid)
        {
            ItemEntry entry = GetItemByUID(uid);
            return (entry?.asset as AudioClip) ?? (defaultAsset as AudioClip);
        }

        /// <summary>
        /// Get a typed asset by its UID. Returns default asset if not found.
        /// </summary>
        public T GetAssetByUID<T>(string uid) where T : UnityEngine.Object
        {
            ItemEntry entry = GetItemByUID(uid);
            return (entry?.asset as T) ?? (defaultAsset as T);
        }



        /// <summary>
        /// Get all prefabs in this registry.
        /// </summary>
        public List<GameObject> GetAllPrefabs()
        {
            List<GameObject> results = new List<GameObject>();
            foreach (var entry in itemEntries)
            {
                if (entry.asset is GameObject go)
                    results.Add(go);
            }
            return results;
        }

        /// <summary>
        /// Get all textures in this registry.
        /// </summary>
        public List<Texture> GetAllTextures()
        {
            List<Texture> results = new List<Texture>();
            foreach (var entry in itemEntries)
            {
                if (entry.asset is Texture tex)
                    results.Add(tex);
            }
            return results;
        }

        /// <summary>
        /// Get all materials in this registry.
        /// </summary>
        public List<Material> GetAllMaterials()
        {
            List<Material> results = new List<Material>();
            foreach (var entry in itemEntries)
            {
                if (entry.asset is Material mat)
                    results.Add(mat);
            }
            return results;
        }

        /// <summary>
        /// Get all meshes in this registry.
        /// </summary>
        public List<Mesh> GetAllMeshes()
        {
            List<Mesh> results = new List<Mesh>();
            foreach (var entry in itemEntries)
            {
                if (entry.asset is Mesh mesh)
                    results.Add(mesh);
            }
            return results;
        }

        /// <summary>
        /// Get all audio clips in this registry.
        /// </summary>
        public List<AudioClip> GetAllAudioClips()
        {
            List<AudioClip> results = new List<AudioClip>();
            foreach (var entry in itemEntries)
            {
                if (entry.asset is AudioClip clip)
                    results.Add(clip);
            }
            return results;
        }

        /// <summary>
        /// Get all tiles that match a tag.
        /// </summary>
        public List<ItemEntry> GetItemsByTag(string tag)
        {
            List<ItemEntry> results = new List<ItemEntry>();
            foreach (var entry in itemEntries)
            {
                if (entry.tags != null && entry.tags.Contains(tag))
                    results.Add(entry);
            }
            return results;
        }

        /// <summary>
        /// Check if a tile with the given UID exists.
        /// </summary>
        public bool HasItem(string uid)
        {
            return GetItemByUID(uid) != null;
        }

        /// <summary>
        /// Add a new item entry to this Registry.
        /// </summary>
        public void AddItem(ItemEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.uid))
            {
                Debug.LogWarning("[ItemRegistry] Cannot add item entry: entry is null or has empty UID");
                return;
            }

            // Validate asset type matches registry type
            if (!ValidateAssetType(entry.asset))
            {
                Debug.LogError($"[ItemRegistry] Cannot add item '{entry.uid}': asset type mismatch. Registry expects {assetType}");
                return;
            }

            // Check for duplicate UIDs
            if (GetItemByUID(entry.uid) != null)
            {
                Debug.LogWarning($"[ItemRegistry] Item with UID '{entry.uid}' already exists in registry");
                return;
            }

            itemEntries.Add(entry);
            isCacheValid = false;
        }

        /// <summary>
        /// Remove a tile entry by UID.
        /// </summary>
        public bool RemoveItem(string uid)
        {
            ItemEntry entry = GetItemByUID(uid);
            if (entry != null)
            {
                itemEntries.Remove(entry);
                isCacheValid = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get all tile entries.
        /// </summary>
        public List<ItemEntry> GetAllItems()
        {
            return new List<ItemEntry>(itemEntries);
        }

        /// <summary>
        /// Rebuild the lookup cache. Called automatically when needed.
        /// </summary>
        private void BuildCache()
        {
            if (isCacheValid && itemCache != null)
                return;

            itemCache = new Dictionary<string, ItemEntry>();

            foreach (var entry in itemEntries)
            {
                if (!string.IsNullOrEmpty(entry.uid))
                {
                    if (itemCache.ContainsKey(entry.uid))
                    {
                        Debug.LogWarning($"[ItemRegistry] Duplicate item UID '{entry.uid}' in registry");
                    }
                    else
                    {
                        itemCache[entry.uid] = entry;
                    }
                }
            }

            isCacheValid = true;
        }

        /// <summary>
        /// Clear the runtime cache (called by editor when data changes).
        /// </summary>
        public void InvalidateCache()
        {
            isCacheValid = false;
            itemCache = null;
        }

#if UNITY_EDITOR
        [Button("Validate Registry")]
        [BoxGroup("Validation")]
        private void ValidateRegistry()
        {
            int duplicates = 0;
            int invalidAssets = 0;
            HashSet<string> seenUIDs = new HashSet<string>();
            string[] validAssetExtensions = { ".prefab", ".png", ".jpg", ".jpeg", ".tga", ".mat", ".mp3", ".wav", ".ogg", ".aiff" };

            foreach (var entry in itemEntries)
            {
                if (string.IsNullOrEmpty(entry.uid))
                {
                    Debug.LogWarning($"[ItemRegistry] Found entry with empty UID");
                }
                else if (seenUIDs.Contains(entry.uid))
                {
                    Debug.LogWarning($"[ItemRegistry] Duplicate UID found: '{entry.uid}'");
                    duplicates++;
                }
                else
                {
                    seenUIDs.Add(entry.uid);
                }

                if (entry.asset == null)
                {
                    Debug.LogWarning($"[ItemRegistry] Entry '{entry.uid}' has no asset assigned");
                    invalidAssets++;
                }
                else
                {
                    // Validate asset type
                    string assetPath = UnityEditor.AssetDatabase.GetAssetPath(entry.asset);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        bool validAsset = false;
                        foreach (string ext in validAssetExtensions)
                        {
                            if (assetPath.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase))
                            {
                                validAsset = true;
                                break;
                            }
                        }

                        if (!validAsset)
                        {
                            Debug.LogWarning($"[ItemRegistry] Entry '{entry.uid}' has invalid asset type: {assetPath}");
                            invalidAssets++;
                        }
                    }
                }
            }

            Debug.Log($"[{assetType}Registry] Validation complete: {itemEntries.Count} entries, {duplicates} duplicates, {invalidAssets} invalid assets");
        }

        [Button("Clear All Items")]
        [BoxGroup("Validation")]
        private void ClearAllItems()
        {
            if (UnityEditor.EditorUtility.DisplayDialog(
                "Clear All Items",
                $"Are you sure you want to remove all {itemEntries.Count} items from this Registry?",
                "Yes", "No"))
            {
                itemEntries.Clear();
                InvalidateCache();
                Debug.Log($"[{assetType}Registry] All items cleared");
            }
        }
#endif

        private void OnValidate()
        {
            InvalidateCache();
            ValidateAllAssetTypes();
            
            // Validate default asset matches registry type
            if (defaultAsset != null && !ValidateAssetType(defaultAsset))
            {
                Debug.LogError($"[{assetType}Registry] Default asset type mismatch! Expected {assetType}, got {defaultAsset.GetType().Name}");
            }
        }

        /// <summary>
        /// Validate that an asset matches the registry's type.
        /// </summary>
        private bool ValidateAssetType(UnityEngine.Object asset)
        {
            if (asset == null) return false;

            switch (assetType)
            {
                case RegistryAssetType.Prefab:
                    return asset is GameObject;
                case RegistryAssetType.Texture:
                    return asset is Texture || asset is Texture2D;
                case RegistryAssetType.Material:
                    return asset is Material;
                case RegistryAssetType.Mesh:
                    return asset is Mesh;
                case RegistryAssetType.Audio:
                    return asset is AudioClip;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Validate all existing assets match the registry type.
        /// </summary>
        private void ValidateAllAssetTypes()
        {
            foreach (var entry in itemEntries)
            {
                if (entry.asset != null && !ValidateAssetType(entry.asset))
                {
                    Debug.LogWarning($"[ItemRegistry] Asset type mismatch in '{entry.uid}': expected {assetType}, got {entry.asset.GetType().Name}");
                }
            }
        }

#if UNITY_EDITOR
        private void OnAssetTypeChanged()
        {
            if (itemEntries.Count > 0)
            {
                Debug.LogWarning($"[ItemRegistry] Changing asset type with existing items! Validate all assets match the new type.");
                ValidateAllAssetTypes();
            }
        }
#endif
    }

    /// <summary>
    /// A single item entry in the Registry.
    /// </summary>
    [System.Serializable]
    [MovedFrom(true, "Core.Registry", null, "TileEntry")]
    public class ItemEntry
    {
        [LabelText("UID")]
        [ValidateInput("@!string.IsNullOrEmpty(uid)", "UID cannot be empty")]
        public string uid = "item_uid";


        [BoxGroup("Asset")]
        [LabelText("Asset Reference")]
        [ValidateInput("@asset != null", "Asset must be assigned")]
        [AssetsOnly]
        [PreviewField(55, ObjectFieldAlignment.Left)]
        public UnityEngine.Object asset;

        [BoxGroup("Properties")]
        [LabelText("Tags")]
        [ValidateInput("@tags != null", "Tags list must not be null")]
        public List<string> tags = new List<string>();

        [BoxGroup("Properties")]
        [LabelText("Description")]
        [TextArea(2, 3)]
        public string description = "";

        [BoxGroup("Properties")]
        [LabelText("Custom Metadata")]
        [DictionaryDrawerSettings(KeyLabel = "Key", ValueLabel = "Value")]
        public SerializableDictionary<string, string> metadata = new SerializableDictionary<string, string>();

        /// <summary>
        /// Get a custom metadata value by key.
        /// </summary>
        public string GetMetadata(string key, string defaultValue = "")
        {
            return metadata != null && metadata.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Check if this item has a specific tag.
        /// </summary>
        public bool HasTag(string tag)
        {
            return tags != null && tags.Contains(tag);
        }
    }



    /// <summary>
    /// Serializable dictionary for custom metadata storage.
    /// </summary>
    /// <summary>
    /// Types of assets that a registry can be locked to.
    /// Each registry can ONLY store one type.
    /// </summary>
    public enum RegistryAssetType
    {
        Prefab,
        Texture,
        Material,
        Mesh,
        Audio
    }

    /// <summary>
    /// Serializable dictionary for custom metadata storage.
    /// </summary>
    [System.Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> keys = new List<TKey>();

        [SerializeField]
        private List<TValue> values = new List<TValue>();

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();

            foreach (var pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();

            if (keys.Count != values.Count)
            {
                Debug.LogError($"Key count ({keys.Count}) does not match value count ({values.Count})");
                return;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                Add(keys[i], values[i]);
            }
        }
    }
}
