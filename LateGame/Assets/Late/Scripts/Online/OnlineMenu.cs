using System;
using System.Collections.Generic;
using System.Text;
using Late.Localization;
using Late.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using L10n = Late.Localization.Localization;

namespace Late.Online
{
    /// <summary>
    /// Interfaz online, construida por código para no tocar la escena: una barra en la esquina del
    /// menú (idioma, ranking, cuenta) y dos paneles a pantalla completa (CU-04 cuenta y CU-05 ranking).
    /// Solo se ve mientras el objeto del menú (<see cref="OnlineSettings.MenuObjectName"/>) está activo.
    /// </summary>
    public class OnlineMenu : MonoBehaviour
    {
        private OnlineService _service;
        private UiKit _ui;
        private Canvas _canvas;
        private GameObject _menuRoot;

        private RectTransform _cornerBar;
        private GameObject _rankingEntry;
        private GameObject _accountBack;
        private GameObject _accountPanel;
        private GameObject _rankingPanel;

        // Cuenta
        private bool _registerMode;
        private GameObject _loggedOut;
        private GameObject _loggedIn;
        private TextMeshProUGUI _modeLogin;
        private TextMeshProUGUI _modeRegister;
        private TMP_InputField _userField;
        private TMP_InputField _emailField;
        private TMP_InputField _passwordField;
        private LocalizedText _userPlaceholder;
        private LocalizedText _submitLabel;
        private Button _submitButton;
        private TextMeshProUGUI _loggedAs;
        private TextMeshProUGUI _pending;
        private TextMeshProUGUI _accountStatus;
        private TextMeshProUGUI _accountEntry;

        // Ranking
        private RectTransform _levelTabs;
        private readonly List<(int Order, TextMeshProUGUI Label)> _tabs = new List<(int, TextMeshProUGUI)>();
        private TextMeshProUGUI _rankingList;
        private int _rankingLevel;
        private int _rankingRequest;

        private void Start()
        {
            _service = GetComponent<OnlineService>();
            _ui = new UiKit(_service.Settings.Font);
            _canvas = UiKit.CreateCanvas(transform, "OnlineCanvas", sortingOrder: 50);

            BuildCornerBar();
            BuildAccountPanel();
            BuildRankingPanel();
            _cornerBar.SetAsLastSibling(); // encima de los paneles: el idioma se puede cambiar desde ellos
            ShowPanel(null);

            _service.Session.Changed += RefreshAccount;
            _service.Synced += OnSynced;
            L10n.Changed += RefreshAccount;
            RefreshAccount();
        }

        private void OnDestroy()
        {
            if (_service != null)
            {
                _service.Session.Changed -= RefreshAccount;
                _service.Synced -= OnSynced;
            }
            L10n.Changed -= RefreshAccount;
        }

        private void Update()
        {
            // Sin cuenta no se juega: la pantalla de cuenta tapa todo (portada incluida) hasta entrar.
            bool gated = !_service.Session.IsLoggedIn;
            if (gated != _gated)
            {
                _gated = gated;
                ShowPanel(gated ? _accountPanel : null);
                RefreshAccount();
            }

            // Con cuenta, la barra solo se ve en el menú: durante la partida el objeto del menú está
            // desactivado, y en la portada está activo pero tapado por ella.
            if (_menuRoot == null) _menuRoot = GameObject.Find(_service.Settings.MenuObjectName);
            bool visible = gated || (_menuRoot != null && _menuRoot.activeInHierarchy && !AnyCoverActive());
            if (_canvas.enabled != visible) _canvas.enabled = visible;
        }

        private bool _gated;

        private GameObject[] _covers;

        private bool AnyCoverActive()
        {
            string[] names = _service.Settings.HiddenWhileActive;
            if (_covers == null) _covers = new GameObject[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                // GameObject.Find solo encuentra objetos activos: si no está, no tapa nada.
                if (_covers[i] == null) _covers[i] = GameObject.Find(names[i]);
                if (_covers[i] != null && _covers[i].activeInHierarchy) return true;
            }
            return false;
        }

