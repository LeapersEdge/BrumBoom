using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace NetGame
{
    /// <summary>
    /// Simple UI bridge for Host/Client/Auto buttons.
    /// </summary>
    public class StartMenuUI : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "Gameplay";
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button autoButton;

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
        }

        private void OnDestroy()
        {
            if (hostButton != null)
                hostButton.onClick.RemoveListener(OnHostClicked);
            if (clientButton != null)
                clientButton.onClick.RemoveListener(OnClientClicked);
            if (autoButton != null)
                autoButton.onClick.RemoveListener(OnAutoClicked);
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
                Debug.LogError($"Failed to load scene '{gameplaySceneName}'. Is it added to Build Settings?");
                yield break;
            }
            Debug.Log($"[StartMenuUI] Loading scene '{gameplaySceneName}' for mode {mode}...");
            while (!op.isDone)
                yield return null;

            // find bootstrap in loaded scene
            var bootstrap = FindObjectOfType<GameBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("GameBootstrap not found in gameplay scene.");
                yield break;
            }
            Debug.Log($"[StartMenuUI] Scene loaded. Starting mode {mode}.");

#region agent log
            try
            {
                var payload = "{\"sessionId\":\"debug-session\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H6\",\"location\":\"StartMenuUI:LoadAndStart\",\"message\":\"button click\",\"data\":{\"mode\":\"" + mode + "\"},\"timestamp\":" + System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}";
                System.IO.File.AppendAllText(@"c:\Users\marti\Desktop\FER\UMRIGR\project\My project\.cursor\debug.log", payload + "\n", System.Text.Encoding.UTF8);
            }
            catch { }
#endregion

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
        }

        private enum GameMode
        {
            Host,
            Client,
            AutoHostOrClient
        }
    }
}

