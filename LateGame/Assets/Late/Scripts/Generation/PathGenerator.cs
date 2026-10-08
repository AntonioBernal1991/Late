using System.Collections;
using System.Collections.Generic;
using Late.Core;
using UnityEngine;

namespace Late.Generation
{
    /// <summary>
    /// Abre el camino dentro de un módulo quitando cubos de la capa superior.
    ///
    /// El camino entra por la casilla de entrada, avanza hasta el centro, allí decide hacia dónde
    /// sigue (recto, izquierda o derecha) y continúa hasta un borde. Desde ese borde encola el
    /// módulo siguiente. En algunos giros crea una bifurcación en "T" que da lugar a otra rama
    /// independiente del laberinto.
    /// </summary>
    public class PathGenerator
    {
        // La sala de una cueva siempre deja libre como mínimo esta distancia alrededor del centro.
        private const float CaveInnerSize = 1f;

        private readonly IMapGenerator _mapGenerator;
        private readonly IObjectPool _pool;
        private readonly int _baseSeed;

        private MapSettings Settings => _mapGenerator.Settings;
        private ModuleGrid Grid => _mapGenerator.Settings.Grid;
        private GenerationPacer Pacer => _mapGenerator.Pacer;

        public PathGenerator(IMapGenerator mapGenerator, IObjectPool pool, int baseSeed)
        {
            _mapGenerator = mapGenerator;
            _pool = pool;
            _baseSeed = baseSeed;
        }

        /// <param name="cubes">Capa superior del módulo; las casillas del camino se ponen a null.</param>
        /// <param name="moduleIndex">Índice del módulo, empezando en 0.</param>
        /// <param name="applyCave">Si este módulo debe excavar una cueva al terminar el camino.</param>
        public IEnumerator GeneratePath(GameObject[,] cubes, int moduleIndex, ModuleInfo moduleInfo, bool applyCave)
        {
            ModuleGrid grid = Grid;
            var context = new PathGenerationContext(_baseSeed, moduleInfo.Position, Settings.ModuleSpacing, Settings.ModuleWorldSize);

            // El primer módulo empieza en el centro avanzando hacia abajo.
            Vector2Int tile = moduleIndex == 0 ? grid.Center : moduleInfo.EntryTile;
            PathDirection direction = moduleIndex == 0 ? PathDirection.Down : moduleInfo.EntryDirection;
            PathDirection entryDirection = direction;

            var usedDirections = new HashSet<PathDirection> { direction };
            var visitedTiles = new HashSet<Vector2Int>();
            var pathTiles = new HashSet<Vector2Int>();

            applyCave &= Settings.CavesEnabled;
            bool hasReachedCenter = false;

            // Si se entra por un lateral, hay que dar al menos un paso antes de poder girar.
            bool hasMovedFromEntry = direction == PathDirection.Down;

            TagModuleEntry(moduleInfo, moduleIndex, entryDirection);

            int maxSteps = grid.Width + grid.Height;
            for (int step = 0; step < maxSteps; step++)
            {
                while (Pacer.IsPaused)
                {
                    yield return null;
                }

                // Nunca se pisa una casilla dos veces.
                if (!visitedTiles.Add(tile))
                {
                    break;
                }

                pathTiles.Add(tile);
                ClearTile(cubes, tile);

                if (grid.IsCenter(tile) && hasMovedFromEntry)
                {
                    hasReachedCenter = true;
                    direction = DetermineNextDirection(tile, direction, usedDirections, context);

                    if (usedDirections.Count == 3)
                    {
                        usedDirections.Clear();
                        usedDirections.Add(direction);
                    }
                }

                hasMovedFromEntry = true;

                if (TryGetBranchDirection(moduleIndex, step, tile, direction, entryDirection, context, out PathDirection branchDirection))
                {
                    // La rama encola su propio módulo ya, con su propio estado, y se excava aparte.
                    Vector2Int branchExit = grid.WalkToBoundary(tile, branchDirection);
                    _mapGenerator.EnqueueNextModule(branchExit, branchDirection, moduleInfo.Position);
                    _mapGenerator.StartCoroutine(GenerateBranch(cubes, tile, branchDirection, pathTiles));
                }

                tile = grid.Step(tile, direction);

                if (grid.IsExitBoundary(tile))
                {
                    pathTiles.Add(tile);
                    ClearTile(cubes, tile);

                    if (applyCave && hasReachedCenter)
                    {
                        CarveCave(cubes, pathTiles);
                    }

                    TagModuleExit(moduleInfo, direction);

                    // El último módulo no continúa: se tapa su salida con un módulo macizo.
                    bool isLastModule = moduleIndex >= Settings.ModuleCount - 1;
                    _mapGenerator.EnqueueNextModule(tile, direction, moduleInfo.Position, isBlocker: isLastModule);
                    yield break;
                }

                if (!Pacer.IsInstant)
                {
                    yield return Pacer.StepDelay;
                }
            }
        }

        // ---------- Bifurcaciones ----------

        private bool TryGetBranchDirection(int moduleIndex, int step, Vector2Int tile, PathDirection direction,
            PathDirection entryDirection, PathGenerationContext context, out PathDirection branchDirection)
        {
            branchDirection = PathDirection.Down;

            // moduleIndex empieza en 0 y BranchingStartsAtModule en 1.
            if (moduleIndex < Settings.BranchingStartsAtModule - 1)
            {
                return false;
            }

            // Primer módulo: bifurcación hacia un lado nada más salir del centro.
            if (moduleIndex == 0 && step == 0 && Grid.IsCenter(tile))
            {
                branchDirection = context.Random.Next(0, 2) == 0 ? PathDirection.Left : PathDirection.Right;
                return true;
            }

            if (direction == PathDirection.Down)
            {
                return false;
            }

            // Resto: en los giros de cada tercer módulo, si la rama va hacia una tercera dirección distinta.
            branchDirection = GetBranchDirection(direction, entryDirection);
            return moduleIndex % 3 == 0 && moduleIndex != 0
                && direction != entryDirection
                && direction != branchDirection
                && entryDirection != branchDirection;
        }