        // ---------- Barra de la esquina ----------

        private void BuildCornerBar()
        {
            RectTransform bar = _cornerBar = UiKit.Stack(_canvas.transform, "CornerBar", vertical: false, spacing: 40f, TextAnchor.MiddleRight);
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(1f, 1f);
            bar.anchoredPosition = new Vector2(-40f, -40f);

            _ui.Button(bar, "language.toggle", ToggleLanguage, UiKit.SmallTextSize);
            _rankingEntry = _ui.Button(bar, "online.ranking", OpenRanking, UiKit.SmallTextSize).gameObject;
            _accountEntry = _ui.Button(bar, null, () => ShowPanel(_accountPanel), UiKit.SmallTextSize)
                .GetComponent<TextMeshProUGUI>();
        }

        private static void ToggleLanguage() =>
            L10n.Current = L10n.Current == Language.Spanish ? Language.English : Language.Spanish;

        // ---------- Paneles ----------

        private RectTransform BuildPanel(string name, string titleKey, out GameObject panel) =>
            BuildPanel(name, titleKey, out panel, out _);

        private RectTransform BuildPanel(string name, string titleKey, out GameObject panel, out GameObject backButton)
        {
            RectTransform root = UiKit.Rect(_canvas.transform, name);
            UiKit.Stretch(root);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = UiKit.Background; // tapa el menú y bloquea sus toques
            panel = root.gameObject;

            RectTransform content = UiKit.Stack(root, "Content", vertical: true, spacing: 28f);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(80f, 0f);
            content.offsetMax = new Vector2(-80f, -160f);

            _ui.Localized(content, titleKey, UiKit.TitleSize);
            UiKit.Spacer(content, 20f);

            Button back = _ui.Button(root, "online.back", () => ShowPanel(null));
            var backRect = (RectTransform)back.transform;
            backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(0f, 0f);
            backRect.anchoredPosition = new Vector2(80f, 120f);
            backRect.sizeDelta = new Vector2(500f, 80f);
            backButton = back.gameObject;
            return content;
        }

        private void ShowPanel(GameObject panel)
        {
            _accountPanel.SetActive(panel == _accountPanel);
            _rankingPanel.SetActive(panel == _rankingPanel);
            if (panel == _accountPanel) RefreshAccount();
        }

        // ---------- Cuenta (CU-04) ----------

        private void BuildAccountPanel()
        {
            RectTransform content = BuildPanel("AccountPanel", "account.title", out _accountPanel, out _accountBack);

            // Sin sesión: entrar o crear cuenta.
            RectTransform loggedOut = UiKit.Stack(content, "LoggedOut", vertical: true, spacing: 24f);
            _loggedOut = loggedOut.gameObject;

            RectTransform modes = UiKit.Stack(loggedOut, "Modes", vertical: false, spacing: 60f);
            _modeLogin = _ui.Button(modes, "account.mode.login", () => SetRegisterMode(false)).GetComponent<TextMeshProUGUI>();
            _modeRegister = _ui.Button(modes, "account.mode.register", () => SetRegisterMode(true)).GetComponent<TextMeshProUGUI>();

            _userField = _ui.Input(loggedOut, "account.field.userOrEmail", TMP_InputField.ContentType.Standard);
            _userPlaceholder = _userField.placeholder.GetComponent<LocalizedText>();
            _emailField = _ui.Input(loggedOut, "account.field.email", TMP_InputField.ContentType.EmailAddress);
            _passwordField = _ui.Input(loggedOut, "account.field.password", TMP_InputField.ContentType.Password);

            _submitButton = _ui.Button(loggedOut, "account.submit.login", Submit);
            _submitLabel = _submitButton.GetComponent<LocalizedText>();
            _ui.Localized(loggedOut, "account.required", UiKit.SmallTextSize, UiKit.Dim);

            // Con sesión.
            RectTransform loggedIn = UiKit.Stack(content, "LoggedIn", vertical: true, spacing: 24f);
            _loggedIn = loggedIn.gameObject;
            _loggedAs = _ui.Text(loggedIn, "");
            _pending = _ui.Text(loggedIn, "", UiKit.TextSize, UiKit.Dim);
            _ui.Button(loggedIn, "account.syncNow", SyncNow);
            _ui.Button(loggedIn, "account.logout", Logout);

            UiKit.Spacer(content, 20f);
            _accountStatus = _ui.Text(content, "", UiKit.SmallTextSize);

            SetRegisterMode(false);
        }

