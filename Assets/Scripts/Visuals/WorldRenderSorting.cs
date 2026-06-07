using UnityEngine;

namespace Scripts.Visuals
{
    public enum RenderDepthCategory
    {
        Background = 0,
        Environment = 1,
        Enemy = 2,
        EnemyRemains = 3,
        GameplayVfx = 4,
        Player = 5,
        PlayerOverlay = 6,
        HeroAttackVfx = 7
    }

    /// <summary>
    /// Central render stack for world sprites (UI canvases stay separate and on top).
    /// Sorting layer order in TagManager: Background → Default → World → VFX → Hero → SFX.
    /// </summary>
    public static class WorldRenderSorting
    {
        public const string LayerBackground = "Background";
        public const string LayerWorld = "World";
        public const string LayerVfx = "VFX";
        public const string LayerHero = "Hero";

        private static RenderStackSettings s_settings;

        public static RenderStackSettings Settings
        {
            get
            {
                if (s_settings == null)
                    s_settings = LoadSettings();
                return s_settings;
            }
        }

        public static string GetSortingLayer(RenderDepthCategory category) => Settings.GetSortingLayer(category);

        public static int ResolveOrder(RenderDepthCategory category, float worldY, int localOffset = 0)
        {
            return Settings.ResolveOrder(category, worldY, localOffset);
        }

        public static void ApplyToRenderers(
            Transform root,
            RenderDepthCategory category,
            float worldY,
            int localOffset = 0,
            bool respectNestedSorters = true)
        {
            if (root == null)
                return;

            RenderStackSettings settings = Settings;
            string layerName = settings.GetSortingLayer(category);
            int order = settings.ResolveOrder(category, worldY, localOffset);
            int spriteIndex = 0;
            int lineIndex = 0;
            ApplyToRenderersRecursive(root, root, layerName, order, respectNestedSorters, ref spriteIndex, ref lineIndex);
        }

        public static WorldDepthSort ConfigureSorter(
            GameObject root,
            RenderDepthCategory category,
            float worldY,
            int localOffset = 0,
            bool staticAnchor = false)
        {
            if (root == null)
                return null;

            WorldDepthSort sorter = root.GetComponent<WorldDepthSort>();
            if (sorter == null)
                sorter = root.AddComponent<WorldDepthSort>();

            sorter.Configure(category, localOffset, staticAnchor, worldY);
            return sorter;
        }

        public static void ConfigureOneShotRenderer(
            SpriteRenderer renderer,
            RenderDepthCategory category,
            float worldY,
            int localOffset = 0)
        {
            if (renderer == null)
                return;

            renderer.sortingLayerName = GetSortingLayer(category);
            renderer.sortingOrder = ResolveOrder(category, worldY, localOffset);
        }

        public static bool ValidateProjectStack(out string error)
        {
            return Settings.ValidateSortingLayerOrder(out error);
        }

        public static void InvalidateSettingsCache()
        {
            s_settings = null;
        }

        private static RenderStackSettings LoadSettings()
        {
            RenderStackSettings loaded = Resources.Load<RenderStackSettings>(RenderStackSettings.DefaultResourcePath);
            if (loaded != null)
                return loaded;

            return ScriptableObject.CreateInstance<RenderStackSettings>();
        }

        private static void ApplyToRenderersRecursive(
            Transform root,
            Transform current,
            string layerName,
            int baseOrder,
            bool respectNestedSorters,
            ref int spriteIndex,
            ref int lineIndex)
        {
            if (respectNestedSorters && current != root && current.GetComponent<WorldDepthSort>() != null)
                return;

            SpriteRenderer spriteRenderer = current.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingLayerName = layerName;
                spriteRenderer.sortingOrder = baseOrder + spriteIndex;
                spriteIndex++;
            }

            LineRenderer lineRenderer = current.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.sortingLayerName = layerName;
                lineRenderer.sortingOrder = baseOrder + lineIndex;
                lineIndex++;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                if (child != null)
                    ApplyToRenderersRecursive(root, child, layerName, baseOrder, respectNestedSorters, ref spriteIndex, ref lineIndex);
            }
        }
    }
}
