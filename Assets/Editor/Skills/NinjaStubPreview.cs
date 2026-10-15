using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Combat;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Skills.Modules;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using Scripts.Visuals;
using Scripts.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class NinjaStubPreview
    {
        public static void Capture()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = new GameObject("Temporary Ninja Stub verification");
            var camera = new GameObject("Ninja preview camera").AddComponent<Camera>();
            var targetTexture = new RenderTexture(480, 270, 24);
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                Vector2 origin = new Vector2(3000, 2000);
                var floor = new GameObject("One-way platform") { layer = LayerMask.NameToLayer("OneWayPlatform") }; floor.transform.SetParent(root.transform);
                floor.transform.position = origin + Vector2.down;
                floor.AddComponent<BoxCollider2D>().size = new Vector2(24, 1);
                var owner = new GameObject("Preview caster"); owner.transform.SetParent(root.transform); owner.transform.position = origin;
                var stats = owner.AddComponent<PlayerStats>();
                stats.GetStat(StatType.DamagePhysical).BaseValue = 10;
                stats.GetStat(StatType.CritChance).BaseValue = 0;
                owner.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1);
                var live = Object.FindObjectsByType<PlayerStats>(FindObjectsSortMode.None).FirstOrDefault(s => s != stats);
                var sprite = live.GetComponentsInChildren<SpriteRenderer>().Where(r => r.sprite != null)
                    .OrderByDescending(r => r.sprite.bounds.size.y).First().sprite;
                var body = new GameObject("Visuals"); body.transform.SetParent(owner.transform, false);
                AddSprite(body, sprite, origin.y, 0);
                var pivot = new GameObject("HandPivot"); pivot.transform.SetParent(body.transform, false);
                pivot.transform.localPosition = new Vector3(0.3f, 0.08f);
                var hand = new GameObject("WeaponHolder"); hand.transform.SetParent(pivot.transform, false);
                var dagger = AssetDatabase.FindAssets("t:WeaponItemSO").Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.Contains("Dagger")).Select(AssetDatabase.LoadAssetAtPath<WeaponItemSO>)
                    .First(w => w.InHandSprite != null);
                AddSprite(hand, dagger.InHandSprite, origin.y, 1);
                var enemies = new EnemyHealth[2];
                var initialHealth = new float[2];
                var moldData = Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
                for (int i = 0; i < 2; i++)
                {
                    var entity = Object.Instantiate(moldData.Prefab, root.transform);
                    entity.transform.position = origin + Vector2.right * (2 + i * 3);
                    Physics2D.SyncTransforms(); entity.Setup(moldData, 1);
                    enemies[i] = entity.GetComponent<EnemyHealth>(); initialHealth[i] = enemies[i].CurrentHealth;
                }
                var runnerHost = new GameObject("Preview runner"); runnerHost.transform.SetParent(root.transform);
                var runner = runnerHost.AddComponent<SkillStepRunner>();
                typeof(SkillBehaviour).GetField("_ownerStats", flags).SetValue(runner, stats);
                var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(NinjaStubBuilder.SkillPath);
                typeof(SkillBehaviour).GetField("_data", flags).SetValue(runner, skill);
                typeof(SkillStepRunner).GetField("_ctx", flags).SetValue(runner, new SkillStepContext { OwnerStats = stats });
                var animation = runner.GetComponent<SkillHandAnimation>(); animation.Initialize(stats); animation.SetActiveStyleId(skill.SwingStyleId);
                Physics2D.SyncTransforms();
                if (Camera.main != null) camera.CopyFrom(Camera.main);
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 270f / 48f; camera.aspect = 480f / 270f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(30, 42, 38, 255);
                camera.transform.position = (Vector3)origin + new Vector3(2.5f, 0, -10);
                camera.ResetWorldToCameraMatrix(); camera.ResetProjectionMatrix();
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0; targetTexture.Create();
                string folder = Path.Combine(Path.GetTempPath(), "ninja-stub-check"); Directory.CreateDirectory(folder);
                void Save(string name)
                {
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = targetTexture });
                    RenderTexture.active = targetTexture; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
                }
                animation.LerpWindup(1); Save("00-windup");
                string report = "Full runtime recipe on real Molds, one use at a time:\n";
                for (int i = 0; i < 2; i++)
                {
                    animation.SetWeaponVisible(true); animation.SetActiveStyleId(skill.SwingStyleId);
                    typeof(SkillStepRunner).GetField("_ctx", flags).SetValue(runner,
                        new SkillStepContext { OwnerStats = stats, TotalDuration = 1f });
                    if (Time.deltaTime <= 0) throw new InvalidOperationException("Unpause Play Mode for recipe verification.");
                    var routine = (IEnumerator)typeof(SkillStepRunner).GetMethod("RunRecipe", flags).Invoke(runner, null);
                    int frames = 0; int blinks = 0;
                    Vector3 lastPosition = owner.transform.position;
                    bool hitAtBlink = true;
                    while (routine.MoveNext())
                    {
                        if (++frames > 10000) throw new InvalidOperationException("Recipe failed to complete.");
                        if (owner.transform.position != lastPosition)
                        {
                            blinks++; lastPosition = owner.transform.position;
                            hitAtBlink &= enemies[i].CurrentHealth < initialHealth[i];
                            Save($"{i + 1}-instant-hit");
                        }
                    }
                    report += $"Use {i + 1}: {blinks} blink, damage registered at blink: {hitAtBlink}; Mold HP {enemies[0].CurrentHealth}, {enemies[1].CurrentHealth}\n";
                    if (blinks != 1 || !hitAtBlink || enemies[i].CurrentHealth != initialHealth[i] - 10
                        || (i == 0 && enemies[1].CurrentHealth != initialHealth[1]))
                        throw new InvalidOperationException("Expected exactly one blink and one immediate stab per use.");
                    var effects = Object.FindObjectsByType<BlinkStabVisual>(FindObjectsSortMode.None).Where(v => v.transform.position.x > 2500).ToArray();
                    foreach (var effect in effects) effect.transform.SetParent(root.transform, true);
                    for (int f = 0; f < 6; f++) { foreach (var effect in effects) effect.Sample(f / 6f); Save($"{i + 1}-stab-{f}"); }
                    foreach (var effect in effects) Object.DestroyImmediate(effect.gameObject);
                }
                File.WriteAllText(Path.Combine(folder, "Report.txt"), report + $"Material supported: {Resources.Load<Material>("Skills/1HWeapon/Wand/ElectricBeam").shader.isSupported}");
            }
            finally
            {
                RenderTexture.active = previous; targetTexture.Release();
                Object.DestroyImmediate(root); Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(targetTexture); Object.DestroyImmediate(image);
            }
        }

        private static void AddSprite(GameObject host, Sprite sprite, float y, int offset)
        {
            var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.Player);
            renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.Player, y, offset);
        }
    }
}
