using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Scripts.Enemies;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class LauncherTowerTossPreview
    {
        public static void Capture()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            Directory.CreateDirectory("Temp/LauncherTowerTossPreview"); File.WriteAllText("Temp/LauncherTowerTossPreview/Report.txt", "Running");
            var runner = new GameObject("Temporary tower toss check").AddComponent<LauncherTowerTossPreviewRunner>();
            runner.StartCoroutine(runner.Run());
        }
    }
    public sealed class LauncherTowerTossPreviewRunner : MonoBehaviour
    {
        public IEnumerator Run()
        {
            var target = new RenderTexture(480, 270, 24); var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active; bool background = Application.runInBackground;
            var materials = new List<Material>(); var report = new List<string>();
            try
            {
                Application.runInBackground = true;
                var live = Object.FindFirstObjectByType<PlayerStats>();
                var heroSprite = live != null && live.GetComponent<SpriteRenderer>() != null
                    ? live.GetComponent<SpriteRenderer>().sprite : Resources.Load<CharacterDataSO>("Heroes/RogueResources/Rogue").Portrait;
                Object.Instantiate(Resources.Load<GameObject>("Prefabs/Dungeon/MineDungeon/MortfallRoom_001"), new Vector3(2800, 0, 0), Quaternion.identity, transform);
                Physics2D.SyncTransforms();
                var floor = Physics2D.Raycast(new Vector2(2796, 50), Vector2.down, 100, 1 << 6);
                if (floor.collider == null) throw new Exception("Preview floor missing.");
                Vector2 center = new Vector2(2796, floor.point.y + .5f);
                var owner = new GameObject("Tower toss caster"); owner.transform.SetParent(transform); owner.transform.position = center;
                var stats = owner.AddComponent<PlayerStats>(); stats.enabled = false;
                stats.GetStat(StatType.MaxHealth).BaseValue = 100; stats.Health.RestoreFull();
                stats.GetStat(StatType.MaxMana).BaseValue = 100; stats.Mana.RestoreFull();
                stats.GetStat(StatType.DamagePhysical).BaseValue = 40; stats.GetStat(StatType.CritChance).BaseValue = 0;
                owner.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1); owner.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                var bodyArt = new GameObject("Body"); bodyArt.transform.SetParent(owner.transform, false);
                AddSprite(bodyArt, heroSprite, 0); bodyArt.transform.localPosition = new Vector3(-heroSprite.bounds.center.x, -.5f - heroSprite.bounds.min.y);
                var visuals = new GameObject("Visuals"); visuals.transform.SetParent(owner.transform, false);
                var hand = new GameObject("HandPivot"); hand.transform.SetParent(visuals.transform, false);
                var holder = new GameObject("WeaponHolder"); holder.transform.SetParent(hand.transform, false);
                WeaponStancePoseTableSO.LoadDefault().TryGetById("shield_guard", out var pose);
                holder.transform.localPosition = pose.LocalPosition; holder.transform.localRotation = Quaternion.Euler(0, 0, pose.LocalEulerZ);
                var tower = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(RelicKeeper.Editor.VFX.TowerShieldChargeBuilder.TowerPath);
                var shieldRenderer = AddSprite(holder, tower.InHandSprite, 1);
                var camera = new GameObject("Tower toss camera").AddComponent<Camera>(); camera.transform.SetParent(transform);
                camera.orthographic = true; camera.orthographicSize = 5.625f; camera.aspect = 480f / 270;
                camera.transform.position = (Vector3)center + new Vector3(.5f, 1.5f, -10); camera.enabled = false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false; camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                target.Create();
                foreach (int direction in new[] { 1, -1 })
                {
                    owner.transform.localScale = new Vector3(direction, 1, 1);
                    stats.Mana.RestoreFull();
                    var source = new GameObject("Tower toss recipe"); source.transform.SetParent(transform);
                    var skill = source.AddComponent<SkillStepRunner>(); skill.SetRuntimeSlot(null, 1);
                    skill.Initialize(stats, AssetDatabase.LoadAssetAtPath<SkillDataSO>(LauncherTowerTossBuilder.SkillPath));
                    var data = Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
                    var enemy = Object.Instantiate(data.Prefab, transform); enemy.Setup(data, 60);
                    enemy.transform.position = center + Vector2.right * (.9f * direction);
                    Physics2D.SyncTransforms(); var collider = enemy.GetComponent<Collider2D>();
                    enemy.transform.position += Vector3.up * (floor.point.y - collider.bounds.min.y);
                    enemy.GetComponent<EnemyBrain>().enabled = false;
                    var health = enemy.GetComponent<EnemyHealth>(); var stun = enemy.GetComponent<EnemyStunController>();
                    float before = health.CurrentHealth, initialY = enemy.transform.position.y, highest = initialY;
                    Physics2D.SyncTransforms(); yield return null; yield return null;
                    float started = Time.time; skill.TryCast();
                    if (stats.Mana.Current != 90 || skill.CooldownRemaining < 7.9f) throw new Exception("Toss mana or cooldown failed.");
                    bool sawStun = false, sawHidden = false, sawVfx = false; int shot = 0;
                    foreach (float captureTime in new[] { .08f, .2f, .28f, .4f, .65f, 1.5f })
                    {
                        while (Time.time - started < captureTime)
                        {
                            sawStun |= stun.IsStunned; sawHidden |= !shieldRenderer.enabled;
                            foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                                if (animator.name.StartsWith("LauncherTowerTossImpact", StringComparison.Ordinal)) sawVfx = true;
                            highest = Mathf.Max(highest, enemy.transform.position.y); yield return null;
                        }
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                        File.WriteAllBytes($"Temp/LauncherTowerTossPreview/Toss-{direction}-{++shot}.png", image.EncodeToPNG());
                    }
                    if (!sawStun || highest - initialY < .15f || health.CurrentHealth >= before) throw new Exception("Mold was not hit, stunned and launched.");
                    if (stun.IsStunned || !shieldRenderer.enabled || skill.IsCasting || !sawHidden || !sawVfx) throw new Exception("Strike presentation or recovery failed.");
                    report.Add($"Facing {direction}: HP {before} -> {health.CurrentHealth}, peak lift {highest - initialY:0.000} m; stun ended and shield returned; 10 mana and 8s CD.");
                    skill.Cancel(); Object.Destroy(source); Object.Destroy(enemy.gameObject); yield return null;
                }
                File.WriteAllText("Temp/LauncherTowerTossPreview/Report.txt", string.Join("\n", report));

                SpriteRenderer AddSprite(GameObject host, Sprite art, int offset)
                {
                    var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = art;
                    var material = new Material(Resources.Load<Material>("Skills/1HWeapon/Wand/ElectricBeam")); material.mainTexture = art.texture;
                    materials.Add(material); renderer.sharedMaterial = material;
                    renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.Player);
                    renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.Player, center.y, offset); return renderer;
                }
            }
            finally
            {
                Application.runInBackground = background; RenderTexture.active = previous;
                target.Release(); Object.Destroy(target); Object.Destroy(image);
                foreach (var material in materials) Object.Destroy(material);
                Object.Destroy(gameObject);
            }
        }
    }
}
