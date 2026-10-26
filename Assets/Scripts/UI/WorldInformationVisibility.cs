using UnityEngine;

namespace Scripts.UI
{
    // Presentation timing only; this does not change aggro, interaction ranges,
    // damage calculation or the dummy's ten-second measurement window.
    public static class WorldInformationVisibility
    {
        public const float ServiceFullDistance = 2.5f;
        public const float ServiceHideDistance = 4f;
        public const float EnemyHoldSeconds = 4f;
        public const float DummyHoldSeconds = 5f;
        public const float FadeSeconds = 0.35f;

        public static float ProximityOpacity(float distance) =>
            1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ServiceFullDistance, ServiceHideDistance, distance));

        public static float RecentActivityOpacity(float now, float lastActivity, float holdSeconds)
        {
            if (float.IsNegativeInfinity(lastActivity)) return 0;
            return 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((now - lastActivity - holdSeconds) / FadeSeconds));
        }

        public static readonly (string Key, string English, string Russian)[] Translations =
        {
            ("settings.worldInfo.serviceNames", "Service names", "Названия услуг"),
            ("settings.worldInfo.enemyBars", "Enemy health bars", "Здоровье врагов"),
            ("settings.worldInfo.dummyDps", "Training DPS", "Урон манекену"),
            ("settings.worldInfo.nearby", "Nearby", "Рядом"),
            ("settings.worldInfo.combat", "Combat", "В бою"),
            ("settings.worldInfo.recentDamage", "After damage", "После урона"),
            ("settings.worldInfo.always", "Always", "Всегда")
        };

        public static string Text(string key)
        {
            foreach (var text in Translations)
                if (text.Key == key) return RuntimeLocalization.Resolve(text.Key, text.English, text.Russian);
            return key;
        }
    }
}
