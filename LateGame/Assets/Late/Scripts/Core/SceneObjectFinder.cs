using UnityEngine;
using UnityEngine.SceneManagement;

namespace Late.Core
{
    /// <summary>
    /// Búsquedas de objetos por nombre en las escenas cargadas, incluidos los desactivados.
    /// Son caras: hay que cachear el resultado, no llamarlas cada frame.
    /// </summary>
    public static class SceneObjectFinder
    {
        /// <summary>
        /// Busca un GameObject con ese nombre exacto en cualquier escena cargada (aunque esté desactivado).
        /// Prefiere los objetos de UI (con RectTransform). <paramref name="exclude"/> y sus hijos se ignoran.
        /// </summary>
        public static GameObject FindByNameIncludingInactive(string exactName, Transform exclude = null)
        {
            if (string.IsNullOrWhiteSpace(exactName)) return null;

            GameObject best = null;
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.name != exactName) continue;
                // Descarta assets y objetos internos del editor.
                if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
                if (go.hideFlags != HideFlags.None) continue;
                if (exclude != null && go.transform.IsChildOf(exclude)) continue;

                if (go.GetComponent<RectTransform>() != null) return go;
                if (best == null) best = go;
            }
            return best;
        }

        /// <summary>Busca un Transform con ese nombre exacto dentro de una escena (aunque esté desactivado).</summary>
        public static Transform FindInScene(Scene scene, string exactName)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(exactName)) return null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == exactName) return t;
                }
            }
            return null;
        }
    }
}
