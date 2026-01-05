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
        [SerializeField] private float minPitch = -60f;
        [SerializeField] private float maxPitch = 60f;

        private float _yaw;
        private float _pitch;
        private float _distance;

        public override void Spawned()
        {
            if (target == null)
            {
                var no = GetComponentInParent<NetworkObject>();
                if (no != null) target = no.transform;
            }

            if (target != null)
            {
                Vector3 offset = transform.position - target.position;
                _distance = offset.magnitude;

                Vector3 dir = offset.normalized;
                _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                _pitch = Mathf.Asin(dir.y) * Mathf.Rad2Deg;
            }
            else
            {
                _distance = 5f;
            }
        }

        private void Update()
        {
            if (Object != null && Object.HasInputAuthority == false)
                return;

            if (target == null)
                return;

            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");

            _yaw += mx * sensitivity;
            _pitch = Mathf.Clamp(_pitch - my * sensitivity, minPitch, maxPitch);

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 offset = rot * Vector3.forward * _distance;

            transform.position = target.position + offset;
            transform.rotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);
        }
    }
}


