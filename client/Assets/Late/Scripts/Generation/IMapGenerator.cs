using System.Collections;
using UnityEngine;

namespace Late.Generation
{
    /// <summary>
    /// Lo que los generadores de módulos y caminos necesitan del generador de mapas.
    /// </summary>
    public interface IMapGenerator
    {
        MapSettings Settings { get; }
        GenerationPacer Pacer { get; }

        /// <summary>
        /// Encola el módulo vecino por el que continúa un camino que sale de un módulo.
        /// No hace nada si ya hay un módulo demasiado cerca de esa posición.
        /// </summary>
        void EnqueueNextModule(Vector2Int exitTile, PathDirection exitDirection, Vector3 fromModulePosition, bool isBlocker = false);

        /// <summary>Lanza una corrutina ligada al ciclo de vida del generador.</summary>
        Coroutine StartCoroutine(IEnumerator routine);
    }
}
