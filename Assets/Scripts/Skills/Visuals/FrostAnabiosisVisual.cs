using UnityEngine;
using Scripts.Visuals;

namespace Scripts.Skills.Visuals
{
    public sealed class FrostAnabiosisVisual : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Material _material;
        private Sprite[] _shards;
        private SpriteRenderer[] _pieces;
        private Vector3[] _pieceOrigins;
        private Vector3 _baseScale;
        private bool _releasing;
        private float _releaseTime;

        public static FrostAnabiosisVisual Create(Transform owner, Collider2D body)
        {
            var sprite = Resources.Load<Sprite>("Skills/Shields/FrostAnabiosisCrystal");
            if (sprite == null) return null;
            var host = new GameObject("Frost Anabiosis crystal");
            host.transform.SetParent(owner, false);
            host.transform.position = body != null ? body.bounds.center : owner.position;
            var visual = host.AddComponent<FrostAnabiosisVisual>();
            visual._renderer = host.AddComponent<SpriteRenderer>();
            visual._renderer.sprite = sprite;
            // Own the material: sprite-specific textures must not leak through URP batching.
            visual._material = new Material(Resources.Load<Material>("Skills/1HWeapon/Wand/ElectricBeam"));
            visual._material.mainTexture = sprite.texture;
            visual._renderer.sharedMaterial = visual._material;
            visual._renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.HeroAttackVfx);
            visual._renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.HeroAttackVfx, host.transform.position.y, 3);
            float height = Mathf.Max(1.7f, body != null ? body.bounds.size.y * 1.4f : 2);
            if (body != null) host.transform.position = new Vector3(body.bounds.center.x, body.bounds.min.y + height * .5f, owner.position.z);
            visual._baseScale = Vector3.one * (height / sprite.bounds.size.y);
            // Cover broad characters too, retaining a tall crystal silhouette.
            if (body != null) visual._baseScale.x = Mathf.Max(visual._baseScale.x, body.bounds.size.x * 1.65f / sprite.bounds.size.x);
            visual.Sample(0);
            return visual;
        }

        public void Sample(float progress)
        {
            if (_releasing || _renderer == null) return;
            float appear = Mathf.Clamp01(progress * 35);
            transform.localScale = _baseScale * Mathf.Lerp(.88f, 1, appear);
            float pulse = Mathf.Sin(progress * Mathf.PI * 10);
            _renderer.color = new Color(.9f, 1, 1, (.68f + .07f * pulse) * PlayerAttackVfxOpacity.Multiplier);
        }

        public void Release()
        {
            if (_releasing) return;
            _releasing = true;
            transform.SetParent(null, true);
            // Separate sections fly apart rather than fading the same silhouette in place.
            _shards = new Sprite[6]; _pieces = new SpriteRenderer[6]; _pieceOrigins = new Vector3[6];
            var sprite = _renderer.sprite; Rect rect = sprite.rect;
            float w = rect.width / 2, h = rect.height / 3;
            for (int i = 0; i < 6; i++)
            {
                Rect piece = new Rect(rect.x + i % 2 * w, rect.y + i / 2 * h, w, h);
                _shards[i] = Sprite.Create(sprite.texture, piece, Vector2.one * .5f, sprite.pixelsPerUnit);
                var child = new GameObject("Melting ice shard"); child.transform.SetParent(transform, false);
                var renderer = child.AddComponent<SpriteRenderer>(); _pieces[i] = renderer;
                renderer.sprite = _shards[i]; renderer.sharedMaterial = _material;
                renderer.sortingLayerID = _renderer.sortingLayerID; renderer.sortingOrder = _renderer.sortingOrder;
                child.transform.localPosition = new Vector3((i % 2 - .5f) * w, (i / 2 - 1) * h) / sprite.pixelsPerUnit;
                _pieceOrigins[i] = child.transform.localPosition;
            }
            _renderer.enabled = false;
        }

        private void Update()
        {
            if (!_releasing) return;
            _releaseTime += Time.deltaTime;
            float t = Mathf.Clamp01(_releaseTime / .26f);
            SampleRelease(t);
            if (t >= 1) Destroy(gameObject);
        }

        public void SampleRelease(float progress)
        {
            if (!_releasing) return;
            float t = Mathf.Clamp01(progress);
            for (int i = 0; i < _pieces.Length; i++)
            {
                var piece = _pieces[i];
                float direction = i % 2 == 0 ? -1 : 1;
                piece.transform.localPosition = _pieceOrigins[i] + new Vector3(direction * .55f * t, (.35f + i / 2 * .2f) * t - .7f * t * t);
                piece.transform.localRotation = Quaternion.Euler(0, 0, direction * t * (15 + i * 4));
                piece.color = new Color(.9f, 1, 1, (1 - t) * .7f * PlayerAttackVfxOpacity.Multiplier);
                piece.transform.localScale = Vector3.one * (1 - t * .35f);
            }
        }

        private void OnDestroy()
        {
            if (_shards != null) foreach (var shard in _shards) Dispose(shard);
            Dispose(_material);
        }

        private static void Dispose(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
