using UnityEngine;

namespace Scripts.Skills.Visuals
{
    // The root sprite remains the stable collision reference; only this child's art animates.
    public sealed class PrismaticProjectileVisual : MonoBehaviour
    {
        [SerializeField] private Sprite _coldSource;
        [SerializeField] private Sprite _lightningSource;
        [SerializeField] private Sprite[] _fireFrames;
        [SerializeField] private Sprite[] _coldFrames;
        [SerializeField] private Sprite[] _lightningFrames;
        [SerializeField] private Sprite[] _prismFrames;
        [SerializeField] private SpriteRenderer _art;
        [SerializeField] private float _framesPerSecond = 20;
        private SpriteRenderer _anchor;
        private Sprite[] _frames;
        private float _age;

        public void Configure(Sprite source, bool prismatic)
        {
            _anchor = GetComponent<SpriteRenderer>();
            _frames = prismatic ? _prismFrames : source == _coldSource ? _coldFrames
                : source == _lightningSource ? _lightningFrames : _fireFrames;
            _age = 0;
            bool ready = _art != null && _frames != null && _frames.Length > 0;
            if (_anchor != null) _anchor.enabled = !ready;
            if (_art != null) _art.enabled = ready;
            Draw();
        }

        private void Update() { _age += Time.deltaTime; Draw(); }
        private void LateUpdate()
        {
            if (_art == null || _anchor == null) return;
            _art.sortingLayerID = _anchor.sortingLayerID;
            _art.sortingOrder = _anchor.sortingOrder;
            _art.color = _anchor.color;
        }
        private void Draw()
        {
            if (_art != null && _frames != null && _frames.Length > 0)
                _art.sprite = _frames[Mathf.FloorToInt(_age * _framesPerSecond) % _frames.Length];
        }
        private void OnDisable() { _age = 0; _frames = null; }
    }
}
