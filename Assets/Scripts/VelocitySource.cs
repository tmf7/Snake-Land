using UnityEngine;

namespace Freehill.SnakeLand
{
    public abstract class VelocitySource : MonoBehaviour
    {
        [SerializeField][Min(0.01f)] private float _baseSpeed = 5.0f;
        [SerializeField][Min(0.01f)] private float _sprintSpeed = 7.0f;

        private Vector3 _currentFacing = Vector3.right;
        protected bool _isSprinting = false;
        protected bool _isStopped = true;

        // all movement is on the XZ plane
        protected static Vector3 TURNING_AXIS = Vector3.up;

        public Vector3 CurrentFacing => _currentFacing;
        public abstract Vector3 TargetFacing { get; }
        public float GroundSpeed => _isSprinting ? _sprintSpeed : _baseSpeed;
        public bool IsSprinting => _isSprinting;
        public bool IsStopped => _isStopped;

        public abstract void Init(SnakesManager snakesManager, Snake ownerSnake);

        /// <summary>
        /// Rotates CurrentFacing towards TargetFacing by an angular velocty
        /// defined by the Snake's turning radius and its current speed.
        /// </summary>
        public void RotateToFaceTargetHeading(float turningRadius)
        {
            _currentFacing = Vector3.RotateTowards(_currentFacing, TargetFacing, (GroundSpeed / turningRadius) * Time.deltaTime, 0.0f);
        }
    }
}
