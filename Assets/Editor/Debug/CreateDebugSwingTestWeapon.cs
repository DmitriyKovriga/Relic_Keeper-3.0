using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;
using Scripts.Stats;

namespace RK.EditorTools.DebugTools
{
    /// <summary>
    /// Upserts the single Debug Blade + DebugSwingSkill set for stance/swing auth testing.
    /// Obtain in Play Mode via X РІвЂ вЂ™ Debug Inventory (ItemDatabase AllItems), not Give menus.
    /// </summary>
    public static class CreateDebugSwingTestWeapon
    {
        private const string RootFolder = "Assets/Resources/Items/Debug";
        private const string TexturePath = RootFolder + "/DebugSwingBlade.png";
        private const string SpriteAssetHint = RootFolder + "/DebugSwingBlade.png";

        private const string SkillPath = RootFolder + "/DebugSwingSkill.asset";
        private const string PoolPath = RootFolder + "/DebugSwingSkillPool.asset";
        private const string WeaponPath = RootFolder + "/Debug Blade.asset";

        private const string ItemDatabasePath = "Assets/Resources/Databases/ItemDatabaseSO.asset";
        private const string CleaveLbPath = "Assets/Resources/Skills/2HWeapon/Axe/LeftButton/CleaveLB.asset";
        private const string CleavePrefabFallback = "Assets/Prefabs/Skills/Skill_Cleave_StepRunner.prefab";

        private static readonly string[] ObsoleteAssetPaths =
        {
            RootFolder + "/DebugSwing_LowGuard.asset",
            RootFolder + "/DebugSwing_Dagger.asset",
            RootFolder + "/DebugSwingTestPool_LowGuard.asset",
            RootFolder + "/DebugSwingTestPool_Dagger.asset",
            RootFolder + "/Debug Swing Blade (LowGuard).asset",
            RootFolder + "/Debug Swing Blade (Dagger).asset",
        };

        // Match real InHand canvases (2HAdventurerSwordHand / SmallAdventurer'sHammerHand: 24x24 @ PPU 24).
        // Grip is painted at canvas center so default Center pivot = handle attach point (same as production).
        private const int TexW = 24;
        private const int TexH = 24;
        private const int Ppu = 24;

        // Debug Blade matches Adventurer's Sword InHand pattern:
        // diagonal PNG bake PngBakeDiagonalZ = -45 (tip upper-right), SO InHandSpriteTiltZ = 0
        // (bake already encodes the diagonal; no extra SO tilt cancel).
        // Apply: finalLocalEulerZ = pose.LocalEulerZ - InHandSpriteTiltZ.
        // Keep PNG bake -45 and 24x24/PPU/center grip.
        private const float PngBakeDiagonalZ = -45f;
        private const float InHandSpriteTiltZOnSo = 0f;

        [MenuItem("Relic Keeper/Debug/Create Or Refresh Debug Blade")]
        public static void CreateOrRefresh()
        {
            EnsureFolder(RootFolder);

            Sprite bladeSprite = CreateOrRefreshBladeSprite();
            GameObject skillPrefab = ResolveSkillPrefab();

            SkillDataSO skill = CreateOrUpdateSkill(skillPrefab);
            SkillPoolSO pool = CreateOrUpdatePool(skill);
            WeaponItemSO weapon = CreateOrUpdateWeapon(bladeSprite, pool);

            DeleteObsoleteAssets();
            RefreshItemDatabase();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[CreateDebugSwingTestWeapon] Debug Blade ready.\n" +
                $"  Sprite: {TexturePath} ({TexW}x{TexH} @ PPU {Ppu}, Center pivot / grip at center, " +
                $"PNG bake {PngBakeDiagonalZ}Р’В°, SO InHandSpriteTiltZ={InHandSpriteTiltZOnSo} -> matches Adventurer's Sword (bake -45, tilt 0))\n" +
                $"  Skill:  {SkillPath} (ID=DebugSwingSkill, HoldStance=Aggressive, SwingStyle=FromStance)\n" +
                $"  Pool:   {PoolPath}\n" +
                $"  Weapon: {WeaponPath} (ID=debug_blade, DropLevel=999)\n" +
                "  Play Mode РІвЂ вЂ™ X РІвЂ вЂ™ Debug Inventory РІвЂ вЂ™ Debug Blade РІвЂ вЂ™ Create РІвЂ вЂ™ equip Main Hand.\n" +
                "  Tune swings: edit DebugSwingSkill HoldStance / SwingStyle in Inspector.");

