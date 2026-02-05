using UnityEngine;
using System.Collections.Generic;
using Scripts.Stats;

namespace Scripts.Skills.PassiveTree
{
    [CreateAssetMenu(menuName = "RPG/Passive Tree/Node Template")]
    public class PassiveNodeTemplateSO : ScriptableObject
    {
        [Header("Visuals")]
        public string Name;
        [TextArea] public string Description;
        public Sprite Icon;
        
        [Header("Stats")]
        public List<SerializableStatModifier> Modifiers;
    }
}