using System.IO;
using System.Linq;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    public static class MortfallSeamDiagnostics
    {
        private static WorldColorPreviewStage _stage;
        public static void Prepare()
        {
            _stage?.Dispose();
            _stage = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, null);
            _stage.FocusAmbient();
            Directory.CreateDirectory("Logs/Seam");
            var sprite = AssetDatabase.LoadAllAssetsAtPath(MortfallSpriteAtlasSetup.GroundPath).OfType<Sprite>().First();
            File.WriteAllText("Logs/Seam/binding.txt", $"Playing={Application.isPlaying}, packed={sprite.packed}, texture={sprite.texture.width}x{sprite.texture.height}");
            var lit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            foreach (var tile in _stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<UnityEngine.Tilemaps.TilemapRenderer>(true)))
            {
                tile.sharedMaterial = lit;
                WorldColorGrading.Bind(tile, Scripts.Visuals.RenderDepthCategory.Environment, true);
            }
        }
        public static void Capture()
        {
            Directory.CreateDirectory("Logs/Seam");
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var target = new RenderTexture(1920, 1080, 24); target.Create(); var previous = RenderTexture.active;
            Vector3 origin = _stage.Camera.transform.position;
            try
            {
                for (int i = 0; i < 8; i++)
                {
                    _stage.Camera.transform.position = origin + new Vector3(i / 192f, i / 192f);
                    RenderPipeline.SubmitRenderRequest(_stage.Camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                    File.WriteAllBytes($"Logs/Seam/Camera-{i}.png", image.EncodeToPNG());
                }
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); Object.DestroyImmediate(target); _stage.Dispose(); _stage = null; }
        }
    }
}
