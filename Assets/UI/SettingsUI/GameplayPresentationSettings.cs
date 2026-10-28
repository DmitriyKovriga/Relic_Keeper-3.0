using System;
using UnityEngine;

public static class GameplayPresentationSettings
{
    public const string HudScaleKey = "presentation_hud_scale";
    public const string HudOpacityKey = "presentation_hud_opacity";
    public const string PlayerAttackVfxOpacityKey = "presentation_player_attack_vfx_opacity";
    public const string NearbyServiceNamesKey = "presentation_nearby_service_names";
    public const string CombatEnemyBarsKey = "presentation_combat_enemy_bars";
    public const string RecentDummyDpsKey = "presentation_recent_dummy_dps";
    public const string CameraImpulseStrengthKey = "presentation_camera_impulse_strength";

    public static event Action Changed;

    public static float HudScale => NormalizeHudScale(PlayerPrefs.GetFloat(HudScaleKey, 0.75f));
    public static float HudOpacity => Mathf.Clamp(PlayerPrefs.GetFloat(HudOpacityKey, 1f), 0.2f, 1f);
    public static float PlayerAttackVfxOpacity => Mathf.Clamp(PlayerPrefs.GetFloat(PlayerAttackVfxOpacityKey, 1f), 0.1f, 1f);
    public static bool NearbyServiceNames => PlayerPrefs.GetInt(NearbyServiceNamesKey, 1) != 0;
    public static bool CombatEnemyBars => PlayerPrefs.GetInt(CombatEnemyBarsKey, 1) != 0;
    public static bool RecentDummyDps => PlayerPrefs.GetInt(RecentDummyDpsKey, 1) != 0;
    public static float CameraImpulseStrength => NormalizeCameraImpulse(PlayerPrefs.GetFloat(CameraImpulseStrengthKey, .5f));

    public static void SetCameraImpulseStrength(float value) => Save(CameraImpulseStrengthKey, NormalizeCameraImpulse(value));

    private static float NormalizeCameraImpulse(float value) => float.IsNaN(value) || float.IsInfinity(value) ? .5f : Mathf.Clamp01(value);

    public static void SetNearbyServiceNames(bool value) => SaveChoice(NearbyServiceNamesKey, value);
    public static void SetCombatEnemyBars(bool value) => SaveChoice(CombatEnemyBarsKey, value);
    public static void SetRecentDummyDps(bool value) => SaveChoice(RecentDummyDpsKey, value);

    private static void SaveChoice(string key, bool value)
    {
        int stored = value ? 1 : 0;
        if (PlayerPrefs.GetInt(key, 1) == stored) return;
        PlayerPrefs.SetInt(key, stored); PlayerPrefs.Save(); Changed?.Invoke();
    }

    public static void SetHudScale(float value)
    {
        Save(HudScaleKey, NormalizeHudScale(value));
    }

    public static void SetHudOpacity(float value)
    {
        Save(HudOpacityKey, Mathf.Clamp(value, 0.2f, 1f));
    }

    public static void SetPlayerAttackVfxOpacity(float value)
    {
        Save(PlayerAttackVfxOpacityKey, Mathf.Clamp(value, 0.1f, 1f));
    }

    public static int GetHudScaleIndex()
    {
        float scale = HudScale;
        if (scale <= 0.625f) return 2;
        if (scale <= 0.875f) return 1;
        return 0;
    }

    public static float GetHudScaleForIndex(int index)
    {
        return index switch
        {
            1 => 0.75f,
            2 => 0.50f,
            _ => 1f
        };
    }

    private static float NormalizeHudScale(float value)
    {
        if (value <= 0.625f) return 0.50f;
        if (value <= 0.875f) return 0.75f;
        return 1f;
    }

    private static void Save(string key, float value)
    {
        if (Mathf.Approximately(PlayerPrefs.GetFloat(key, float.NaN), value))
            return;

        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
