using System.Collections.Generic;
using UnityEngine;

namespace Freehill.Boids
{
    public class BoidSpawner : MonoBehaviour
    {
        [SerializeField] private WorldBounds _worldBounds;
        [SerializeField] private Boid[] _boidPrefabs;
        [SerializeField] private int _spawnAmount = 50;
        [SerializeField][Min(0.25f)] private float _spawnRadius = 20.0f;

        // FIXME: make these dynamic, like attach an "Attractor" script or subtypes to a transform
        // then either (1) register the attractor with the spawner to share w/boids or (2) have each boid discover objects via collision
        // SOLUTION: don't make this so generic, solve a specific use case...so...what's the use case?
        // [] move within a volume
        // [] group with some, avoid some, attract to some
        [SerializeField] private Transform[] _attractors;
        [SerializeField] private Transform[] _repulsors;

        [Header("Boid Weights")]
        [SerializeField][Min(0.1f)] private float _minSpeed = 10.0f;
        [SerializeField][Min(0.1f)] private float _maxSpeed = 20.0f;
        [SerializeField][Min(0.0f)] private float _separationWeight;
        [SerializeField][Min(0.0f)] private float _alignmentWeight;
        [SerializeField][Min(0.0f)] private float _cohesionWeight;
        [SerializeField][Min(0.0f)] private float _randomWeight;
        [SerializeField][Min(0.0f)] private float _attractionWeight;
        [SerializeField][Min(0.0f)] private float _repulsionWeight;
        [SerializeField][Min(0.0f)] private float _boundaryPushWeight;
        [SerializeField][Min(0.0f)] private float _groundPushWeight;
        [SerializeField][Min(0.0f)] private float _gravityWeight;

        [Header("Boid Awareness")]
        [SerializeField] private float _boidNeighborhoodRadius = 3;
        [SerializeField][Min(0.0f)] private float _groundProximityRadius;

        [SerializeField][Min(0.0f)] private float _boundsProximityRadius;
        [SerializeField][Min(0.0f)] private float _attractorProximityRadius;
        [SerializeField][Min(0.0f)] private float _repulsorProximityRadius;

        // DEBUG: unique random base speeds from this spawner, the same prefab in a different spawner will have a different random base speed
        private Dictionary<int, float> _boidSpeeds = new Dictionary<int, float>();
        private List<Boid> _spawnedBoids = new List<Boid>();

        public WorldBounds WorldBounds => _worldBounds;
        public float SeparationWeight => _separationWeight;
        public float AlignmentWeight => _alignmentWeight;
        public float CohesionWeight => _cohesionWeight;
        public float RandomWeight => _randomWeight;
        public float AttractionWeight => _attractionWeight;
        public float RepulsionWeight => _repulsionWeight;
        public float BoundaryPushWeight => _boundaryPushWeight;
        public float GravityWeight => _gravityWeight;

        public float BoidNeighborhoodRadius => _boidNeighborhoodRadius;
        public float GroundProximityRadius => _groundProximityRadius;
        public float BoundsProximityRadius => _boundsProximityRadius;
        public float AttractorProximityRadius => _attractorProximityRadius;
        public float RepulsorProximityRadius => _repulsorProximityRadius;

        public IEnumerable<Boid> SpawnedBoids => _spawnedBoids;
        public IEnumerable<Transform> Attractors => _attractors;
        public IEnumerable<Transform> Repulsors => _repulsors;

        private void Awake()
        {
            Spawn();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, _spawnRadius);
        }

        private void Spawn()
        {
            ClearBoids();

            for (int i = 0; i < _spawnAmount; i++) 
            {
                Vector3 localSpawnPosition = transform.position + Random.insideUnitSphere * _spawnRadius;

                int boidType = Random.Range(0, _boidPrefabs.Length - 1);
                float speed = Random.Range(_minSpeed, _maxSpeed);

                if (_boidSpeeds.ContainsKey(boidType))
                {
                    speed = _boidSpeeds[boidType];
                }
                else
                {
                    _boidSpeeds[boidType] = speed;
                }

                Boid newBoid = Instantiate(_boidPrefabs[boidType], localSpawnPosition, Quaternion.identity, transform);
                newBoid.Initialize(this, boidType, speed);
                _spawnedBoids.Add(newBoid);
            }
        }

        public void ClearBoids()
        {
            foreach (Boid boid in _spawnedBoids)
            { 
                Destroy(boid.gameObject);
            }

            _spawnedBoids.Clear();
        }
    }
}