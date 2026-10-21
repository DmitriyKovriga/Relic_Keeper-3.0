using UnityEngine;

namespace Scripts.Skills.Visuals
{
    public sealed class SkillSpinTrail : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _referenceRadius = 2.2f;
        [SerializeField] private float _referencePlaneDepth = .16f;
        public float ReferenceRadius => Mathf.Max(.01f, _referenceRadius);
        public float ReferencePlaneDepth => Mathf.Max(.01f, _referencePlaneDepth);
        public void Sample(float phase, SpriteRenderer body)
        {
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer == null || _frames == null || _frames.Length == 0) return;
            int frame = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(phase) * (_frames.Length - 1)), 0, _frames.Length - 1);
            renderer.sprite = _frames[frame];
            renderer.color = PlayerAttackVfxOpacity.MultiplyAlpha(Color.white);
            if (body != null)
            {
                renderer.sortingLayerID = body.sortingLayerID;
                renderer.sortingOrder = body.sortingOrder + (Mathf.Sin(phase * Mathf.PI * 2) < -.05f ? -1 : 1);
            }
        }
    }
}
