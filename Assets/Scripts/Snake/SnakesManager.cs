using System.Collections.Generic;
using UnityEngine;

namespace Freehill.SnakeLand
{
    public class SnakesManager : MonoBehaviour
    {
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private List<GameObject> _aiSnakePrefabs = new List<GameObject>();

        [SerializeField] private int _aiSnakeSpawns = 50;
        [SerializeField][Min(0.0f)] private float _pickupSeekWeight;
        [SerializeField][Min(0.0f)] private float _wanderWeight;
        [SerializeField][Range(0.0f, 180.0f)] private float _wanderErraticness;
        [SerializeField][Min(0.0f)] private float _snakePartEvadeWeight;
        [SerializeField][Min(0.0f)] private float _snakeHeadPursueWeight;
        [SerializeField][Min(0.0f)] private float _snakeHeadEvadeWeight;
        [SerializeField][Min(0.0f)] private float _boundaryPushWeight;
        [SerializeField][Min(0.0f)] private float _boundsProximityRadius;

        public float PickupSeekWeight => _pickupSeekWeight;
        public float WanderWeight => _wanderWeight;
        public float WanderErraticness => _wanderErraticness;
        public float SnakePartEvadeWeight => _snakePartEvadeWeight;
        public float SnakeHeadPursueWeight => _snakeHeadPursueWeight;
        public float SnakeHeadEvadeWeight => _snakeHeadEvadeWeight;
        public float BoundaryPushWeight => _boundaryPushWeight;
        public float BoundsProximityRadius => _boundsProximityRadius;

        private List<Snake> _spawnedSnakeAIs = new List<Snake>();

        private void Start()
        {
            GameObject player = Instantiate(_playerPrefab);
            player.GetComponentInChildren<Snake>().Init(this);

            for (int i = 0; i < _aiSnakeSpawns; ++i)
            {
                GameObject aiSnakeGO = Instantiate(_aiSnakePrefabs[Random.Range(0, _aiSnakePrefabs.Count)]);
                aiSnakeGO.name += $" ({i})";
                Snake aiSnake = aiSnakeGO.GetComponent<Snake>();
                aiSnake.Init(this);
                _spawnedSnakeAIs.Add(aiSnake);
            }
        }
    }
}
