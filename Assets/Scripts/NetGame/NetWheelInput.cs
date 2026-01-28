using Fusion;
using UnityEngine;

namespace NetGame
{
    /// <summary>
    /// Fusion input relay for the existing WheelController.
    /// Runs on State Authority, applies CarInput to wheel colliders.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetWheelInput : NetworkBehaviour
    {
        [SerializeField] private WheelController wheelController;

        private void Awake()
        {
            if (wheelController == null)
                wheelController = GetComponentInChildren<WheelController>();
#region agent log
            try
            {
                var payload = "{\"sessionId\":\"debug-session\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H5\",\"location\":\"NetWheelInput:Awake\",\"message\":\"wc ref\",\"data\":{\"hasWC\":" + (wheelController != null ? 1 : 0) + "},\"timestamp\":" + System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}";
                System.IO.File.AppendAllText(@"c:\Users\marti\Desktop\FER\UMRIGR\project\My project\.cursor\debug.log", payload + "\n", System.Text.Encoding.UTF8);
            }
            catch { }
#endregion
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            if (!Runner.TryGetInputForPlayer(Object.InputAuthority, out CarInput input)) return;
            if (wheelController == null) return;
            var health = GetComponent<NetworkHealth>();
            if (health != null && health.IsEliminated) return;

            wheelController.SetExternalInput(input.Move.y, input.Steer, input.Brake);
        }
    }
}


