using System;
using System.IO;
using System.Reflection;
using Scripts.Visuals;
using Scripts.Items;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    public static class WarriorMovementPreview
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void CaptureRuntimeViews()
        {
            Capture("Assets/Resources/Items/2HWeapon/Bow/Adventurer's Bow.asset", "bow_stance", "Warrior-runtime-");
        }

        public static void CaptureAxeRuntimeViews()
        {
            Capture("Assets/Scripts/Items/Weapon/LowTier/TwoHandedWeapon/LowTierBases/AxeOfSettler/Axe of Settler.asset", "default", "Warrior-axe-runtime-");
        }

        public static void CaptureDoubleJumpRuntimeViews()
        {
            Capture("Assets/Scripts/Items/Weapon/LowTier/TwoHandedWeapon/LowTierBases/AxeOfSettler/Axe of Settler.asset", "default", "Warrior-double-runtime-", true);
        }

        public static void CaptureForwardJumpRuntimeViews()
        {
            Capture("Assets/Scripts/Items/Weapon/LowTier/TwoHandedWeapon/LowTierBases/AxeOfSettler/Axe of Settler.asset", "default", "Warrior-forward-runtime-", false, true);
        }

        public static void CaptureRogueRuntimeViews()
        {
            Capture("Assets/Scripts/Items/Weapon/LowTier/OneHandedWeapon/Daggers/Adventurer's Dagger/Adventurer's Dagger.asset", "default", "Rogue-runtime-", false, false, "Heroes/RogueResources/Rogue");
        }

        public static void CaptureRogueBowRuntimeViews()
        {
            Capture("Assets/Resources/Items/2HWeapon/Bow/Adventurer's Bow.asset", "bow_stance", "Rogue-bow-runtime-", false, true, "Heroes/RogueResources/Rogue");
        }

        public static void CaptureRogueDoubleJumpRuntimeViews()
        {
            Capture("Assets/Scripts/Items/Weapon/LowTier/OneHandedWeapon/Daggers/Adventurer's Dagger/Adventurer's Dagger.asset", "default", "Rogue-double-runtime-", true, true, "Heroes/RogueResources/Rogue");
        }

        private static void Capture(string weaponPath, string stanceId, string outputPrefix, bool doubleJump = false, bool forwardJump = false,
            string characterResourcePath = "Heroes/WarriorResources/Warrior")
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var root = new GameObject("Temporary character animation preview");
            var camera = new GameObject("Character animation preview camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 270f / 48;
            camera.aspect = 480f / 270;
            camera.transform.position = new Vector3(2000, 1, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.15f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
            var pixel = camera.gameObject.AddComponent<PixelPerfectCamera>();
            pixel.assetsPPU = 24;
            pixel.refResolutionX = 480;
            pixel.refResolutionY = 270;
            pixel.gridSnapping = PixelPerfectCamera.GridSnapping.None;
            var target = new RenderTexture(480, 270, 24);
            camera.targetTexture = target;
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            Sprite floorSprite = null;
            var previous = RenderTexture.active;
            try
            {
                var warrior = Resources.Load<CharacterDataSO>(characterResourcePath);
                var heldWeapon = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(weaponPath);
                if (heldWeapon == null) throw new InvalidOperationException("Preview weapon missing: " + weaponPath);
                WeaponStancePoseTableSO.LoadDefault().TryGetById(stanceId, out var weaponPose);
                var actors = new Transform[2];
                var leans = new float[2];
                var displays = new SpriteRenderer[2];
                var sources = new SpriteRenderer[2];
                var controllers = new PlayerCharacterAnimationController[2];
                var type = typeof(PlayerCharacterAnimationController);
                var stateType = type.GetNestedType("AnimationState", BindingFlags.NonPublic);
                var play = type.GetMethod("Play", Flags);
                var tick = type.GetMethod("TickCurrentAnimation", Flags);
                var timer = type.GetField("_frameTimer", Flags);
                for (int side = 0; side < 2; side++)
                {
                    int facing = side == 0 ? -1 : 1;
                    var fake = new GameObject("Inactive animation driver " + side);
                    fake.SetActive(false);
                    fake.transform.SetParent(root.transform, false);
                    var controller = fake.AddComponent<PlayerCharacterAnimationController>();
                    type.GetMethod("Awake", Flags).Invoke(controller, null);
                    type.GetMethod("ApplyCharacterData", Flags).Invoke(controller, new object[] { warrior });
                    play.Invoke(controller, new[] { Enum.Parse(stateType, "Run"), type.GetField("_run", Flags).GetValue(controller) });
                    controllers[side] = controller;
                    sources[side] = fake.GetComponent<SpriteRenderer>();
                    var actor = new GameObject("Warrior facing " + facing).transform;
                    actor.SetParent(root.transform, false);
                    actor.position = new Vector3(2000 + facing * 2, 0, 0);
                    actor.localScale = new Vector3(facing, 1, 1);
                    var display = new GameObject("MovementSprite").AddComponent<SpriteRenderer>();
                    display.transform.SetParent(actor, false);
                    actors[side] = actor;
                    displays[side] = display;
                    var weapons = new GameObject("Visuals").transform;
                    weapons.SetParent(actor, false);
                    // Match gameplay's physical centre half a unit above the floor so lean uses the same pivot.
                    var pivot = new GameObject("HandPivot").transform;
                    pivot.SetParent(weapons, false);
                    pivot.localPosition = new Vector3(-1f / 24, 0f, 0);
                    var holder = new GameObject("WeaponHolder").transform;
                    holder.SetParent(pivot, false);
                    holder.localPosition = weaponPose.LocalPosition + heldWeapon.InHandSpriteLocalOffset;
                    holder.localRotation = Quaternion.Euler(0, 0, weaponPose.LocalEulerZ - heldWeapon.InHandSpriteTiltZ);
                    var weapon = holder.gameObject.AddComponent<SpriteRenderer>();
                    weapon.sprite = heldWeapon.InHandSprite;
                    weapon.flipX = weaponPose.FlipX;
                    weapon.flipY = weaponPose.FlipY;
                    weapon.sortingOrder = weaponPose.SortBehindCharacter ? -1 : 1;
                    actor.gameObject.AddComponent<PlayerRenderAnchor>().Initialize(display.transform, weapons);
                }
                var floor = new GameObject("Floor").AddComponent<SpriteRenderer>();
                floor.transform.SetParent(root.transform);
                floor.transform.position = new Vector3(2000, -0.05f, 0);
                floorSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
                floor.sprite = floorSprite;
                floor.transform.localScale = new Vector3(24, 0.1f, 1);
                floor.color = new Color(0.3f, 0.33f, 0.27f);
                target.Create();
                string folder = Path.Combine(Path.GetTempPath(), "codex-" + warrior.ID.ToLowerInvariant() + "-refresh");
                Directory.CreateDirectory(folder);
                for (int frame = 0; frame < (doubleJump ? 94 : 80); frame++)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        var controller = controllers[side];
                        if (frame == 32) type.GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                        if (doubleJump && frame == 42) type.GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                        if (frame == (doubleJump ? 56 : 46)) play.Invoke(controller, new[] { Enum.Parse(stateType, "Fall"), type.GetField("_fall", Flags).GetValue(controller) });
                        if (frame == (doubleJump ? 74 : 60)) type.GetMethod("HandleLanded", Flags).Invoke(controller, null);
                        if (frame == (doubleJump ? 81 : 67)) play.Invoke(controller, new[] { Enum.Parse(stateType, "Run"), type.GetField("_run", Flags).GetValue(controller) });
                        timer.SetValue(controller, (float)timer.GetValue(controller) + 1f / 24 - Time.deltaTime);
                        tick.Invoke(controller, null);
                        displays[side].sprite = sources[side].sprite;
                        displays[side].transform.localScale = new Vector3(controller.PresentationScale.x, controller.PresentationScale.y, 1f);
                        actors[side].GetComponent<PlayerRenderAnchor>().WeaponPresentationOffset = controller.PresentationWeaponOffset;
                        float airborne = frame >= 32 && frame < 60 ? (frame - 32) / 28f : 0;
                        float height = 4 * 1.5f * airborne * (1 - airborne);
                        if (doubleJump && frame >= 42 && frame < 74)
                        {
                            float secondArc = (frame - 42) / 32f;
                            float startHeight = 6f * (10f / 28) * (1 - 10f / 28);
                            height = startHeight * (1 - secondArc) + 6f * secondArc * (1 - secondArc);
                        }
                        else if (doubleJump && frame >= 74) height = 0f;
                        var position = actors[side].position;
                        position.y = height + 0.5f;
                        actors[side].position = position;
                        // Same full-rectangle foot anchoring used by PlayerMovementVisual at scale 1.
                        displays[side].transform.localPosition = Vector3.up * (-0.5f - displays[side].sprite.bounds.min.y * controller.PresentationScale.y);
                        bool inAir = frame >= 32 && frame < (doubleJump ? 74 : 60);
                        int facing = side == 0 ? -1 : 1;
                        float targetLean = PlayerMovementVisual.CalculateAirborneLean(forwardJump ? facing * 5f : 0f, facing, inAir);
                        leans[side] = inAir ? Mathf.Lerp(leans[side], targetLean, 1f - Mathf.Exp(-PlayerMovementVisual.DefaultAirborneLeanResponse / 24f)) : 0f;
                        actors[side].GetComponent<PlayerRenderAnchor>().PresentationLeanDegrees = leans[side];
                    }
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(folder, outputPrefix + frame.ToString("D2") + ".png"), image.EncodeToPNG());
                }
                Debug.Log("Warrior run, jump, apex hold, fall and landing transitions rendered in both directions: " + folder);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(camera.gameObject);
                if (floorSprite != null) Object.DestroyImmediate(floorSprite);
            }
        }
    }
}
