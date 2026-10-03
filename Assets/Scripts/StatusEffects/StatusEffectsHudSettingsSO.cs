using UnityEngine;

namespace Scripts.StatusEffects
{
    [CreateAssetMenu(menuName = "RPG/Status Effects HUD Settings", fileName = "StatusEffectsHudSettings")]
    public sealed class StatusEffectsHudSettingsSO : ScriptableObject
    {
        [Header("HUD Layout")]
        [Min(1f)] public float IconSizePixels = 16f;
        [Min(0f)] public float IconSpacingPixels = 1f;
        [Min(1)] public int IconColumns = 4;
        [Tooltip("Шейдер свечения персонажа, пока цветной бафф активен.")]
        public Material AuraSpriteMaterial;

#if UNITY_EDITOR
        private void OnValidate()
        {
            IconSizePixels = Mathf.Max(1f, Mathf.Round(IconSizePixels));
            IconSpacingPixels = Mathf.Max(0f, Mathf.Round(IconSpacingPixels));
            IconColumns = Mathf.Max(1, IconColumns);
        }
#endif
    }
}
