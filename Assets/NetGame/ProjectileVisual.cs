using UnityEngine;

namespace NetGame
{
    /// <summary>
    /// Lightweight visual projectile used on clients.
    /// </summary>
    public class ProjectileVisual : MonoBehaviour
    {
        private Vector3 _direction;
        private float _speed;
        private float _timeLeft;

        [SerializeField] private float radius = 0.1f;
        [SerializeField] private LayerMask hitMask = ~0; // make hitMask enabled on all layers

        public void Initialize(Vector3 direction, float speed, float lifetime)
        {
            _direction = direction.normalized;
            _speed = speed;
            _timeLeft = lifetime;
        }

        private void Update()
        {
            Vector3 start = transform.position;
            float deltaDistance = _speed * Time.deltaTime;

            // Check for hit before moving (simulate server SphereCast)
            if (Physics.SphereCast(start, radius, _direction, out RaycastHit hit, deltaDistance, hitMask, QueryTriggerInteraction.Ignore))
            {
                // Move to hit point and destroy
                transform.position = hit.point;
                Destroy(gameObject);
                return;
            }

            transform.position += _direction * _speed * Time.deltaTime;
            _timeLeft -= Time.deltaTime;

            if (_timeLeft <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
