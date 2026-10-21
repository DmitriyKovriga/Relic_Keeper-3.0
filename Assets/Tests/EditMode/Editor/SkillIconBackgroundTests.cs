using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Skills;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class SkillIconBackgroundTests
    {
        [Test]
        public void EveryAssignedSkillIconHasAnOpaqueBackground()
        {
            var icons = AssetDatabase.FindAssets("t:SkillDataSO")
                .Select(guid => AssetDatabase.LoadAssetAtPath<SkillDataSO>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(skill => skill != null && skill.Icon != null).Select(skill => skill.Icon).Distinct().ToArray();
            Assert.That(icons.Length, Is.GreaterThan(0), "The icon audit must inspect real skill assets.");
            foreach (var icon in icons)
            {
                var image = SkillIconBackgrounds.Read(icon);
                try
                {
                    Assert.That(image.GetPixels32().All(pixel => pixel.a == 255), Is.True,
                        $"Skill icon '{icon.name}' needs an opaque background: {AssetDatabase.GetAssetPath(icon)}");
                }
                finally { Object.DestroyImmediate(image); }
            }
        }
    }
}
