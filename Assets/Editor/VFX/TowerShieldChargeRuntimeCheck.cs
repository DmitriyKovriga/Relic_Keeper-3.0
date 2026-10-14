using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Scripts.Combat;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    public class TowerChargeRuntimeTarget : MonoBehaviour, IDamageable
    {
        public int Hits;
        public DamageSnapshot Last;
        public bool TakeDamage(DamageSnapshot damage) { Hits++; Last = damage; return true; }
    }

    public static class TowerShieldChargeRuntimeCheck
    {
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            string report = Path.Combine(Path.GetTempPath(), "tower-charge-runtime-check.txt");
            string folder = Path.Combine(Path.GetTempPath(), "codex-tower-charge"); Directory.CreateDirectory(folder);
            File.WriteAllText(report, "Running");
            var stage = new GameObject("Temporary tower charge check");
            var playerObject = new GameObject("Tower charge actor"); playerObject.SetActive(false);
            playerObject.transform.SetParent(stage.transform); playerObject.transform.position = new Vector3(2000, 10, 0);
            var rb = playerObject.AddComponent<Rigidbody2D>(); rb.gravityScale = 2; rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var body = playerObject.AddComponent<SpriteRenderer>();
            playerObject.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1.3f);
            var player = playerObject.AddComponent<PlayerStats>();
            var movement = playerObject.AddComponent<PlayerMovement>();
            var ground = new GameObject("Ground check").transform; ground.SetParent(playerObject.transform, false); ground.localPosition = new Vector3(0, -0.65f);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(PlayerMovement).GetField("_groundCheckPoint", flags).SetValue(movement, ground);
            typeof(PlayerMovement).GetField("_groundLayer", flags).SetValue(movement, (LayerMask)(1 << 6));
            var visuals = new GameObject("Visuals").transform; visuals.SetParent(playerObject.transform, false);
            var pivot = new GameObject("HandPivot").transform; pivot.SetParent(visuals, false);
            var holder = new GameObject("WeaponHolder").AddComponent<SpriteRenderer>(); holder.transform.SetParent(pivot, false);
            holder.sprite = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(TowerShieldChargeBuilder.TowerPath).InHandSprite;
            holder.transform.localPosition = new Vector3(0.5f, -0.03f); holder.sortingOrder = 1;
            var camera = new GameObject("Tower charge check camera").AddComponent<Camera>(); camera.transform.SetParent(stage.transform);
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 2; camera.aspect = 480f / 270;
            camera.transform.position = new Vector3(2001.4f, 10.3f, -10); camera.backgroundColor = new Color(0.12f, 0.16f, 0.15f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
            var targetTexture = new RenderTexture(480, 270, 24); targetTexture.Create(); camera.targetTexture = targetTexture;
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var floorSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            var floor = new GameObject("Charge floor"); floor.transform.SetParent(stage.transform); floor.transform.position = new Vector3(2001, 9.3f);
            floor.layer = 6;
            floor.AddComponent<BoxCollider2D>().size = new Vector2(20, 0.1f);
            var floorVisual = new GameObject("Floor visual"); floorVisual.transform.SetParent(floor.transform, false);
            floorVisual.transform.localScale = new Vector3(20, 0.1f, 1);
            var floorRenderer = floorVisual.AddComponent<SpriteRenderer>(); floorRenderer.sprite = floorSprite;
            floorRenderer.color = new Color(0.3f, 0.33f, 0.27f);
            var wall = new GameObject("Charge blocking wall"); wall.transform.SetParent(stage.transform); wall.transform.position = new Vector3(2002.8f, 10);
            wall.layer = 6;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 3);
            var targets = new List<TowerChargeRuntimeTarget>();
            foreach (float x in new[] { 1.1f, 1.8f })
            {
                var enemy = new GameObject("Charge target"); enemy.transform.SetParent(stage.transform); enemy.transform.position = new Vector3(2000 + x, 10);
                var collider = enemy.AddComponent<BoxCollider2D>(); collider.size = new Vector2(0.3f, 1); collider.isTrigger = true;
                targets.Add(enemy.AddComponent<TowerChargeRuntimeTarget>());
            }
            playerObject.SetActive(true);
            player.Initialize(Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior"));
            player.GetStat(StatType.Armor).BaseValue = 1000; player.GetStat(StatType.DamagePhysical).BaseValue = 0;
            player.GetStat(StatType.CritChance).BaseValue = 0; player.GetStat(StatType.AttackSpeed).BaseValue = 1;
            player.GetStat(StatType.MaxMana).BaseValue = 100; player.Mana.RestoreFull();
            player.GetStat(StatType.ManaRegen).BaseValue = 0; player.GetStat(StatType.ManaRegenPercent).BaseValue = 0;
            var skillObject = new GameObject("Authored tower charge"); skillObject.transform.SetParent(playerObject.transform, false);
            var skill = skillObject.AddComponent<SkillStepRunner>(); skill.SetRuntimeSlot(null, -1);
            skill.Initialize(player, AssetDatabase.LoadAssetAtPath<SkillDataSO>(TowerShieldChargeBuilder.SkillPath));
            float started = 0; bool castStarted = false; double wallStarted = EditorApplication.timeSinceStartup;
            bool previousBackground = Application.runInBackground; float previousScale = Time.timeScale;
            Application.runInBackground = true; Time.timeScale = 0.25f; int frame = 0; bool dustSeen = false; bool bodyVfxSeen = false;
            void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
            void Tick()
            {
                try
                {
                    Require(EditorApplication.isPlaying && stage != null, "Check interrupted."); EditorApplication.QueuePlayerLoopUpdate();
                    Require(EditorApplication.timeSinceStartup - wallStarted < 15, "Check timed out.");
                    if (!castStarted)
                    {
                        // Avoid the large scene-loading delta; slow presentation only for readable captures.
                        if (Time.deltaTime > 0.05f) return;
                        started = Time.time; skill.TryCast(); castStarted = true;
                    }
                    float age = Time.time - started;
                    dustSeen |= playerObject.GetComponentInChildren<Animator>() != null;
                    foreach (var animator in playerObject.GetComponentsInChildren<Animator>())
                        if (animator.name.StartsWith("TowerChargeBody", StringComparison.Ordinal)) bodyVfxSeen = true;
                    if (frame < 14 && age >= frame * 0.08f)
                    {
                        var previous = RenderTexture.active;
                        try
                        {
                            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = targetTexture });
                            RenderTexture.active = targetTexture; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                            File.WriteAllBytes(Path.Combine(folder, $"TowerCharge-{frame:D2}.png"), image.EncodeToPNG()); frame++;
                        }
                        finally { RenderTexture.active = previous; }
                    }
                    if (age < 1.3f) return;
                    Require(playerObject.transform.position.x > 2001.4f && playerObject.transform.position.x < 2002.6f,
                        $"Charge must advance and stop at the wall: position={playerObject.transform.position}, velocity={rb.linearVelocity}, grounded={movement.IsGrounded}, mana={player.Mana.Current}, hits={targets[0].Hits}/{targets[1].Hits}, floor={floor.GetComponent<BoxCollider2D>().size}.");
                    foreach (var enemy in targets)
                    {
                        Require(enemy.Hits == 1, "Each crossed enemy must take exactly one hit.");
                        Require(Mathf.Abs(enemy.Last.Physical - 50) < 0.01f, $"1000 armor must grant 50 physical damage; got {enemy.Last.Physical}.");
                        Require(enemy.Last.PushbackRating == 200, "Charge needs small pushback.");
                    }
                    Require(!skill.IsCasting && !movement.IsMovementLocked && holder.enabled, "Cast must restore movement and held shield.");
                    Require(dustSeen, "Charge dust animation must appear.");
                    Require(bodyVfxSeen, "Animated body charge effect must appear and follow the hero.");
                    Require(Mathf.Abs(player.Mana.Current - 80) < 1, "Charge must consume 20 mana.");
                    Require(skill.CooldownRemaining > 0, "Charge must enter cooldown.");
                    File.WriteAllText(report, "PASS: authored charge recipe moves through both targets, hits each once for 50 physical from 1000 armor, small pushback, wall collision, dust and animated body VFX, mana, cooldown and movement restoration.");
                    Debug.Log("Tower charge Play Mode check passed.");
                }
                catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex.Message); Debug.LogException(ex); }
                EditorApplication.update -= Tick;
                if (EditorApplication.isPlaying) { Application.runInBackground = previousBackground; Time.timeScale = previousScale; }
                camera.targetTexture = null; targetTexture.Release(); Object.Destroy(targetTexture); Object.Destroy(image); Object.Destroy(floorSprite);
                if (stage != null) Object.Destroy(stage);
            }
            EditorApplication.update += Tick; EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
