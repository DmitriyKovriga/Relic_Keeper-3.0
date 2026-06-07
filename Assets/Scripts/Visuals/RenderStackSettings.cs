using System;
using UnityEngine;

namespace Scripts.Visuals
{
    [CreateAssetMenu(fileName = "RenderStackSettings", menuName = "RK/Visuals/Render Stack Settings")]
    public sealed class RenderStackSettings : ScriptableObject
    {
        public const string DefaultResourcePath = "Visuals/RenderStackSettings";

        [Header("Sorting layers (TagManager order: Background → Default → World → VFX → Hero → SFX)")]
        [SerializeField] private string _layerBackground = WorldRenderSorting.LayerBackground;
        [SerializeField] private string _layerDefault = "Default";
        [SerializeField] private string _layerWorld = WorldRenderSorting.LayerWorld;
        [SerializeField] private string _layerVfx = WorldRenderSorting.LayerVfx;
        [SerializeField] private string _layerHero = WorldRenderSorting.LayerHero;
        [SerializeField] private string _layerSfx = "SFX";

        [Header("Y-sort")]
        [SerializeField] private float _ySortScale = 100f;
        [SerializeField] private int _ySortRange = 7000;
        [SerializeField] private float _ySortUpdateThreshold = 0.005f;

        [Header("Sorting order bases")]
        [SerializeField] private int _backgroundBase;
        [SerializeField] private int _environmentBase = 500;
        [SerializeField] private int _enemyBase;
        [SerializeField] private int _gameplayVfxBase;
        [SerializeField] private int _playerBase = 8000;
        [SerializeField] private int _playerOverlayBase = 8500;
        [SerializeField] private int _heroAttackBase = 9000;

        [Header("Scene content conventions (Default layer)")]
        [SerializeField] private int _legacyEnvironmentOrderMax = 200;

        public string LayerBackground => _layerBackground;
        public string LayerDefault => _layerDefault;
        public string LayerWorld => _layerWorld;
        public string LayerVfx => _layerVfx;
        public string LayerHero => _layerHero;
        public string LayerSfx => _layerSfx;
        public float YSortScale => _ySortScale;
        public int YSortRange => _ySortRange;
        public float YSortUpdateThreshold => _ySortUpdateThreshold;
        public int BackgroundBase => _backgroundBase;
        public int EnvironmentBase => _environmentBase;
        public int EnemyBase => _enemyBase;
        public int GameplayVfxBase => _gameplayVfxBase;
        public int PlayerBase => _playerBase;
        public int PlayerOverlayBase => _playerOverlayBase;
        public int HeroAttackBase => _heroAttackBase;
        public int LegacyEnvironmentOrderMax => _legacyEnvironmentOrderMax;

        public string[] GetLitSortingLayerNamesArray()
        {
            return new[]
            {
                _layerBackground,
                _layerDefault,
                _layerWorld,
                _layerVfx,
                _layerHero,
                _layerSfx
            };
        }

        public string GetSortingLayer(RenderDepthCategory category)
        {
            switch (category)
            {
                case RenderDepthCategory.Background:
                    return _layerBackground;
                case RenderDepthCategory.Environment:
                    return _layerBackground;
                case RenderDepthCategory.Enemy:
                case RenderDepthCategory.EnemyRemains:
                    return _layerWorld;
                case RenderDepthCategory.GameplayVfx:
                    return _layerVfx;
                case RenderDepthCategory.Player:
                case RenderDepthCategory.PlayerOverlay:
                case RenderDepthCategory.HeroAttackVfx:
                    return _layerHero;
                default:
                    return _layerDefault;
            }
        }

        public int GetCategoryBase(RenderDepthCategory category)
        {
            switch (category)
            {
                case RenderDepthCategory.Enemy:
                case RenderDepthCategory.EnemyRemains:
                    return _enemyBase;
                case RenderDepthCategory.GameplayVfx:
                    return _gameplayVfxBase;
                default:
                    return 0;
            }
        }

        public int ResolveOrder(RenderDepthCategory category, float worldY, int localOffset = 0)
        {
            int yOrder = Mathf.RoundToInt(-worldY * _ySortScale);

            switch (category)
            {
                case RenderDepthCategory.Background:
                    return _backgroundBase + localOffset;
                case RenderDepthCategory.Environment:
                    return _environmentBase + localOffset;
                case RenderDepthCategory.Enemy:
                case RenderDepthCategory.EnemyRemains:
                case RenderDepthCategory.GameplayVfx:
                    return ClampBand(GetCategoryBase(category) + yOrder + localOffset, GetCategoryBase(category));
                case RenderDepthCategory.Player:
                    return ClampBand(_playerBase + yOrder + localOffset, _playerBase);
                case RenderDepthCategory.PlayerOverlay:
                    return ClampBand(_playerOverlayBase + yOrder + localOffset, _playerOverlayBase);
                case RenderDepthCategory.HeroAttackVfx:
                    return ClampBand(_heroAttackBase + yOrder + localOffset, _heroAttackBase);
                default:
                    return localOffset;
            }
        }

        public bool ValidateSortingLayerOrder(out string error)
        {
            string[] expected =
            {
                _layerBackground,
                _layerDefault,
                _layerWorld,
                _layerVfx,
                _layerHero,
                _layerSfx
            };

            SortingLayer[] layers = SortingLayer.layers;
            if (layers.Length < expected.Length)
            {
                error = $"TagManager has {layers.Length} sorting layers, expected at least {expected.Length}.";
                return false;
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (!string.Equals(layers[i].name, expected[i], StringComparison.Ordinal))
                {
                    error = $"Sorting layer index {i} is '{layers[i].name}', expected '{expected[i]}'.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private int ClampBand(int value, int bandBase)
        {
            return Mathf.Clamp(value, bandBase, bandBase + _ySortRange);
        }
    }
}
