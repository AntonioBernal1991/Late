using System.Collections;
using Late.Core;
using UnityEngine;

namespace Late.Generation
{
    /// <summary>
    /// Construye los módulos: dos capas de cubos (paredes arriba, suelo abajo) y un colisionador
    /// de suelo. Después deja que <see cref="PathGenerator"/> abra el camino y combina las mallas.
    /// </summary>
    public class ModuleGenerator
    {
        private readonly IMapGenerator _mapGenerator;
        private readonly IObjectPool _pool;
        private readonly ModuleQueue _queue;
        private readonly PathGenerator _pathGenerator;
        private readonly Transform _root;
        private int _modulesGenerated;

        private MapSettings Settings => _mapGenerator.Settings;

        public ModuleGenerator(IMapGenerator mapGenerator, IObjectPool pool, ModuleQueue queue, PathGenerator pathGenerator, Transform root)
        {
            _mapGenerator = mapGenerator;
            _pool = pool;
            _queue = queue;
            _pathGenerator = pathGenerator;
            _root = root;
        }

        /// <summary>
        /// Genera módulos de la cola hasta llegar a <paramref name="moduleCount"/> o vaciarla.
        /// Las bifurcaciones añaden módulos a la cola, así que cada rama se va generando por turnos.
        /// Al llegar al límite solo se generan los módulos que tapan salidas.
        /// </summary>
        public IEnumerator Generate(int moduleCount)
        {
            while (_queue.Count > 0)
            {
                if (_modulesGenerated >= moduleCount && !_queue.Peek().IsBlocker)
                {
                    yield break;
                }

                while (_mapGenerator.Pacer.IsPaused)
                {
                    yield return null;
                }

                yield return GenerateModule(_queue.Dequeue());
                _modulesGenerated++;

                if (!_mapGenerator.Pacer.IsInstant)
                {
                    yield return null;
                }
            }
        }

        private IEnumerator GenerateModule(ModuleInfo moduleInfo)
        {
            var module = new GameObject($"Module_{_modulesGenerated + 1}");
            module.transform.SetParent(_root, true);
            module.transform.position = moduleInfo.Position;
            moduleInfo.Root = module.transform;

            // Capa superior: las paredes. Necesitan colisionador para que el jugador choque con ellas.
            GameObject[,] wallLayer = GenerateLayer(module.transform, Settings.GrassMaterial, 0f, enableColliders: true);
            // Capa inferior: solo visual. Un único colisionador invisible hace de suelo.
            GenerateLayer(module.transform, Settings.GroundMaterial, -1f, enableColliders: false);
            GenerateFloorCollider(module.transform, -1f);

            if (!moduleInfo.IsBlocker)
            {
                // Algunos módulos (cada 8 o cada 10) tienen una cueva.
                bool applyCave = _modulesGenerated % 8 == 0 || _modulesGenerated % 10 == 0;
                yield return _pathGenerator.GeneratePath(wallLayer, _modulesGenerated, moduleInfo, applyCave);
            }

            MeshCombiner.CombineMeshesByMaterial(module);
        }

        private GameObject[,] GenerateLayer(Transform parent, Material material, float y, bool enableColliders)
        {
            ModuleGrid grid = Settings.Grid;
            var layer = new GameObject[grid.Width, grid.Height];

            for (int x = 0; x < grid.Width; x++)
            {
                for (int z = 0; z < grid.Height; z++)
                {
                    GameObject cube = _pool.GetObject();
                    cube.transform.position = parent.position + Settings.TileLocalPosition(new Vector2Int(x, z), y);
                    cube.transform.rotation = Quaternion.identity;
                    cube.transform.parent = parent;

                    // sharedMaterial: todos los cubos comparten el mismo material. Con .material Unity crearía
                    // una copia por cubo, se dispararían las draw calls y no se podrían combinar las mallas.
                    Renderer cubeRenderer = cube.GetComponent<Renderer>();
                    if (cubeRenderer != null) cubeRenderer.sharedMaterial = material;

                    Collider cubeCollider = cube.GetComponent<Collider>();
                    if (enableColliders)
                    {
                        if (cubeCollider == null) cubeCollider = cube.AddComponent<BoxCollider>();
                        cubeCollider.enabled = true;
                        cubeCollider.isTrigger = false;
                    }
                    else if (cubeCollider != null)
                    {
                        cubeCollider.enabled = false;
                    }

                    layer[x, z] = cube;
                }
            }

            return layer;
        }

        /// <summary>Un único cubo invisible, del tamaño de todo el módulo, que hace de suelo físico.</summary>
        private void GenerateFloorCollider(Transform parent, float y)
        {
            ModuleGrid grid = Settings.Grid;
            float spacing = Settings.TileSpacing;

            // Cubre lo mismo que la cuadrícula: de 0 a (n-1)*spacing más medio cubo por cada lado.
            float sizeX = (grid.Width - 1) * spacing + 1f;
            float sizeZ = (grid.Height - 1) * spacing + 1f;
            Vector3 center = new Vector3((grid.Width - 1) * spacing * 0.5f, y, (grid.Height - 1) * spacing * 0.5f);

            GameObject floor = _pool.GetObject();
            floor.name = MeshCombiner.FloorColliderName;
            floor.transform.SetParent(parent, true);
            floor.transform.position = parent.position + center;
            floor.transform.rotation = Quaternion.identity;
            floor.transform.localScale = new Vector3(sizeX, 1f, sizeZ);

            Renderer floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null) floorRenderer.enabled = false;

            Collider floorCollider = floor.GetComponent<Collider>();
            if (floorCollider == null) floorCollider = floor.AddComponent<BoxCollider>();
            floorCollider.enabled = true;
            floorCollider.isTrigger = false;
            floorCollider.material = Settings.GroundPhysicMaterial;
        }
    }
}
