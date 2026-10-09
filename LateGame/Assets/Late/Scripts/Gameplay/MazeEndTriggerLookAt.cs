using System.Collections;
using UnityEngine;

namespace Late.Gameplay
{
    /// <summary>
    /// Meta del laberinto (un collider de tipo trigger). Al cruzarla avisa de que la partida ha
    /// terminado, para al jugador y gira la cámara hacia el ojo.
    /// </summary>
    [DisallowMultipleComponent]
    public class MazeEndTriggerLookAt : MonoBehaviour
    {
        [Tooltip("Si está vacío, usa el CameraLookAtOnKey de la cámara principal.")]
        [SerializeField] private CameraLookAtOnKey cameraLookAt;

        [Header("Quién la activa")]
        [Tooltip("Solo se activa si entra este objeto (o un hijo suyo).")]
        [SerializeField] private Transform requiredRoot;
        [Tooltip("Si no hay Required Root, solo se activa con la cámara principal.")]
        [SerializeField] private bool requireMainCamera = true;
        [Tooltip("Tag que tiene que tener quien entra (o un padre). Vacío = cualquiera.")]
        [SerializeField] private string requiredTag = "";

        [Header("Comportamiento")]
        [Tooltip("Espera antes de girar la cámara hacia el ojo.")]
        [SerializeField] private float delaySeconds = 0f;
        [Tooltip("Solo se activa una vez por partida.")]
        [SerializeField] private bool triggerOnce = true;
        [Tooltip("Desactiva el avance automático al cruzarla, para que el jugador no se mueva durante la secuencia.")]
        [SerializeField] private bool disableAutoForwardOnTrigger = true;

        private bool _hasTriggered;

        /// <summary>Permite volver a activarla (al repetir el nivel sin recargar la escena).</summary>
        public void ResetTriggerState()
        {
            _hasTriggered = false;
        }

        private void Reset()
        {
            if (TryGetComponent(out Collider c)) c.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered && triggerOnce) return;
            if (other == null || !IsPlayer(other)) return;

            _hasTriggered = true;

            // Primero el aviso: el resultado depende de si la música sigue sonando en este instante.
            RunEvents.RaiseEndReached();

            if (disableAutoForwardOnTrigger && Camera.main != null
                && Camera.main.TryGetComponent(out AutoForwardCameraController player))
            {
                player.enabled = false;
            }

            StartCoroutine(LookAtEyeRoutine());
        }

        private bool IsPlayer(Collider other)
        {
            if (requiredRoot != null)
            {
                if (!other.transform.IsChildOf(requiredRoot)) return false;
            }
            else if (requireMainCamera)
            {
                Camera cam = other.GetComponentInParent<Camera>();
                if (cam == null || cam != Camera.main) return false;
            }

            if (string.IsNullOrWhiteSpace(requiredTag)) return true;

            for (Transform t = other.transform; t != null; t = t.parent)
            {
                if (t.CompareTag(requiredTag)) return true;
            }
            return false;
        }

        private IEnumerator LookAtEyeRoutine()
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            if (cameraLookAt == null && Camera.main != null)
            {
                cameraLookAt = Camera.main.GetComponent<CameraLookAtOnKey>();
            }

            if (cameraLookAt == null)
            {
                Debug.LogWarning("[MazeEndTriggerLookAt] No hay CameraLookAtOnKey: asígnalo en el Inspector.", this);
                yield break;
            }

            // Qué pantalla final se muestra se decide ahora, aunque se active cuando acabe la animación.
            cameraLookAt.CacheMusicStateForFovReachedActivation();
            cameraLookAt.StartLookAtTarget();
        }
    }
}
