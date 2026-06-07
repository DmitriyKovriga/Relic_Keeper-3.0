using UnityEngine;
using UnityEngine.Rendering;

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
    /// Sorting layer order in TagManager: Background → Default (scene props) → World → VFX → Hero → SFX.
    /// Within dynamic layers, higher sortingOrder draws on top; Y-position drives order for actors.
    /// </summary>
    public static class WorldRenderSorting
    {
        public const string LayerBackground = "Background";
        public const string LayerWorld = "World";
        public const string LayerVfx = "VFX";
        public const string LayerHero = "Hero";

        private const float YSortScale = 100f;
        private const int YSortRange = 7000;

        private const int BackgroundBase = 0;
        private const int EnvironmentBase = 500;
        private const int EnemyBase = 0;
        private const int GameplayVfxBase = 0;
        private const int PlayerBase = 8000;
        private const int PlayerOverlayBase = 8500;
        private const int HeroAttackBase = 9000;

        public static string GetSortingLayer(RenderDepthCategory category)
        {
            switch (category)
            {
                case RenderDepthCategory.Background:
                case RenderDepthCategory.Environment:
                    return LayerBackground;
                case RenderDepthCategory.Enemy:
                case RenderDepthCategory.EnemyRemains:
                    return LayerWorld;
                case RenderDepthCategory.GameplayVfx:
                    return LayerVfx;
                case RenderDepthCategory.Player:
                case RenderDepthCategory.PlayerOverlay:
                case RenderDepthCategory.HeroAttackVfx:
                    return LayerHero;
                default:
                    return SortingLayer.layers.Length > 0 ? SortingLayer.layers[0].name : "Default";
            }
        }

        public static int ResolveOrder(RenderDepthCategory category, float worldY, int localOffset = 0)
        {
            int yOrder = Mathf.RoundToInt(-worldY * YSortScale);

            switch (category)
            {
                case RenderDepthCategory.Background:
                    return BackgroundBase + localOffset;

                case RenderDepthCategory.Environment:
                    return EnvironmentBase + localOffset;

                case RenderDepthCategory.Enemy:
                case RenderDepthCategory.EnemyRemains:
                case RenderDepthCategory.GameplayVfx:
                    return ClampBand(GetCategoryBase(category) + yOrder + localOffset, GetCategoryBase(category));

                case RenderDepthCategory.Player:
                    return ClampBand(PlayerBase + yOrder + localOffset, PlayerBase);

                case RenderDepthCategory.PlayerOverlay:
                    return ClampBand(PlayerOverlayBase + yOrder + localOffset, PlayerOverlayBase);

                case RenderDepthCategory.HeroAttackVfx:
                    return ClampBand(HeroAttackBase + yOrder + localOffset, HeroAttackBase);

                default:
                    return localOffset;
            }
        }

        public static void ApplyToSortingGroup(SortingGroup group, RenderDepthCategory category, float worldY, int localOffset = 0)
        {
            if (group == null)
                return;

            group.sortingLayerName = GetSortingLayer(category);
            group.sortingOrder = ResolveOrder(category, worldY, localOffset);
        }

        public static void ApplyToRenderers(GameObject root, RenderDepthCategory category, float worldY, int localOffset = 0)
        {
            if (root == null)
                return;

            string layerName = GetSortingLayer(category);
            int order = ResolveOrder(category, worldY, localOffset);

            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.sortingLayerName = layerName;
                renderer.sortingOrder = order + i;
            }

            var lineRenderers = root.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lineRenderers.Length; i++)
            {
                LineRenderer lineRenderer = lineRenderers[i];
                if (lineRenderer == null)
                    continue;

                lineRenderer.sortingLayerName = layerName;
                lineRenderer.sortingOrder = order + i;
            }
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

            var sorter = root.GetComponent<WorldDepthSort>();
            if (sorter == null)
                sorter = root.AddComponent<WorldDepthSort>();

            sorter.Configure(category, localOffset, staticAnchor, worldY);
            return sorter;
        }

        private static int GetCategoryBase(RenderDepthCategory category)
        {
            switch (category)
            {
                case RenderDepthCategory.Enemy:
                case RenderDepthCategory.EnemyRemains:
                    return EnemyBase;
                case RenderDepthCategory.GameplayVfx:
                    return GameplayVfxBase;
                default:
                    return 0;
            }
        }

        private static int ClampBand(int value, int bandBase)
        {
            return Mathf.Clamp(value, bandBase, bandBase + YSortRange);
        }
    }
}
