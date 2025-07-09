using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Freehill.SnakeLand
{
    public class SnakeMovement : MonoBehaviour
    {
        [SerializeField] private VelocitySource _velocitySource; // PlayerMovment or AIMovement

        /// <summary>
        /// Every snake part is spherical and symmetrically scaled, 
        /// so this adds/subtracts space betweeen parts beyond Scale/radius
        /// </summary>
        [SerializeField] private float _linkLengthOffset = 0.5f;

        private Snake _owner;
        private SnakeHead _snakeHead;
        private List<SnakePart> _snakeParts = new List<SnakePart>(DEFAULT_SNAKE_CAPACITY);
        private Terrain _terrain;
        private int _targetLength = MIN_SNAKE_LENGTH;
        private float _growthLinkLength = 0.0f;

        private float _accumulatedMovement = 0.0f;
        private List<(Vector3 position, float pathLength)> _pathWaypoints = new List<(Vector3, float)>(DEFAULT_SNAKE_CAPACITY);

        public const int MIN_SNAKE_LENGTH = 2;
        public const int DEFAULT_SNAKE_CAPACITY = 200;

        private const float PART_DIVISOR = 1.0f / MIN_SNAKE_LENGTH;
        private const float SCALE_MULTIPLIER = 1.5f;
        private const float WAYPOINTS_PER_LINK = 2.0f;
        private const float VELOCITY_BUFFER_FACTOR = 1.33f;
        private const float GROWTH_RATE = 0.33f;
        private const float SCALE_RATE = 0.05f;
        private const float SNAKE_EPSILON = 0.01f;

        // scales from 1 @ 6 parts to ~5 @ 200 parts
        private float _currentScale = 1.0f;
        private float TargetScale => ActiveLength > MIN_SNAKE_LENGTH ? SCALE_MULTIPLIER * Mathf.Log(ActiveLength * PART_DIVISOR) + 1 : 1.0f;
        private Terrain Terrain => _terrain ??= SpawnPointManager.WorldBounds.Terrain;

        /// <summary>
        /// Returns true if the link just behind the head (ie the "neck") 
        /// has near LinkLength in size (over/under), otherwise returns false.
        /// </summary>
        private bool IsNewPartInPosition => Mathf.Abs(_growthLinkLength - LinkLength) < SNAKE_EPSILON;

        public SnakeHead Head => _snakeHead;
        public VelocitySource VelocitySource => _velocitySource;

        /// <summary> Dynamic turning radius directly proportional to the snake's scale. </summary>
        public float TurningRadius => 1.0f * _currentScale;

        public float LinkLength => _currentScale + _linkLengthOffset;

        /// <summary> Returns the visible, active, length of the snake. </summary>
        public int ActiveLength => _snakeParts.Count(part => part.gameObject.activeSelf);

        /// <summary> 
        /// Returns the active head position of the snake
        /// at which new parts should be instantiated and positioned as they move into their final ordered position
        /// </summary>
        public Vector3 HeadPosition => _snakeParts[0].transform.position; // DEBUG: equivalent to _snakeHead.transform.position;

        /// <summary>
        /// Returns true if the given item is behind (not at) the given 
        /// body part number along the visible length of the snake, with 0 being the head.
        /// Returns false if part is not part of the snake, or at/in-front-of the given part number.
        /// </summary>
        public bool IsPartBehind(SnakePart part, int partNumber)
        {
            // DEBUG: _snakeParts is arranged as [0][199]...[2][1]
            // so an input of 1 will check against [0] and [199]
            int partIndex = _snakeParts.IndexOf(part);

            if (partIndex == -1 || partNumber == 0)
            {
                return false;
            }

            int partVisibleIndex = ActiveLength - partIndex;
            return partVisibleIndex > partNumber;
        }

        /// <summary>
        /// Returns the part closer to the head by <paramref name="offset"/> if positive, 
        /// and tail if <paramref name="offset"/> is negative.
        /// Returns the head or tail if offset is longer than available length.
        /// </summary>
        public SnakePart GetPartAheadOf(SnakePart part, int offset)
        {
            if (!_owner.IsPartOfSelf(part)) 
            {
                return null;
            }

            if (offset == 0)
            {
                return part;
            }

            int activeLength = ActiveLength;

            // DEBUG: account for [0][199][198]...[2][1] visible structure returning the head
            int partIndex = _snakeParts.IndexOf(part);
            if (partIndex == 0) 
            { 
                partIndex = activeLength;
            }

            int partVisibleIndex = activeLength - partIndex;
            int offsetVisibleIndex = Mathf.Clamp(partVisibleIndex - offset, 0, activeLength - 1);
            if (offsetVisibleIndex == 0)
            { 
                offsetVisibleIndex = activeLength;
            }

            return _snakeParts[activeLength - offsetVisibleIndex];
        }

        public void Init(SnakesManager snakesManager, Snake owner, SnakeHead head, SnakePart tail)
        {
            _owner = owner;
            _snakeHead = head;

            AddPart(_snakeHead);
            AddPart(tail);

            // DEBUG: head SpawnPoint must place the head's pivot on the Terrain surface
            // such that this places the head and tail tangent to the terrain surface
            head.transform.position += Vector3.up * (_currentScale * 0.5f);
            head.transform.forward = _velocitySource.CurrentFacing;

            Vector3 tailOffset = new Vector3(-0.707f, 0.0f, 0.707f) * LinkLength;
            Vector3 tailPosition = tail.transform.position + tailOffset;
            tailPosition.y = Terrain.SampleHeight(tailPosition) + (_currentScale * 0.5f);
            tail.transform.position = tailPosition;
            tail.transform.LookAt(head.transform);

            _growthLinkLength = (_snakeHead.transform.position - tail.transform.position).magnitude;
            _pathWaypoints.Add((tail.transform.position, 0.0f));
            _pathWaypoints.Add((_snakeHead.transform.position, _growthLinkLength));

            _velocitySource.Init(snakesManager, _owner);
        }

        private void UpdateScale()
        {
            // increasing length gradually increases _currentScale
            // and decreasing length immediately sets _currentScale to TargetScale
            _currentScale = Mathf.Min(_currentScale + SCALE_RATE * VelocitySource.GroundSpeed * Time.deltaTime, TargetScale);

            foreach (SnakePart snakePart in _snakeParts)
            {
                snakePart.transform.localScale = _currentScale * Vector3.one;
            }
        }

        /// <summary>
        /// Accumulates movement distance regardless of direction.
        /// When a threshold is reached the current position is cached. 
        /// <para/>
        /// DEBUG: applying movement before or after will result in a different cached position.
        /// </summary>
        private void AddMovementHistory(float movementMagnitude)
        {
            float frameMovementThreshold = LinkLength / WAYPOINTS_PER_LINK;

            _accumulatedMovement += movementMagnitude;

            if (_accumulatedMovement > frameMovementThreshold)
            {
                _pathWaypoints.Add((_snakeHead.transform.position, _accumulatedMovement));
                _accumulatedMovement = 0.0f;

                while (_pathWaypoints.Count > (_targetLength * WAYPOINTS_PER_LINK * VELOCITY_BUFFER_FACTOR))
                {
                    _pathWaypoints.RemoveAt(0);
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.5f);
            for (int i = 0; i < _pathWaypoints.Count; i++) 
            {
                Gizmos.DrawSphere(_pathWaypoints[i].position, _currentScale * 0.25f);
            }
        }

        /// <summary>
        /// Snake will grow by the given amount as path length becomes available (this is not instant length addition).
        /// NOTE: negative numbers are zeroed
        /// </summary>
        public void AddToTargetLength(int addLength)
        {
            _targetLength += Mathf.Max(0, addLength);
        }

        private void SubtractFromTargetLength(int removeLength)
        {
            _targetLength -= Mathf.Max(0, removeLength);
        }

        /// <summary> Returns true if a pre-instantiated, inactive, part was activated. Otherwise, returns false. </summary>
        public bool TryActivatePart()
        {
            int firstInactiveIndex = ActiveLength;

            if (firstInactiveIndex < _snakeParts.Count)
            { 
                _snakeParts[firstInactiveIndex].gameObject.SetActive(true);
                return true;
            }

            return false;
        }

        /// <summary> Adds a single part to the tail of the snake. </summary>
        public void AddPart(SnakePart newPart)
        {
            if (!_snakeParts.Contains(newPart))
            {
                _snakeParts.Add(newPart);
                newPart.transform.localScale = _currentScale * Vector3.one;
                newPart.transform.forward = _snakeHead.transform.forward;
            }
        }

        /// <summary> 
        /// Deactivates (not destroys) all parts positioned behind the given part, and returns all removed parts' positions.
        /// </summary>
        public List<Vector3> CutAt(SnakePart cutPart)
        {
            var partPositions = new List<Vector3>();
            int activeLength = ActiveLength;
            int cutIndex = _snakeParts.IndexOf(cutPart);

            // [0][199]...[2][1] with MIN_SNAKE_LENGTH == 2 means [0] and [199] are kill cuts
            bool ignoreCut = cutIndex >= activeLength + 1 - MIN_SNAKE_LENGTH || cutIndex == 0;
            if (ignoreCut) 
            {
                return partPositions;
            }

            // snakeParts are physically positioned as HEAD[0]...[4][3][2][1]TAIL,
            // therefore part index directly indicates the maximum number of parts to cut while preserving MIN_SNAKE_LENGTH
            int numPartsAvailableToCut = activeLength - MIN_SNAKE_LENGTH;
            int numPartsToCut = Mathf.Min(numPartsAvailableToCut, cutIndex);

            // DEBUG: deactivate parts starting at the neck, but return positions of parts starting from tail up towards head
            // eg: if activeLength == 200, MIN_SNAKE_LENGTH == 2, and cutPartIndex == 100, then numPartsToCut == 100,
            // so [0][199][198]...[3][2][1] deactivaates 100 indeces to become [0][99][98]...[3][2][1]
            // and [100][99]...[3][2][1] spawn positions are returned
            int positionIndex = 1; // the tail object is at [1]
            int surrogateCutIndex = activeLength - 1; // the neck position just behind head is at [activeLength - 1]
            for (int i = numPartsToCut; i > 0; --i)
            {
                partPositions.Add(_snakeParts[positionIndex].transform.position);
                _snakeParts[surrogateCutIndex].gameObject.SetActive(false);
                positionIndex++;
                surrogateCutIndex--;
            }

            SubtractFromTargetLength(numPartsToCut);
            return partPositions;
        }

        public void UpdateBody()
        {
            if (_velocitySource.IsStopped) 
            {
                return;
            }

            _velocitySource.RotateToFaceTargetHeading(TurningRadius);
            Vector3 headMovement = Time.deltaTime * _velocitySource.CurrentFacing * _velocitySource.GroundSpeed;

            // DEBUG: assumes headMovement is a vector on the XZ plane,
            // and all movement is on the XZ plane
            // account for sudden y-axis growth
            Vector3 initialHeadPosition = HeadPosition;
            Vector3 finalHeadPosition = initialHeadPosition + headMovement;
            finalHeadPosition.y = Terrain.SampleHeight(finalHeadPosition) + (_currentScale * 0.5f);

            headMovement = finalHeadPosition - initialHeadPosition;
            finalHeadPosition = initialHeadPosition + headMovement;

            float headMovementMagnitude = headMovement.magnitude;
            _snakeHead.transform.LookAt(finalHeadPosition);
            _snakeHead.transform.position = finalHeadPosition;
            AddMovementHistory(headMovementMagnitude);

            if (_pathWaypoints.Count < 1)
            {
                return;
            }

            // initialize pathLength to account for continuous motion (ie: not waypoint snapping)
            float pathLength = (_pathWaypoints[_pathWaypoints.Count - 1].position - finalHeadPosition).magnitude;
            float waypointLength = 0.0f;
            int activeLength = ActiveLength;
            bool isGrowing = activeLength < _targetLength;

            // grow neck link from 0 to linkLength, then adds the next part
            if (IsNewPartInPosition) 
            {
                if (isGrowing) 
                { 
                    _owner.Grow();
                    _growthLinkLength = 0.0f;
                    activeLength++;
                }
            }
            else
            {
                _growthLinkLength = Mathf.Min(_growthLinkLength + GROWTH_RATE * headMovementMagnitude, LinkLength);
            }

            // DEBUG: when the scale reaches a approx-plateau, the delta when adding new parts
            // can become smaller than SNAKE_EPSILON, thereby slowing UpdateScale calls until more parts are added
            if (Mathf.Abs(_currentScale - TargetScale) > SNAKE_EPSILON)
            {
                UpdateScale();
            }

            // interpolates positions of all parts along the cached path of the head
            // iterating pathIndex from [Count - 1, 0] is the path head to tail
            // _snakeParts[0] is always head object, _snakeParts[1] is always tail object, all other parts are "neck" parts
            // eg: the snakeParts array is iterated as it expands like this => [0][1], then [0][2][1], then [0][3][2][1], then [0][4][3][2][1]
            // with [0] (head) being updated outside this loop
            for (int i = activeLength - 1, pathIndex = _pathWaypoints.Count - 1; i > 0 && pathIndex >= 1; --i)
            {
                float targetLinkLength = !IsNewPartInPosition && i == activeLength - 1 ? _growthLinkLength : LinkLength;

                // never use the 0th waypointLength because it cant provide a inter-waypoint path direction
                while (pathIndex >= 1)
                {
                    waypointLength = _pathWaypoints[pathIndex].pathLength;

                    // never use the 0th waypointLength
                    if (pathLength + waypointLength > targetLinkLength || pathIndex == 1)
                    {
                        break;
                    }

                    pathLength += waypointLength;
                    pathIndex--;
                }

                // [0,1] fraction of current waypointLength needed to match LinkLength
                float t = (targetLinkLength - pathLength) / waypointLength;
                Vector3 offset = t * (_pathWaypoints[pathIndex - 1].position - _pathWaypoints[pathIndex].position);
                _snakeParts[i].transform.position = _pathWaypoints[pathIndex].position + offset;
                _snakeParts[i].transform.LookAt(_snakeParts[i + 1 < activeLength ? i + 1 : 0].transform.position); // account for [0][199][198]...[2][1] array order

                // preserve any remainder for the next part
                // ensure pathIndex (ie one waypointLength) isnt counted twice
                pathLength = (1.0f - t) * waypointLength;
                pathIndex--;
            }
        }
    }
}