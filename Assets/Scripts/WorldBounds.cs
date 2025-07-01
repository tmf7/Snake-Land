using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Freehill.Boids
{
    /// <summary>
    /// Axis-aligned global bounding box that avoids the need for a BoxCollider and physics operations
    /// </summary>
    public class WorldBounds : MonoBehaviour
    {
        [Header("Gizmo Tests")]
        public Transform _testOrigin;
        public float _testLineLength = 100.0f;
        public float _bufferDistance = 5.0f;

        [Header("Bounds")]
        [SerializeField] private TerrainCollider _terrainCollider;

        private Terrain _terrain;
        private RaycastHit _terrainHitInfo;

        // only draw extents in inspector because transform position is used as center
        [HideInInspector][SerializeField] private Bounds _bounds;

        public Terrain Terrain => _terrain ??= _terrainCollider.GetComponent<Terrain>();
        public Vector3 Center => _bounds.center;
        public Vector3 Extents => _bounds.extents;

        /// <summary> Returns true if the given point is on or within the bounds, returns false otherwise </summary>
        public bool Contains(Vector3 point) => _bounds.Contains(point);

        /// <summary> Returns the given point if within the bounds, otherwise returns the nearest point on the surface of the bounds </summary>
        public Vector3 ClosestPoint(Vector3 point) => _bounds.ClosestPoint(point);

        /// <summary>
        /// Returns the given movement such that if it exceeds the bounds on any axis, that axis of movment is reversed
        /// </summary>
        /// <param name="position"></param>
        /// <param name="movement"></param>
        /// <returns></returns>
        public Vector3 ConstrainMovement(Vector3 position, Vector3 movement)
        {
            Vector3 newPosition = position + movement;

            if (newPosition.x < _bounds.min.x || newPosition.x > _bounds.max.x) { movement.x *= -1.0f; }
            if (newPosition.y < _bounds.min.y || newPosition.y > _bounds.max.y) { movement.y *= -1.0f; }
            if (newPosition.z < _bounds.min.z || newPosition.z > _bounds.max.z) { movement.z *= -1.0f; }
            
            return movement;
        }

        /// <summary> Returns a point above the TerrainCollider for this bounds, or the point itself if already above the terrain. </summary>
        public Vector3 GetAboveGroundPosition(Vector3 point, float bufferDistance = 0.0f)
        {
            // DEBUG: TerrainColliders can only be Raycast from above, hence why the ray origin is far above the terrain
            if (_terrainCollider.Raycast(new Ray(point + Vector3.up * Extents.sqrMagnitude, Vector3.down), out _terrainHitInfo, Extents.sqrMagnitude + bufferDistance))
            {
                return _terrainHitInfo.point + Vector3.up * bufferDistance;
            }

            return point;
        }

        /// <summary> Returns the point precisely on the TerrainCollider for the given worldspace position </summary>
        public Vector3 GetOnGroundPosition(Vector3 point)
        {
            point.y = Terrain.SampleHeight(point);
            return point;
        }

        #region MESSING_AROUND_WITH_GEOMETRIC_TESTS
        private void OnDrawGizmos()
        {
            if (_testOrigin == null || _terrainCollider == null)
            {
                return;
            }

            // ground testing
            Vector3 aboveGround = GetAboveGroundPosition(_testOrigin.position, 2.0f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(aboveGround, 2.0f);
            Gizmos.DrawLine(_testOrigin.position, _testOrigin.position + Vector3.up * Extents.sqrMagnitude);


            // collide and slide testing
            Vector3 start = _testOrigin.position;
            Vector3 end = _testOrigin.position + _testOrigin.forward * _testLineLength;

            Vector3 intersection;
            Vector3 normal;
            Vector3 bentEnd = BendLineAlongSurface(start, end, out intersection, out normal, _bufferDistance);

            if (bentEnd != end)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(intersection, intersection + normal * 100.0f);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(intersection, 2.0f);
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(bentEnd, 2.0f);

                Gizmos.DrawLine(start, intersection);
                Gizmos.DrawLine(intersection, bentEnd);
            }
            else
            {
                Gizmos.DrawLine(start, end);
            }

            // edge vector testing
            Vector3 xOffset = Vector3.zero;
            Vector3 yOffset = Vector3.zero;
            Vector3 zOffset = Vector3.zero;
            GetBoundaryOffsets(_testOrigin.position, ref xOffset, ref yOffset, ref zOffset);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(_testOrigin.position, _testOrigin.position - yOffset);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(_testOrigin.position, _testOrigin.position - xOffset);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(_testOrigin.position, _testOrigin.position - zOffset);
        }

        /// <summary>
        /// Returns a new line segment endpoint such that if it exits the bounds, then line is first trucated, 
        /// then bent along the surface normal for its remaining length. If line does not leave bounds, then original endpoint is returned unmodified.
        /// </summary>
        /// <param name="bufferDistance"> How far the bent end position should be back from the bounds. Default places the bent end on the surface. </param>
        public Vector3 BendLineAlongSurface(Vector3 start, Vector3 end, out Vector3 intersection, out Vector3 normal, float bufferDistance = 0.0f)
        {
            float distance;
            intersection = Vector3.zero;
            normal = Vector3.zero;

            // TODO: bufferDistance should put the end point on a plane that distance from the hit plane
            // and bend along the corresponding buffered intersection point, trading lengths
            if (IntersectLineWithSurface(start, end, out distance))
            {
                Vector3 direction = (end - start).normalized;
                intersection = start + direction * distance;

                Vector3 remainingLine = end - intersection;
                normal = GetReflectionNormal(intersection, direction);
                Vector3 bentDirection = Vector3.ProjectOnPlane(remainingLine, normal).normalized;

                end = intersection + bentDirection * remainingLine.magnitude;
            }

            return end;
        }

        /// <summary>
        /// Returns the normal vector of the plane to which the given point belongs.
        /// The direction vector determines which front|back face normal is returned.
        /// Normal always opposes the given direction.
        /// </summary>
        /// <param name="ofPoint">A point found in one of the bounds' surface planes. </param>
        /// <param name="inDirection">The direction used to determine the reflection normal.</param>
        /// <returns></returns>
        private Vector3 GetReflectionNormal(Vector3 ofPoint, Vector3 inDirection)
        {
            if (PlaneContainsPoint(Vector3.right, Extents.x, ofPoint)) { return (Vector3.Dot(Vector3.right, inDirection) < 0.0f ? Vector3.right : Vector3.left); }
            if (PlaneContainsPoint(Vector3.left, Extents.x, ofPoint)) { return (Vector3.Dot(Vector3.left, inDirection) < 0.0f ? Vector3.left : Vector3.right); }
            if (PlaneContainsPoint(Vector3.up, Extents.y, ofPoint)) { return (Vector3.Dot(Vector3.up, inDirection) < 0.0f ? Vector3.up : Vector3.down); }
            if (PlaneContainsPoint(Vector3.down, Extents.y, ofPoint)) { return (Vector3.Dot(Vector3.down, inDirection) < 0.0f ? Vector3.down : Vector3.up); }
            if (PlaneContainsPoint(Vector3.forward, Extents.z, ofPoint)) { return (Vector3.Dot(Vector3.forward, inDirection) < 0.0f ? Vector3.forward : Vector3.back); }
            if (PlaneContainsPoint(Vector3.back, Extents.z, ofPoint)) { return (Vector3.Dot(Vector3.back, inDirection) < 0.0f ? Vector3.back : Vector3.forward); }

            return Vector3.zero;
        }

        private bool PlaneContainsPoint(Vector3 planeNormal, float planeDist, Vector3 point)
        {
            const float EPSILON = 0.0001f;

            Vector3 pointInPlane = Center + planeNormal * planeDist;
            float d = Vector3.Dot(planeNormal, pointInPlane);

            return Mathf.Abs(Vector3.Dot(planeNormal, point) - d) < EPSILON;
        }

        /// <summary>
        /// Fills the three vectors containing the shortest distances to the x, y, and z planes of this bounds to the given point.
        /// All vectors point into the bounds.
        /// </summary>
        public void GetBoundaryOffsets(Vector3 point, ref Vector3 xOffset, ref Vector3 yOffset, ref Vector3 zOffset)
        {
            Vector3 centerOffset = point - Center;

            float xDist = Vector3.Dot(centerOffset, Vector3.right);
            float yDist = Vector3.Dot(centerOffset, Vector3.up);
            float zDist = Vector3.Dot(centerOffset, Vector3.forward);

            // Extents.x - Abs(xDist) = shortest dist to the x plane, and -Sign(xDist) ensures the vector always points into the bounds
            xOffset.Set(-Mathf.Sign(xDist) * (Extents.x - Mathf.Abs(xDist)), 0.0f, 0.0f);
            yOffset.Set(0.0f, -Mathf.Sign(yDist) * (Extents.y - Mathf.Abs(yDist)), 0.0f);
            zOffset.Set(0.0f, 0.0f, -Mathf.Sign(zDist) * (Extents.z - Mathf.Abs(zDist)));
        }

        public bool IntersectLineWithSurface(Vector3 start, Vector3 end, out float distance)
        {
            Vector3 direction = end - start;
            return IntersectRayWithSurface(start, direction.normalized, out distance, direction.magnitude);
        }

        /// <summary>
        /// Returns true if the given ray intersects this bounds within maxDistance, false otherwise. 
        /// Sets the intersection point to the nearest point on the surface of the bounds from the origin along the direction.
        /// DEBUG: assumes direction is normalized
        /// </summary>
        public bool IntersectRayWithSurface(Vector3 origin, Vector3 direction, out float distance, float maxDistance = float.MaxValue)
        {
            distance = 0.0f;

            float minDist = 0.0f;
            float maxDist = maxDistance;

            for (int i = 0; i < 3; i++)
            {
                if (Mathf.Abs(direction[i]) < Mathf.Epsilon)
                {
                    // ray is parallel to slab. no hit if origin is not within the slab
                    // nested if to avoid updates to dist values if within this slab
                    if (origin[i] < _bounds.min[i] || origin[i] > _bounds.max[i])
                    {
                        return false;
                    }
                }
                else
                { 
                    // compute intersection t value of ray with near and far plane of slab
                    float ood = 1.0f / direction[i];
                    float nearDist = (_bounds.min[i] - origin[i]) * ood;
                    float farDist = (_bounds.max[i] - origin[i]) * ood;

                    // ensure nearDist is the intersection with near plane, farDist with far plane
                    if (nearDist > farDist)
                    {
                        float temp = nearDist;
                        nearDist = farDist;
                        farDist = temp;
                    }

                    minDist = Mathf.Max(minDist, nearDist);
                    maxDist = Mathf.Min(maxDist, farDist);

                    // exit with no collision as soon as slab intersection becomes empty
                    if (minDist > maxDist)
                    {
                        return false;
                    }
                }
            }

            // use-case for this program: return the far slab point of intersection if within the bounds
            if (minDist < Mathf.Epsilon)
            {
                // too far for intersection with all slabs
                if (maxDist >= maxDistance)
                {
                    return false;
                }
                distance = maxDist;
            }
            else
            {
                distance = minDist;
            }

            return true;
        }
        #endregion MESSING_AROUND_WITH_GEOMETRIC_TESTS

        #region EDITOR
#if UNITY_EDITOR
        [CustomEditor(typeof(WorldBounds))]
        public class WorldBoundsEditor : Editor
        {
            private WorldBounds _worldBounds;
            private EditorWindow _sceneView;

            private void OnEnable()
            {
                _worldBounds = target as WorldBounds;
                _worldBounds._bounds.center = _worldBounds.transform.position;
                _sceneView = EditorWindow.GetWindow<SceneView>();
            }

            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();

                Undo.RecordObject(_worldBounds, "updating extents");

                EditorGUI.BeginChangeCheck();
                Vector3 extents = EditorGUILayout.Vector3Field("Extents", _worldBounds.Extents);

                if (EditorGUI.EndChangeCheck() ) 
                {
                    extents = new Vector3(Mathf.Max(extents.x, 0.0f), Mathf.Max(extents.y, 0.0f), Mathf.Max(extents.z, 0.0f));
                    _worldBounds._bounds.extents = extents;
                    _sceneView.Repaint();
                }
            }

            private void OnSceneGUI()
            {
                _worldBounds._bounds.center = _worldBounds.transform.position;
                Handles.DrawWireCube(_worldBounds.Center, _worldBounds.Extents * 2.0f);

                Undo.RecordObject(_worldBounds, "updating extents");

                EditorGUI.BeginChangeCheck();
                Vector3 extents = Handles.ScaleHandle(_worldBounds.Extents, _worldBounds.Center, Quaternion.identity, 45);

                if (EditorGUI.EndChangeCheck())
                {
                    extents = new Vector3(Mathf.Max(extents.x, 0.0f), Mathf.Max(extents.y, 0.0f), Mathf.Max(extents.z, 0.0f));
                    _worldBounds._bounds.extents = extents;
                    _sceneView.Repaint();
                }
            }
        }
#endif
        #endregion EDITOR
    }
}
