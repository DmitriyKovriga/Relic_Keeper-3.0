using System.Linq;
using NUnit.Framework;
using Scripts.Editor.Affixes;
using Scripts.Items.Affixes;
using UnityEditor;

namespace RelicKeeper.Tests.EditMode
{
    public class AffixPoolEditorWindowTests
    {
        private const string CritChanceFolder = "Assets/Resources/Affixes/ByStat/Critical/CritChance/";

        [Test]
        public void ExistingLocalCopy_DisablesCreatingAnotherCopy()
        {
            ItemAffixSO global = Load("CritChance_Increase_Medium");
            ItemAffixSO local = Load("Local_CritChance_Increase_Medium");
            ItemAffixSO withoutLocalCopy = Load("CritChance_Increase_Light");

            Assert.That(AffixPoolEditorWindow.IsLocalAffix(global), Is.False);
            Assert.That(AffixPoolEditorWindow.IsLocalAffix(local), Is.True);
            Assert.That(AffixPoolEditorWindow.CanCreateLocalCopy(global), Is.False);
            Assert.That(AffixPoolEditorWindow.CanCreateLocalCopy(local), Is.False);
            Assert.That(AffixPoolEditorWindow.CanCreateLocalCopy(withoutLocalCopy), Is.True);
        }

        [Test]
        public void LocalCopy_AppearsImmediatelyAfterItsParent()
        {
            ItemAffixSO global = Load("CritChance_Increase_Medium");
            ItemAffixSO local = Load("Local_CritChance_Increase_Medium");
            ItemAffixSO other = Load("CritChance_Flat_Medium");

            ItemAffixSO[] ordered = AffixPoolEditorWindow.OrderAffixesForDisplay(
                new[] { local, other, global }).ToArray();

            Assert.That(ordered, Is.EqualTo(new[] { other, global, local }));
        }

        private static ItemAffixSO Load(string name)
        {
            ItemAffixSO affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(CritChanceFolder + name + ".asset");
            Assert.That(affix, Is.Not.Null, name);
            return affix;
        }
    }
}
