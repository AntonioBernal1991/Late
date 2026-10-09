using System;
using System.Collections;
using Late.Audio;
using Late.Core;
using Late.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Late.Game
{
    /// <summary>
    /// Flujo de la partida. Vive en la escena principal (menú) y sobrevive a los cambios de escena.
    ///
    /// Al empezar precarga Run0 detrás del menú. Al pulsar jugar: carga el nivel de forma aditiva,
    /// coloca al jugador en StartPosition, aplica el RunTuning del nivel, arranca la música y el
    /// avance automático. Al cruzar la meta publica el resultado en <see cref="RunFinished"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>Se lanza al cruzar la meta con el resultado de la partida.</summary>
        public static event Action<RunResult> RunFinished;

        private const string StartPositionName = "StartPosition";
        private const string EyeName = "Eye";

        [Header("Ciclo de vida")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Niveles")]
        [Tooltip("Nombres de las escenas de nivel por índice. Si está vacío se usa Run0, Run1, Run2...")]
        [SerializeField] private string[] runSceneNames;
        [Tooltip("Escena del primer nivel (tiene que estar en Build Settings).")]
        [SerializeField] private string run0SceneName = "Run0";
        [Tooltip("Precarga Run0 al arrancar y coloca la cámara, pero sin empezar la partida.")]
        [SerializeField] private bool loadRun0OnStart = true;
        [SerializeField] private bool logStartup = false;
        [Tooltip("Descarga el nivel anterior ANTES de cargar el siguiente: menos memoria, pero puede verse un frame vacío sin overlay.")]
        [SerializeField] private bool unloadPreviousBeforeLoad = false;

        [Header("Jugador")]
        [Tooltip("Objeto que se coloca en StartPosition. Si está vacío, la cámara principal.")]
        [SerializeField] private Transform playerRig;
        [Tooltip("Si está vacío, se busca en el jugador o la cámara principal.")]
        [SerializeField] private AutoForwardCameraController autoForward;

        [Header("Ajustes por nivel")]
        [Tooltip("Un RunTuning por nivel: índice 0 = Run0, índice 1 = Run1...")]
        [SerializeField] private RunTuning[] runTuningsByIndex;

        [Header("Cámara")]
        [Tooltip("FOV al cargar cada nivel, para que no arrastre el de la secuencia final.")]
        [SerializeField] private float defaultFovOnRunLoad = 105f;

        [Header("Música")]
        [Tooltip("Objeto del reproductor de música; se activa al empezar la partida.")]
        [SerializeField] private GameObject musicManagerRoot;
        [Tooltip("Lo desactiva al arrancar para que la música no suene en el menú.")]
        [SerializeField] private bool disableMusicOnAwake = true;

        [Header("Pantalla de carga")]
        [Tooltip("CanvasGroup de una imagen negra a pantalla completa que tapa los cambios de nivel.")]
        [SerializeField] private CanvasGroup loadingOverlay;
        [Tooltip("Segundos del fundido al terminar de cargar. 0 = de golpe.")]
        [SerializeField] [Min(0f)] private float loadingOverlayFadeOutSeconds = 0.15f;

        [Header("Eventos")]
        [Tooltip("Al empezar la partida (nivel cargado, jugador colocado, música y avance activos).")]
        [SerializeField] private UnityEvent onRunStarted;
        [Tooltip("Al cruzar la meta.")]
        [SerializeField] private UnityEvent onEndSequenceStarted;

        private RunSceneLoader _sceneLoader;
        private LoadingOverlay _overlay;
        private Coroutine _runRoutine;
        private int _currentRunIndex;
        private string _loadedRunSceneName;
        private bool _runStarted;
        private float _runStartTime;

        public int CurrentRunIndex => _currentRunIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // La escena de arranque se guarda antes de pasar este objeto a DontDestroyOnLoad.
            _sceneLoader = new RunSceneLoader(SceneManager.GetActiveScene(), logStartup);
            _overlay = new LoadingOverlay(loadingOverlay, this);

            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            if (disableMusicOnAwake && musicManagerRoot != null)
            {
                musicManagerRoot.SetActive(false);
            }
        }

        private void OnEnable() => RunEvents.EndReached += OnEndReached;

        private void OnDisable() => RunEvents.EndReached -= OnEndReached;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (loadRun0OnStart)
            {
                LoadRun(0, startRun: false);
            }
        }

        // ---------- API para los botones de la UI ----------

        /// <summary>
        /// Botón de jugar. La primera vez empieza el nivel actual. Después pasa al siguiente si está
        /// desbloqueado; si no, repite el actual desde cero.
        /// </summary>
        public void StartRun()
        {
            if (!_runStarted)
            {
                LoadRun(_currentRunIndex, startRun: true);
                return;
            }

            int next = _currentRunIndex + 1;
            string nextScene = GetRunSceneName(next);
            bool canAdvance = LevelManager.Instance != null
                && !string.IsNullOrWhiteSpace(nextScene)
                && Application.CanStreamedLevelBeLoaded(nextScene)
                && LevelManager.Instance.IsUnlocked(next);

            if (canAdvance) LoadRun(next, startRun: true);
            else LoadRun(_currentRunIndex, startRun: true, forceReload: true);
        }

        public void StartRun0() => LoadRun(0, startRun: true);

        /// <summary>Empieza un nivel concreto (selector de niveles).</summary>
        public void StartRunIndex(int runIndex)
        {
            LoadRun(runIndex, startRun: true, forceReload: _runStarted && runIndex == _currentRunIndex);
        }

        // ---------- Fin de partida ----------

        private void OnEndReached()
        {
            if (Instance != this) return;

            onEndSequenceStarted?.Invoke();

            // Se gana si la música sigue sonando al cruzar la meta.
            bool beatTheMusic = BackgroundMusicPlayer.Instance != null && BackgroundMusicPlayer.Instance.IsPlaying;
            var result = new RunResult(_currentRunIndex, Time.time - _runStartTime, beatTheMusic);
            if (logStartup) Debug.Log($"[GameManager] Fin de partida: {result}", this);

            RunFinished?.Invoke(result);
        }

        // ---------- Carga de niveles ----------

        private void LoadRun(int runIndex, bool startRun, bool forceReload = false)
        {
            _overlay.ShowImmediate();
            ResetCameraFov();

            if (_runRoutine != null) StopCoroutine(_runRoutine);
            _runRoutine = StartCoroutine(LoadRunRoutine(Mathf.Max(0, runIndex), startRun, forceReload));
        }

        /// <summary>
        /// Orden: descargar si toca → cargar → colocar al jugador → aplicar ajustes →
        /// (si empieza la partida) música y avance → descargar el nivel anterior → quitar la pantalla negra.
        /// </summary>
        private IEnumerator LoadRunRoutine(int runIndex, bool startRun, bool forceReload)
        {
            ResetCameraFov();

            // Sin moverse mientras se carga y se teletransporta.
            AutoForwardCameraController player = ResolveAutoForward();
            if (player != null) player.enabled = false;

            string sceneName = GetRunSceneName(runIndex);
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning($"[GameManager] No hay escena para el nivel {runIndex}.", this);
                yield break;
            }

            // Repetir el mismo nivel: se recarga entero para reiniciar triggers, ojo, eventos, etc.
            if (forceReload)
            {
                yield return _sceneLoader.Unload(sceneName);
            }

            bool isDifferentScene = !string.IsNullOrWhiteSpace(_loadedRunSceneName) && _loadedRunSceneName != sceneName;
            if (unloadPreviousBeforeLoad && isDifferentScene)
            {
                yield return _sceneLoader.Unload(_loadedRunSceneName);
            }

            yield return _sceneLoader.LoadAdditive(sceneName);

            if (!RunSceneLoader.TrySetActive(sceneName, out Scene runScene))
            {
                Debug.LogWarning($"[GameManager] No se ha cargado '{sceneName}'. Revisa Build Settings y el nombre exacto.", this);
            }

            BindEyeTarget(runScene);
            ResetCameraFov();
            bool positioned = TryPositionPlayerAtStart(runScene);
            PrepareRunScene(runScene, runIndex);

            // Por si algo crea o mueve StartPosition en su Start: se reintenta un frame después.
            yield return null;
            if (!positioned) TryPositionPlayerAtStart(runScene);
            PrepareRunScene(runScene, runIndex);

            if (startRun)
            {
                StartPlaying(runIndex);
            }

            // Por defecto el nivel anterior se descarga después, con el nuevo ya listo.
            if (!unloadPreviousBeforeLoad && isDifferentScene)
            {
                yield return _sceneLoader.Unload(_loadedRunSceneName);
            }

            _loadedRunSceneName = sceneName;
            _currentRunIndex = runIndex;
            if (startRun) _runStarted = true;
            _runRoutine = null;

            _overlay.FadeOut(loadingOverlayFadeOutSeconds);
        }

        private void PrepareRunScene(Scene runScene, int runIndex)
        {
            ResetCameraLookState();
            ResetEndTriggers(runScene);
            ApplyPlayerTuning(runIndex);
        }

        private void StartPlaying(int runIndex)
        {
            if (musicManagerRoot != null) musicManagerRoot.SetActive(true);

            BackgroundMusicPlayer music = BackgroundMusicPlayer.Instance;
            if (music != null)
            {
                ApplyMusicTuning(music, runIndex);
                music.PlayMusic(restart: true);
            }

            AutoForwardCameraController player = ResolveAutoForward();
            if (player != null) player.enabled = true;

            _runStartTime = Time.time;
            onRunStarted?.Invoke();
        }

        private string GetRunSceneName(int runIndex)
        {
            if (runSceneNames != null && runSceneNames.Length > 0)
            {
                return runIndex >= 0 && runIndex < runSceneNames.Length ? runSceneNames[runIndex] : null;
            }
            return runIndex == 0 ? run0SceneName : $"Run{runIndex}";
        }

        // ---------- Ajustes por nivel ----------

        private RunTuning GetRunTuning(int runIndex)
        {
            if (runTuningsByIndex == null || runIndex < 0 || runIndex >= runTuningsByIndex.Length) return null;
            return runTuningsByIndex[runIndex];
        }

        private void ApplyPlayerTuning(int runIndex)
        {
            RunTuning tuning = GetRunTuning(runIndex);
            AutoForwardCameraController player = ResolveAutoForward();
            if (tuning == null || player == null) return;

            if (tuning.overrideMoveSpeed) player.MoveSpeed = tuning.moveSpeed;
            if (tuning.overrideStopDistance) player.StopDistance = tuning.stopDistance;
            if (tuning.overrideTurnDuration) player.TurnDuration = tuning.turnDuration;
        }

        private void ApplyMusicTuning(BackgroundMusicPlayer music, int runIndex)
        {
            RunTuning tuning = GetRunTuning(runIndex);
            if (tuning == null) return;

            if (tuning.overrideMusicClip && tuning.musicClip != null) music.SetMusicClip(tuning.musicClip);
            if (tuning.overrideMusicStartAtSeconds) music.SetStartAtSeconds(tuning.musicStartAtSeconds);
        }

        // ---------- Jugador y cámara ----------

        private AutoForwardCameraController ResolveAutoForward()
        {
            if (autoForward != null) return autoForward;

            if (playerRig != null && playerRig.TryGetComponent(out autoForward)) return autoForward;
            if (Camera.main != null && Camera.main.TryGetComponent(out autoForward)) return autoForward;

            autoForward = FindObjectOfType<AutoForwardCameraController>();
            return autoForward;
        }

        private Transform ResolvePlayerTransform()
        {
            if (playerRig != null) return playerRig;
            if (Camera.main != null) return Camera.main.transform;

            AutoForwardCameraController player = ResolveAutoForward();
            return player != null ? player.transform : null;
        }

        private bool TryPositionPlayerAtStart(Scene runScene)
        {
            Transform start = SceneObjectFinder.FindInScene(runScene, StartPositionName);
            if (start == null)
            {
                Debug.LogWarning($"[GameManager] La escena '{runScene.name}' no tiene un objeto '{StartPositionName}'.", this);
                return false;
            }

            Transform player = ResolvePlayerTransform();
            if (player == null)
            {
                Debug.LogWarning("[GameManager] No hay jugador ni cámara principal que colocar.", this);
                return false;
            }

            // El CharacterController deshace los cambios de posición si está activo al teletransportar.
            CharacterController cc = player.GetComponent<CharacterController>();
            bool ccWasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;

            player.SetPositionAndRotation(start.position, start.rotation);
            if (logStartup) Debug.Log($"[GameManager] '{player.name}' colocado en {start.position}.", this);

            if (cc != null) cc.enabled = ccWasEnabled;
            return true;
        }

        /// <summary>El ojo es parte de cada nivel: hay que decirle a la cámara cuál mirar en la secuencia final.</summary>
        private void BindEyeTarget(Scene runScene)
        {
            Transform eye = SceneObjectFinder.FindInScene(runScene, EyeName);
            CameraLookAtOnKey look = GetCameraLookAt();

            if (eye != null && look != null)
            {
                look.SetTarget(eye);
            }
            else if (logStartup)
            {
                Debug.LogWarning($"[GameManager] No se ha podido asignar el ojo de '{runScene.name}' a la cámara.", this);
            }
        }

        private void ResetCameraFov()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            CameraLookAtOnKey look = cam.GetComponent<CameraLookAtOnKey>();
            if (look != null) look.ForceResetFov(defaultFovOnRunLoad);
            else cam.fieldOfView = defaultFovOnRunLoad;
        }

        /// <summary>
        /// Si la partida anterior acabó mirando al ojo, StartLookAtTarget no haría nada en la siguiente.
        /// Se toma la pose actual como la normal.
        /// </summary>
        private static void ResetCameraLookState()
        {
            CameraLookAtOnKey look = GetCameraLookAt();
            if (look != null) look.ForceResetLookStateToCurrentPose();
        }

        private static CameraLookAtOnKey GetCameraLookAt() =>
            Camera.main != null ? Camera.main.GetComponent<CameraLookAtOnKey>() : null;

        private static void ResetEndTriggers(Scene runScene)
        {
            if (!runScene.IsValid() || !runScene.isLoaded) return;

            foreach (GameObject root in runScene.GetRootGameObjects())
            {
                foreach (MazeEndTriggerLookAt trigger in root.GetComponentsInChildren<MazeEndTriggerLookAt>(true))
                {
                    trigger.ResetTriggerState();
                }
            }
        }
    }
}
