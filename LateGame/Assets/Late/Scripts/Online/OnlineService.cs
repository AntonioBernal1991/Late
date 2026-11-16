using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Late.Data;
using Late.Game;
using Late.Localization;
using L10n = Late.Localization.Localization;
using Late.Shared;
using UnityEngine;

namespace Late.Online
{
    /// <summary>
    /// Parte online del juego: sesión, envío de partidas pendientes y acceso a la API para la interfaz.
    /// Se crea sola al arrancar y vive toda la partida (DontDestroyOnLoad).
    ///
    /// Cuándo envía las pendientes: al arrancar, al terminar cada partida, al entrar en la cuenta y,
    /// mientras quede alguna, cada <see cref="OnlineSettings.RetrySeconds"/> (sin conexión no se pierde nada).
    /// </summary>
    public class OnlineService : MonoBehaviour
    {
        private const string LanguageKey = "idioma";

        public static OnlineService Instance { get; private set; }

        public OnlineSettings Settings { get; private set; }
        public LateApiClient Api { get; private set; }
        public AccountSession Session { get; private set; }

        /// <summary>Tras cada intento de envío (para refrescar la interfaz).</summary>
        public event Action<SyncOutcome> Synced;

        private SqliteProgressStore _store;
        private PendingResultsUploader _uploader;
        private bool _syncing;
        private float _nextRetryTime = float.MaxValue;

        // ---------- Arranque ----------

        /// <summary>Antes de cargar la escena, para que los textos salgan ya en el idioma guardado.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadLanguage()
        {
            SqliteProgressStore store = LocalDataBootstrap.OpenStore();
            if (store == null) return;

            if (L10n.TryParse(store.LoadSetting(LanguageKey), out Language saved))
            {
                L10n.Load(saved);
            }
            L10n.Saver = language => store.SaveSetting(LanguageKey, L10n.Code(language));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(OnlineService));
            DontDestroyOnLoad(go);
            go.AddComponent<OnlineService>();
        }

        private void Awake()
        {
            Instance = this;
            Settings = OnlineSettings.Load();
            Api = new LateApiClient(Settings.ServerUrl);
            _store = LocalDataBootstrap.OpenStore();
            Session = new AccountSession(_store);
            if (_store != null) _uploader = new PendingResultsUploader(_store, Api);

            GameManager.RunFinished += OnRunFinished;
            gameObject.AddComponent<OnlineMenu>();
        }

        private void OnDestroy()
        {
            GameManager.RunFinished -= OnRunFinished;
            if (Instance == this) Instance = null;
        }

