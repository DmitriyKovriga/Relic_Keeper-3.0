using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Skills;
using Scripts.Skills.Modules;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class BowShotPreview
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void CaptureRuntimeViews()
        {
            Capture(BowShotBuilder.Root + "SimpleBowShotSkill.asset", "codex-bow-shot", "BowShot");
        }

        public static void CapturePiercingRuntimeViews()
        {
            Capture(PiercingBowShotBuilder.Root + "PiercingBowShotSkill.asset", "codex-piercing-bow-shot", "PiercingShot");
        }

        private static void Capture(string skillPath, string folderName, string fileName)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var root = new GameObject("Temporary bow shot verification");
            var camera = new GameObject("Bow shot verification camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 270f / 48;
            camera.aspect = 480f / 270;
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.15f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(2000, 0, -10);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
            var target = new RenderTexture(480, 270, 24);
            var texture = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            var owners = new PlayerStats[2];
            try
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(skillPath);
                var shot = skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile");
                var energyClip = shot.GetObject<GameObject>("ProjectilePrefab").GetComponent<Animator>()?.runtimeAnimatorController?.animationClips.FirstOrDefault();
                var weapon = AssetDatabase.LoadAssetAtPath<Scripts.Items.WeaponItemSO>("Assets/Resources/Items/2HWeapon/Bow/Adventurer's Bow.asset");
                WeaponStancePoseTableSO.LoadDefault().TryGetById("bow_stance", out var pose);
                var bodySprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Heroes/WarriorResources/Warrioir_v0.1.png")
                    .OfType<Sprite>().OrderBy(sprite => sprite.name).First();
                var animations = new SkillHandAnimation[2];
                var runners = new SkillStepRunner[2];
                var arrows = new SkillProjectile[2];
                for (int side = 0; side < 2; side++)
                {
                    int facing = side == 0 ? -1 : 1;
                    var owner = new GameObject("Inactive preview stats " + side);
                    owner.SetActive(false);
                    owner.transform.SetParent(root.transform);
                    owner.transform.position = new Vector3(2000 + facing * 2, 0, 0);
                    owner.transform.localScale = new Vector3(facing, 1, 1);
                    var stats = owner.AddComponent<PlayerStats>();
                    owners[side] = stats;
                    var visuals = new GameObject("Visuals").transform;
                    visuals.SetParent(owner.transform, false);
                    visuals.gameObject.AddComponent<SpriteRenderer>().sprite = bodySprite;
                    var pivot = new GameObject("HandPivot").transform;
                    pivot.SetParent(visuals, false);
                    pivot.localPosition = Vector3.left / 24;
                    var holder = new GameObject("WeaponHolder").transform;
                    holder.SetParent(pivot, false);
                    holder.localPosition = pose.LocalPosition;
                    holder.localRotation = Quaternion.Euler(0, 0, pose.LocalEulerZ);
                    var renderer = holder.gameObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = weapon.InHandSprite;
                    renderer.sortingOrder = 1;
                    var runner = owner.AddComponent<SkillStepRunner>();
                    var animation = owner.GetComponent<SkillHandAnimation>();
                    typeof(SkillStepRunner).GetField("_animCtrl", Flags).SetValue(runner, animation);
                    typeof(SkillStepRunner).GetField("_moveCtrl", Flags).SetValue(runner, owner.GetComponent<SkillMovementControl>());
                    runner.Initialize(stats, skill);
                    typeof(SkillStepRunner).GetField("_ctx", Flags).SetValue(runner,
                        new SkillStepContext { OwnerStats = stats, TotalDuration = 1f / 0.7f });
                    animation.SetActiveStyleId("bow_draw");
                    animation.PrepareDrawnProjectile(shot.GetObject<GameObject>("ProjectilePrefab"));
                    // Render the visuals without activating preview stats or touching the real player.
                    visuals.SetParent(root.transform, true);
                    animations[side] = animation;
                    runners[side] = runner;
                }
                target.Create();
                string folder = Path.Combine(Path.GetTempPath(), folderName);
                Directory.CreateDirectory(folder);
                for (int frame = 0; frame < 44; frame++)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        int facing = side == 0 ? -1 : 1;
                        var animation = animations[side];
                        if (frame < 20) animation.LerpWindup(frame / 19f);
                        else if (frame == 20)
                        {
                            animation.TryGetDrawnProjectilePosition(out var origin);
                            var existing = Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None);
                            typeof(SkillStepRunner).GetMethod("ExecuteSpawnProjectile", Flags).Invoke(runners[side], new object[] { shot });
                            arrows[side] = Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Except(existing).Single();
                            if (energyClip != null) arrows[side].GetComponent<Animator>().enabled = false;
                            if (Vector2.Distance(origin, arrows[side].transform.position) > 0.001f)
                                throw new InvalidOperationException("Arrow jumped at release.");
                            int strike = skill.Recipe.Steps.FindIndex(step => step.StepDefinition.Id == "WeaponStrike");
                            typeof(SkillStepRunner).GetMethod("ExecuteStepLogic", Flags).Invoke(runners[side],
                                new object[] { strike, skill.Recipe.Steps[strike], 1f, 0f });
                        }
                        else
                        {
                            animation.LerpRecovery(Mathf.Clamp01((frame - 20) / 10f));
                            if (arrows[side] != null)
                                arrows[side].transform.position += Vector3.right * (facing * shot.GetFloat("BaseSpeed", 18) / 24f);
                        }
                        if (energyClip != null && arrows[side] != null)
                            energyClip.SampleAnimation(arrows[side].gameObject, (frame - 20) % 8 / 24f);
                    }
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, fileName + "-" + frame.ToString("D2") + ".png"), texture.EncodeToPNG());
                }
                foreach (var animation in animations)
                {
                    animation.ForceReset();
                    if (animation.TryGetDrawnProjectilePosition(out _)) throw new InvalidOperationException("Nocked arrow survived reset.");
                }
                Debug.Log("Bow draw, mirrored release origin and reset verified: " + folder);
            }
            finally
            {
                foreach (var owner in owners)
                    if (owner != null) SkillProjectile.DespawnAllForOwner(owner);
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(root);
            }
        }
    }
}
