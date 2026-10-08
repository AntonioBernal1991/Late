using System.Linq;
using Late.Generation;
using UnityEditor;
using UnityEngine;

namespace Late.EditorTools
{
    /// <summary>
    /// Ventana para hornear módulos generados como prefabs: el objeto seleccionado o todos
    /// los Module_* que cuelgan de MazeRoot.
    /// </summary>
    public class ModulePrefabBakerWindow : EditorWindow
    {
        private const string DefaultOutputFolder = "Assets/Late/Levels/Modules";
        private const string Title = "Hornear módulos";

        private string _outputFolder = DefaultOutputFolder;
        private bool _stripInactiveChildren = true;

        [MenuItem("Late/Generador/Hornear módulos")]
        public static void ShowWindow()
        {
            GetWindow<ModulePrefabBakerWindow>(Title);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Guarda como prefabs los módulos generados.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            _outputFolder = EditorGUILayout.TextField("Carpeta de salida", _outputFolder);
            _stripInactiveChildren = EditorGUILayout.ToggleLeft("Quitar hijos desactivados (prefabs más pequeños)", _stripInactiveChildren);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Hornear selección"))
                {
                    BakeSelected();
                }
                if (GUILayout.Button("Hornear todo MazeRoot"))
                {
                    BakeAllUnderMazeRoot();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "1. Genera el laberinto (crea MazeRoot/Module_1, Module_2...).\n" +
                "2. Selecciona un módulo o usa \"Hornear todo MazeRoot\".",
                MessageType.Info);
        }

        private void BakeSelected()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(Title, "Selecciona primero un módulo en la jerarquía.", "OK");
                return;
            }

            string folder = MapPrefabExporter.EnsureFolder(_outputFolder);
            string path = $"{folder}/{MapPrefabExporter.MakeSafeFileName(selected.name)}.prefab";
            MapPrefabExporter.SavePrefab(selected, path, _stripInactiveChildren);
            Debug.Log($"Módulo guardado: {path}", AssetDatabase.LoadAssetAtPath<Object>(path));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void BakeAllUnderMazeRoot()
        {
            GameObject mazeRoot = GameObject.Find(MapGenerator3D.MazeRootName);
            if (mazeRoot == null)
            {
                EditorUtility.DisplayDialog(Title, $"No hay ningún objeto '{MapGenerator3D.MazeRootName}' en la escena.", "OK");
                return;
            }

            bool hasModules = mazeRoot.transform.Cast<Transform>().Any(t => t.name.StartsWith(MapPrefabExporter.ModulePrefix));
            if (!hasModules)
            {
                EditorUtility.DisplayDialog(Title, $"No hay hijos '{MapPrefabExporter.ModulePrefix}*' en {MapGenerator3D.MazeRootName}.", "OK");
                return;
            }

            MapPrefabExporter.ExportModules(mazeRoot.transform, _outputFolder, _stripInactiveChildren);
        }
    }
}
