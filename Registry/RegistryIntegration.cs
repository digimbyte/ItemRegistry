using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;

namespace Core.Registry
{
    /// <summary>
    /// Runtime utility layer for asset lifecycle management.
    /// Provides object pooling, instantiation, memory management, and asset streaming.
    /// Integrates with RegistryManager to optimize asset usage at runtime.
    /// </summary>
    public class RegistryIntegration : MonoBehaviour
    {
        [SerializeField]
        [LabelText("Registry Manager")]
        [InfoBox("Reference to the RegistryManager singleton")]
        private RegistryManager RegistryManager;

        [SerializeField]
        [LabelText("Spawned Objects Parent")]
        [InfoBox("Parent transform for all spawned objects")]
        private Transform spawnParent;

        [SerializeField]
        [LabelText("Use Object Pooling")]
        [InfoBox("Pool instances for performance optimization")]
        private bool usePooling = true;

        [SerializeField]
        [LabelText("Max Pool Size Per Asset")]
        [ShowIf("usePooling")]
        [MinValue(10)]
        private int maxPoolSizePerAsset = 100;

        [SerializeField]
        [LabelText("Enable Memory Tracking")]
        [InfoBox("Track instantiated objects for memory profiling")]
        private bool enableMemoryTracking = false;

        [SerializeField]
        [LabelText("Auto-Cleanup Threshold")]
        [ShowIf("enableMemoryTracking")]
        [MinValue(100)]
        [InfoBox("Destroy pooled objects when total count exceeds threshold")]
        private int autoCleanupThreshold = 500;

        // Pooling system
        private Dictionary<string, Queue<GameObject>> objectPools = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<GameObject, string> pooledObjectUIDs = new Dictionary<GameObject, string>();
        
        // Memory tracking
        private Dictionary<string, int> activeInstanceCounts = new Dictionary<string, int>();
        private int totalPooledObjects = 0;

        public static RegistryIntegration Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (RegistryManager == null)
                RegistryManager = RegistryManager.Instance;

            if (spawnParent == null)
                spawnParent = transform;
        }


        /// <summary>
        /// Spawn a prefab at a world position by UID with pooling support.
        /// </summary>
        public GameObject Spawn(string uid, Vector3 position, Quaternion rotation = default)
        {
            var itemEntry = RegistryManager.GetItemByUID(uid);
            if (itemEntry == null)
            {
                Debug.LogWarning($"[RegistryIntegration] Asset '{uid}' not found");
                return null;
            }

            GameObject instance;

            // Try to get from pool
            if (usePooling && objectPools.TryGetValue(uid, out var pool) && pool.Count > 0)
            {
                instance = pool.Dequeue();
                totalPooledObjects--;
                instance.SetActive(true);
                instance.transform.position = position;
                instance.transform.rotation = rotation;
            }
            else
            {
                // Validate asset is a prefab
                if (itemEntry.asset is GameObject prefab)
                {
                    instance = Instantiate(
                        prefab,
                        position,
                        rotation,
                        spawnParent
                    );
                    instance.name = $"{uid}_instance";
                }
                else
                {
                    Debug.LogError($"[RegistryIntegration] Asset '{uid}' is not a GameObject prefab");
                    return null;
                }
            }

            // Track instance
            if (usePooling)
                pooledObjectUIDs[instance] = uid;
                
            if (enableMemoryTracking)
            {
                if (!activeInstanceCounts.ContainsKey(uid))
                    activeInstanceCounts[uid] = 0;
                activeInstanceCounts[uid]++;
            }

            return instance;
        }

        /// <summary>
        /// Despawn an object and return it to pool if pooling is enabled.
        /// </summary>
        public void Despawn(GameObject instance)
        {
            if (instance == null)
                return;

            if (usePooling && pooledObjectUIDs.TryGetValue(instance, out var uid))
            {
                if (!objectPools.ContainsKey(uid))
                    objectPools[uid] = new Queue<GameObject>();

                if (objectPools[uid].Count < maxPoolSizePerAsset)
                {
                    instance.SetActive(false);
                    instance.transform.SetParent(spawnParent);
                    objectPools[uid].Enqueue(instance);
                    totalPooledObjects++;
                    
                    // Track memory
                    if (enableMemoryTracking && activeInstanceCounts.ContainsKey(uid))
                        activeInstanceCounts[uid]--;
                    
                    // Auto cleanup if needed
                    if (enableMemoryTracking && totalPooledObjects > autoCleanupThreshold)
                        AutoCleanup();
                    
                    return;
                }
            }

            // Destroy if pool is full or pooling disabled
            Destroy(instance);
            pooledObjectUIDs.Remove(instance);
            
            if (enableMemoryTracking && pooledObjectUIDs.TryGetValue(instance, out var trackUid))
            {
                if (activeInstanceCounts.ContainsKey(trackUid))
                    activeInstanceCounts[trackUid]--;
            }
        }


