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

            if (cameraRoot != null)
                cameraRoot.SetActive(isLocal);

            if (audioListener != null)
                audioListener.enabled = isLocal;

        }
    }
}


