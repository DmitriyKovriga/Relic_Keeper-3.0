using UnityEngine;

namespace Scripts.Visuals
{
    internal static class RenderStackRuntimeValidator
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ValidateOnLoad()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (WorldRenderSorting.ValidateProjectStack(out string error))
                return;

            Debug.LogError($"[RenderStack] Invalid project sorting layer order: {error}. " +
                           "Fix TagManager or run Tools/Relic Keeper/Visuals/Validate Render Stack in the editor.");
#endif
        }
    }
}
