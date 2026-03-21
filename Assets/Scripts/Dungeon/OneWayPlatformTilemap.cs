using UnityEngine;
using UnityEngine.Tilemaps;

namespace Scripts.Dungeon
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tilemap))]
    [RequireComponent(typeof(TilemapRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(TilemapCollider2D))]
    [RequireComponent(typeof(CompositeCollider2D))]
    [RequireComponent(typeof(PlatformEffector2D))]
    public class OneWayPlatformTilemap : MonoBehaviour
    {
        private const string LayerName = "OneWayPlatform";

        [SerializeField] private bool _matchGroundSorting = true;
        [SerializeField] private string _sortingLayerName = "Default";
        [SerializeField] private int _sortingOrder = 0;

        private void Reset()
        {
            ApplySetup();
        }

        private void OnValidate()
        {
            ApplySetup();
        }

        [ContextMenu("Apply One-Way Platform Setup")]
        public void ApplySetup()
        {
            int platformLayer = LayerMask.NameToLayer(LayerName);
            if (platformLayer >= 0)
                gameObject.layer = platformLayer;

            TilemapRenderer tilemapRenderer = GetComponent<TilemapRenderer>();
            if (tilemapRenderer != null)
            {
                if (_matchGroundSorting)
                {
                    tilemapRenderer.sortingLayerName = _sortingLayerName;
                    tilemapRenderer.sortingOrder = _sortingOrder;
                }
            }

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            rb.simulated = true;
            rb.gravityScale = 0f;

            TilemapCollider2D tilemapCollider = GetComponent<TilemapCollider2D>();
            tilemapCollider.isTrigger = false;
            tilemapCollider.usedByEffector = false;
            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

            CompositeCollider2D compositeCollider = GetComponent<CompositeCollider2D>();
            compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
            compositeCollider.generationType = CompositeCollider2D.GenerationType.Synchronous;
            compositeCollider.isTrigger = false;
            compositeCollider.usedByEffector = true;

            PlatformEffector2D effector = GetComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            effector.useColliderMask = false;
            effector.rotationalOffset = 0f;
            effector.surfaceArc = 140f;
            effector.sideArc = 0f;
        }
    }
}
