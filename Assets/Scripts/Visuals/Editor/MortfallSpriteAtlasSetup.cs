using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace RelicKeeper.Editor.Visuals
{
    public static class MortfallSpriteAtlasSetup
    {
        public const string AtlasPath = "Assets/Visuals/MortfallSprites.spriteatlas";
        public const string GroundPath = "Assets/Resources/Sprites/WallAndGrounds/MortfallDungeon/MortFallAssets/Mortfall-ground-platforms.png";
        public static void Apply()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
            if (atlas == null) { atlas = new SpriteAtlas(); AssetDatabase.CreateAsset(atlas, AtlasPath); }
            var sources = new[] { GroundPath,
                "Assets/Resources/Sprites/WallAndGrounds/MortfallDungeon/MortFallAssets/Mortfall-Walls-tileset_2.png",
                "Assets/Resources/Sprites/WallAndGrounds/MortfallDungeon/MortFallAssets/Mortfall-Walls-tileset_3.png",
                "Assets/Visuals/EnvironmentAnimations/CaveDrip.png" };
            atlas.Remove(atlas.GetPackables());
            atlas.Add(sources.Select(x => AssetDatabase.LoadAssetAtPath<Texture2D>(x)).Where(x => x != null).ToArray());
            var packing = atlas.GetPackingSettings(); packing.padding = 8; packing.enableRotation = false; packing.enableTightPacking = false;
            atlas.SetPackingSettings(packing);
            var texture = atlas.GetTextureSettings(); texture.filterMode = FilterMode.Point; texture.generateMipMaps = false;
            atlas.SetTextureSettings(texture); atlas.SetIncludeInBuild(true);
            var platform = atlas.GetPlatformSettings("DefaultTexturePlatform"); platform.maxTextureSize = 2048;
            platform.textureCompression = TextureImporterCompression.Uncompressed; platform.format = TextureImporterFormat.RGBA32;
            atlas.SetPlatformSettings(platform);
            EditorUtility.SetDirty(atlas); AssetDatabase.SaveAssetIfDirty(atlas);
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
        }
    }
}
