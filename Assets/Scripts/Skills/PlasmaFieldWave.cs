using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Skills
{
    /// <summary>Tracks two swept radial passes independently, including frames that cross the reversal.</summary>
    public sealed class PlasmaFieldWave
    {
        public readonly struct Sweep
        {
            public readonly float Inner, Outer;
            public readonly bool Active;
            public Sweep(float from, float to, float width)
            { Inner = Mathf.Max(0, Mathf.Min(from, to) - width * .5f); Outer = Mathf.Max(from, to) + width * .5f; Active = true; }

            public bool Intersects(Bounds bounds, Vector2 center)
            {
                Vector2 nearest = bounds.ClosestPoint(center);
                if ((nearest - center).sqrMagnitude > Outer * Outer) return false;
                Vector2 farthest = new Vector2(Mathf.Abs(bounds.center.x - center.x) + bounds.extents.x,
                    Mathf.Abs(bounds.center.y - center.y) + bounds.extents.y);
                return farthest.sqrMagnitude >= Inner * Inner;
            }
        }

        public readonly HashSet<int> OutwardHits = new HashSet<int>();
        public readonly HashSet<int> ReturnHits = new HashSet<int>();
        public float MaxRadius { get; }
        public float Width { get; }
        private float _previous;
        private bool _started;

        public PlasmaFieldWave(float radius, float width)
        { MaxRadius = Mathf.Max(.1f, radius); Width = Mathf.Max(.05f, width); }

        public float RadiusAt(float progress)
        { return progress >= 1 ? 0 : MaxRadius * Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress)); }

        public void Advance(float progress, out Sweep outward, out Sweep inward)
        {
            progress = Mathf.Max(_previous, Mathf.Clamp01(progress));
            outward = default; inward = default;
            if (_previous < .5f && (!_started || progress > _previous))
                outward = new Sweep(RadiusAt(_previous), RadiusAt(Mathf.Min(.5f, progress)), Width);
            if (progress > .5f && progress > _previous)
                inward = new Sweep(RadiusAt(Mathf.Max(.5f, _previous)), RadiusAt(progress), Width);
            _previous = progress; _started = true;
        }
    }
}
