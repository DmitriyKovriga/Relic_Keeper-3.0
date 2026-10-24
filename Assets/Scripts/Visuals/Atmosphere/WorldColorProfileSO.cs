using System;
using UnityEngine;

namespace Scripts.Visuals.Atmosphere
{
    public enum WorldColorRole { Unaffected = 0, Characters = 1, Environment = 2, Background = 3 }

    [Serializable]
    public sealed class WorldColorAdjustment
    {
        [Range(-2f, 2f), Tooltip("Brightness in exposure stops. 0 leaves brightness unchanged.")]
        public float exposure;
        [Range(0f, 2f)] public float contrast = 1f;
        [Range(0f, 2f)] public float saturation = 1f;
        public Color tint = Color.white;

        public Vector4 Parameters => new Vector4(Mathf.Pow(2f, exposure), contrast, saturation, 0f);
        public void Reset() { exposure = 0f; contrast = saturation = 1f; tint = Color.white; }
    }

    [CreateAssetMenu(menuName = "Relic Keeper/Visuals/World Color Profile", fileName = "WorldColorProfile")]
    public sealed class WorldColorProfileSO : ScriptableObject
    {
        public WorldColorAdjustment characters = new WorldColorAdjustment();
        public WorldColorAdjustment environment = new WorldColorAdjustment();
        public WorldColorAdjustment background = new WorldColorAdjustment();
        [Range(0f, 1f), Tooltip("Profile influence on combat effects. 0 preserves their original colors.")]
        public float effectsInfluence = 0.35f;

        public WorldColorAdjustment GetAdjustment(WorldColorRole role)
        {
            switch (role)
            {
                case WorldColorRole.Characters: return characters;
                case WorldColorRole.Environment: return environment;
                case WorldColorRole.Background: return background;
                default: return null;
            }
        }

        [ContextMenu("Reset to neutral")]
        public void ResetToNeutral()
        {
            characters.Reset(); environment.Reset(); background.Reset();
            effectsInfluence = 0.35f;
        }
    }
}
