using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// TODO: optimize above 30FPS:
// [] compute shader, or ECS, for movement instead of staggering update times (80% of frame time is this)
// [] butterfly wings quads (draw both faces)
// [] decimate ant model mesh
namespace Freehill.Boids
{
    public class Boid : MonoBehaviour
    {
        [Tooltip("After moving for more than a minimum time, " +
                 "boid will stop at first touched attractor for a few moments.")]
        public bool needsIntermittentRest = false;

        [Tooltip("Boid will only move along the ground terrain collider.")]
        public bool isGroundBoid = false;

        [Tooltip("How close this boid needs to be to a collider to be considered touching it.")]
        public float touchRange = 0.5f;

        private BoidSpawner _spawner;
        private List<Boid> _neighbors = new List<Boid>();
        private float _baseSpeed;
        private Vector3 _velocity;
        private RaycastHit _groundHitInfo; // used for forward and downward raycasts, as well as raycasts against terrain and other colliders
        private RaycastHit _groundBoidHitInfo; // only cached for downward raycasts
        private int _type;
        private Animator _animator;
        private bool _isTouchingGround = false;

        // resting logic
        private float _restTimeRemaining = 0.0f;
        private float _timeBeforeRestNeeded = 0.0f;
        private bool _isStoppedOnAttactor = false;
        private bool _isTouchingAttractor = false;
        private Vector3 _restingNormal;
        private Vector3 _restingPosition;

        // boundary logic
        private Vector3 _worldOffsetX;
        private Vector3 _worldOffsetY;
        private Vector3 _worldOffsetZ;

        private readonly int AnimationSpeedParameter = Animator.StringToHash("AnimationSpeed");

        private const float MAX_MOVE_SEC = 3.0f;
        private const float MAX_REST_SEC = 3.0f;
        private const float SMOOTH_TIME = 0.1f;

        private Animator Animator => _animator ??= GetComponentInChildren<Animator>();

        public void Initialize(BoidSpawner spawner, int type, float speed)
        {
            _spawner = spawner;
            _type = type;
            _baseSpeed = speed;
            _velocity = _baseSpeed * Random.onUnitSphere;
            _timeBeforeRestNeeded = MAX_MOVE_SEC;

            StartAnimator();
        }

        private void StartAnimator()
        { 
            Animator?.SetFloat(AnimationSpeedParameter, Random.Range(1.0f, 5.0f)); 
        }

        private void StopAnimator()
        {
            Animator?.SetFloat(AnimationSpeedParameter, 0.0f);
        }

        private void Update()
        {
            if (UpdateRestTime()) 
            {
                return;
            }

            UpdateNeighbors();
            UpdateVelocity();
            Move();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_groundHitInfo.point, 2.0f);

            Gizmos.color = (_triggered ? Color.red : Color.blue);
            Gizmos.DrawSphere(transform.position, 0.5f);
        }

        private void UpdateVelocity()
        {
            Vector3 alignment = GetAlignmentForce();
            Vector3 separation = GetSeparationForce();
            Vector3 cohesion = GetCohesionForce();
            Vector3 random = GetRandomForce();
            Vector3 attraction = GetAttractionForce();
            Vector3 repulsion = GetRepulsionForce();
            Vector3 worldPush = GetWorldForce();
            Vector3 acceleration = (alignment * _spawner.AlignmentWeight)
                                 + (separation * _spawner.SeparationWeight)
                                 + (cohesion * _spawner.CohesionWeight)
                                 + (random * _spawner.RandomWeight)
                                 + (attraction * _spawner.AttractionWeight)
                                 + (repulsion * _spawner.RepulsionWeight)
                                 + (worldPush * _spawner.BoundaryPushWeight)
                                 + (Physics.gravity * _spawner.GravityWeight);

            Vector3 targetVelocity = _velocity + acceleration;// * Time.deltaTime; // DEBUG: SmoothDamp takes care of acceleration
            targetVelocity = targetVelocity.normalized * _baseSpeed;//  Vector3.ClampMagnitude(targetVelocity, _baseSpeed); // allow slowdown, but not faster (may get zeroed)

            if (isGroundBoid)
            {
                if (_isTouchingGround)
                {
                    _velocity = Vector3.ProjectOnPlane(_velocity, _groundBoidHitInfo.normal).normalized * _baseSpeed;
                    targetVelocity = Vector3.ProjectOnPlane(targetVelocity, _groundBoidHitInfo.normal).normalized * _baseSpeed;
                }
                else
                {
                    _velocity = Physics.gravity;
                    targetVelocity = Physics.gravity; // no real acceleration past this value
                }
            }

            _velocity = Vector3.SmoothDamp(_velocity, targetVelocity, ref _velocity, SMOOTH_TIME);
        }

