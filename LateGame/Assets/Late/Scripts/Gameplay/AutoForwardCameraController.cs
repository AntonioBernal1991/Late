using UnityEngine;

namespace Late.Gameplay
{
    /// <summary>
    /// El jugador es la cámara. Avanza solo por el plano XZ y el jugador únicamente decide cuándo girar
    /// 90° (A/D o swipe). Se para ante una pared de frente y se desliza si la roza en diagonal.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class AutoForwardCameraController : MonoBehaviour
    {
        [Header("Avance")]
        [SerializeField] private float moveSpeed = 3.5f;
        [Tooltip("Distancia extra por delante para considerar que el camino está bloqueado.")]
        [SerializeField] private float stopDistance = 0.15f;
        [Tooltip("Capas que cuentan como paredes.")]
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Colisiones")]
        [Tooltip("Se para solo si la pared está de frente. Es dot(normal, avance): -1 = de frente, 0 = pared lateral.")]
        [SerializeField] [Range(-1f, 0f)] private float stopWhenFacingDotIsAtMost = -0.85f;
        [Tooltip("Si la pared está algo de frente pero no del todo, se desliza en vez de pararse. Más cerca de 0 = desliza más.")]
        [SerializeField] [Range(-1f, 0f)] private float slideWhenFacingDotIsAtMost = -0.2f;

        [Header("Giros")]
        [Tooltip("Segundos que tarda un giro de 90°. 0 = instantáneo.")]
        [SerializeField] private float turnDuration = 0.12f;
        [Tooltip("Permite girar. Se desactiva en secuencias como la meta.")]
        [SerializeField] private bool turningEnabled = true;

        [Header("Llaves")]
        [Tooltip("Muestra el aviso de las puertas cerradas solo si NO se lleva llave.")]
        [SerializeField] private bool showLockedPromptOnlyWhenNoKey = true;

        [Header("Giros en móvil (swipe)")]
        [SerializeField] private bool enableSwipeTurns = true;
        [Tooltip("Distancia mínima del gesto, en píxeles.")]
        [SerializeField] private float swipeMinPixels = 80f;
        [Tooltip("El gesto tiene que ser sobre todo horizontal: |dx| >= esto * |dy|.")]
        [SerializeField] private float swipeHorizontalDominance = 1.5f;
        [Tooltip("Duración máxima del gesto en segundos. 0 = sin límite.")]
        [SerializeField] private float swipeMaxTime = 0.5f;
        [Tooltip("En el editor, arrastrar con el ratón simula el swipe.")]
        [SerializeField] private bool swipeEnableMouseInEditor = true;

        [Header("Depuración")]
        [SerializeField] private bool debugDraw = false;

        [Header("Render")]
        [Tooltip("Desactiva el occlusion culling de la cámara: con niveles procedurales provoca huecos que aparecen y desaparecen.")]
        [SerializeField] private bool disableOcclusionCulling = true;

        private CharacterController _cc;
        private PlayerKeyInventory _keys;
        private readonly SwipeDetector _swipe = new SwipeDetector();
        private LockedCube _lockedPromptTarget;

        private bool _isTurning;
        private Quaternion _turnFrom;
        private Quaternion _turnTo;
        private float _turnT;

        public PlayerKeyInventory Keys => _keys;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        public float StopDistance
        {
            get => stopDistance;
            set => stopDistance = Mathf.Max(0f, value);
        }

        public float TurnDuration
        {
            get => turnDuration;
            set => turnDuration = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _keys = GetComponent<PlayerKeyInventory>();
            if (_keys == null) _keys = gameObject.AddComponent<PlayerKeyInventory>();

            if (disableOcclusionCulling && TryGetComponent(out Camera cam))
            {
                cam.useOcclusionCulling = false;
            }
        }

        private void Update()
        {
            HandleTurningInput();
            TickTurn(Time.deltaTime);
            TickMove(Time.deltaTime);
        }

        /// <summary>Activa o desactiva los giros (A/D y swipe). Para secuencias como la meta.</summary>
        public void SetTurningEnabled(bool enabled, bool cancelCurrentTurn = true)
        {
            turningEnabled = enabled;
            _swipe.Cancel();

            if (!enabled && cancelCurrentTurn)
            {
                _isTurning = false;
            }
        }

        // ---------- Giros ----------

        private void HandleTurningInput()
        {
            if (!turningEnabled)
            {
                _swipe.Cancel();
                return;
            }
            if (_isTurning) return;

            int swipe = ReadSwipe();
            if (Input.GetKeyDown(KeyCode.A) || swipe < 0)
            {
                StartTurn(-90f);
            }
            else if (Input.GetKeyDown(KeyCode.D) || swipe > 0)
            {
                StartTurn(90f);
            }
        }

        private int ReadSwipe()
        {
            if (!enableSwipeTurns) return 0;

            _swipe.MinPixels = swipeMinPixels;
            _swipe.HorizontalDominance = swipeHorizontalDominance;
            _swipe.MaxTime = swipeMaxTime;
            _swipe.MouseInEditor = swipeEnableMouseInEditor;
            return _swipe.Tick();
        }

        private void StartTurn(float deltaYawDegrees)
        {
            _isTurning = true;
            _turnT = 0f;
            _turnFrom = transform.rotation;
            _turnTo = Quaternion.Euler(0f, deltaYawDegrees, 0f) * _turnFrom;

            if (turnDuration <= 0f)
            {
                transform.rotation = _turnTo;
                _isTurning = false;
            }
        }

        private void TickTurn(float dt)
        {
            if (!_isTurning || turnDuration <= 0f) return;

            _turnT += dt / Mathf.Max(0.0001f, turnDuration);
            float t = Mathf.Clamp01(_turnT);
            transform.rotation = Quaternion.Slerp(_turnFrom, _turnTo, t);

            if (t >= 1f)
            {
                _isTurning = false;
            }
        }

        // ---------- Avance ----------

        private void TickMove(float dt)
        {
            if (_isTurning || moveSpeed <= 0f) return;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return;
            forward.Normalize();

            Vector3 moveDir = GetMoveDirection(forward);
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                _cc.Move(moveDir * (moveSpeed * dt));
            }
        }

