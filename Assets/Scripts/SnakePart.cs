using UnityEngine;

namespace Freehill.SnakeLand
{
    public class SnakePart : MonoBehaviour 
    {
        protected Snake _owner;
        public Snake Owner => _owner ??= GetComponentInParent<Snake>();
    }
}
