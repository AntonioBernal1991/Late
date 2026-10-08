using UnityEngine;

namespace Late.Generation
{
    /// <summary>
    /// Cuadrícula de casillas de un módulo. X va de izquierda a derecha y Z (la "y" del Vector2Int)
    /// en la dirección de avance (Down). Un camino sale del módulo al tocar el borde inferior o un lateral.
    /// </summary>
    public readonly struct ModuleGrid
    {
        public int Width { get; }
        public int Height { get; }

        public ModuleGrid(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public Vector2Int Center => new Vector2Int(Width / 2, Height / 2);

        public bool IsCenter(Vector2Int tile) => tile == Center;

        public bool Contains(Vector2Int tile) =>
            tile.x >= 0 && tile.x < Width && tile.y >= 0 && tile.y < Height;

        public bool IsExitBoundary(Vector2Int tile) =>
            tile.y == Height - 1 || tile.x == 0 || tile.x == Width - 1;

        /// <summary>Avanza una casilla en la dirección dada sin salirse por los lados.</summary>
        public Vector2Int Step(Vector2Int tile, PathDirection direction)
        {
            switch (direction)
            {
                case PathDirection.Left: return new Vector2Int(Mathf.Max(0, tile.x - 1), tile.y);
                case PathDirection.Right: return new Vector2Int(Mathf.Min(Width - 1, tile.x + 1), tile.y);
                default: return new Vector2Int(tile.x, tile.y + 1);
            }
        }

        /// <summary>Casilla del borde a la que se llega avanzando en línea recta.</summary>
        public Vector2Int WalkToBoundary(Vector2Int start, PathDirection direction)
        {
            Vector2Int tile = start;
            while (!IsExitBoundary(tile))
            {
                tile = Step(tile, direction);
            }
            return tile;
        }

        /// <summary>Casilla por la que entra el camino al módulo vecino, según por dónde salió de este.</summary>
        public Vector2Int EntryTileFromExit(Vector2Int exitTile, PathDirection exitDirection)
        {
            switch (exitDirection)
            {
                case PathDirection.Down: return new Vector2Int(exitTile.x, 0);
                case PathDirection.Left: return new Vector2Int(Width - 1, exitTile.y);
                default: return new Vector2Int(0, exitTile.y);
            }
        }
    }
}
