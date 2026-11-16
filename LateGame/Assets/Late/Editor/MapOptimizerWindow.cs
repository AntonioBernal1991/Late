using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Late.EditorTools
{
    /// <summary>
    /// Ventana para optimizar los niveles ya ajustados a mano (R03F02).
    /// El prefab original no se toca: sigue siendo el que se edita cubo a cubo. La versión optimizada
    /// se guarda al lado como <c>Nombre_Opt.prefab</c>, con sus mallas en <c>Nombre_Opt_Meshes.asset</c>,
    /// y es la que se coloca en la escena del nivel. Al volver a optimizar se sobrescribe y conserva su
    /// GUID, así que la escena sigue apuntando a ella.
    /// </summary>
    public class MapOptimizerWindow : EditorWindow
    {
        private const string Title = "Optimizar";

        private MapOptimizer.Options _options = MapOptimizer.Options.Default;
        private readonly List<Report> _reports = new List<Report>();
        private Vector2 _scroll;

        public struct Report
        {
            public string SourcePath;
            public string OutputPath;
            public MapOptimizer.Result Result;
            public long SourceBytes;
            public long OutputBytes;
        }

        [MenuItem("Late/Generador/Optimizar")]
        public static void ShowWindow()
        {
            GetWindow<MapOptimizerWindow>(Title);
        }

        private void OnSelectionChange() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Crea una copia optimizada de los prefabs de nivel seleccionados en el Project. " +
                "Se ve y se juega igual: solo cambia cómo se dibuja.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            _options.RemoveDuplicates = EditorGUILayout.ToggleLeft("Quitar cubos duplicados", _options.RemoveDuplicates);
            _options.RemoveBottomFaces = EditorGUILayout.ToggleLeft(
                "Quitar caras inferiores (la cámara nunca baja del suelo)", _options.RemoveBottomFaces);
            EditorGUILayout.Space();

            List<string> paths = SelectedLevelPrefabs();
            EditorGUILayout.LabelField("Se optimizará",
                paths.Count == 0 ? "Nada. Selecciona uno o más prefabs de nivel en el Project." : string.Join(", ", paths.ConvertAll(Path.GetFileNameWithoutExtension)),
                EditorStyles.wordWrappedLabel);

            using (new EditorGUI.DisabledScope(paths.Count == 0))
            {
                if (GUILayout.Button("Optimizar selección", GUILayout.Height(28)))
                {
                    _reports.Clear();
                    foreach (string path in paths)
                    {
                        _reports.Add(OptimizePrefab(path, _options));
                    }
                }
            }

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (Report report in _reports)
            {
                DrawReport(report);
            }
            EditorGUILayout.EndScrollView();
        }

        private static List<string> SelectedLevelPrefabs()
        {
            var paths = new List<string>();
            foreach (Object selected in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (!path.EndsWith(".prefab")) continue;
                if (Path.GetFileNameWithoutExtension(path).EndsWith(MapOptimizer.OptimizedSuffix)) continue;
                paths.Add(path);
            }
            return paths;
        }

        // ---------- Optimizar y guardar ----------

        /// <summary>Optimiza un prefab de nivel y guarda la copia junto a él. Devuelve el informe.</summary>
        public static Report OptimizePrefab(string sourcePath, MapOptimizer.Options options)
        {
            string folder = Path.GetDirectoryName(sourcePath).Replace("\\", "/");
            string name = Path.GetFileNameWithoutExtension(sourcePath) + MapOptimizer.OptimizedSuffix;
            string outputPath = $"{folder}/{name}.prefab";
            string meshesPath = $"{folder}/{name}_Meshes.asset";

            // LoadPrefabContents conserva los prefabs anidados (llave, puerta...); Instantiate los desharía.
            GameObject contents = PrefabUtility.LoadPrefabContents(sourcePath);
            var report = new Report { SourcePath = sourcePath, OutputPath = outputPath };
            try
            {
                report.Result = MapOptimizer.Optimize(contents, options);
                SaveMeshes(report.Result.CreatedMeshes, meshesPath);
                contents.name = name;
                PrefabUtility.SaveAsPrefabAsset(contents, outputPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            report.SourceBytes = FileSize(sourcePath);
            report.OutputBytes = FileSize(outputPath) + FileSize(meshesPath);
            Debug.Log(Describe(report));
            return report;
        }

        /// <summary>Las mallas nuevas tienen que vivir en un asset para que el prefab las pueda referenciar.</summary>
        private static void SaveMeshes(List<Mesh> meshes, string meshesPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(meshesPath) != null)
            {
                AssetDatabase.DeleteAsset(meshesPath);
            }
            if (meshes.Count == 0) return;

            AssetDatabase.CreateAsset(meshes[0], meshesPath);
            for (int i = 1; i < meshes.Count; i++)
            {
                AssetDatabase.AddObjectToAsset(meshes[i], meshesPath);
            }
        }

        private static long FileSize(string assetPath)
        {
            var file = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
            return file.Exists ? file.Length : 0;
        }

        // ---------- Informe ----------

        public static string Describe(Report r)
        {
            MapStats b = r.Result.Before, a = r.Result.After;
            return $"[Optimizar] {Path.GetFileName(r.SourcePath)} → {Path.GetFileName(r.OutputPath)}\n" +
                   $"  Objetos:        {b.GameObjects,8} → {a.GameObjects,8}\n" +
                   $"  Renderers:      {b.Renderers,8} → {a.Renderers,8}\n" +
                   $"  Vértices:       {b.Vertices,8} → {a.Vertices,8}\n" +
                   $"  Triángulos:     {b.Triangles,8} → {a.Triangles,8}\n" +
                   $"  Colisionadores: {b.Colliders,8} → {a.Colliders,8}\n" +
                   $"  Disco:          {r.SourceBytes / 1024,6} KB → {r.OutputBytes / 1024,6} KB\n" +
                   $"  Duplicados quitados: {r.Result.DuplicatesRemoved}; triángulos ocultos quitados: {r.Result.TrianglesRemoved}";
        }

        private static void DrawReport(Report r)
        {
            EditorGUILayout.LabelField(Path.GetFileName(r.OutputPath), EditorStyles.boldLabel);
            MapStats b = r.Result.Before, a = r.Result.After;
            Row("", "Antes", "Después");
            Row("Objetos", b.GameObjects, a.GameObjects);
            Row("Renderers", b.Renderers, a.Renderers);
            Row("Vértices", b.Vertices, a.Vertices);
            Row("Triángulos", b.Triangles, a.Triangles);
            Row("Colisionadores", b.Colliders, a.Colliders);
            Row("Disco (KB)", r.SourceBytes / 1024, r.OutputBytes / 1024);

            if (b.Colliders != a.Colliders)
            {
                EditorGUILayout.HelpBox("El número de colisionadores ha cambiado: revisa el nivel.", MessageType.Error);
            }

            if (GUILayout.Button("Seleccionar"))
            {
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(r.OutputPath);
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            EditorGUILayout.Space();
        }

        private static void Row(string label, object before, object after)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(110));
                EditorGUILayout.LabelField(before.ToString(), GUILayout.Width(90));
                EditorGUILayout.LabelField(after.ToString(), GUILayout.Width(90));
            }
        }

        // ---------- Línea de comandos ----------

        /// <summary>
        /// Para ejecutarlo sin abrir el editor:
        /// Unity -batchmode -quit -projectPath LateGame -executeMethod Late.EditorTools.MapOptimizerWindow.OptimizeRunLevels
        /// </summary>
        public static void OptimizeRunLevels()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab Run", new[] { "Assets/Late/Levels/Maps" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith("Run") || file.EndsWith(MapOptimizer.OptimizedSuffix)) continue;
                OptimizePrefab(path, MapOptimizer.Options.Default);
            }
        }
    }
}