        // Con la sesión guardada: subir lo pendiente y traer el progreso de la cuenta.
        private void Start()
        {
            if (Session.IsLoggedIn) _ = SyncAndLoadProgressAsync(switchedAccount: false);
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRetryTime) _ = SyncNowAsync();
        }

        // LevelManager guarda la partida en el mismo evento; se espera un momento para enviarla ya guardada.
        private void OnRunFinished(RunResult result) => Invoke(nameof(SyncSoon), 1f);

        private void SyncSoon() => _ = SyncNowAsync();

        // ---------- Partidas pendientes ----------

        public int PendingCount => _store?.LoadPendingResults().Count ?? 0;

        public async Task<SyncOutcome> SyncNowAsync()
        {
            if (_uploader == null) return new SyncOutcome(SyncStatus.ServerError, 0);
            if (!Session.IsLoggedIn) return new SyncOutcome(SyncStatus.NotLoggedIn, 0);
            if (_syncing) return new SyncOutcome(SyncStatus.Done, 0);

            _syncing = true;
            _nextRetryTime = float.MaxValue;
            SyncOutcome outcome;
            try
            {
                outcome = await _uploader.SyncAsync(Session.Token);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                outcome = new SyncOutcome(SyncStatus.ServerError, 0);
            }
            finally
            {
                _syncing = false;
            }

            if (outcome.Status == SyncStatus.SessionExpired) Session.SignOut();
            if (outcome.Status == SyncStatus.Offline || outcome.Status == SyncStatus.ServerError)
            {
                _nextRetryTime = Time.unscaledTime + Settings.RetrySeconds;
            }
            Synced?.Invoke(outcome);
            return outcome;
        }

        // ---------- Cuenta ----------

        public async Task<ApiResult<AuthResponse>> LoginAsync(string userNameOrEmail, string password)
        {
            ApiResult<AuthResponse> result = await Api.LoginAsync(new LoginRequest
            {
                UserNameOrEmail = userNameOrEmail?.Trim(),
                Password = password,
            });
            if (result.Ok) await OnSignedIn(result.Data);
            return result;
        }

        public async Task<ApiResult<AuthResponse>> RegisterAsync(string userName, string email, string password)
        {
            ApiResult<AuthResponse> result = await Api.RegisterAsync(new RegisterRequest
            {
                UserName = userName?.Trim(),
                Email = email?.Trim(),
                Password = password,
            });
            if (result.Ok) await OnSignedIn(result.Data);
            return result;
        }

        private async Task OnSignedIn(AuthResponse auth)
        {
            string previousOwner = _store?.LoadSetting(ProgressOwnerKey);
            bool switchedAccount = previousOwner != null &&
                                   !string.Equals(previousOwner, auth.UserName, StringComparison.OrdinalIgnoreCase);
            Session.SignIn(auth);

            if (switchedAccount && _store != null)
            {
                // Partidas de otra cuenta que no llegaron a enviarse (su sesión caducó sin conexión):
                // no se pueden atribuir a esta cuenta.
                _store.RemovePendingResults(_store.LoadPendingResults().Select(p => p.Id));
            }
            await SyncAndLoadProgressAsync(switchedAccount);
        }

        // ---------- Progreso de la cuenta ----------

        /// <summary>De qué cuenta es el progreso guardado en este dispositivo.</summary>
        public const string ProgressOwnerKey = "progreso.cuenta";

        /// <summary>
        /// Sube las partidas pendientes y después trae el progreso de la cuenta, para que el servidor ya
        /// cuente con ellas. Si la cuenta es otra, su progreso sustituye al del dispositivo.
        /// </summary>
        private async Task SyncAndLoadProgressAsync(bool switchedAccount)
        {
            await SyncNowAsync();
            if (_store == null || !Session.IsLoggedIn) return;

            ApiResult<ProgressResponse> progress = await Api.GetProgressAsync(Session.Token);
            if (!progress.Ok)
            {
                if (progress.Status == 401) Session.SignOut();
                return; // sin conexión: se juega con el progreso local y se reintenta al volver a entrar
            }

            var bestTimes = new Dictionary<int, double>();
            foreach (LevelProgress level in progress.Data.Levels)
            {
                if (level.BestSeconds.HasValue) bestTimes[level.LevelOrder] = level.BestSeconds.Value;
            }
            _store.ApplyAccountProgress(progress.Data.HighestUnlockedLevel, bestTimes, replace: switchedAccount);
            _store.SaveSetting(ProgressOwnerKey, Session.UserName);

            if (LevelManager.Instance != null) LevelManager.Instance.ReloadProgress();
            ProgressLoaded?.Invoke();
        }

        /// <summary>Cuando el progreso de la cuenta ya está aplicado en el dispositivo.</summary>
        public event Action ProgressLoaded;

        /// <summary>
        /// Cierra la sesión. Si hay partidas sin enviar, antes intenta enviarlas: si no puede (sin conexión),
        /// no cierra, para que no se pierdan ni acaben en otra cuenta. Devuelve si se ha cerrado.
        /// </summary>
        public async Task<bool> LogoutAsync()
        {
            if (PendingCount > 0) await SyncNowAsync();
            if (PendingCount > 0) return false;
            Session.SignOut();
            return true;
        }
    }
}
