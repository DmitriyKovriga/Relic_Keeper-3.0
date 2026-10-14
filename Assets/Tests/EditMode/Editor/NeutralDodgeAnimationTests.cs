using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class NeutralDodgeAnimationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase("Warrior", true)]
        [TestCase("Warrior", false)]
        [TestCase("Rogue", true)]
        [TestCase("Rogue", false)]
        [TestCase("Mage", true)]
        [TestCase("Mage", false)]
        public void NeutralDodge_PlaysGeneratedFramesAtFixedCenter_ThenRestoresLocomotion(string hero, bool grounded)
        {
            var owner = new GameObject("Neutral dodge animation test");
            owner.SetActive(false);
            PlayerMovementVisual visual = null;
            try
            {
                var attack = owner.AddComponent<PlayerAttackInput>();
                var controller = owner.AddComponent<PlayerCharacterAnimationController>();
                typeof(PlayerCharacterAnimationController).GetMethod("Awake", Private).Invoke(controller, null);
                var data = Resources.Load<CharacterDataSO>("Heroes/" + hero + "Resources/" + hero);
                typeof(PlayerCharacterAnimationController).GetMethod("ApplyCharacterData", Private).Invoke(controller, new object[] { data });
                var frames = Resources.LoadAll<Sprite>(data.NeutralDodgeAnimation.SpriteSheetResourcesPath).OrderBy(s => s.name).ToArray();
                Assert.That(frames.Length, Is.EqualTo(9));
                Assert.That(frames.All(s => s.pixelsPerUnit == 24 && s.texture.filterMode == FilterMode.Point), Is.True);
                var source = owner.GetComponent<SpriteRenderer>();
                Sprite before = source.sprite;
                var movement = owner.GetComponent<PlayerMovement>();
                typeof(PlayerMovement).GetField("_isGrounded", Private).SetValue(movement, grounded);
                visual = owner.AddComponent<PlayerMovementVisual>();
                typeof(PlayerMovementVisual).GetMethod("Awake", Private).Invoke(visual, null);
                typeof(PlayerMovementVisual).GetMethod("LateUpdate", Private).Invoke(visual, null);
                Vector3 center = visual.DisplayRenderer.transform.localPosition;
                typeof(PlayerAttackInput).GetField("_isStationaryDodge", Private).SetValue(attack, true);
                typeof(PlayerAttackInput).GetField("_isDodging", Private).SetValue(attack, true);
                typeof(PlayerAttackInput).GetField("_currentDodgeDuration", Private).SetValue(attack, 0.24f);
                foreach (float progress in new[] { 0.25f, 0.5f, 0.75f })
                {
                    typeof(PlayerAttackInput).GetField("_currentDodgeStartTime", Private).SetValue(attack, Time.time - progress * 0.24f);
                    typeof(PlayerCharacterAnimationController).GetMethod("Update", Private).Invoke(controller, null);
                    Assert.That(source.sprite, Is.SameAs(frames[Mathf.Min((int)(attack.StationaryDodgeProgress * frames.Length), frames.Length - 1)]));
                    Assert.That(controller.IsPlayingNeutralDodge, Is.True);
                    Assert.That(controller.PresentationScale, Is.EqualTo(Vector2.one), "The tumble is drawn, not transform rotated or scaled.");
                    typeof(PlayerMovementVisual).GetMethod("LateUpdate", Private).Invoke(visual, null);
                    Assert.That(visual.DisplayRenderer.transform.localPosition, Is.EqualTo(center));
                    Assert.That(owner.GetComponent<PlayerRenderAnchor>().HideWeaponsForDodge, Is.True);
                    Assert.That(owner.transform.rotation, Is.EqualTo(Quaternion.identity));
                }
                controller.SampleNeutralDodge(1f);
                Assert.That(source.sprite, Is.SameAs(frames[8]));
                typeof(PlayerAttackInput).GetField("_isDodging", Private).SetValue(attack, false);
                typeof(PlayerCharacterAnimationController).GetMethod("Update", Private).Invoke(controller, null);
                typeof(PlayerMovementVisual).GetMethod("LateUpdate", Private).Invoke(visual, null);
                Assert.That(controller.IsPlayingNeutralDodge, Is.False);
                Assert.That(source.sprite, Is.SameAs(before));
                Assert.That(owner.GetComponent<PlayerRenderAnchor>().HideWeaponsForDodge, Is.False);
            }
            finally
            {
                if (visual != null && visual.DisplayRenderer != null)
                {
                    UnityEngine.Object.DestroyImmediate(visual.DisplayRenderer.gameObject);
                    typeof(PlayerMovementVisual).GetField("_display", Private).SetValue(visual, null);
                }
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
