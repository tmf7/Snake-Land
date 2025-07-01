using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Freehill.SnakeLand
{
    /// <summary> Object pool that moves pickups around the map between spawn points, and freeform points </summary>
    public class PickupManager : MonoBehaviour
    {
        private static PickupManager _instance;

        [SerializeField] private List<Pickup> _foodPrefabs = new List<Pickup>();
        [SerializeField] private Pickup _blastMagnetPrefab;
        [SerializeField] private Pickup _fireballPrefab;
        [SerializeField] private Pickup _immunityPrefab;
        [SerializeField] private Pickup _lovePrefab;

        [SerializeField] private int _maxFood = 1000;
        [SerializeField] private int _maxBlastMagnet = 20;
        [SerializeField] private int _maxFireball = 50;
        [SerializeField] private int _maxImmunity = 10;

        private Pickup[] _foodPickups;
        private Pickup[] _blastMagnetPickups;
        private Pickup[] _fireballPickups;
        private Pickup[] _immunityPickups;
        private List<Pickup> _lovePickups = new List<Pickup>(BASE_LOVE_CAPACITY);

        private const int BASE_LOVE_CAPACITY = 200;
        private const float FOOD_RESPAWN_TIME_SEC = 5.0f;
        private const float POWER_UP_RESPAWN_TIME_SEC = 20.0f;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else
            {
                Destroy(this);
            }

            _foodPickups = new Pickup[_maxFood];
            _blastMagnetPickups = new Pickup[_maxBlastMagnet];
            _fireballPickups = new Pickup[_maxFireball];
            _immunityPickups = new Pickup[_maxImmunity];
        }

        private void Start()
        {
            for (int i = 0; i < _foodPickups.Length; ++i) 
            {
                _foodPickups[i] = Instantiate(_foodPrefabs[Random.Range(0, _foodPrefabs.Count)], transform);
                _foodPickups[i].Init();
            }

            for (int i = 0; i < _blastMagnetPickups.Length; ++i)
            {
                _blastMagnetPickups[i] = Instantiate(_blastMagnetPrefab, transform);
                _blastMagnetPickups[i].Init();
            }

            for (int i = 0; i < _fireballPickups.Length; ++i)
            {
                _fireballPickups[i] = Instantiate(_fireballPrefab, transform);
                _fireballPickups[i].Init();
            }

            for (int i = 0; i < _immunityPickups.Length; ++i)
            {
                _immunityPickups[i] = Instantiate(_immunityPrefab, transform);
                _immunityPickups[i].Init();
            }

            StartCoroutine(RespawnFood());
            StartCoroutine(RespawnPowerUps());
        }

        private IEnumerator RespawnFood()
        {
            while (Application.isPlaying)
            {
                yield return new WaitForSeconds(FOOD_RESPAWN_TIME_SEC);

                for (int i = 0; i < _foodPickups.Length; ++i)
                {
                    if (_foodPickups[i].NeedsRespawn)
                    {
                        _foodPickups[i].Init();
                    }
                }
            }
        }

        private IEnumerator RespawnPowerUps()
        {
            while (Application.isPlaying)
            {
                yield return new WaitForSeconds(POWER_UP_RESPAWN_TIME_SEC);

                for (int i = 0; i < _blastMagnetPickups.Length; ++i)
                {
                    if (_blastMagnetPickups[i].NeedsRespawn)
                    {
                        _blastMagnetPickups[i].Init();
                    }
                }

                for (int i = 0; i < _fireballPickups.Length; ++i)
                {
                    if (_fireballPickups[i].NeedsRespawn)
                    {
                        _fireballPickups[i].Init();
                    }
                }

                for (int i = 0; i < _immunityPickups.Length; ++i)
                {
                    if (_immunityPickups[i].NeedsRespawn)
                    {
                        _immunityPickups[i].Init();
                    }
                }
            }
        }

        public static void SpawnLove(List<Vector3> positions)
        {
            int spawnsNeeded = positions.Count;
            int spawnCount = 0;
            
            for (int i = 0; i < _instance._lovePickups.Count && spawnCount < spawnsNeeded; ++i)
            {
                if (_instance._lovePickups[i].NeedsRespawn)
                {
                    _instance._lovePickups[i].Init(positions[spawnCount]);
                    spawnCount++;
                }
            }

            for (int i = spawnCount; i < spawnsNeeded; ++i)
            {
                Pickup newLove = Instantiate(_instance._lovePrefab, _instance.transform);
                newLove.Init(positions[i]);
                _instance._lovePickups.Add(newLove);
            }
        }
    }
}
