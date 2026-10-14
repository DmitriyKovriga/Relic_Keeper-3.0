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

        public static void CaptureMageRuntimeViews()
        {
            Capture("Assets/Resources/Items/2HWeapon/Staff/Lightning/Adventurer/Lightning Staff Of Adventurer.asset", "staff", "Mage-staff-runtime-", false, true, "Heroes/MageResources/Mage");
        }

        public static void CaptureMageWandRuntimeViews()
        {
            Capture("Assets/Resources/Items/1H Weapon/1HWand/MysticAdventurer'sWand.asset", "default", "Mage-wand-runtime-", false, true, "Heroes/MageResources/Mage");
        }

        public static void CaptureMageDoubleJumpRuntimeViews()
        {
            Capture("Assets/Resources/Items/2HWeapon/Staff/Lightning/Adventurer/Lightning Staff Of Adventurer.asset", "staff", "Mage-double-runtime-", true, true, "Heroes/MageResources/Mage");
        }

        public static void CaptureJumpPuffRuntimeViews()
        {
            Capture("Assets/Scripts/Items/Weapon/LowTier/TwoHandedWeapon/LowTierBases/AxeOfSettler/Axe of Settler.asset", "default", "Jump-puff-runtime-", true, true, "Heroes/WarriorResources/Warrior", true);
        }

        public static void CaptureVerticalJumpPuffRuntimeViews()
        {
            Capture("Assets/Scripts/Items/Weapon/LowTier/TwoHandedWeapon/LowTierBases/AxeOfSettler/Axe of Settler.asset", "default", "Vertical-jump-runtime-", false, false, "Heroes/WarriorResources/Warrior", true);
        }

        public static void CaptureGroundDashRuntimeViews()
        {
            Capture("Assets/Scripts/Items/Weapon/LowTier/TwoHandedWeapon/LowTierBases/AxeOfSettler/Axe of Settler.asset", "default", "Ground-dash-runtime-", false, false, "Heroes/WarriorResources/Warrior", false, true);
        }

        public static void CaptureAirDodgeRuntimeViews()
        {
            Capture("Assets/Scripts/Items/Weapon/LowTier/TwoHandedWeapon/LowTierBases/AxeOfSettler/Axe of Settler.asset", "default", "Air-dodge-runtime-", false, true, "Heroes/WarriorResources/Warrior", false, false, true);
        }

        public static void CaptureDashJumpRuntimeViews()
        {
            Capture("Assets/Scripts/Items/Weapon/LowTier/TwoHandedWeapon/LowTierBases/AxeOfSettler/Axe of Settler.asset", "default", "Dash-jump-runtime-", false, true, "Heroes/WarriorResources/Warrior", false, false, false, true);
        }

        private static void Capture(string weaponPath, string stanceId, string outputPrefix, bool doubleJump = false, bool forwardJump = false,
            string characterResourcePath = "Heroes/WarriorResources/Warrior", bool jumpPuffs = false, bool groundDashPreview = false, bool airDodgePreview = false, bool dashJumpPreview = false)
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
                var puffs = new System.Collections.Generic.List<GroundingSpriteSheetVfx>();
                var dashTrails = new DashJumpTrail[2];
                var dashMovements = new PlayerMovement[2];
                if (dashJumpPreview)
                for (int side = 0; side < 2; side++)
                {
                    var driver = new GameObject("Inactive dash jump driver");
                    driver.SetActive(false);
                    driver.transform.SetParent(root.transform);
                    dashMovements[side] = driver.AddComponent<PlayerMovement>();
                    typeof(PlayerMovement).GetField("_rb", Flags).SetValue(dashMovements[side], driver.GetComponent<Rigidbody2D>());
                    typeof(PlayerMovement).GetField("_isDashJump", Flags).SetValue(dashMovements[side], true);
                    dashTrails[side] = driver.AddComponent<DashJumpTrail>();
                }
                var ghosts = new System.Collections.Generic.Dictionary<DodgeAfterImageGhost, int>();
                PlayerAttackInput afterimageDriver = null;
                if (groundDashPreview || airDodgePreview)
                {
                    var driver = new GameObject("Inactive afterimage driver");
                    driver.SetActive(false);
                    driver.transform.SetParent(root.transform);
                    afterimageDriver = driver.AddComponent<PlayerAttackInput>();
                    typeof(PlayerAttackInput).GetField("_isGroundDash", Flags).SetValue(afterimageDriver, groundDashPreview);
                }
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
                        if (groundDashPreview)
                            position.x = 2000 + (side == 0 ? -1 : 1) * (2f + Mathf.Clamp(frame - 9, 0, 4) * 0.22f);
                        if (airDodgePreview)
                            position.x = 2000 + (side == 0 ? -1 : 1) * (2f + Mathf.Clamp(frame - 37, 0, 4) * 0.22f);
                        if (dashJumpPreview)
                            position.x = 2000 + (side == 0 ? -1 : 1) * (2f + Mathf.Clamp(frame - 32, 0, 9) * 0.25f);
                        if (jumpPuffs && forwardJump)
                            position.x = 2000 + (side == 0 ? -1 : 1) * (2f + Mathf.Clamp(frame - 32, 0, 9) * 0.18f);
                        position.y = height + 0.5f;
                        actors[side].position = position;
                        if (jumpPuffs && (frame == 32 || frame == 42))
                        {
                            float width = frame == 32 ? 0.75f : 0.99f;
                            var puff = new GameObject("Preview jump puff");
                            puff.transform.SetParent(root.transform);
                            puff.transform.position = new Vector3(position.x, height + width * (frame == 32 ? 0.5f : 0.5f - AirJumpPuffSprites.ContactHeightRatio), 0);
                            var overlay = puff.AddComponent<GroundingSpriteSheetVfx>();
                            overlay.Initialize(frame == 32 ? (forwardJump ? JumpPuffSprites.Frames : VerticalJumpPuffSprites.Frames) : AirJumpPuffSprites.Frames,
                                frame == 32 ? 0.32f : 0.48f, 0.9f, displays[side].sortingLayerID, 3, width, frame == 32 && forwardJump && side == 1);
                            puffs.Add(overlay);
                        }
                        // Same full-rectangle foot anchoring used by PlayerMovementVisual at scale 1.
                        displays[side].transform.localPosition = Vector3.up * (-0.5f - displays[side].sprite.bounds.min.y * controller.PresentationScale.y);
                        if (groundDashPreview)
                        {
                            float amount = frame >= 10 && frame < 14 ? 1f : frame >= 14 ? Mathf.Exp(-(frame - 13) * 18f / 24f) : 0f;
                            Vector2 compression = Vector2.Lerp(Vector2.one, PlayerMovementVisual.GroundDashScale, amount);
                            displays[side].transform.localScale = new Vector3(compression.x, compression.y, 1);
                            displays[side].transform.localPosition = Vector3.up * (-0.5f - displays[side].sprite.bounds.min.y * compression.y);
                            actors[side].GetComponent<PlayerRenderAnchor>().WeaponPresentationOffset = PlayerMovementVisual.AdjustDashGrip(controller.PresentationWeaponOffset, compression);
                            if (frame == 10)
                            {
                                var puff = new GameObject("Preview dash push off");
                                puff.transform.SetParent(root.transform);
                                puff.transform.position = new Vector3(position.x - (side == 0 ? -1 : 1) * 0.244f, 0.55f, 0);
                                var overlay = puff.AddComponent<GroundingSpriteSheetVfx>();
                                overlay.Initialize(GroundDashPuffSprites.Frames, 0.8f, 0.95f, displays[side].sortingLayerID, 3, 1.08f, side == 0);
                                puffs.Add(overlay);
                            }
                            if (frame == 10 || frame == 12)
                            {
                                typeof(PlayerAttackInput).GetField("_playerRenderers", Flags).SetValue(afterimageDriver, actors[side].GetComponentsInChildren<SpriteRenderer>());
                                typeof(PlayerAttackInput).GetMethod("SpawnAfterImageSnapshot", Flags).Invoke(afterimageDriver, null);
                                foreach (var ghost in Object.FindObjectsByType<DodgeAfterImageGhost>(FindObjectsSortMode.None))
                                    if (!ghosts.ContainsKey(ghost)) { ghosts.Add(ghost, frame); ghost.transform.SetParent(root.transform, true); }
                            }
                            foreach (var pair in ghosts)
                                foreach (var renderer in pair.Key.GetComponentsInChildren<SpriteRenderer>())
                                {
                                    float alpha = Mathf.Max(0, 1f - (frame - pair.Value) / (24f * 0.3f));
                                    renderer.color = new Color(0.86f, 0.93f, 0.95f, 0.5f * alpha);
                                }
                            foreach (var puff in puffs)
                            {
                                var sprite = puff.GetComponent<SpriteRenderer>();
                                float age = (frame - 10) / 24f;
                                sprite.enabled = age >= 0 && age < 0.8f;
                                if (sprite.enabled) sprite.sprite = GroundDashPuffSprites.Frames[Mathf.Clamp((int)(age / 0.8f * GroundDashPuffSprites.Frames.Length), 0, GroundDashPuffSprites.Frames.Length - 1)];
                            }
                        }
                        if (airDodgePreview)
                        {
                            if (frame == 38 || frame == 40)
                            {
                                typeof(PlayerAttackInput).GetField("_playerRenderers", Flags).SetValue(afterimageDriver, actors[side].GetComponentsInChildren<SpriteRenderer>());
                                typeof(PlayerAttackInput).GetMethod("SpawnAfterImageSnapshot", Flags).Invoke(afterimageDriver, null);
                                foreach (var ghost in Object.FindObjectsByType<DodgeAfterImageGhost>(FindObjectsSortMode.None))
                                    if (!ghosts.ContainsKey(ghost)) { ghosts.Add(ghost, frame); ghost.transform.SetParent(root.transform, true); }
                            }
                            foreach (var pair in ghosts)
                                foreach (var renderer in pair.Key.GetComponentsInChildren<SpriteRenderer>())
                                    renderer.color = new Color(0.86f, 0.93f, 0.95f, 0.4f * Mathf.Max(0, 1f - (frame - pair.Value) / (24f * 0.16f)));
                        }
                        if (dashJumpPreview && frame >= 32)
                        {
                            var trail = dashTrails[side];
                            trail.transform.position = actors[side].position;
                            dashMovements[side].GetComponent<Rigidbody2D>().linearVelocity = new Vector2((side == 0 ? -1 : 1)*6f, frame < 46 ? 5f : -3f);
                            if (frame == 32)
                            {
                                trail.Begin();
                                typeof(DashJumpTrail).GetField("_body", Flags).SetValue(trail, null);
                                typeof(DashJumpTrail).GetField("_visual", Flags).SetValue(trail, displays[side]);
                                var puff = new GameObject("Dash jump launch fan");
                                puff.transform.SetParent(root.transform);
                                puff.transform.position = new Vector3(position.x-(side==0?-1:1)*0.3f, 0.76f, 0);
                                var overlay = puff.AddComponent<GroundingSpriteSheetVfx>();
                                overlay.Initialize(DashJumpPuffSprites.Frames,0.65f,0.95f,displays[side].sortingLayerID,-1,1.5f,side == 0);
                                puffs.Add(overlay);
                            }
                            trail.Sample(Time.time + (frame-32)/24f, new Vector2((side==0?-1:1)*6f,5f), actors[side].position, true);
                            foreach (var puff in puffs)
                            {
                                var renderer = puff.GetComponent<SpriteRenderer>();
                                float age = (frame-32)/24f;
                                renderer.enabled = age<0.65f;
                                if(renderer.enabled) renderer.sprite=DashJumpPuffSprites.Frames[Mathf.Clamp((int)(age/0.65f*DashJumpPuffSprites.Frames.Length),0,DashJumpPuffSprites.Frames.Length-1)];
                            }
                        }
                        bool inAir = frame >= 32 && frame < (doubleJump ? 74 : 60);
                        int facing = side == 0 ? -1 : 1;
                        float targetLean = PlayerMovementVisual.CalculateAirborneLean(forwardJump ? facing * 5f : 0f, facing, inAir);
                        leans[side] = inAir ? Mathf.Lerp(leans[side], targetLean, 1f - Mathf.Exp(-PlayerMovementVisual.DefaultAirborneLeanResponse / 24f)) : 0f;
                        actors[side].GetComponent<PlayerRenderAnchor>().PresentationLeanDegrees = leans[side];
                    }
                    if (jumpPuffs)
                    {
                        int age = frame >= 42 ? frame - 42 : frame - 32;
                        foreach (var puff in puffs)
                        {
                            var sprite = puff.GetComponent<SpriteRenderer>();
                            float seconds = age / 24f;
                            sprite.enabled = seconds >= 0 && seconds < (frame >= 42 ? 0.48f : 0.32f);
                            if (sprite.enabled) sprite.sprite = (frame >= 42 ? AirJumpPuffSprites.Frames : forwardJump ? JumpPuffSprites.Frames : VerticalJumpPuffSprites.Frames)[Mathf.Clamp((int)(seconds / (frame >= 42 ? 0.48f : 0.32f) * 8), 0, 7)];
                        }
                        if (frame == 41) { foreach (var puff in puffs) puff.GetComponent<SpriteRenderer>().enabled = false; puffs.Clear(); }
                    }
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(folder, outputPrefix + frame.ToString("D2") + ".png"), image.EncodeToPNG());
                }
                if (dashJumpPreview) foreach(var trail in dashTrails) trail.Clear();
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
