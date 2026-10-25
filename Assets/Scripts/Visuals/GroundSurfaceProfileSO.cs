using UnityEngine;

namespace Scripts.Visuals
{
    [CreateAssetMenu(menuName = "Relic Keeper/Visuals/Ground Surface")]
    public sealed class GroundSurfaceProfileSO : ScriptableObject
    {
        public Sprite[] stepFrames;
        public Sprite[] landingFrames;
        [Header("Sparse footsteps")]
        [Min(0.1f)] public float runStride = 1.6f;
        [Min(0.05f)] public float runInterval = 0.25f;
        [Min(0f)] public float runMinSpeed = 1f;
        [Min(0.1f)] public float stepWidth = 1.4f;
        [Min(0.1f)] public float maxWorldWidth = 2.2f;
        [Min(0.04f)] public float stepDuration = 0.18f;
        [Range(0f, 1f)] public float stepOpacity = 0.7f;
        [Header("Landing — visual response only")]
        [Min(0f)] public float minLandingSpeed = 2f;
        [Min(0.1f)] public float fullLandingSpeed = 14f;
        [Min(0.1f)] public float landingWidth = 3.6f;
        [Min(0.04f)] public float landingDuration = 0.3f;
        [Range(0f, 1f)] public float landingOpacity = 0.85f;
        [Min(0.1f)] public float lightImpactScale = 0.7f;
        [Min(0.1f)] public float fullImpactScale = 1.25f;
        [Min(0.1f)] public float maxImpactScale = 1.5f;
        [Min(1f)] public float heavyScale = 1.15f;
        [Header("Other ground contacts")]
        [Min(0.1f)] public float takeoffWidth = 1.2f;
        [Min(0.04f)] public float takeoffDuration = 0.2f;
        [Min(0.1f)] public float dashWidth = 2.2f;
        [Min(0.04f)] public float dashDuration = 0.3f;

        public readonly struct Landing
        {
            public readonly bool visible;
            public readonly float width, opacity, duration;
            public Landing(float width, float opacity, float duration)
            { visible = true; this.width = width; this.opacity = opacity; this.duration = duration; }
        }
        public Landing EvaluateLanding(float downwardSpeed, bool heavy, float strength = 1)
        {
            if (downwardSpeed < minLandingSpeed || strength <= 0) return default;
            float t = Mathf.InverseLerp(minLandingSpeed, Mathf.Max(minLandingSpeed + 0.01f, fullLandingSpeed), downwardSpeed);
            float scale = Mathf.Min(maxImpactScale, Mathf.Lerp(lightImpactScale, fullImpactScale, t) * (heavy ? heavyScale : 1) * Mathf.Clamp(strength, 0, 2));
            return new Landing(landingWidth * scale, landingOpacity * Mathf.Lerp(0.65f, 1, t), landingDuration);
        }
        public bool ShouldStep(float distance, float elapsed, float speed, float density, bool heavy)
        {
            density = Mathf.Clamp01(density);
            return density > 0 && speed >= runMinSpeed && elapsed >= runInterval &&
                distance >= runStride * (heavy ? heavyScale : 1) / density;
        }
    }
}