        /// <summary>
        /// Get a random asset from a specific registry.
        /// </summary>
        public T GetRandomAsset<T>(string registryName) where T : UnityEngine.Object
        {
            var registry = RegistryManager.GetRegistry(registryName);
            if (registry == null)
            {
                Debug.LogWarning($"[RegistryIntegration] Registry '{registryName}' not found");
                return null;
            }

            var items = registry.GetAllItems();
            if (items.Count == 0)
                return null;

            return items[Random.Range(0, items.Count)].asset as T;
        }

        /// <summary>
        /// Pre-warm the pool for a specific asset.
        /// </summary>
        public void PreWarmPool(string uid, int count)
        {
            if (!usePooling)
                return;

            var prefab = RegistryManager.GetPrefabByUID(uid);
            if (prefab == null)
            {
                Debug.LogWarning($"[RegistryIntegration] Cannot pre-warm pool: '{uid}' not found or is not a prefab");
                return;
            }

            if (!objectPools.ContainsKey(uid))
                objectPools[uid] = new Queue<GameObject>();

            var pool = objectPools[uid];

            for (int i = pool.Count; i < count; i++)
            {
                var instance = Instantiate(prefab, spawnParent);
                instance.name = $"{uid}_pooled_{i}";
                instance.SetActive(false);
                pool.Enqueue(instance);
                pooledObjectUIDs[instance] = uid;
                totalPooledObjects++;
            }

            Debug.Log($"[RegistryIntegration] Pre-warmed pool for '{uid}': {count} instances");
        }

        /// <summary>
        /// Pre-warm pools for all prefabs in a registry.
        /// </summary>
        public void PreWarmPoolForRegistry(string registryName, int countPerAsset)
        {
            var registry = RegistryManager.GetRegistry(registryName);
            if (registry == null)
            {
                Debug.LogWarning($"[RegistryIntegration] Registry '{registryName}' not found");
                return;
            }

            if (registry.AssetType != RegistryAssetType.Prefab)
            {
                Debug.LogWarning($"[RegistryIntegration] Registry '{registryName}' is not a prefab registry");
                return;
            }

            foreach (var item in registry.GetAllItems())
            {
                PreWarmPool(item.uid, countPerAsset);
            }

            Debug.Log($"[RegistryIntegration] Pre-warmed pools for registry '{registryName}'");
        }

        /// <summary>
        /// Clear all pools and destroy pooled objects.
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var pool in objectPools.Values)
            {
                foreach (var instance in pool)
                {
                    if (instance != null)
                        Destroy(instance);
                }
                pool.Clear();
            }

            objectPools.Clear();
            pooledObjectUIDs.Clear();
            totalPooledObjects = 0;
            activeInstanceCounts.Clear();
            Debug.Log("[RegistryIntegration] All pools cleared");
        }

        /// <summary>
        /// Clear the pool for a specific asset.
        /// </summary>
        public void ClearPool(string uid)
        {
            if (!objectPools.ContainsKey(uid))
                return;

            var pool = objectPools[uid];
            foreach (var instance in pool)
            {
                if (instance != null)
                {
                    pooledObjectUIDs.Remove(instance);
                    Destroy(instance);
                    totalPooledObjects--;
                }
            }
            pool.Clear();
            objectPools.Remove(uid);
        }

        /// <summary>
        /// Auto-cleanup: destroy oldest pooled objects when threshold exceeded.
        /// </summary>
        private void AutoCleanup()
        {
            int toRemove = totalPooledObjects - (autoCleanupThreshold / 2);
            if (toRemove <= 0) return;

            int removed = 0;
            foreach (var kvp in objectPools.ToList())
            {
                var pool = kvp.Value;
                while (pool.Count > 0 && removed < toRemove)
                {
                    var instance = pool.Dequeue();
                    if (instance != null)
                    {
                        pooledObjectUIDs.Remove(instance);
                        Destroy(instance);
                        totalPooledObjects--;
                        removed++;
                    }
                }

                if (removed >= toRemove)
                    break;
            }

            Debug.Log($"[RegistryIntegration] Auto-cleanup: removed {removed} pooled objects");
        }

        /// <summary>
        /// Get the number of active instances for a specific asset.
        /// </summary>
        public int GetActiveInstanceCount(string uid)
        {
            return enableMemoryTracking && activeInstanceCounts.ContainsKey(uid) ? activeInstanceCounts[uid] : -1;
        }

        /// <summary>
        /// Get the number of pooled instances for a specific asset.
        /// </summary>
        public int GetPooledInstanceCount(string uid)
        {
            return objectPools.ContainsKey(uid) ? objectPools[uid].Count : 0;
        }

        /// <summary>
        /// Get total number of pooled objects across all assets.
        /// </summary>
        public int GetTotalPooledCount()
        {
            return totalPooledObjects;
        }

        /// <summary>
        /// Get total number of unique asset pools.
        /// </summary>
        public int GetPoolCount()
        {
            return objectPools.Count;
        }
    }
}
