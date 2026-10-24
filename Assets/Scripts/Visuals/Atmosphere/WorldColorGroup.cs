using UnityEngine;

namespace Scripts.Visuals.Atmosphere
{
    // Color membership is independent of sorting, physics layers and sprite data.
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Relic Keeper/Visuals/World Color Group")]
    public sealed class WorldColorGroup : MonoBehaviour
    {
        [SerializeField] private WorldColorRole _role = WorldColorRole.Environment;
        [SerializeField, Range(0f, 1f)] private float _influence = 1f;
        [SerializeField] private bool _combatEffect;
        public WorldColorRole Role => _role;
        public float Influence => _influence;
        public bool CombatEffect => _combatEffect;

        public void Configure(WorldColorRole role, float influence = 1f, bool combatEffect = false)
        {
            _role = role; _influence = Mathf.Clamp01(influence); _combatEffect = combatEffect;
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void OnDisable() => Refresh();
        private void OnValidate() => Refresh();
        private void OnTransformChildrenChanged() => Refresh();
        private void Refresh() => WorldColorGrading.RefreshRoot(transform);
    }
}
