using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelingSystem
{
    public event Action OnLevelUp;
    public event Action OnXPChanged;
    public event Action OnSkillPointsChanged; 
    public int SkillPoints { get; private set; } = 0;

    public int Level { get; private set; } = 1;
    public float CurrentXP { get; private set; }
    public float RequiredXP { get; private set; }
    
    private const int MAX_LEVEL = 30; // В PoE обычно 100

    public LevelingSystem(int startLevel, float startXP, float startReqXP, int startPoints = 0)
    {
        Level = startLevel;
        CurrentXP = startXP;
        RequiredXP = startReqXP > 0 ? startReqXP : 100f;
        SkillPoints = startPoints;
    }

    public void AddXP(float amount)
    {
        if (Level >= MAX_LEVEL) return;

        CurrentXP += amount;
        
        while (CurrentXP >= RequiredXP && Level < MAX_LEVEL)
        {
            CurrentXP -= RequiredXP;
            Level++;
            RequiredXP = CalculateNextLevelXP(Level);
            
            // Даем 1 очко за уровень
            SkillPoints++;
            OnLevelUp?.Invoke();
            OnSkillPointsChanged?.Invoke();
        }
        
        OnXPChanged?.Invoke();
    }

    public void RefundPoint(int amount = 1)
    {
        SkillPoints += amount;
        OnSkillPointsChanged?.Invoke();
    }

    public bool TrySpendPoint(int amount = 1)
    {
        if (SkillPoints >= amount)
        {
            SkillPoints -= amount;
            OnSkillPointsChanged?.Invoke();
            return true;
        }
        return false;
    }

    // Простая формула прогрессии, потом можно усложнить
    private float CalculateNextLevelXP(int level)
    {
        return Mathf.Round(100f * Mathf.Pow(1.2f, level - 1));
    }
}

public class ExperienceSoulPickup : MonoBehaviour
{
    private const float PixelsPerUnit = 24f;
    private const float PixelStep = 1f / PixelsPerUnit;
    private const int SortingOrder = 18;
    private const int TailSegmentCount = 6;
    private const int HistoryCapacity = 26;

    private static Sprite s_coreSprite;
    private static Sprite s_tailSprite;
    private static Material s_spriteMaterial;

    private enum SoulState
    {
        Delay,
        Arc,
        Homing
    }

    private float _xpAmount;
    private float _stateTimer;
    private float _delayDuration;
    private float _arcDuration;
    private float _historyAccumulator;
    private float _collectRadius;
    private float _homingResponsiveness;
    private float _minHomingSpeed;
    private float _maxHomingSpeed;
    private float _timeAlive;
    private float _tailWidth;

    private Vector2 _velocity;
    private Vector3 _lastHistoryPosition;
    private Vector3 _arcStart;
    private Vector3 _arcControl;
    private SoulState _state;

    private PlayerStats _playerStats;
    private Transform _target;
    private SpriteRenderer _coreRenderer;
    private readonly List<SpriteRenderer> _tailSegments = new();
    private readonly List<Vector3> _history = new();

    public static void Spawn(float xpAmount, Vector3 worldPosition, Transform parent)
    {
        if (xpAmount <= 0f)
            return;

        GameObject go = new GameObject($"XP Soul ({xpAmount:0})");
        go.transform.SetParent(parent, true);
        go.transform.position = SnapToPixelGrid(worldPosition);
        var soul = go.AddComponent<ExperienceSoulPickup>();
        soul.Initialize(xpAmount);
    }

