using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Hub;
using RelicKeeper.Editor.Visuals;
using Scripts.Visuals;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class SurfaceMovementTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Test]
        public void LandingHasQuietThresholdGrowingResponseAndHardVisualCap()
        {
            var profile = ScriptableObject.CreateInstance<GroundSurfaceProfileSO>();
            try
            {
                Assert.That(profile.EvaluateLanding(1.9f, false).visible, Is.False);
                Assert.That(profile.EvaluateLanding(12, false, 0).visible, Is.False);
                var light = profile.EvaluateLanding(3, false); var strong = profile.EvaluateLanding(12, false);
                Assert.That(strong.width, Is.GreaterThan(light.width)); Assert.That(strong.opacity, Is.GreaterThan(light.opacity));
                Assert.That(profile.EvaluateLanding(12, true).width, Is.GreaterThan(strong.width));
                Assert.That(profile.EvaluateLanding(10000, true, 2).width, Is.EqualTo(profile.landingWidth * profile.maxImpactScale));
            }
            finally { Object.DestroyImmediate(profile); }
        }
        [Test]
        public void FootstepsRequireTravelSpeedTimeAndNonzeroDensity()
        {
            var profile = ScriptableObject.CreateInstance<GroundSurfaceProfileSO>();
            try
            {
                Assert.That(profile.ShouldStep(0, 5, 4, 1, false), Is.False);
                Assert.That(profile.ShouldStep(20, 5, 0, 1, false), Is.False);
                Assert.That(profile.ShouldStep(20, 0, 4, 1, false), Is.False);
                Assert.That(profile.ShouldStep(20, 5, 4, 0, false), Is.False);
                Assert.That(profile.ShouldStep(2, 1, 4, 1, false), Is.True);
                Assert.That(profile.ShouldStep(1.599f, 1, 5, 1, false), Is.True, "Sub-pixel travel error must not miss a visible foot contact.");
                Assert.That(profile.ShouldStep(1.55f, 1, 5, 1, false), Is.False, "Contact tolerance cannot bypass real stride spacing.");
                Assert.That(profile.ShouldStep(2, 1, 4, 0.25f, false), Is.False);
                Assert.That(profile.ShouldStep(1.7f, 1, 4, 1, true), Is.False);
            }
            finally { Object.DestroyImmediate(profile); }
        }
        [Test]
        public void SavedEarthAndStoneUsePixelLabClipsAndEveryPhysicalFloorIsAssigned()
        {
            foreach (string path in new[] { SurfaceMovementSetup.EarthPath, SurfaceMovementSetup.StonePath })
            {
                var profile = AssetDatabase.LoadAssetAtPath<GroundSurfaceProfileSO>(path); Assert.That(profile, Is.Not.Null);
                foreach (var frames in new[] { profile.stepFrames, profile.landingFrames })
                {
                    Assert.That(frames.Length, Is.EqualTo(8)); Assert.That(frames.All(x => x != null && x.pixelsPerUnit == 24), Is.True);
                    Assert.That(frames.All(x => x.pivot == x.rect.size * 0.5f), Is.True);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frames[0]));
                    Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
                }
            }
            var hub = AssetDatabase.LoadAssetAtPath<GameObject>(Hub20Builder.EnvironmentPath);
            Assert.That(hub.transform.Find("GroundCollision").GetComponent<GroundSurfaceVisuals>().Profile,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<GroundSurfaceProfileSO>(SurfaceMovementSetup.EarthPath)));
            int count = 0;
            foreach (string path in WorldColorPreviewStage.GetMortfallRooms())
            {
                var room = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var map in room.GetComponentsInChildren<Tilemap>(true).Where(x => x.GetComponent<TilemapCollider2D>() != null))
                {
                    Assert.That(map.GetComponent<GroundSurfaceVisuals>()?.Profile,
                        Is.SameAs(AssetDatabase.LoadAssetAtPath<GroundSurfaceProfileSO>(SurfaceMovementSetup.StonePath)), path + "/" + map.name);
                    count++;
                }
            }
            Assert.That(count, Is.GreaterThanOrEqualTo(20));
        }
        [Test]
        public void ActualSupportSwitchesProfiles_PrefersTheLedge_AndRejectsFarFloorDust()
        {
            using (var actor = new Actor())
            {
                Assert.That(actor.Call("ResolveSurfaceProfile"), Is.SameAs(actor.Earth));
                actor.Root.transform.position = new Vector3(1010, 0.5f); Physics2D.SyncTransforms();
                Assert.That(actor.Call("ResolveSurfaceProfile"), Is.SameAs(actor.Stone));
                actor.Root.transform.position = new Vector3(1003.1f, 0.5f); Physics2D.SyncTransforms();
                Assert.That(actor.Call("ResolveSurfaceProfile"), Is.SameAs(actor.Earth), "One supported foot must beat the stone floor three units below.");
                actor.Root.transform.position = new Vector3(1010, 4); Physics2D.SyncTransforms();
                Assert.That(actor.Call("ResolveSurfaceProfile"), Is.Null);
                Assert.That(((Vector3)actor.Call("ResolveGroundEffectPosition", 0f)).y, Is.GreaterThan(3), "Never snap an airborne contact effect to a distant floor.");
            }
        }
        [Test]
        public void SelectedSurfaceDrivesRealSpawnedArt_BoundedImpact_AndVisibilitySwitches()
        {
            using (var actor = new Actor())
            {
                // Match the player's narrow footprint; broad actors can already reach the world-size cap on light contacts.
                actor.Root.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 1);
                Physics2D.SyncTransforms();
                actor.Call("SpawnRunDust", false);
                var step = actor.Effects.Single().GetComponent<SpriteRenderer>();
                Assert.That(step.sprite, Is.SameAs(actor.Earth.stepFrames[0])); Assert.That(step.bounds.min.y, Is.InRange(0, 1f / 24));
                Assert.That(step.sortingLayerName, Is.EqualTo(WorldRenderSorting.Settings.LayerDefault));
                Assert.That(WorldColorGrading.ResolveRole(step, RenderDepthCategory.Environment, out _, out bool combatEffect), Is.EqualTo(WorldColorRole.Environment));
                Assert.That(combatEffect, Is.False, "Physical dust shares environment grading and torch lighting.");
                actor.ClearEffects(); actor.Set("_peakFallSpeed", 3f); actor.Set("_previousVerticalSpeed", -3f);
                actor.Call("SpawnLandingOrHeavyImpact", false);
                float small = actor.Effects.Single().GetComponent<SpriteRenderer>().bounds.size.x;
                actor.ClearEffects(); actor.Set("_peakFallSpeed", 20f); actor.Set("_previousVerticalSpeed", -3f);
                actor.Call("SpawnLandingOrHeavyImpact", true);
                var landing = actor.Effects.Single().GetComponent<SpriteRenderer>();
                Assert.That(landing.sprite, Is.SameAs(actor.Earth.landingFrames[0]));
                Assert.That(landing.bounds.size.x, Is.GreaterThan(small)); Assert.That(landing.bounds.size.x, Is.LessThanOrEqualTo(actor.Earth.maxWorldWidth + 0.001f));
                actor.ClearEffects(); actor.Appearance.surfaceMovementEffects = false; actor.Call("SpawnRunDust", false);
                Assert.That(actor.Effects, Is.Empty);
                actor.Appearance.surfaceMovementEffects = true; actor.Appearance.surfaceEffectOpacity = 0; actor.Call("SpawnRunDust", false);
                Assert.That(actor.Effects, Is.Empty);
                actor.Appearance.surfaceEffectOpacity = 1;
                actor.Root.transform.position = new Vector3(1010, 0.5f); Physics2D.SyncTransforms(); actor.Call("SpawnRunDust", false);
                Assert.That(actor.Effects.Single().GetComponent<SpriteRenderer>().sprite, Is.SameAs(actor.Stone.stepFrames[0]));
                Assert.That(actor.Root.GetComponent<BoxCollider2D>().size, Is.EqualTo(new Vector2(0.5f, 1)));
            }
        }
        [Test]
        public void RunningDensityZeroSuppressesSteps_AndDisableResetsPendingMotion()
        {
            using (var actor = new Actor())
            {
                actor.Call("ResolveSurfaceProfile"); actor.Set("_wasGrounded", true); actor.Set("_hasRunDustSample", true);
                actor.Set("_lastRunDustX", 998f); actor.Set("_lastSurfaceStepTime", Time.time - 1);
                actor.Root.GetComponent<Rigidbody2D>().linearVelocity = Vector2.right * 4;
                actor.Appearance.surfaceRunDust = 0; actor.Call("UpdateGroundingEffects"); Assert.That(actor.Effects, Is.Empty);
                actor.Appearance.surfaceRunDust = 1; actor.Call("UpdateGroundingEffects"); Assert.That(actor.Effects.Length, Is.EqualTo(1));
                actor.Set("_peakFallSpeed", 25f); actor.Set("_airborneTime", 2f); actor.Call("OnDisable");
                Assert.That(actor.Get("_peakFallSpeed"), Is.EqualTo(0)); Assert.That(actor.Get("_airborneTime"), Is.EqualTo(0));
                Assert.That(actor.Get("_hasRunDustSample"), Is.False); Assert.That(actor.Get("_lastContactSurface"), Is.Null);
            }
        }
        [Test]
        public void SameStepLandingAndJumpUseOneLandingAndTheGroundTakeoffArt()
        {
            using (var actor = new Actor())
            {
                var move = actor.Root.AddComponent<PlayerMovement>();
                actor.Set("_playerMovement", move);
                typeof(PlayerMovement).GetField("<LastLandingSpeed>k__BackingField", Private).SetValue(move, 8f);
                actor.Set("_wasGrounded", false);
                actor.Call("HandleLanded");
                Assert.That(actor.Get("_wasGrounded"), Is.True);
                Assert.That(actor.Effects.Length, Is.EqualTo(1));
                Assert.That(actor.Effects[0].GetComponent<SpriteRenderer>().sprite, Is.SameAs(actor.Earth.landingFrames[0]));
                actor.Call("HandleJumpStarted");
                Assert.That(actor.Effects.Length, Is.EqualTo(2));
                Assert.That(actor.Effects.Any(x => x.GetComponent<SpriteRenderer>().sprite == actor.Earth.stepFrames[0]), Is.True,
                    "A buffered ground jump must not use the air-jump puff because LateUpdate never saw the contact.");
                actor.Call("UpdateGroundingEffects");
                Assert.That(actor.Effects.Length, Is.EqualTo(2), "LateUpdate cannot repeat the contact event.");
            }
        }
        [Test]
        public void PreviewLoopsContactsWithoutPhysics_AndHonorsGroundEffectControls()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            using (var stage = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, null))
            {
                var draft = Object.Instantiate(stage.Controller.ActiveDepthMistProfile);
                try
                {
                    stage.Controller.PreviewDepthMistProfile = draft; draft.surfaceMovementEffects = true; draft.surfaceRunDust = 1;
                    stage.FocusGroundMovement(); stage.SampleGroundMovement(true, 0.07f);
                    var effect = stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<GroundingSpriteSheetVfx>(true)).Single();
                    Assert.That(effect.enabled, Is.False); Assert.That(effect.GetComponent<SpriteRenderer>().enabled, Is.True);
                    Assert.That(effect.GetComponent<SpriteRenderer>().sprite.texture, Is.SameAs(AssetDatabase.LoadAssetAtPath<GroundSurfaceProfileSO>(SurfaceMovementSetup.StonePath).stepFrames[0].texture));
                    draft.surfaceRunDust = 0; stage.SampleGroundMovement(true, 0.07f); Assert.That(effect.GetComponent<SpriteRenderer>().enabled, Is.False);
                    stage.SampleGroundMovement(true, 2.5f); Assert.That(effect.GetComponent<SpriteRenderer>().enabled, Is.True);
                    draft.surfaceMovementEffects = false; stage.SampleGroundMovement(true, 2.5f); Assert.That(effect.GetComponent<SpriteRenderer>().enabled, Is.False);
                    Assert.That(stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Collider2D>(true)), Is.Empty);
                }
                finally { stage.Controller.PreviewDepthMistProfile = null; Object.DestroyImmediate(draft); }
            }
        }

        private sealed class Actor : IDisposable
        {
            private readonly GameObject _container;
            private readonly Sprite _sprite;
            public readonly GameObject Root;
            public readonly GroundingVisualController Controller;
            public readonly AtmosphereProfileSO Appearance;
            public GroundSurfaceProfileSO Earth => AssetDatabase.LoadAssetAtPath<GroundSurfaceProfileSO>(SurfaceMovementSetup.EarthPath);
            public GroundSurfaceProfileSO Stone => AssetDatabase.LoadAssetAtPath<GroundSurfaceProfileSO>(SurfaceMovementSetup.StonePath);
            public GroundingSpriteSheetVfx[] Effects => _container.GetComponentsInChildren<GroundingSpriteSheetVfx>(true);
            public Actor()
            {
                _container = new GameObject("Surface regression objects");
                Floor(1000, -0.5f, 6, Earth); Floor(1010, -0.5f, 6, Stone); Floor(1003, -3.5f, 16, Stone);
                Root = new GameObject("Surface actor"); Root.SetActive(false); Root.transform.SetParent(_container.transform);
                Root.transform.position = new Vector3(1000, 0.5f);
                Root.AddComponent<BoxCollider2D>().size = Vector2.one;
                Root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
                Root.AddComponent<SpriteRenderer>().sprite = _sprite;
                Controller = Root.AddComponent<GroundingVisualController>(); Root.SetActive(true); Call("Awake");
                var camera = new GameObject("Surface controls", typeof(Camera)); camera.transform.SetParent(_container.transform);
                var atmosphere = camera.AddComponent<AtmosphereController>(); Appearance = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
                atmosphere.Profile = Appearance; Set("_atmosphere", atmosphere); Physics2D.SyncTransforms();
            }
            private void Floor(float x, float y, float width, GroundSurfaceProfileSO profile)
            {
                var floor = new GameObject("Surface floor"); floor.transform.SetParent(_container.transform); floor.transform.position = new Vector3(x, y);
                floor.layer = LayerMask.NameToLayer("Ground"); floor.AddComponent<BoxCollider2D>().size = new Vector2(width, 1);
                floor.AddComponent<GroundSurfaceVisuals>().Configure(profile);
            }
            public object Call(string name, params object[] args) => typeof(GroundingVisualController).GetMethod(name, Private).Invoke(Controller, args);
            public void Set(string name, object value) => typeof(GroundingVisualController).GetField(name, Private).SetValue(Controller, value);
            public object Get(string name) => typeof(GroundingVisualController).GetField(name, Private).GetValue(Controller);
            public void ClearEffects() { foreach (var effect in Effects) Object.DestroyImmediate(effect.gameObject); }
            public void Dispose()
            {
                var shadow = (GameObject)Get("_shadowObject"); if (shadow != null) Object.DestroyImmediate(shadow); Set("_shadowObject", null);
                Object.DestroyImmediate(_container); Object.DestroyImmediate(_sprite); Object.DestroyImmediate(Appearance);
            }
        }
    }
}
