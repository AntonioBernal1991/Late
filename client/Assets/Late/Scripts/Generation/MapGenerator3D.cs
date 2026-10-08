using System;
using System.Collections;
using Late.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Late.Generation
{
    /// <summary>
    /// Punto de entrada del generador procedural. Al empezar la escena genera el laberinto descrito
    /// por un <see cref="MapSettings"/> bajo un hijo "MazeRoot".
    ///
    /// Flujo para crear un nivel: generar en Play Mode, exportar el mapa a prefab desde el inspector,
    /// pulirlo a mano y optimizarlo. Los niveles del juego usan ese prefab, no generan en tiempo real.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapGenerator3D : MonoBehaviour, IMapGenerator
    {
        public const string MazeRootName = "MazeRoot";

        /// <summary>Se lanza al terminar de generar. El editor lo usa para exportar automáticamente.</summary>
        public static event Action<MapGenerator3D> GenerationCompleted;

        [SerializeField] private MapSettings _settings;

        [Header("Control de la generación")]
        [Tooltip("Si está activo, la generación solo avanza mientras se mantiene pulsado Espacio.")]
        [SerializeField] private bool _holdSpaceToGenerate = true;
        [Tooltip("Si está activo, genera todo de golpe, sin pausas entre pasos ni entre módulos.")]
        [SerializeField] private bool _generateInstantly = false;

        [Header("Exportar módulos a prefabs (editor)")]
        [Tooltip("Carpeta dentro de Assets/ donde se guardan los prefabs de módulos.")]
        [SerializeField] private string _modulePrefabExportFolder = "Assets/Late/Levels/Modules";
        [Tooltip("Quita los hijos desactivados antes de guardar (prefabs más pequeños).")]
        [SerializeField] private bool _exportStripInactiveChildren = true;
        [Tooltip("Exporta los módulos automáticamente al terminar la generación.")]
        [SerializeField] private bool _autoExportModulePrefabsAfterGeneration = false;

        [Header("Exportar el mapa completo a prefab (editor)")]
        [Tooltip("Carpeta dentro de Assets/ donde se guarda el prefab del mapa.")]
        [SerializeField] private string _mapPrefabExportFolder = "Assets/Late/Levels/Maps";
        [Tooltip("Exporta el mapa automáticamente al terminar la generación.")]
        [SerializeField] private bool _autoExportMapPrefabAfterGeneration = false;
        [Tooltip("Quita las mallas combinadas para que el mapa se pueda editar cubo a cubo.")]
        [SerializeField] private bool _mapExportRemoveCombinedMeshes = true;
        [Tooltip("Activa el MeshRenderer de todos los cubos (el suelo invisible sigue invisible).")]
        [SerializeField] private bool _mapExportEnableAllCubeRenderers = true;

        private Transform _mazeRoot;
        private ModuleQueue _queue;
        private ModuleGenerator _moduleGenerator;

        public MapSettings Settings => _settings;
        public GenerationPacer Pacer { get; private set; }
        public Transform MazeRoot => _mazeRoot;
        public bool IsGenerationComplete { get; private set; }

        public string ModulePrefabExportFolder => _modulePrefabExportFolder;
        public bool ExportStripInactiveChildren => _exportStripInactiveChildren;
        public bool AutoExportModulePrefabsAfterGeneration => _autoExportModulePrefabsAfterGeneration;
        public string MapPrefabExportFolder => _mapPrefabExportFolder;
        public bool AutoExportMapPrefabAfterGeneration => _autoExportMapPrefabAfterGeneration;
        public bool MapExportRemoveCombinedMeshes => _mapExportRemoveCombinedMeshes;
        public bool MapExportEnableAllCubeRenderers => _mapExportEnableAllCubeRenderers;

        private void Awake()
        {
            if (_settings == null || _settings.CubePrefab == null)
            {
                Debug.LogError($"{nameof(MapGenerator3D)}: falta asignar un MapSettings con su prefab de cubo.", this);
                enabled = false;
                return;
            }

            // MazeRoot cuelga del generador para que viva en su misma escena. Si no, al cargar el nivel
            // de forma aditiva Unity lo crearía en la escena activa y no se descargaría con el nivel.
            _mazeRoot = new GameObject(MazeRootName).transform;
            _mazeRoot.SetParent(transform, worldPositionStays: false);
            _mazeRoot.localPosition = Vector3.zero;
            _mazeRoot.localRotation = Quaternion.identity;
        }

        private void Start()
        {
            if (_mazeRoot == null) return;

            Random.InitState(_settings.Seed);
            int baseSeed = Random.state.GetHashCode();

            Pacer = new GenerationPacer(_generateInstantly, _holdSpaceToGenerate);
            _queue = new ModuleQueue();

            // Un módulo más que los pedidos: el que tapa la salida final. Los cubos del pool cuelgan
            // del generador (no de MazeRoot) para que no acaben dentro del mapa exportado.
            int poolSize = _settings.ModuleWidth * _settings.ModuleHeight * (_settings.ModuleCount + 1);
            var pool = new ObjectPool(_settings.CubePrefab, poolSize, transform);
            var pathGenerator = new PathGenerator(this, pool, baseSeed);
            _moduleGenerator = new ModuleGenerator(this, pool, _queue, pathGenerator, _mazeRoot);

            // El primer módulo está en el origen y su camino empieza en el centro.
            ModuleGrid grid = _settings.Grid;
            _queue.Enqueue(new ModuleInfo(Vector3.zero, PathDirection.Down, grid.Center));
            PlaceMainCameraAt(_settings.TileLocalPosition(grid.Center));

            StartCoroutine(GenerateModules());
        }

        private void OnDestroy()
        {
            // Por si algo sacó MazeRoot de debajo del generador: no dejar geometría huérfana.
            if (_mazeRoot != null && _mazeRoot.parent != transform)
            {
                Destroy(_mazeRoot.gameObject);
            }
        }

        private IEnumerator GenerateModules()
        {
            yield return _moduleGenerator.Generate(_settings.ModuleCount);
            IsGenerationComplete = true;
            GenerationCompleted?.Invoke(this);
        }

        private static void PlaceMainCameraAt(Vector3 position)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            cam.transform.position = new Vector3(position.x, cam.transform.position.y, position.z);
        }

        public void EnqueueNextModule(Vector2Int exitTile, PathDirection exitDirection, Vector3 fromModulePosition, bool isBlocker = false)
        {
            Vector3 position = _settings.NeighbourModulePosition(fromModulePosition, exitDirection);

            // Evita solapes: no se coloca un módulo a menos del 80 % del tamaño de un módulo de otro.
            Vector2 size = _settings.ModuleWorldSize;
            float minDistance = Mathf.Min(size.x, size.y) * 0.8f;
            if (_queue.IsTooClose(position, minDistance))
            {
                return;
            }

            Vector2Int entryTile = _settings.Grid.EntryTileFromExit(exitTile, exitDirection);
            _queue.Enqueue(new ModuleInfo(position, exitDirection, entryTile, isBlocker));
        }
    }
}
