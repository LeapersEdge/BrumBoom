using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NetGame
{
    /// <summary>
    /// Simple UI bridge for Host/Client/Auto buttons.
    /// </summary>
    public class StartMenuUI : MonoBehaviour
    {
        private const string PlayerNameKey = "PlayerName";
        [SerializeField] private string gameplaySceneName = "Gameplay";
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button autoButton;
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private TMP_InputField nameInput;

        public static string LocalPlayerName { get; private set; } = "Player";

        private bool _clicked;

        private void Awake()
        {
            // Keep this GO alive across scene load so coroutine survives.
            DontDestroyOnLoad(gameObject);

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
        }

        private void OnDestroy()
        {
            if (hostButton != null)
                hostButton.onClick.RemoveListener(OnHostClicked);
            if (clientButton != null)
                clientButton.onClick.RemoveListener(OnClientClicked);
            if (autoButton != null)
                autoButton.onClick.RemoveListener(OnAutoClicked);

            if (nameInput != null)
                nameInput.onEndEdit.RemoveListener(OnNameEdited);
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

        private System.Collections.IEnumerator LoadAndStart(GameMode mode)
        {
            _clicked = true;
            SetButtonsInteractable(false);

            // load gameplay scene
            var op = SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Single);
            if (op == null)
            {
                yield break;
            }
            while (!op.isDone)
                yield return null;

            // find bootstrap in loaded scene
            var bootstrap = FindObjectOfType<GameBootstrap>();
            if (bootstrap == null)
            {
                yield break;
            }

            switch (mode)
            {
                case GameMode.Host:
                    bootstrap.StartHost();
                    break;
                case GameMode.Client:
                    bootstrap.StartClient();
                    break;
                case GameMode.AutoHostOrClient:
                    bootstrap.StartAuto();
                    break;
            }

            // lock cursor after we start the network
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // hide menu UI while in gameplay
            if (menuRoot != null)
                menuRoot.SetActive(false);
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
    }
}

