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

        public void Initialize(Vector3 direction, float speed, float lifetime)
        {
            _direction = direction.normalized;
            _speed = speed;
            _timeLeft = lifetime;
        }

        private void Update()
        {
            transform.position += _direction * _speed * Time.deltaTime;
            _timeLeft -= Time.deltaTime;

            if (_timeLeft <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
