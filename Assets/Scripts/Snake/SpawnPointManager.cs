using System.Linq;
using UnityEngine;
using Freehill.Boids;

namespace Freehill.SnakeLand
{
    public class SpawnPoint
    {
        public Vector3 position;
        public bool available;
    }

    public class SpawnPointManager : MonoBehaviour
    {
        private static SpawnPointManager _instance;

        [SerializeField] private WorldBounds _mapBounds;
        [SerializeField][Min(1.0f)] private float _spawnPointRadius = 2.0f;

        private SpawnPoint[] _spawnPoints;
        private int _rowWidth;

        public static WorldBounds WorldBounds => _instance._mapBounds;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else
            {
                Destroy(this);
                return;
            }

            Vector3 mapSize = _mapBounds.Extents * 2.0f;
            Vector3 mapOrigin = _mapBounds.Center - _mapBounds.Extents;
            float x = mapOrigin.x + _spawnPointRadius;
            float z = mapOrigin.z + _spawnPointRadius;
            float maxX = mapOrigin.x + mapSize.x;
            float maxZ = mapOrigin.z + mapSize.z;

            // map is oriented on XZ-plane
            int xCells = Mathf.FloorToInt(mapSize.x / (2.0f * _spawnPointRadius));
            int zCells = Mathf.FloorToInt(mapSize.z / (2.0f * _spawnPointRadius));
            _spawnPoints = new SpawnPoint[xCells * zCells];
            _rowWidth = zCells;

            for (int i = 0; i < _spawnPoints.Length; ++i)
            {
                _spawnPoints[i] = new SpawnPoint
                {
                    position = new Vector3(x, 0.0f, z),
                    available = true
                };

                z += 2.0f * _spawnPointRadius;

                if (z > maxZ)
                {
                    x += 2.0f * _spawnPointRadius;
                    z = mapOrigin.z + _spawnPointRadius;
                }

                if (x > maxX)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Permanently marks all spawn points within the radius of the given position as unavailable
        /// </summary>
        public static void BlockSpawnPoints(Vector3 position, float radius)
        {
            Vector3 mapOrigin = _instance._mapBounds.Center - _instance._mapBounds.Extents;

            int row = Mathf.FloorToInt((position.x - mapOrigin.x) / _instance._spawnPointRadius);
            int col = Mathf.FloorToInt((position.z - mapOrigin.z) / _instance._spawnPointRadius);

            int index = col + (row * _instance._rowWidth);
            _instance._spawnPoints[index].available = false;
            Vector3 blockOrigin = _instance._spawnPoints[index].position;

            // check neighbors of blockOrigin, then their neighbors, until no blockage is found
            bool blockageFound = true;
            for (int n = 1; n < _instance._rowWidth && blockageFound; ++n)
            {
                blockageFound = false;
                for (int r = row - n; r <= row + n; ++r)
                {
                    for (int c = col - n; c <= col + n; ++c)
                    {
                        index = col + (row * _instance._rowWidth);
                        if (index >= 0 && index < _instance._spawnPoints.Length
                            && Vector3.Distance(_instance._spawnPoints[index].position, blockOrigin) <= radius)
                        {
                            _instance._spawnPoints[index].available = false;
                            blockageFound = true;
                        }
                    }
                }
            }
        }

        public static void FreeSpawnPoint(SpawnPoint spawnPoint)
        {
            spawnPoint.available = true;
        }

        /// <summary> 
        /// Returns a reference to a random available spawn point within the map area.
        /// NOTE: call <see cref="FreeSpawnPoint(SpawnPoint)"/> when done with it. 
        /// </summary>
        public static SpawnPoint GetRandomSpawnPoint()
        {
            int attempts = 1;

            SpawnPoint spawnPoint = _instance._spawnPoints[Random.Range(0, _instance._spawnPoints.Length)];
            while (!spawnPoint.available && attempts < _instance._spawnPoints.Length)
            {
                spawnPoint = _instance._spawnPoints[Random.Range(0, _instance._spawnPoints.Length)];
                attempts++;
            }

            if (!spawnPoint.available) 
            {
                spawnPoint = _instance._spawnPoints.FirstOrDefault(point => point.available);
            }

            spawnPoint.available = false;

            return spawnPoint;
        }

        /// <summary> Returns the position jittered and positioned on the surface of the bounds' terrain within the given radius </summary>
        public static Vector3 GetJitteredPosition(SpawnPoint spawnPoint)
        {
            //Vector3 onXZUnitCircle = new Vector3(Random.Range(-1.0f, 1.0f), 0.0f, Random.Range(-1.0f, 1.0f)).normalized;
            //return spawnPoint.position + onXZUnitCircle * _instance._spawnPointRadius;

            return _instance._mapBounds.GetOnGroundPosition(spawnPoint.position + Random.onUnitSphere * _instance._spawnPointRadius);
        }
    }
}