        private void SetRegisterMode(bool register)
        {
            _registerMode = register;
            _emailField.gameObject.SetActive(register);
            _userPlaceholder.Key = register ? "account.field.user" : "account.field.userOrEmail";
            _submitLabel.Key = register ? "account.submit.register" : "account.submit.login";
            _modeLogin.color = register ? UiKit.Dim : UiKit.Green;
            _modeRegister.color = register ? UiKit.Green : UiKit.Dim;
            SetStatus(null, false);
        }

        private void RefreshAccount()
        {
            if (_service == null || _loggedIn == null) return;
            bool loggedIn = _service.Session.IsLoggedIn;
            _loggedIn.SetActive(loggedIn);
            _loggedOut.SetActive(!loggedIn);

            // Sin cuenta no hay a dónde volver ni ranking que ver: solo entrar, registrarse o cambiar el idioma.
            _accountBack.SetActive(loggedIn);
            _rankingEntry.SetActive(loggedIn);
            _accountEntry.gameObject.SetActive(loggedIn);
            _accountEntry.text = loggedIn ? $"> {_service.Session.UserName}" : L10n.Get("online.account");
            if (loggedIn)
            {
                _loggedAs.text = L10n.Format("account.loggedAs", _service.Session.UserName);
                _pending.text = L10n.Format("account.pending", _service.PendingCount);
            }
        }

