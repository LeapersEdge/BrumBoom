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

        private void Awake()
        {
            Instance = this;
        }

        public void AddSpawnPoint(Transform transform)
        {
            spawnPoints.Add(transform);
        }

        public async void StartHost() => await StartRunner(GameMode.Host);
        public async void StartClient() => await StartRunner(GameMode.Client);

        /// <summary>
        /// Pokuša se spojiti kao klijent; ako nema sessiona, pokreće host.
        /// </summary>
        public async void StartAuto()
        {
            // First try as client; if no session, start as host.
            bool ok = await StartRunner(GameMode.Client);
            if (!ok)
                await StartRunner(GameMode.Host);
        }

        private async Task<bool> StartRunner(GameMode mode)
        {
        // guard: prevent double start
        if (_starting)
        {
            return false;
        }

        // reuse if already running
        if (_runner != null && _runner.IsRunning)
        {
            return true;
        }

        // cleanup stale runner
        if (_runner != null && _runner.IsRunning == false)
        {
            try { await _runner.Shutdown(); } catch { }
            _runner = null;
        }

        // ensure runner exists
        if (_runner == null)
            _runner = GetComponent<NetworkRunner>();
        if (_runner == null)
            _runner = gameObject.AddComponent<NetworkRunner>();

        // ensure scene manager
        var sceneManager = GetComponent<INetworkSceneManager>();
        if (sceneManager == null)
            sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);
        _starting = true;
        var result = await _runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = sessionName,
            Scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex),
            SceneManager = sceneManager
        });
        _starting = false;

        if (result.Ok == false)
        {
            try { await _runner.Shutdown(); } catch { }
            _runner = null;
            return false;
        }

        return true;
        }

        #region INetworkRunnerCallbacks

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer == false)
                return;

            if (carPrefab == null)
            {
                return;
            }

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
            var data = new CarInput
            {
                Move = new Vector2(
                    0f,
                    Input.GetAxisRaw("Vertical")
                ),
                Steer = Input.GetAxisRaw("Horizontal"),
                Turret = ReadTurretInput(),
                Fire = ReadFireInput(),
                Brake = Input.GetKey(KeyCode.Space)
            };

            input.Set(data);
        }

        private static float ReadTurretInput()
        {
            float dir = 0f;
            // NOTE:
            // We intentionally do NOT use LeftArrow/RightArrow here because Unity's default
            // "Horizontal" axis already maps to arrow keys. If we used arrows for turret too,
            // one key press would rotate the turret AND steer the car, causing the "turret
            // slightly rotates the car" bug.
            if (Input.GetKey(KeyCode.Q))
                dir -= 1f;
            if (Input.GetKey(KeyCode.E))
                dir += 1f;
            return Mathf.Clamp(dir, -1f, 1f);
        }

        private static NetworkBool ReadFireInput()
        {
            bool pressed = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
            return pressed;
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

