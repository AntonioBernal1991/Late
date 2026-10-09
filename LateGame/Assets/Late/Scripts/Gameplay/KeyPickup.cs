using UnityEngine;

namespace Late.Gameplay
{
    /// <summary>
    /// Llave recogible. Necesita un collider de tipo trigger: cuando el jugador lo atraviesa,
    /// la llave pasa a su inventario y desaparece del nivel.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyPickup : MonoBehaviour
    {
        [Tooltip("Si está activo, desactiva toda la llave al recogerla. Si no, solo sus renderers y colliders.")]
        [SerializeField] private bool deactivateGameObjectOnPickup = true;
        [Tooltip("Solo la puede recoger la cámara principal.")]
        [SerializeField] private bool requireMainCamera = true;
        [Tooltip("Tag que tiene que tener el jugador (o un padre). Vacío = cualquiera.")]
        [SerializeField] private string requiredTag = "";
        [Tooltip("Objeto extra que se activa al recogerla. El icono de la llave ya lo gestiona PlayerKeyInventory.")]
        [SerializeField] private GameObject activateOnPickup;

        [Header("Depuración")]
        [SerializeField] private bool logPickup = false;

        private void Reset()
        {
            if (TryGetComponent(out Collider c)) c.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            PlayerKeyInventory keys = other.GetComponentInParent<PlayerKeyInventory>();
            if (keys == null) return;

            if (requireMainCamera)
            {
                Camera cam = other.GetComponentInParent<Camera>();
                if (cam == null || cam != Camera.main) return;
            }

            if (!string.IsNullOrWhiteSpace(requiredTag) && !HasTagInParents(other.transform, requiredTag)) return;

            keys.GiveKey();
            if (logPickup) Debug.Log($"[KeyPickup] Recogida '{name}'.", this);

            if (activateOnPickup != null) activateOnPickup.SetActive(true);

            if (deactivateGameObjectOnPickup)
            {
                gameObject.SetActive(false);
            }
            else
            {
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
            }
        }

        private static bool HasTagInParents(Transform t, string tag)
        {
            for (; t != null; t = t.parent)
            {
                if (t.CompareTag(tag)) return true;
            }
            return false;
        }
    }
}
