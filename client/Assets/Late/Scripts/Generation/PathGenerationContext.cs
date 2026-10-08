using UnityEngine;

namespace Late.Generation
{
    /// <summary>
    /// Estado propio del camino de un módulo: su generador aleatorio y si acaba de repetir dirección.
    /// La semilla depende de la posición del módulo, así que cada módulo decide de forma
    /// independiente pero siempre igual para la misma semilla global.
    /// </summary>
    internal class PathGenerationContext
    {
        // La semilla se calcula como si el hueco entre módulos fuera siempre este valor.
        // Así, cambiar el hueco en la configuración no cambia la forma del laberinto.
        private const float NormalizedModuleSpacing = 1.2f;

        public System.Random Random { get; }
        public bool IsRepeating { get; set; }

        public PathGenerationContext(int baseSeed, Vector3 modulePosition, float moduleSpacing, Vector2 moduleWorldSize)
        {
            Vector3 normalized = NormalizeModulePosition(modulePosition, moduleSpacing, moduleWorldSize);
            int moduleSeed = Mathf.RoundToInt(normalized.x * 1000 + normalized.z * 10000);
            Random = new System.Random(baseSeed + moduleSeed);
        }

        private static Vector3 NormalizeModulePosition(Vector3 position, float moduleSpacing, Vector2 moduleWorldSize)
        {
            if (Mathf.Approximately(moduleSpacing, NormalizedModuleSpacing))
            {
                return position;
            }

            // Cuántos módulos hay entre el origen y esta posición en cada eje.
            // El umbral evita que errores de coma flotante cuenten como un paso.
            const float threshold = 0.1f;
            int stepsX = Mathf.Abs(position.x) > threshold ? Mathf.RoundToInt(position.x / (moduleWorldSize.x + moduleSpacing)) : 0;
            int stepsZ = Mathf.Abs(position.z) > threshold ? Mathf.RoundToInt(position.z / (moduleWorldSize.y + moduleSpacing)) : 0;

            return new Vector3(
                stepsX * (moduleWorldSize.x + NormalizedModuleSpacing),
                0f,
                stepsZ * (moduleWorldSize.y + NormalizedModuleSpacing));
        }
    }
}
