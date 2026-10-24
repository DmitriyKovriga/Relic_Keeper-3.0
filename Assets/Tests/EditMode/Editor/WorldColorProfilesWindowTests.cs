using NUnit.Framework;
using RelicKeeper.Editor.Visuals;
using Scripts.Visuals.Atmosphere;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class WorldColorProfilesWindowTests
    {
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
                        Assert.That(root.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true), Is.Empty,
                            "Flat color samples must not register lights or depend on game-scene ambient light.");
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