            Selection.activeObject = weapon;
            EditorGUIUtility.PingObject(weapon);
        }

        private static Sprite CreateOrRefreshBladeSprite()
        {
            // Always rewrite PNG so refresh is idempotent (size/pivot/art stay in sync with this generator).
            WriteBakedTiltBladePng();

            // Force Unity to re-read bytes before importer settings (avoids stale sliced rects).
            AssetDatabase.ImportAsset(
                TexturePath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer != null)
            {
                // Mirror production InHand importers: Sprite/Single, PPU 24, Point, Center pivot.
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = Ppu;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                // Drop leftover sheet entries from older oversized imports (e.g. 6x48).
                importer.spritesheet = System.Array.Empty<SpriteMetaData>();

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spritePixelsPerUnit = Ppu;
                settings.spriteMeshType = SpriteMeshType.Tight;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                settings.filterMode = FilterMode.Point;
                settings.mipmapEnabled = false;
                settings.alphaIsTransparency = true;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteAssetHint);
            if (sprite == null)
            {
                Debug.LogError($"[CreateDebugSwingTestWeapon] Failed to load sprite at {SpriteAssetHint}");
                return null;
            }

            Debug.Log(
                $"[CreateDebugSwingTestWeapon] Sprite ready: rect={sprite.rect.size}, " +
                $"pivot={sprite.pivot}, ppu={sprite.pixelsPerUnit}, bounds={sprite.bounds.size}");
            return sprite;
        }

        /// <summary>
        /// White blade + red tip, grip at canvas center. Painted along ~-45 deg
        /// diagonal (tip upper-right). PNG bake stays -45; SO InHandSpriteTiltZ
        /// stays 0 to match Adventurer's Sword (bake encodes diagonal, no SO cancel).
        /// Unity SetPixel y=0 is bottom.
        /// </summary>
        private static void WriteBakedTiltBladePng()
        {
            // Paint directly along the same ~-45В° diagonal family as 2HAdventurerSwordHand
            // (tip upper-right, pommel lower-left, grip at canvas center). Tip-upв†’rotate
            // of a thin stick clips at corners and leaves a short stub; corner-to-corner
            // draw matches production opaque span (~22x22 @ 24x24).
            // Unity SetPixel y=0 is bottom. Image/PIL y=0 is top вЂ” production ASCII shows
            // tip at top-right, so in Unity pixels tip is high Y + high X.
            var white = new Color32(255, 255, 255, 255);
            var red = new Color32(255, 0, 0, 255);
            var clear = new Color32(0, 0, 0, 0);

            var tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < TexH; y++)
                for (int x = 0; x < TexW; x++)
                    tex.SetPixel(x, y, clear);

            float mid = (TexW - 1) * 0.5f; // 11.5
            // Axis: tip-up (+Y) rotated by PngBakeDiagonalZ (-45) в†’ direction (sin(-45), cos(-45))
            // in Unity XY with Y up: dir в‰€ (-0.707, 0.707) wait вЂ” Z-rot of (0,1):
            // x' = -sin(Оё)*y0 with Оё=-45 в†’ x' = -sin(-45)= +0.707; y' = cos(-45)=0.707
            // So tip direction = (+X, +Y) = upper-right in Unity (and top-right in image).
            float rad = PngBakeDiagonalZ * Mathf.Deg2Rad;
            float dirX = -Mathf.Sin(rad); // +0.707 for -45
            float dirY = Mathf.Cos(rad);  // +0.707
            float nX = -dirY;             // perpendicular (blade thickness)
            float nY = dirX;