    private void Initialize(float xpAmount)
    {
        EnsureVisualAssetsBuilt();

        _xpAmount = xpAmount;
        _delayDuration = UnityEngine.Random.Range(0.08f, 0.14f);
        _arcDuration = UnityEngine.Random.Range(0.34f, 0.44f);
        _collectRadius = 0.42f;
        _homingResponsiveness = 18f;
        _minHomingSpeed = 7.2f;
        _maxHomingSpeed = 13.5f;
        _velocity = Vector2.zero;
        _tailWidth = 0.7f;
        _state = SoulState.Delay;
        _stateTimer = 0f;

        gameObject.layer = 0;

        _coreRenderer = gameObject.AddComponent<SpriteRenderer>();
        _coreRenderer.sprite = s_coreSprite;
        _coreRenderer.material = s_spriteMaterial;
        _coreRenderer.sortingOrder = SortingOrder;
        _coreRenderer.color = new Color(0.68f, 0.95f, 1f, 1f);

        for (int i = 0; i < TailSegmentCount; i++)
        {
            GameObject tail = new GameObject($"Tail_{i}");
            tail.transform.SetParent(transform, false);
            var renderer = tail.AddComponent<SpriteRenderer>();
            renderer.sprite = s_tailSprite;
            renderer.material = s_spriteMaterial;
            renderer.sortingOrder = SortingOrder - 1 - i;
            renderer.color = new Color(0.55f, 0.88f, 1f, 0.65f - (i * 0.08f));
            _tailSegments.Add(renderer);
        }

        _lastHistoryPosition = transform.position;
        _history.Add(transform.position);
        ResolvePlayerTarget();
        UpdateTailVisuals();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _timeAlive += dt;
        _stateTimer += dt;

        if ((_target == null || !_target.gameObject.activeInHierarchy) && (_timeAlive > 0.1f))
            ResolvePlayerTarget();

        switch (_state)
        {
            case SoulState.Delay:
                UpdateDelay(dt);
                break;

            case SoulState.Arc:
                UpdateArc(dt);
                break;

            case SoulState.Homing:
                UpdateHoming(dt);
                break;
        }

        if (_state == SoulState.Homing)
            TryCollect();
        PushHistoryPoint();
        UpdateTailVisuals();
    }

    private void UpdateDelay(float dt)
    {
        float bob = Mathf.Sin((_timeAlive * 10f) + (_xpAmount * 0.1f)) * 0.004f;
        transform.position = SnapToPixelGrid(transform.position + new Vector3(0f, bob, 0f));

        if (_stateTimer >= _delayDuration)
        {
            BeginArc();
            _state = SoulState.Arc;
            _stateTimer = 0f;
        }
    }

    private void BeginArc()
    {
        ResolvePlayerTarget();
        _arcStart = transform.position;
        Vector3 targetAnchor = _target != null ? GetTargetAnchor() : (_arcStart + new Vector3(1.2f, 0.2f, 0f));
        float arcDirection = Mathf.Sign(targetAnchor.x - _arcStart.x);
        if (Mathf.Approximately(arcDirection, 0f))
            arcDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f;

        _arcControl = _arcStart + new Vector3(
            arcDirection * UnityEngine.Random.Range(0.45f, 0.8f),
            UnityEngine.Random.Range(1.5f, 2.1f),
            0f);
    }

    private void UpdateArc(float dt)
    {
        Vector3 targetAnchor = _target != null ? GetTargetAnchor() : (_arcStart + new Vector3(1.2f, 0.2f, 0f));
        float t = Mathf.Clamp01(_stateTimer / Mathf.Max(0.01f, _arcDuration));
        float easedT = 1f - Mathf.Pow(1f - t, 2.2f);
        Vector3 p0 = _arcStart;
        Vector3 p1 = _arcControl;
        Vector3 p2 = targetAnchor;
        Vector3 pos = ((1f - easedT) * (1f - easedT) * p0) + (2f * (1f - easedT) * easedT * p1) + (easedT * easedT * p2);
        transform.position = SnapToPixelGrid(pos);

        if (t >= 1f)
        {
            _state = SoulState.Homing;
            _stateTimer = 0f;
        }
    }

    private void UpdateHoming(float dt)
    {
        if (_target == null)
        {
            ResolvePlayerTarget();
            return;
        }

        Vector3 targetPosition = GetTargetAnchor();
        Vector2 toTarget = (Vector2)(targetPosition - transform.position);
        float distance = Mathf.Max(0.001f, toTarget.magnitude);

        Vector2 desiredVelocity = toTarget.normalized * Mathf.Lerp(_minHomingSpeed, _maxHomingSpeed, Mathf.Clamp01(1f - (distance / 8f)));
        _velocity = Vector2.Lerp(_velocity, desiredVelocity, 1f - Mathf.Exp(-_homingResponsiveness * dt));

        transform.position = SnapToPixelGrid(transform.position + (Vector3)(_velocity * dt));
    }

    private void Collect()
    {
        if (_playerStats != null)
            _playerStats.AddExperience(_xpAmount);

        Destroy(gameObject);
    }

    private void ResolvePlayerTarget()
    {
        _playerStats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
        _target = _playerStats != null ? _playerStats.transform : null;
    }

