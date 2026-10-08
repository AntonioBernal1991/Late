using System.IO;
using Late.Generation;
using UnityEditor;
using UnityEngine;

namespace Late.EditorTools
{
    /// <summary>
    /// Guarda como prefabs los módulos o el mapa completo que ha generado <see cref="MapGenerator3D"/>.
    /// También exporta automáticamente al terminar la generación si el generador lo tiene activado.
    /// </summary>
    [InitializeOnLoad]
    public static class MapPrefabExporter
    {
        public const string ModulePrefix = "Module_";

        static MapPrefabExporter()
        {
            MapGenerator3D.GenerationCompleted -= OnGenerationCompleted;
            MapGenerator3D.GenerationCompleted += OnGenerationCompleted;
        }

        private static void OnGenerationCompleted(MapGenerator3D generator)
        {
            if (generator.AutoExportModulePrefabsAfterGeneration)
            {
                ExportModules(generator);
            }
            if (generator.AutoExportMapPrefabAfterGeneration)
            {
                ExportFullMap(generator);
            }
        }

        /// <summary>Guarda cada módulo (MazeRoot/Module_*) como un prefab.</summary>
        public static int ExportModules(MapGenerator3D generator)
        {
            return ExportModules(generator.MazeRoot, generator.ModulePrefabExportFolder, generator.ExportStripInactiveChildren);
        }

        public static int ExportModules(Transform mazeRoot, string folder, bool stripInactiveChildren)
        {
            if (mazeRoot == null || string.IsNullOrWhiteSpace(folder)) return 0;

            folder = EnsureFolder(folder);

            int exported = 0;
            foreach (Transform child in mazeRoot)
            {
                if (!child.name.StartsWith(ModulePrefix)) continue;

                SavePrefab(child.gameObject, $"{folder}/{MakeSafeFileName(child.name)}.prefab", stripInactiveChildren);
                exported++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Exportados {exported} módulos a '{folder}'.");
            return exported;
        }

        /// <summary>
        /// Guarda todo MazeRoot como un único prefab llamado Map_seed{semilla}_modules{n}.
        /// Nunca sobrescribe: si el nombre existe, Unity añade un número.
        /// </summary>
        public static string ExportFullMap(MapGenerator3D generator)
        {
            if (generator.MazeRoot == null || string.IsNullOrWhiteSpace(generator.MapPrefabExportFolder)) return null;

            string folder = EnsureFolder(generator.MapPrefabExportFolder);

            MapSettings settings = generator.Settings;
            string baseName = MakeSafeFileName($"Map_seed{settings.Seed}_modules{settings.ModuleCount}");
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.prefab");

            GameObject clone = Object.Instantiate(generator.MazeRoot.gameObject);
            clone.name = "GeneratedMap";
            try
            {
                if (generator.MapExportRemoveCombinedMeshes)
                {
                    RemoveCombinedMeshes(clone.transform);
                }
                if (generator.MapExportEnableAllCubeRenderers)
                {
                    EnableCubeRenderers(clone.transform);
                }

                PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(clone);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"Mapa exportado a '{prefabPath}'.");
            return prefabPath;
        }

        /// <summary>Guarda una copia del objeto como prefab, sin tocar el original de la escena.</summary>
        public static void SavePrefab(GameObject source, string prefabPath, bool stripInactiveChildren)
        {
            GameObject clone = Object.Instantiate(source);
            clone.name = source.name;
            try
            {
                if (stripInactiveChildren)
                {
                    StripInactiveChildren(clone.transform);
                }
                PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        // Los cubos que el camino "quita" vuelven desactivados al pool, pero siguen colgando del módulo.
        private static void StripInactiveChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    Object.DestroyImmediate(child.gameObject);
                    continue;
                }
                StripInactiveChildren(child);
            }
        }

        private static void RemoveCombinedMeshes(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child.name.Contains(MeshCombiner.CombinedSuffix))
                {
                    Object.DestroyImmediate(child.gameObject);
                    continue;
                }
                RemoveCombinedMeshes(child);
            }
        }

        private static void EnableCubeRenderers(Transform root)
        {
            foreach (MeshRenderer meshRenderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (meshRenderer.gameObject.name == MeshCombiner.FloorColliderName) continue;

                meshRenderer.enabled = true;
                if (!meshRenderer.gameObject.activeSelf) meshRenderer.gameObject.SetActive(true);
            }
        }

        public static string MakeSafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        /// <summary>
        /// Crea la carpeta (y las intermedias) si no existe y devuelve la ruta normalizada.
        /// Tiene que estar dentro de Assets/.
        /// </summary>
        public static string EnsureFolder(string folderPath)
        {
            folderPath = folderPath.Replace("\\", "/").TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath)) return folderPath;

            if (folderPath != "Assets" && !folderPath.StartsWith("Assets/"))
            {
                throw new System.ArgumentException($"La carpeta debe estar dentro de Assets/: '{folderPath}'.");
            }

            string[] parts = folderPath.Split('/');
            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
            return folderPath;
        }
    }
}
