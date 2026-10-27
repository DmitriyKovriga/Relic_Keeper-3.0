using UnityEngine;
using UnityEngine.Tilemaps;

namespace Scripts.Visuals.Tiles
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Tilemap))]
    [AddComponentMenu("Relic Keeper/Visuals/Mortfall Wall Support")]
    public sealed class MortfallWallSupport : MonoBehaviour
    {
        [SerializeField] private Tilemap _ground;
        [SerializeField] private Tilemap _platforms;
        private Tilemap _wall;
        public Tilemap Ground => _ground;
        public Tilemap Platforms => _platforms;

        public void Configure(Tilemap ground, Tilemap platforms)
        {
            _ground = ground; _platforms = platforms; _wall = GetComponent<Tilemap>();
            _wall.RefreshAllTiles();
        }

        public bool HasSupport(Vector3Int wallCell)
        {
            if (_wall == null) _wall = GetComponent<Tilemap>();
            Vector3 world = _wall.GetCellCenterWorld(wallCell);
            return (_ground != null && _ground.HasTile(_ground.WorldToCell(world))) ||
                (_platforms != null && _platforms.HasTile(_platforms.WorldToCell(world)));
        }

        private void OnEnable()
        {
            _wall = GetComponent<Tilemap>();
            Tilemap.tilemapTileChanged -= TilesChanged; Tilemap.tilemapTileChanged += TilesChanged;
            _wall.RefreshAllTiles();
        }
        private void OnDisable()
        {
            Tilemap.tilemapTileChanged -= TilesChanged;
            if (_wall != null) _wall.RefreshAllTiles();
        }
        private void OnValidate()
        {
            _wall = GetComponent<Tilemap>();
            // OnValidate can run during deserialization; native tile refresh stays in OnEnable/Configure.
        }

        private void TilesChanged(Tilemap map, Tilemap.SyncTile[] changes)
        {
            if (_wall == null || (map != _ground && map != _platforms)) return;
            foreach (var change in changes)
            {
                Vector3Int cell = _wall.WorldToCell(map.GetCellCenterWorld(change.position)) + Vector3Int.up;
                for (int x = -1; x <= 1; x++)
                {
                    var affected = cell + new Vector3Int(x, 0);
                    if (_wall.GetTile(affected) is MortfallWallRuleTile) _wall.RefreshTile(affected);
                }
            }
        }
    }
}
