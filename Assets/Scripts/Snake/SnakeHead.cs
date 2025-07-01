using UnityEngine;

namespace Freehill.SnakeLand
{
    public class SnakeHead : SnakePart
    {
        private Snake _owner;
        private Snake Owner => _owner ??= GetComponentInParent<Snake>();

        // FIXME: maybe change to capsule collider and use a short axis radius
        private SphereCollider _sphereCollider;

        /// <summary>
        /// Returns the worldscale radius of the head's SphereCollider (assumes uniform scale)
        /// </summary>
        public float WorldRadius => _sphereCollider.radius * transform.lossyScale.x;

        private void Awake()
        {
            _sphereCollider = GetComponent<SphereCollider>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.activeSelf)
            {
                return;
            }

            var hitSnake = other.transform.parent?.GetComponent<Snake>();
            var hitPickup = other.GetComponent<Pickup>();

            if (hitSnake != null)
            {
                Owner.HitSnake(hitSnake, other.transform);
            }
            else if (hitPickup != null)
            {
                Owner.HitPickup(hitPickup);
            }
        }
    }
}
