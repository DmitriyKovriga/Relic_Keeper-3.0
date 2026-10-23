using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Visuals
{
    /// <summary>PixelLab frames fitted between the same endpoints as the beam hitbox.</summary>
    public sealed class AuthoredBeamVisual : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _referenceLengthPixels = 160;
        [SerializeField] private float _referenceHitWidth = .35f;
        private float _lifetime, _age;
        private SpriteRenderer _renderer;

        public static AuthoredBeamVisual Spawn(GameObject prefab, Vector2 start, Vector2 end, float width, float lifetime)
        {
            if (prefab == null || !Application.isPlaying) return null;
            var instance = Instantiate(prefab, (start + end) * .5f, Quaternion.identity);
            var visual = instance.GetComponent<AuthoredBeamVisual>();
            if (visual == null) { Destroy(instance); return null; }
            visual._lifetime = Mathf.Max(.01f, lifetime);
            instance.transform.localScale = new Vector3(
                Mathf.Sign(end.x - start.x) * Vector2.Distance(start, end) / (Mathf.Max(1, visual._referenceLengthPixels) / 24),
                width / Mathf.Max(.05f, visual._referenceHitWidth), 1);
            WorldRenderSorting.ConfigureAutoSorter(instance, RenderDepthCategory.HeroAttackVfx, start.y, 2);
            visual.Sample(0);
            return visual;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _lifetime) { Destroy(gameObject); return; }
            Sample(_age / _lifetime);
        }

        public void Sample(float progress)
        {
            if (_frames == null || _frames.Length == 0) return;
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _renderer.sprite = _frames[Mathf.Clamp(Mathf.FloorToInt(progress * _frames.Length), 0, _frames.Length - 1)];
            _renderer.color = PlayerAttackVfxOpacity.MultiplyAlpha(Color.white);
        }
    }
}
