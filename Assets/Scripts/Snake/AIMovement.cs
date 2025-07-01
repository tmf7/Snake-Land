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

        private Vector3 HeadPosition => _ownerSnake.Head.transform.position;

        public override void Init(SnakesManager snakesManager, Snake ownerSnake)
        {
            _snakesManager = snakesManager;
            _ownerSnake = ownerSnake;
            _trackingVelocity = GroundSpeed * Random.onUnitSphere;
            _isStopped = false;
        }

        // TODO: perform UpdateNeighborhood on a subset of snakes every X frames to amortize costs
        // TODO: display FPS on screen

        private void UpdateNeighborhood() 
        {
            // TODO: move this const to SnakeManager
            const float neighborhoodDistance_TEST = 10.0f;
            var hitColliders = Physics.OverlapSphere(HeadPosition, neighborhoodDistance_TEST); // FIXME: use NonAlloc
            //Debug.Log($"OverlapShere [{name}]: [{results.Length}]");

            /*
            _nearbyPickups.Clear();
            _nearbySnakeParts.Clear();
            _nearbySnakeHeads.Clear();

            foreach (Collider hitCollider in hitColliders) 
            {
                var hitPickup = hitCollider.GetComponent<Pickup>();
                var hitSnakePart = hitCollider.GetComponent<SnakePart>();
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
            */
        }

        private void Update()
        {
            UpdateNeighborhood();

            Vector3 seek = GetSeekPickupForce();
            Vector3 wander = GetWanderForce();
            Vector3 evade = GetSnakePartEvadeForce();
            Vector3 pursue = GetSnakeHeadPursueForce();
            Vector3 worldPush = GetWorldForce();

            Vector3 acceleration = (worldPush * _snakesManager.BoundaryPushWeight) 
                                 + (wander * _snakesManager.WanderWeight);
                //(seek * _snakesManager.PickupSeekWeight)
                //                 + (wander * _snakesManager.WanderWeight)
                //                 + (evade * _snakesManager.SnakePartEvadeWeight)
                //                 + (pursue * _snakesManager.SnakeHeadPursueWeight)
                //                 // + (pursue * _snakesManager.SnakeHeadEvadeWeight)
                //                 + (worldPush * _snakesManager.BoundaryPushWeight);

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

        private Vector3 GetSeekPickupForce()
        {
            Vector3 seekAcceleration = Vector3.zero;
            Pickup pickupTarget = null;
            float nearestPickupRange = float.MaxValue;

            for (int i = 0; i < _nearbyPickups.Count; ++i)
            {
                float pickupRange = (_nearbyPickups[i].transform.position - HeadPosition).sqrMagnitude;
                if (pickupRange < nearestPickupRange)
                {
                    nearestPickupRange = pickupRange;
                    pickupTarget = _nearbyPickups[i];
                }
            }

            // seek the nearest pickup this frame, but don't commit to it
            if (pickupTarget != null)
            {
                Vector3 targetOffset = pickupTarget.transform.position - HeadPosition;
                seekAcceleration = targetOffset / targetOffset.sqrMagnitude;
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
            // TODO: position the trigger such that its in front of the head, not centered on the head (maybe a capsule)
            // TODO: project each neighbor part along its travel, then flee that point
            // ...ignore the MIN_SNAKE_LENGTH elements of self
            Vector3 evadeAcceleration = Vector3.zero;

            for (int i = 0; i < _nearbySnakeParts.Count; ++i) 
            {
                var snakePart = _nearbySnakeParts[i];
                if (!_ownerSnake.SnakeMovement.IsSelf(snakePart.transform) 
                    || _ownerSnake.SnakeMovement.IsPartBehind(snakePart.transform, SnakeMovement.MIN_SNAKE_LENGTH))
                {
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