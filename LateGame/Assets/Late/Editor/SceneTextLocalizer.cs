using System.Collections.Generic;
using System.Linq;
using Late.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Late.EditorTools
{
    /// <summary>
    /// Añade <see cref="LocalizedText"/> a los textos de la escena abierta que coinciden exactamente con
    /// el texto en inglés de una clave de <see cref="Strings"/>. Así los textos ya colocados a mano
    /// cambian de idioma sin rehacer la interfaz. No toca los que ya tienen el componente.
    /// </summary>
    public static class SceneTextLocalizer
    {
        public const string MenuPath = "Late/Idiomas/Traducir textos de la escena abierta";

        [MenuItem(MenuPath)]
        private static void LocalizeOpenScene()
        {
            List<string> report = Localize(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Idiomas", report.Count == 0 ? "No hay textos nuevos que traducir." : string.Join("\n", report), "Vale");
        }

        /// <summary>Línea de comandos: traduce la escena Main y la guarda.</summary>
        public static void LocalizeMainScene()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Late/Scenes/Main.unity");
            foreach (string line in Localize(scene)) Debug.Log("[Idiomas] " + line);
            EditorSceneManager.SaveScene(scene);
        }

        public static List<string> Localize(Scene scene)
        {
            // Texto en inglés → clave. Se normalizan los saltos de línea para comparar.
            Dictionary<string, string> keyByEnglish = Strings.Keys
                .GroupBy(key => Normalize(Strings.Get(key, Language.English)))
                .ToDictionary(g => g.Key, g => g.First());

            var report = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.GetComponent<LocalizedText>() != null) continue;
                    if (!keyByEnglish.TryGetValue(Normalize(text.text), out string key)) continue;

                    Undo.AddComponent<LocalizedText>(text.gameObject).Key = key;
                    report.Add($"{Path(text.transform)} → {key}");
                }
            }

            if (report.Count > 0) EditorSceneManager.MarkSceneDirty(scene);
            return report;
        }

        private static string Normalize(string text) => text?.Replace("\r\n", "\n").Replace("\r", "\n");

        private static string Path(Transform t) => t.parent == null ? t.name : $"{Path(t.parent)}/{t.name}";
    }
}
