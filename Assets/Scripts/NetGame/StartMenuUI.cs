using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NetGame
{
    /// <summary>
    /// Simple UI bridge for Host/Client/Auto buttons.
    /// </summary>
    public partial class StartMenuUI : MonoBehaviour, INetworkRunnerCallbacks
    {
        private const string PlayerNameKey = "PlayerName";
        private const string PlayerNameAutoKey = "PlayerNameAuto";
        private const string MapIndexKey = "MapIndex";
        private const string MapIndexAutoKey = "MapIndexAuto";
        private const string PropertyMap = "map";
        private const string PropertyHost = "host";
        private const string DefaultGameplaySceneName = "Gameplay";
        private string gameplaySceneName = DefaultGameplaySceneName;
        [HideInInspector] [SerializeField] private Button hostButton;
        [HideInInspector] [SerializeField] private Button clientButton;
        [HideInInspector] [SerializeField] private Button autoButton;
        [SerializeField] private Button playButton;
        [HideInInspector] [SerializeField] private Button createTabButton;
        [HideInInspector] [SerializeField] private Button joinTabButton;
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
        [Header("Map Selection")]
        [SerializeField] private string[] mapSceneNames;
        [SerializeField] private string[] mapDisplayNames;
        [SerializeField] private bool excludeGameplayFromMapList = true;
        [SerializeField] private bool randomizePlayerNameOnLaunch = true;
        [SerializeField] private bool randomizeMapSelectionOnLaunch = false;
        [SerializeField] private bool forceRuntimeMapDropdown = true;

        public static string LocalPlayerName { get; private set; } = "Player";

        private bool _clicked;
        private NetworkRunner _lobbyRunner;
        private List<SessionInfo> _sessionList = new();
        private readonly List<SessionListEntryUI> _sessionEntries = new();
        private string _selectedSessionName;
        private bool _uiInitialized;
        private bool _joinPanelBuilt;
        private bool _runtimeMapDropdownCreated;
        private TMP_FontAsset _fallbackFont;
        private readonly List<string> _mapSceneOptions = new();
        private readonly List<string> _mapDisplayOptions = new();

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
            Debug.Log($"[StartMenuUI] LoadMapAndJoin. Map='{mapScene}'");
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

        private void CacheFallbackFont()
        {
            if (_fallbackFont != null)
                return;

            var anyText = GetComponentInChildren<TMP_Text>(true);
            if (anyText != null)
                _fallbackFont = anyText.font;
        }

        private static string GetStringProperty(SessionInfo info, string key)
        {
            if (info.Properties == null || !info.Properties.TryGetValue(key, out var prop))
                return null;

            if (prop.IsString)
                return prop.PropertyValue as string;

            return prop.ToString();
        }

        private void InitializeName()
        {
            string saved = PlayerPrefs.GetString(PlayerNameKey, string.Empty);
            bool auto = PlayerPrefs.GetInt(PlayerNameAutoKey, 1) == 1;
            if (randomizePlayerNameOnLaunch && auto)
                saved = GenerateRandomName();
            else if (string.IsNullOrWhiteSpace(saved) || IsLegacyRandomName(saved))
                saved = GenerateRandomName();

            SetLocalName(saved);
            PlayerPrefs.SetInt(PlayerNameAutoKey, 1);
            PlayerPrefs.Save();

            if (nameInput != null)
                nameInput.text = LocalPlayerName;
        }

        private void OnNameEdited(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                value = GenerateRandomName();

            SetLocalName(value);
            PlayerPrefs.SetInt(PlayerNameAutoKey, 0);
            PlayerPrefs.Save();

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

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[StartMenuUI] Scene loaded: '{scene.name}' mode={mode}");
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
                return;

            if (scene.name == gameplaySceneName)
                HideMenuUI();
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

