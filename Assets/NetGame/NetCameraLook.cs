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
        [SerializeField] private float minPitch = 10f;
        [SerializeField] private float maxPitch = 60f;
        [SerializeField] private float startYaw = 0f;
        [SerializeField] private Vector3 startOffset = new Vector3(0f, 5f, -8f); // higher & behind
        private float _yaw;
        private float _pitch;
        private float _distance;
        private bool _initialized;
        private Transform _defaultTarget;
        private bool _isSpectating;

        public override void Spawned()
        {
            if (target == null)
            {
                var no = GetComponentInParent<NetworkObject>();
                if (no != null) target = no.transform;
            }

            _initialized = false;
            _defaultTarget = target;
        }

        private void Update()
        {
            if (Object != null && Object.HasInputAuthority == false)
                return;

            if (target == null)
                return;

            UpdateSpectatorState();

            if (_isSpectating)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                    CycleTarget(-1);
                else if (Input.GetKeyDown(KeyCode.E))
                    CycleTarget(1);
            }

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

        public void SetTarget(Transform newTarget)
        {
            target = newTarget != null ? newTarget : _defaultTarget;
            _initialized = false;
        }

        private void UpdateSpectatorState()
        {
            var localHealth = GetLocalHealth();
            _isSpectating = localHealth != null && localHealth.IsEliminated;
        }

        private NetworkHealth GetLocalHealth()
        {
            foreach (var nh in FindObjectsOfType<NetworkHealth>())
            {
                var no = nh.GetComponent<NetworkObject>();
                if (no != null && no.HasInputAuthority)
                    return nh;
            }
            return null;
        }

        private void CycleTarget(int direction)
        {
            var all = FindObjectsOfType<NetworkHealth>();
            if (all == null || all.Length == 0)
                return;

            var alive = new System.Collections.Generic.List<NetworkHealth>();
            foreach (var nh in all)
            {
                if (nh != null && !nh.IsEliminated)
                    alive.Add(nh);
            }

            if (alive.Count == 0)
                return;

            int currentIndex = 0;
            for (int i = 0; i < alive.Count; i++)
            {
                if (alive[i] != null && alive[i].transform == target)
                {
                    currentIndex = i;
                    break;
                }
            }

            int next = (currentIndex + direction) % alive.Count;
            if (next < 0) next += alive.Count;

            SetTarget(alive[next].transform);
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


