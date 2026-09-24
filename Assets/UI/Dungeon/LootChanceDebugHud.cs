#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Scripts.Configuration;
using Scripts.Dungeon;
using Scripts.Enemies;
using Scripts.Items;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Editor-only live view of the effective loot rolls in the current room.</summary>
public sealed class LootChanceDebugHud : MonoBehaviour
{
    public const int PanelWidth = 132;
    public const int RowHeight = 7;
    public const float SortingOrder = 1900f;

    private const float RefreshInterval = 0.25f;
    private static readonly Color PanelColor = new Color(0.035f, 0.03f, 0.045f, 0.88f);
    private static readonly Color BorderColor = new Color(0.48f, 0.31f, 0.62f, 0.85f);
    private static readonly Color HeaderColor = new Color(0.9f, 0.72f, 1f, 1f);
    private static readonly Color TextColor = new Color(0.86f, 0.84f, 0.9f, 1f);
    private static readonly Color MutedColor = new Color(0.66f, 0.63f, 0.7f, 1f);
    private static readonly Color MagicColor = new Color(0.42f, 0.7f, 1f, 1f);
    private static readonly Color RareColor = new Color(1f, 0.78f, 0.24f, 1f);

    private static LootChanceDebugHud _instance;

    private readonly List<EnemyDataSO> _activeEnemyTypes = new List<EnemyDataSO>();
    private readonly HashSet<EnemyDataSO> _enemySet = new HashSet<EnemyDataSO>();
    private readonly Dictionary<CraftingOrbSO, float> _currencyChances = new Dictionary<CraftingOrbSO, float>();
    private UIDocument _document;
    private VisualElement _panel;
    private VisualElement _content;
    private ItemDatabaseSO _database;
    private CraftingOrbSO[] _orbs;
    private float _nextRefreshTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying || _instance != null)
            return;

        var host = new GameObject("LootChanceDebugHUD");
        DontDestroyOnLoad(host);
        _instance = host.AddComponent<LootChanceDebugHud>();
    }

    private void Update()
    {
        bool visible = PlaytestConfiguration.ShowLootChanceInfo;
        if (!visible)
        {
            if (_panel != null)
                _panel.style.display = DisplayStyle.None;
            return;
        }

        if (!TryBuild())
            return;

        _panel.style.display = DisplayStyle.Flex;
        if (Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + RefreshInterval;
        RefreshValues();
    }

    private bool TryBuild()
    {
        if (_panel != null)
            return true;

        PanelSettings panelSettings = ResolvePanelSettings();
        if (panelSettings == null)
            return false;

        _document = gameObject.AddComponent<UIDocument>();
        _document.panelSettings = panelSettings;
        _document.sortingOrder = SortingOrder;

        VisualElement root = _document.rootVisualElement;
        if (root == null)
            return false;

        UIFontApplier.ApplyToRoot(root);
        root.pickingMode = PickingMode.Ignore;

        _panel = new VisualElement { name = "LootChanceDebugPanel", pickingMode = PickingMode.Ignore };
        _panel.style.position = Position.Absolute;
        _panel.style.left = 3;
        _panel.style.bottom = 3;
        _panel.style.width = PanelWidth;
        _panel.style.flexDirection = FlexDirection.Column;
        _panel.style.paddingLeft = 3;
        _panel.style.paddingRight = 3;
        _panel.style.paddingTop = 2;
        _panel.style.paddingBottom = 3;
        _panel.style.backgroundColor = PanelColor;
        SetSquareBorder(_panel, 1, BorderColor);
        root.Add(_panel);

        var title = new Label("LOOT CHANCE — EFFECTIVE") { pickingMode = PickingMode.Ignore };
        title.style.fontSize = 6;
        title.style.height = 9;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.unityTextAlign = TextAnchor.MiddleLeft;
        title.style.color = HeaderColor;
        ResetSpacing(title);
        _panel.Add(title);

        _content = new VisualElement { name = "LootChanceDebugContent", pickingMode = PickingMode.Ignore };
        _content.style.flexDirection = FlexDirection.Column;
        ResetSpacing(_content);
        _panel.Add(_content);

        _database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
        _orbs = Resources.LoadAll<CraftingOrbSO>(ProjectPaths.ResourcesCraftingOrbsFolder);
        SortCurrencies(_orbs);
        return true;
    }

    private void RefreshValues()
    {
        if (_content == null)
            return;

        _content.Clear();
        if (_database == null)
            _database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
        if (_database == null)
        {
            AddRow("ItemDatabaseSO missing", string.Empty, RareColor);
            return;
        }

        DungeonModifierContext modifiers = DungeonController.Instance != null
            ? DungeonController.Instance.CurrentModifiers
            : null;
        float quantityMultiplier = modifiers != null ? modifiers.LootQuantityMultiplier : 1f;
        float rarityMultiplier = modifiers != null ? modifiers.LootRarityMultiplier : 1f;
        AddRow("Quantity", $"x{quantityMultiplier:0.##}", MutedColor);
        AddRow("Rarity", $"x{rarityMultiplier:0.##}", MutedColor);

        CollectActiveEnemyTypes();
        AddSection("ITEM DROP — AT LEAST 1");
        AddEffectiveDropRows(_database.BaseItemDropChance, quantityMultiplier);

        EnemyLootDropService.GetRarityChances(
            rarityMultiplier,
            _database.MagicItemDropChance,
            _database.RareItemDropChance,
            out float commonChance,
            out float magicChance,
            out float rareChance);
        AddSection("RARITY AMONG ITEMS");
        AddRow("Common", Percent(commonChance), TextColor);
        AddRow("Magic", Percent(magicChance), MagicColor);
        AddRow("Rare", Percent(rareChance), RareColor);

        AddSection("CURRENCY — AT LEAST 1");
        AddEffectiveDropRows(_database.BaseCurrencyDropChance, quantityMultiplier);

        AddSection("TYPE AMONG CURRENCY");
        EnemyLootDropService.GetCraftingOrbChances(_orbs, rarityMultiplier, _currencyChances);
        if (_orbs != null)
        {
            for (int i = 0; i < _orbs.Length; i++)
            {
                CraftingOrbSO orb = _orbs[i];
                if (orb == null)
                    continue;
                _currencyChances.TryGetValue(orb, out float chance);
                AddRow(ShortCurrencyName(orb), Percent(chance), HeaderColor);
            }
        }
    }

    private void CollectActiveEnemyTypes()
    {
        _activeEnemyTypes.Clear();
        _enemySet.Clear();
        EnemyEntity[] enemies = FindObjectsByType<EnemyEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyDataSO data = enemies[i] != null ? enemies[i].Data : null;
            if (data != null && _enemySet.Add(data))
                _activeEnemyTypes.Add(data);
        }

        _activeEnemyTypes.Sort((left, right) => string.Compare(
            EnemyName(left),
            EnemyName(right),
            StringComparison.OrdinalIgnoreCase));
    }

    private void AddEffectiveDropRows(float baseChance, float roomQuantityMultiplier)
    {
        if (_activeEnemyTypes.Count == 0)
        {
            float chance = EnemyLootDropService.GetDropChance(baseChance, 1f, roomQuantityMultiplier, 0);
            AddRow("Base enemy x1", Percent(chance), TextColor);
            return;
        }

        for (int i = 0; i < _activeEnemyTypes.Count; i++)
        {
            EnemyDataSO enemy = _activeEnemyTypes[i];
            float chance = EnemyLootDropService.GetDropChance(
                baseChance,
                enemy.LootDropMultiplier,
                enemy.LootQuantityMultiplier * roomQuantityMultiplier,
                0);
            AddRow(EnemyName(enemy), Percent(chance), TextColor);
        }
    }

    private void AddSection(string text)
    {
        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.style.fontSize = 5;
        label.style.height = 8;
        label.style.marginTop = 1;
        label.style.marginBottom = 0;
        label.style.marginLeft = 0;
        label.style.marginRight = 0;
        label.style.paddingLeft = 0;
        label.style.paddingRight = 0;
        label.style.paddingTop = 0;
        label.style.paddingBottom = 0;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.LowerLeft;
        label.style.color = HeaderColor;
        _content.Add(label);
    }

    private void AddRow(string name, string value, Color color)
    {
        var row = new VisualElement { pickingMode = PickingMode.Ignore };
        row.style.height = RowHeight;
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexShrink = 0;
        ResetSpacing(row);

        var nameLabel = new Label(name ?? string.Empty) { pickingMode = PickingMode.Ignore };
        nameLabel.style.fontSize = 5;
        nameLabel.style.height = RowHeight;
        nameLabel.style.flexGrow = 1;
        nameLabel.style.flexShrink = 1;
        nameLabel.style.overflow = Overflow.Hidden;
        nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        nameLabel.style.color = color;
        ResetSpacing(nameLabel);
        row.Add(nameLabel);

        var valueLabel = new Label(value ?? string.Empty) { pickingMode = PickingMode.Ignore };
        valueLabel.style.fontSize = 5;
        valueLabel.style.width = 34;
        valueLabel.style.height = RowHeight;
        valueLabel.style.flexShrink = 0;
        valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        valueLabel.style.color = color;
        ResetSpacing(valueLabel);
        row.Add(valueLabel);
        _content.Add(row);
    }

    private static string EnemyName(EnemyDataSO enemy)
    {
        if (enemy == null)
            return "Enemy";
        if (!string.IsNullOrWhiteSpace(enemy.DisplayName))
            return enemy.DisplayName;
        return enemy.name.StartsWith("SO_", StringComparison.Ordinal) ? enemy.name.Substring(3) : enemy.name;
    }

    private static string ShortCurrencyName(CraftingOrbSO orb)
    {
        string id = orb != null ? orb.ID : string.Empty;
        return id.StartsWith("RelicOf", StringComparison.OrdinalIgnoreCase) ? id.Substring(7) : id;
    }

    private static string Percent(float chance) => $"{Mathf.Clamp01(chance) * 100f:0.##}%";

    private static void SortCurrencies(CraftingOrbSO[] orbs)
    {
        if (orbs == null)
            return;
        Array.Sort(orbs, (left, right) =>
        {
            bool leftDefault = left != null && string.Equals(left.ID, "RelicOfMutation", StringComparison.OrdinalIgnoreCase);
            bool rightDefault = right != null && string.Equals(right.ID, "RelicOfMutation", StringComparison.OrdinalIgnoreCase);
            if (leftDefault != rightDefault)
                return leftDefault ? 1 : -1;
            float leftChance = left != null ? left.UpgradeChance : float.MaxValue;
            float rightChance = right != null ? right.UpgradeChance : float.MaxValue;
            int chanceOrder = leftChance.CompareTo(rightChance);
            return chanceOrder != 0
                ? chanceOrder
                : string.Compare(left != null ? left.ID : string.Empty, right != null ? right.ID : string.Empty, StringComparison.Ordinal);
        });
    }

    private static PanelSettings ResolvePanelSettings()
    {
        UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < documents.Length; i++)
        {
            if (documents[i] != null && documents[i].panelSettings != null)
                return documents[i].panelSettings;
        }
        return null;
    }

    private static void ResetSpacing(VisualElement element)
    {
        element.style.marginLeft = 0;
        element.style.marginRight = 0;
        element.style.marginTop = 0;
        element.style.marginBottom = 0;
        element.style.paddingLeft = 0;
        element.style.paddingRight = 0;
        element.style.paddingTop = 0;
        element.style.paddingBottom = 0;
    }

    private static void SetSquareBorder(VisualElement element, float width, Color color)
    {
        element.style.borderLeftWidth = width;
        element.style.borderRightWidth = width;
        element.style.borderTopWidth = width;
        element.style.borderBottomWidth = width;
        element.style.borderLeftColor = color;
        element.style.borderRightColor = color;
        element.style.borderTopColor = color;
        element.style.borderBottomColor = color;
        element.style.borderTopLeftRadius = 0;
        element.style.borderTopRightRadius = 0;
        element.style.borderBottomLeftRadius = 0;
        element.style.borderBottomRightRadius = 0;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
#endif