    private Vector3 GetTargetAnchor()
    {
        if (_target == null)
            return transform.position;

        return SnapToPixelGrid(_target.position + new Vector3(0f, 0.55f, 0f));
    }

    private void TryCollect()
    {
        if (_target == null)
            return;

        if (Vector2.Distance(transform.position, GetTargetAnchor()) <= _collectRadius)
            Collect();
    }

    private void PushHistoryPoint()
    {
        float moved = Vector3.Distance(_lastHistoryPosition, transform.position);
        _historyAccumulator += moved;
        if (_historyAccumulator < (PixelStep * 0.75f) && _history.Count > 0)
            return;

        _historyAccumulator = 0f;
        _lastHistoryPosition = transform.position;
        _history.Insert(0, transform.position);
        if (_history.Count > HistoryCapacity)
            _history.RemoveAt(_history.Count - 1);
    }

    private void UpdateTailVisuals()
    {
        if (_coreRenderer != null)
            _coreRenderer.transform.position = SnapToPixelGrid(transform.position);

        for (int i = 0; i < _tailSegments.Count; i++)
        {
            var segment = _tailSegments[i];
            if (segment == null)
                continue;

            int startIndex = Mathf.Min(_history.Count - 1, i * 2);
            int endIndex = Mathf.Min(_history.Count - 1, (i + 1) * 2 + 1);
            if (_history.Count < 2 || startIndex < 0 || endIndex < 0 || startIndex == endIndex)
            {
                segment.enabled = false;
                continue;
            }

            Vector3 start = _history[startIndex];
            Vector3 end = _history[endIndex];
            Vector3 delta = start - end;
            float length = delta.magnitude;
            if (length <= 0.001f)
            {
                segment.enabled = false;
                continue;
            }

            segment.enabled = true;
            segment.transform.position = SnapToPixelGrid((start + end) * 0.5f);
            segment.transform.right = delta.normalized;
            float width = Mathf.Lerp(_tailWidth, _tailWidth * 0.45f, i / (float)Mathf.Max(1, _tailSegments.Count - 1));
            float scaleX = Mathf.Max(length * PixelsPerUnit / 4f, 0.8f);
            float scaleY = Mathf.Max(width, 0.55f);
            segment.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            float alpha = Mathf.Lerp(0.58f, 0.06f, i / (float)Mathf.Max(1, _tailSegments.Count - 1));
            segment.color = new Color(0.52f, 0.88f, 1f, alpha);
        }
    }

    private static void EnsureVisualAssetsBuilt()
    {
        if (s_coreSprite == null)
            s_coreSprite = BuildSoulCoreSprite();
        if (s_tailSprite == null)
            s_tailSprite = BuildSoulTailSprite();
        if (s_spriteMaterial == null)
            s_spriteMaterial = BuildMaterial();
    }

    private static Material BuildMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        return shader != null ? new Material(shader) : null;
    }

    private static Sprite BuildSoulCoreSprite()
    {
        const int size = 6;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color core = new Color(0.78f, 0.99f, 1f, 1f);
        Color mid = new Color(0.37f, 0.85f, 1f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, clear);
        }

        int[,] pixels =
        {
            { 2, 5, 1 }, { 3, 5, 1 },
            { 1, 4, 1 }, { 2, 4, 2 }, { 3, 4, 2 }, { 4, 4, 1 },
            { 1, 3, 2 }, { 2, 3, 2 }, { 3, 3, 2 }, { 4, 3, 2 },
            { 1, 2, 1 }, { 2, 2, 2 }, { 3, 2, 2 }, { 4, 2, 1 },
            { 2, 1, 1 }, { 3, 1, 1 },
        };

        for (int i = 0; i < pixels.GetLength(0); i++)
        {
            int x = pixels[i, 0];
            int y = pixels[i, 1];
            int tone = pixels[i, 2];
            texture.SetPixel(x, y, tone == 2 ? core : mid);
        }

        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    private static Sprite BuildSoulTailSprite()
    {
        const int width = 4;
        const int height = 2;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color color = new Color(0.46f, 0.82f, 1f, 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, clear);
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, color);
        }

        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    private static Vector3 SnapToPixelGrid(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x / PixelStep) * PixelStep,
            Mathf.Round(position.y / PixelStep) * PixelStep,
            position.z);
    }
}
