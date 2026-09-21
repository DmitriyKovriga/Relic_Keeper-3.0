using UnityEngine;

namespace Scripts.Visuals.Atmosphere
{
    public enum AtmosphereQuality { Low, Medium, High }

    [CreateAssetMenu(menuName = "Relic Keeper/Visuals/Atmosphere Profile")]
    public sealed class AtmosphereProfileSO : ScriptableObject
    {
        [Header("World — neutral by default")]
        [Range(0f, 1f)] public float intensity = 1f;
        [Range(0f, 1f)] public float darkness;
        [Range(0f, 1f)] public float fogOpacity;
        public Color fogTint = new Color(0.15f, 0.19f, 0.24f, 1f);
        [Range(0f, 1f)] public float vignette;

        [Header("Player reveal — world units")]
        [Min(0f)] public float revealRadius = 3f;
        [Min(0.001f)] public float revealSoftness = 2f;
        [Range(0f, 1f)] public float revealIntensity = 1f;

        [Header("World-anchored fog")]
        [Range(0f, 1f)] public float noiseAmount = 0.2f;
        [Min(0.001f)] public float noiseScale = 0.4f;
        public Vector2 driftSpeed = new Vector2(0.025f, 0.01f);
        [Range(0f, 0.5f)] public float pulseAmount;
        [Min(0f)] public float pulseFrequency = 0.5f;

        [Header("Layered mist — depth behind gameplay")]
        [Range(0f, 1f)] public float backgroundMist;
        public Color backgroundTint = new Color(0.25f, 0.4f, 0.43f, 1f);
        [Tooltip("0 = uniform haze; 1 = separated drifting wisps with clear gaps.")]
        [Range(0f, 1f)] public float wispStrength;
        [Range(0f, 1f)] public float shafts;
        [Header("Player lantern / airborne dust")]
        [Range(0f, 1f)] public float lantern;
        public Color lanternTint = new Color(1f, 0.6f, 0.24f, 1f);
        [Min(0.1f)] public float lanternRadius = 2.8f;
        [Range(0f, 1f)] public float motes;

        [Header("Pixel grid / quality")]
        [Tooltip("Match the sprite PPU and Pixel Perfect Camera. This project uses 24.")]
        [Min(1f)] public float pixelsPerUnit = 24f;
        public Vector2Int referenceResolution = new Vector2Int(480, 270);
        public AtmosphereQuality quality = AtmosphereQuality.Medium;
        [Tooltip("Static ordered dither; never a random pattern per frame.")]
        [Range(0f, 1f)] public float dither = 0.35f;

        public bool HasVisibleEffect => intensity > 0f && (darkness > 0f || fogOpacity > 0f || vignette > 0f || lantern > 0f || motes > 0f);
        public bool HasDepthLayer => intensity > 0f && (backgroundMist > 0f || shafts > 0f);

        public void Sanitize()
        {
            intensity = Mathf.Clamp01(intensity);
            darkness = Mathf.Clamp01(darkness);
            fogOpacity = Mathf.Clamp01(fogOpacity);
            vignette = Mathf.Clamp01(vignette);
            revealRadius = Mathf.Max(0f, revealRadius);
            revealSoftness = Mathf.Max(0.001f, revealSoftness);
            revealIntensity = Mathf.Clamp01(revealIntensity);
            noiseAmount = Mathf.Clamp01(noiseAmount);
            noiseScale = Mathf.Max(0.001f, noiseScale);
            pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
            referenceResolution = new Vector2Int(Mathf.Max(1, referenceResolution.x), Mathf.Max(1, referenceResolution.y));
            pulseAmount = Mathf.Clamp(pulseAmount, 0f, 0.5f);
            pulseFrequency = Mathf.Max(0f, pulseFrequency);
            dither = Mathf.Clamp01(dither);
            backgroundMist = Mathf.Clamp01(backgroundMist);
            wispStrength = Mathf.Clamp01(wispStrength);
            shafts = Mathf.Clamp01(shafts);
            lantern = Mathf.Clamp01(lantern);
            lanternRadius = Mathf.Max(0.1f, lanternRadius);
            motes = Mathf.Clamp01(motes);
        }

        private void OnValidate() => Sanitize();

        // Mirrors the shader: radius is the fully clear core, softness is the outer transition.
        public static float EvaluateReveal(Vector2 world, Vector2 player, float radius, float softness, float strength)
        {
            float t = Mathf.Clamp01((Vector2.Distance(world, player) - Mathf.Max(0f, radius)) / Mathf.Max(0.001f, softness));
            return (1f - t * t * (3f - 2f * t)) * Mathf.Clamp01(strength);
        }

        public static Vector2 SnapToPixel(Vector2 world, float ppu)
        {
            ppu = Mathf.Max(1f, ppu);
            return new Vector2(Mathf.Floor(world.x * ppu) + 0.5f, Mathf.Floor(world.y * ppu) + 0.5f) / ppu;
        }
    }
}
