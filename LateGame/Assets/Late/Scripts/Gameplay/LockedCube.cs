using System.Collections;
using Late.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Late.Gameplay
{
    /// <summary>
    /// Puerta cerrada: un cubo que bloquea el camino hasta que el jugador llega con una llave.
    /// Entonces se desvanece y deja pasar. Mientras el jugador está delante sin llave se muestra un aviso.
    /// </summary>
    [DisallowMultipleComponent]
    public class LockedCube : MonoBehaviour
    {
        [Header("Apertura")]
        [Tooltip("Si está activo, abrir la puerta gasta la llave.")]
        [SerializeField] private bool consumeKeyOnUnlock = true;
        [Tooltip("Si está activo, el cubo se desvanece antes de desaparecer.")]
        [SerializeField] private bool fadeOutOnUnlock = true;
        [SerializeField] [Range(0f, 3f)] private float fadeSeconds = 1f;
        [Tooltip("Propiedad de color que se desvanece. Standard usa _Color; URP usa _BaseColor.")]
        [SerializeField] private string colorProperty = "_Color";
        [Tooltip("Si está activo, desactiva el GameObject al terminar (recomendado).")]
        [SerializeField] private bool deactivateOnUnlock = true;
        [SerializeField] private UnityEvent onUnlocked;

        [Header("Aviso de llave")]
        [Tooltip("Objeto (normalmente UI) que se muestra mientras el jugador está delante sin llave.")]
        [SerializeField] private GameObject activateWhileInside;
        [Tooltip("Si no hay aviso asignado, lo busca por nombre (también desactivado).")]
        [SerializeField] private bool autoFindPromptByName = true;
        [SerializeField] private string promptObjectName = "Look4Key";
        [Tooltip("Segundos mínimos entre búsquedas por nombre.")]
        [SerializeField] [Min(0f)] private float promptFindRetrySeconds = 0.5f;
        [Tooltip("Muestra el aviso solo mientras la puerta sigue cerrada.")]
        [SerializeField] private bool showOnlyWhileLocked = true;
        [Tooltip("Solo reacciona a la cámara principal.")]
        [SerializeField] private bool requireMainCamera = true;

        [Header("Depuración")]
        [SerializeField] private bool logUnlock = false;

        private bool _unlocked;
        private bool _unlocking;
        private float _lastPromptFindAttempt = -999f;
        private MaterialPropertyBlock _mpb;
        private FadeTarget[] _fadeTargets;

        private struct FadeTarget
        {
            public Renderer Renderer;
            public int PropertyId;
            public Color StartColor;
        }

        public bool IsUnlocked => _unlocked;
        public bool IsUnlocking => _unlocking;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _fadeTargets = CollectFadeTargets();
            ResolvePrompt(force: true);
        }

        /// <summary>
        /// Lo llama el jugador cuando detecta la puerta delante. Sirve aunque se pare antes de
        /// entrar en el trigger.
        /// </summary>
        public void SetProximityPromptActive(bool active)
        {
            ResolvePrompt();
            bool canShow = !_unlocking && !(showOnlyWhileLocked && _unlocked);
            SetPromptActive(active && canShow);
        }

        /// <summary>Intenta abrir la puerta con la llave del jugador. True si se abre o ya estaba abierta.</summary>
        public bool TryUnlock(PlayerKeyInventory keys)
        {
            if (_unlocked || _unlocking) return true;
            if (keys == null || !keys.HasKey) return false;

            if (consumeKeyOnUnlock) keys.ConsumeKey();

            _unlocking = true;
            if (logUnlock) Debug.Log($"[LockedCube] Abierta '{name}'.", this);

            onUnlocked?.Invoke();
            SetPromptActive(false);
            StartCoroutine(UnlockRoutine());
            return true;
        }

        private IEnumerator UnlockRoutine()
        {
            if (fadeOutOnUnlock)
            {
                float duration = Mathf.Max(0f, fadeSeconds);
                for (float t = 0f; t < duration; t += Time.deltaTime)
                {
                    ApplyAlpha(1f - t / duration);
                    yield return null;
                }
                ApplyAlpha(0f);
            }

            _unlocked = true;
            _unlocking = false;

            // Ya no bloquea.
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;

            if (deactivateOnUnlock)
            {
                gameObject.SetActive(false);
            }
            else
            {
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
        }

        // ---------- Trigger: el jugador dentro de la puerta ----------

        private void OnTriggerEnter(Collider other)
        {
            ResolvePrompt();
            if (activateWhileInside == null) return;
            if (_unlocking) { SetPromptActive(false); return; }
            if (showOnlyWhileLocked && _unlocked) return;

            PlayerKeyInventory keys = FindPlayer(other);
            if (keys == null) return;

            // Con llave no hace falta el aviso.
            SetPromptActive(!keys.HasKey);
        }

        private void OnTriggerExit(Collider other)
        {
            ResolvePrompt();
            if (activateWhileInside == null) return;
            if (FindPlayer(other) != null) SetPromptActive(false);
        }

        private PlayerKeyInventory FindPlayer(Collider other)
        {
            if (other == null) return null;

            PlayerKeyInventory keys = other.GetComponentInParent<PlayerKeyInventory>();
            if (keys == null) return null;

            if (requireMainCamera)
            {
                Camera cam = other.GetComponentInParent<Camera>();
                if (cam == null || cam != Camera.main) return null;
            }
            return keys;
        }

        // ---------- Aviso ----------

        private void SetPromptActive(bool active)
        {
            if (activateWhileInside != null && activateWhileInside.activeSelf != active)
            {
                activateWhileInside.SetActive(active);
            }
        }

        private void ResolvePrompt(bool force = false)
        {
            if (activateWhileInside != null || !autoFindPromptByName) return;
            if (!force && Time.unscaledTime - _lastPromptFindAttempt < promptFindRetrySeconds) return;

            _lastPromptFindAttempt = Time.unscaledTime;
            activateWhileInside = SceneObjectFinder.FindByNameIncludingInactive(promptObjectName);
        }

        // ---------- Desvanecido ----------

        private FadeTarget[] CollectFadeTargets()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            var targets = new System.Collections.Generic.List<FadeTarget>(renderers.Length);

            foreach (Renderer r in renderers)
            {
                Material m = r.sharedMaterial;
                if (m == null) continue;

                string property = FindColorProperty(m);
                if (property == null) continue;

                targets.Add(new FadeTarget
                {
                    Renderer = r,
                    PropertyId = Shader.PropertyToID(property),
                    StartColor = m.GetColor(property)
                });
            }
            return targets.ToArray();
        }

        private string FindColorProperty(Material m)
        {
            if (!string.IsNullOrWhiteSpace(colorProperty) && m.HasProperty(colorProperty)) return colorProperty;
            if (m.HasProperty("_Color")) return "_Color";
            if (m.HasProperty("_BaseColor")) return "_BaseColor";
            return null;
        }

        private void ApplyAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            foreach (FadeTarget target in _fadeTargets)
            {
                if (target.Renderer == null) continue;

                Color c = target.StartColor;
                c.a *= alpha;

                target.Renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(target.PropertyId, c);
                target.Renderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
