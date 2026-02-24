using System;
using System.Collections.Generic;
using Scripts.Enemies;
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
            var tracked = new TrackedPlayer
            {
                Stats = player,
                Transform = player.transform,
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
            var tracked = new TrackedEnemy
            {
                Health = enemy,
                Transform = enemy.transform,
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
        var root = CreateRoot($"PlayerBars_{debugName}", new Vector2(22f, 7f));
        var hp = CreateBarRow(root, "HP", new Vector2(0f, 1.5f), 20f, 2f, new Color(0.1f, 0.05f, 0.05f, 0.9f), new Color(0.8f, 0.15f, 0.15f, 0.95f));
        var mp = CreateBarRow(root, "MP", new Vector2(0f, -1.5f), 20f, 2f, new Color(0.05f, 0.07f, 0.1f, 0.9f), new Color(0.15f, 0.45f, 0.9f, 0.95f));
        return new StatusBarView(root, hp, mp);
    }

    private StatusBarView CreateEnemyView(string debugName)
    {
        var root = CreateRoot($"EnemyBar_{debugName}", new Vector2(18f, 4f));
        var hp = CreateBarRow(root, "HP", Vector2.zero, 16f, 2f, new Color(0.12f, 0.05f, 0.05f, 0.9f), new Color(0.85f, 0.18f, 0.18f, 0.95f));
        return new StatusBarView(root, hp, null);
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
        return new BarRow(fill, fillRect, width);
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
        public float MaxWidth { get; }

        public BarRow(Image fillImage, RectTransform fillRect, float maxWidth)
        {
            FillImage = fillImage;
            FillRect = fillRect;
            MaxWidth = maxWidth;
        }
    }

    private sealed class StatusBarView
    {
        public RectTransform Root { get; }
        private readonly BarRow _healthRow;
        private readonly BarRow _manaRow;

        public StatusBarView(RectTransform root, BarRow healthRow, BarRow manaRow)
        {
            Root = root;
            _healthRow = healthRow;
            _manaRow = manaRow;
        }

        public void SetHealth(float normalized)
        {
            SetRowNormalized(_healthRow, normalized);
        }

        public void SetMana(float normalized)
        {
            SetRowNormalized(_manaRow, normalized);
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

        private static void SetRowNormalized(BarRow row, float normalized)
        {
            if (row == null || row.FillRect == null)
                return;

            float n = Mathf.Clamp01(normalized);
            var size = row.FillRect.sizeDelta;
            size.x = row.MaxWidth * n;
            row.FillRect.sizeDelta = size;
            if (row.FillImage != null)
                row.FillImage.enabled = n > 0.001f;
        }
    }
}
