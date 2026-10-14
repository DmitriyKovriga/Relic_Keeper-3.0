using System;
using System.IO;
using Scripts.Items;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class AdventurerDaggerVisualBuilder
    {
        private const string Root = "Assets/Scripts/Items/Weapon/LowTier/OneHandedWeapon/Daggers/Adventurer's Dagger/";

        public static void Apply()
        {
            string path = Root + "AdventurerDaggerHeld.png";
            File.Copy("Docs/Art/Weapons/AdventurerDaggerHeldSource.png", path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            // Native grip centre, in bottom-left pixel coordinates. No painted hand.
            settings.spritePivot = new Vector2(12.5f / 20f, 15.5f / 20f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(Root + "Adventurer's Dagger.asset");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (weapon == null || sprite == null) throw new InvalidOperationException("Adventurer dagger held sprite import failed.");
            weapon.InHandSprite = sprite;
            // Reference art points down; compensate for the stance's tip-up convention.
            weapon.InHandSpriteTiltZ = 180f;
            // Align the reference's hooked grip with the hand, rather than the chest.
            weapon.InHandSpriteLocalOffset = Vector2.up * (3f / 24f);
            EditorUtility.SetDirty(weapon);
            AssetDatabase.SaveAssets();
            Debug.Log("Adventurer's Dagger now has its PixelLab held sprite and native grip pivot.");
        }
    }
}
