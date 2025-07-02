using System.Collections.Generic;
using UnityEngine;

namespace Freehill.SnakeLand
{
    public class AIMovement : VelocitySource
    {
        private Snake _ownerSnake;
        private SnakesManager _snakesManager;
        private Vector3 _trackingVelocity; // used for relative snake motion calculations
        private Vector3 _wanderDirection = Vector3.right;

        private List<Pickup> _nearbyPickups = new List<Pickup>();
        private List<SnakePart> _nearbySnakeParts = new List<SnakePart>();
        private List<SnakeHead> _nearbySnakeHeads = new List<SnakeHead>();

        // boundary logic
        private Vector3 _worldOffsetX;
        private Vector3 _worldOffsetY;
        private Vector3 _worldOffsetZ;

        public override Vector3 TargetFacing => _trackingVelocity.normalized;

        private Vector3 HeadPosition => _ownerSnake.HeadPosition;

        public override void Init(SnakesManager snakesManager, Snake ownerSnake)
        {
            _snakesManager = snakesManager;
            _ownerSnake = ownerSnake;
            _trackingVelocity = GroundSpeed * Random.onUnitSphere;
            _isStopped = false;
        }

        // TODO: perform UpdateNeighborhood on a subset of snakes every X frames to amortize costs
        private Collider[] _hitColliders = new Collider[30];
        private void UpdateNeighborhood() 
        {
            _nearbyPickups.Clear();
            _nearbySnakeParts.Clear();
            _nearbySnakeHeads.Clear();

            int hitCount = Physics.OverlapSphereNonAlloc(HeadPosition, _snakesManager.NeighborhoodRadius, _hitColliders);

            for (int i = 0; i < hitCount; ++i) 
            {
                // DEBUG: assumes Pickup and SnakePart are on the same object as their Collider
                var hitPickup = _hitColliders[i].GetComponent<Pickup>();
                var hitSnakePart = _hitColliders[i].GetComponent<SnakePart>();
                if (hitPickup != null)
                {
                    _nearbyPickups.Add(hitPickup);
                }
                else if (hitSnakePart != null)
                {
                    if (hitSnakePart is not SnakeHead)
                    {
                        _nearbySnakeParts.Add(hitSnakePart);
                    }
                    else
                    {
                        _nearbySnakeHeads.Add(hitSnakePart as SnakeHead);
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (_ownerSnake != null)
            {
                Gizmos.color = new Color(0.5f, 1.0f, 1.0f, 0.5f);
                Gizmos.DrawSphere(HeadPosition, _snakesManager.NeighborhoodRadius);
            }
        }

        // TODO: change to FixedUpdate so it generally happens less often
        private void Update()
        {
            UpdateNeighborhood();

            // NOTE: make Seek stronger than Wander so stuff is actually picked up
            // NOTE: ensure all pickups can be picked up (ie: not too high off ground)...or give a timeout and exclusion
            Vector3 seek = GetSeekPickupForce();
            Vector3 wander = GetWanderForce();
            Vector3 evade = GetSnakePartEvadeForce();
            Vector3 pursue = GetSnakeHeadPursueForce();
            Vector3 worldPush = GetWorldForce();

            Vector3 acceleration = (worldPush * _snakesManager.BoundaryPushWeight)
                                 + (wander * _snakesManager.WanderWeight)
                                 + (seek * _snakesManager.PickupSeekWeight);
                //                 + (evade * _snakesManager.SnakePartEvadeWeight)
                //                 + (pursue * _snakesManager.SnakeHeadPursueWeight)

            _trackingVelocity += acceleration * Time.deltaTime;
            //_velocity = _velocity.normalized * Speed;
        }

        // TODO: 
        // [] Evade snakeHeads (if running behind)
        // [] Pursue snakeHeads (if running ahead) (a point beyond and in front of the head) 
        // [x] flee snakeParts always (self or other)
        // [x] seek Pickups
        // [x] Wander always

        // FIXME: change this to decide which heads to evade, and which to pursue (if only one)
        // ...currently this only seeks heads like pickups
        private Vector3 GetSnakeHeadPursueForce()
        {
            Vector3 pursueAcceleration = Vector3.zero;
            SnakeHead snakeHeadTarget = null;
            float nearestHeadRange = float.MaxValue;

            // DEBUG: _nearbySnakeHeads should never include own head
            for (int i = 0; i < _nearbySnakeHeads.Count; ++i)
            {
                float headRange = (_nearbySnakeHeads[i].transform.position - HeadPosition).sqrMagnitude;
                if (headRange < nearestHeadRange)
                {
                    nearestHeadRange = headRange;
                    snakeHeadTarget = _nearbySnakeHeads[i];
                }
            }

            // pursue the nearest head this frame, but don't commit to it
            if (snakeHeadTarget != null)
            {
                Vector3 targetOffset = snakeHeadTarget.transform.position - HeadPosition;
                pursueAcceleration = targetOffset / targetOffset.sqrMagnitude;
            }

            return pursueAcceleration;
        }


        private Pickup _pickupTarget = null;
        // seek toward one in-range pickup at a time
        private Vector3 GetSeekPickupForce()
        {
            Vector3 seekAcceleration = Vector3.zero;
            
            if (_pickupTarget == null || _pickupTarget.NeedsRespawn || !_nearbyPickups.Contains(_pickupTarget))
            {
                float nearestPickupRange = float.MaxValue;
                _pickupTarget = null;

                for (int i = 0; i < _nearbyPickups.Count; ++i)
                {
                    float pickupRange = (_nearbyPickups[i].transform.position - HeadPosition).sqrMagnitude;
                    if (pickupRange < nearestPickupRange)
                    {
                        nearestPickupRange = pickupRange;
                        _pickupTarget = _nearbyPickups[i];
                    }
                }
            }

            if (_pickupTarget != null)
            {
                Vector3 seekFacing = (_pickupTarget.transform.position - HeadPosition).normalized;
                seekAcceleration = (seekFacing - CurrentFacing) * GroundSpeed;
            }

            return seekAcceleration;
        }

        // wander on the XZ plane by nudging the wander direction each frame
        private Vector3 GetWanderForce()
        {
            var randomRotation = Quaternion.Euler(0.0f, Random.Range(-_snakesManager.WanderErraticness, _snakesManager.WanderErraticness), 0.0f);
            _wanderDirection = randomRotation * _wanderDirection;
            return (_wanderDirection * GroundSpeed - _trackingVelocity);
        }

        private Vector3 GetSnakePartEvadeForce()
        {
            Vector3 evadeAcceleration = Vector3.zero;

            foreach (SnakePart snakePart in _nearbySnakeParts) 
            {
                // TODO: also ignore a part if not moving towards it...or it is not moving towards self

                // DEBUG: allow self to overlap MIN_SNAKE_LENGTH of self
                if (!_ownerSnake.SnakeMovement.IsSelf(snakePart.transform) 
                    || _ownerSnake.SnakeMovement.IsPartBehind(snakePart.transform, SnakeMovement.MIN_SNAKE_LENGTH))
                {
                    // SnakeMovement enforces that all parts (besides the head) follow the path of the part in front of it,
                    // therefore this logic approximates that the current part will be somewhere between
                    // its current position and the position of the part ahead of it in the next frame.
                    Transform partAhead = _ownerSnake.SnakeMovement.GetPartAheadOf(snakePart.transform, 1);

                    // TODO: interpolate (or just use) current to partAhead forward axis



                    Vector3 currentPartOffset = HeadPosition - snakePart.transform.position;

                    // TODO: calculate a scaled collision time based on distance/path offet and headings, and speeds
                    // and evade the future point instead of the current point
                    //float alignmentWeight = Vector3.Dot(snakePart.transform.forward, transform.forward);
                    //float sqrPartOffset = currentPartOffset.sqrMagnitude;
                    //float collisionTime = 0.0f;

                    //Vector3 predictedPartPosition = snakePart.transform.position + snakePart.transform.forward * _baseSpeed * collisionTime;
                    //Vector3 predictedPartOffset = HeadPosition - predictedPartPosition;

                    // FIXME: maybe just divide by sqrMagnitude instead of normalize
                    Vector3 desiredVelocity = currentPartOffset.normalized * GroundSpeed;
                    evadeAcceleration += desiredVelocity - _trackingVelocity;
                }
            }

            // TODO: average or normalize?
            return evadeAcceleration;
        }

        private Vector3 GetWorldForce()
        {
            SpawnPointManager.WorldBounds.GetBoundaryOffsets(HeadPosition, ref _worldOffsetX, ref _worldOffsetY, ref _worldOffsetZ);

            Vector3 boundsPushVector = GetWorldPushFrom(_worldOffsetX) + GetWorldPushFrom(_worldOffsetZ);

            return boundsPushVector.normalized;
        }

        /// <summary> Returns the scaled pushing force in the direction of <paramref name="boundsAxisOffset"/> as this boid approaches the bounds </summary>
        /// <param name="boundsAxisOffset"> The vector from the nearest boundary plane along one axis to this boid </param>
        private Vector3 GetWorldPushFrom(Vector3 boundsAxisOffset)
        {
            float sqrOffsetMagnitude = boundsAxisOffset.sqrMagnitude;

            // FIXME: if boid is out of bounds ignore the radius limit,
            // and make the strength directly proportional to the distance away
            // SOLUTION: or just snap them in bounds, or tune forces to ensure movement usually doesn't escape bounds
            // SOLUTION: apply a direct center-seeking force with dot-product related strength (weaker when below, stronger when above, but always non-zero)
            if (sqrOffsetMagnitude <= _snakesManager.BoundsProximityRadius * _snakesManager.BoundsProximityRadius)
            {
                return boundsAxisOffset / (sqrOffsetMagnitude);
            }

            return Vector3.zero;
        }
    }
}