using Fusion;
using UnityEngine;

namespace NetGame
{
    /// <summary>
    /// Local-only orbit camera: pivots around target (player/car) with yaw/pitch and keeps distance.
    /// Works only on the instance that has InputAuthority.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class NetCameraLook : NetworkBehaviour
    {
        [SerializeField] private Transform target; // player/car root or look target
        [SerializeField] private float sensitivity = 2f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private float startYaw = 0f;
        [SerializeField] private Vector3 startOffset = new Vector3(0f, 5f, -8f); // higher & behind
        private float _yaw;
        private float _pitch;
        private float _distance;
        private bool _initialized;

        public override void Spawned()
        {
            if (target == null)
            {
                var no = GetComponentInParent<NetworkObject>();
                if (no != null) target = no.transform;
            }

            _initialized = false;
        }

        private void Update()
        {
            if (Object != null && Object.HasInputAuthority == false)
                return;

            if (target == null)
                return;

            if (!_initialized)
                InitializeFromOffset();

            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");

            _yaw += mx * sensitivity;
            _pitch = Mathf.Clamp(_pitch - my * sensitivity, minPitch, maxPitch);

            // Orbit relative to target rotation
            Quaternion rot = target.rotation * Quaternion.Euler(-_pitch, _yaw + startYaw, 0f);
            Vector3 offset = rot * Vector3.forward * _distance;

            transform.position = target.position + offset;
            transform.rotation = Quaternion.LookRotation(-offset.normalized, target.up);

        }

        private void InitializeFromOffset()
        {
            Vector3 offsetLocal = startOffset.sqrMagnitude > 0.0001f
                ? startOffset
                : new Vector3(0f, 5f, -8f);

            // Use target rotation to position offset relative to the car orientation
            Vector3 offsetWorld = target.rotation * offsetLocal;
            _distance = offsetLocal.magnitude;

            // Place camera immediately
            transform.position = target.position + offsetWorld;
            transform.rotation = Quaternion.LookRotation(-offsetWorld.normalized, target.up);

            Vector3 dir = offsetWorld.normalized;
            _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Asin(dir.y) * Mathf.Rad2Deg;

            _initialized = true;
        }
    }
}