        private static PathDirection GetBranchDirection(PathDirection direction, PathDirection entryDirection)
        {
            foreach (PathDirection candidate in new[] { PathDirection.Down, PathDirection.Left, PathDirection.Right })
            {
                if (candidate != direction && candidate != entryDirection)
                {
                    return candidate;
                }
            }

            // Si entrada y salida coinciden, la rama va al lado contrario.
            switch (direction)
            {
                case PathDirection.Left: return PathDirection.Right;
                case PathDirection.Right: return PathDirection.Left;
                default: return PathDirection.Down;
            }
        }

        /// <summary>Excava la rama en línea recta hasta el borde del módulo.</summary>
        private IEnumerator GenerateBranch(GameObject[,] cubes, Vector2Int start, PathDirection direction, HashSet<Vector2Int> pathTiles)
        {
            ModuleGrid grid = Grid;
            Vector2Int tile = start;

            while (!grid.IsExitBoundary(tile))
            {
                while (Pacer.IsPaused)
                {
                    yield return null;
                }

                pathTiles.Add(tile);
                ClearTile(cubes, tile);

                tile = grid.Step(tile, direction);

                if (grid.IsExitBoundary(tile))
                {
                    pathTiles.Add(tile);
                    ClearTile(cubes, tile);
                    yield break;
                }

                if (!Pacer.IsInstant)
                {
                    yield return Pacer.StepDelay;
                }
            }
        }

        // ---------- Elección de dirección ----------

        /// <summary>
        /// Elige la dirección al llegar al centro: 50 % recto, 25 % izquierda, 25 % derecha.
        /// No permite repetir la misma dirección más de dos veces seguidas.
        /// </summary>
        private PathDirection DetermineNextDirection(Vector2Int tile, PathDirection direction,
            HashSet<PathDirection> usedDirections, PathGenerationContext context)
        {
            int roll = context.Random.Next(0, 10);
            PathDirection preferred = roll < 5 ? PathDirection.Down : (roll < 7 ? PathDirection.Left : PathDirection.Right);
            PathDirection chosen = ResolveDirection(preferred, tile, direction);

            if (chosen == direction)
            {
                if (context.IsRepeating)
                {
                    return DetermineNextDirection(tile, chosen, usedDirections, context);
                }
                context.IsRepeating = true;
            }
            else
            {
                context.IsRepeating = false;
            }

            usedDirections.Add(chosen);
            return chosen;
        }

        /// <summary>Usa la dirección preferida si cabe; si no, la primera alternativa válida.</summary>
        private PathDirection ResolveDirection(PathDirection preferred, Vector2Int tile, PathDirection direction)
        {
            ModuleGrid grid = Grid;
            bool canGoDown = tile.y < grid.Height - 1;
            bool canGoLeft = tile.x > 1 && direction != PathDirection.Right;
            bool canGoRight = tile.x < grid.Width - 2 && direction != PathDirection.Left;

            if (preferred == PathDirection.Down && canGoDown) return PathDirection.Down;
            if (preferred == PathDirection.Left && canGoLeft) return PathDirection.Left;
            if (preferred == PathDirection.Right && canGoRight) return PathDirection.Right;

            if (canGoDown) return PathDirection.Down;
            if (canGoLeft) return PathDirection.Left;
            if (canGoRight) return PathDirection.Right;

            return direction;
        }

        // ---------- Cubos y metadatos ----------

        private void ClearTile(GameObject[,] cubes, Vector2Int tile)
        {
            if (Grid.Contains(tile) && cubes[tile.x, tile.y] != null)
            {
                _pool.ReturnObject(cubes[tile.x, tile.y]);
                cubes[tile.x, tile.y] = null;
            }
        }

        private void CarveCave(GameObject[,] cubes, HashSet<Vector2Int> pathTiles)
        {
            ModuleGrid grid = Grid;
            bool[,] mask = CaveGenerator.SquareCaveMask(grid, grid.Center, CaveInnerSize, Settings.CaveSize, pathTiles);

            for (int x = 0; x < grid.Width; x++)
            {
                for (int z = 0; z < grid.Height; z++)
                {
                    if (mask[x, z])
                    {
                        ClearTile(cubes, new Vector2Int(x, z));
                    }
                }
            }
        }

        private static void TagModuleEntry(ModuleInfo moduleInfo, int moduleIndex, PathDirection entryDirection)
        {
            GeneratedModulePathInfo info = GetOrAddPathInfo(moduleInfo);
            if (info != null) info.SetEntry(moduleIndex, entryDirection);
        }

        private static void TagModuleExit(ModuleInfo moduleInfo, PathDirection exitDirection)
        {
            GeneratedModulePathInfo info = GetOrAddPathInfo(moduleInfo);
            if (info != null) info.SetExit(exitDirection);
        }

        private static GeneratedModulePathInfo GetOrAddPathInfo(ModuleInfo moduleInfo)
        {
            if (moduleInfo.Root == null) return null;

            GeneratedModulePathInfo info = moduleInfo.Root.GetComponent<GeneratedModulePathInfo>();
            return info != null ? info : moduleInfo.Root.gameObject.AddComponent<GeneratedModulePathInfo>();
        }
    }
}
