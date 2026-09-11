using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Enemies;
using Scripts.Items;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor.Enemy
{
    public sealed class EnemyLootEditorWindow : EditorWindow
    {
        private const float PreviewSize = 52f;

        private readonly List<EnemyDataSO> _enemies = new List<EnemyDataSO>();
        private readonly Dictionary<EnemyDataSO, Sprite> _previewCache = new Dictionary<EnemyDataSO, Sprite>();
        private readonly List<CraftingOrbSO> _orbs = new List<CraftingOrbSO>();
        private ItemDatabaseSO _itemDatabase;
        private Vector2 _scroll;
        private string _search = string.Empty;
        private bool _showOrbChances = true;

        [MenuItem("Tools/Enemy Loot Settings")]
        public static void Open()
        {
            var window = GetWindow<EnemyLootEditorWindow>();
            window.titleContent = new GUIContent("Enemy Loot");
            window.minSize = new Vector2(820f, 540f);
            window.Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            _itemDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(EditorPaths.ItemDatabase);
            _enemies.Clear();
            _orbs.Clear();
            _previewCache.Clear();

            foreach (string guid in AssetDatabase.FindAssets("t:EnemyDataSO"))
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (enemy != null)
                    _enemies.Add(enemy);
            }

            _enemies.Sort((left, right) => string.Compare(
                ResolveName(left),
                ResolveName(right),
                StringComparison.OrdinalIgnoreCase));

            foreach (string guid in AssetDatabase.FindAssets("t:CraftingOrbSO"))
            {
                var orb = AssetDatabase.LoadAssetAtPath<CraftingOrbSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (orb != null)
                    _orbs.Add(orb);
            }
            _orbs.Sort((left, right) => string.Compare(left.ID, right.ID, StringComparison.OrdinalIgnoreCase));
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawBaseChances();
            DrawOrbDropChances();
            DrawEnemyList();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Search", GUILayout.Width(44f));
            _search = GUILayout.TextField(_search, EditorStyles.toolbarTextField, GUILayout.Width(230f));
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                Refresh();
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(55f)))
                AssetDatabase.SaveAssets();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBaseChances()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Base drop chances per enemy", EditorStyles.boldLabel);

            if (_itemDatabase == null)
            {
                EditorGUILayout.HelpBox($"Item database was not found at {EditorPaths.ItemDatabase}.", MessageType.Error);
                return;
            }

            EditorGUI.BeginChangeCheck();
            float common = DrawPercentSlider("Common (white)", _itemDatabase.CommonItemDropChance);
            float magic = DrawPercentSlider("Magic (blue)", _itemDatabase.MagicItemDropChance);
            float rare = DrawPercentSlider("Rare", _itemDatabase.RareItemDropChance);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_itemDatabase, "Change enemy loot chances");
                _itemDatabase.CommonItemDropChance = common;
                _itemDatabase.MagicItemDropChance = magic;
                _itemDatabase.RareItemDropChance = rare;
                EditorUtility.SetDirty(_itemDatabase);
            }

            float total = common + magic + rare;
            EditorGUILayout.LabelField($"Total chance at multiplier 1: {total * 100f:0.##}%", EditorStyles.miniLabel);
            if (total > 1f)
                EditorGUILayout.HelpBox("The combined base chance exceeds 100%. Lower-priority common drops can be crowded out.", MessageType.Warning);

            EditorGUILayout.HelpBox(
                "Base XP is the reward for a level 1 enemy. Enemy-level scaling and dungeon XP modifiers are applied at runtime; no dungeon modifier means x1.",
                MessageType.Info);

            EditorGUILayout.Space(10f);
        }

        private void DrawOrbDropChances()
        {
            _showOrbChances = EditorGUILayout.Foldout(
                _showOrbChances,
                $"Crafting currency drop chances ({_orbs.Count})",
                true,
                EditorStyles.foldoutHeader);
            if (!_showOrbChances)
            {
                EditorGUILayout.Space(6f);
                return;
            }

            EditorGUILayout.HelpBox(
                "Each currency rolls independently. Enemy Loot multiplier and the room drop-chance modifier affect these rates.",
                MessageType.Info);

            float combinedChance = 0f;
            foreach (CraftingOrbSO orb in _orbs)
            {
                if (orb == null)
                    continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(28f));
                Rect iconRect = GUILayoutUtility.GetRect(20f, 20f, GUILayout.Width(20f), GUILayout.Height(20f));
                if (orb.Icon != null && orb.Icon.texture != null)
                    GUI.DrawTextureWithTexCoords(iconRect, orb.Icon.texture, ResolveTextureCoordinates(orb.Icon), true);
                else
                    EditorGUI.DrawRect(iconRect, Color.white);

                GUILayout.Space(5f);
                string currencyId = string.IsNullOrWhiteSpace(orb.ID) ? orb.name : orb.ID;
                EditorGUILayout.LabelField(
                    ObjectNames.NicifyVariableName(currencyId).Replace("Relic Of ", "Relic of "),
                    GUILayout.Width(180f));

                EditorGUI.BeginChangeCheck();
                float chance = EditorGUILayout.Slider(Mathf.Clamp01(orb.BaseDropChance), 0f, 1f);
                EditorGUILayout.LabelField($"{chance * 100f:0.###}%", GUILayout.Width(70f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(orb, "Change crafting currency drop chance");
                    orb.BaseDropChance = chance;
                    EditorUtility.SetDirty(orb);
                }
                combinedChance += chance;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.LabelField(
                $"Expected currency drops per enemy at multiplier 1: {combinedChance * 100f:0.###}%",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(8f);
        }

        private void DrawEnemyList()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("Sprite", EditorStyles.boldLabel, GUILayout.Width(PreviewSize));
            GUILayout.Label("Enemy", EditorStyles.boldLabel, GUILayout.MinWidth(220f));
            GUILayout.Label(new GUIContent("Base XP", "Experience from a level 1 enemy at dungeon multiplier x1."), EditorStyles.boldLabel, GUILayout.Width(90f));
            GUILayout.Label("Loot multiplier", EditorStyles.boldLabel, GUILayout.Width(135f));
            GUILayout.Label(new GUIContent("Item chance", "Chance of any equipment item at room multiplier x1."), EditorStyles.boldLabel, GUILayout.Width(90f));
            GUILayout.Label(new GUIContent("Currency chance", "Chance of at least one crafting currency at room multiplier x1."), EditorStyles.boldLabel, GUILayout.Width(105f));
            EditorGUILayout.EndHorizontal();

            string normalizedSearch = _search?.Trim();
            IEnumerable<EnemyDataSO> visible = string.IsNullOrEmpty(normalizedSearch)
                ? _enemies
                : _enemies.Where(enemy => ResolveName(enemy).IndexOf(normalizedSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          (enemy.ID ?? string.Empty).IndexOf(normalizedSearch, StringComparison.OrdinalIgnoreCase) >= 0);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (EnemyDataSO enemy in visible)
                DrawEnemyRow(enemy);
            EditorGUILayout.EndScrollView();
        }

        private void DrawEnemyRow(EnemyDataSO enemy)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(PreviewSize + 8f));

            Rect previewRect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
            Sprite preview = ResolvePreview(enemy);
            if (preview != null && preview.texture != null)
                GUI.DrawTextureWithTexCoords(previewRect, preview.texture, ResolveTextureCoordinates(preview), true);
            else
                EditorGUI.DrawRect(previewRect, new Color(0.18f, 0.18f, 0.18f, 1f));

            EditorGUILayout.BeginVertical(GUILayout.MinWidth(220f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(ResolveName(enemy), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(enemy.ID) ? enemy.name : enemy.ID, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.Space(8f);

            EditorGUI.BeginChangeCheck();
            float baseExperience = EditorGUILayout.FloatField(Mathf.Max(0f, enemy.XPReward), GUILayout.Width(90f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(enemy, "Change enemy base experience");
                enemy.XPReward = Mathf.Max(0f, baseExperience);
                EditorUtility.SetDirty(enemy);
            }

            EditorGUI.BeginChangeCheck();
            float multiplier = EditorGUILayout.FloatField(Mathf.Max(0f, enemy.LootDropMultiplier), GUILayout.Width(135f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(enemy, "Change enemy loot multiplier");
                enemy.LootDropMultiplier = Mathf.Max(0f, multiplier);
                EditorUtility.SetDirty(enemy);
            }

            float totalChance = _itemDatabase != null
                ? Mathf.Clamp01((_itemDatabase.CommonItemDropChance + _itemDatabase.MagicItemDropChance + _itemDatabase.RareItemDropChance) * Mathf.Max(0f, enemy.LootDropMultiplier))
                : 0f;
            EditorGUILayout.LabelField($"{totalChance * 100f:0.##}%", GUILayout.Width(90f));
            EditorGUILayout.LabelField(
                $"{CalculateAnyCurrencyChance(enemy.LootDropMultiplier) * 100f:0.##}%",
                GUILayout.Width(105f));
            EditorGUILayout.EndHorizontal();
        }

        private float CalculateAnyCurrencyChance(float lootMultiplier)
        {
            float noneChance = 1f;
            foreach (CraftingOrbSO orb in _orbs)
            {
                if (orb == null)
                    continue;
                float chance = Mathf.Clamp01(orb.BaseDropChance * Mathf.Max(0f, lootMultiplier));
                noneChance *= 1f - chance;
            }
            return 1f - noneChance;
        }

        private static float DrawPercentSlider(string label, float value)
        {
            EditorGUILayout.BeginHorizontal();
            float result = EditorGUILayout.Slider(label, Mathf.Clamp01(value), 0f, 1f);
            EditorGUILayout.LabelField($"{result * 100f:0.##}%", GUILayout.Width(62f));
            EditorGUILayout.EndHorizontal();
            return result;
        }

        private Sprite ResolvePreview(EnemyDataSO enemy)
        {
            if (_previewCache.TryGetValue(enemy, out Sprite cached))
                return cached;

            Sprite sprite = null;
            if (enemy.Prefab != null)
                sprite = enemy.Prefab.GetComponentInChildren<SpriteRenderer>(true)?.sprite;

            if (sprite == null && enemy.Animation != null && !string.IsNullOrWhiteSpace(enemy.Animation.IdleSpritesResourcePath))
            {
                Sprite[] frames = Resources.LoadAll<Sprite>(enemy.Animation.IdleSpritesResourcePath.Trim());
                if (frames != null && frames.Length > 0)
                    sprite = frames[0];
            }

            _previewCache[enemy] = sprite;
            return sprite;
        }

        private static Rect ResolveTextureCoordinates(Sprite sprite)
        {
            Rect rect = sprite.textureRect;
            Texture texture = sprite.texture;
            return new Rect(
                rect.x / texture.width,
                rect.y / texture.height,
                rect.width / texture.width,
                rect.height / texture.height);
        }

        private static string ResolveName(EnemyDataSO enemy)
        {
            return string.IsNullOrWhiteSpace(enemy.DisplayName) ? enemy.name : enemy.DisplayName;
        }
    }
}
