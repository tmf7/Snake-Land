using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Freehill.SnakeLand
{
    [RequireComponent(typeof(PositionConstraint))]
    public class PlayerMovement : VelocitySource
    {
        [SerializeField] private Camera _playerCamera;

        // CAMERA
        [SerializeField] private PositionConstraint _cameraPositionConstraint;
        private Vector3 _maxCameraPosition;
        private float _cameraZoom;

        // BODY MOVEMENT
        private Vector3 _targetFacing;
        private Snake _ownerSnake;

        private const float MOVE_THRESHOLD = 2500.0f; // square root is 50 pixels
        private const float CAMERA_ANGULAR_SPEED_DEGREES = 90.0f;
        private const float CAMERA_ROTATION_THRESHOLD = 0.1f;
        private const float CAMERA_MAX_ZOOM = 0.8f;

        public override Vector3 TargetFacing => _targetFacing;

        public override void Init(SnakesManager snakesManager, Snake ownerSnake)
        {
            _ownerSnake = ownerSnake;
            SetCameraConstraintSource(_ownerSnake.Head.transform);
        }

        /// <summary>
        /// Sets the PositionConstraint on this transform to only evaluate relative to the given <paramref name="constraintSource"/>
        /// </summary>
        private void SetCameraConstraintSource(Transform constraintSource)
        {
            _cameraPositionConstraint.AddSource(new ConstraintSource { sourceTransform = constraintSource, weight = 1.0f });
        }

        private void Awake()
        {
            EnhancedTouchSupport.Enable(); // must be manually enabled

            _maxCameraPosition = _playerCamera.transform.localPosition;
            _cameraZoom = 0.0f;
        }

        private void Update()
        {
            if (!EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Enable();
            }

            if (Touch.activeTouches.Count == 1)
            {
                Move(Touch.activeTouches[0]);
            }
            else if (Touch.activeTouches.Count == 2)
            {
                ZoomCamera(Touch.activeTouches[0], Touch.activeTouches[1]);
                RotateCamera(Touch.activeTouches[0], Touch.activeTouches[1]);
            }
        }

        private void RotateCamera(Touch firstTouch, Touch secondTouch)
        {
            Vector2 initialPairOffset = firstTouch.screenPosition - secondTouch.screenPosition;
            Vector2 finalPairOffset = (firstTouch.screenPosition - firstTouch.delta) - (secondTouch.screenPosition - secondTouch.delta);
            float angle = Vector2.SignedAngle(initialPairOffset, finalPairOffset);

            if (Mathf.Abs(angle) > CAMERA_ROTATION_THRESHOLD)
            {
                angle = Mathf.Sign(angle) * CAMERA_ANGULAR_SPEED_DEGREES * Time.deltaTime;
                transform.rotation *= Quaternion.AngleAxis(angle, transform.up);
            }
        }

        private void ZoomCamera(Touch firstTouch, Touch secondTouch)
        {
            float initialPinchOffset = ((firstTouch.screenPosition - firstTouch.delta) - (secondTouch.screenPosition - secondTouch.delta)).sqrMagnitude;
            float currentPinchOffset = (firstTouch.screenPosition - secondTouch.screenPosition).sqrMagnitude;

            // zoomDelta > -CAMERA_ZOOM_SPEED, without limit but generally [-CAMERA_ZOOM_SPEED, CAMERA_ZOOM_SPEED]
            float zoomDelta = ((currentPinchOffset / initialPinchOffset) - 1.0f);

            _cameraZoom = Mathf.Clamp(_cameraZoom + zoomDelta, 0.0f, CAMERA_MAX_ZOOM);
            _playerCamera.transform.localPosition = Vector3.Lerp(_maxCameraPosition, Vector3.zero, _cameraZoom);
        }

        private void Move(Touch touch)
        {
            Vector2 screenMoveDirection = touch.screenPosition - touch.startScreenPosition;

            // terrain is a height map on the XZ-plane so worldMoveDirection is moved to that plane
            Vector3 worldMoveDirection = new Vector3(screenMoveDirection.x, 0.0f, screenMoveDirection.y);

            // always move relative to the camera forward along the XZ-plane
            worldMoveDirection = Vector3.ProjectOnPlane(_playerCamera.transform.rotation * worldMoveDirection, TURNING_AXIS);
            float moveAmountSqr = worldMoveDirection.sqrMagnitude;

            if (moveAmountSqr >= MOVE_THRESHOLD)
            {
                _isStopped = false;
                _targetFacing = worldMoveDirection / Mathf.Sqrt(moveAmountSqr);
            }
        }
    }
}