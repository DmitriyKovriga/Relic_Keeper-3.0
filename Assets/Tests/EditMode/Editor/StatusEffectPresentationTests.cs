using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Skills;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class StatusEffectPresentationTests
    {
        private sealed class TestSkill : SkillBehaviour
        {
            protected override void Execute()
            {
            }
        }

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }
        }

        [Test]
        public void ResolveHudIcon_PrefersApplyingSkillArt()
        {
            Sprite skillIcon = CreateSprite(Color.red);
            Sprite effectIcon = CreateSprite(Color.blue);
            SkillBehaviour skill = CreateSkill(skillIcon);

            StatusEffectSO effect = ScriptableObject.CreateInstance<StatusEffectSO>();
            effect.Icon = effectIcon;
            _created.Add(effect);

            Assert.That(StatusEffectPresentation.ResolveHudIcon(effect, skill), Is.SameAs(skillIcon));
            Assert.That(StatusEffectPresentation.ResolveHudIcon(effect, null), Is.SameAs(effectIcon));
        }

        [Test]
        public void AuraTint_MapsTheFourBuffColors()
        {
            Assert.That(StatusEffectPresentation.AuraTint(StatusAuraColor.None), Is.EqualTo(Color.white));
            Assert.That(StatusEffectPresentation.AuraTint(StatusAuraColor.Red).r, Is.GreaterThan(0.9f));
            Assert.That(StatusEffectPresentation.AuraTint(StatusAuraColor.Green).g, Is.GreaterThan(0.9f));
            Assert.That(StatusEffectPresentation.AuraTint(StatusAuraColor.Blue).b, Is.GreaterThan(0.9f));
            Assert.That(StatusEffectPresentation.AuraTint(StatusAuraColor.Purple).r, Is.GreaterThan(0.6f));
            Assert.That(StatusEffectPresentation.AuraTint(StatusAuraColor.Purple).b, Is.GreaterThan(0.9f));
        }

        [Test]
        public void ResolveStepAura_DefaultsUnnamedBuffsToGreen()
        {
            var step = new Scripts.Skills.Steps.StepEntry();
            Assert.That(StatusEffectPresentation.ResolveStepAura(step), Is.EqualTo(StatusAuraColor.Green));

            step.SetOverrideInt("AuraColor", (int)StatusAuraColor.None);
            Assert.That(StatusEffectPresentation.ResolveStepAura(step), Is.EqualTo(StatusAuraColor.None));

            step.SetOverrideInt("AuraColor", (int)StatusAuraColor.Blue);
            Assert.That(StatusEffectPresentation.ResolveStepAura(step), Is.EqualTo(StatusAuraColor.Blue));
        }

        [Test]
        public void ApplyStatusEffect_StoresSkillIconAndAura()
        {
            var host = new GameObject("StatusPresentationHost");
            _created.Add(host);
            host.AddComponent<PlayerStats>();
            StatusEffectController controller = host.AddComponent<StatusEffectController>();
            Invoke(controller, "Awake");
            Invoke(controller, "OnEnable");

            Sprite skillIcon = CreateSprite(Color.green);
            SkillBehaviour skill = CreateSkill(skillIcon);

            StatusEffectSO effect = ScriptableObject.CreateInstance<StatusEffectSO>();
            effect.Id = "PresentationBuff";
            effect.Kind = StatusEffectKind.Buff;
            effect.ShowInHud = true;
            effect.BaseDurationSeconds = 4f;
            effect.AuraColor = StatusAuraColor.Purple;
            effect.Modifiers = new List<SerializableStatModifier>
            {
                new SerializableStatModifier { Stat = StatType.MoveSpeed, Value = 10f, Type = StatModType.PercentAdd }
            };
            _created.Add(effect);

            controller.ApplyStatusEffect(effect, skill);

            Assert.That(controller.ActiveEffects.Count, Is.EqualTo(1));
            Assert.That(controller.ActiveEffects[0].HudIcon, Is.SameAs(skillIcon));
            Assert.That(controller.ActiveEffects[0].AuraColor, Is.EqualTo(StatusAuraColor.Purple));

            Invoke(controller, "OnDisable");
        }

        private SkillBehaviour CreateSkill(Sprite icon)
        {
            var data = ScriptableObject.CreateInstance<SkillDataSO>();
            data.Icon = icon;
            _created.Add(data);

            var skillObject = new GameObject("PresentationSkill");
            _created.Add(skillObject);
            TestSkill skill = skillObject.AddComponent<TestSkill>();
            typeof(SkillBehaviour).GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(skill, data);
            return skill;
        }

        private Sprite CreateSprite(Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            _created.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
            _created.Add(sprite);
            return sprite;
        }

        private static void Invoke(Object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, null);
        }
    }
}
