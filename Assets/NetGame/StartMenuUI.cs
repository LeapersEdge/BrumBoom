using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NetGame
{
    /// <summary>
    /// Simple UI bridge for Host/Client/Auto buttons.
    /// </summary>
    public class StartMenuUI : MonoBehaviour, INetworkRunnerCallbacks
    {
        private const string PlayerNameKey = "PlayerName";
        private const string PropertyMap = "map";
        private const string PropertyHost = "host";
        [SerializeField] private string gameplaySceneName = "Gameplay";
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button autoButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Button createTabButton;
        [SerializeField] private Button joinTabButton;
        [SerializeField] private Button createConfirmButton;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private GameObject mainButtonsRoot;
        [SerializeField] private GameObject playMenuRoot;
        [SerializeField] private GameObject createPanel;
        [SerializeField] private GameObject joinPanel;
        [SerializeField] private TMP_InputField createSessionNameInput;
        [SerializeField] private TMP_InputField maxPlayersInput;
        [SerializeField] private TMP_Dropdown mapDropdown;
        [SerializeField] private Transform sessionListRoot;
        [SerializeField] private SessionListEntryUI sessionEntryPrefab;
        [SerializeField] private Button joinSelectionButton;
        [SerializeField] private TMP_Text noGamesLabel;
        [SerializeField] private bool useRuntimeUI = false;
        [SerializeField] private string[] mapSceneNames;
        [SerializeField] private string[] mapDisplayNames;

        public static string LocalPlayerName { get; private set; } = "Player";

        private bool _clicked;
        private NetworkRunner _lobbyRunner;
        private List<SessionInfo> _sessionList = new();
        private readonly List<SessionListEntryUI> _sessionEntries = new();
        private string _selectedSessionName;
        private bool _uiInitialized;
        private bool _joinPanelBuilt;
        private TMP_FontAsset _fallbackFont;

        private void Awake()
        {
            // Keep this GO alive across scene load so coroutine survives.
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (hostButton != null)
                hostButton.onClick.AddListener(OnHostClicked);
            if (clientButton != null)
                clientButton.onClick.AddListener(OnClientClicked);
            if (autoButton != null)
                autoButton.onClick.AddListener(OnAutoClicked);

            if (menuRoot == null)
                menuRoot = gameObject;

            if (nameInput == null)
                nameInput = GetComponentInChildren<TMP_InputField>(true);

            InitializeName();

            if (nameInput != null)
                nameInput.onEndEdit.AddListener(OnNameEdited);

            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);
            if (createTabButton != null)
                createTabButton.onClick.AddListener(OnCreateTabClicked);
            if (joinTabButton != null)
                joinTabButton.onClick.AddListener(OnJoinTabClicked);
            if (createConfirmButton != null)
                createConfirmButton.onClick.AddListener(OnCreateConfirmClicked);
            if (backButton != null)
                backButton.onClick.AddListener(BackToMain);

            AutoWireLegacyButtons();
            TryAutoWirePlayMenuRoots();
            if (useRuntimeUI)
                EnsureRuntimeUI();
            else
                AutoWireMenuElements();
            RefreshRuntimeBindings();
            ApplyTheme();
            CacheFallbackFont();
            InitializeMapDropdown();
            ShowPlayMenu(false);
            HideLegacyButtons();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (hostButton != null)
                hostButton.onClick.RemoveListener(OnHostClicked);
            if (clientButton != null)
                clientButton.onClick.RemoveListener(OnClientClicked);
            if (autoButton != null)
                autoButton.onClick.RemoveListener(OnAutoClicked);

            if (nameInput != null)
                nameInput.onEndEdit.RemoveListener(OnNameEdited);

            if (playButton != null)
                playButton.onClick.RemoveListener(OnPlayClicked);
            if (createTabButton != null)
                createTabButton.onClick.RemoveListener(OnCreateTabClicked);
            if (joinTabButton != null)
                joinTabButton.onClick.RemoveListener(OnJoinTabClicked);
            if (createConfirmButton != null)
                createConfirmButton.onClick.RemoveListener(OnCreateConfirmClicked);
            if (backButton != null)
                backButton.onClick.RemoveListener(BackToMain);
        }

        private void OnHostClicked()
        {
            if (_clicked) return;
            StartCoroutine(LoadAndStart(GameMode.Host));
        }

        private void OnClientClicked()
        {
            if (_clicked) return;
            StartCoroutine(LoadAndStart(GameMode.Client));
        }

        private void OnAutoClicked()
        {
            if (_clicked) return;
            StartCoroutine(LoadAndStart(GameMode.AutoHostOrClient));
        }

        public void OnExitClicked()
        {
            Application.Quit();
        }

        public static string GetLocalPlayerName()
        {
            if (string.IsNullOrWhiteSpace(LocalPlayerName))
                LocalPlayerName = PlayerPrefs.GetString(PlayerNameKey, "Player");
            return LocalPlayerName;
        }

        private void SetButtonsInteractable(bool value)
        {
            if (hostButton != null) hostButton.interactable = value;
            if (clientButton != null) clientButton.interactable = value;
            if (autoButton != null) autoButton.interactable = value;
        }

        private void ResetClickGate()
        {
            _clicked = false;
            SetButtonsInteractable(true);
        }

        private void HideMenuUI()
        {
            if (menuRoot != null && menuRoot != gameObject)
            {
                menuRoot.SetActive(false);
                return;
            }

            if (playMenuRoot != null)
                playMenuRoot.SetActive(false);
            if (mainButtonsRoot != null)
                mainButtonsRoot.SetActive(false);
            if (nameInput != null)
                nameInput.gameObject.SetActive(false);
        }

        private void EnsureNameInputPlacement()
        {
            if (nameInput == null || mainButtonsRoot == null)
                return;

            if (nameInput.transform.parent != mainButtonsRoot.transform)
                nameInput.transform.SetParent(mainButtonsRoot.transform, false);
        }

        private System.Collections.IEnumerator LoadAndStart(GameMode mode)
        {
            _clicked = true;
            SetButtonsInteractable(false);

            // load gameplay scene
            int buildIndex = GetBuildIndexByName(gameplaySceneName);
            if (buildIndex < 0)
            {
                Debug.LogError($"[StartMenuUI] Scene '{gameplaySceneName}' not found in Build Settings.");
                LogBuildScenes();
                ResetClickGate();
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[StartMenuUI] Failed to load scene '{gameplaySceneName}'. Is it in Build Settings?");
                ResetClickGate();
                yield break;
            }
            while (!op.isDone)
                yield return null;

            // find bootstrap in loaded scene
            var bootstrap = FindObjectOfType<GameBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("[StartMenuUI] GameBootstrap not found in gameplay scene.");
                ResetClickGate();
                yield break;
            }

            switch (mode)
            {
                case GameMode.Host:
                    Debug.Log("[StartMenuUI] Starting Host...");
                    bootstrap.StartHost();
                    break;
                case GameMode.Client:
                    Debug.Log("[StartMenuUI] Starting Client...");
                    bootstrap.StartClient();
                    break;
                case GameMode.AutoHostOrClient:
                    Debug.Log("[StartMenuUI] Starting Auto...");
                    bootstrap.StartAuto();
                    break;
            }

            // lock cursor after we start the network
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // hide menu UI while in gameplay
            HideMenuUI();
        }

        private void OnPlayClicked()
        {
            ShowPlayMenu(true);
            _ = EnsureLobbyRunner();
        }

        private void OnCreateTabClicked() => ShowCreatePanel(true);
        private void OnJoinTabClicked() => ShowCreatePanel(false);

        private void ShowPlayMenu(bool show)
        {
            if (!_uiInitialized)
                TryAutoWirePlayMenuRoots();

            if (mainButtonsRoot != null)
                mainButtonsRoot.SetActive(!show);
            if (playMenuRoot != null)
                playMenuRoot.SetActive(show);
            if (nameInput != null)
                nameInput.gameObject.SetActive(!show);

            if (show)
            {
                if (useRuntimeUI && joinPanel != null)
                {
                    BuildJoinPanelContent(joinPanel.transform);
                    _joinPanelBuilt = true;
                    RefreshJoinPanelReferences();
                }
                ShowCreatePanel(true);
                RebuildSessionList();
            }
        }

        private void ShowCreatePanel(bool create)
        {
            if (createPanel != null)
                createPanel.SetActive(create);
            if (joinPanel != null)
                joinPanel.SetActive(true);
            UpdateJoinButtonState();
        }

        private void BackToMain()
        {
            ShowPlayMenu(false);
        }

        private async Task EnsureLobbyRunner()
        {
            if (_lobbyRunner != null)
                return;

            _lobbyRunner = gameObject.GetComponent<NetworkRunner>();
            if (_lobbyRunner == null)
                _lobbyRunner = gameObject.AddComponent<NetworkRunner>();

            _lobbyRunner.ProvideInput = false;
            _lobbyRunner.AddCallbacks(this);

            try
            {
                await _lobbyRunner.JoinSessionLobby(SessionLobby.ClientServer, null, null, null, null, CancellationToken.None, true);
            }
            catch
            {
                // ignore; user can still host/join by name
            }
        }

        private void OnCreateConfirmClicked()
        {
            string sessionName = createSessionNameInput != null ? createSessionNameInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(sessionName))
                sessionName = GenerateRandomName();

            int maxPlayers = 8;
            if (maxPlayersInput != null && int.TryParse(maxPlayersInput.text, out int parsed))
                maxPlayers = Mathf.Clamp(parsed, 2, 32);

            string mapScene = GetSelectedMapScene();
            int mapIndex = GetBuildIndexByName(mapScene);
            Debug.Log($"[StartMenuUI] Create clicked. Session='{sessionName}', MaxPlayers={maxPlayers}, Map='{mapScene}', BuildIndex={mapIndex}");
            LogBuildScenes();

            var props = new Dictionary<string, SessionProperty>
            {
                [PropertyMap] = mapScene,
                [PropertyHost] = GetLocalPlayerName()
            };

            StartCoroutine(LoadMapAndStartHost(sessionName, maxPlayers, props, mapScene));
        }

        private System.Collections.IEnumerator LoadMapAndStartHost(string sessionName, int maxPlayers, Dictionary<string, SessionProperty> props, string mapScene)
        {
            Debug.Log($"[StartMenuUI] LoadMapAndStartHost 시작. Map='{mapScene}'");
            int buildIndex = GetBuildIndexByName(mapScene);
            if (buildIndex < 0)
            {
                Debug.LogError($"[StartMenuUI] Scene '{mapScene}' not found in Build Settings.");
                LogBuildScenes();
                ResetClickGate();
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"Failed to load scene '{mapScene}' for hosting.");
                ResetClickGate();
                yield break;
            }

            float startTime = Time.realtimeSinceStartup;
            float nextLog = startTime + 1f;
            while (!op.isDone)
            {
                if (Time.realtimeSinceStartup >= nextLog)
                {
                    Debug.Log($"[StartMenuUI] Loading '{mapScene}'... progress={op.progress:0.00}");
                    nextLog = Time.realtimeSinceStartup + 1f;
                }
                if (Time.realtimeSinceStartup - startTime > 15f)
                {
                    Debug.LogError($"[StartMenuUI] Scene '{mapScene}' load timeout. progress={op.progress:0.00}");
                    ResetClickGate();
                    yield break;
                }
                yield return null;
            }

            var bootstrap = FindObjectOfType<GameBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("GameBootstrap not found in gameplay scene. Hosting cannot start.");
                var found = FindObjectsOfType<GameBootstrap>(true);
                Debug.LogWarning($"[StartMenuUI] GameBootstrap count in scene: {found.Length}");
                ResetClickGate();
                yield break;
            }

            Debug.Log($"[StartMenuUI] Scene '{mapScene}' loaded. Starting host session '{sessionName}'.");
            bootstrap.ConfigureSession(sessionName, maxPlayers, props);
            bootstrap.StartHost();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            HideMenuUI();
        }

        private void JoinSession(SessionInfo info)
        {
            string mapScene = GetStringProperty(info, PropertyMap) ?? GetSelectedMapScene();
            StartCoroutine(LoadMapAndJoin(info.Name, mapScene));
        }

        private System.Collections.IEnumerator LoadMapAndJoin(string sessionName, string mapScene)
        {
            Debug.Log($"[StartMenuUI] LoadMapAndJoin 시작. Map='{mapScene}'");
            int buildIndex = GetBuildIndexByName(mapScene);
            if (buildIndex < 0)
            {
                Debug.LogError($"[StartMenuUI] Scene '{mapScene}' not found in Build Settings.");
                LogBuildScenes();
                ResetClickGate();
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"Failed to load scene '{mapScene}' for joining.");
                ResetClickGate();
                yield break;
            }

            float startTime = Time.realtimeSinceStartup;
            float nextLog = startTime + 1f;
            while (!op.isDone)
            {
                if (Time.realtimeSinceStartup >= nextLog)
                {
                    Debug.Log($"[StartMenuUI] Loading '{mapScene}'... progress={op.progress:0.00}");
                    nextLog = Time.realtimeSinceStartup + 1f;
                }
                if (Time.realtimeSinceStartup - startTime > 15f)
                {
                    Debug.LogError($"[StartMenuUI] Scene '{mapScene}' load timeout. progress={op.progress:0.00}");
                    ResetClickGate();
                    yield break;
                }
                yield return null;
            }

            var bootstrap = FindObjectOfType<GameBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("GameBootstrap not found in gameplay scene. Join cannot start.");
                var found = FindObjectsOfType<GameBootstrap>(true);
                Debug.LogWarning($"[StartMenuUI] GameBootstrap count in scene: {found.Length}");
                ResetClickGate();
                yield break;
            }

            Debug.Log($"[StartMenuUI] Scene '{mapScene}' loaded. Joining session '{sessionName}'.");
            bootstrap.ConfigureSession(sessionName, -1, null);
            bootstrap.StartClient();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            HideMenuUI();
        }

        private void InitializeMapDropdown()
        {
            if (mapDropdown == null)
                return;

            mapDropdown.ClearOptions();
            var options = new List<string>();
            if (mapDisplayNames != null && mapDisplayNames.Length > 0)
                options.AddRange(mapDisplayNames);
            else if (mapSceneNames != null && mapSceneNames.Length > 0)
                options.AddRange(mapSceneNames);

            if (options.Count == 0)
                options.Add(gameplaySceneName);

            mapDropdown.AddOptions(options);
        }

        private void CacheFallbackFont()
        {
            if (_fallbackFont != null)
                return;

            var anyText = GetComponentInChildren<TMP_Text>(true);
            if (anyText != null)
                _fallbackFont = anyText.font;
        }

        private string GetSelectedMapScene()
        {
            if (mapSceneNames != null && mapSceneNames.Length > 0 && mapDropdown != null)
            {
                int idx = Mathf.Clamp(mapDropdown.value, 0, mapSceneNames.Length - 1);
                return mapSceneNames[idx];
            }

            return gameplaySceneName;
        }

        private static string GetStringProperty(SessionInfo info, string key)
        {
            if (info.Properties == null || !info.Properties.TryGetValue(key, out var prop))
                return null;

            if (prop.IsString)
                return prop.PropertyValue as string;

            return prop.ToString();
        }

        private void RebuildSessionList()
        {
            EnsureSessionListRoot();

            if (sessionListRoot == null || sessionEntryPrefab == null)
            {
                if (sessionListRoot == null)
                    return;
            }

            Debug.Log($"[StartMenuUI] Session list count: {(_sessionList == null ? 0 : _sessionList.Count)}");
            for (int i = sessionListRoot.childCount - 1; i >= 0; i--)
                Destroy(sessionListRoot.GetChild(i).gameObject);
            _sessionEntries.Clear();

            if (_sessionList == null || _sessionList.Count == 0)
            {
                EnsureNoGamesLabel();
                if (noGamesLabel != null)
                {
                    noGamesLabel.text = "No games available";
                    noGamesLabel.gameObject.SetActive(true);
                }
                _selectedSessionName = null;
                UpdateJoinButtonState();
                ForceSessionListLayout();
                return;
            }
            if (noGamesLabel != null)
                noGamesLabel.gameObject.SetActive(false);

            foreach (var info in _sessionList)
            {
                if (!info.IsValid)
                    continue;

                SessionListEntryUI entry = null;
                if (sessionEntryPrefab != null)
                    entry = Instantiate(sessionEntryPrefab, sessionListRoot);
                if (entry == null || !entry.HasValidLayout())
                {
                    if (entry != null)
                        Destroy(entry.gameObject);
                    entry = CreateRuntimeSessionEntry(sessionListRoot);
                }
                string host = GetStringProperty(info, PropertyHost) ?? "Unknown";
                string map = GetStringProperty(info, PropertyMap) ?? "Unknown";
                entry.SetData(info.Name, $"Host: {host}", $"Map: {map}", $"Players: {info.PlayerCount}/{info.MaxPlayers}", () => SelectSession(info.Name));
                entry.ApplyStyle();
                entry.ApplyFont(_fallbackFont);
                entry.EnsureVisible();
                _sessionEntries.Add(entry);
            }

            UpdateSelectionVisuals();
            UpdateJoinButtonState();
            ForceSessionListLayout();
        }

        private void EnsureNoGamesLabel()
        {
            if (sessionListRoot == null)
                return;

            if (noGamesLabel != null)
                return;

            var scroll = sessionListRoot.GetComponentInParent<ScrollRect>(true);
            if (scroll == null)
                return;

            var parent = scroll.transform;
            noGamesLabel = CreateText("NoGamesLabel", parent);
            var rect = noGamesLabel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240, 26);
            noGamesLabel.alignment = TextAlignmentOptions.Center;
            noGamesLabel.fontSize = 16;
            noGamesLabel.color = Color.black;
            noGamesLabel.raycastTarget = false;
        }

        private void ForceSessionListLayout()
        {
            if (sessionListRoot == null)
                return;

            var scroll = sessionListRoot.GetComponentInParent<ScrollRect>(true);
            if (scroll != null)
                scroll.gameObject.SetActive(true);

            sessionListRoot.gameObject.SetActive(true);
            var rect = sessionListRoot.GetComponent<RectTransform>();
            if (rect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        private void SelectSession(string sessionName)
        {
            _selectedSessionName = sessionName;
            UpdateSelectionVisuals();
            UpdateJoinButtonState();
        }

        private void UpdateSelectionVisuals()
        {
            if (_sessionEntries == null || _sessionEntries.Count == 0)
                return;

            bool foundSelected = false;
            foreach (var entry in _sessionEntries)
            {
                if (entry == null)
                    continue;

                bool selected = false;
                if (!string.IsNullOrWhiteSpace(_selectedSessionName))
                    selected = string.Equals(entry.GetGameName(), _selectedSessionName, System.StringComparison.Ordinal);
                if (selected)
                    foundSelected = true;

                entry.SetSelected(selected);
            }

            if (!foundSelected && !string.IsNullOrWhiteSpace(_selectedSessionName))
            {
                _selectedSessionName = null;
                UpdateJoinButtonState();
            }
        }

        private void UpdateJoinButtonState()
        {
            if (joinSelectionButton == null)
                return;

            if (string.IsNullOrWhiteSpace(_selectedSessionName))
            {
                joinSelectionButton.interactable = false;
                return;
            }

            var info = GetSelectedSessionInfo();
            if (info == null)
            {
                joinSelectionButton.interactable = false;
                return;
            }

            bool isFull = info.PlayerCount >= info.MaxPlayers;
            joinSelectionButton.interactable = !isFull;
        }

        private void JoinSelectedSession()
        {
            if (string.IsNullOrWhiteSpace(_selectedSessionName))
                return;

            if (_sessionList != null)
            {
                foreach (var info in _sessionList)
                {
                    if (info.IsValid && info.Name == _selectedSessionName)
                    {
                        JoinSession(info);
                        return;
                    }
                }
            }

            Debug.LogWarning($"[StartMenuUI] Selected session '{_selectedSessionName}' no longer exists.");
            _selectedSessionName = null;
            UpdateJoinButtonState();
        }

        private SessionInfo? GetSelectedSessionInfo()
        {
            if (_sessionList == null || string.IsNullOrWhiteSpace(_selectedSessionName))
                return null;

            foreach (var info in _sessionList)
            {
                if (info.IsValid && info.Name == _selectedSessionName)
                    return info;
            }

            return null;
        }

        private void InitializeName()
        {
            string saved = PlayerPrefs.GetString(PlayerNameKey, string.Empty);
            if (string.IsNullOrWhiteSpace(saved) || IsLegacyRandomName(saved))
                saved = GenerateRandomName();

            SetLocalName(saved);

            if (nameInput != null)
                nameInput.text = LocalPlayerName;
        }

        private void OnNameEdited(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                value = GenerateRandomName();

            SetLocalName(value);

            if (nameInput != null && nameInput.text != LocalPlayerName)
                nameInput.text = LocalPlayerName;
        }

        private static void SetLocalName(string value)
        {
            LocalPlayerName = SanitizeName(value);
            PlayerPrefs.SetString(PlayerNameKey, LocalPlayerName);
            PlayerPrefs.Save();
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Player";

            value = value.Trim();
            if (value.Length > 24)
                value = value.Substring(0, 24);
            return value;
        }

        private static string GenerateRandomName()
        {
            string[] first = { "Swift", "Iron", "Neon", "Shadow", "Crimson", "Frost", "Wild", "Silent", "Solar", "Storm" };
            string[] second = { "Rider", "Wolf", "Falcon", "Tiger", "Nova", "Blade", "Ghost", "Viper", "Comet", "Raven" };

            string a = first[Random.Range(0, first.Length)];
            string b = second[Random.Range(0, second.Length)];
            return $"{a}{b}";
        }

        private static bool IsLegacyRandomName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();
            if (!value.StartsWith("Player", System.StringComparison.OrdinalIgnoreCase))
                return false;

            if (value.Length <= 6)
                return false;

            for (int i = 6; i < value.Length; i++)
            {
                if (!char.IsDigit(value[i]))
                    return false;
            }

            return true;
        }

        private enum GameMode
        {
            Host,
            Client,
            AutoHostOrClient
        }

        private static int GetBuildIndexByName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return -1;

            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(name, sceneName, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static void LogBuildScenes()
        {
            int count = SceneManager.sceneCountInBuildSettings;
            if (count == 0)
            {
                Debug.LogWarning("[StartMenuUI] Build Settings has 0 scenes.");
                return;
            }

            var list = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = Path.GetFileNameWithoutExtension(path);
                list.Add($"{i}:{name}");
            }
            Debug.Log($"[StartMenuUI] Build scenes: {string.Join(", ", list)}");
        }

        private void AutoWireLegacyButtons()
        {
            if (hostButton == null)
                hostButton = FindButtonByName("Host") ?? FindButtonByName("HostButton");
            if (clientButton == null)
                clientButton = FindButtonByName("Client") ?? FindButtonByName("ClientButton");
            if (autoButton == null)
                autoButton = FindButtonByName("Auto") ?? FindButtonByName("AutoButton");

            if (mainButtonsRoot == null)
            {
                if (hostButton != null)
                    mainButtonsRoot = hostButton.transform.parent.gameObject;
                else if (menuRoot != null)
                    mainButtonsRoot = menuRoot;
            }
        }

        private void HideLegacyButtons()
        {
            if (hostButton != null)
                hostButton.gameObject.SetActive(false);
            if (clientButton != null)
                clientButton.gameObject.SetActive(false);
            if (autoButton != null)
                autoButton.gameObject.SetActive(false);
        }

        private void TryAutoWirePlayMenuRoots()
        {
            if (_uiInitialized)
                return;

            if (playMenuRoot == null && menuRoot != null)
                playMenuRoot = FindChildByName(menuRoot.transform, "PlayMenuRoot");
            if (createPanel == null && playMenuRoot != null)
                createPanel = FindChildByName(playMenuRoot.transform, "CreatePanel");
            if (joinPanel == null && playMenuRoot != null)
                joinPanel = FindChildByName(playMenuRoot.transform, "JoinPanel");
            if (sessionListRoot == null && playMenuRoot != null)
                sessionListRoot = FindChildByName(playMenuRoot.transform, "SessionListRoot")?.transform;
            if (joinSelectionButton == null && playMenuRoot != null)
                joinSelectionButton = FindButtonByName("JoinSelectionButton");
            if (noGamesLabel == null && playMenuRoot != null)
                noGamesLabel = FindChildByName(playMenuRoot.transform, "NoGamesLabel")?.GetComponent<TMP_Text>();

            _uiInitialized = true;
        }

        private void AutoWireMenuElements()
        {
            if (menuRoot == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                menuRoot = canvas != null ? canvas.gameObject : gameObject;
            }

            if (mainButtonsRoot == null && menuRoot != null)
                mainButtonsRoot = FindChildByName(menuRoot.transform, "MainMenu") ?? menuRoot;

            if (nameInput == null)
                nameInput = FindChildByName(menuRoot.transform, "NameInput")?.GetComponent<TMP_InputField>();

            if (playButton == null)
                playButton = FindButtonByName("PlayButton");
            if (backButton == null)
                backButton = FindButtonByName("BackButton");
            if (createConfirmButton == null)
                createConfirmButton = FindButtonByName("CreateConfirmButton");
            if (joinSelectionButton == null)
                joinSelectionButton = FindButtonByName("JoinSelectionButton");

            if (createSessionNameInput == null)
                createSessionNameInput = FindChildByName(menuRoot.transform, "CreateSessionNameInput")?.GetComponent<TMP_InputField>();
            if (maxPlayersInput == null)
                maxPlayersInput = FindChildByName(menuRoot.transform, "MaxPlayersInput")?.GetComponent<TMP_InputField>();
            if (mapDropdown == null)
                mapDropdown = FindChildByName(menuRoot.transform, "MapDropdown")?.GetComponent<TMP_Dropdown>();

            EnsureNameInputPlacement();
        }

        private void EnsureSessionListRoot()
        {
            if (sessionListRoot != null)
                return;

            if (playMenuRoot != null)
                sessionListRoot = FindChildByName(playMenuRoot.transform, "SessionListRoot")?.transform;
            if (sessionListRoot == null && joinPanel != null)
                sessionListRoot = FindChildByName(joinPanel.transform, "SessionListRoot")?.transform;
        }

        private void EnsureRuntimeUI()
        {
            if (menuRoot == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                menuRoot = canvas != null ? canvas.gameObject : gameObject;
            }

            if (mainButtonsRoot == null && menuRoot != null)
                mainButtonsRoot = FindChildByName(menuRoot.transform, "MainMenu") ?? menuRoot;

            if (nameInput == null && mainButtonsRoot != null)
            {
                var nameBlock = CreatePanel("PlayerNameBlock", mainButtonsRoot.transform, new Vector2(360, 70));
                nameBlock.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
                var label = CreateLabel("Player Name", nameBlock.transform);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                nameInput = CreateInputField("NameInput", nameBlock.transform, "Enter name...");
            }
            EnsureNameInputPlacement();

            if (playButton == null && mainButtonsRoot != null)
            {
                playButton = CreateButton("PlayButton", mainButtonsRoot.transform, "Play");
                var rect = playButton.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(220, 40);
            }

            if (playMenuRoot == null && menuRoot != null)
            {
                playMenuRoot = CreatePanel("PlayMenuRoot", menuRoot.transform, new Vector2(820, 440));
                playMenuRoot.SetActive(false);
            }

            if (playMenuRoot != null)
            {
                var playRect = playMenuRoot.GetComponent<RectTransform>();
                if (playRect != null)
                    playRect.sizeDelta = new Vector2(820, 440);

                var contentRoot = FindChildByName(playMenuRoot.transform, "PlayMenuContent");
                if (contentRoot == null)
                {
                    var contentGo = new GameObject("PlayMenuContent");
                    var rect = contentGo.AddComponent<RectTransform>();
                    rect.SetParent(playMenuRoot.transform, false);
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(800, 420);
                    contentRoot = contentGo;

                    var layout = contentGo.AddComponent<VerticalLayoutGroup>();
                    layout.childAlignment = TextAnchor.UpperCenter;
                    layout.spacing = 10f;
                    layout.childControlWidth = true;
                    layout.childForceExpandWidth = false;
                    layout.childControlHeight = false;
                    layout.childForceExpandHeight = false;
                }
                else
                {
                    var rect = contentRoot.GetComponent<RectTransform>();
                    if (rect != null)
                        rect.sizeDelta = new Vector2(800, 420);
                }

                var panelsRow = FindChildByName(contentRoot.transform, "PanelsRow");
                if (panelsRow == null)
                {
                    var rowGo = new GameObject("PanelsRow");
                    var rowRect = rowGo.AddComponent<RectTransform>();
                    rowRect.SetParent(contentRoot.transform, false);
                    rowRect.sizeDelta = new Vector2(780, 300);
                    panelsRow = rowGo;

                    var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
                    rowLayout.childAlignment = TextAnchor.UpperCenter;
                    rowLayout.spacing = 12f;
                    rowLayout.childControlWidth = true;
                    rowLayout.childForceExpandWidth = false;
                }
                else
                {
                    var rowRect = panelsRow.GetComponent<RectTransform>();
                    if (rowRect != null)
                        rowRect.sizeDelta = new Vector2(780, 300);
                }

                HideForeignPanels(playMenuRoot.transform, contentRoot.transform);
                DisableForeignButtons(playMenuRoot.transform);

                if (createPanel != null && createPanel.transform.parent != panelsRow.transform)
                    createPanel.transform.SetParent(panelsRow.transform, false);
                if (joinPanel != null && joinPanel.transform.parent != panelsRow.transform)
                    joinPanel.transform.SetParent(panelsRow.transform, false);
                if (backButton != null && backButton.transform.parent != contentRoot.transform)
                    backButton.transform.SetParent(contentRoot.transform, false);

                if (createTabButton != null)
                    createTabButton.gameObject.SetActive(false);
                if (joinTabButton != null)
                    joinTabButton.gameObject.SetActive(false);

                if (createPanel == null)
                {
                    createPanel = CreatePanel("CreatePanel", panelsRow.transform, new Vector2(340, 280));
                    var layout = createPanel.AddComponent<VerticalLayoutGroup>();
                    layout.childAlignment = TextAnchor.UpperCenter;
                    layout.spacing = 8f;
                    layout.childControlWidth = true;
                    layout.childForceExpandWidth = true;
                }
                else
                {
                    var img = createPanel.GetComponent<Image>();
                    if (img != null)
                        img.color = new Color(0f, 0f, 0f, 0.45f);
                    var rect = createPanel.GetComponent<RectTransform>();
                    if (rect != null)
                        rect.sizeDelta = new Vector2(340, 280);
                }
                if (createPanel != null)
                {
                    var layoutElement = createPanel.GetComponent<LayoutElement>();
                    if (layoutElement == null)
                        layoutElement = createPanel.AddComponent<LayoutElement>();
                    layoutElement.preferredWidth = 340;
                    layoutElement.preferredHeight = 280;
                }

                if (joinPanel == null)
                {
                    joinPanel = CreatePanel("JoinPanel", panelsRow.transform, new Vector2(420, 280));
                    var layout = joinPanel.AddComponent<VerticalLayoutGroup>();
                    layout.childAlignment = TextAnchor.UpperCenter;
                    layout.spacing = 8f;
                    layout.childControlWidth = true;
                    layout.childForceExpandWidth = true;
                }
                else
                {
                    var img = joinPanel.GetComponent<Image>();
                    if (img != null)
                        img.color = new Color(0f, 0f, 0f, 0.45f);
                    var rect = joinPanel.GetComponent<RectTransform>();
                    if (rect != null)
                        rect.sizeDelta = new Vector2(420, 280);
                }
                if (joinPanel != null)
                {
                    var layoutElement = joinPanel.GetComponent<LayoutElement>();
                    if (layoutElement == null)
                        layoutElement = joinPanel.AddComponent<LayoutElement>();
                    layoutElement.preferredWidth = 420;
                    layoutElement.preferredHeight = 280;
                }

                if (createPanel != null)
                {
                    if (FindChildByName(createPanel.transform, "CreateHeader") == null)
                    {
                        var header = CreateText("CreateHeader", createPanel.transform);
                        header.text = "Create Game";
                        header.fontSize = 18;
                        header.alignment = TextAlignmentOptions.Center;
                        header.color = Color.black;
                    }

                    if (createSessionNameInput == null)
                        createSessionNameInput = CreateInputField("CreateSessionNameInput", createPanel.transform, "Game name...");
                    if (maxPlayersInput == null)
                        maxPlayersInput = CreateInputField("MaxPlayersInput", createPanel.transform, "Max players (e.g. 8)");
                    if (mapDropdown == null)
                        mapDropdown = CreateDropdown("MapDropdown", createPanel.transform);
                    if (createConfirmButton == null)
                        createConfirmButton = CreateButton("CreateConfirmButton", createPanel.transform, "Create");
                }

                if (joinPanel != null)
                {
                    var joinHeader = FindChildByName(joinPanel.transform, "JoinHeader");
                    if (joinHeader != null)
                        joinHeader.SetActive(false);

                    if (!_joinPanelBuilt)
                    {
                        BuildJoinPanelContent(joinPanel.transform);
                        _joinPanelBuilt = true;
                    }

                    if (joinSelectionButton == null)
                        joinSelectionButton = CreateButton("JoinSelectionButton", contentRoot.transform, "Join Selected");

                    CleanupJoinPanel(joinPanel.transform);
                }

                var bottomRow = FindChildByName(contentRoot.transform, "BottomRow");
                if (bottomRow == null)
                {
                    var rowGo = new GameObject("BottomRow");
                    var rowRect = rowGo.AddComponent<RectTransform>();
                    rowRect.SetParent(contentRoot.transform, false);
                    rowRect.sizeDelta = new Vector2(420, 40);
                    bottomRow = rowGo;

                    var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
                    rowLayout.childAlignment = TextAnchor.MiddleCenter;
                    rowLayout.spacing = 12f;
                    rowLayout.childControlWidth = true;
                    rowLayout.childForceExpandWidth = false;
                }

                if (joinSelectionButton != null && joinSelectionButton.transform.parent != bottomRow.transform)
                    joinSelectionButton.transform.SetParent(bottomRow.transform, false);

                if (backButton == null)
                    backButton = CreateButton("BackButton", bottomRow.transform, "Back");
                else if (backButton.transform.parent != bottomRow.transform)
                    backButton.transform.SetParent(bottomRow.transform, false);
                if (backButton != null)
                {
                    backButton.gameObject.SetActive(true);
                    var rect = backButton.GetComponent<RectTransform>();
                    if (rect != null)
                        rect.sizeDelta = new Vector2(200, 34);
                    var layout = backButton.GetComponent<LayoutElement>();
                    if (layout == null)
                        layout = backButton.gameObject.AddComponent<LayoutElement>();
                    layout.preferredHeight = 34;
                    layout.preferredWidth = 200;
                    layout.minHeight = 34;
                    layout.minWidth = 200;
                    var label = backButton.GetComponentInChildren<TMP_Text>(true);
                    if (label == null)
                    {
                        var labelGo = new GameObject("Text");
                        labelGo.transform.SetParent(backButton.transform, false);
                        label = labelGo.AddComponent<TextMeshProUGUI>();
                    }
                    label.text = "Back";
                    label.alignment = TextAlignmentOptions.Center;
                    label.fontSize = 16;
                    label.color = Color.white;
                }

                if (joinSelectionButton != null)
                {
                    var rect = joinSelectionButton.GetComponent<RectTransform>();
                    if (rect != null)
                        rect.sizeDelta = new Vector2(200, 34);
                    var layout = joinSelectionButton.GetComponent<LayoutElement>();
                    if (layout == null)
                        layout = joinSelectionButton.gameObject.AddComponent<LayoutElement>();
                    layout.preferredHeight = 34;
                    layout.preferredWidth = 200;
                    layout.minHeight = 34;
                    layout.minWidth = 200;
                }
            }
        }

        private void RefreshRuntimeBindings()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayClicked);
                playButton.onClick.AddListener(OnPlayClicked);
            }

            if (createTabButton != null)
                createTabButton.onClick.RemoveListener(OnCreateTabClicked);

            if (joinTabButton != null)
                joinTabButton.onClick.RemoveListener(OnJoinTabClicked);

            if (createConfirmButton != null)
            {
                createConfirmButton.onClick.RemoveListener(OnCreateConfirmClicked);
                createConfirmButton.onClick.AddListener(OnCreateConfirmClicked);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(BackToMain);
                backButton.onClick.AddListener(BackToMain);
            }

            if (joinSelectionButton != null)
            {
                joinSelectionButton.onClick.RemoveListener(JoinSelectedSession);
                joinSelectionButton.onClick.AddListener(JoinSelectedSession);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[StartMenuUI] Scene loaded: '{scene.name}' mode={mode}");
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
                return;

            if (scene.name == gameplaySceneName)
                HideMenuUI();
        }

        private bool HasAnySessions()
        {
            if (_sessionList == null)
                return false;

            foreach (var info in _sessionList)
            {
                if (info.IsValid)
                    return true;
            }

            return false;
        }

        private void DisableForeignButtons(Transform root)
        {
            if (root == null)
                return;

            var buttons = root.GetComponentsInChildren<Button>(true);
            foreach (var button in buttons)
            {
                if (button == null)
                    continue;

                if (button == playButton || button == createTabButton || button == joinTabButton ||
                    button == createConfirmButton || button == backButton || button == joinSelectionButton)
                    continue;

                if (sessionListRoot != null && button.transform.IsChildOf(sessionListRoot))
                    continue;

                if (button.name.Contains("Host") || button.name.Contains("Client") || button.name.Contains("Auto"))
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                button.interactable = false;
            }
        }

        private static Button FindButtonByName(string name)
        {
            var buttons = FindObjectsOfType<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn != null && btn.name.Equals(name))
                    return btn;
            }
            return null;
        }

        private static GameObject FindChildByName(Transform root, string name)
        {
            if (root == null)
                return null;

            foreach (Transform child in root)
            {
                if (child.name == name)
                    return child.gameObject;

                var found = FindChildByName(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void CleanupJoinPanel(Transform joinPanelRoot)
        {
            if (joinPanelRoot == null)
                return;

            foreach (Transform child in joinPanelRoot)
            {
                if (child == null)
                    continue;

                if (child.name == "SessionListScroll" || child.name == "SessionListRoot")
                    continue;

                child.gameObject.SetActive(false);
            }
        }

        private void BuildJoinPanelContent(Transform joinPanelRoot)
        {
            if (joinPanelRoot == null)
                return;

            for (int i = joinPanelRoot.childCount - 1; i >= 0; i--)
                Destroy(joinPanelRoot.GetChild(i).gameObject);

            var scrollGo = new GameObject("SessionListScroll");
            var scrollRect = scrollGo.AddComponent<RectTransform>();
            scrollRect.SetParent(joinPanelRoot, false);
            scrollRect.sizeDelta = new Vector2(400, 240);
            var scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = new Color(0.9f, 0.9f, 0.9f, 0.98f);
            var scroll = scrollGo.AddComponent<ScrollRect>();

            var content = new GameObject("SessionListRoot");
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.SetParent(scrollGo.transform, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;

            var layoutElement = scrollGo.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 400;
            layoutElement.preferredHeight = 240;

            sessionListRoot = content.transform;
            noGamesLabel = null;
        }

        private void RefreshJoinPanelReferences()
        {
            if (joinPanel == null)
                return;

            sessionListRoot = FindChildByName(joinPanel.transform, "SessionListRoot")?.transform;
            noGamesLabel = null;
        }

        private SessionListEntryUI CreateRuntimeSessionEntry(Transform parent)
        {
            var root = new GameObject("SessionEntry");
            var rect = root.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(360, 82);
            var layoutElement = root.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 360;
            layoutElement.preferredHeight = 82;
            var bg = root.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.98f);

            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 2;
            layout.padding = new RectOffset(8, 8, 6, 6);

            var entry = root.AddComponent<SessionListEntryUI>();
            var name = CreateText("GameName", root.transform);
            name.fontSize = 16;
            name.alignment = TextAlignmentOptions.MidlineLeft;
            name.color = Color.black;

            var host = CreateText("HostName", root.transform);
            host.fontSize = 14;
            host.alignment = TextAlignmentOptions.MidlineLeft;
            host.color = Color.black;

            var row3 = new GameObject("Row3");
            var rowRect = row3.AddComponent<RectTransform>();
            rowRect.SetParent(root.transform, false);
            var rowLayout = row3.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = true;
            rowLayout.spacing = 10;
            var rowLayoutElement = row3.AddComponent<LayoutElement>();
            rowLayoutElement.preferredWidth = 360;
            rowLayoutElement.minHeight = 18;

            var map = CreateText("MapName", row3.transform);
            map.fontSize = 14;
            map.color = Color.black;
            var count = CreateText("Players", row3.transform);
            count.fontSize = 14;
            count.color = Color.black;

            entry.Bind(name, host, map, count, null, bg);

            return entry;
        }

        private static TMP_Text CreateText(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = Color.black;
            return text;
        }

        private static Button CreateButton(string label, Transform parent)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.98f);
            var btn = go.AddComponent<Button>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 16;
            text.color = Color.black;
            return btn;
        }

        private static Button CreateButton(string name, Transform parent, string label)
        {
            var btn = CreateButton(label, parent);
            btn.gameObject.name = name;
            return btn;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.92f, 0.92f, 0.92f, 0.98f);
            return go;
        }

        private static TMP_Text CreateLabel(string text, Transform parent)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 18;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = Color.black;
            return label;
        }

        private static TMP_InputField CreateInputField(string name, Transform parent, string placeholder)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(480, 34);

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.95f);

            var input = go.AddComponent<TMP_InputField>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18;
            text.color = new Color(0f, 0f, 0f, 1f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(10f, 6f);
            textRect.offsetMax = new Vector2(-10f, -6f);

            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(go.transform, false);
            var placeholderText = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholderText.fontSize = 18;
            placeholderText.color = new Color(0f, 0f, 0f, 0.5f);
            placeholderText.alignment = TextAlignmentOptions.MidlineLeft;
            placeholderText.text = placeholder;
            var placeholderRect = placeholderText.GetComponent<RectTransform>();
            placeholderRect.anchorMin = new Vector2(0f, 0f);
            placeholderRect.anchorMax = new Vector2(1f, 1f);
            placeholderRect.offsetMin = new Vector2(10f, 6f);
            placeholderRect.offsetMax = new Vector2(-10f, -6f);

            input.textComponent = text;
            input.placeholder = placeholderText;
            input.pointSize = 20;

            return input;
        }

        private static TMP_Dropdown CreateDropdown(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(480, 34);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.95f);

            var dropdown = go.AddComponent<TMP_Dropdown>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 18;
            label.color = new Color(0f, 0f, 0f, 1f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(10f, 6f);
            labelRect.offsetMax = new Vector2(-30f, -6f);

            var arrowGo = new GameObject("Arrow");
            arrowGo.transform.SetParent(go.transform, false);
            var arrowText = arrowGo.AddComponent<TextMeshProUGUI>();
            arrowText.text = "v";
            arrowText.fontSize = 18;
            arrowText.alignment = TextAlignmentOptions.MidlineRight;
            var arrowRect = arrowText.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = new Vector2(1f, 1f);
            arrowRect.sizeDelta = new Vector2(20f, 0f);
            arrowRect.anchoredPosition = new Vector2(-10f, 0f);

            var template = new GameObject("Template");
            var templateRect = template.AddComponent<RectTransform>();
            templateRect.SetParent(go.transform, false);
            templateRect.sizeDelta = new Vector2(0, 200);
            var templateImg = template.AddComponent<Image>();
            templateImg.color = new Color(1f, 1f, 1f, 0.95f);
            var scrollRect = template.AddComponent<ScrollRect>();
            template.SetActive(false);

            var viewport = new GameObject("Viewport");
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.SetParent(template.transform, false);
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content");
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.SetParent(viewport.transform, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;

            var item = new GameObject("Item");
            var itemRect = item.AddComponent<RectTransform>();
            itemRect.SetParent(content.transform, false);
            itemRect.sizeDelta = new Vector2(0f, 30f);
            var itemToggle = item.AddComponent<Toggle>();
            var itemBg = item.AddComponent<Image>();
            itemBg.color = new Color(1f, 1f, 1f, 1f);
            itemToggle.targetGraphic = itemBg;

            var itemLabelGo = new GameObject("ItemLabel");
            itemLabelGo.transform.SetParent(item.transform, false);
            var itemLabel = itemLabelGo.AddComponent<TextMeshProUGUI>();
            itemLabel.fontSize = 18;
            itemLabel.color = new Color(0f, 0f, 0f, 1f);
            itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var itemLabelRect = itemLabel.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = new Vector2(0f, 0f);
            itemLabelRect.anchorMax = new Vector2(1f, 1f);
            itemLabelRect.offsetMin = new Vector2(10f, 4f);
            itemLabelRect.offsetMax = new Vector2(-10f, -4f);

            dropdown.template = templateRect;
            dropdown.captionText = label;
            dropdown.itemText = itemLabel;
            dropdown.itemImage = null;
            dropdown.targetGraphic = img;

            return dropdown;
        }

        private void ApplyTheme()
        {
            if (menuRoot != null)
            {
                var buttons = menuRoot.GetComponentsInChildren<Button>(true);
                foreach (var button in buttons)
                    ApplyButtonTheme(button);
            }

            ApplyButtonTheme(playButton);
            ApplyButtonTheme(createTabButton);
            ApplyButtonTheme(joinTabButton);
            ApplyButtonTheme(createConfirmButton);
            ApplyButtonTheme(backButton);
            ApplyButtonTheme(joinSelectionButton);
            ApplyButtonTheme(hostButton);
            ApplyButtonTheme(clientButton);
            ApplyButtonTheme(autoButton);
        }

        private static void ApplyButtonTheme(Button button)
        {
            if (button == null)
                return;

            var img = button.GetComponent<Image>();
            if (img != null)
                img.color = new Color(1f, 1f, 1f, 0.98f);

            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.color = Color.black;

            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.98f);
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private void HideForeignPanels(Transform root, Transform contentRoot)
        {
            if (root == null || contentRoot == null)
                return;

            foreach (Transform child in root)
            {
                if (child == contentRoot)
                    continue;

                if (child == createPanel || child == joinPanel)
                    continue;

                child.gameObject.SetActive(false);
            }
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            _sessionList = sessionList;
            RebuildSessionList();
            if (playMenuRoot != null && playMenuRoot.activeSelf)
                ShowCreatePanel(createPanel == null || createPanel.activeSelf);
        }

        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}

