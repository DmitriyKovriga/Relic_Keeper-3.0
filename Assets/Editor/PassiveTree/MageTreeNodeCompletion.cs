using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Scripts.Editor.PassiveTree
{
    // Targeted content update, intentionally not a tree generator or layout rebuild.
    public static class MageTreeNodeCompletion
    {
        public const string ManaBarrierPath = "Assets/Resources/PassiveTrees/Templates/Misc/NewKeystoneNode 1/ManaBarrier.asset";
        public const string ManaPowerPath = "Assets/Resources/PassiveTrees/Templates/Misc/NewKeystoneNode/keyStoneManaStack.asset";
        public const string CritPath = "Assets/Resources/PassiveTrees/Templates/Misc/NewNotableNode 4/WasteSpellsCrit.asset";
        public const string ChainPath = "Assets/Resources/PassiveTrees/Templates/Misc/NewNotableNode 5/ChainInDamage.asset";
        public const string TreePath = "Assets/Resources/PassiveTrees/MagePassiveSkillTree/MagePassiveSkillTree.asset";

        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Apply authored node data outside Play Mode.");
            string treeBefore = File.ReadAllText(TreePath);
            var barrier = Load(ManaBarrierPath);
            if (barrier.Name == "NewKeystoneNode") barrier.Name = "Mana Barrier";
            barrier.Icon = ImportIcon(Path.ChangeExtension(ManaBarrierPath, ".png"));
            barrier.Description = "Mana absorbs part of incoming damage before Health. One Mana pays one damage; any shortfall reaches Health.";
            barrier.Modifiers.RemoveAll(x => x.Stat == StatType.ManaBarrierAbsorption);
            barrier.Modifiers.Add(new SerializableStatModifier { Stat = StatType.ManaBarrierAbsorption, Value = 50f, Type = StatModType.Flat });
            Save(barrier);

            var power = Load(ManaPowerPath);
            power.Icon = ImportIcon("Assets/Resources/PassiveTrees/Templates/Misc/NewKeystoneNode/ManaPower.png");
            Save(power);

            var crit = Load(CritPath);
            crit.Description = "Specialize critical strikes around skills that use Mystic Shield.";
            crit.HitScalingRules.RemoveAll(x => x.Source == PassiveHitScalingSource.MysticShieldSkill || x.Source == PassiveHitScalingSource.NonMysticShieldSkill);
            crit.HitScalingRules.Add(new PassiveHitScalingRule
            {
                Source = PassiveHitScalingSource.MysticShieldSkill, SourcePerStep = 1f, MaxSteps = 1f,
                ModifiersPerStep = new List<SerializableStatModifier>
                {
                    new SerializableStatModifier { Stat = StatType.CritChance, Value = 50f, Type = StatModType.PercentAdd },
                    new SerializableStatModifier { Stat = StatType.CritMultiplier, Value = 75f, Type = StatModType.Flat }
                }
            });
            crit.HitScalingRules.Add(new PassiveHitScalingRule
            {
                Source = PassiveHitScalingSource.NonMysticShieldSkill, SourcePerStep = 1f, MaxSteps = 1f,
                ModifiersPerStep = new List<SerializableStatModifier>
                {
                    new SerializableStatModifier { Stat = StatType.CritChance, Value = 30f, Type = StatModType.PercentSub }
                }
            });
            Save(crit);

            var chain = Load(ChainPath);
            chain.Description = "Successive chain bounces build damage. The first target receives no bounce bonus.";
            chain.HitScalingRules.RemoveAll(x => x.Source == PassiveHitScalingSource.ChainBounceCount);
            chain.HitScalingRules.Add(new PassiveHitScalingRule
            {
                Source = PassiveHitScalingSource.ChainBounceCount, SourcePerStep = 1f, UseWholeSteps = true, MaxSteps = 4f,
                ModifiersPerStep = new List<SerializableStatModifier>
                {
                    new SerializableStatModifier { Stat = StatType.DamagePhysical, Value = 15f, Type = StatModType.PercentMult },
                    new SerializableStatModifier { Stat = StatType.DamageFire, Value = 15f, Type = StatModType.PercentMult },
                    new SerializableStatModifier { Stat = StatType.DamageCold, Value = 15f, Type = StatModType.PercentMult },
                    new SerializableStatModifier { Stat = StatType.DamageLightning, Value = 15f, Type = StatModType.PercentMult }
                }
            });
            Save(chain);

            // Its authored scaling is already implemented (20% spell damage per maximum charge).
            // Remove the stale manual 15% claim; the existing generated stat text shows actual data.
            var capacity = Load("Assets/Resources/PassiveTrees/Templates/Misc/NewNotableNode 3/NoteBarrierInDamage.asset");
            capacity.Description = "Maximum Mystic Shield capacity also grants spell damage, regardless of remaining charges.";
            Save(capacity);

            var stats = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase);
            stats.GetOrCreateEntry(StatType.ManaBarrierAbsorption);
            Save(stats);
            var defaults = Resources.Load<GlobalBaseStatsSO>(GlobalBaseStatsSO.DefaultResourcesPath);
            if (!defaults.TryGetValue(StatType.ManaBarrierAbsorption, out _))
                defaults.BaseStats.Add(new CharacterDataSO.StatConfig { Type = StatType.ManaBarrierAbsorption, Value = 0f });
            Save(defaults);

            var labels = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels);
            Set(labels, "stats.ManaBarrierAbsorption", "Mana Barrier Absorption", "Поглощение урона маной");
            Set(labels, "passive.node.ManaBarrier.name", "Mana Barrier", "Манабарьер");
            Set(labels, "passive.node.ManaBarrier.description", barrier.Description,
                "Мана поглощает часть получаемого урона до здоровья. 1 ед. маны поглощает 1 ед. урона; при нехватке маны остаток наносится здоровью.");
            Set(labels, "passive.node.WasteSpellsCrit.description", crit.Description,
                "Усиливает критические удары навыков, использующих Мистический щит, ослабляя остальные навыки.");
            Set(labels, "passive.node.ChainInDamage.description", chain.Description,
                "Последовательные отскоки цепи усиливают урон. Первая цель не получает бонус за отскоки.");
            Set(labels, "passive.node.NoteBarrierInDamage.description", capacity.Description,
                "Максимальное число зарядов Мистического щита усиливает урон заклинаний независимо от оставшихся зарядов.");
            Save(labels.SharedData);
            if (File.ReadAllText(TreePath) != treeBefore) throw new InvalidOperationException("Mage tree layout changed unexpectedly.");
            Debug.Log("[Mage Tree] Two icons and three description-only effects completed. Unassigned tree nodes and layout untouched.");
        }

        private static PassiveNodeTemplateSO Load(string path) => AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>(path)
            ?? throw new InvalidOperationException("Missing authored node: " + path);

        private static Sprite ImportIcon(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing icon: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null || sprite.rect.width != 48 || sprite.rect.height != 48) throw new InvalidOperationException("Icon must be native 48x48: " + path);
            return sprite;
        }

        private static void Set(StringTableCollection labels, string key, string en, string ru)
        {
            foreach (string locale in new[] { "en", "ru" })
            {
                var table = labels.GetTable(locale) as StringTable;
                if (table == null) throw new InvalidOperationException("Missing locale: " + locale);
                table.AddEntry(key, locale == "en" ? en : ru);
                Save(table);
            }
        }

        private static void Save(UnityEngine.Object asset) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
    }
}
