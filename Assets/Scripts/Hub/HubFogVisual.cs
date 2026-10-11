using UnityEngine;

namespace Scripts.Hub
{
    public sealed class HubFogVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _current;
        [SerializeField] private SpriteRenderer _next;
        [SerializeField] private Sprite[] _frames;
        [SerializeField, Min(0.01f)] private float _frameDuration = 1.44f;
        [SerializeField, Range(0f, 1f)] private float _opacity = 0.4f;
        private float _elapsed;

        public void Configure(SpriteRenderer current, SpriteRenderer next, Sprite[] frames, float frameDuration)
        {
            _current = current;
            _next = next;
            _frames = frames;
            _frameDuration = Mathf.Max(0.01f, frameDuration);
            Sample(0f);
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            Sample(0f);
        }

        private void LateUpdate()
        {
            _elapsed += Time.deltaTime;
            Sample(_elapsed);
        }

        public void Sample(float elapsed)
        {
            if (_current == null || _next == null || _frames == null || _frames.Length == 0)
                return;

            float frame = Mathf.Repeat(Mathf.Max(0f, elapsed) / _frameDuration, _frames.Length);
            int index = Mathf.FloorToInt(frame);
            float blend = Mathf.SmoothStep(0f, 1f, frame - index);
            _current.sprite = _frames[index];
            _next.sprite = _frames[(index + 1) % _frames.Length];
            _current.color = new Color(1f, 1f, 1f, _opacity * (1f - blend));
            _next.color = new Color(1f, 1f, 1f, _opacity * blend);
        }
    }
}
