using System.IO;
using Late.Generation;
using UnityEditor;
using UnityEngine;

namespace Late.EditorTools
{
    /// <summary>
    /// Inspector de <see cref="MapGenerator3D"/>: añade selectores de carpeta y los botones
    /// para exportar a prefabs lo que se ha generado en Play Mode.
    /// </summary>
    [CustomEditor(typeof(MapGenerator3D))]
    public class MapGenerator3DEditor : UnityEditor.Editor
    {
        private static readonly string[] ExportProperties =
        {
            "_modulePrefabExportFolder",
            "_exportStripInactiveChildren",
            "_autoExportModulePrefabsAfterGeneration",
            "_mapPrefabExportFolder",
            "_autoExportMapPrefabAfterGeneration",
            "_mapExportRemoveCombinedMeshes",
            "_mapExportEnableAllCubeRenderers",
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, ExportProperties);

            var generator = (MapGenerator3D)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Exportar módulos a prefabs", EditorStyles.boldLabel);
            DrawFolderField(serializedObject.FindProperty("_modulePrefabExportFolder"), "Carpeta");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_exportStripInactiveChildren"), new GUIContent("Quitar hijos desactivados"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_autoExportModulePrefabsAfterGeneration"), new GUIContent("Exportar al terminar"));
            DrawPlayModeButton("Exportar módulos ahora", () => MapPrefabExporter.ExportModules(generator));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Exportar el mapa completo a prefab", EditorStyles.boldLabel);
            DrawFolderField(serializedObject.FindProperty("_mapPrefabExportFolder"), "Carpeta");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_mapExportRemoveCombinedMeshes"), new GUIContent("Quitar mallas combinadas"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_mapExportEnableAllCubeRenderers"), new GUIContent("Activar renderers de los cubos"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_autoExportMapPrefabAfterGeneration"), new GUIContent("Exportar al terminar"));
            DrawPlayModeButton("Exportar mapa ahora", () => MapPrefabExporter.ExportFullMap(generator));

            EditorGUILayout.HelpBox(
                "1. Entra en Play Mode y deja que se genere el laberinto.\n" +
                "2. Exporta el mapa completo: se guarda un prefab editable cubo a cubo.\n" +
                "3. Púlelo a mano y optimízalo antes de usarlo en un nivel.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPlayModeButton(string label, System.Action action)
        {
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button(label))
                {
                    serializedObject.ApplyModifiedProperties();
                    action();
                }
            }
        }

        /// <summary>Campo de texto con un botón para elegir una carpeta dentro de Assets/.</summary>
        private static void DrawFolderField(SerializedProperty property, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label));
                if (!GUILayout.Button("Elegir", GUILayout.Width(70))) return;

                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string current = property.stringValue;
                string start = !string.IsNullOrWhiteSpace(current) && current.StartsWith("Assets/")
                    ? Path.Combine(projectRoot, current)
                    : Application.dataPath;

                string picked = EditorUtility.OpenFolderPanel("Elige una carpeta dentro de Assets/", start, "");
                if (string.IsNullOrEmpty(picked)) return;

                string full = Path.GetFullPath(picked);
                string relative = full.StartsWith(projectRoot)
                    ? full.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace("\\", "/")
                    : null;

                if (relative == "Assets" || (relative != null && relative.StartsWith("Assets/")))
                {
                    property.stringValue = relative;
                }
                else
                {
                    EditorUtility.DisplayDialog("Carpeta no válida", "Elige una carpeta dentro de Assets/.", "OK");
                }
            }
        }
    }
}
