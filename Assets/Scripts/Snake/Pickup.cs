using UnityEngine;

namespace Freehill.SnakeLand
{
    public class Pickup : MonoBehaviour
    {
        public enum POWER : int
        { 
            GROW, // food, love
            BLAST_MAGNET,// watermellon
            FIREBALL, // pineapple
            TEMP_IMMUNITY // coconut
        }

        [SerializeField] private POWER _power = POWER.GROW;

        private SphereCollider _sphereCollider;
        private SpawnPoint _spawnPoint;

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
        public void Init(Vector3? forcedSpawnPosition = null)
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
        }

        public void SetUsed()
        {
            gameObject.SetActive(false);

            if (_spawnPoint != null)
            {
                SpawnPointManager.FreeSpawnPoint(_spawnPoint);
            }
        }
    }
}
