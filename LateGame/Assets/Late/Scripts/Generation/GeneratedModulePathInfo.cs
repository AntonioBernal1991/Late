using UnityEngine;

namespace Late.Generation
{
    /// <summary>
    /// Metadatos que se guardan en cada módulo generado (Module_1, Module_2...).
    /// Indican si el módulo obliga a girar (la dirección de entrada es distinta de la de salida).
    /// </summary>
    [DisallowMultipleComponent]
    public class GeneratedModulePathInfo : MonoBehaviour
    {
        // Los nombres de estas propiedades no deben cambiar: están serializados en los niveles horneados.
        [field: SerializeField] public int ModuleIndex { get; private set; } = -1;
        [field: SerializeField] public PathDirection EntryDirection { get; private set; } = PathDirection.Down;
        [field: SerializeField] public PathDirection ExitDirection { get; private set; } = PathDirection.Down;
        [field: SerializeField] public bool IsTurnModule { get; private set; } = false;

        public void SetEntry(int moduleIndex, PathDirection entry)
        {
            ModuleIndex = moduleIndex;
            EntryDirection = entry;
            Recompute();
        }

        public void SetExit(PathDirection exit)
        {
            ExitDirection = exit;
            Recompute();
        }

        private void Recompute()
        {
            IsTurnModule = EntryDirection != ExitDirection;
        }
    }
}
