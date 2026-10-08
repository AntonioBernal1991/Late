using System.Collections.Generic;
using UnityEngine;

namespace Late.Generation
{
    /// <summary>
    /// Cola de módulos pendientes de generar. Recuerda todas las posiciones usadas
    /// para que dos módulos no se coloquen en el mismo sitio.
    /// </summary>
    public class ModuleQueue
    {
        private readonly Queue<ModuleInfo> _queue = new Queue<ModuleInfo>();
        private readonly HashSet<Vector3> _usedPositions = new HashSet<Vector3>();

        public int Count => _queue.Count;

        /// <summary>True si ya hay un módulo a menos de <paramref name="minDistance"/> de la posición.</summary>
        public bool IsTooClose(Vector3 position, float minDistance)
        {
            foreach (Vector3 used in _usedPositions)
            {
                if (Vector3.Distance(position, used) < minDistance)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Añade el módulo, salvo que ya haya otro exactamente en la misma posición.</summary>
        public void Enqueue(ModuleInfo module)
        {
            if (!_usedPositions.Add(module.Position))
            {
                return;
            }
            _queue.Enqueue(module);
        }

        public ModuleInfo Dequeue() => _queue.Count > 0 ? _queue.Dequeue() : null;

        public ModuleInfo Peek() => _queue.Count > 0 ? _queue.Peek() : null;

        public void Clear()
        {
            _queue.Clear();
            _usedPositions.Clear();
        }
    }
}
