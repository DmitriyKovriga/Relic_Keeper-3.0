using System;
using UnityEngine;

namespace Scripts.Visuals
{
    /// <summary>One short echo of the portal's existing animated art. Never scales the portal/collider.</summary>
    public sealed class PortalUnlockVisual : IDisposable
    {
        private readonly SpriteRenderer _source;
        private SpriteRenderer _echo;
        private readonly bool _reduced;
        private float _age;
        public bool Finished => _echo == null;

        public PortalUnlockVisual(SpriteRenderer source)
        {
            _source = source; _reduced = GameplayPresentationSettings.ReducedCombatEffects;
            if (source == null) return;
            var material = Resources.Load<Material>("Visuals/SourceGlow/Halo");
            if (material == null) return;
            var host = new GameObject("PortalUnlockEcho") { hideFlags = HideFlags.DontSave };
            host.transform.SetParent(source.transform, false); host.layer = source.gameObject.layer;
            _echo = host.AddComponent<SpriteRenderer>(); _echo.sharedMaterial = material;
            _echo.sortingLayerID = source.sortingLayerID; _echo.sortingOrder = source.sortingOrder - 1;
            Tick(0);
        }

        public void Tick(float deltaTime)
        {
            if (_echo == null) return;
            _age += Mathf.Max(0, deltaTime);
            float duration = _reduced ? .4f : .65f;
            if (_source == null || !_source.gameObject.activeInHierarchy || _age >= duration) { Dispose(); return; }
            float t = _age / duration;
            _echo.sprite = _source.sprite; _echo.enabled = _source.enabled && _source.sprite != null;
            _echo.transform.localScale = Vector3.one * Mathf.Lerp(1f, _reduced ? 1.22f : 1.5f, 1f - (1f - t) * (1f - t));
            _echo.color = new Color(.52f, .65f, 1f, (_reduced ? .3f : .55f) * (1f - t));
        }

        public void Dispose()
        {
            if (_echo == null) return;
            _echo.enabled = false;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_echo.gameObject);
            else UnityEngine.Object.DestroyImmediate(_echo.gameObject);
            _echo = null;
        }
    }
}
