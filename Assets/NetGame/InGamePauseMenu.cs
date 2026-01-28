using System.Collections;
using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NetGame
{
    public class InGamePauseMenu : MonoBehaviour
    {
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private Button resumeButton;
        private NetworkRunner _runner;
        private bool _isVisible;

        private void Awake()
        {
            EnsureRunner();
            EnsureMenuUI();
            SetVisible(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (menuRoot != null && menuRoot.activeSelf)
                {
                    SetVisible(false);
                }
                else
                {
                    SetVisible(true);
                }
            }

            if (_isVisible)
                UpdateActionLabel();
        }

        private void LateUpdate()
        {
            if (!_isVisible)
                ForceCursorLocked();
        }

        private void EnsureRunner()
        {
            if (_runner != null)
                return;

            if (GameBootstrap.Instance != null)
                _runner = GameBootstrap.Instance.GetComponent<NetworkRunner>();

            if (_runner == null)
                _runner = FindObjectOfType<NetworkRunner>();
        }

        private void EnsureMenuUI()
        {
            if (menuRoot != null)
                return;

            var canvasGo = new GameObject("PauseMenuCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            menuRoot = canvasGo;

            EnsureEventSystem();

            var panel = new GameObject("Panel");
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.SetParent(canvasGo.transform, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(280, 180);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.95f, 0.95f, 0.95f, 0.98f);

            var title = new GameObject("Title");
            var titleRect = title.AddComponent<RectTransform>();
            titleRect.SetParent(panel.transform, false);
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -16f);
            titleRect.sizeDelta = new Vector2(240, 26);
            var titleText = title.AddComponent<TextMeshProUGUI>();
            titleText.text = "Paused";
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontSize = 18;
            titleText.color = Color.black;

            actionButton = CreateButton(panel.transform, "ActionButton", "Leave Match", new Vector2(0f, -50f));
            actionLabel = actionButton.GetComponentInChildren<TMP_Text>(true);
            actionButton.onClick.AddListener(OnActionClicked);
            ApplyButtonColors(actionButton);

            resumeButton = CreateButton(panel.transform, "ResumeButton", "Resume", new Vector2(0f, -95f));
            resumeButton.onClick.AddListener(() => SetVisible(false));
            ApplyButtonColors(resumeButton);
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private static void ApplyButtonColors(Button button)
        {
            if (button == null)
                return;

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

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(200, 32);
            rect.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            var button = go.AddComponent<Button>();

            var textGo = new GameObject("Text");
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.SetParent(go.transform, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 16;
            text.color = Color.black;

            return button;
        }

        private void SetVisible(bool visible)
        {
            if (menuRoot == null)
                return;

            menuRoot.SetActive(visible);
            _isVisible = visible;
            if (visible)
            {
                EnsureRunner();
                UpdateActionLabel();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                ForceCursorLocked();
            }
        }

        private void UpdateActionLabel()
        {
            if (actionLabel == null)
                return;

            EnsureRunner();
            bool isHost = _runner != null && _runner.IsServer;
            actionLabel.text = isHost ? "Close Match" : "Leave Match";
        }

        private void OnActionClicked()
        {
            LeaveMatchHelper.Run(_runner, mainMenuSceneName);
        }

        private class LeaveMatchHelper : MonoBehaviour
        {
            private NetworkRunner _runner;
            private string _sceneName;

            public static void Run(NetworkRunner runner, string sceneName)
            {
                var go = new GameObject("LeaveMatchHelper");
                DontDestroyOnLoad(go);
                var helper = go.AddComponent<LeaveMatchHelper>();
                helper._runner = runner;
                helper._sceneName = sceneName;
                helper.StartCoroutine(helper.LeaveRoutine());
            }

            private IEnumerator LeaveRoutine()
            {
                if (_runner != null)
                {
                    Task shutdown = _runner.Shutdown();
                    while (!shutdown.IsCompleted)
                        yield return null;
                }

                var op = SceneManager.LoadSceneAsync(_sceneName, LoadSceneMode.Single);
                if (op != null)
                {
                    while (!op.isDone)
                        yield return null;
                }

                Destroy(gameObject);
            }
        }

        private void ForceCursorLocked()
        {
            var locker = FindObjectOfType<CursorLocker>();
            if (locker != null)
                locker.enabled = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
