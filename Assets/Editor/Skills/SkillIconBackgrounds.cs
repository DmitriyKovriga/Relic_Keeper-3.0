using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Skills;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class SkillIconBackgrounds
    {
        public const string Work = ".agents-unity-bridge/SkillIconBackgrounds/";
        [Serializable] public class Entry
        {
            public int index;
            public string texture, guid, spriteName;
            public long fileId;
            public int width, height;
            public float transparentFraction, borderOpaqueFraction;
            public string[] skills;
        }
        [Serializable] public class Report { public Entry[] entries; }
        public static Texture2D Read(Sprite sprite)
        {
            var rect = sprite.rect;
            var rt = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(sprite.texture, rt); RenderTexture.active = rt;
                var result = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
                result.ReadPixels(rect, 0, 0); result.Apply(); return result;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); }
        }
        [MenuItem("Tools/Skills/Audit Icon Backgrounds")]
        public static void Audit()
        {
            Directory.CreateDirectory(Work);
            var skills = AssetDatabase.FindAssets("t:SkillDataSO").Select(guid => AssetDatabase.LoadAssetAtPath<SkillDataSO>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(skill => skill != null && skill.Icon != null).ToArray();
            var entries = new List<Entry>();
            foreach (var group in skills.GroupBy(skill => skill.Icon).OrderBy(group => AssetDatabase.GetAssetPath(group.Key)))
            {
                var sprite = group.Key; var image = Read(sprite);
                try
                {
                    var pixels = image.GetPixels32(); int clear = 0, border = 0, opaqueBorder = 0;
                    for (int y = 0; y < image.height; y++) for (int x = 0; x < image.width; x++)
                    {
                        byte alpha = pixels[y * image.width + x].a; if (alpha < 250) clear++;
                        if (x == 0 || y == 0 || x == image.width - 1 || y == image.height - 1) { border++; if (alpha >= 250) opaqueBorder++; }
                    }
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId);
                    var entry = new Entry { index = entries.Count, texture = AssetDatabase.GetAssetPath(sprite), guid = guid, fileId = fileId,
                        spriteName = sprite.name, width = image.width, height = image.height, transparentFraction = (float)clear / pixels.Length,
                        borderOpaqueFraction = (float)opaqueBorder / border, skills = group.Select(skill => AssetDatabase.GetAssetPath(skill)).ToArray() };
                    File.WriteAllBytes(Work + entry.index.ToString("D3") + ".png", image.EncodeToPNG()); entries.Add(entry);
                }
                finally { Object.DestroyImmediate(image); }
            }
            File.WriteAllText(Work + "Audit.json", JsonUtility.ToJson(new Report { entries = entries.ToArray() }, true));
        }
    }
}
