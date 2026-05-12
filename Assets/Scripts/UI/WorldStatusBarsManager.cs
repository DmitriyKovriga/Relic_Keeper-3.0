using System;
using System.Collections.Generic;
using Scripts.Enemies;
using Scripts.StatusEffects;
using Scripts.Stats;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

/// <summary>
/// Runtime manager for world-space unit bars rendered in screen-space canvas.
/// Player: HP + Mana, Enemy: HP only.
/// </summary>
public sealed class WorldStatusBarsManager : MonoBehaviour
{
    private const float RescanInterval = 0.5f;
    private const float DamageTrailDuration = 0.22f;
    private const float MinAutoHeight = 0.45f;
    private const float EnemyExtraYOffset = 0.12f;
    private const float PlayerExtraYOffset = 0.18f;
    private const float OffscreenPaddingPx = 48f;

    private readonly Dictionary<PlayerStats, TrackedPlayer> _players = new Dictionary<PlayerStats, TrackedPlayer>();
    private readonly Dictionary<EnemyHealth, TrackedEnemy> _enemies = new Dictionary<EnemyHealth, TrackedEnemy>();

    private Canvas _canvas;
    private RectTransform _canvasRect;
    private RectTransform _barsRoot;
    private Camera _mainCamera;
    private float _nextRescanTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (UObject.FindFirstObjectByType<WorldStatusBarsManager>() != null)
            return;

