using System.Linq;
using Scripts.Dungeon;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.Hub
{
    public static class DungeonNpcArrivalBuilder
    {
        [MenuItem("Tools/Hub/Build Dungeon NPC Arrival Assets")]
        public static void Build()
        {
            const string path = "Assets/Resources/Dungeons/ServiceNpcArrival.asset";
            var settings = AssetDatabase.LoadAssetAtPath<DungeonServiceArrivalSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<DungeonServiceArrivalSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }
            settings.MarketWalk = ImportWalk("Market");
            settings.StashWalk = ImportWalk("Warehouse");
            var portal = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Spawner/NextRoomPortal.prefab");
            settings.PortalSprite = portal.GetComponent<SpriteRenderer>().sprite;
            settings.PortalController = portal.GetComponent<Animator>().runtimeAnimatorController;
            settings.SpriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(Hub20Builder.ArtRoot + "/HubArt.mat");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Dungeon NPC arrival configured with existing dungeon portal and native walk sprites.");
        }

        private static Sprite[] ImportWalk(string name)
        {
            string path = Hub20Builder.ArtRoot + "/Characters/" + name + "NpcWalk.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var options = new TextureImporterSettings();
            importer.ReadTextureSettings(options);
            options.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(options);
            importer.spritesheet = Enumerable.Range(0, 9).Select(i => new SpriteMetaData
            {
                name = name + "NpcWalk_" + i.ToString("D2"), rect = new Rect(i * 28, 0, 28, 28),
                alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f)
            }).ToArray();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name).ToArray();
        }

    }
}