            // Param t along axis: t>0 toward tip, t<0 toward pommel. Cover ~В±15 px so spanв‰€22.
            // Regions (approx px along axis): pommel [-14,-9], grip [-5,+3], guard [3,5], blade [5,12], tip [12,15]
            for (int y = 0; y < TexH; y++)
            {
                for (int x = 0; x < TexW; x++)
                {
                    float dx = x - mid;
                    float dy = y - mid;
                    float t = dx * dirX + dy * dirY;   // projection on tip axis
                    float n = dx * nX + dy * nY;       // signed thickness

                    float half;
                    Color32 col = clear;
                    if (t >= 12f)
                    {
                        half = 1.2f; // tip
                        if (Mathf.Abs(n) <= half)
                            col = red;
                    }
                    else if (t >= 5f)
                    {
                        half = 2.0f; // blade
                        if (Mathf.Abs(n) <= half)
                            col = white;
                    }
                    else if (t >= 2.5f)
                    {
                        half = 3.2f; // crossguard
                        if (Mathf.Abs(n) <= half)
                            col = white;
                    }
                    else if (t >= -5f)
                    {
                        half = 1.2f; // grip through pivot (tв‰€0)
                        if (Mathf.Abs(n) <= half)
                            col = white;
                    }
                    else if (t >= -14.5f)
                    {
                        half = 2.0f; // pommel / lower handle
                        if (Mathf.Abs(n) <= half)
                            col = white;
                    }

                    if (col.a > 0)
                        tex.SetPixel(x, y, col);
                }
            }

            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string abs = Path.GetFullPath(TexturePath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs) ?? RootFolder);
            File.WriteAllBytes(abs, png);
        }

        private static GameObject ResolveSkillPrefab()
        {
            var cleave = AssetDatabase.LoadAssetAtPath<SkillDataSO>(CleaveLbPath);
            if (cleave != null && cleave.SkillPrefab != null)
                return cleave.SkillPrefab;

            return AssetDatabase.LoadAssetAtPath<GameObject>(CleavePrefabFallback);
        }

        private static SkillDataSO CreateOrUpdateSkill(GameObject skillPrefab)
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(SkillPath);
            if (skill == null)
            {
                skill = ScriptableObject.CreateInstance<SkillDataSO>();
                AssetDatabase.CreateAsset(skill, SkillPath);
            }

            skill.ID = "DebugSwingSkill";
            skill.SkillName = "Debug Swing";
            skill.DescriptionMode = SkillDescriptionMode.Automatic;
            skill.Description =
                "Slow debug swing for HandPivot / stance auth. " +
                "Defaults: Aggressive + FromStance (matches Adventurer's Sword / Wave Cut for hold-pose compare). " +
                "Tune HoldStance and SwingStyle in the Inspector.";
            skill.IsActive = true;
            skill.Cooldown = 0.1f;
            skill.ManaCost = 0f;
            skill.ActionSpeedMode = SkillActionSpeedMode.Attack;
            skill.SkillSpeedMultiplier = 0.25f;
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee;
            skill.EnablePushback = false;
            skill.PushbackRating = 0f;
            skill.SkillPrefab = skillPrefab;
            skill.AnimationTrigger = "Attack";
            // Aggressive so XРІвЂ вЂ™equip matches Adventurer's Sword / Wave Cut hold compare by default.
            skill.HoldStance = WeaponHoldStance.Aggressive;
            skill.SwingStyle = WeaponSwingStyle.FromStance;

            var cleave = AssetDatabase.LoadAssetAtPath<SkillDataSO>(CleaveLbPath);
            if (cleave != null)
                skill.Recipe = cleave.Recipe;

            EditorUtility.SetDirty(skill);
            return skill;
        }

        private static SkillPoolSO CreateOrUpdatePool(SkillDataSO skill)
        {
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (pool == null)
            {
                pool = ScriptableObject.CreateInstance<SkillPoolSO>();
                AssetDatabase.CreateAsset(pool, PoolPath);
            }

            pool.name = "DebugSwingSkillPool";
            pool.PossibleSkills = new List<SkillPoolSO.SkillWeight>
            {
                new SkillPoolSO.SkillWeight { Skill = skill, Weight = 100 }
            };
            EditorUtility.SetDirty(pool);
            return pool;
        }

        private static WeaponItemSO CreateOrUpdateWeapon(Sprite sprite, SkillPoolSO pool)
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(WeaponPath);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<WeaponItemSO>();
                AssetDatabase.CreateAsset(weapon, WeaponPath);
            }

            weapon.ID = "debug_blade";
            weapon.ItemName = "Debug Blade";
            weapon.Icon = sprite;
            weapon.Width = 1;
            weapon.Height = 2;
            weapon.Slot = EquipmentSlot.MainHand;
            // DropLevel 999: EnemyLootDropService.SelectBaseItem requires DropLevel <= enemy/player level,
            // so this never self-drops from loot/market at normal levels. No other exclude flag exists.
            weapon.DropLevel = 999;
            weapon.AffixPool = null;
            weapon.SkillPool = pool;
            weapon.SkillCount = 1;
            weapon.IsTwoHanded = false;
            weapon.InHandSprite = sprite;
            // Match Adventurer's Sword: PNG bake -45, SO tilt 0.
            weapon.InHandSpriteTiltZ = InHandSpriteTiltZOnSo;
            weapon.MinPhysicalDamage = 4f;
            weapon.MaxPhysicalDamage = 8f;
            weapon.AttacksPerSecond = 0.35f;
            weapon.BaseCritChance = 5f;
            weapon.SecondarySkillPool = null;

            EditorUtility.SetDirty(weapon);
            return weapon;
        }

        private static void DeleteObsoleteAssets()
        {
            foreach (string path in ObsoleteAssetPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(path) == null)
                    continue;

                if (!AssetDatabase.DeleteAsset(path))
                    Debug.LogWarning($"[CreateDebugSwingTestWeapon] Failed to delete obsolete asset: {path}");
                else
                    Debug.Log($"[CreateDebugSwingTestWeapon] Deleted obsolete: {path}");
            }
        }

        private static void RefreshItemDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(ItemDatabasePath);
            if (db == null)
            {
                Debug.LogError($"[CreateDebugSwingTestWeapon] ItemDatabaseSO missing at {ItemDatabasePath}");
                return;
            }

            // Same logic as ItemDatabaseEditor.RefreshDatabase (items portion).
            db.AllItems = FindAssetsByType<EquipmentItemSO>();
            db.AllSkills = FindAssetsByType<SkillDataSO>();

            var affixes = FindAssetsByType<ItemAffixSO>();
            foreach (var affix in affixes)
            {
                string path = AssetDatabase.GetAssetPath(affix);
                string smartID = path.Replace("Assets/", "").Replace(".asset", "");
                if (affix.UniqueID != smartID)
                {
                    affix.UniqueID = smartID;
                    EditorUtility.SetDirty(affix);
                }
            }
            db.AllAffixes = affixes;

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            bool hasBlade = false;
            if (db.AllItems != null)
            {
                foreach (var item in db.AllItems)
                {
                    if (item != null && item.ID == "debug_blade")
                    {
                        hasBlade = true;
                        break;
                    }
                }
            }

            Debug.Log(
                $"[CreateDebugSwingTestWeapon] ItemDatabase refreshed. Items={db.AllItems?.Count ?? 0}, " +
                $"Skills={db.AllSkills?.Count ?? 0}, debug_blade={(hasBlade ? "YES" : "MISSING")}");
        }

        private static List<T> FindAssetsByType<T>() where T : ScriptableObject
        {
            var assets = new List<T>();
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                    assets.Add(asset);
            }
            return assets;
        }

        private static void EnsureFolder(string unityPath)
        {
            if (AssetDatabase.IsValidFolder(unityPath))
                return;

            string[] parts = unityPath.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
