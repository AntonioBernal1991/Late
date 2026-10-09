using UnityEngine;

namespace Late.Performance
{
    /// <summary>
    /// Ajustes de rendimiento para móvil que se aplican solos al arrancar, antes de cargar la primera escena.
    /// En Android Unity limita el juego a 30 FPS si no se pide otra cosa: aquí se piden 60 y se quitan
    /// los ajustes de calidad que más cuestan en un móvil.
    /// </summary>
    public static class MobilePerformanceBootstrap
    {
        private const int TargetFps = 60;

        // 1080x1920 son ~2 millones de píxeles; al 75 % quedan ~1,2 millones. Gran ahorro de GPU a cambio de algo de nitidez.
        private const float RenderScale = 0.75f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (!Application.isMobilePlatform) return;

            // Sin vSync: si no, el sistema puede limitar el juego por debajo del objetivo.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;

            QualitySettings.antiAliasing = 0;
            QualitySettings.pixelLightCount = 0;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.shadowDistance = 0f;

            // Resolución interna reducida.
            QualitySettings.resolutionScalingFixedDPIFactor = RenderScale;
            try
            {
                ScalableBufferManager.ResizeBuffers(RenderScale, RenderScale);
            }
            catch
            {
                // En algunos dispositivos no hay resolución dinámica; el factor fijo de arriba ya ayuda.
            }
        }
    }
}
