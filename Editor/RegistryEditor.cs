using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Core.Registry.Editor
{
    [CustomEditor(typeof(Registry))]
    public class RegistryEditor : Sirenix.OdinInspector.Editor.OdinEditor
    {
        private bool importFoldout = true;
        private bool importRecursive = false;
        private DefaultAsset folderToImport;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space(10);
            DrawImportSection();
        }

        private void DrawImportSection()
        {
            Registry registry = (Registry)target;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            importFoldout = EditorGUILayout.Foldout(importFoldout, "Import Assets from Folder", true, EditorStyles.foldoutHeader);
            
            if (importFoldout)
            {
                EditorGUILayout.Space(5);
                
                // Recursive option
                importRecursive = EditorGUILayout.Toggle("Recursive (Include Subfolders)", importRecursive);
                
                EditorGUILayout.Space(5);
                
                // Folder drag-and-drop area
                EditorGUILayout.LabelField("Drag Folder Here:", EditorStyles.boldLabel);
                
                Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
                GUI.Box(dropArea, "Drop Folder to Import Assets", EditorStyles.helpBox);
                
                // Handle folder field
                EditorGUI.BeginChangeCheck();
                folderToImport = (DefaultAsset)EditorGUI.ObjectField(
                    new Rect(dropArea.x + 5, dropArea.y + 15, dropArea.width - 10, 20),
                    folderToImport,
                    typeof(DefaultAsset),
                    false
                );
                
                // Handle drag and drop
                HandleDragAndDrop(dropArea, registry);
                
                if (EditorGUI.EndChangeCheck() && folderToImport != null)
                {
                    string folderPath = AssetDatabase.GetAssetPath(folderToImport);
                    if (AssetDatabase.IsValidFolder(folderPath))
                    {
                        ImportAssetsFromFolder(registry, folderPath);
                        folderToImport = null;
                    }
                    else
                    {
                        Debug.LogWarning("[RegistryEditor] Selected object is not a folder.");
                        folderToImport = null;
                    }
                }
                
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    $"Import all {registry.AssetType} assets from a folder.\n" +
                    "UIDs will be generated from file names.\n" +
                    (importRecursive ? "Subfolder paths will be prefixed to UIDs (e.g., 'Subfolder/AssetName')." : "Only assets in the root folder will be imported."),
                    MessageType.Info
                );
            }
            
            EditorGUILayout.EndVertical();
        }

        private void HandleDragAndDrop(Rect dropArea, Registry registry)
        {
            Event evt = Event.current;
            
            if (dropArea.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        
                        foreach (Object draggedObject in DragAndDrop.objectReferences)
                        {
                            string path = AssetDatabase.GetAssetPath(draggedObject);
                            if (AssetDatabase.IsValidFolder(path))
                            {
                                ImportAssetsFromFolder(registry, path);
                            }
                            else
                            {
                                Debug.LogWarning($"[RegistryEditor] '{draggedObject.name}' is not a folder.");
                            }
                        }
                    }
                    
                    evt.Use();
                }
            }
        }

        private void ImportAssetsFromFolder(Registry registry, string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogError($"[RegistryEditor] Invalid folder path: {folderPath}");
                return;
            }

            List<string> assetPaths = new List<string>();
            
            // Search for assets based on registry type
            string searchPattern = GetSearchPatternForAssetType(registry.AssetType);
            
            if (importRecursive)
            {
                // Recursively find all matching assets
                assetPaths = AssetDatabase.FindAssets(searchPattern, new[] { folderPath })
                    .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                    .Where(path => IsValidAssetForRegistry(path, registry.AssetType))
                    .ToList();
            }
            else
            {
                // Only get assets in the root folder (not subfolders)
                assetPaths = AssetDatabase.FindAssets(searchPattern, new[] { folderPath })
                    .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                    .Where(path => 
                    {
                        string directory = Path.GetDirectoryName(path).Replace('\\', '/');
                        return directory == folderPath && IsValidAssetForRegistry(path, registry.AssetType);
                    })
                    .ToList();
            }

            if (assetPaths.Count == 0)
            {
                Debug.LogWarning($"[RegistryEditor] No matching {registry.AssetType} assets found in '{folderPath}'{(importRecursive ? " (recursive)" : "")}.");
                return;
            }

            int importedCount = 0;
            int skippedCount = 0;

            Undo.RecordObject(registry, "Import Assets to Registry");

            foreach (string assetPath in assetPaths)
            {
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (asset == null)
                    continue;

                // Generate UID from path
                string uid = GenerateUIDFromPath(assetPath, folderPath);

                // Check if UID already exists
                if (registry.HasItem(uid))
                {
                    Debug.LogWarning($"[RegistryEditor] Skipping '{asset.name}' - UID '{uid}' already exists.");
                    skippedCount++;
                    continue;
                }

                // Create new entry
                ItemEntry newEntry = new ItemEntry
                {
                    uid = uid,
                    asset = asset,
                    description = $"Imported from {assetPath}",
                    tags = new List<string>(),
                    metadata = new SerializableDictionary<string, string>()
                };

                registry.AddItem(newEntry);
                importedCount++;
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            Debug.Log($"[RegistryEditor] Import complete: {importedCount} assets imported, {skippedCount} skipped (duplicates).");
        }

        private string GenerateUIDFromPath(string assetPath, string baseFolderPath)
        {
            // Normalize paths
            assetPath = assetPath.Replace('\\', '/');
            baseFolderPath = baseFolderPath.Replace('\\', '/');
            
            // Get relative path from base folder
            string relativePath = assetPath.Substring(baseFolderPath.Length + 1);
            
            // Remove file extension
            relativePath = Path.ChangeExtension(relativePath, null);
            
            if (importRecursive)
            {
                // Keep subfolder structure in UID (e.g., "Subfolder/AssetName")
                return relativePath.Replace('\\', '/');
            }
            else
            {
                // Only use filename as UID
                return Path.GetFileNameWithoutExtension(assetPath);
            }
        }

        private string GetSearchPatternForAssetType(RegistryAssetType assetType)
        {
            switch (assetType)
            {
                case RegistryAssetType.Prefab:
                    return "t:Prefab";
                case RegistryAssetType.Texture:
                    return "t:Texture2D";
                case RegistryAssetType.Material:
                    return "t:Material";
                case RegistryAssetType.Mesh:
                    return "t:Mesh";
                case RegistryAssetType.Audio:
                    return "t:AudioClip";
                default:
                    return "";
            }
        }

        private bool IsValidAssetForRegistry(string path, RegistryAssetType assetType)
        {
            string extension = Path.GetExtension(path).ToLower();
            
            switch (assetType)
            {
                case RegistryAssetType.Prefab:
                    return extension == ".prefab";
                case RegistryAssetType.Texture:
                    return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || 
                           extension == ".tga" || extension == ".psd" || extension == ".bmp";
                case RegistryAssetType.Material:
                    return extension == ".mat";
                case RegistryAssetType.Mesh:
                    return extension == ".fbx" || extension == ".obj" || extension == ".asset";
                case RegistryAssetType.Audio:
                    return extension == ".mp3" || extension == ".wav" || extension == ".ogg" || extension == ".aiff";
                default:
                    return false;
            }
        }
    }
}
