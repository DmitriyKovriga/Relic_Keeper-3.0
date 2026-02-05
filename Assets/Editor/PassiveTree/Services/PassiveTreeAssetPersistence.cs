using UnityEditor;
using UnityEngine;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Сохранение ассета дерева пассивок. Единственная ответственность — SetDirty и SaveAssets.
    /// </summary>
    public static class PassiveTreeAssetPersistence
    {
        public static void SetDirty(PassiveSkillTreeSO tree)
        {
            if (tree != null)
                EditorUtility.SetDirty(tree);
        }

        public static void SaveAssets(PassiveSkillTreeSO tree)
        {
            if (tree != null)
            {
                EditorUtility.SetDirty(tree);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
