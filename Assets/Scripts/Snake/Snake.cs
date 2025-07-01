using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Freehill.SnakeLand
{
    [RequireComponent(typeof(SnakeMovement))]
    public class Snake : MonoBehaviour
    {
        public SnakeHead _snakeHeadPrefab;
        public Transform _snakePartPrefab;
        public Transform _snakeTailPrefab;

        [SerializeField] private SnakeMovement _snakeMovement;

        public SnakeMovement SnakeMovement => _snakeMovement;
        public SnakeHead Head => _snakeMovement.Head;

        private static List<int> _lengths = new List<int>(50);
        private static int _availableLengthIndex = 0;
        private static Random rand = new Random();
        static Snake()
        {
            _lengths.Clear();
            for (int i = 0; i < 47; i++)
            {
                _lengths.Add(rand.Next(0, 16));
            }
            _lengths.Add(200);
            _lengths.Add(200);
            _lengths.Add(200);
            _availableLengthIndex = 0;
            Debug.Log("Snake Static CTOR Called");
        }

        private static int GetLength()
        {
            return _lengths[_availableLengthIndex++];
        }

        public void Init(SnakesManager snakesManager)
        {
            // DEBUG: snake spawn points are never freed for re-use
            SpawnPoint spawnPoint = SpawnPointManager.GetRandomSpawnPoint();
            transform.position = SpawnPointManager.GetJitteredPosition(spawnPoint);
            transform.rotation = Quaternion.identity;

            SnakeHead snakeHead = Instantiate(_snakeHeadPrefab, transform.position, Quaternion.identity);
            snakeHead.transform.SetParent(transform);

            Transform snakeTail = Instantiate(_snakeTailPrefab, transform.position, Quaternion.identity);
            snakeTail.transform.SetParent(transform);

            _snakeMovement.Init(snakesManager, this, snakeHead, snakeTail);
            _snakeMovement.AddToTargetLength(20);// GetLength());
        }

        private void Update()
        {
            _snakeMovement.UpdateBody();
        }

        public void HitSnake(Snake hitSnake, Transform hitPart)
        {
            //List<Vector3> cutPartPositions = hitSnake._snakeMovement.CutAt(hitPart);
            //PickupManager.SpawnLove(cutPartPositions);
        }

        public void HitPickup(Pickup pickup)
        {
            pickup.SetUsed();

            switch (pickup.Power)
            {
                // TODO(~): love is 1 growth, fruit is partial growth (0.3, 0.5, etc) and only grow at whole # accumulation
                case Pickup.POWER.GROW: _snakeMovement.AddToTargetLength(1); break;
                case Pickup.POWER.BLAST_MAGNET: break;
                case Pickup.POWER.FIREBALL: break;
                case Pickup.POWER.TEMP_IMMUNITY: break;
            }
        }

        public void Grow()
        {
            if (!_snakeMovement.TryActivatePart())
            { 
                Transform snakePart = Instantiate(_snakePartPrefab, _snakeMovement.HeadPosition, Quaternion.identity);
                snakePart.transform.SetParent(transform);
                _snakeMovement.AddPart(snakePart);
            }
        }
    }
}
