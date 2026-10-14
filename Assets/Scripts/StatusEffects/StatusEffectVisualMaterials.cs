using UnityEngine;

namespace Scripts.StatusEffects
{
    internal static class StatusEffectVisualMaterials
    {
        // A Resources material keeps its shader in player builds; Shader.Find alone does not.
        internal const string HaloResourcePath = "Visuals/StatusAuraHalo";

        internal static Material LoadHalo()
        {
            Material material = Resources.Load<Material>(HaloResourcePath);
            return material != null && material.shader != null && material.shader.isSupported ? material : null;
        }
    }
}
