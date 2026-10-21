using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills.Visuals
{
    public sealed class NeedlePrickVisual : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _framesPerSecond = 36;
        private SpriteRenderer _renderer;
        private float _age;
        private float _playback = 1;
        public static void Spawn(GameObject prefab, Vector2 point, float facing, float reach, float playback)
        {
            if (prefab == null || !Application.isPlaying) return;
            var instance = Instantiate(prefab, point, Quaternion.identity);
            instance.transform.localScale = new Vector3(Mathf.Sign(facing) * reach / (26f / 24), 1, 1);
            var visual = instance.GetComponent<NeedlePrickVisual>();
            if (visual != null) visual._playback = Mathf.Max(.05f, playback);
            WorldRenderSorting.ConfigureAutoSorter(instance, RenderDepthCategory.HeroAttackVfx, point.y);
        }
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
