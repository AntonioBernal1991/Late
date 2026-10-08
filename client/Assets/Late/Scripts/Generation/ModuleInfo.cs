using UnityEngine;

namespace Late.Generation
{
    /// <summary>
    /// Datos de un módulo pendiente de generar: dónde va y por dónde entra el camino.
    /// </summary>
    public class ModuleInfo
    {
        public Vector3 Position { get; }
        public PathDirection EntryDirection { get; }
        public Vector2Int EntryTile { get; }

        /// <summary>Módulo macizo, sin camino. Se usa para tapar la salida del último módulo.</summary>
        public bool IsBlocker { get; }

        /// <summary>GameObject del módulo, una vez creado.</summary>
        public Transform Root { get; set; }

        public ModuleInfo(Vector3 position, PathDirection entryDirection, Vector2Int entryTile, bool isBlocker = false)
        {
            Position = position;
            EntryDirection = entryDirection;
            EntryTile = entryTile;
            IsBlocker = isBlocker;
        }

        public override string ToString() =>
            $"Position: {Position}, EntryDirection: {EntryDirection}, EntryTile: {EntryTile}, IsBlocker: {IsBlocker}";
    }
}
