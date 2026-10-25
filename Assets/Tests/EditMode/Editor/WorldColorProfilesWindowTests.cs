using NUnit.Framework;
using RelicKeeper.Editor.Visuals;
using Scripts.Visuals.Atmosphere;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class WorldColorProfilesWindowTests
    {
        private const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        [SetUp]
        public void ResumePreviewRendering() => WorldColorGrading.SetBindingsSuspended(false);

        [Test]
        public void MistDraftsStaySeparateBetweenLocationsAndNeverEditTheSource()
        {
            var hub = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            var dungeon = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            var window = ScriptableObject.CreateInstance<WorldColorProfilesWindow>();
            try
            {
                hub.backgroundMist = 0.1f; dungeon.backgroundMist = 0.17f;
                typeof(WorldColorProfilesWindow).GetMethod("LoadMist", Private).Invoke(window, new object[] { hub });
                var hubDraft = (AtmosphereProfileSO)typeof(WorldColorProfilesWindow).GetField("_mistWorking", Private).GetValue(window);
                hubDraft.backgroundMist = 0.6f;
                typeof(WorldColorProfilesWindow).GetMethod("LoadMist", Private).Invoke(window, new object[] { dungeon });
                var dungeonDraft = (AtmosphereProfileSO)typeof(WorldColorProfilesWindow).GetField("_mistWorking", Private).GetValue(window);
                dungeonDraft.backgroundMist = 0.4f;
                typeof(WorldColorProfilesWindow).GetMethod("LoadMist", Private).Invoke(window, new object[] { hub });
                Assert.That(typeof(WorldColorProfilesWindow).GetField("_mistWorking", Private).GetValue(window), Is.SameAs(hubDraft));
                Assert.That(hubDraft.backgroundMist, Is.EqualTo(0.6f));
                Assert.That(hub.backgroundMist, Is.EqualTo(0.1f));
                Assert.That(dungeon.backgroundMist, Is.EqualTo(0.17f));
                Assert.That(hubDraft.hideFlags.HasFlag(HideFlags.NotEditable), Is.False);
            }
            finally { Object.DestroyImmediate(window); Object.DestroyImmediate(hub); Object.DestroyImmediate(dungeon); }
        }

        [Test]
        public void OneSavePersistsBothColorsAndMistWithoutChangingOtherMistFields()
        {
            var colors = ScriptableObject.CreateInstance<WorldColorProfileSO>();
            var mist = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            string suffix = System.Guid.NewGuid().ToString("N");
            string colorPath = "Assets/__AppearanceColors_" + suffix + ".asset";
            string mistPath = "Assets/__AppearanceMist_" + suffix + ".asset";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            WorldColorProfilesWindow window = null;
            try
            {
                mist.backgroundMist = 0.17f; mist.darkness = 0.37f; mist.limitDepthMistHeight = true;
                UnityEditor.AssetDatabase.CreateAsset(colors, colorPath);
                UnityEditor.AssetDatabase.CreateAsset(mist, mistPath);
                var camera = new GameObject("Appearance save camera", typeof(Camera));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera, scene);
                var controller = camera.AddComponent<AtmosphereController>();
                var fields = new UnityEditor.SerializedObject(controller);
                fields.FindProperty("_hubColorProfile").objectReferenceValue = colors;
                fields.FindProperty("_hubDepthMistProfile").objectReferenceValue = mist;
                fields.ApplyModifiedPropertiesWithoutUndo();
                window = ScriptableObject.CreateInstance<WorldColorProfilesWindow>();
                typeof(WorldColorProfilesWindow).GetField("_controller", Private).SetValue(window, controller);
                typeof(WorldColorProfilesWindow).GetMethod("Load", Private).Invoke(window, new object[] { colors });
                var colorDraft = (WorldColorProfileSO)typeof(WorldColorProfilesWindow).GetField("_working", Private).GetValue(window);
                var mistDraft = (AtmosphereProfileSO)typeof(WorldColorProfilesWindow).GetField("_mistWorking", Private).GetValue(window);
                colorDraft.background.exposure = -0.5f; mistDraft.backgroundMist = 0.42f; mistDraft.limitDepthMistHeight = false;
                mistDraft.layeredParallax = true; mistDraft.parallaxFar = 0.73f;
                mistDraft.localLights = true; mistDraft.localLightStrength = 1.25f;
                mistDraft.ambientAnimations = false; mistDraft.chimneySmokeOpacity = 0.35f; mistDraft.dripFrequency = 1.4f;
                mistDraft.ambientParticles = false; mistDraft.ambientParticleDensity = 0.23f; mistDraft.ambientParticleOpacity = 0.61f;
                mistDraft.surfaceMovementEffects = false; mistDraft.surfaceRunDust = 0.37f; mistDraft.surfaceEffectOpacity = 0.6f; mistDraft.surfaceLandingStrength = 1.3f;
                Assert.That(colors.background.exposure, Is.Zero);
                Assert.That(mist.backgroundMist, Is.EqualTo(0.17f));
                window.SaveAppearance();
                Assert.That(colors.background.exposure, Is.EqualTo(-0.5f));
                Assert.That(mist.backgroundMist, Is.EqualTo(0.42f));
                Assert.That(mist.darkness, Is.EqualTo(0.37f), "Unshown screen-fog fields must be preserved.");
                Assert.That(mist.limitDepthMistHeight, Is.False, "Save must retain full-view coverage.");
                Assert.That(mist.layeredParallax, Is.True);
                Assert.That(mist.parallaxFar, Is.EqualTo(0.73f), "One Save must include background movement settings.");
                Assert.That(mist.localLights, Is.True);
                Assert.That(mist.localLightStrength, Is.EqualTo(1.25f), "Save must also keep local lighting settings.");
                Assert.That(mist.ambientAnimations, Is.False); Assert.That(mist.chimneySmokeOpacity, Is.EqualTo(0.35f));
                Assert.That(mist.dripFrequency, Is.EqualTo(1.4f), "The same Save includes environmental animation settings.");
                Assert.That(mist.ambientParticles, Is.False); Assert.That(mist.ambientParticleDensity, Is.EqualTo(0.23f));
                Assert.That(mist.ambientParticleOpacity, Is.EqualTo(0.61f), "Particle settings use the same location Save.");
                Assert.That(mist.surfaceMovementEffects, Is.False); Assert.That(mist.surfaceRunDust, Is.EqualTo(0.37f));
                Assert.That(mist.surfaceEffectOpacity, Is.EqualTo(0.6f)); Assert.That(mist.surfaceLandingStrength, Is.EqualTo(1.3f));
                StringAssert.Contains("backgroundMist: 0.42", System.IO.File.ReadAllText(mistPath));
                Assert.That(colors.hideFlags, Is.EqualTo(HideFlags.None));
                Assert.That(mist.hideFlags, Is.EqualTo(HideFlags.None));
            }
            finally
            {
                if (window != null) Object.DestroyImmediate(window);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                UnityEditor.AssetDatabase.DeleteAsset(colorPath); UnityEditor.AssetDatabase.DeleteAsset(mistPath);
                if (colors != null) Object.DestroyImmediate(colors);
                if (mist != null) Object.DestroyImmediate(mist);
            }
        }

        [Test]
        public void CompareAndDiscardUseSavedColorsAndMist_ClosingClearsBothOverrides()
        {
            var colors = ScriptableObject.CreateInstance<WorldColorProfileSO>();
            var mist = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var camera = new GameObject("Appearance preview camera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera, scene);
            var controller = camera.AddComponent<AtmosphereController>();
            var fields = new UnityEditor.SerializedObject(controller);
            fields.FindProperty("_hubColorProfile").objectReferenceValue = colors;
            fields.FindProperty("_hubDepthMistProfile").objectReferenceValue = mist;
            fields.ApplyModifiedPropertiesWithoutUndo();
            var window = ScriptableObject.CreateInstance<WorldColorProfilesWindow>();
            try
            {
                mist.backgroundMist = 0.17f;
                typeof(WorldColorProfilesWindow).GetField("_controller", Private).SetValue(window, controller);
                typeof(WorldColorProfilesWindow).GetMethod("Load", Private).Invoke(window, new object[] { colors });
                var colorDraft = (WorldColorProfileSO)typeof(WorldColorProfilesWindow).GetField("_working", Private).GetValue(window);
                var mistDraft = (AtmosphereProfileSO)typeof(WorldColorProfilesWindow).GetField("_mistWorking", Private).GetValue(window);
                colorDraft.characters.exposure = 1f; mistDraft.backgroundMist = 0.6f;
                typeof(WorldColorProfilesWindow).GetMethod("ApplyPreview", Private).Invoke(window, new object[] { true });
                Assert.That(controller.ActiveDepthMistProfile, Is.SameAs(mistDraft), "Live preview must use the temporary mist draft.");
                typeof(WorldColorProfilesWindow).GetField("_showSaved", Private).SetValue(window, true);
                typeof(WorldColorProfilesWindow).GetMethod("ApplyPreview", Private).Invoke(window, new object[] { true });
                Assert.That(controller.ActiveDepthMistProfile, Is.SameAs(mist), "Comparison must show the saved mist preset.");
                Assert.That(controller.ActiveColorProfile, Is.SameAs(colors));
                window.DiscardAppearance();
                Assert.That(controller.ActiveDepthMistProfile.backgroundMist, Is.EqualTo(0.17f));
                Assert.That(controller.ActiveColorProfile.characters.exposure, Is.Zero);
                Object.DestroyImmediate(window); window = null;
                Assert.That(controller.PreviewDepthMistProfile, Is.Null);
                Assert.That(controller.PreviewColorProfile, Is.Null);
            }
            finally
            {
                if (window != null) Object.DestroyImmediate(window);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(colors); Object.DestroyImmediate(mist);
            }
        }

        [Test]
        public void SwitchingProfilesKeepsUnfinishedEditsSeparateFromTheSavedAssets()
        {
            var hub = ScriptableObject.CreateInstance<WorldColorProfileSO>();
            var dungeon = ScriptableObject.CreateInstance<WorldColorProfileSO>();
            var window = ScriptableObject.CreateInstance<WorldColorProfilesWindow>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(WorldColorProfilesWindow);
            try
            {
                type.GetMethod("Load", flags).Invoke(window, new object[] { hub });
                var firstDraft = (WorldColorProfileSO)type.GetField("_working", flags).GetValue(window);
                firstDraft.background.exposure = -1f;
                type.GetMethod("Load", flags).Invoke(window, new object[] { dungeon });
                var secondDraft = (WorldColorProfileSO)type.GetField("_working", flags).GetValue(window);
                secondDraft.environment.saturation = 0.5f;
                type.GetMethod("Load", flags).Invoke(window, new object[] { hub });
                Assert.That(type.GetField("_working", flags).GetValue(window), Is.SameAs(firstDraft));
                Assert.That(firstDraft.background.exposure, Is.EqualTo(-1f));
                Assert.That(hub.background.exposure, Is.Zero);
                Assert.That(dungeon.environment.saturation, Is.EqualTo(1f));
                type.GetMethod("Load", flags).Invoke(window, new object[] { dungeon });
                Assert.That(type.GetField("_working", flags).GetValue(window), Is.SameAs(secondDraft));
            }
            finally { Object.DestroyImmediate(window); Object.DestroyImmediate(hub); Object.DestroyImmediate(dungeon); }
        }

        [Test]
        public void MortfallPreviewCopiesVisualsWithoutRunningGameplayAndReleasesItsScene()
        {
            int previews = UnityEditor.SceneManagement.EditorSceneManager.previewSceneCount;
            string[] rooms = WorldColorPreviewStage.GetMortfallRooms();
            Assert.That(rooms.Length, Is.GreaterThan(1));
            Assert.That(new System.Collections.Generic.HashSet<string>(rooms).Count, Is.EqualTo(rooms.Length));
            using (var stage = WorldColorPreviewStage.Create(true, rooms[0], null))
            {
                Assert.That(stage.Controller.ActiveColorProfile, Is.SameAs(stage.AssignedProfile));
                Assert.That(stage.AssignedProfile, Is.Not.Null);
                Assert.That(stage.Camera.enabled, Is.False);
                int tilemaps = 0;
                foreach (GameObject root in stage.Scene.GetRootGameObjects())
                {
                    tilemaps += root.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true).Length;
                    Assert.That(root.GetComponentsInChildren<Scripts.Dungeon.RoomController>(true), Is.Empty);
                    Assert.That(root.GetComponentsInChildren<Scripts.Enemies.EnemyEntity>(true), Is.Empty);
                    Assert.That(root.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                }
                Assert.That(tilemaps, Is.GreaterThan(0));
                stage.SetView(true, false);
                Assert.That(stage.Camera.orthographicSize, Is.GreaterThanOrEqualTo(5.625f));
            }
            Assert.That(UnityEditor.SceneManagement.EditorSceneManager.previewSceneCount, Is.EqualTo(previews));
        }

        [Test]
        public void MortfallPreviewCanCoexistWithTheGamesGlobalLight()
        {
            var existing = new GameObject("Existing game global light");
            var light = existing.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            try
            {
                using (var stage = WorldColorPreviewStage.Create(true, WorldColorPreviewStage.GetMortfallRooms()[0], null))
                {
                    foreach (GameObject root in stage.Scene.GetRootGameObjects())
                        foreach (var sampleLight in root.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true))
                        {
                            Assert.That(sampleLight.lightType, Is.Not.EqualTo(UnityEngine.Rendering.Universal.Light2D.LightType.Global),
                                "Samples must not register a duplicate ambient/global light.");
                            Assert.That(sampleLight.GetComponent<LocalLight2DSource>(), Is.Not.Null,
                                "Only source-owned local lights may be copied into the preview.");
                        }
                }
                Assert.That(light.lightType, Is.EqualTo(UnityEngine.Rendering.Universal.Light2D.LightType.Global));
            }
            finally { Object.DestroyImmediate(existing); }
        }

        [Test]
        public void ReopeningWindowRecreatesTheEditableCopyForTheRetainedProfile()
        {
            var source = ScriptableObject.CreateInstance<WorldColorProfileSO>();
            var window = ScriptableObject.CreateInstance<WorldColorProfilesWindow>();
            var type = typeof(WorldColorProfilesWindow);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                type.GetMethod("Load", flags).Invoke(window, new object[] { source });
                type.GetMethod("OnDisable", flags).Invoke(window, null);
                type.GetMethod("OnEnable", flags).Invoke(window, null);
                type.GetMethod("ConnectMainCamera", flags).Invoke(window, null);
                var working = (WorldColorProfileSO)type.GetField("_working", flags).GetValue(window);
                Assert.That(working, Is.Not.Null, "A retained Profile field must have editable controls after recompilation.");
                Assert.That(working.hideFlags.HasFlag(HideFlags.NotEditable), Is.False);
            }
            finally { Object.DestroyImmediate(window); Object.DestroyImmediate(source); }
        }

        [Test]
        public void PreviewCopyIsEditableTemporaryAndIndependentOfSavedProfile()
        {
            var source = ScriptableObject.CreateInstance<WorldColorProfileSO>();
            WorldColorProfileSO preview = null;
            try
            {
                preview = WorldColorProfilesWindow.CreateEditablePreview(source);
                Assert.That(preview.hideFlags.HasFlag(HideFlags.NotEditable), Is.False, "NotEditable greys out the controls.");
                Assert.That(preview.hideFlags.HasFlag(HideFlags.DontSaveInEditor), Is.True);
                Assert.That(preview.hideFlags.HasFlag(HideFlags.DontSaveInBuild), Is.True);
                preview.background.exposure = -1f;
                Assert.That(source.background.exposure, Is.Zero, "Preview edits must not overwrite the saved profile.");
            }
            finally
            {
                if (preview != null) Object.DestroyImmediate(preview);
                Object.DestroyImmediate(source);
            }
        }
    }
}
