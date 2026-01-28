using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NetGame
{
    public class MatchStateController : MonoBehaviour
    {
        private enum MatchState
        {
            Waiting,
            Running,
            Finished,
            Closed
        }

        [SerializeField] private Vector2 bannerSize = new Vector2(420, 30);
        [SerializeField] private float bannerY = -12f;
        [SerializeField] private string waitingText = "Waiting for players to come";
        [SerializeField] private string winText = "You have won";

        private NetworkRunner _runner;
        private MatchState _state;
        private TMP_Text _bannerText;
        private bool _initialized;
        private bool _ghostApplied;
        private int _maxPlayersSeen;

        private void Awake()
        {
            EnsureRunner();
            BuildBanner();
            InitializeState();
        }

        private void Update()
        {
            EnsureRunner();
            if (_runner == null || !_runner.IsRunning)
                return;

            int maxPlayers = _runner.SessionInfo.MaxPlayers;
            int playerCount = _runner.SessionInfo.PlayerCount;
            if (playerCount > _maxPlayersSeen)
                _maxPlayersSeen = playerCount;

            if (_state == MatchState.Waiting && maxPlayers > 0 && playerCount >= maxPlayers)
            {
                SetState(MatchState.Running);
            }
            else if (_state == MatchState.Running)
            {
                int alive = CountAlivePlayers();
                if (_maxPlayersSeen > 1 && alive <= 1)
                    SetState(MatchState.Finished);
            }

            if (_state == MatchState.Waiting && _runner.IsServer && !_ghostApplied)
                ApplyWaitingGhost();
        }

        private void EnsureRunner()
        {
            if (_runner == null && GameBootstrap.Instance != null)
                _runner = GameBootstrap.Instance.GetComponent<NetworkRunner>();
        }

        private void InitializeState()
        {
            if (_initialized)
                return;

            _initialized = true;

            if (_runner != null)
            {
                int maxPlayers = _runner.SessionInfo.MaxPlayers;
                int playerCount = _runner.SessionInfo.PlayerCount;
                _maxPlayersSeen = playerCount;
                _state = (maxPlayers > 0 && playerCount >= maxPlayers) ? MatchState.Running : MatchState.Waiting;
            }
            else
            {
                _state = MatchState.Waiting;
            }

            ApplyStateEffects();
        }

        private void SetState(MatchState newState)
        {
            if (_state == newState)
                return;

            _state = newState;
            ApplyStateEffects();
        }

        private void ApplyStateEffects()
        {
            if (_runner != null && _runner.IsServer)
            {
                if (_state == MatchState.Waiting)
                    ApplyWaitingGhost();
                else
                    ClearWaitingGhost();
            }

            UpdateBanner();
        }

        private void ApplyWaitingGhost()
        {
            foreach (var nh in FindObjectsOfType<NetworkHealth>())
            {
                if (nh != null)
                    nh.ForceGhost(true);
            }
            _ghostApplied = true;
        }

        private void ClearWaitingGhost()
        {
            foreach (var nh in FindObjectsOfType<NetworkHealth>())
            {
                if (nh != null)
                    nh.ForceGhost(false);
            }
            _ghostApplied = false;
        }

        private void UpdateBanner()
        {
            if (_bannerText == null)
                return;

            if (_state == MatchState.Waiting)
            {
                _bannerText.text = waitingText;
                _bannerText.gameObject.SetActive(true);
                return;
            }

            if (_state == MatchState.Finished)
            {
                var local = FindLocalPlayer();
                bool won = local != null && !local.IsEliminated;
                _bannerText.text = won ? winText : string.Empty;
                _bannerText.gameObject.SetActive(won);
                return;
            }

            _bannerText.text = string.Empty;
            _bannerText.gameObject.SetActive(false);
        }

        private int CountAlivePlayers()
        {
            int alive = 0;
            foreach (var nh in FindObjectsOfType<NetworkHealth>())
            {
                if (nh != null && !nh.IsEliminated)
                    alive++;
            }
            return alive;
        }

        private NetworkHealth FindLocalPlayer()
        {
            foreach (var nh in FindObjectsOfType<NetworkHealth>())
            {
                var no = nh != null ? nh.Object : null;
                if (no != null && no.HasInputAuthority)
                    return nh;
            }
            return null;
        }

        private void BuildBanner()
        {
            var canvasGo = new GameObject("MatchStateCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var textGo = new GameObject("MatchStateText");
            var rect = textGo.AddComponent<RectTransform>();
            rect.SetParent(canvasGo.transform, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, bannerY);
            rect.sizeDelta = bannerSize;

            _bannerText = textGo.AddComponent<TextMeshProUGUI>();
            _bannerText.fontSize = 28;
            _bannerText.alignment = TextAlignmentOptions.Center;
            _bannerText.color = Color.white;
            _bannerText.outlineColor = new Color(0f, 0f, 0f, 0.7f);
            _bannerText.outlineWidth = 0.2f;
        }
    }
}
