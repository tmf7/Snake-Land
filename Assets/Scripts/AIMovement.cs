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
        private Pickup _pickupTarget = null;

        private List<Pickup> _nearbyPickups = new List<Pickup>();
        private List<SnakePart> _nearbySnakeParts = new List<SnakePart>();
        private List<SnakeHead> _nearbySnakeHeads = new List<SnakeHead>();

        // boundary logic
        private Vector3 _worldOffsetX;
        private Vector3 _worldOffsetY;
        private Vector3 _worldOffsetZ;
        private Collider[] _hitColliders = new Collider[30];

        public override Vector3 TargetFacing => _trackingVelocity.normalized;

        private Vector3 SelfHeadPosition => _ownerSnake.HeadPosition;

        public override void Init(SnakesManager snakesManager, Snake ownerSnake)
        {
            _snakesManager = snakesManager;
            _ownerSnake = ownerSnake;
            _trackingVelocity = GroundSpeed * Random.onUnitSphere;
            _isStopped = false;
        }

        // TODO: perform UpdateNeighborhood on a subset of snakes every X frames to amortize costs
        private void UpdateNeighborhood() 
        {
            _nearbyPickups.Clear();
            _nearbySnakeParts.Clear();
            _nearbySnakeHeads.Clear();

            int hitCount = Physics.OverlapSphereNonAlloc(SelfHeadPosition, _snakesManager.NeighborhoodRadius, _hitColliders);

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
                    else if (!_ownerSnake.IsPartOfSelf(hitSnakePart)) // never consider own head in any logic
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
                Gizmos.DrawSphere(SelfHeadPosition, _snakesManager.NeighborhoodRadius);
            }
        }

        // TODO: change to FixedUpdate so it generally happens less often
        private void Update()
        {
            UpdateNeighborhood();

            // NOTE: make Seek stronger than Wander so stuff is actually picked up
            // DEBUG: ensure all pickups can be picked up (ie: not too high off ground), or give a timeout and exclusion
            Vector3 worldPush = GetWorldForce();
            Vector3 wander = GetWanderForce();
            Vector3 seek = GetSeekPickupForce();
            Vector3 evade = GetSnakePartEvadeForce();

            Vector3 acceleration = (worldPush * _snakesManager.BoundaryPushWeight)
                                 + (wander * _snakesManager.WanderWeight)
                                 + (seek * _snakesManager.PickupSeekWeight)
                                 + (evade * _snakesManager.SnakePartEvadeWeight)
                                 + GetKillShotForce();

            _trackingVelocity += acceleration * Time.deltaTime;
        }

        // TODO: 
        // [] Evade snakeHeads (if running behind) ...exactly like snakeParts, but with different T logic
        // [] Pursue snakeHeads (if running ahead) (a point beyond and in front of the head) 
        // [x] flee snakeParts always (self or other)
        // [x] seek Pickups
        // [x] Wander always

        // FIXME: snake clash or snake rivals or OG snake logic
        // Clash => full on cut and eat/cut eachother with tie-breakers for head-to head collisions for kills, can intersect own body
        // Rivals => head hits body is complete/instant kill, can intersect own body
        // OG: head his anything, even own body is complete/instant kill
        // NEW: hit own body CUT SELF, hit OTHER body or obstacle DIE SELF ==> aim to cut in front of snake heads

        // combines head cut-off pursuit and head evasion (if can't cut-off)
        private Vector3 GetKillShotForce()
        {
            Vector3 pursueHeadAcceleration = Vector3.zero;
            Vector3 evadeHeadAcceleration = Vector3.zero;

            // DEBUG: _nearbySnakeHeads should never include own head
            for (int i = 0; i < _nearbySnakeHeads.Count; ++i)
            {
                SnakeHead otherSnakeHead = _nearbySnakeHeads[i];
                Vector3 currentHeadsOffset = otherSnakeHead.transform.position - SelfHeadPosition;
                float relativeHeading = Vector3.Dot(CurrentFacing, otherSnakeHead.Owner.CurrentFacing);// towards > 0, away < 0, parallel == 0
                float relativePosition = Vector3.Dot(CurrentFacing, currentHeadsOffset); // ahead < 0, behind > 0, beside == 0

                // TODO: use currentHeadsOffset and relative values to determine
                // whether to go for kill shot (seek tangent position) or evade (flee predicted position)
            }

            return (pursueHeadAcceleration * _snakesManager.SnakeHeadPursueWeight) 
                 + (evadeHeadAcceleration * _snakesManager.SnakeHeadEvadeWeight);
        }

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
                    float pickupRange = (_nearbyPickups[i].transform.position - SelfHeadPosition).sqrMagnitude;
                    if (pickupRange < nearestPickupRange)
                    {
                        nearestPickupRange = pickupRange;
                        _pickupTarget = _nearbyPickups[i];
                    }
                }
            }

            if (_pickupTarget != null)
            {
                Vector3 seekFacing = (_pickupTarget.transform.position - SelfHeadPosition).normalized;
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

        // directly flee predicted positions of all nearby parts (and most of self), regardless of proximity or facing
        private Vector3 GetSnakePartEvadeForce()
        {
            Vector3 evadeFacing = Vector3.zero;

            // DEBUG: _nearbySnakeParts intentionally never includes a SnakeHead (despite it inheriting from SnakePart)
            // in order to (1) treat SnakeHeads as unique, and (2) avoid SnakePart evasion edge cases

            // Leverages the fact that SnakeParts move along a static path drawn by their SnakeHead, therefore:
            // (1) any individual SnakePart must move to the position and rotation of the SnakePart ahead of it
            // (2) if the SnakePart behind a given SnakePart is in _nearbySnakeParts, then this logic will implictly
            // evade the current SnakePart as if it never moved.
            for (int i = 0; i < _nearbySnakeParts.Count; ++i)
            {
                SnakePart snakePart = _nearbySnakeParts[i];

                // DEBUG: evade own body, except allow self to overlap MIN_SNAKE_LENGTH of self
                if (!_ownerSnake.IsPartOfSelf(snakePart) 
                    || _ownerSnake.SnakeMovement.IsPartBehind(snakePart, SnakeMovement.MIN_SNAKE_LENGTH))
                {
                    Snake evadeSnake = snakePart.Owner;
                    SnakePart snakePartAhead = evadeSnake.SnakeMovement.GetPartAheadOf(snakePart, 1);

                    
                    evadeFacing += (SelfHeadPosition - snakePartAhead.transform.position).normalized;
                }
            }

            if (evadeFacing == Vector3.zero) 
            { 
                return Vector3.zero;
            }

            return (evadeFacing - CurrentFacing) * GroundSpeed;
        }

        private Vector3 GetWorldForce()
        {
            SpawnPointManager.WorldBounds.GetBoundaryOffsets(SelfHeadPosition, ref _worldOffsetX, ref _worldOffsetY, ref _worldOffsetZ);

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