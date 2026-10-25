using UnityEngine;

namespace Scripts.Visuals
{
    // Authored on a supporting collider/tilemap. No per-frame work or physics changes.
    [DisallowMultipleComponent]
    public sealed class GroundSurfaceVisuals : MonoBehaviour
    {
        [SerializeField] private GroundSurfaceProfileSO _profile;
        public GroundSurfaceProfileSO Profile => _profile;
        public void Configure(GroundSurfaceProfileSO profile) => _profile = profile;
    }
}
