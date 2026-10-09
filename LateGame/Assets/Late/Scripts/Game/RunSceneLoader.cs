using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Late.Game
{
    /// <summary>
    /// Carga y descarga de las escenas de nivel, que se añaden de forma aditiva a la escena principal.
    /// </summary>
    public class RunSceneLoader
    {
        // Unity no deja activar su escena interna DontDestroyOnLoad.
        private const string DontDestroyOnLoadSceneName = "DontDestroyOnLoad";

        private readonly Scene _bootstrapScene;
        private readonly bool _log;

        /// <param name="bootstrapScene">Escena con la que arranca el juego (la del menú).</param>
        public RunSceneLoader(Scene bootstrapScene, bool log)
        {
            _bootstrapScene = bootstrapScene;
            _log = log;
        }

        public static bool IsLoaded(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        public IEnumerator LoadAdditive(string sceneName)
        {
            if (IsLoaded(sceneName)) yield break;
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }

        public IEnumerator Unload(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) yield break;

            // No se puede descargar la escena activa: antes se activa otra.
            EnsureActiveSceneIsNot(sceneName);
            if (_log) Debug.Log($"[RunSceneLoader] Descargando '{sceneName}'.");
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        /// <summary>Activa la escena para que los objetos nuevos se creen en ella. False si no está cargada.</summary>
        public static bool TrySetActive(string sceneName, out Scene scene)
        {
            scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return false;

            SceneManager.SetActiveScene(scene);
            return true;
        }

        private void EnsureActiveSceneIsNot(string excludedScene)
        {
            Scene active = SceneManager.GetActiveScene();
            if (IsUsable(active, excludedScene)) return;

            if (IsUsable(_bootstrapScene, excludedScene))
            {
                SceneManager.SetActiveScene(_bootstrapScene);
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (IsUsable(scene, excludedScene))
                {
                    SceneManager.SetActiveScene(scene);
                    return;
                }
            }

            if (_log) Debug.LogWarning("[RunSceneLoader] No hay otra escena cargada que activar antes de descargar.");
        }

        private static bool IsUsable(Scene scene, string excludedScene) =>
            scene.IsValid() && scene.isLoaded && scene.name != DontDestroyOnLoadSceneName && scene.name != excludedScene;
    }
}
