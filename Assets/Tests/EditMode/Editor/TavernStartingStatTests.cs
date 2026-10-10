using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class TavernStartingStatTests
    {
        [Test]
        public void StartingStats_ShowOnlyTheDifferenceFromGlobalBase()
        {
            GlobalBaseStatsSO bases = ScriptableObject.CreateInstance<GlobalBaseStatsSO>();
            try
            {
                bases.SetValue(StatType.MaxHealth, 100f);
                bases.SetValue(StatType.MaxMana, 75f);
                bases.SetValue(StatType.AttackSpeed, 0f);
                bases.SetValue(StatType.ManaRegen, 1f);

                var starting = new List<CharacterDataSO.StatConfig>
                {
                    new CharacterDataSO.StatConfig { Type = StatType.MaxHealth, Value = 75f },
                    new CharacterDataSO.StatConfig { Type = StatType.MaxMana, Value = 50f },
                    new CharacterDataSO.StatConfig { Type = StatType.AttackSpeed, Value = 0f },
                    new CharacterDataSO.StatConfig { Type = StatType.ManaRegen, Value = 5f },
                    new CharacterDataSO.StatConfig { Type = StatType.AreaOfEffect, Value = 100f }
                };

                List<string> lines = TavernUI.FormatStartingStatDeltaLines(starting, bases).ToList();

                Assert.That(lines, Does.Contain("-25 " + CharacterWindowLoc.StatName(StatType.MaxHealth)));
                Assert.That(lines, Does.Contain("-25 " + CharacterWindowLoc.StatName(StatType.MaxMana)));
                Assert.That(lines, Does.Contain("+4 " + CharacterWindowLoc.StatName(StatType.ManaRegen)));
                Assert.That(lines, Does.Contain("+100% " + CharacterWindowLoc.StatName(StatType.AreaOfEffect)));
                Assert.That(lines, Has.None.Contains("Attack"));
                Assert.That(string.Join("\n", lines), Does.Not.Contain(": 75"));
            }
            finally
            {
                Object.DestroyImmediate(bases);
            }
        }
    }
}
