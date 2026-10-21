using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Visuals
{
    public sealed class TectonicPressVisual : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _framesPerSecond = 24;
        [SerializeField] private Vector2 _referenceSize;
        private SpriteRenderer _renderer;
        private float _age;

        public static void Spawn(GameObject prefab, Vector2 point, Vector2 size, float facing)
        {
            if (prefab == null || !Application.isPlaying) return;
            var instance = Instantiate(prefab, point, Quaternion.identity);
            var visual = instance.GetComponent<TectonicPressVisual>();
            if (visual == null) { Destroy(instance); return; }
            visual._renderer = instance.GetComponent<SpriteRenderer>();
            var sprite = visual._renderer.sprite;
            if (sprite != null)
            {
                Vector2 reference = visual._referenceSize.x > 0 && visual._referenceSize.y > 0 ? visual._referenceSize : (Vector2)sprite.bounds.size;
                instance.transform.localScale = new Vector3(size.x / reference.x * Mathf.Sign(facing), size.y / reference.y, 1);
            }
            WorldRenderSorting.ConfigureAutoSorter(instance, RenderDepthCategory.HeroAttackVfx, point.y);
            visual._renderer.color = PlayerAttackVfxOpacity.MultiplyAlpha(Color.white);
        }
        private void Update() => Sample(_age + Time.deltaTime);
        public void Sample(float age)
        {
            _age = Mathf.Max(0, age);
            int frame = Mathf.FloorToInt(_age * _framesPerSecond);
            if (_frames == null || frame >= _frames.Length) { Destroy(gameObject); return; }
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _renderer.sprite = _frames[frame];
        }
    }
}
