using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Visuals
{
    public sealed class NeedlePrickVisual : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _framesPerSecond = 42;
        [SerializeField] private float _referenceWidthPixels = 30;
        [SerializeField] private int _sortOffset;
        private SpriteRenderer _renderer;
        private float _age;
        private float _playback = 1;
        public static void Spawn(GameObject prefab, Vector2 point, float facing, float reach, float playback)
        {
            if (prefab == null || !Application.isPlaying) return;
            var instance = Instantiate(prefab, point, Quaternion.identity);
            var visual = instance.GetComponent<NeedlePrickVisual>();
            float reference = visual != null ? Mathf.Max(1, visual._referenceWidthPixels) : 30;
            instance.transform.localScale = new Vector3(Mathf.Sign(facing) * (reach > 0 ? reach / (reference / 24) : 1), 1, 1);
            if (visual != null) visual._playback = Mathf.Max(.05f, playback);
            WorldRenderSorting.ConfigureAutoSorter(instance, RenderDepthCategory.HeroAttackVfx, point.y, visual != null ? visual._sortOffset : 0);
        }
        public static void SpawnContact(GameObject prefab, Vector2 point, float facing, float playback)
            => Spawn(prefab, point, facing, 0, playback);
        private void Update() => Sample(_age + Time.deltaTime * _playback);
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
