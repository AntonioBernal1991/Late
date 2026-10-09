using Late.Core;
using UnityEngine;

namespace Late.Gameplay
{
    /// <summary>
    /// La llave que lleva el jugador (como mucho una) y el icono de la UI que lo indica.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerKeyInventory : MonoBehaviour
    {
        [Tooltip("Se pone a true al recoger una llave.")]
        [SerializeField] private bool hasKey = false;

        [Header("UI (opcional)")]
        [Tooltip("Icono de la UI que indica que se lleva la llave.")]
        [SerializeField] private GameObject keyUiObject;
        [Tooltip("Si no hay icono asignado, lo busca por nombre (también desactivado).")]
        [SerializeField] private bool autoFindKeyUiByName = true;
        [SerializeField] private string keyUiObjectName = "Key";
        [Tooltip("Segundos mínimos entre búsquedas por nombre.")]
        [SerializeField] [Min(0f)] private float keyUiFindRetrySeconds = 0.5f;

        private float _lastKeyUiFindAttempt = -999f;

        public bool HasKey => hasKey;

        private void Awake()
        {
            ResolveKeyUi(force: true);
        }

        public void GiveKey()
        {
            hasKey = true;
            SetKeyUiActive(true);
        }

        /// <summary>Gasta la llave. Devuelve false si no había ninguna.</summary>
        public bool ConsumeKey()
        {
            if (!hasKey) return false;
            hasKey = false;
            SetKeyUiActive(false);
            return true;
        }

        private void SetKeyUiActive(bool active)
        {
            ResolveKeyUi();
            if (keyUiObject != null && keyUiObject.activeSelf != active)
            {
                keyUiObject.SetActive(active);
            }
        }

        private void ResolveKeyUi(bool force = false)
        {
            if (keyUiObject != null || !autoFindKeyUiByName) return;
            if (!force && Time.unscaledTime - _lastKeyUiFindAttempt < keyUiFindRetrySeconds) return;

            _lastKeyUiFindAttempt = Time.unscaledTime;
            keyUiObject = SceneObjectFinder.FindByNameIncludingInactive(keyUiObjectName);
        }
    }
}
