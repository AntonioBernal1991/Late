using System.IO;
using Late.Data;
using UnityEditor;

namespace Late.EditorTools
{
    /// <summary>Utilidades del editor para la base de datos local (late.db).</summary>
    public static class LocalDataMenu
    {
        [MenuItem("Late/Datos locales/Abrir carpeta")]
        private static void OpenFolder()
        {
            EditorUtility.RevealInFinder(LocalDataBootstrap.DatabasePath);
        }

        [MenuItem("Late/Datos locales/Borrar progreso")]
        private static void DeleteDatabase()
        {
            string path = LocalDataBootstrap.DatabasePath;
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Datos locales", "No hay progreso guardado todavía.", "Vale");
                return;
            }
            if (!EditorUtility.DisplayDialog("Datos locales",
                    $"Se borrará el progreso, los tiempos y los ajustes guardados en:\n{path}", "Borrar", "Cancelar"))
            {
                return;
            }

            LocalDataBootstrap.Close();
            File.Delete(path);
            UnityEngine.Debug.Log($"[LocalData] Borrado '{path}'.");
        }

        [MenuItem("Late/Datos locales/Borrar progreso", true)]
        private static bool CanDeleteDatabase() => !EditorApplication.isPlaying;
    }
}