        private bool UpdateRestTime()
        {
            if (!needsIntermittentRest)
            {
                return false;
            }

            if (_timeBeforeRestNeeded <= 0.0f && !_isStoppedOnAttactor && _isTouchingAttractor)
            {
                _isStoppedOnAttactor = true;
                _velocity = Vector3.zero;
                _restTimeRemaining = MAX_REST_SEC;
                transform.position = _restingPosition;
                transform.rotation = Quaternion.LookRotation(new Vector3(_restingNormal.y, _restingNormal.x), _restingNormal);

                StopAnimator();
                return true;
            }
            else if (_isStoppedOnAttactor && _restTimeRemaining > 0.0f)
            {
                _restTimeRemaining -= Time.deltaTime;
                return true;
            }
            else if (_isStoppedOnAttactor)
            {
                _isStoppedOnAttactor = false;
                _timeBeforeRestNeeded = MAX_MOVE_SEC;

                StartAnimator();
            }
            else
            { 
                _timeBeforeRestNeeded -= Time.deltaTime;
            }

            return false;
        }

        private List<Boid> UpdateNeighbors()
        {
            _neighbors.Clear();
            float sqrNeighborhoodRadius = _spawner.BoidNeighborhoodRadius * _spawner.BoidNeighborhoodRadius;

            foreach (Boid boid in _spawner.SpawnedBoids)
            { 
                if (boid == this || boid._type != _type)
                { 
                    continue; 
                }

                Vector3 toNeighbor = boid.transform.position - transform.position;
                if (toNeighbor.sqrMagnitude <= sqrNeighborhoodRadius)
                { 
                    _neighbors.Add(boid);
                }
            }

            return _neighbors;
        }

        private Vector3 GetAlignmentForce()
        {
            Vector3 result = Vector3.zero;

            foreach (Boid neighbor in _neighbors) 
            {
                result += neighbor._velocity;
            }

            if (_neighbors.Count > 0)
            {
                result /= _neighbors.Count;
            }

            return result.normalized;
        }

        private Vector3 GetSeparationForce()
        {
            Vector3 result = Vector3.zero;

            foreach (Boid neighbor in _neighbors)
            {
                Vector3 neighborPush = transform.position - neighbor.transform.position;
                result += (neighborPush / neighborPush.sqrMagnitude); // force of push scales by square of the distance between boids (closer is stronger)
            }

            return result.normalized;
        }

        private Vector3 GetCohesionForce()
        {
            Vector3 result = Vector3.zero;

            foreach (Boid neighbor in _neighbors)
            {
                result += neighbor.transform.position;
            }

            if (_neighbors.Count > 0)
            {
                result /= _neighbors.Count;
                result -= transform.position;
            }

            return result.normalized;
        }

        private Vector3 GetRandomForce()
        {
            return Random.onUnitSphere;
        }

        private void Move()
        {
            Vector3 movement = _velocity * Time.deltaTime;
            //movement = _spawner.WorldBounds.ConstrainMovement(transform.position, movement);

            Vector3 newPosition = transform.position + movement;
            newPosition = _spawner.WorldBounds.GetAboveGroundPosition(newPosition, touchRange * 0.8f);

            transform.position = newPosition;

            //_velocity = movement.normalized * _baseSpeed;
            transform.rotation = Quaternion.LookRotation(_velocity, isGroundBoid ? _groundBoidHitInfo.normal : Vector3.up);
        }

        private Vector3 GetWorldForce()
        {
            _isTouchingAttractor = false; // one of the GetGroundPushFrom() calls will update this value

            // DEBUG: transform.forward always points in the direction of _velocity
            // also forward and down are sufficient for the simple boid models
            // DEBUG: Vector3.down is tested last for the sake of ground boids' _isTouchingGround to update last
            Vector3 groundPushVector = GetGroundPushFrom(transform.forward) + GetGroundPushFrom(Vector3.down);

            _spawner.WorldBounds.GetBoundaryOffsets(transform.position, ref _worldOffsetX, ref _worldOffsetY, ref _worldOffsetZ);

            Vector3 boundsPushVector = GetWorldPushFrom(_worldOffsetX) + GetWorldPushFrom(_worldOffsetZ);

            // push off the sky
            if (!isGroundBoid) 
            {
                boundsPushVector += GetWorldPushFrom(_worldOffsetY);
            }

            return (groundPushVector + boundsPushVector).normalized;
        }

