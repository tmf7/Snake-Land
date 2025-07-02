using UnityEngine;
using Random = UnityEngine.Random;

namespace Freehill.SnakeLand
{
    public class Pickup : MonoBehaviour
    {
        public enum POWER : int
        { 
            GROW, // apple, love
            BLAST_MAGNET,// watermellon
            FIREBALL, // pineapple
            TEMP_IMMUNITY // coconut
        }

        [SerializeField] private POWER _power = POWER.GROW;

        private SphereCollider _sphereCollider;
        private SpawnPoint _spawnPoint;
        private Vector3 _initialPosition;
        private float _currentRotationRadians;
        private float _rotationSpeedRadians;
        private float _bounceHeight;

        private const float GROUND_OFFSET_SCALER = 1.2f;

        /// <summary> Informs how a snake should respond to touching this pickup. </summary>
        public POWER Power => _power;

        /// <summary>
        /// Returns true if object in pool is disabled and can be re-positioned/re-activated.
        /// </summary>
        public bool NeedsRespawn => !gameObject.activeSelf;

        private void Awake()
        {
            _sphereCollider = GetComponent<SphereCollider>();
        }

        /// <summary>
        /// Positions and activates this at a random <see cref="SpawnPointManager.GetRandomSpawnPoint"/> or the <paramref name="forcedSpawnPosition"/>
        /// </summary>
        /// <param name="forcedSpawnPosition"> The specific world position this pickup should have. Default is a random spawn point. </param>
        public void Init(float rotationSpeedRadians, float bounceHeight, Vector3? forcedSpawnPosition = null)
        {
            if (forcedSpawnPosition == null)
            {
                _spawnPoint = SpawnPointManager.GetRandomSpawnPoint();
                Vector3 position = SpawnPointManager.GetJitteredPosition(_spawnPoint);
                position.y += _sphereCollider.radius * GROUND_OFFSET_SCALER;
                transform.position = position;
            }
            else
            {
                transform.position = (Vector3)forcedSpawnPosition;
            }

            gameObject.SetActive(true);
            _initialPosition = transform.position;
            _currentRotationRadians = Random.Range(-Mathf.PI, Mathf.PI);
            _rotationSpeedRadians = rotationSpeedRadians;
            _bounceHeight = bounceHeight;
        }

        public void SetUsed()
        {
            gameObject.SetActive(false);

            if (_spawnPoint != null)
            {
                SpawnPointManager.FreeSpawnPoint(_spawnPoint);
            }
        }

        private void Update()
        {
            float radiansDelta = _rotationSpeedRadians * Time.deltaTime;
            _currentRotationRadians += radiansDelta;
            transform.position = _initialPosition + Vector3.up * _bounceHeight * (1.0f + Mathf.Sin(_currentRotationRadians));
            transform.Rotate(transform.up, radiansDelta * Mathf.Rad2Deg);
        }
    }
}
