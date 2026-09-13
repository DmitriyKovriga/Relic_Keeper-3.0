using UnityEngine;

namespace Scripts.Skills.Visuals
{
    public static class PlayerAttackVfxOpacity
    {
        public static float Multiplier => GameplayPresentationSettings.PlayerAttackVfxOpacity;

        public static void ApplyOnce(GameObject root)
        {
            if (root == null)
                return;

            float multiplier = Multiplier;
            if (Mathf.Approximately(multiplier, 1f))
                return;

            SpriteRenderer[] sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                Color color = sprites[i].color;
                color.a *= multiplier;
                sprites[i].color = color;
            }

            LineRenderer[] lines = root.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null) continue;
                lines[i].startColor = MultiplyAlpha(lines[i].startColor, multiplier);
                lines[i].endColor = MultiplyAlpha(lines[i].endColor, multiplier);
            }

            TrailRenderer[] trails = root.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] == null) continue;
                trails[i].startColor = MultiplyAlpha(trails[i].startColor, multiplier);
                trails[i].endColor = MultiplyAlpha(trails[i].endColor, multiplier);
            }

            ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] == null) continue;
                var main = particles[i].main;
                main.startColor = ScaleGradient(main.startColor, multiplier);
            }
        }

        public static Color MultiplyAlpha(Color color)
        {
            return MultiplyAlpha(color, Multiplier);
        }

        private static Color MultiplyAlpha(Color color, float multiplier)
        {
            color.a = Mathf.Clamp01(color.a * multiplier);
            return color;
        }

        private static ParticleSystem.MinMaxGradient ScaleGradient(
            ParticleSystem.MinMaxGradient source,
            float multiplier)
        {
            ParticleSystem.MinMaxGradient result;
            switch (source.mode)
            {
                case ParticleSystemGradientMode.TwoColors:
                    result = new ParticleSystem.MinMaxGradient(
                        MultiplyAlpha(source.colorMin, multiplier),
                        MultiplyAlpha(source.colorMax, multiplier));
                    break;
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                    result = new ParticleSystem.MinMaxGradient(ScaleGradient(source.gradient, multiplier));
                    result.mode = source.mode;
                    break;
                case ParticleSystemGradientMode.TwoGradients:
                    result = new ParticleSystem.MinMaxGradient(
                        ScaleGradient(source.gradientMin, multiplier),
                        ScaleGradient(source.gradientMax, multiplier));
                    break;
                default:
                    result = new ParticleSystem.MinMaxGradient(MultiplyAlpha(source.color, multiplier));
                    break;
            }

            return result;
        }

        private static Gradient ScaleGradient(Gradient source, float multiplier)
        {
            if (source == null)
                return null;

            var result = new Gradient();
            GradientAlphaKey[] alphaKeys = source.alphaKeys;
            for (int i = 0; i < alphaKeys.Length; i++)
                alphaKeys[i].alpha = Mathf.Clamp01(alphaKeys[i].alpha * multiplier);
            result.SetKeys(source.colorKeys, alphaKeys);
            return result;
        }
    }
}
