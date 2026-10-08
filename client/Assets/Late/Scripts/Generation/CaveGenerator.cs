using System.Collections.Generic;
using UnityEngine;

namespace Late.Generation
{
    /// <summary>
    /// Excava "cuevas": salas abiertas dentro de un módulo que rompen la monotonía de los pasillos.
    /// </summary>
    public static class CaveGenerator
    {
        /// <summary>
        /// Máscara de una sala cuadrada alrededor de <paramref name="center"/> (distancia de Chebyshev).
        /// true = quitar el cubo. Las casillas de <paramref name="protectedTiles"/> nunca se marcan.
        /// </summary>
        public static bool[,] SquareCaveMask(ModuleGrid grid, Vector2Int center, float innerSize, float outerSize,
            HashSet<Vector2Int> protectedTiles = null)
        {
            bool[,] mask = new bool[grid.Width, grid.Height];

            for (int x = 0; x < grid.Width; x++)
            {
                for (int z = 0; z < grid.Height; z++)
                {
                    if (protectedTiles != null && protectedTiles.Contains(new Vector2Int(x, z)))
                    {
                        continue;
                    }

                    float distance = Mathf.Max(Mathf.Abs(x - center.x), Mathf.Abs(z - center.y));
                    mask[x, z] = distance < innerSize || distance <= outerSize;
                }
            }

            return mask;
        }
    }
}
