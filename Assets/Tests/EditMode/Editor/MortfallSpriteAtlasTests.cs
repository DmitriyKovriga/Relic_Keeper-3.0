using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class MortfallSpriteAtlasTests
    {
        [Test]
        public void TerrainAndDropsHavePaddedPointFilteredAtlasIncludedInBuild()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(MortfallSpriteAtlasSetup.AtlasPath);
            Assert.That(atlas, Is.Not.Null);
            var packing = atlas.GetPackingSettings(); var texture = atlas.GetTextureSettings();
            Assert.That(packing.padding, Is.GreaterThanOrEqualTo(8)); Assert.That(packing.enableRotation || packing.enableTightPacking, Is.False);
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(texture.generateMipMaps, Is.False);
            Assert.That(new SerializedObject(atlas).FindProperty("m_EditorData.bindAsDefault").boolValue, Is.True);
            foreach (string path in new[] { MortfallSpriteAtlasSetup.GroundPath, EnvironmentAnimationSetup.ArtRoot + "/CaveDrip.png" })
                foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                    Assert.That(atlas.CanBindTo(sprite), Is.True, sprite.name);
        }

        [UnityTest]
        public IEnumerator FractionalCameraPositionsCannotSamplePixelsOutsideTheRimSprite()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var source = new Texture2D(2, 2); source.LoadImage(File.ReadAllBytes(MortfallSpriteAtlasSetup.GroundPath));
            var sprite = AssetDatabase.LoadAllAssetsAtPath(MortfallSpriteAtlasSetup.GroundPath).OfType<Sprite>().Single(x => x.name == "Mortfall-ground-platforms_21");
            var body = new GameObject("Rim edge regression", typeof(SpriteRenderer)); SceneManager.MoveGameObjectToScene(body, scene);
            var renderer = body.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            var cameraObject = new GameObject("Fractional camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 4f / 3; camera.aspect = 2;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.allowMSAA = false;
            var target = new RenderTexture(512, 256, 24); target.Create();
            var image = new Texture2D(512, 256, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                yield return null;
                var originals = source.GetPixels32(); var rect = sprite.rect; var bounds = renderer.bounds;
                float unit = camera.orthographicSize * 2 / 256;
                for (int offset = 0; offset < 8; offset++)
                {
                    camera.transform.position = new Vector3(offset / 192f, offset / 192f, -10);
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 512, 256), 0, 0); image.Apply();
                    var pixels = image.GetPixels32(); int checkedPixels = 0;
                    for (int y = 70; y < 186; y++) for (int x = 198; x < 314; x++)
                    {
                        float fx = (camera.transform.position.x + (x + 0.5f - 256) * unit - bounds.min.x) * 24;
                        float fy = (camera.transform.position.y + (y + 0.5f - 128) * unit - bounds.min.y) * 24;
                        if (fx < 0 || fx >= 24 || fy < 0 || fy >= 24) continue;
                        // The one-pixel border is where adjacent source-sheet rows used to leak in.
                        if (fx > 1 && fx < 23 && fy > 1 && fy < 23) continue;
                        var actual = pixels[y * 512 + x]; bool match = false;
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        {
                            int sx = (int)rect.x + Mathf.Clamp(Mathf.FloorToInt(fx) + dx, 0, 23);
                            int sy = (int)rect.y + Mathf.Clamp(Mathf.FloorToInt(fy) + dy, 0, 23);
                            var expected = originals[sy * source.width + sx];
                            if (expected.a == 0) expected = new Color32(0, 0, 0, 255);
                            match |= Mathf.Abs(actual.r - expected.r) <= 2 && Mathf.Abs(actual.g - expected.g) <= 2 && Mathf.Abs(actual.b - expected.b) <= 2;
                        }
                        Assert.That(match, Is.True, $"Foreign texel at camera step {offset}, pixel {x},{y}: {actual}"); checkedPixels++;
                    }
                    Assert.That(checkedPixels, Is.GreaterThan(100));
                }
            }
            finally
            {
                RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(source); Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            }
        }
    }
}
