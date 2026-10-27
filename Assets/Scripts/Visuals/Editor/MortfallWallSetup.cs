using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Scripts.Visuals.Atmosphere;
using Scripts.Visuals.Tiles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    public static class MortfallWallSetup
    {
        public const string RulePath = "Assets/Resources/Sprites/WallAndGrounds/MortfallDungeon/MortFallAssets/MortFallWallRuleTile.asset";
        public const string AtlasPath = "Assets/Visuals/Environment/MortfallWalls/ExposedBottoms.png";
        private static WorldColorPreviewStage _capture;

        public static void PrepareCapture()
        {
            _capture?.Dispose();
            _capture = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, Camera.main?.GetComponent<AtmosphereController>());
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void Capture()
        {
            if (_capture == null) throw new InvalidOperationException("Prepare the wall preview first.");
            var stage = _capture; var old = ScriptableObject.CreateInstance<RuleTile>();
            EditorJsonUtility.FromJsonOverwrite(File.ReadAllText("Logs/WallRules/LegacyRule.json"), old);
            var current = AssetDatabase.LoadAssetAtPath<MortfallWallRuleTile>(RulePath);
            var maps = stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Tilemap>(true)).ToArray();
            var wall = maps.Single(x => x.name == "Background"); var ground = maps.Single(x => x.name == "Ground");
            var platforms = maps.Single(x => x.name == "Platforms");
            foreach (var light in stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<LocalLight2DSource>(true)))
            {
                var fields = new SerializedObject(light); fields.FindProperty("_flame").boolValue = false; fields.ApplyModifiedPropertiesWithoutUndo();
            }
            var profile = Object.Instantiate(stage.Controller.ActiveDepthMistProfile); stage.Controller.PreviewDepthMistProfile = profile;
            profile.ambientAnimations = false; profile.ambientParticles = false; profile.sourceGlow = false;
            var target = new RenderTexture(480, 270, 24); target.Create(); var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            void Render(string name)
            {
                RenderPipeline.SubmitRenderRequest(stage.Camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                File.WriteAllBytes("Logs/WallRules/" + name + ".png", image.EncodeToPNG());
            }
            Vector3Int[] Cells()
            {
                var result = new System.Collections.Generic.List<Vector3Int>();
                foreach (var cell in wall.cellBounds.allPositionsWithin) if (wall.HasTile(cell)) result.Add(cell);
                return result.ToArray();
            }
            void Compare(string name)
            {
                var cells = Cells();
                foreach (var cell in cells) wall.SetTile(cell, old); Render(name + "-Before");
                foreach (var cell in cells) wall.SetTile(cell, current); Render(name + "-After");
            }
            try
            {
                stage.Camera.orthographicSize = 3.5f; stage.Camera.transform.position = new Vector3(-1, 6, -10);
                Compare("Entrance");
                stage.Camera.transform.position = new Vector3(20.5f, 6, -10); Compare("RightSide");
                TileBase groundArt = null, platformArt = null;
                foreach (var cell in ground.cellBounds.allPositionsWithin) if (ground.HasTile(cell)) { groundArt = ground.GetTile(cell); break; }
                foreach (var cell in platforms.cellBounds.allPositionsWithin) if (platforms.HasTile(cell)) { platformArt = platforms.GetTile(cell); break; }
                foreach (var map in maps) map.ClearAllTiles();
                foreach (var sprite in stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SpriteRenderer>(true)))
                    if (sprite.name != "DungeonSharedBackground") sprite.enabled = false;
                for (int group = 0; group < 3; group++)
                {
                    int left = group * 6;
                    for (int x = left; x < left + 3; x++) for (int y = 0; y < 3; y++) wall.SetTile(new Vector3Int(x, y), current);
                    if (group > 0)
                    {
                        var floor = group == 1 ? ground : platforms; var art = group == 1 ? groundArt : platformArt;
                        for (int x = left - 1; x <= left + 3; x++) floor.SetTile(new Vector3Int(x, -1), art);
                    }
                }
                // One-cell opening, narrow hanging pillar, and descending overhang.
                for (int x = 0; x < 3; x++) for (int y = 5; y < 8; y++)
                    if (x != 1 || y != 6) wall.SetTile(new Vector3Int(x, y), current);
                for (int y = 5; y < 9; y++) wall.SetTile(new Vector3Int(6, y), current);
                for (int x = 11; x < 16; x++) for (int y = 4 + (x - 11) / 2; y < 8; y++) wall.SetTile(new Vector3Int(x, y), current);
                stage.Camera.orthographicSize = 5.625f; stage.Camera.transform.position = new Vector3(7.5f, 3.5f, -10);
                Compare("Shapes");
                Vector3 start = stage.Camera.transform.position;
                for (int i = 0; i < 9; i++)
                {
                    stage.Camera.transform.position = start + Vector3.right * ((i - 4) / 24f); Render("Motion-" + i.ToString("00"));
                }
            }
            finally
            {
                RenderTexture.active = previous; stage.Controller.PreviewDepthMistProfile = null;
                Object.DestroyImmediate(old); Object.DestroyImmediate(profile); Object.DestroyImmediate(image); Object.DestroyImmediate(target);
                stage.Dispose(); _capture = null;
            }
        }

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Upgrade wall content outside Play Mode.");
            Directory.CreateDirectory("Logs/WallRules/Before");
            var old = AssetDatabase.LoadAssetAtPath<RuleTile>(RulePath);
            if (!(old is MortfallWallRuleTile))
            {
                File.WriteAllText("Logs/WallRules/LegacyRule.json", EditorJsonUtility.ToJson(old));
                File.Copy(RulePath, "Logs/WallRules/Before/WallRule.asset", true);
                string guid = AssetDatabase.AssetPathToGUID("Assets/Scripts/Visuals/Tiles/MortfallWallRuleTile.cs");
                string text = File.ReadAllText(RulePath);
                text = Regex.Replace(text, @"m_Script: \{fileID: 11500000, guid: [0-9a-f]+, type: 3\}",
                    "m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}");
                text = text.Replace("Unity.2D.Tilemap.Extras::UnityEngine.RuleTile", "Assembly-CSharp::Scripts.Visuals.Tiles.MortfallWallRuleTile");
                File.WriteAllText(RulePath, text); AssetDatabase.ImportAsset(RulePath, ImportAssetOptions.ForceUpdate);
            }
            var rule = AssetDatabase.LoadAssetAtPath<MortfallWallRuleTile>(RulePath);
            if (rule == null) throw new InvalidOperationException("The existing wall asset could not be upgraded.");
            if (rule.m_TilingRules.Any(x => x.m_RuleTransform != RuleTile.TilingRuleOutput.Transform.Fixed || x.m_Output != RuleTile.TilingRuleOutput.OutputSprite.Single))
                throw new InvalidOperationException("Mortfall's wall patterns must use fixed single sprites.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false; importer.SetTextureSettings(settings);
            if (!MortfallWallRuleTile.BottomMasks.All(mask => importer.spritesheet.Any(x => x.name == "Bottom_" + mask.ToString("D3"))))
                importer.spritesheet = MortfallWallRuleTile.BottomMasks.Select((mask, i) => new SpriteMetaData
                {
                    name = "Bottom_" + mask.ToString("D3"), rect = new Rect(i % 4 * 24, 240 - (i / 4 + 1) * 24, 24, 24),
                    alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * 0.5f
                }).ToArray();
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToDictionary(x => x.name);
            rule.ConfigureBottoms(MortfallWallRuleTile.BottomMasks.Select(mask => sprites["Bottom_" + mask.ToString("D3")]).ToArray());
            EditorUtility.SetDirty(rule); AssetDatabase.SaveAssets();
            try
            {
                foreach (string path in WorldColorPreviewStage.GetMortfallRooms())
                {
                    string backup = "Logs/WallRules/Before/" + Path.GetFileName(path);
                    if (!File.Exists(backup)) File.Copy(path, backup);
                    WorldColorGrading.SuspendBindings();
                    var room = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var maps = room.GetComponentsInChildren<Tilemap>(true);
                        var wall = maps.Single(x => x.name == "Background");
                        var support = wall.GetComponent<MortfallWallSupport>() ?? wall.gameObject.AddComponent<MortfallWallSupport>();
                        support.Configure(maps.Single(x => x.name == "Ground"), maps.Single(x => x.name == "Platforms"));
                        WorldColorGrading.SuspendBindings(); PrefabUtility.SaveAsPrefabAsset(room, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(room); }
                }
            }
            finally { WorldColorGrading.RefreshRegisteredScenes(); }
        }
    }
}
