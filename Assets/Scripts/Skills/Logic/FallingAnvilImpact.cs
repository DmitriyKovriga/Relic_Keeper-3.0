using UnityEngine;

namespace Scripts.Skills
{
    /// <summary>One landing per cast; only actual downward travel adds damage.</summary>
    public sealed class FallingAnvilImpact
    {
        public bool IsAerial { get; }
        public bool HasLanded { get; private set; }
        public float FallHeight { get; private set; }
        private float _peakY;

        public FallingAnvilImpact(bool grounded, float currentY, float airbornePeakY)
        {
            IsAerial = !grounded;
            _peakY = Mathf.Max(currentY, airbornePeakY);
        }

        public void Observe(float y, bool grounded)
        {
            if (!IsAerial || HasLanded) return;
            _peakY = Mathf.Max(_peakY, y);
            if (!grounded) return;
            HasLanded = true;
            FallHeight = Mathf.Max(0, _peakY - y);
        }

        public float DamageScale(float bonusPerUnit, float maximumBonus) =>
            1f + Mathf.Min(FallHeight * Mathf.Max(0, bonusPerUnit), Mathf.Max(0, maximumBonus));
    }
}
