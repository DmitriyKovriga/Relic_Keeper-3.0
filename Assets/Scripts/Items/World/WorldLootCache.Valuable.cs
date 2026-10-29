using System;
using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Visuals;
using Scripts.Visuals.Atmosphere;
using UnityEngine;

namespace Scripts.Items.World
{
    public sealed partial class WorldLootCache
    {
        public const float ValuableArrivalCooldown = 1f;
        public const float ValuableWaveDuration = 10f / 15f;
        private const float UniqueWaveDelay = .16f;
        private static Sprite[] _valuableFrames;
        private static Sprite[] _uniqueValuableFrames;
        private readonly List<InventoryItem> _valuableItems = new List<InventoryItem>();
        private SpriteRenderer[] _valuableWaves;
        private SpriteRenderer[] _valuableGlows;
        private LocalLight2DSource _valuableLight;
        private float _valuableAge, _valuableCooldown, _secondWaveStart;
        private int _valuableStrength, _lastValuableStrength;
        private int _valuableLightStyle;

        public int ValuableArrivalStrength => _valuableStrength;

        private void NotifyValuableArrival(InventoryItem item)
        {
            int strength = ArrivalStrength(item);
            if (strength == 0) return;
            if (_valuableItems.Count == 0)
            {
                if (_valuableCooldown > 0 && strength <= _lastValuableStrength) return;
                _valuableAge = 0f; _secondWaveStart = UniqueWaveDelay;
                _valuableCooldown = ValuableArrivalCooldown;
                _valuableStrength = strength;
                EnsureValuableVisual();
            }
            else if (strength > _valuableStrength)
            {
                // Escalate one existing batch, never queue another set of bursts.
                _valuableStrength = strength;
                _secondWaveStart = Mathf.Max(UniqueWaveDelay, _valuableAge);
                _valuableCooldown = ValuableArrivalCooldown;
            }
            _lastValuableStrength = Mathf.Max(_lastValuableStrength, strength);
            if (!_valuableItems.Contains(item)) _valuableItems.Add(item);
            ApplyValuable();
        }

        private int ArrivalStrength(InventoryItem item)
        {
            if (!CanPresentArrival(item)) return 0;
            var tier = ItemRarity.GetTier(item);
            return tier == ItemRarityTier.Unique ? 2 : tier == ItemRarityTier.Rare ? 1 : 0;
        }

        private void UpdateValuable(float deltaTime)
        {
            float dt = Mathf.Max(0f, deltaTime);
            _valuableCooldown = Mathf.Max(0f, _valuableCooldown - dt);
            if (_valuableCooldown == 0f) _lastValuableStrength = 0;
            PruneValuable();
            if (_valuableItems.Count == 0) return;
            _valuableAge += dt;
            float duration = ValuableWaveDuration + (_valuableStrength == 2 ? _secondWaveStart : 0f);
            if (_valuableAge >= duration) ResetValuable();
            else ApplyValuable();
        }

        private void PruneValuable()
        {
            int strength = 0;
            for (int i = _valuableItems.Count - 1; i >= 0; i--)
            {
                int itemStrength = ArrivalStrength(_valuableItems[i]);
                if (itemStrength == 0) _valuableItems.RemoveAt(i);
                else strength = Mathf.Max(strength, itemStrength);
            }
            _valuableStrength = strength;
            if (strength == 0) ResetValuable();
            else ApplyValuable();
        }

        private void ResetValuable()
        {
            _valuableItems.Clear(); _valuableStrength = 0;
            if (_valuableWaves != null)
                foreach (var wave in _valuableWaves) if (wave != null) wave.enabled = false;
            if (_valuableGlows != null)
                foreach (var glow in _valuableGlows) if (glow != null) glow.enabled = false;
            if (_valuableLight != null) { _valuableLight.SetStrength(0); _valuableLight.enabled = false; }
        }

