using UnityEngine;

namespace Late.Generation
{
    /// <summary>
    /// Controla el ritmo de la generación. En modo instantáneo no espera nunca. Si no, avanza
    /// paso a paso (útil para ver cómo se construye el laberinto) y puede pausarse hasta
    /// que se mantenga pulsado Espacio.
    /// </summary>
    public class GenerationPacer
    {
        private const float StepSeconds = 0.07f;

        private readonly bool _holdSpaceToGenerate;
        private readonly WaitForSeconds _stepDelay = new WaitForSeconds(StepSeconds);

        public GenerationPacer(bool generateInstantly, bool holdSpaceToGenerate)
        {
            IsInstant = generateInstantly;
            _holdSpaceToGenerate = holdSpaceToGenerate;
        }

        public bool IsInstant { get; }

        /// <summary>True mientras haya que esperar a que se pulse Espacio.</summary>
        public bool IsPaused => !IsInstant && _holdSpaceToGenerate && !Input.GetKey(KeyCode.Space);

        /// <summary>Espera entre pasos. Solo debe usarse cuando <see cref="IsInstant"/> es false.</summary>
        public WaitForSeconds StepDelay => _stepDelay;
    }
}
