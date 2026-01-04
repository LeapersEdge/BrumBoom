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
            // log presence of input each tick to see if runner feeds us at all
#region agent log
            try
            {
                var hasInput = GetInput<CarInput>(out _);
                var payload = "{\"sessionId\":\"debug-session\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H1\",\"location\":\"NetWheelInput:FixedUpdateNetwork\",\"message\":\"tick\",\"data\":{\"hasInput\":" + (hasInput ? 1 : 0) + ",\"hasState\":" + (Object.HasStateAuthority ? 1 : 0) + "},\"timestamp\":" + System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}";
                System.IO.File.AppendAllText(@"c:\Users\marti\Desktop\FER\UMRIGR\project\My project\.cursor\debug.log", payload + "\n", System.Text.Encoding.UTF8);
            }
            catch { }
#endregion

            if (!Object.HasStateAuthority)
                return;

            if (!GetInput(out CarInput input))
                return;

            if (wheelController == null)
                return;

#region agent log
            try
            {
                var payload = "{\"sessionId\":\"debug-session\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H1\",\"location\":\"NetWheelInput:FixedUpdateNetwork\",\"message\":\"apply input\",\"data\":{\"hasState\":" + (Object.HasStateAuthority ? 1 : 0) + ",\"hasInputAuth\":" + (Object.HasInputAuthority ? 1 : 0) + ",\"move\":" + input.Move.y + ",\"steer\":" + input.Steer + ",\"brake\":" + (bool)input.Brake + "},\"timestamp\":" + System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}";
                System.IO.File.AppendAllText(@"c:\Users\marti\Desktop\FER\UMRIGR\project\My project\.cursor\debug.log", payload + "\n", System.Text.Encoding.UTF8);
            }
            catch { }
#endregion

            wheelController.SetExternalInput(input.Move.y, input.Steer, input.Brake);
        }
    }
}


