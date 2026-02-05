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

        [Header("Colors - Allocated (Bought)")]
        public Color AllocatedFill = new Color(0.8f, 0.6f, 0.1f);
        public Color AllocatedBorder = new Color(1f, 0.8f, 0.2f);
        
        [Header("Colors - Available (Can Buy)")]
        public Color AvailableFill = new Color(0.15f, 0.15f, 0.15f);
        public Color AvailableBorder = new Color(0.5f, 0.5f, 0.5f);
        public Color AvailableHighlight = new Color(1f, 1f, 1f, 0.3f); 

        [Header("Colors - Locked")]
        public Color LockedFill = new Color(0.1f, 0.1f, 0.1f);
        public Color LockedBorder = new Color(0.2f, 0.2f, 0.2f);

        [Header("Colors - Connections")]
        public Color LineAllocated = new Color(1f, 0.8f, 0.2f, 0.8f);
        public Color LinePath = new Color(0.7f, 0.7f, 0.7f, 0.5f);    
        public Color LineLocked = new Color(0.15f, 0.15f, 0.15f, 0.5f);
    }
}