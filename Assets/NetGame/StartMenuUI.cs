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
        private const string PlayerNameAutoKey = "PlayerNameAuto";
        private const string MapIndexKey = "MapIndex";
        private const string MapIndexAutoKey = "MapIndexAuto";
        private const string PropertyMap = "map";
        private const string PropertyHost = "host";
        [SerializeField] private string gameplaySceneName = "Gameplay"; // Fallback scene name
        [SerializeField] private Button createConfirmButton;
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
        [SerializeField] private string[] mapSceneNames;
        [SerializeField] private string[] mapDisplayNames;
        [SerializeField] private bool randomizePlayerNameOnLaunch = true;
        [SerializeField] private bool randomizeMapSelectionOnLaunch = true;

        public static string LocalPlayerName { get; private set; } = "Player";

        private List<SessionInfo> _sessionList = new();
        private readonly List<SessionListEntryUI> _sessionEntries = new();
        private string _selectedSessionName;
        private bool _uiInitialized;
        private TMP_FontAsset _fallbackFont;
        private NetworkRunner _lobbyRunner;
        private static StartMenuUI _instance;

        private void Awake()
        {
            // Singleton pattern - destroy duplicates when returning to menu scene
            if (_instance != null && _instance != this)
            {
                Debug.Log("[StartMenuUI] Destroying duplicate StartMenuUI instance");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Keep this GO alive across scene load so coroutine survives.
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (menuRoot == null)
                menuRoot = gameObject;

            if (nameInput == null)
                nameInput = GetComponentInChildren<TMP_InputField>(true);

            InitializeName();

            if (nameInput != null)
                nameInput.onEndEdit.AddListener(OnNameEdited);

            if (createConfirmButton != null)
                createConfirmButton.onClick.AddListener(OnCreateConfirmClicked);

            TryAutoWirePlayMenuRoots();
            AutoWireMenuElements();
            RefreshRuntimeBindings();
            CacheFallbackFont();
            InitializeMapDropdown();
            ShowPlayMenu(false);
            _ = EnsureLobbyRunner();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (nameInput != null)
                nameInput.onEndEdit.RemoveListener(OnNameEdited);

            if (createConfirmButton != null)
                createConfirmButton.onClick.RemoveListener(OnCreateConfirmClicked);
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

        private void ShowMenuUI()
        {
            if (menuRoot != null && menuRoot != gameObject)
            {
                menuRoot.SetActive(true);
                return;
            }

            if (mainButtonsRoot != null)
                mainButtonsRoot.SetActive(true);
            if (nameInput != null)
                nameInput.gameObject.SetActive(true);
        }

        private void EnsureNameInputPlacement()
        {
            if (nameInput == null || mainButtonsRoot == null)
                return;

            if (nameInput.transform.parent != mainButtonsRoot.transform)
                nameInput.transform.SetParent(mainButtonsRoot.transform, false);
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
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"Failed to load scene '{mapScene}' for hosting.");
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
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"Failed to load scene '{mapScene}' for joining.");
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

            mapDropdown.onValueChanged.RemoveListener(OnMapDropdownChanged);
            mapDropdown.onValueChanged.AddListener(OnMapDropdownChanged);
            
            // Fix dropdown template rendering issue in DontDestroyOnLoad canvas
            if (mapDropdown.template != null)
            {
                // Ensure the template is active initially so it can be properly initialized
                var templateGo = mapDropdown.template.gameObject;
                bool wasActive = templateGo.activeSelf;
                templateGo.SetActive(true);
                
                // Make sure the template has a Canvas component for proper rendering
                var templateCanvas = templateGo.GetComponent<Canvas>();
                if (templateCanvas == null)
                {
                    templateCanvas = templateGo.AddComponent<Canvas>();
                    templateCanvas.overrideSorting = true;
                    templateCanvas.sortingOrder = 30000; // Very high sorting order to appear on top
                }
                
                // Add GraphicRaycaster if missing
                if (templateGo.GetComponent<GraphicRaycaster>() == null)
                {
                    templateGo.AddComponent<GraphicRaycaster>();
                }
                
                // Restore original active state
                templateGo.SetActive(wasActive);
            }

            if (options.Count > 1)
            {
                bool auto = PlayerPrefs.GetInt(MapIndexAutoKey, 1) == 1;
                if (randomizeMapSelectionOnLaunch && auto)
                {
                    int idx = Random.Range(0, options.Count);
                    mapDropdown.value = idx;
                    PlayerPrefs.SetInt(MapIndexKey, idx);
                    PlayerPrefs.SetInt(MapIndexAutoKey, 1);
                    PlayerPrefs.Save();
                }
                else if (PlayerPrefs.HasKey(MapIndexKey))
                {
                    int idx = Mathf.Clamp(PlayerPrefs.GetInt(MapIndexKey, 0), 0, options.Count - 1);
                    mapDropdown.value = idx;
                }
            }
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
                if (info.MaxPlayers > 0 && info.PlayerCount >= info.MaxPlayers)
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

        private void OnMapDropdownChanged(int index)
        {
            PlayerPrefs.SetInt(MapIndexKey, index);
            PlayerPrefs.SetInt(MapIndexAutoKey, 0);
            PlayerPrefs.Save();
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

        private void RefreshRuntimeBindings()
        {
            if (createConfirmButton != null)
            {
                createConfirmButton.onClick.RemoveListener(OnCreateConfirmClicked);
                createConfirmButton.onClick.AddListener(OnCreateConfirmClicked);
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
            
            // Check if we loaded a gameplay scene (hide menu)
            bool isGameplayScene = false;
            
            // Check against all configured map scenes
            if (mapSceneNames != null)
            {
                foreach (var mapScene in mapSceneNames)
                {
                    if (scene.name == mapScene)
                    {
                        isGameplayScene = true;
                        break;
                    }
                }
            }
            
            // Also check the legacy gameplaySceneName
            if (!string.IsNullOrWhiteSpace(gameplaySceneName) && scene.name == gameplaySceneName)
            {
                isGameplayScene = true;
            }
            
            if (isGameplayScene)
            {
                HideMenuUI();
            }
            else
            {
                // Returned to menu scene - show the menu again
                ShowMenuUI();
                
                // Reset UI state
                ShowPlayMenu(false);
                
                // Unlock cursor
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
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

