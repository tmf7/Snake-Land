using System.Collections.Generic;
using UnityEngine;

namespace Freehill.SnakeLand
{
    [RequireComponent(typeof(Terrain), typeof(TerrainCollider))]
    public class TerrainRandomizer : MonoBehaviour
    {
        [SerializeField] private List<TerrainData> _terrainData = new List<TerrainData>();
        [SerializeField] private List<TerrainLayer> _terrainLayers = new List<TerrainLayer>();

        private Terrain _terrain;
        private TerrainCollider _terrainCollider;

        private void Awake()
        {
            _terrain = GetComponent<Terrain>();
            _terrainCollider = GetComponent<TerrainCollider>();

            var terrainData = _terrainData[Random.Range(0, _terrainLayers.Count)];
            terrainData.terrainLayers = new TerrainLayer[] { _terrainLayers[Random.Range(0, _terrainLayers.Count)] };
            _terrain.terrainData = terrainData;
            _terrainCollider.terrainData = terrainData;
        }
    }
}
