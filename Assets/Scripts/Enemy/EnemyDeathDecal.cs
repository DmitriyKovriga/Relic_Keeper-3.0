using UnityEngine;

namespace Scripts.Enemies
{
    public class EnemyDeathDecal : MonoBehaviour
    {
        private EnemyDeathRemainsSheet _sheet;
        private SpriteRenderer _renderer;
        private Color _baseColor;
        private float _lifetime;
        private float _fadeDuration;
        private float _age;
        private bool _returned;

        public bool IsActiveEffect => !_returned && isActiveAndEnabled;

        public void Initialize(float lifetime, float fadeDuration, EnemyDeathRemainsSheet sheet)
        {
            _sheet = sheet;
            _lifetime = Mathf.Max(0.1f, lifetime);
            _fadeDuration = Mathf.Clamp(fadeDuration, 0f, _lifetime);
            _age = 0f;
            _returned = false;
            _renderer = GetComponent<SpriteRenderer>();
            _baseColor = _renderer != null ? _renderer.color : Color.white;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _lifetime)
            {
                ReturnToPool();
                return;
            }
            if (_renderer == null || _fadeDuration <= 0f || _age < _lifetime - _fadeDuration)
                return;
            Color color = _baseColor;
            color.a *= 1f - Mathf.InverseLerp(_lifetime - _fadeDuration, _lifetime, _age);
            _renderer.color = color;
        }

        public void ForceReturnToPool()
        {
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (_returned)
                return;
            _returned = true;
            if (_sheet == null)
                Destroy(gameObject);
            else
            {
                _sheet.ReleaseDecal();
                _sheet.Pool.ReturnDecal(gameObject);
            }
        }
    }
}
