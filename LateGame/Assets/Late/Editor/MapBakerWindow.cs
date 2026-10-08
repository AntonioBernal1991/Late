using System.Collections.Generic;
using System.IO;
using Late.Generation;
using UnityEditor;
using UnityEngine;

namespace Late.EditorTools
{
    /// <summary>
    /// Única herramienta para guardar como prefab lo que genera <see cref="MapGenerator3D"/>.
    /// Hornea la selección: un módulo, varios, o el mapa completo si se selecciona MazeRoot,
    /// el generador o nada. El prefab resultante se puede editar cubo a cubo.
    /// </summary>
    public class MapBakerWindow : EditorWindow
    {
        private const string Title = "Hornear";
        private const string DefaultOutputFolder = "Assets/Late/Levels/Maps";

        private string _outputFolder = DefaultOutputFolder;

        [MenuItem("Late/Generador/Hornear")]
        public static void ShowWindow()
        {
            GetWindow<MapBakerWindow>(Title);
        }

        private void OnSelectionChange() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Guarda como prefab lo que hay seleccionado en la jerarquía.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            _outputFolder = EditorGUILayout.TextField("Carpeta de salida", _outputFolder);
            EditorGUILayout.Space();

            List<BakeTarget> targets = ResolveTargets(out string error);
            string summary = error ?? string.Join(", ", targets.ConvertAll(t => t.Name));
            EditorGUILayout.LabelField("Se horneará", summary, EditorStyles.wordWrappedLabel);

            using (new EditorGUI.DisabledScope(error != null))
            {
                if (GUILayout.Button("Hornear selección", GUILayout.Height(28)))
                {
                    Bake(targets);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "1. Entra en Play Mode y deja que se genere el laberinto.\n" +
                "2. Selecciona un módulo (Module_*) para guardar solo ese, o MazeRoot / MapGenerator " +
                "(o nada) para guardar el mapa completo.\n" +
                "3. Nunca sobrescribe: si el nombre ya existe, se añade un número.",
                MessageType.Info);
        }

        // ---------- Qué se hornea ----------

        private readonly struct BakeTarget
        {
            public readonly GameObject Source;
            public readonly string Name;
            public readonly MapGenerator3D Generator;

            public BakeTarget(GameObject source, string name, MapGenerator3D generator)
            {
                Source = source;
                Name = name;
                Generator = generator;
            }
        }

        private static List<BakeTarget> ResolveTargets(out string error)
        {
            var targets = new List<BakeTarget>();
            error = null;

            foreach (GameObject selected in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(selected)) continue; // assets del Project, no de la escena

                MapGenerator3D generator = selected.GetComponent<MapGenerator3D>();
                if (generator != null)
                {
                    if (generator.MazeRoot != null) targets.Add(MapTarget(generator));
                }
                else if (selected.name == MapGenerator3D.MazeRootName)
                {
                    MapGenerator3D owner = selected.GetComponentInParent<MapGenerator3D>();
                    targets.Add(new BakeTarget(selected, owner != null ? MapName(owner) : "GeneratedMap", owner));
                }
                else
                {
                    targets.Add(new BakeTarget(selected, selected.name, selected.GetComponentInParent<MapGenerator3D>()));
                }
            }

            if (targets.Count > 0) return targets;

            // Sin selección: el mapa completo del generador de la escena.
            MapGenerator3D sceneGenerator = FindObjectOfType<MapGenerator3D>();
            if (sceneGenerator == null || sceneGenerator.MazeRoot == null)
            {
                error = "Nada. Entra en Play Mode para generar el laberinto.";
                return targets;
            }

            targets.Add(MapTarget(sceneGenerator));
            return targets;
        }

        private static BakeTarget MapTarget(MapGenerator3D generator) =>
            new BakeTarget(generator.MazeRoot.gameObject, MapName(generator), generator);

        private static string MapName(MapGenerator3D generator) =>
            generator.Settings != null
                ? $"Map_seed{generator.Settings.Seed}_modules{generator.Settings.ModuleCount}"
                : "GeneratedMap";

        // ---------- Horneado ----------

        private void Bake(List<BakeTarget> targets)
        {
            bool stillGenerating = targets.Exists(t => t.Generator != null && !t.Generator.IsGenerationComplete);
            if (stillGenerating && !EditorUtility.DisplayDialog(Title,
                    "El laberinto todavía se está generando. ¿Hornear igualmente lo que hay?", "Hornear", "Cancelar"))
            {
                return;
            }

            string folder = EnsureFolder(_outputFolder);
            string lastPath = null;

            foreach (BakeTarget target in targets)
            {
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{MakeSafeFileName(target.Name)}.prefab");
                SavePrefab(target.Source, path, Path.GetFileNameWithoutExtension(path));
                lastPath = path;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Horneados {targets.Count} prefab(s) en '{folder}'.");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(lastPath));
        }

        /// <summary>Guarda una copia del objeto como prefab, sin tocar el original de la escena.</summary>
        private static void SavePrefab(GameObject source, string prefabPath, string prefabName)
        {
            GameObject clone = Instantiate(source);
            clone.name = prefabName;
            try
            {
                PrepareForPrefab(clone.transform);
                PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);
            }
            finally
            {
                DestroyImmediate(clone);
            }
        }

        /// <summary>
        /// Deja la copia lista para guardarla. El orden importa:
        /// 1. Los cubos que el camino quitó siguen colgando del módulo, desactivados: se borran.
        /// 2. Las mallas combinadas solo existen en memoria y en un prefab quedarían vacías: se borran.
        /// 3. El combinador ocultó los renderers de los cubos: se vuelven a mostrar.
        /// </summary>
        private static void PrepareForPrefab(Transform root)
        {
            RemoveChildren(root, child => !child.gameObject.activeSelf || child.name.Contains(MeshCombiner.CombinedSuffix));

            foreach (MeshRenderer meshRenderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (meshRenderer.gameObject.name == MeshCombiner.FloorColliderName) continue;
                meshRenderer.enabled = true;
            }
        }

        private static void RemoveChildren(Transform root, System.Func<Transform, bool> shouldRemove)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (shouldRemove(child))
                {
                    DestroyImmediate(child.gameObject);
                    continue;
                }
                RemoveChildren(child, shouldRemove);
            }
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        /// <summary>Crea la carpeta (y las intermedias) si no existe y devuelve la ruta normalizada.</summary>
        private static string EnsureFolder(string folderPath)
        {
            folderPath = folderPath.Replace("\\", "/").TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath)) return folderPath;

            if (!folderPath.StartsWith("Assets/"))
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