        private void EnsureValuableVisual()
        {
            if (_valuableWaves != null) return;
            if (_valuableFrames == null)
            {
                _valuableFrames = Resources.LoadAll<Sprite>("Visuals/LootCache/ValuableArrival");
                Array.Sort(_valuableFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
                _uniqueValuableFrames = Resources.LoadAll<Sprite>("Visuals/LootCache/UniqueArrival");
                Array.Sort(_uniqueValuableFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            var root = new GameObject("ValuableArrival"); root.transform.SetParent(transform, false);
            var material = Resources.Load<Material>("Visuals/SourceGlow/Halo");
            var bodyMaterial = Resources.Load<Material>("Visuals/DefenseFeedback/DefenseAccent");
            _valuableWaves = new SpriteRenderer[2];
            _valuableGlows = new SpriteRenderer[2];
            for (int i = 0; i < _valuableWaves.Length; i++)
            {
                var host = new GameObject("WaveAndStars" + i); host.transform.SetParent(root.transform, false);
                var wave = host.AddComponent<SpriteRenderer>();
                wave.sharedMaterial = bodyMaterial;
                wave.sortingLayerName = WorldRenderSorting.LayerVfx;
                wave.sortingOrder = WorldDroppedItem.TopVisualSortingOrder + 6;
                wave.enabled = false; _valuableWaves[i] = wave;
                var haloHost = new GameObject("StarLight" + i); haloHost.transform.SetParent(host.transform, false);
                haloHost.transform.localScale = Vector3.one * 1.08f;
                var halo = haloHost.AddComponent<SpriteRenderer>(); halo.sharedMaterial = material;
                halo.sortingLayerName = wave.sortingLayerName; halo.sortingOrder = wave.sortingOrder - 1;
                halo.color = new Color(1f, .7f, .2f, .24f); halo.enabled = false; _valuableGlows[i] = halo;
            }
            var lightHost = new GameObject("ArrivalLight"); lightHost.transform.SetParent(root.transform, false);
            _valuableLight = lightHost.AddComponent<LocalLight2DSource>();
            _valuableLight.ConfigureFill(0f, 1.8f, new Color(1f, .75f, .3f));
            _valuableLight.enabled = false;
        }

        private void ApplyValuable()
        {
            if (_valuableWaves == null || _valuableFrames == null) return;
            bool unique = _valuableStrength == 2;
            var frames = unique && _uniqueValuableFrames.Length > 0 ? _uniqueValuableFrames : _valuableFrames;
            Color lightColor = unique ? new Color(1f, .28f, .12f) : new Color(1f, .75f, .3f);
            if (_valuableLightStyle != _valuableStrength)
            {
                _valuableLightStyle = _valuableStrength;
                _valuableLight.ConfigureFill(0f, unique ? 3.6f : 1.8f, lightColor);
            }
            for (int i = 0; i < _valuableWaves.Length; i++)
            {
                float age = _valuableAge - (i == 1 ? _secondWaveStart : 0f);
                var wave = _valuableWaves[i];
                bool visible = i < _valuableStrength && age >= 0 && age < ValuableWaveDuration && frames.Length > 0;
                wave.enabled = visible;
                _valuableGlows[i].enabled = visible;
                if (visible)
                {
                    wave.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(age * 15f))];
                    wave.transform.localScale = Vector3.one * (unique ? 2f : 1f);
                    // A mirrored second burst also spreads its stars to the other side.
                    wave.flipX = i == 1;
                    wave.color = Color.white;
                    _valuableGlows[i].sprite = wave.sprite; _valuableGlows[i].flipX = wave.flipX;
                    _valuableGlows[i].color = new Color(lightColor.r, lightColor.g, lightColor.b, .24f);
                }
            }
            float glow = Mathf.Sin(Mathf.Clamp01(_valuableAge / (ValuableWaveDuration + (_valuableStrength == 2 ? _secondWaveStart : 0f))) * Mathf.PI);
            _valuableLight.enabled = _valuableStrength > 0;
            _valuableLight.SetStrength(.28f * _valuableStrength * glow);
        }
    }
}
