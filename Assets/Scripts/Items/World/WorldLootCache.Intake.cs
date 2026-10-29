using System.Collections.Generic;
using Scripts.Inventory;
using UnityEngine;

namespace Scripts.Items.World
{
    public sealed partial class WorldLootCache
    {
        public const float IntakeDuration = 0.38f;
        private static readonly Color IdleGlow = new Color(0.62f, 0.18f, 1f, 0.32f);
        private readonly List<InventoryItem> _intakeItems = new List<InventoryItem>();
        private Transform _visualRoot;
        private float _intakeAge = IntakeDuration;

        public bool CanPresentArrival(InventoryItem item) => isActiveAndEnabled && !_destroying &&
            item?.Data != null && _items.Contains(item) && !LootFilterSettings.ShouldHide(item);

        // A cosmetic completion signal only: never transfers items or raises grid Changed.
        public void NotifyLootArrived(InventoryItem item)
        {
            if (!CanPresentArrival(item) || _intakeItems.Contains(item)) return;
            if (_intakeAge >= IntakeDuration)
            {
                _intakeItems.Clear();
                _intakeAge = 0f;
            }
            // Arrivals during an active pulse merge without restarting or queuing it.
            _intakeItems.Add(item);
            NotifyValuableArrival(item);
        }

        private void UpdateIntake(float deltaTime)
        {
            PruneIntake();
            _intakeAge = Mathf.Min(IntakeDuration, _intakeAge + Mathf.Max(0f, deltaTime));
            if (_intakeAge >= IntakeDuration) _intakeItems.Clear();
            ApplyIntake();
            UpdateValuable(deltaTime);
        }

        private void PruneIntake()
        {
            for (int i = _intakeItems.Count - 1; i >= 0; i--)
                if (!CanPresentArrival(_intakeItems[i])) _intakeItems.RemoveAt(i);
            if (_intakeItems.Count == 0) ResetIntake();
        }

        private void ResetIntake()
        {
            _intakeItems.Clear();
            _intakeAge = IntakeDuration;
            ApplyIntake();
        }

        private void ApplyIntake()
        {
            if (_visualRoot == null || _glowRenderer == null) return;
            float t = Mathf.Clamp01(_intakeAge / IntakeDuration);
            float compression = t < 0.23f ? Mathf.SmoothStep(0f, 1f, t / 0.23f) :
                Mathf.SmoothStep(1f, 0f, (t - 0.23f) / 0.77f);
            // The host transform/collider never animate. Reuse the sphere and glow.
            _visualRoot.localScale = new Vector3(1f + 0.12f * compression, 1f - 0.16f * compression, 1f);
            float idle = 1.18f + Mathf.Sin(Time.time * 3.5f) * 0.08f;
            float convergence = Mathf.Lerp(1.52f, 0.82f, Mathf.Clamp01(t / 0.45f));
            _glowRenderer.transform.localScale = Vector3.one * Mathf.Lerp(idle, convergence, compression);
            _glowRenderer.color = Color.Lerp(IdleGlow, new Color(0.88f, 0.60f, 1f, 0.64f), compression);
        }
    }
}