        /// <summary> Returns the scaled pushing force in the direction of <paramref name="boundsAxisOffset"/> as this boid approaches the bounds </summary>
        /// <param name="boundsAxisOffset"> The vector from the nearest boundary plane along one axis to this boid </param>
        private Vector3 GetWorldPushFrom(Vector3 boundsAxisOffset)
        {
            float offsetMagnitude = boundsAxisOffset.magnitude;

            // FIXME: if boid is out of bounds the ignore the radius limit,
            // and make the strength directly proportional to the distance away
            // SOLUTION: or just snap them in bounds, or tune forces to ensure movement usually doesn't escape bounds
            // SOLUTION: apply a direct center-seeking force with dot-product related strength (weaker when below, stronger when above, but always non-zero)
            if (offsetMagnitude <= _spawner.BoundsProximityRadius)
            {
                // DEBUG: intenially not normalized
                return boundsAxisOffset / (offsetMagnitude * offsetMagnitude);
            }

            return Vector3.zero;
        }

        private bool _triggered = false;
        private void FixedUpdate()
        {
            _triggered = false;
        }

        // these are the neighbors, etc
        private void OnTriggerStay(Collider other)
        {
            _triggered = true;
        }

        /// <summary>
        /// Determines how hard to push away from the ground, 
        /// if the boid is touching the ground,
        /// and if the boid is touching an attractor (using a single raycast)
        /// </summary>
        private Vector3 GetGroundPushFrom(Vector3 direction)
        {
            _isTouchingGround = false;
            
            // DEBUG: use a single raycast to test the WorldBounds' TerrainCollider, as well as any Attractor' colliders
            if (Physics.Raycast(new Ray(transform.position, direction), out _groundHitInfo, _spawner.GroundProximityRadius))
            {
                Vector3 toHit = _groundHitInfo.point - transform.position;
                bool canTouch = toHit.sqrMagnitude <= touchRange * touchRange;

                if (isGroundBoid && direction == Vector3.down)
                {
                    _groundBoidHitInfo = _groundHitInfo;
                    _isTouchingGround = canTouch;
                }

                // DEBUG: if the first hit is an attractor, then no ground push is necessary
                if (_spawner.Attractors.Contains(_groundHitInfo.transform))
                {
                    if (canTouch)
                    {
                        _isTouchingAttractor = true;
                        _restingPosition = _groundHitInfo.point;
                        _restingNormal = _groundHitInfo.normal;
                    }
                    return Vector3.zero;
                }

                if (!isGroundBoid) // outer if-statement implies toHit.magnitude <= _spawner.GroundProximityRadius
                {
                    // how directly at the surface is the boid moving
                    float directionWeight = Vector3.Dot(transform.forward, _groundHitInfo.normal);

                    if (directionWeight < 0.0f) // only push away if moving toward the ground
                    {
                        // how close the boid is to the ground
                        float proximityWeight = Mathf.Clamp01(1.0f - (toHit.magnitude / _spawner.GroundProximityRadius));

                        // allow the boid to get closer to the ground
                        // DEBUG: intenially not normalized
                        return _groundHitInfo.normal * (proximityWeight * -directionWeight); // [0,1] based on approach
                    }
                }
            }

            return Vector3.zero;
        }

        private Vector3 GetAttractionForce()
        { 
            Vector3 result = Vector3.zero;

            foreach (Transform attractor in _spawner.Attractors) 
            {
                Vector3 attractorPull = attractor.position - transform.position;

                // force of attraction decreases by square of distance to target, but only within a fixed range
                if (attractorPull.sqrMagnitude <= _spawner.AttractorProximityRadius * _spawner.AttractorProximityRadius)
                {
                    result += (attractorPull / attractorPull.sqrMagnitude);
                }
            }

            if (_spawner.Attractors.Count() > 1)
            {
                result /= _spawner.Attractors.Count();
            }

            return result.normalized;
        }

        private Vector3 GetRepulsionForce()
        {
            Vector3 result = Vector3.zero;

            foreach (Transform repulsor in _spawner.Repulsors)
            {
                // TODO: use a collider or body radius nearest-point, and a different "repulsorRange"/"attractorRange"
                Vector3 repulsorPush = transform.position - repulsor.position; 

                // force of repulsion decreases by square of distance to target, but only within a fixed range
                if (repulsorPush.sqrMagnitude <= _spawner.RepulsorProximityRadius * _spawner.RepulsorProximityRadius)
                { 
                    result += (repulsorPush / repulsorPush.sqrMagnitude);
                }
            }

            if (_spawner.Repulsors.Count() > 1) 
            { 
                result /= _spawner.Repulsors.Count();
            }

            return result.normalized;
        }
    }
}