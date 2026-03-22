using UnityEngine;

namespace Scripts.Enemies
{
    public class EnemyDeathDecal : MonoBehaviour
    {
        private SpriteRenderer[] _renderers;
        private float _lifetime;
        private float _fadeDuration;
        private float _age;
        private Color[] _baseColors;

        public void Initialize(float lifetime, float fadeDuration)
        {
            _lifetime = Mathf.Max(0.1f, lifetime);
            _fadeDuration = Mathf.Clamp(fadeDuration, 0f, _lifetime);
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _baseColors[i] = _renderers[i].color;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (_fadeDuration <= 0f || _age < _lifetime - _fadeDuration)
                return;

            float fadeT = 1f - Mathf.InverseLerp(_lifetime - _fadeDuration, _lifetime, _age);
            for (int i = 0; i < _renderers.Length; i++)
            {
                Color color = _baseColors[i];
                color.a *= fadeT;
                _renderers[i].color = color;
            }
        }
    }
}
