using Fusion;
using UnityEngine;

namespace NetGame
{
    /// <summary>
    /// Synchronizes gun rotation across the network.
    /// Works with GunController to sync rotation calculated on local instance to remote instances.
    /// 
    /// Setup:
    /// 1. Attach this component to the same GameObject that has NetworkObject (usually the car/player root)
    /// 2. Set gunTransform to the Transform of the gun that should rotate (usually a child GameObject named "Gun")
    /// 3. gunController will be auto-found from the same GameObject, or set it manually if needed
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetGunRotation : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform gunTransform;
        [SerializeField] private GunController gunController;

        [Header("Aiming")]
        [SerializeField] private float rotationSpeed = 5f;
        [SerializeField] private float yawOffsetDegrees = 0f;

        [Networked] private Quaternion NetworkedGunRotation { get; set; }
        
        // Track if we've initialized
        private bool _initialized;

        public override void Spawned()
        {
            // Auto-find GunController
            if (gunController == null)
                gunController = GetComponent<GunController>();

            if (gunController == null)
                gunController = GetComponentInParent<GunController>();

            if (gunController == null)
                gunController = GetComponentInChildren<GunController>();

            // Auto-find gunTransform if not set (look for "Gun" child)
            if (gunTransform == null)
            {
                // First try direct child named "Gun"
                Transform gunChild = transform.Find("Gun");
                
                // If not found, search all children recursively for anything with "Gun" in the name
                if (gunChild == null)
                {
                    foreach (Transform child in transform.GetComponentsInChildren<Transform>(includeInactive: true))
                    {
                        if (child != transform && child.name.Contains("Gun"))
                        {
                            gunChild = child;
                            break;
                        }
                    }
                }
                
                gunTransform = gunChild;
            }

            // Initialize rotation
            if (gunTransform != null)
            {
                NetworkedGunRotation = gunTransform.localRotation;
                _initialized = true;
            }
            else
            {
                Debug.LogWarning($"[NetGunRotation] Could not find gunTransform on {gameObject.name}. " +
                    $"Please set it manually in the Inspector. Looking for a child GameObject named 'Gun'.");
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (gunTransform == null || !_initialized)
                return;

            // State Authority owns the networked state. Use input to compute the turret rotation.
            if (!Object.HasStateAuthority)
                return;

            var health = GetComponent<NetworkHealth>();
            if (health != null && health.IsEliminated)
                return;

            if (!Runner.TryGetInputForPlayer(Object.InputAuthority, out CarInput input))
                return;

            Vector3 viewDir = new Vector3(input.TurretDir.x, 0f, input.TurretDir.y);
            if (viewDir.sqrMagnitude < 0.0001f)
                viewDir = transform.forward;

            viewDir.Normalize();

            Quaternion targetWorld = Quaternion.LookRotation(viewDir, Vector3.up) * Quaternion.Euler(0f, yawOffsetDegrees, 0f);
            Quaternion targetLocal = gunTransform.parent != null
                ? Quaternion.Inverse(gunTransform.parent.rotation) * targetWorld
                : targetWorld;

            NetworkedGunRotation = Quaternion.Slerp(
                NetworkedGunRotation,
                targetLocal,
                rotationSpeed * Runner.DeltaTime);
        }

        public override void Render()
        {
            if (gunTransform == null || !_initialized)
                return;

            // On remote instances: apply the synchronized rotation every frame
            // Render() is called every frame (typically 60 FPS) for smooth visual updates
            if (!Object.HasInputAuthority)
            {
                // Apply rotation directly - this ensures it's updated every frame
                // The rotation is already smoothed by GunController on the local instance,
                // so we just need to apply the synchronized value here
                gunTransform.localRotation = NetworkedGunRotation;
            }
            // On local instance: GunController.Update() handles rotation, nothing to do here
        }
    }
}
