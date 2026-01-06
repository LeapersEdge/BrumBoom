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
        [SerializeField] private float logInterval = 0.5f; // seconds between logs

        private float _yaw;
        private float _pitch;
        private float _distance;
        private bool _initialized;
        private float _logTimer;
        private bool _loggedNoTarget;
        private bool _loggedNoAuthority;

        public override void Spawned()
        {
            if (target == null)
            {
                var no = GetComponentInParent<NetworkObject>();
                if (no != null) target = no.transform;
            }

            var tgtName = target ? target.name : "null";
            var tgtPos = target ? target.position : Vector3.zero;
            var tgtRot = target ? target.rotation.eulerAngles : Vector3.zero;
            Debug.Log($"[NetCameraLook] Spawned target={tgtName} targetPos={tgtPos} targetRot={tgtRot} startOffset={startOffset} startYaw={startYaw}");
            _initialized = false;
        }

        private void Update()
        {
            if (Object != null && Object.HasInputAuthority == false)
            {
                if (!_loggedNoAuthority)
                {
                    _loggedNoAuthority = true;
                    Debug.Log("[NetCameraLook] Skipping Update - no input authority on this instance.");
                }
                return;
            }

            if (target == null)
            {
                if (!_loggedNoTarget)
                {
                    _loggedNoTarget = true;
                    Debug.Log("[NetCameraLook] Skipping Update - target is null.");
                }
                return;
            }

            if (!_initialized)
                InitializeFromOffset();

            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");

            _yaw += mx * sensitivity;
            // Mouse up (positive Y) now pitches camera down toward the car
            _pitch = Mathf.Clamp(_pitch - my * sensitivity, minPitch, maxPitch);

            // Orbit relative to target rotation
            // Use negative pitch so that a positive stored pitch keeps camera above the target
            Quaternion rot = target.rotation * Quaternion.Euler(-_pitch, _yaw + startYaw, 0f);
            Vector3 offset = rot * Vector3.forward * _distance;

            transform.position = target.position + offset;
            transform.rotation = Quaternion.LookRotation(-offset.normalized, target.up);

            _logTimer += Time.deltaTime;
            if (_logTimer >= logInterval)
            {
                _logTimer = 0f;
                Debug.Log($"[NetCameraLook] Update camPos={transform.position} camRotEuler={transform.rotation.eulerAngles} tgtPos={target.position} tgtRotEuler={target.rotation.eulerAngles} offset={offset} dist={_distance} yaw={_yaw} pitch={_pitch} mouse=({mx},{my}) startOffset={startOffset}");
            }
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
            Debug.Log($"[NetCameraLook] InitializeFromOffset camPos={transform.position} camRotEuler={transform.rotation.eulerAngles} tgtPos={target.position} tgtRotEuler={target.rotation.eulerAngles} offsetWorld={offsetWorld} offsetLocal={offsetLocal} dist={_distance} yaw={_yaw} pitch={_pitch}");
        }
    }
}


