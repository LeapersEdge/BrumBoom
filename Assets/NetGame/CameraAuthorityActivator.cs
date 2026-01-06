using Fusion;
using UnityEngine;

namespace NetGame
{
    /// <summary>
    /// Enables the camera only on the instance that has InputAuthority.
    /// Attach to the NetworkObject root (e.g., Police_car_Network) and assign the camera GameObject and AudioListener.
    /// </summary>
    public class CameraAuthorityActivator : NetworkBehaviour
    {
        [SerializeField] private GameObject cameraRoot;
        [SerializeField] private AudioListener audioListener;

        public override void Spawned()
        {
            bool isLocal = Object != null && Object.HasInputAuthority;

#region agent log
            try
            {
                var payload = "{\"sessionId\":\"debug-session\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H7\",\"location\":\"CameraAuthorityActivator:Spawned\",\"message\":\"camera toggle\",\"data\":{\"isLocal\":" + (isLocal ? 1 : 0) + ",\"hasCam\":" + (cameraRoot != null ? 1 : 0) + ",\"hasAudio\":" + (audioListener != null ? 1 : 0) + "},\"timestamp\":" + System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}";
                System.IO.File.AppendAllText(@"c:\Users\marti\Desktop\FER\UMRIGR\project\My project\.cursor\debug.log", payload + "\n", System.Text.Encoding.UTF8);
            }
            catch { }
#endregion

            if (cameraRoot != null)
                cameraRoot.SetActive(isLocal);

            if (audioListener != null)
                audioListener.enabled = isLocal;

        }
    }
}


