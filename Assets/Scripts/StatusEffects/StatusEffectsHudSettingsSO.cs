using UnityEngine;

namespace Scripts.StatusEffects
{
    [CreateAssetMenu(menuName = "RPG/Status Effects HUD Settings", fileName = "StatusEffectsHudSettings")]
    public sealed class StatusEffectsHudSettingsSO : ScriptableObject
    {
        [Header("HUD Layout")]
        [Min(1f)] public float IconSizePixels = 5f;
        [Min(0f)] public float IconSpacingPixels = 0f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            IconSizePixels = Mathf.Max(1f, Mathf.Round(IconSizePixels));
            IconSpacingPixels = Mathf.Max(0f, Mathf.Round(IconSpacingPixels));
        }
#endif
    }
}
