using System.Collections;
using Late.Core;
using UnityEngine;

namespace Late.Generation
{
    /// <summary>
    /// Punto de entrada del generador procedural. Al empezar la escena genera el laberinto descrito
    /// por un <see cref="MapSettings"/> bajo un hijo "MazeRoot".
    ///
    /// Flujo para crear un nivel: generar en Play Mode, hornearlo a prefab con Late > Generador > Hornear,
    /// pulirlo a mano y optimizarlo. Los niveles del juego usan ese prefab, no generan en tiempo real.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapGenerator3D : MonoBehaviour, IMapGenerator
    {
        public const string MazeRootName = "MazeRoot";

        [SerializeField] private MapSettings _settings;

        [Header("Control de la generación")]
        [Tooltip("Si está activo, la generación solo avanza mientras se mantiene pulsado Espacio.")]
        [SerializeField] private bool _holdSpaceToGenerate = true;
        [Tooltip("Si está activo, genera todo de golpe, sin pausas entre pasos ni entre módulos.")]
        [SerializeField] private bool _generateInstantly = false;

        private Transform _mazeRoot;
        private ModuleQueue _queue;
        private ModuleGenerator _moduleGenerator;

        public MapSettings Settings => _settings;
        public GenerationPacer Pacer { get; private set; }
        public Transform MazeRoot => _mazeRoot;
        public bool IsGenerationComplete { get; private set; }

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

            Pacer = new GenerationPacer(_generateInstantly, _holdSpaceToGenerate);
            _queue = new ModuleQueue();

            // Un módulo más que los pedidos: el que tapa la salida final. Los cubos del pool cuelgan
            // del generador (no de MazeRoot) para que no acaben dentro del mapa exportado.
            int poolSize = _settings.ModuleWidth * _settings.ModuleHeight * (_settings.ModuleCount + 1);
            var pool = new ObjectPool(_settings.CubePrefab, poolSize, transform);
            var pathGenerator = new PathGenerator(this, pool, _settings.BaseSeed);
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