        private async void Submit()
        {
            _submitButton.interactable = false;
            SetStatus("account.working", false);
            try
            {
                ApiResult<AuthResponse> result = _registerMode
                    ? await _service.RegisterAsync(_userField.text, _emailField.text, _passwordField.text)
                    : await _service.LoginAsync(_userField.text, _passwordField.text);

                if (result.Ok)
                {
                    _passwordField.text = "";
                    SetStatus(null, false);
                    _accountStatus.text = L10n.Format("account.welcome", result.Data.UserName);
                }
                else
                {
                    ShowError(result.ErrorKey, result.Status);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowError("error.unknown", 0);
            }
            finally
            {
                if (_submitButton != null) _submitButton.interactable = true;
            }
        }

        private async void Logout()
        {
            SetStatus("account.working", false);
            if (!await _service.LogoutAsync()) ShowError("account.logoutPending", 0);
            else SetStatus(null, false);
        }

        private async void SyncNow()
        {
            SetStatus("account.working", false);
            SyncOutcome outcome = await _service.SyncNowAsync();
            if (outcome.Status == SyncStatus.Offline) ShowError("error.network", 0);
            else if (outcome.Status == SyncStatus.SessionExpired) ShowError("error.sessionExpired", 401);
            else if (outcome.Status == SyncStatus.ServerError) ShowError("error.unknown", 500);
            else _accountStatus.text = L10n.Format("account.synced", outcome.Sent);
        }

        private void OnSynced(SyncOutcome outcome)
        {
            RefreshAccount();
            if (outcome.Status == SyncStatus.SessionExpired) ShowError("error.sessionExpired", 401);
        }

        private void SetStatus(string key, bool error)
        {
            if (_accountStatus == null) return;
            _accountStatus.color = error ? UiKit.Red : UiKit.Green;
            _accountStatus.text = key == null ? "" : L10n.Get(key);
        }

        private void ShowError(string key, int status)
        {
            SetStatus(key, true);
            if (key == "error.unknown") _accountStatus.text = L10n.Format(key, status);
        }

        // ---------- Ranking (CU-05) ----------

        private void BuildRankingPanel()
        {
            RectTransform content = BuildPanel("RankingPanel", "ranking.title", out _rankingPanel);
            _levelTabs = UiKit.Stack(content, "Levels", vertical: false, spacing: 60f);
            UiKit.Spacer(content, 10f);
            _rankingList = _ui.Text(content, "", UiKit.TextSize);
            _rankingList.enableWordWrapping = false;

            // Hasta que responda la API, los niveles conocidos del juego.
            SetLevelTabs(new List<LevelDto>
            {
                new LevelDto { Order = 0, Name = "Run0" },
                new LevelDto { Order = 1, Name = "Run1" },
            });
        }

        private void SetLevelTabs(List<LevelDto> levels)
        {
            foreach (Transform child in _levelTabs) Destroy(child.gameObject);
            _tabs.Clear();
            foreach (LevelDto level in levels)
            {
                int order = level.Order;
                TextMeshProUGUI label = _ui.Button(_levelTabs, null, () => LoadRanking(order), UiKit.TextSize,
                    level.Name.ToLowerInvariant()).GetComponent<TextMeshProUGUI>();
                _tabs.Add((order, label));
            }
            HighlightTab();
        }

        private void HighlightTab()
        {
            foreach ((int order, TextMeshProUGUI label) in _tabs)
            {
                label.color = order == _rankingLevel ? UiKit.Green : UiKit.Dim;
            }
        }

        private async void OpenRanking()
        {
            ShowPanel(_rankingPanel);
            LoadRanking(_rankingLevel);
            ApiResult<List<LevelDto>> levels = await _service.Api.GetLevelsAsync();
            if (levels.Ok && levels.Data.Count > 0) SetLevelTabs(levels.Data);
        }

        private async void LoadRanking(int levelOrder)
        {
            _rankingLevel = levelOrder;
            HighlightTab();
            int request = ++_rankingRequest; // si se cambia de nivel antes de que responda, se ignora la respuesta vieja
            _rankingList.color = UiKit.Dim;
            _rankingList.text = L10n.Get("ranking.loading");

            ApiResult<RankingResponse> result = await _service.Api.GetRankingAsync(levelOrder);
            if (request != _rankingRequest || _rankingList == null) return;

            if (!result.Ok)
            {
                _rankingList.color = UiKit.Red;
                _rankingList.text = result.ErrorKey == "error.unknown"
                    ? L10n.Format(result.ErrorKey, result.Status)
                    : L10n.Get(result.ErrorKey);
                return;
            }

            _rankingList.color = UiKit.Green;
            _rankingList.text = FormatRanking(result.Data.Entries, _service.Session.IsLoggedIn ? _service.Session.UserName : null);
        }

        /// <summary>Una línea por jugador, en columnas (la fuente es monoespaciada).</summary>
        public static string FormatRanking(List<RankingEntry> entries, string currentUser)
        {
            if (entries == null || entries.Count == 0) return L10n.Get("ranking.empty");

            var text = new StringBuilder();
            foreach (RankingEntry entry in entries)
            {
                string name = entry.UserName.Length > 14 ? entry.UserName.Substring(0, 14) : entry.UserName;
                string you = string.Equals(entry.UserName, currentUser, StringComparison.OrdinalIgnoreCase)
                    ? " " + L10n.Get("ranking.you")
                    : "";
                text.Append(entry.Position.ToString("00")).Append("  ")
                    .Append(name.PadRight(14)).Append(' ')
                    .Append(entry.BestSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).PadLeft(7))
                    .Append('s').Append(you).Append('\n');
            }
            return text.ToString();
        }
    }
}
