using System;
using Scripts.GameplayEvents;
using UnityEngine;

namespace Scripts.Visuals
{
    public enum DefenseFeedbackKind { Parry, Evade, MysticShield }

    /// <summary>Separate bounded lanes: a late duplicate never extends an earlier response.</summary>
    public sealed class DefenseFeedbackPulse
    {
        private readonly float[] _start = new float[3], _duration = new float[3], _next = new float[3];
        private readonly bool[] _used = new bool[3];

        public static bool Classify(GameplayEventContext context, GameObject player, out DefenseFeedbackKind kind)
        {
            kind = default;
            if (player == null || context == null || context.Target != player) return false;
            if (context.Type == GameplayEventType.MysticShieldConsumed)
            { kind = DefenseFeedbackKind.MysticShield; return context.Count > 0; }
            if (context.Damage == null || !context.Damage.IsDirectHit || context.Damage.TotalDamage <= 0 ||
                float.IsNaN(context.Damage.TotalDamage) || float.IsInfinity(context.Damage.TotalDamage)) return false;
            if (context.Type == GameplayEventType.Parried) { kind = DefenseFeedbackKind.Parry; return true; }
            if (context.Type == GameplayEventType.Evaded) { kind = DefenseFeedbackKind.Evade; return true; }
            return false;
        }

        public bool Trigger(DefenseFeedbackKind kind, float now, float duration, float interval)
        {
            int index = (int)kind;
            if (index < 0 || index >= 3 || !Finite(now) || !Finite(duration) || !Finite(interval) || duration <= 0 ||
                _used[index] && now < _next[index]) return false;
            _used[index] = true; _start[index] = now; _duration[index] = Mathf.Clamp(duration, .05f, .25f);
            _next[index] = now + Mathf.Max(.2f, Mathf.Max(interval, _duration[index] + .05f));
            return true;
        }

        public bool Sample(DefenseFeedbackKind kind, float now, out float progress)
        {
            int index = (int)kind; progress = 0;
            if (index < 0 || index >= 3 || !_used[index] || !Finite(now) || now < _start[index] || now >= _start[index] + _duration[index]) return false;
            progress = (now - _start[index]) / _duration[index]; return true;
        }
        public void Clear() { Array.Clear(_used, 0, 3); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Player-owned reusable accents. No changes to damage, movement, time or status material channels.</summary>
    [DefaultExecutionOrder(340), DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class PlayerDefenseFeedback : MonoBehaviour
    {
        [SerializeField] private CombatPoseProfile _profile;
        private readonly DefenseFeedbackPulse _pulse = new DefenseFeedbackPulse();
        private readonly SpriteRenderer[] _renderers = new SpriteRenderer[3];
        private readonly float[] _side = new float[3];
        private static Sprite[][] s_frames;
        private static Material s_material;
        private static readonly string[] Names = { "relic-keeper-parry-success", "relic-keeper-evasion-shimmer", "relic-keeper-mystic-charge-pulse" };
        private static readonly float[] Fps = { 24, 30, 30 };
        private PlayerStats _stats;
        private PlayerMovementVisual _visual;
        private SpriteRenderer _source;
        private CharacterDataSO _character;
        private Vector3 _lastPosition;
        public int AcceptedResponses { get; private set; }
        public int ActiveResponses { get; private set; }

        public static Sprite[] Frames(DefenseFeedbackKind kind)
        {
            s_frames ??= new Sprite[3][];
            int index = (int)kind;
            if (s_frames[index] == null)
            {
                var sprites = Resources.LoadAll<Sprite>("Visuals/DefenseFeedback/" + Names[index]);
                Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name)); s_frames[index] = sprites;
            }
            return s_frames[index];
        }

        private void OnEnable()
        {
            _stats = GetComponent<PlayerStats>(); _visual = GetComponent<PlayerMovementVisual>(); _source = GetComponent<SpriteRenderer>();
            if (_profile == null) _profile = CombatPoseProfile.LoadDefault();
            _lastPosition = transform.position;
            GameplayEventBus.EventRaised += React;
        }
        private bool Ready => isActiveAndEnabled && _profile != null && _profile.EnableDefenseFeedback && _profile.DefenseFeedbackOpacity > 0 &&
            _stats != null && _stats.isActiveAndEnabled && _stats.Health != null && _stats.Health.Current > 0 &&
            _source != null && _source.enabled && _visual != null && _visual.isActiveAndEnabled;

        private void React(GameplayEventContext context)
        {
            if (!Ready || GamePauseService.IsPaused || !DefenseFeedbackPulse.Classify(context, gameObject, out var kind)) return;
            CheckScope();
            var frames = Frames(kind); if (frames.Length == 0) return;
            if (!_pulse.Trigger(kind, Time.time, frames.Length / Fps[(int)kind], _profile.DefenseFeedbackInterval)) return;
            _character = _stats.CurrentCharacterData;
            _side[(int)kind] = context.Source != null && context.Source != gameObject
                ? Mathf.Sign(context.Source.transform.position.x - transform.position.x) : Mathf.Sign(transform.lossyScale.x);
            EnsureRenderer(kind); AcceptedResponses++;
        }

        private void EnsureRenderer(DefenseFeedbackKind kind)
        {
            int index = (int)kind; if (_renderers[index] != null) return;
            var host = new GameObject("Defense " + kind); host.layer = gameObject.layer; host.transform.SetParent(transform, false);
            var renderer = host.AddComponent<SpriteRenderer>();
            if (s_material == null) s_material = Resources.Load<Material>("Visuals/DefenseFeedback/DefenseAccent");
            renderer.sharedMaterial = s_material != null ? s_material : _source.sharedMaterial;
            renderer.enabled = false; _renderers[index] = renderer;
            WorldRenderSorting.ConfigureAutoSorter(host, RenderDepthCategory.PlayerOverlay, transform.position.y, localOffset: 8);
        }

        private void CheckScope()
        {
            if (_stats.CurrentCharacterData != _character || (transform.position - _lastPosition).sqrMagnitude > 16f) Clear();
            _lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (!Ready) { Clear(); return; }
            CheckScope(); ActiveResponses = 0;
            var display = _visual.DisplayRenderer;
            if (display == null || !display.enabled) { Clear(); return; }
            Bounds body = display.bounds;
            for (int i = 0; i < 3; i++)
            {
                var renderer = _renderers[i]; if (renderer == null) continue;
                bool active = _pulse.Sample((DefenseFeedbackKind)i, Time.time, out float progress);
                renderer.enabled = active;
                if (!active) continue;
                var frames = Frames((DefenseFeedbackKind)i);
                renderer.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(progress * frames.Length))];
                renderer.color = new Color(1, 1, 1, Mathf.Clamp01(_profile.DefenseFeedbackOpacity));
                renderer.transform.position = body.center + (i == 0 ? new Vector3(_side[i] * (body.extents.x + .1f), .05f, 0) : Vector3.zero);
                ActiveResponses++;
            }
        }

        public void Clear()
        {
            _pulse.Clear(); ActiveResponses = 0;
            foreach (var renderer in _renderers) if (renderer != null) renderer.enabled = false;
            _character = _stats != null ? _stats.CurrentCharacterData : null;
        }
        private void OnDisable() { GameplayEventBus.EventRaised -= React; Clear(); }
        private void OnDestroy()
        {
            foreach (var renderer in _renderers)
                if (renderer != null) { if (Application.isPlaying) Destroy(renderer.gameObject); else DestroyImmediate(renderer.gameObject); }
        }
    }
}
