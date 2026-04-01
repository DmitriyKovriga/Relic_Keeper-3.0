using UnityEngine;

namespace Scripts.Skills.PassiveTree.UI
{
    [CreateAssetMenu(menuName = "RPG/UI/Passive Tree Theme")]
    public class PassiveTreeThemeSO : ScriptableObject
    {
        [Header("Node Sizes")]
        public float NodeSizeSmall = 40f;
        public float NodeSizeNotable = 60f;
        public float NodeSizeKeystone = 80f;
        public float LineThickness = 4f;

        [Header("Node Frames")]
        public Sprite SmallNodeFrame;
        public Sprite NotableNodeFrame;
        public Sprite KeystoneNodeFrame;

        [Header("Colors - Allocated (Bought)")]
        public Color AllocatedFill = new Color(0.8f, 0.6f, 0.1f);
        public Color AllocatedBorder = new Color(1f, 0.8f, 0.2f);
        public Color AllocatedGlow = new Color(1f, 0.75f, 0.25f, 0.30f);
        
        [Header("Colors - Available (Can Buy)")]
        public Color AvailableFill = new Color(0.15f, 0.15f, 0.15f);
        public Color AvailableBorder = new Color(0.5f, 0.5f, 0.5f);
        public Color AvailableGlow = new Color(1f, 1f, 1f, 0.24f); 

        [Header("Colors - Locked")]
        public Color LockedFill = new Color(0.1f, 0.1f, 0.1f);
        public Color LockedBorder = new Color(0.2f, 0.2f, 0.2f);

        [Header("Node Highlight")]
        public Color AllocatedHighlightColor = new Color(1f, 0.78f, 0.25f, 0.34f);
        public float AllocatedHighlightScale = 1.42f;
        public Color AvailableHighlightColor = new Color(0.95f, 0.95f, 0.95f, 0.24f);
        public float AvailableHighlightScale = 1.38f;

        [Header("Colors - Connections")]
        public Color LineAllocated = new Color(1f, 0.8f, 0.2f, 0.8f);
        public Color LinePath = new Color(0.7f, 0.7f, 0.7f, 0.5f);    
        public Color LineLocked = new Color(0.15f, 0.15f, 0.15f, 0.5f);

        [Header("Connection Track")]
        [Range(0.1f, 0.95f)] public float LineLockedInnerThicknessScale = 0.46f;
        [Range(0.1f, 0.95f)] public float LinePathInnerThicknessScale = 0.28f;
        [Range(0.1f, 0.95f)] public float LineAllocatedInnerThicknessScale = 0.30f;
        public Color LineAllocatedOuter = new Color(0.87f, 0.67f, 0.24f, 0.98f);
        public Color LineAllocatedInner = new Color(1f, 0.96f, 0.76f, 0.82f);
        public Color LinePathOuter = new Color(0.68f, 0.68f, 0.68f, 0.95f);
        public Color LinePathInner = new Color(0.93f, 0.93f, 0.93f, 0.72f);
        public Color LineLockedOuter = new Color(0.42f, 0.34f, 0.17f, 0.75f);
        public Color LineLockedInner = new Color(0.12f, 0.10f, 0.07f, 0.88f);
        [Range(0.1f, 2.5f)] public float LinePathPulseSpeed = 0.9f;
        [Range(0f, 1f)] public float LinePathPulseMinAlpha = 0.05f;
        [Range(0f, 1f)] public float LinePathPulseMaxAlpha = 0.55f;
    }
}
