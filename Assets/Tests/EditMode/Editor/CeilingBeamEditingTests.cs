using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Visuals;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class CeilingBeamEditingTests
    {
        private const string Room = "Assets/Tests/EditMode/Editor/CeilingBeamEditingTestRoom.prefab";
        [SetUp]
        public void SetUp()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            Assert.That(AssetDatabase.CopyAsset(LocalLightingSetup.RoomPath, Room), Is.True);
            // Keep the fixture deterministic while level designers freely add beams to the real room.
            var room = PrefabUtility.LoadPrefabContents(Room);
            try
            {
                var beams = room.GetComponentsInChildren<LocalLightShaftSource>(true);
                foreach (var extra in beams.Skip(1)) UnityEngine.Object.DestroyImmediate(extra.gameObject);
                if (beams.Length > 1) PrefabUtility.SaveAsPrefabAsset(room, Room);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }
        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Room);

        [Test]
        public void ShapeAndPositionPreviewImmediately_CompareAndRoomSwitchPreserveTheDraft()
        {
            var edits = new CeilingBeamEditing(); LocalLightShaftSource.Appearance original, changed;
            Vector3 position;
            using (var stage = WorldColorPreviewStage.Create(true, Room, null))
            {
                var source = stage.Shafts.Single(); original = source.ReadAppearance();
                var draft = edits.Get(Room, stage).Single();
                changed = original; changed.size = new Vector2(2, 1.3f); changed.angle = 17; changed.opacity = 0.3f;
                changed.softness = 2.3f; changed.endFade = 0.6f; changed.dust = 0.2f; changed.tint = new Color(0.7f, 0.8f, 1);
                draft.appearance = changed; draft.position += new Vector3(0.25f, -0.125f); position = draft.position;
                edits.Apply(Room, stage, false);
                Assert.That(source.ReadAppearance(), Is.EqualTo(changed)); Assert.That(source.transform.localPosition, Is.EqualTo(position));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Room).GetComponentInChildren<LocalLightShaftSource>(true).ReadAppearance(), Is.EqualTo(original), "A preview must not write the room asset.");
                edits.Apply(Room, stage, true); Assert.That(source.ReadAppearance(), Is.EqualTo(original));
                Assert.That(edits.HasChanges(Room), Is.True, "Comparing must keep pending edits.");
                edits.Apply(Room, stage, false); Assert.That(source.ReadAppearance(), Is.EqualTo(changed));
            }
            using (var stage = WorldColorPreviewStage.Create(true, Room, null))
            {
                edits.Apply(Room, stage, false);
                Assert.That(stage.Shafts.Single().ReadAppearance(), Is.EqualTo(changed));
                Assert.That(stage.Shafts.Single().transform.localPosition, Is.EqualTo(position));
                edits.Discard(Room);
            }
            using (var stage = WorldColorPreviewStage.Create(true, Room, null))
                Assert.That(edits.Get(Room, stage).Single().appearance, Is.EqualTo(original));
        }

        [Test]
        public void SaveWritesNestedPrefabOverridesAndReclips_AndKeepsOtherLights()
        {
            var edits = new CeilingBeamEditing(); LocalLightShaftSource.Appearance changed; Vector3 position;
            int fillCount = AssetDatabase.LoadAssetAtPath<GameObject>(Room).GetComponentsInChildren<LocalLight2DSource>(true).Length;
            using (var stage = WorldColorPreviewStage.Create(true, Room, null))
            {
                var draft = edits.Get(Room, stage).Single(); changed = draft.appearance;
                changed.size = new Vector2(2.4f, 1.2f); changed.angle = -12; changed.opacity = 0.23f; changed.tint = Color.cyan;
                draft.appearance = changed; draft.position += Vector3.right * 0.375f; position = draft.position;
                edits.Apply(Room, stage, false); edits.Save(Room);
                Assert.That(edits.HasChanges(Room), Is.False);
            }
            var room = PrefabUtility.LoadPrefabContents(Room);
            try
            {
                var source = room.GetComponentsInChildren<LocalLightShaftSource>(true).Single();
                Assert.That(source.ReadAppearance(), Is.EqualTo(changed)); Assert.That(source.transform.localPosition, Is.EqualTo(position));
                var maps = room.GetComponentsInChildren<Tilemap>(true).Where(x => x.GetComponent<TilemapCollider2D>() != null).ToArray();
                Assert.That(source.Visibility, Is.EqualTo(CeilingBeamVisibility.Bake(maps, source)));
                Assert.That(room.GetComponentsInChildren<LocalLight2DSource>(true).Length, Is.EqualTo(fillCount));
                Assert.That(EditorUtility.IsPersistent(source.Artwork), Is.True);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }

        [Test]
        public void ExternalRoomChangeIsDetectedBeforeAnyBeamWrite()
        {
            var edits = new CeilingBeamEditing();
            using (var stage = WorldColorPreviewStage.Create(true, Room, null))
            {
                var draft = edits.Get(Room, stage).Single(); var change = draft.appearance; change.angle = 25; draft.appearance = change;
                var room = PrefabUtility.LoadPrefabContents(Room);
                try
                {
                    var source = room.GetComponentInChildren<LocalLightShaftSource>(true);
                    var other = source.ReadAppearance(); other.angle = -30; source.ApplyAppearance(other);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(source);
                    PrefabUtility.SaveAsPrefabAsset(room, Room);
                }
                finally { PrefabUtility.UnloadPrefabContents(room); }
                Assert.Throws<System.InvalidOperationException>(() => edits.Save(Room));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Room).GetComponentInChildren<LocalLightShaftSource>(true).Angle, Is.EqualTo(-30));
                Assert.That(edits.HasChanges(Room), Is.True);
            }
        }

        [Test]
        public void AppearanceWindowSaveAndDiscardIncludeTheSelectedRoomsBeam()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var window = ScriptableObject.CreateInstance<WorldColorProfilesWindow>();
            try
            {
                var stage = WorldColorPreviewStage.Create(true, Room, null);
                typeof(WorldColorProfilesWindow).GetField("_stage", flags).SetValue(window, stage);
                typeof(WorldColorProfilesWindow).GetField("_environment", flags).SetValue(window, WorldColorProfilesWindow.PreviewEnvironment.Mortfall);
                typeof(WorldColorProfilesWindow).GetField("_rooms", flags).SetValue(window, new[] { Room });
                var edits = (CeilingBeamEditing)typeof(WorldColorProfilesWindow).GetField("_beamEdits", flags).GetValue(window);
                var draft = edits.Get(Room, stage).Single(); var saved = draft.appearance; saved.angle = 11; draft.appearance = saved;
                edits.Apply(Room, stage, false); window.SaveAppearance();
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Room).GetComponentInChildren<LocalLightShaftSource>(true).Angle, Is.EqualTo(11));
                var unsaved = saved; unsaved.angle = -22; draft.appearance = unsaved; edits.Apply(Room, stage, false);
                window.DiscardAppearance();
                var reloaded = (WorldColorPreviewStage)typeof(WorldColorProfilesWindow).GetField("_stage", flags).GetValue(window);
                Assert.That(reloaded.Shafts.Single().Angle, Is.EqualTo(11));
                Assert.That(edits.HasChanges(Room), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void EachBeamKeepsItsOwnInspectorAndPreviewSettings_AfterSaveAndReload()
        {
            var room = PrefabUtility.LoadPrefabContents(Room);
            try
            {
                var first = room.GetComponentInChildren<LocalLightShaftSource>(true);
                var secondObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LocalLightShaftSetup.PrefabPath), room.scene);
                secondObject.transform.SetParent(room.transform, false); secondObject.name = first.name;
                secondObject.transform.localPosition = first.transform.localPosition + Vector3.right * 2;
                PrefabUtility.SaveAsPrefabAsset(room, Room);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
            var edits = new CeilingBeamEditing();
            using (var stage = WorldColorPreviewStage.Create(true, Room, null))
            {
                var drafts = edits.Get(Room, stage); Assert.That(drafts.Count, Is.EqualTo(2));
                var first = drafts[0].appearance; first.opacity = 0.37f; first.followEnvironment = false; first.visible = true;
                first.dust = 0.6f; first.size = new Vector2(1.7f, 0.9f); first.strength = 2.2f;
                first.dustOpacity = 0.3f; first.dustSpeed = 1.7f; drafts[0].appearance = first;
                edits.Apply(Room, stage, false);
                room = PrefabUtility.LoadPrefabContents(Room);
                LocalLightShaftSource.Appearance second;
                try
                {
                    var source = room.GetComponentsInChildren<LocalLightShaftSource>(true)[1];
                    second = source.ReadAppearance(); second.opacity = 0.22f; second.angle = 32; second.visible = false;
                    var point = source.transform.localPosition + CeilingBeamControls.AnchorOffset(source) + Vector3.up * 0.25f;
                    CeilingBeamControls.Commit(source, second, point);
                    PrefabUtility.SaveAsPrefabAsset(room, Room);
                    Undo.FlushUndoRecordObjects();
                    Undo.ClearUndo(source); Undo.ClearUndo(source.transform);
                }
                finally { PrefabUtility.UnloadPrefabContents(room); }
                edits.Save(Room);
                room = PrefabUtility.LoadPrefabContents(Room);
                var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
                try
                {
                    var beams = room.GetComponentsInChildren<LocalLightShaftSource>(true);
                    Assert.That(beams[0].ReadAppearance(), Is.EqualTo(first));
                    Assert.That(beams[1].ReadAppearance(), Is.EqualTo(second), "Saving the first beam's preview must keep the second beam's Inspector edits.");
                    profile.localShaftStrength = 0.074f; profile.localShaftDust = 0;
                    Assert.That(beams[0].Strength(profile), Is.EqualTo(1)); Assert.That(beams[0].DustDensity(profile), Is.EqualTo(0.6f));
                    Assert.That(beams[1].Strength(profile), Is.Zero); Assert.That(beams[1].DustDensity(profile), Is.Zero);
                    profile.localShafts = false; Assert.That(beams[0].Strength(profile), Is.Zero, "The environment switch remains a master switch.");
                }
                finally { PrefabUtility.UnloadPrefabContents(room); UnityEngine.Object.DestroyImmediate(profile); }
            }
        }

        [Test]
        public void PrefabSceneViewRendersTheBeamWithoutANearbyGameCamera()
        {
            var previousStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (previousStage != null && previousStage.scene.isDirty) Assert.Ignore("Keep the user's unsaved prefab work.");
            string previousPath = previousStage != null ? previousStage.assetPath : null;
            var stage = PrefabStageUtility.OpenPrefab(Room);
            var source = stage.prefabContentsRoot.GetComponentsInChildren<LocalLightShaftSource>(true).Single();
            var appearance = source.ReadAppearance();
            var cameraObject = new GameObject("Beam Scene view probe", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, stage.scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = stage.scene; camera.enabled = false;
            camera.cameraType = CameraType.SceneView; camera.orthographic = true; camera.orthographicSize = 5.625f; camera.aspect = 480f / 270f;
            camera.transform.position = source.SourcePosition + new Vector3(0.6f, -2.5f, -10);
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false); var previousTarget = RenderTexture.active;
            Color32[] Render()
            {
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply(); return image.GetPixels32();
            }
            try
            {
                Assert.That(CeilingBeamScenePreview.Resolve(source, camera), Is.Not.Null);
                var hidden = appearance; hidden.opacity = 0; source.ApplyAppearance(hidden); var before = Render();
                var visible = appearance; visible.opacity = 0.8f; source.ApplyAppearance(visible); var after = Render();
                Assert.That(after.Where((pixel, i) => pixel.r > before[i].r + 3 || pixel.g > before[i].g + 3 || pixel.b > before[i].b + 3).Count(), Is.GreaterThan(30));
                System.IO.Directory.CreateDirectory("Logs/BeamEditing"); System.IO.File.WriteAllBytes("Logs/BeamEditing/SceneView.png", image.EncodeToPNG());
                camera.cameraType = CameraType.Game;
                Assert.That(CeilingBeamScenePreview.Resolve(source, camera), Is.Null, "The editor fallback must never leak into game cameras.");
            }
            finally
            {
                RenderTexture.active = previousTarget; source.ApplyAppearance(appearance);
                UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
                // The probe never changes the saved room; leave the prefab stage clean before returning.
                typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] { stage.scene });
                if (previousPath != null) PrefabStageUtility.OpenPrefab(previousPath); else StageUtility.GoToMainStage();
            }
        }
    }
}