        var go = new GameObject("WorldStatusBarsManager");
        DontDestroyOnLoad(go);
        go.AddComponent<WorldStatusBarsManager>();
    }

    private void LateUpdate()
    {
        EnsureUiRoot();
        if (_barsRoot == null)
            return;

        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (Time.unscaledTime >= _nextRescanTime)
        {
            RescanTargets();
            _nextRescanTime = Time.unscaledTime + RescanInterval;
        }

        UpdatePlayers();
        UpdateEnemies();
    }

    private void OnDisable()
    {
        ClearAllViews();
    }

    private void EnsureUiRoot()
    {
        if (_barsRoot != null && _barsRoot.gameObject != null && _barsRoot.gameObject.activeInHierarchy)
            return;

        _canvas = null;
        _canvasRect = null;
        _barsRoot = null;

        var allCanvases = UObject.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < allCanvases.Length; i++)
        {
            if (allCanvases[i] == null) continue;
            if (allCanvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _canvas = allCanvases[i];
                break;
            }
        }
        if (_canvas == null && allCanvases.Length > 0)
            _canvas = allCanvases[0];

        if (_canvas == null)
        {
            var canvasGo = new GameObject("WorldBarsCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;
            canvasGo.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasGo);
        }

        _canvasRect = _canvas.GetComponent<RectTransform>();
        if (_canvasRect == null)
            return;

        var root = _canvas.transform.Find("WorldBarsRoot");
        if (root != null)
        {
            _barsRoot = root as RectTransform;
        }
        else
        {
            var rootGo = new GameObject("WorldBarsRoot");
            _barsRoot = rootGo.AddComponent<RectTransform>();
            _barsRoot.SetParent(_canvasRect, false);
            _barsRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _barsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _barsRoot.pivot = new Vector2(0.5f, 0.5f);
            _barsRoot.anchoredPosition = Vector2.zero;
            _barsRoot.sizeDelta = Vector2.zero;
        }
    }

    private void RescanTargets()
    {
        var foundPlayers = UObject.FindObjectsByType<PlayerStats>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var foundEnemies = UObject.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        var playerSet = new HashSet<PlayerStats>(foundPlayers);
        var enemySet = new HashSet<EnemyHealth>(foundEnemies);

        var playersToRemove = new List<PlayerStats>();
        foreach (var kv in _players)
        {
            if (kv.Key == null || !playerSet.Contains(kv.Key))
                playersToRemove.Add(kv.Key);
        }
        for (int i = 0; i < playersToRemove.Count; i++)
        {
            RemovePlayer(playersToRemove[i]);
        }

        var enemiesToRemove = new List<EnemyHealth>();
        foreach (var kv in _enemies)
        {
            if (kv.Key == null || !enemySet.Contains(kv.Key))
                enemiesToRemove.Add(kv.Key);
        }
        for (int i = 0; i < enemiesToRemove.Count; i++)
        {
            RemoveEnemy(enemiesToRemove[i]);
        }

        for (int i = 0; i < foundPlayers.Length; i++)
        {
            var player = foundPlayers[i];
            if (player == null || _players.ContainsKey(player))
                continue;

            var view = CreatePlayerView(player.name);
            MysticShieldController.TryResolve(player.transform, out var mysticShield);
            AilmentController.TryResolve(player.transform, out var ailments);
            var tracked = new TrackedPlayer
            {
                Stats = player,
                Transform = player.transform,
                MysticShield = mysticShield,
                Ailments = ailments,
                AutoHeight = ComputeAutoHeight(player.transform),
                View = view
            };
            tracked.PlayerChangedHandler = () =>
            {
                if (tracked.Stats == null) return;
                var hp = tracked.Stats.Health;
                var mp = tracked.Stats.Mana;
                tracked.CachedHealthNormalized = hp != null ? Mathf.Clamp01(hp.Percent) : 0f;
                tracked.CachedManaNormalized = mp != null ? Mathf.Clamp01(mp.Percent) : 0f;
            };
            player.OnAnyStatChanged += tracked.PlayerChangedHandler;
            tracked.PlayerChangedHandler.Invoke();
            _players[player] = tracked;
        }

        for (int i = 0; i < foundEnemies.Length; i++)
        {
            var enemy = foundEnemies[i];
            if (enemy == null || _enemies.ContainsKey(enemy))
                continue;

            var view = CreateEnemyView(enemy.name);
            AilmentController.TryResolve(enemy.transform, out var ailments);
            var tracked = new TrackedEnemy
            {
                Health = enemy,
                Transform = enemy.transform,
                Ailments = ailments,
                AutoHeight = ComputeAutoHeight(enemy.transform),
                View = view
            };
            tracked.EnemyHealthHandler = (cur, max) =>
            {
                tracked.CachedHealthNormalized = max > 0f ? Mathf.Clamp01(cur / max) : 0f;
            };
            tracked.EnemyDeathHandler = _ => tracked.CachedHealthNormalized = 0f;
            enemy.OnHealthChanged += tracked.EnemyHealthHandler;
            enemy.OnDeath += tracked.EnemyDeathHandler;
            tracked.EnemyHealthHandler.Invoke(enemy.CurrentHealth, enemy.MaxHealth);
            _enemies[enemy] = tracked;
        }
    }

    private void UpdatePlayers()
    {
        if (_canvasRect == null || _barsRoot == null)
            return;

        foreach (var kv in _players)
        {
            var tracked = kv.Value;
            if (tracked == null || tracked.Stats == null || tracked.Transform == null || tracked.View == null)
                continue;

            var hp = tracked.Stats.Health;
            var mp = tracked.Stats.Mana;
            if (hp == null || mp == null)
            {
                tracked.View.SetVisible(false);
                continue;
            }

            // Poll as fallback, events are still the main update path.
            tracked.CachedHealthNormalized = Mathf.Clamp01(hp.Percent);
            tracked.CachedManaNormalized = Mathf.Clamp01(mp.Percent);
            tracked.View.SetHealth(tracked.CachedHealthNormalized);
            tracked.View.SetMana(tracked.CachedManaNormalized);
            if (tracked.MysticShield == null)
                MysticShieldController.TryResolve(tracked.Transform, out tracked.MysticShield);
            if (tracked.MysticShield != null)
                tracked.View.SetMysticShield(tracked.MysticShield.CurrentCharges, tracked.MysticShield.MaxCharges, tracked.MysticShield.RechargeProgressNormalized);
            else
                tracked.View.SetMysticShield(0, 0, 0f);
            if (tracked.Ailments == null)
                AilmentController.TryResolve(tracked.Transform, out tracked.Ailments);
            tracked.View.SetAilmentStacks(tracked.Ailments != null ? tracked.Ailments.GetStackCount(AilmentType.Poison) : 0);
            tracked.View.Tick(Time.unscaledDeltaTime);

            var worldPos = tracked.Transform.position + Vector3.up * (Mathf.Max(MinAutoHeight, tracked.AutoHeight) + PlayerExtraYOffset);
            tracked.View.SetVisible(SetUiPosition(tracked.View.Root, worldPos));
        }
    }

    private void UpdateEnemies()
    {
        if (_canvasRect == null || _barsRoot == null)
            return;

        var toRemove = new List<EnemyHealth>();
        foreach (var kv in _enemies)
        {
            var key = kv.Key;
            var tracked = kv.Value;
            if (key == null || tracked == null || tracked.Health == null || tracked.Transform == null || tracked.View == null)
            {
                toRemove.Add(key);
                continue;
            }

            float max = tracked.Health.MaxHealth;
            float cur = tracked.Health.CurrentHealth;
            if (tracked.Health.IsDead || max <= 0f || cur <= 0f)
            {
                tracked.View.SetVisible(false);
                continue;
            }

            // Poll as fallback, events are still the main update path.
            tracked.CachedHealthNormalized = Mathf.Clamp01(cur / max);
            tracked.View.SetHealth(tracked.CachedHealthNormalized);
            if (tracked.Ailments == null)
                AilmentController.TryResolve(tracked.Transform, out tracked.Ailments);
            tracked.View.SetAilmentStacks(tracked.Ailments != null ? tracked.Ailments.GetStackCount(AilmentType.Poison) : 0);
            tracked.View.Tick(Time.unscaledDeltaTime);
            var worldPos = tracked.Transform.position + Vector3.up * (Mathf.Max(MinAutoHeight, tracked.AutoHeight) + EnemyExtraYOffset);
            tracked.View.SetVisible(SetUiPosition(tracked.View.Root, worldPos));
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            RemoveEnemy(toRemove[i]);
        }
    }

    private bool SetUiPosition(RectTransform target, Vector3 worldPosition)
    {
        if (_canvasRect == null || target == null)
            return false;

        var cam = _mainCamera != null ? _mainCamera : Camera.main;
        if (cam == null)
            return false;

        Vector3 screen = cam.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0f)
            return false;

        if (screen.x < -OffscreenPaddingPx || screen.x > Screen.width + OffscreenPaddingPx ||
            screen.y < -OffscreenPaddingPx || screen.y > Screen.height + OffscreenPaddingPx)
            return false;

        Camera uiCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, uiCam, out var local))
        {
            target.anchoredPosition = local;
            return true;
        }

        return false;
    }

    private StatusBarView CreatePlayerView(string debugName)
    {
        var root = CreateRoot($"PlayerBars_{debugName}", new Vector2(22f, 9f));
        var mysticShield = CreateMysticShieldRow(root, "MysticShield", new Vector2(0f, 4.1f), 20f, 1.2f, 3f, 1f,
            new Color(0.08f, 0.22f, 0.34f, 1f),
            new Color(0.5f, 0.95f, 1f, 1f),
            new Color(0.22f, 0.62f, 1f, 0.95f));
        var ailments = CreateAilmentStackRow(root, "Ailments", new Vector2(0f, 6.6f));
        var hp = CreateBarRow(root, "HP", new Vector2(0f, 1.5f), 20f, 2f, new Color(0.1f, 0.05f, 0.05f, 0.9f), new Color(0.8f, 0.15f, 0.15f, 0.95f));
        var mp = CreateBarRow(root, "MP", new Vector2(0f, -1.5f), 20f, 2f, new Color(0.05f, 0.07f, 0.1f, 0.9f), new Color(0.15f, 0.45f, 0.9f, 0.95f));
        return new StatusBarView(root, hp, mp, mysticShield, ailments, true);
    }

    private StatusBarView CreateEnemyView(string debugName)
    {
        var root = CreateRoot($"EnemyBar_{debugName}", new Vector2(18f, 4f));
        var ailments = CreateAilmentStackRow(root, "Ailments", new Vector2(0f, 3f));
        var hp = CreateBarRow(root, "HP", Vector2.zero, 16f, 2f, new Color(0.12f, 0.05f, 0.05f, 0.9f), new Color(0.85f, 0.18f, 0.18f, 0.95f));
        return new StatusBarView(root, hp, null, null, ailments, false);
    }

    private RectTransform CreateRoot(string name, Vector2 size)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(_barsRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return rect;
    }

    private static BarRow CreateBarRow(RectTransform parent, string name, Vector2 pos, float width, float height, Color bgColor, Color fillColor)
    {
        var bgGo = new GameObject(name + "_BG");
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.SetParent(parent, false);
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.sizeDelta = new Vector2(width, height);
        bgRect.anchoredPosition = pos;

        var bg = bgGo.AddComponent<Image>();
        bg.color = bgColor;
        bg.raycastTarget = false;

        var trailGo = new GameObject(name + "_Trail");
        var trailRect = trailGo.AddComponent<RectTransform>();
        trailRect.SetParent(bgRect, false);
        trailRect.anchorMin = new Vector2(0f, 0f);
        trailRect.anchorMax = new Vector2(0f, 1f);
        trailRect.pivot = new Vector2(0f, 0.5f);
        trailRect.anchoredPosition = Vector2.zero;
        trailRect.sizeDelta = new Vector2(width, 0f);

        var trail = trailGo.AddComponent<Image>();
        trail.color = new Color(1f, 0.55f, 0.15f, 0f);
        trail.raycastTarget = false;

        var fillGo = new GameObject(name + "_Fill");
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.SetParent(bgRect, false);
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(width, 0f);

        var fill = fillGo.AddComponent<Image>();
        fill.color = fillColor;
        fill.raycastTarget = false;
        return new BarRow(fill, fillRect, trail, trailRect, width);
    }

    private static MysticShieldRow CreateMysticShieldRow(
        RectTransform parent,
        string name,
        Vector2 pos,
        float maxWidth,
        float height,
        float preferredSlotWidth,
        float spacing,
        Color emptyColor,
        Color fullColor,
        Color rechargeColor)
    {
        var rowGo = new GameObject(name + "_Root");
        var rowRect = rowGo.AddComponent<RectTransform>();
        rowRect.SetParent(parent, false);
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = pos;
        rowRect.sizeDelta = new Vector2(maxWidth, height);
        rowRect.gameObject.SetActive(false);

        return new MysticShieldRow(rowRect, maxWidth, height, preferredSlotWidth, spacing, emptyColor, fullColor, rechargeColor);
    }

    private static AilmentStackRow CreateAilmentStackRow(RectTransform parent, string name, Vector2 pos)
    {
        var rowGo = new GameObject(name + "_Root");
        var rowRect = rowGo.AddComponent<RectTransform>();
        rowRect.SetParent(parent, false);
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = pos;
        rowRect.sizeDelta = new Vector2(12f, 5f);
        rowRect.gameObject.SetActive(false);

        var iconGo = new GameObject("PoisonIcon", typeof(RectTransform), typeof(Image));
        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.SetParent(rowRect, false);
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-3f, 0f);
        iconRect.sizeDelta = new Vector2(3f, 3f);
        var icon = iconGo.GetComponent<Image>();
        icon.color = new Color(0.02f, 0.23f, 0.08f, 0.95f);
        icon.raycastTarget = false;

        var textGo = new GameObject("PoisonCount", typeof(RectTransform), typeof(Text));
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(rowRect, false);
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, 0f);
        textRect.sizeDelta = new Vector2(9f, 6f);

        var text = textGo.GetComponent<Text>();
        text.raycastTarget = false;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 5;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = new Color(0.62f, 1f, 0.48f, 1f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return new AilmentStackRow(rowRect, text, pos);
    }

    private static float ComputeAutoHeight(Transform target)
    {
        if (target == null)
            return MinAutoHeight;

        float topY = target.position.y;
        bool found = false;

        var renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || !r.enabled) continue;
            topY = Mathf.Max(topY, r.bounds.max.y);
            found = true;
        }

        if (!found)
        {
            var colliders2D = target.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders2D.Length; i++)
            {
                var c = colliders2D[i];
                if (c == null || !c.enabled) continue;
                topY = Mathf.Max(topY, c.bounds.max.y);
                found = true;
            }
        }

        if (!found)
        {
            var colliders3D = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders3D.Length; i++)
            {
                var c = colliders3D[i];
                if (c == null || !c.enabled) continue;
                topY = Mathf.Max(topY, c.bounds.max.y);
                found = true;
            }
        }

        return Mathf.Max(MinAutoHeight, topY - target.position.y);
    }

    private void RemovePlayer(PlayerStats player)
    {
        if (!_players.TryGetValue(player, out var tracked))
            return;

        if (tracked.PlayerChangedHandler != null && tracked.Stats != null)
            tracked.Stats.OnAnyStatChanged -= tracked.PlayerChangedHandler;

        tracked?.View?.Destroy();
        _players.Remove(player);
    }

    private void RemoveEnemy(EnemyHealth enemy)
    {
        if (!_enemies.TryGetValue(enemy, out var tracked))
            return;

        if (tracked.EnemyHealthHandler != null && tracked.Health != null)
            tracked.Health.OnHealthChanged -= tracked.EnemyHealthHandler;
        if (tracked.EnemyDeathHandler != null && tracked.Health != null)
            tracked.Health.OnDeath -= tracked.EnemyDeathHandler;

        tracked?.View?.Destroy();
        _enemies.Remove(enemy);
    }

    private void ClearAllViews()
    {
        foreach (var kv in _players)
        {
            kv.Value?.View?.Destroy();
        }
        _players.Clear();

        foreach (var kv in _enemies)
        {
            kv.Value?.View?.Destroy();
        }
        _enemies.Clear();
    }

    private sealed class TrackedPlayer
    {
        public PlayerStats Stats;
        public Transform Transform;
        public MysticShieldController MysticShield;
        public AilmentController Ailments;
        public float AutoHeight;
        public StatusBarView View;
        public float CachedHealthNormalized;
        public float CachedManaNormalized;
        public Action PlayerChangedHandler;
    }

    private sealed class TrackedEnemy
    {
        public EnemyHealth Health;
        public Transform Transform;
        public AilmentController Ailments;
        public float AutoHeight;
        public StatusBarView View;
        public float CachedHealthNormalized;
        public Action<float, float> EnemyHealthHandler;
        public Action<EnemyHealth> EnemyDeathHandler;
    }

    private sealed class BarRow
    {
        public Image FillImage { get; }
        public RectTransform FillRect { get; }
        public Image TrailImage { get; }
        public RectTransform TrailRect { get; }
        public float MaxWidth { get; }
        public bool TrailActive;
        public float TrailElapsed;
        public float TrailStartWidth;
        public float TrailTargetWidth;

        public BarRow(Image fillImage, RectTransform fillRect, Image trailImage, RectTransform trailRect, float maxWidth)
        {
            FillImage = fillImage;
            FillRect = fillRect;
            TrailImage = trailImage;
            TrailRect = trailRect;
            MaxWidth = maxWidth;
        }
    }

    private sealed class MysticShieldRow
    {
        private readonly RectTransform _root;
        private readonly float _maxWidth;
        private readonly float _height;
        private readonly float _preferredSlotWidth;
        private readonly float _spacing;
        private readonly Color _emptyColor;
        private readonly Color _fullColor;
        private readonly Color _rechargeColor;
        private readonly List<MysticShieldSlot> _slots = new List<MysticShieldSlot>();
        public float CurrentHeight { get; private set; }

        public MysticShieldRow(
            RectTransform root,
            float maxWidth,
            float height,
            float preferredSlotWidth,
            float spacing,
            Color emptyColor,
            Color fullColor,
            Color rechargeColor)
        {
            _root = root;
            _maxWidth = Mathf.Max(1f, maxWidth);
            _height = Mathf.Max(1f, height);
            _preferredSlotWidth = Mathf.Max(1f, preferredSlotWidth);
            _spacing = Mathf.Max(0f, spacing);
            _emptyColor = emptyColor;
            _fullColor = fullColor;
            _rechargeColor = rechargeColor;
        }

        public void Set(int currentCharges, int maxCharges, float rechargeProgress)
        {
            if (_root == null)
                return;

            if (maxCharges <= 0)
            {
                _root.gameObject.SetActive(false);
                CurrentHeight = 0f;
                return;
            }

            EnsureSlotCount(maxCharges);

            int current = Mathf.Clamp(currentCharges, 0, maxCharges);
            float progress = Mathf.Clamp01(rechargeProgress);
            int slotsPerRow = ResolveSlotsPerRow();
            int rowCount = Mathf.CeilToInt(maxCharges / (float)slotsPerRow);
            float rowStep = _height + _spacing;
            CurrentHeight = _height + Mathf.Max(0, rowCount - 1) * rowStep;
            _root.sizeDelta = new Vector2(_maxWidth, CurrentHeight);
            _root.gameObject.SetActive(true);

            for (int i = 0; i < _slots.Count; i++)
            {
                MysticShieldSlot slot = _slots[i];
                if (slot == null)
                    continue;

                bool active = i < maxCharges;
                slot.SetActive(active);
                if (!active)
                    continue;

                int row = i / slotsPerRow;
                int column = i % slotsPerRow;
                float x = -_maxWidth * 0.5f + column * (_preferredSlotWidth + _spacing);
                float y = row * rowStep;

                slot.SetLayout(new Vector2(x, y), new Vector2(_preferredSlotWidth, _height));
                float fill = i < current ? 1f : (i == current ? progress : 0f);
                Color fillColor = i < current ? _fullColor : _rechargeColor;
                slot.SetFill(fill, _emptyColor, fillColor);
            }
        }

        private void EnsureSlotCount(int count)
        {
            while (_slots.Count < count)
            {
                int index = _slots.Count;
                var slotGo = new GameObject($"MysticShieldSlot_{index}", typeof(RectTransform), typeof(Image));
                var rect = slotGo.GetComponent<RectTransform>();
                rect.SetParent(_root, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);

                Image background = slotGo.GetComponent<Image>();
                background.raycastTarget = false;

                var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                var fillRect = fillGo.GetComponent<RectTransform>();
                fillRect.SetParent(rect, false);
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = Vector2.zero;

                Image fill = fillGo.GetComponent<Image>();
                fill.raycastTarget = false;
                _slots.Add(new MysticShieldSlot(rect, background, fill));
            }
        }

        private int ResolveSlotsPerRow()
        {
            float slotStep = _preferredSlotWidth + _spacing;
            return Mathf.Max(1, Mathf.FloorToInt((_maxWidth + _spacing) / Mathf.Max(1f, slotStep)));
        }

    }

    private sealed class MysticShieldSlot
    {
        private readonly RectTransform _rect;
        private readonly Image _background;
        private readonly Image _fill;

        public MysticShieldSlot(RectTransform rect, Image background, Image fill)
        {
            _rect = rect;
            _background = background;
            _fill = fill;
        }

        public void SetActive(bool active)
        {
            if (_rect != null && _rect.gameObject.activeSelf != active)
                _rect.gameObject.SetActive(active);
        }

        public void SetLayout(Vector2 position, Vector2 size)
        {
            if (_rect == null)
                return;

            _rect.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
            _rect.sizeDelta = new Vector2(Mathf.Round(size.x), Mathf.Round(size.y));
        }

        public void SetFill(float normalized, Color emptyColor, Color fillColor)
        {
            if (_background != null)
                _background.color = emptyColor;

            if (_fill == null || _fill.rectTransform == null || _rect == null)
                return;

            float width = Mathf.Round(_rect.rect.width * Mathf.Clamp01(normalized));
            _fill.rectTransform.sizeDelta = new Vector2(width, 0f);
            _fill.color = fillColor;
            _fill.enabled = width > 0.01f;
        }
    }

    private sealed class AilmentStackRow
    {
        private readonly RectTransform _root;
        private readonly Text _countText;
        private readonly Vector2 _basePosition;

        public AilmentStackRow(RectTransform root, Text countText, Vector2 basePosition)
        {
            _root = root;
            _countText = countText;
            _basePosition = basePosition;
        }

        public void SetPoisonCount(int count)
        {
            if (_root == null)
                return;

            bool visible = count > 0;
            if (_root.gameObject.activeSelf != visible)
                _root.gameObject.SetActive(visible);

            if (_countText != null)
                _countText.text = count.ToString();
        }

        public void SetYOffset(float y)
        {
            if (_root == null)
                return;

            _root.anchoredPosition = new Vector2(_basePosition.x, Mathf.Round(y));
        }
    }

    private sealed class StatusBarView
    {
        public RectTransform Root { get; }
        private readonly BarRow _healthRow;
        private readonly BarRow _manaRow;
        private readonly MysticShieldRow _mysticShieldRow;
        private readonly AilmentStackRow _ailmentStackRow;
        private readonly bool _isPlayer;

        public StatusBarView(RectTransform root, BarRow healthRow, BarRow manaRow, MysticShieldRow mysticShieldRow, AilmentStackRow ailmentStackRow, bool isPlayer)
        {
            Root = root;
            _healthRow = healthRow;
            _manaRow = manaRow;
            _mysticShieldRow = mysticShieldRow;
            _ailmentStackRow = ailmentStackRow;
            _isPlayer = isPlayer;
        }

        public void SetHealth(float normalized)
        {
            SetRowNormalized(_healthRow, normalized, useDamageTrail: true);
        }

        public void SetMana(float normalized)
        {
            SetRowNormalized(_manaRow, normalized, useDamageTrail: false);
        }

        public void SetMysticShield(int currentCharges, int maxCharges, float rechargeProgress)
        {
            _mysticShieldRow?.Set(currentCharges, maxCharges, rechargeProgress);
            if (_isPlayer && _ailmentStackRow != null)
                _ailmentStackRow.SetYOffset(4.1f + (_mysticShieldRow?.CurrentHeight ?? 0f) + 1.4f);
        }

        public void SetAilmentStacks(int poisonCount)
        {
            _ailmentStackRow?.SetPoisonCount(poisonCount);
        }

        public void Tick(float dt)
        {
            TickRowTrail(_healthRow, dt);
        }

        public void SetVisible(bool visible)
        {
            if (Root != null && Root.gameObject.activeSelf != visible)
                Root.gameObject.SetActive(visible);
        }

        public void Destroy()
        {
            if (Root != null)
                UObject.Destroy(Root.gameObject);
        }

        private static void SetRowNormalized(BarRow row, float normalized, bool useDamageTrail)
        {
            if (row == null || row.FillRect == null)
                return;

            float n = Mathf.Clamp01(normalized);
            float prevWidth = row.FillRect.sizeDelta.x;
            float newWidth = row.MaxWidth * n;

            if (useDamageTrail)
            {
                if (newWidth < prevWidth - 0.01f)
                {
                    float currentTrailWidth = row.TrailRect != null ? row.TrailRect.sizeDelta.x : prevWidth;
                    row.TrailStartWidth = Mathf.Max(prevWidth, currentTrailWidth);
                    row.TrailTargetWidth = newWidth;
                    row.TrailElapsed = 0f;
                    row.TrailActive = true;
                    SetTrailWidth(row, row.TrailStartWidth);
                    SetTrailAlpha(row, 1f);
                }
                else if (newWidth > prevWidth + 0.01f)
                {
                    row.TrailActive = false;
                    SetTrailWidth(row, newWidth);
                    SetTrailAlpha(row, 0f);
                }
            }
            else
            {
                row.TrailActive = false;
                SetTrailWidth(row, newWidth);
                SetTrailAlpha(row, 0f);
            }

            var size = row.FillRect.sizeDelta;
            size.x = newWidth;
            row.FillRect.sizeDelta = size;
            if (row.FillImage != null)
                row.FillImage.enabled = n > 0.001f;
        }

        private static void TickRowTrail(BarRow row, float dt)
        {
            if (row == null || !row.TrailActive)
                return;

            row.TrailElapsed += Mathf.Max(0f, dt);
            float t = Mathf.Clamp01(row.TrailElapsed / DamageTrailDuration);
            float width = Mathf.Lerp(row.TrailStartWidth, row.TrailTargetWidth, t);
            float alpha = 1f - t;

            SetTrailWidth(row, width);
            SetTrailAlpha(row, alpha);

            if (t >= 1f)
            {
                row.TrailActive = false;
                SetTrailWidth(row, row.TrailTargetWidth);
                SetTrailAlpha(row, 0f);
            }
        }

        private static void SetTrailWidth(BarRow row, float width)
        {
            if (row == null || row.TrailRect == null)
                return;
            var size = row.TrailRect.sizeDelta;
            size.x = Mathf.Clamp(width, 0f, row.MaxWidth);
            row.TrailRect.sizeDelta = size;
        }

        private static void SetTrailAlpha(BarRow row, float alpha)
        {
            if (row == null || row.TrailImage == null)
                return;
            var c = row.TrailImage.color;
            c.a = Mathf.Clamp01(alpha);
            row.TrailImage.color = c;
            row.TrailImage.enabled = c.a > 0.001f;
        }
    }
}
