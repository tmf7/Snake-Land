using UnityEngine;

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
        public Vector3 HeadPosition => _snakeMovement.HeadPosition;

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
            //_snakeMovement.AddToTargetLength(20);
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
