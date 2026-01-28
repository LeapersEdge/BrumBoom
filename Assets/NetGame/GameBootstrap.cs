using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace NetGame
{
    /// <summary>
    /// Starts the runner in Host/Client mode and handles player join/leave and input.
    /// Attach to an empty GameObject in the scene.
    /// </summary>
    [RequireComponent(typeof(NetworkRunner))]
    public class GameBootstrap : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static GameBootstrap Instance { get; private set; }

        [Header("Prefabs & Scene")]
        [SerializeField] private NetworkObject carPrefab;
        [SerializeField] private List<Transform> spawnPoints = new();
        [SerializeField] private string sessionName = "Room";

        private NetworkRunner _runner;
        private int _nextSpawn;
        private bool _starting;
        private string _pendingSessionName;
        private int _pendingMaxPlayers = -1;
        private Dictionary<string, SessionProperty> _pendingSessionProperties;

        private void Awake()
        {
            Instance = this;
            if (GetComponent<InGamePauseMenu>() == null)
                gameObject.AddComponent<InGamePauseMenu>();
        }

        public void AddSpawnPoint(Transform transform)
        {
            spawnPoints.Add(transform);
        }

        public async void StartHost() => await StartRunner(GameMode.Host);
        public async void StartClient() => await StartRunner(GameMode.Client);

        public async void StartAuto()
        {
            bool ok = await StartRunner(GameMode.Client);
            if (!ok)
                await StartRunner(GameMode.Host);
        }

        private async Task<bool> StartRunner(GameMode mode)
        {
            // guard: prevent double start
            if (_starting) return false;

            // reuse if already running
            if (_runner != null && _runner.IsRunning) return true;

            // cleanup stale runner
            if (_runner != null && _runner.IsRunning == false)
            {
                try { await _runner.Shutdown(); } catch { }
                _runner = null;
            }

            // ensure runner exists
            if (_runner == null) _runner = GetComponent<NetworkRunner>();

            // ensure scene manager
            var sceneManager = GetComponent<INetworkSceneManager>();
            if (sceneManager == null)
                sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

            _runner.ProvideInput = true;
            _runner.AddCallbacks(this);
            _starting = true;

            string chosenSessionName = string.IsNullOrWhiteSpace(_pendingSessionName) ? sessionName : _pendingSessionName;
            Debug.Log($"[GameBootstrap] StartRunner: mode={mode}, session='{chosenSessionName}', sceneIndex={UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex}");
            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = chosenSessionName,
                Scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex),
                SceneManager = sceneManager
            };

            if (_pendingMaxPlayers > 0)
                args.PlayerCount = _pendingMaxPlayers;

            if (_pendingSessionProperties != null && _pendingSessionProperties.Count > 0)
                args.SessionProperties = _pendingSessionProperties;

            var result = await _runner.StartGame(args);

            _starting = false;

            if (result.Ok == false)
            {
                Debug.LogError($"[GameBootstrap] StartGame failed: {result.ShutdownReason} - {result.ErrorMessage}");
                try { await _runner.Shutdown(); } catch { }
                _runner = null;
                return false;
            }
            Debug.Log("[GameBootstrap] StartGame OK.");

            _pendingSessionName = null;
            _pendingMaxPlayers = -1;
            _pendingSessionProperties = null;
            return true;
        }

        public void ConfigureSession(string name, int maxPlayers, Dictionary<string, SessionProperty> properties)
        {
            _pendingSessionName = string.IsNullOrWhiteSpace(name) ? null : name;
            _pendingMaxPlayers = maxPlayers;
            _pendingSessionProperties = properties;
        }

        #region INetworkRunnerCallbacks

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer == false) return;
            if (carPrefab == null) return; 

            var spawn = spawnPoints.Count > 0
                ? spawnPoints[_nextSpawn++ % spawnPoints.Count]
                : null;

            Vector3 pos = spawn ? spawn.position : Vector3.zero;
            Quaternion rot = spawn ? spawn.rotation : Quaternion.identity;

            var car = runner.Spawn(carPrefab, pos, rot, player);
            runner.SetPlayerObject(player, car);
        }

        public bool TryGetSpawn(out Vector3 pos, out Quaternion rot)
        {
            if (spawnPoints.Count > 0)
            {
                var spawn = spawnPoints[_nextSpawn++ % spawnPoints.Count];
                pos = spawn.position;
                rot = spawn.rotation;
                return true;
            }

            pos = Vector3.zero;
            rot = Quaternion.identity;
            return false;
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner.TryGetPlayerObject(player, out var obj))
            {
                runner.Despawn(obj);
            }
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (IsLocalEliminated(runner))
            {
                input.Set(new CarInput());
                return;
            }

            var data = new CarInput
            {
                Move = new Vector2(
                    0f,
                    Input.GetAxisRaw("Vertical")
                ),
                Steer = Input.GetAxisRaw("Horizontal"),
                TurretDir = ReadTurretDir(),
                Fire = ReadFireInput(),
                Brake = Input.GetKey(KeyCode.Space)
            };

            input.Set(data);
        }

        private static bool IsLocalEliminated(NetworkRunner runner)
        {
            if (runner == null) return false;
            if (!runner.TryGetPlayerObject(runner.LocalPlayer, out var obj)) return false;
            var health = obj.GetComponent<NetworkHealth>();
            return health != null && health.IsEliminated;
        }

        private static Vector2 ReadTurretDir()
        {
            var cam = Camera.main;
            if (cam == null)
                return Vector2.zero;

            Vector3 viewDir = cam.transform.forward;
            viewDir.y = 0f;
            if (viewDir.sqrMagnitude < 0.0001f)
                return Vector2.zero;

            viewDir.Normalize();
            return new Vector2(viewDir.x, viewDir.z);
        }

        private static NetworkBool ReadFireInput()
        {
            // Only left mouse button for firing (Space removed to avoid conflict with brake)
            return Input.GetMouseButton(0);
        }

        // Unused callbacks for this prototype
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        // Fusion 2 requires AOI callbacks on INetworkRunnerCallbacks
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        #endregion

    }
}

