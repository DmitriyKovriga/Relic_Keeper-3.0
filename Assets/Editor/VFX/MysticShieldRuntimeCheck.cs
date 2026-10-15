using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Scripts.Skills;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    public sealed class RepriseCheckCooldown : SkillBehaviour
    {
        protected override void Execute() { }
    }

    public static class MysticShieldRuntimeCheck
    {
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            string report = Path.Combine(Path.GetTempPath(), "mystic-shield-runtime-check.txt");
            File.WriteAllText(report, "Running");
            var actor = new GameObject("Temporary Reprise check"); actor.SetActive(false);
            actor.transform.position = new Vector3(2000, 10);
            actor.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            actor.AddComponent<BoxCollider2D>();
            var movement = actor.AddComponent<PlayerMovement>(); movement.enabled = false;
            var visuals = new GameObject("Visuals").transform; visuals.SetParent(actor.transform, false);
            var hand = new GameObject("HandPivot").transform; hand.SetParent(visuals, false);
            var holder = new GameObject("WeaponHolder").AddComponent<SpriteRenderer>(); holder.transform.SetParent(hand, false);
            holder.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Skills/Shields/MysticShieldHeld.png");
            var stats = actor.AddComponent<PlayerStats>();
            var manager = actor.AddComponent<PlayerSkillManager>(); manager.enabled = false;
            actor.SetActive(true); stats.enabled = false;
            stats.GetStat(StatType.MaxMana).BaseValue = 100; stats.Mana.RestoreFull();
            stats.GetStat(StatType.AttackSpeed).BaseValue = 1;
            var active = (Dictionary<int, SkillBehaviour>)typeof(PlayerSkillManager)
                .GetField("_activeSkills", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            var otherData = ScriptableObject.CreateInstance<SkillDataSO>(); otherData.Cooldown = 20;
            var other = new GameObject("Other cooling skill").AddComponent<RepriseCheckCooldown>();
            other.transform.SetParent(actor.transform, false); other.Initialize(stats, otherData); other.SetRuntimeSlot(manager, 0);
            active[0] = other; other.AddCooldownRemaining(12);
            var runner = new GameObject("Authored Arcane Reprise").AddComponent<SkillStepRunner>();
            runner.transform.SetParent(actor.transform, false);
            runner.Initialize(stats, AssetDatabase.LoadAssetAtPath<SkillDataSO>(MysticShieldRepriseBuilder.SkillPath));
            runner.SetRuntimeSlot(manager, 1); active[1] = runner;
            double wallStart = EditorApplication.timeSinceStartup;
            float start = 0, otherBefore = 0;
            bool cast = false, pulseSeen = false;
            bool background = Application.runInBackground; Application.runInBackground = true;
            void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
            void Tick()
            {
                try
                {
                    Require(EditorApplication.isPlaying && actor != null, "Check interrupted.");
                    Require(EditorApplication.timeSinceStartup - wallStart < 15, "Check timed out.");
                    EditorApplication.QueuePlayerLoopUpdate();
                    if (!cast)
                    {
                        if (Time.deltaTime > 0.05f) return;
                        start = Time.time; otherBefore = other.CooldownRemaining; runner.TryCast(); cast = true;
                    }
                    foreach (var animator in actor.GetComponentsInChildren<Animator>())
                        pulseSeen |= animator.name.StartsWith("ArcaneRepriseVFX", StringComparison.Ordinal);
                    float age = Time.time - start;
                    if (age < 1.2f) return;
                    Require(Mathf.Abs(stats.Mana.Current - 90) < 0.01f, "Must consume exactly 10 mana.");
                    Require(Mathf.Abs(runner.CooldownRemaining - (10 - age)) < 0.15f, "Own cooldown must remain 10 seconds.");
                    Require(Mathf.Abs(other.CooldownRemaining - (otherBefore - age - 5)) < 0.15f, "Must reduce other cooldowns by 5 seconds exactly once.");
                    Require(pulseSeen, "Animated violet pulse must spawn.");
                    Require(!runner.IsCasting && holder.enabled, "Cast must finish and restore the held shield.");
                    File.WriteAllText(report, "PASS: full authored recipe reduces other cooldowns by exactly 5 seconds once, excludes itself, spends 10 mana, spawns the animated pulse, completes and restores the held sprite.");
                    Debug.Log("Mystic Shield full recipe Play Mode check passed.");
                }
                catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex.Message); Debug.LogException(ex); }
                EditorApplication.update -= Tick;
                Application.runInBackground = background;
                if (actor != null) Object.Destroy(actor);
                Object.Destroy(otherData);
            }
            EditorApplication.update += Tick; EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
