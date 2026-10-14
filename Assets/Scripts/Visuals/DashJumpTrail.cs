using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Visuals
{
    /// <summary>Short world-space pixel streaks emitted only during the launch of a dash jump.</summary>
    public sealed class DashJumpTrail : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float _emissionDuration = 0.32f;
        [SerializeField, Min(0.01f)] private float _interval = 0.035f;
        [SerializeField, Min(0.01f)] private float _lifetime = 0.18f;
        private PlayerMovement _movement;
        private Collider2D _body;
        private SpriteRenderer _visual;
        private Sprite _pixel;
        private float _until, _next;
        private bool _emitting;
        private readonly List<Segment> _segments = new();
        private struct Segment { public SpriteRenderer Renderer; public float Born; }

        public void Begin()
        {
            _movement = GetComponent<PlayerMovement>();
            _body = GetComponent<Collider2D>();
            _visual = GetComponent<PlayerMovementVisual>()?.DisplayRenderer ?? GetComponent<SpriteRenderer>();
            if (_pixel == null) _pixel = Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*0.5f,24);
            _until = Time.time + _emissionDuration;
            _next = Time.time;
            _emitting = true;
        }

        private void LateUpdate() => Tick(Time.time);

        public void Tick(float now)
        {
            Vector2 centre = _body != null ? (Vector2)_body.bounds.center : (Vector2)transform.position;
            Sample(now, _movement != null ? _movement.CurrentVelocity : Vector2.zero, centre, _movement != null && _movement.IsDashJump);
        }

        public void Sample(float now, Vector2 velocity, Vector2 centre, bool dashJumpActive)
        {
            if (!dashJumpActive || now >= _until) _emitting = false;
            if (_emitting && now >= _next)
            {
                _next = now + _interval;
                if (velocity.sqrMagnitude > 0.01f)
                {
                    Vector2 direction = velocity.normalized;
                    var effect = new GameObject("Dash jump white streak");
                    effect.transform.position = centre + Vector2.up*0.18f - direction*0.32f;
                    effect.transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
                    effect.transform.localScale = new Vector3(4.5f,1f,1f);
                    var renderer = effect.AddComponent<SpriteRenderer>();
                    renderer.sprite = _pixel;
                    renderer.color = new Color(0.96f,0.98f,1f,0.65f);
                    if (_visual != null) { renderer.sortingLayerID=_visual.sortingLayerID; renderer.sortingOrder=_visual.sortingOrder-1; }
                    _segments.Add(new Segment { Renderer=renderer, Born=now });
                }
            }
            for (int i=_segments.Count-1;i>=0;i--)
            {
                var segment=_segments[i];
                if (segment.Renderer == null) { _segments.RemoveAt(i); continue; }
                float age=(now-segment.Born)/_lifetime;
                if (age>=1) { segment.Renderer.enabled=false; Release(segment.Renderer.gameObject); _segments.RemoveAt(i); continue; }
                segment.Renderer.color=new Color(0.96f,0.98f,1f,0.65f*(1-age));
            }
        }

        public void Stop() => _emitting=false;
        public void Clear()
        {
            Stop();
            foreach (var segment in _segments) if (segment.Renderer!=null) Release(segment.Renderer.gameObject);
            _segments.Clear();
            if (_pixel != null) Release(_pixel);
            _pixel = null;
        }
        private void OnDisable() => Clear();
        private void OnDestroy() { if (_pixel!=null) Release(_pixel); }
        private static void Release(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
    }
}
