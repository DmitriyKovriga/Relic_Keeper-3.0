using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Visuals
{
    public sealed class RetreatingThrustVisual : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _framesPerSecond = 30;
        private SpriteRenderer _renderer;
        private float _age;
        public static void Spawn(GameObject prefab, Vector2 point, float facing, float lengthScale = 1)
        {
            if (prefab == null || !Application.isPlaying) return;
            var instance = Instantiate(prefab, point, Quaternion.identity);
            instance.transform.localScale = new Vector3(Mathf.Sign(facing) * Mathf.Max(.01f, lengthScale), 1, 1);
            WorldRenderSorting.ConfigureAutoSorter(instance, RenderDepthCategory.HeroAttackVfx, point.y);
        }
        private void Awake() => _renderer = GetComponent<SpriteRenderer>();
        private void Update() => Sample(_age + Time.deltaTime);
        public void Sample(float age)
        {
            _age = Mathf.Max(0, age);
            int frame = Mathf.FloorToInt(_age * _framesPerSecond);
            if (_frames == null || frame >= _frames.Length) { Destroy(gameObject); return; }
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _renderer.sprite = _frames[frame];
            _renderer.color = PlayerAttackVfxOpacity.MultiplyAlpha(Color.white);
        }
    }
}
