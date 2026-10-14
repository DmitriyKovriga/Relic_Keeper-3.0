using System;
using System.IO;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    /// <summary>Checks the authored recipe in Play Mode without modifying scene equipment.</summary>
    public static class BucklerParryRuntimeCheck
    {
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            string report = Path.Combine(Path.GetTempPath(), "buckler-parry-runtime-check.txt");
            File.WriteAllText(report, "Running");
            var root = new GameObject("Temporary parry recipe check");
            root.transform.position = new Vector3(2000, 2000, 0);
            var player = root.AddComponent<PlayerStats>();
            player.GetStat(StatType.MaxHealth).BaseValue = 100;
            player.GetStat(StatType.MaxMana).BaseValue = 100;
            player.Health.RestoreFull(); player.Mana.RestoreFull();
            var receiver = root.AddComponent<PlayerDamageReceiver>();
            var hand = new GameObject("Visuals").transform;
            hand.SetParent(root.transform, false);
            var pivot = new GameObject("HandPivot").transform;
            pivot.SetParent(hand, false);
            var holder = new GameObject("WeaponHolder").AddComponent<SpriteRenderer>();
            holder.transform.SetParent(pivot, false);
            holder.sprite = AssetDatabase.LoadAssetAtPath<Scripts.Items.WeaponItemSO>(BucklerParryBuilder.BucklerPath).InHandSprite;
            var enemy = new GameObject("Temporary parry attacker");
            enemy.transform.SetParent(root.transform, false);
            enemy.AddComponent<EnemyStats>().Initialize(null, 1);
            var health = enemy.AddComponent<EnemyHealth>(); health.Initialize();
            var stun = enemy.AddComponent<EnemyStunController>(); stun.Initialize(null);
            var skillObject = new GameObject("Temporary authored parry skill");
            skillObject.transform.SetParent(root.transform, false);
            var skill = skillObject.AddComponent<SkillStepRunner>();
            skill.SetRuntimeSlot(null, 0);
            skill.Initialize(player, AssetDatabase.LoadAssetAtPath<SkillDataSO>(BucklerParryBuilder.SkillPath));
            float started = Time.time;
            double wallStarted = EditorApplication.timeSinceStartup;
            float previousTimeScale = Time.timeScale;
            bool previousRunInBackground = Application.runInBackground;
            Time.timeScale = 1f;
            Application.runInBackground = true;
            bool hitChecked = false;
            skill.TryCast();
            void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
            void Tick()
            {
                try
                {
                    if (!EditorApplication.isPlaying || root == null) throw new InvalidOperationException("Preview interrupted.");
                    EditorApplication.QueuePlayerLoopUpdate();
                    Require(EditorApplication.timeSinceStartup - wallStarted < 15,
                        $"Recipe check timed out (age {Time.time - started:0.00}, time scale {Time.timeScale}, casting {skill.IsCasting}).");
                    float age = Time.time - started;
                    var parry = root.GetComponent<PlayerParryController>();
                    if (!hitChecked && parry != null && parry.IsActive)
                    {
                        Require(!holder.enabled, "Held shield must disappear during the parry window.");
                        Require(root.GetComponentInChildren<Animator>() != null, "The recipe must spawn its animated VFX.");
                        for (int i = 0; i < 2; i++)
                            Require(receiver.TakeDamageDetailed(new DamageSnapshot(health) { Physical = 10 }).WasParried, "Every hit must be absorbed.");
                        Require(Mathf.Abs(stun.Normalized - 0.5f) < 0.001f, "Two hits must remove 50% of the attacker's maximum stun meter.");
                        Require(Mathf.Approximately(player.Health.Current, 100), "Parried hits must not damage the player.");
                        Require(Mathf.Approximately(health.CurrentHealth, health.MaxHealth), "Default parry must not deal HP damage.");
                        hitChecked = true;
                    }
                    if (age < 1.2f) return;
                    Require(hitChecked, "The authored recipe never opened a parry window.");
                    Require(!parry.IsActive && !skill.IsCasting && holder.enabled, "Cast completion must close the guard and restore the shield.");
                    Require(Mathf.Approximately(player.Mana.Current, 90), "Parry must consume 10 mana.");
                    Require(skill.CooldownRemaining > 0, "Parry must enter cooldown.");
                    File.WriteAllText(report, "PASS: authored recipe, VFX, repeated hits, 25% maximum stun per hit, no HP damage, mana, cooldown, expiry and shield restoration.");
                    Debug.Log("Buckler parry Play Mode recipe check passed.");
                }
                catch (Exception ex)
                {
                    File.WriteAllText(report, "FAIL: " + ex.Message);
                    Debug.LogException(ex);
                }
                EditorApplication.update -= Tick;
                if (EditorApplication.isPlaying)
                {
                    Time.timeScale = previousTimeScale;
                    Application.runInBackground = previousRunInBackground;
                }
                if (root != null) Object.Destroy(root);
            }
            EditorApplication.update += Tick;
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
