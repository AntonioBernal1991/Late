using UnityEngine;

namespace Late.Game
{
    /// <summary>
    /// Ajustes de un nivel: velocidad del jugador y canción. Hay un asset por nivel y GameManager
    /// los aplica por índice al cargarlo. Cada ajuste solo se aplica si su "override" está activo.
    /// </summary>
    [CreateAssetMenu(menuName = "Late/Run Tuning", fileName = "RunTuning_Run0")]
    public class RunTuning : ScriptableObject
    {
        [Header("Jugador")]
        public bool overrideMoveSpeed = true;
        [Tooltip("Velocidad de avance (unidades/segundo). Cada nivel debería ser más rápido que el anterior.")]
        [Min(0f)] public float moveSpeed = 3.5f;

        public bool overrideStopDistance = false;
        [Tooltip("Hueco que se deja con la pared antes de pararse.")]
        [Min(0f)] public float stopDistance = 0.15f;

        public bool overrideTurnDuration = false;
        [Tooltip("Segundos que tarda un giro de 90°. 0 = instantáneo.")]
        [Min(0f)] public float turnDuration = 0.12f;

        [Header("Música")]
        public bool overrideMusicClip = false;
        [Tooltip("Canción del nivel: es el cronómetro.")]
        public AudioClip musicClip;

        public bool overrideMusicStartAtSeconds = false;
        [Tooltip("Segundo de la canción desde el que empieza a sonar.")]
        [Min(0f)] public float musicStartAtSeconds = 0f;
    }
}
