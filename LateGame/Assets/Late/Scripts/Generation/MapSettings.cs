using UnityEngine;

namespace Late.Generation
{
    /// <summary>
    /// Configuración de un tipo de nivel: tamaño de los módulos, semilla, longitud, cuevas y aspecto.
    /// Cada tipo de nivel es un asset distinto, así que el mismo generador sirve para todos.
    /// </summary>
    [CreateAssetMenu(fileName = "MapSettings", menuName = "Late/Map Settings")]
    public class MapSettings : ScriptableObject
    {
        [Header("Módulos")]
        [Tooltip("Casillas de ancho de cada módulo.")]
        [SerializeField] [Min(3)] private int _moduleWidth = 13;
        [Tooltip("Casillas de largo de cada módulo.")]
        [SerializeField] [Min(3)] private int _moduleHeight = 13;
        [Tooltip("Distancia entre casillas, en unidades de mundo.")]
        [SerializeField] private float _tileSpacing = 1.2f;
        [Tooltip("Hueco extra entre un módulo y el siguiente.")]
        [SerializeField] private float _moduleSpacing = 1.2f;

        [Header("Generación")]
        [Tooltip("Misma semilla = mismo laberinto.")]
        [SerializeField] private int _seed = 0;
        [Tooltip("Número de módulos del camino (sin contar el módulo final que cierra la salida).")]
        [SerializeField] [Min(1)] private int _moduleCount = 3;
        [Tooltip("Las bifurcaciones empiezan a aparecer a partir de este módulo (empezando en 1).")]
        [SerializeField] [Min(1)] private int _branchingStartsAtModule = 7;

        [Header("Cuevas")]
        [Tooltip("Si está activo, algunos módulos excavan una sala cuadrada alrededor del centro.")]
        [SerializeField] private bool _cavesEnabled = true;
        [Tooltip("Medio lado de la sala, en casillas.")]
        [SerializeField] [Range(1f, 8f)] private float _caveSize = 2f;

        [Header("Aspecto")]
        [SerializeField] private GameObject _cubePrefab;
        [Tooltip("Material de la capa superior (las paredes del laberinto).")]
        [SerializeField] private Material _grassMaterial;
        [Tooltip("Material de la capa inferior (el suelo).")]
        [SerializeField] private Material _groundMaterial;
        [Tooltip("Material físico del suelo (fricción y rebote).")]
        [SerializeField] private PhysicMaterial _groundPhysicMaterial;

        public int ModuleWidth => _moduleWidth;
        public int ModuleHeight => _moduleHeight;
        public float TileSpacing => _tileSpacing;
        public float ModuleSpacing => _moduleSpacing;
        public int Seed => _seed;
        public int ModuleCount => _moduleCount;
        public int BranchingStartsAtModule => Mathf.Max(1, _branchingStartsAtModule);
        public bool CavesEnabled => _cavesEnabled;
        public float CaveSize => _caveSize;
        public GameObject CubePrefab => _cubePrefab;
        public Material GrassMaterial => _grassMaterial;
        public Material GroundMaterial => _groundMaterial;
        public PhysicMaterial GroundPhysicMaterial => _groundPhysicMaterial;

        public ModuleGrid Grid => new ModuleGrid(_moduleWidth, _moduleHeight);

        /// <summary>
        /// Semilla base de los generadores aleatorios de cada módulo. Se deriva solo de la semilla
        /// configurada (hash multiplicativo de Knuth), así que es la misma en cualquier sesión y equipo.
        /// </summary>
        public int BaseSeed => unchecked((int)((uint)_seed * 2654435761u));

        /// <summary>Tamaño de un módulo en el mundo, sin el hueco entre módulos.</summary>
        public Vector2 ModuleWorldSize => new Vector2(_moduleWidth * _tileSpacing, _moduleHeight * _tileSpacing);

        /// <summary>Posición de la casilla dentro del módulo, relativa a su origen.</summary>
        public Vector3 TileLocalPosition(Vector2Int tile, float y = 0f) =>
            new Vector3(tile.x * _tileSpacing, y, tile.y * _tileSpacing);

        /// <summary>Origen del módulo vecino en la dirección dada.</summary>
        public Vector3 NeighbourModulePosition(Vector3 modulePosition, PathDirection direction)
        {
            Vector2 size = ModuleWorldSize;
            switch (direction)
            {
                case PathDirection.Down: return new Vector3(modulePosition.x, 0f, modulePosition.z + size.y + _moduleSpacing);
                case PathDirection.Left: return new Vector3(modulePosition.x - size.x - _moduleSpacing, 0f, modulePosition.z);
                default: return new Vector3(modulePosition.x + size.x + _moduleSpacing, 0f, modulePosition.z);
            }
        }
    }
}