        /// <summary>
        /// Lanza una cápsula con la forma del CharacterController hacia delante y decide si avanzar,
        /// pararse o deslizarse. De paso gestiona las puertas cerradas que tenga delante.
        /// </summary>
        private Vector3 GetMoveDirection(Vector3 planarForward)
        {
            Vector3 center = transform.TransformPoint(_cc.center);
            float skin = Mathf.Max(0f, _cc.skinWidth);
            float radius = Mathf.Max(0.01f, (_cc.radius - skin) * 0.95f);
            float height = Mathf.Max(_cc.height, radius * 2f);
            float capOffset = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 p1 = center + Vector3.up * capOffset;
            Vector3 p2 = center - Vector3.up * capOffset;

            // stopDistance es el hueco que se deja entre la cápsula y la pared.
            float distance = Mathf.Max(0f, stopDistance) + skin + 0.02f;

            if (debugDraw)
            {
                Debug.DrawLine(center, center + planarForward * (distance + radius), Color.yellow);
            }

            bool hit = Physics.CapsuleCast(p1, p2, radius, planarForward, out RaycastHit info, distance, obstacleMask, triggerInteraction);

            // Sin choque, o choque con el borde del suelo (normal casi vertical): seguir recto.
            if (!hit || Vector3.Dot(info.normal, Vector3.up) > 0.75f)
            {
                UpdateLockedPrompt(null, false);
                return planarForward;
            }

            // -1 = pared de frente, 0 = pared lateral.
            float facing = Vector3.Dot(info.normal, planarForward);
            bool isSideContact = facing > slideWhenFacingDotIsAtMost;

            HandleLockedCube(info.collider.GetComponentInParent<LockedCube>(), isSideContact);

            if (isSideContact) return planarForward;
            if (facing <= stopWhenFacingDotIsAtMost) return Vector3.zero;

            // Roce en diagonal: deslizarse a lo largo de la pared.
            Vector3 slide = Vector3.ProjectOnPlane(planarForward, info.normal);
            slide.y = 0f;
            return slide.sqrMagnitude < 0.0001f ? Vector3.zero : slide.normalized;
        }

        // ---------- Puertas cerradas ----------

        private void HandleLockedCube(LockedCube locked, bool isSideContact)
        {
            bool showPrompt = locked != null && !locked.IsUnlocked && !locked.IsUnlocking
                && !(showLockedPromptOnlyWhenNoKey && _keys.HasKey)
                && !isSideContact;
            UpdateLockedPrompt(locked, showPrompt);

            // Con llave, la puerta se desvanece. Sigue bloqueando hasta que termina de desaparecer.
            if (locked != null && _keys.HasKey && locked.TryUnlock(_keys))
            {
                UpdateLockedPrompt(locked, false);
            }
        }

        private void UpdateLockedPrompt(LockedCube locked, bool active)
        {
            if (_lockedPromptTarget != null && _lockedPromptTarget != locked)
            {
                _lockedPromptTarget.SetProximityPromptActive(false);
            }

            _lockedPromptTarget = locked;
            if (_lockedPromptTarget != null)
            {
                _lockedPromptTarget.SetProximityPromptActive(active);
            }
        }
    }
}
