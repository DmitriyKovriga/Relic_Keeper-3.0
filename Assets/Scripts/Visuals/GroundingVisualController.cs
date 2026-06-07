using System;
using System.Collections.Generic;
using Scripts.Enemies;
using UnityEngine;

namespace Scripts.Visuals
{
    [DisallowMultipleComponent]
    public sealed class GroundingVisualController : MonoBehaviour
    {
        private enum ShadowPreset
        {
            Auto = 0,
            Small = 1,
            Medium = 2,
            Large = 3
        }

        private const string ShadowSmallPath = "VFX/Grounds/Shadow_Small";
        private const string ShadowMediumPath = "VFX/Grounds/Shadow_Medium";
        private const string ShadowLargePath = "VFX/Grounds/Shadow_Large";
        private const string DustLandSmallPath = "VFX/Grounds/Dust_Land_Small-Sheet";
        private const string DustDashSmallPath = "VFX/Grounds/Dust_Dash_Small-Sheet";
        private const string DustRunSmallPath = "VFX/Grounds/Dust_Run_Small-Sheet";
        private const string DustRunHeavyPath = "VFX/Grounds/Dust_Run_Heavy-Sheet";
        private const string GroundScrapePath = "VFX/Grounds/Ground_Scrape-Sheet";
        private const string DustHeavyImpactPath = "VFX/Grounds/Dust_Heavy_Impact";
        private const string DustHeavyImpactSheetPath = "VFX/Grounds/Dust_Heavy_Impact-Sheet";

        [Header("Shadow")]
        [SerializeField] private ShadowPreset _shadowPreset = ShadowPreset.Auto;
        [SerializeField, Range(-0.2f, 0.2f)] private float _shadowLift = -0.015f;
        [SerializeField, Range(0.5f, 1.5f)] private float _shadowWidthMultiplier = 1f;
        [SerializeField, Range(0f, 6f)] private float _shadowContactOverlapPixels = 1.5f;
        [SerializeField] private int _shadowOrderOffset = -1;

        [Header("Dust")]
        [SerializeField] private bool _spawnLandingDust = true;
        [SerializeField] private bool _spawnRunDust = true;
        [SerializeField] private bool _spawnDashDust = true;
        [SerializeField] private bool _spawnGroundScrape = true;
        [SerializeField, Min(0.01f)] private float _runDustIntervalSmall = 0.18f;
        [SerializeField, Min(0.01f)] private float _runDustIntervalHeavy = 0.28f;
        [SerializeField, Min(0f)] private float _runDustMinSpeed = 0.55f;
        [SerializeField, Min(0f)] private float _landingDustMinDownwardSpeed = 1.2f;
        [SerializeField, Min(0.01f)] private float _landingDustMinAirborneTime = 0.08f;
        [SerializeField] private int _dustOrderOffset = 3;
        [SerializeField] private int _landingDustOrderOffset = 6;
        [SerializeField, Min(0.05f)] private float _runDustStrideDistanceSmall = 1.15f;
        [SerializeField, Min(0.05f)] private float _runDustStrideDistanceHeavy = 1.45f;
        [SerializeField, Min(0f)] private float _groundEffectSurfaceLift = 0.01f;
        [SerializeField, Min(40)] private int _landingDustDurationMs = 280;
        [SerializeField, Min(40)] private int _heavyImpactDurationMs = 320;
        [SerializeField, Min(40)] private int _dashDustDurationMs = 360;
        [SerializeField, Min(40)] private int _groundScrapeDurationMs = 260;
        [SerializeField, Min(40)] private int _runDustSmallDurationMs = 160;
        [SerializeField, Min(40)] private int _runDustHeavyDurationMs = 220;
        [SerializeField, Range(0.25f, 3f)] private float _landingDustWidthRelativeToBody = 1.1f;
        [SerializeField, Range(0.25f, 3f)] private float _heavyImpactWidthRelativeToBody = 1.3f;
        [SerializeField, Range(0.25f, 3f)] private float _dashDustWidthRelativeToBody = 0.85f;
        [SerializeField, Range(0.25f, 3f)] private float _groundScrapeWidthRelativeToBody = 0.7f;
        [SerializeField, Range(0.25f, 3f)] private float _runDustSmallWidthRelativeToBody = 0.5f;
        [SerializeField, Range(0.25f, 3f)] private float _runDustHeavyWidthRelativeToBody = 0.65f;

        private SpriteRenderer _primaryRenderer;
        private Collider2D _collider;
        private PlayerMovement _playerMovement;
        private PlayerAttackInput _playerAttackInput;
        private EnemyLocomotion2D _enemyLocomotion;
        private EnemyEntity _enemyEntity;

        private GameObject _shadowObject;
        private SpriteRenderer _shadowRenderer;

        private bool _wasGrounded;
        private bool _wasDodging;
        private float _previousVerticalSpeed;
        private float _nextRunDustTime;
        private int _stepSide = 1;
        private int _groundMask;
        private float _lastRunDustX;
        private bool _hasRunDustSample;
        private float _airborneTime;
        private readonly List<Vector2> _shapeBuffer = new();
        private readonly List<Vector2> _localShapeBuffer = new();

        private Sprite _shadowSmall;
        private Sprite _shadowMedium;
        private Sprite _shadowLarge;
        private Sprite[] _dustLandSmall = Array.Empty<Sprite>();
        private Sprite[] _dustDashSmall = Array.Empty<Sprite>();
        private Sprite[] _dustRunSmall = Array.Empty<Sprite>();
        private Sprite[] _dustRunHeavy = Array.Empty<Sprite>();
        private Sprite[] _groundScrape = Array.Empty<Sprite>();
        private Sprite[] _dustHeavyImpact = Array.Empty<Sprite>();

        private void Awake()
        {
            CacheReferences();
            LoadResources();
            BuildGroundMask();
            CreateShadowRendererIfNeeded();
            _wasGrounded = ResolveGroundedState();
            _previousVerticalSpeed = ResolveVelocity().y;
        }

        private void LateUpdate()
        {
            CacheReferences();
            UpdateShadowVisual();
            UpdateGroundingEffects();
        }

        private void OnDestroy()
        {
            if (_shadowObject != null)
                Destroy(_shadowObject);
        }

        private void CacheReferences()
        {
            if (_enemyEntity == null)
                _enemyEntity = GetComponent<EnemyEntity>();

            if (_playerMovement == null)
                _playerMovement = GetComponent<PlayerMovement>();

            if (_playerAttackInput == null)
                _playerAttackInput = GetComponent<PlayerAttackInput>();

            if (_enemyLocomotion == null)
                _enemyLocomotion = GetComponent<EnemyLocomotion2D>();

            if (_collider == null)
                _collider = GetComponent<Collider2D>();

            if (_primaryRenderer == null)
            {
                if (_enemyEntity != null)
                    _primaryRenderer = _enemyEntity.VisualRenderer;

                if (_primaryRenderer == null)
                    _primaryRenderer = GetComponent<SpriteRenderer>();

                if (_primaryRenderer == null)
                {
                    SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
                    int bestSorting = int.MinValue;
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        SpriteRenderer renderer = renderers[i];
                        if (renderer == null || renderer == _shadowRenderer)
                            continue;

                        if (renderer.sortingOrder > bestSorting)
                        {
                            bestSorting = renderer.sortingOrder;
                            _primaryRenderer = renderer;
                        }
                    }
                }
            }
        }

        private void LoadResources()
        {
            _shadowSmall = LoadSprite(ShadowSmallPath);
            _shadowMedium = LoadSprite(ShadowMediumPath);
            _shadowLarge = LoadSprite(ShadowLargePath);
            _dustLandSmall = LoadSprites(DustLandSmallPath);
            _dustDashSmall = LoadSprites(DustDashSmallPath);
            _dustRunSmall = LoadSprites(DustRunSmallPath);
            _dustRunHeavy = LoadSprites(DustRunHeavyPath);
            _groundScrape = LoadSprites(GroundScrapePath);
            _dustHeavyImpact = LoadSprites(DustHeavyImpactPath);
            if (_dustHeavyImpact.Length == 0)
                _dustHeavyImpact = LoadSprites(DustHeavyImpactSheetPath);
        }

        private void BuildGroundMask()
        {
            _groundMask = 1 << 6;
            int oneWayPlatformLayer = LayerMask.NameToLayer("OneWayPlatform");
            if (oneWayPlatformLayer >= 0)
                _groundMask |= 1 << oneWayPlatformLayer;
        }

        private void CreateShadowRendererIfNeeded()
        {
            if (_shadowRenderer != null)
                return;

            _shadowObject = new GameObject("GroundShadow");
            Transform parent = transform.parent;
            if (parent != null)
                _shadowObject.transform.SetParent(parent, true);

            _shadowRenderer = _shadowObject.AddComponent<SpriteRenderer>();
            _shadowRenderer.enabled = false;
        }

        private void UpdateShadowVisual()
        {
            if (_shadowRenderer == null || _primaryRenderer == null)
                return;

            if (!TryResolveGroundAnchor(out Vector2 groundAnchor, out float heightAboveGround))
            {
                _shadowRenderer.enabled = false;
                return;
            }

            Sprite shadowSprite = ResolveShadowSprite();
            if (shadowSprite == null)
            {
                _shadowRenderer.enabled = false;
                return;
            }

            float targetWidth = ResolveShadowFootprintWidth() * Mathf.Max(0.5f, _shadowWidthMultiplier);
            float spriteWidth = Mathf.Max(0.01f, shadowSprite.bounds.size.x);
            float scale = targetWidth / spriteWidth;
            float topAnchorOffset = ResolveShadowTopAnchorOffset(shadowSprite, scale);
            float contactOverlap = ResolveShadowContactOverlapWorld(shadowSprite, scale);

            _shadowRenderer.enabled = true;
            _shadowRenderer.sprite = shadowSprite;
            _shadowRenderer.sortingLayerName = WorldRenderSorting.LayerBackground;
            _shadowRenderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.Environment, groundAnchor.y, _shadowOrderOffset);
            _shadowRenderer.color = Color.white;

            _shadowObject.transform.rotation = Quaternion.identity;
            _shadowObject.transform.localScale = new Vector3(scale, scale, 1f);
            _shadowObject.transform.position = new Vector3(groundAnchor.x, groundAnchor.y + _shadowLift - topAnchorOffset + contactOverlap, 0f);
        }

        private void UpdateGroundingEffects()
        {
            bool groundedNow = ResolveGroundedState();
            bool dodgingNow = _playerAttackInput != null && _playerAttackInput.IsDamageImmune;
            Vector2 velocity = ResolveVelocity();
            bool isHeavy = ResolveIsHeavy();

            bool qualifiesForLandingDust = _airborneTime >= _landingDustMinAirborneTime ||
                                           _previousVerticalSpeed <= -Mathf.Max(0.1f, _landingDustMinDownwardSpeed);

            if (_spawnLandingDust && groundedNow && !_wasGrounded && qualifiesForLandingDust)
                SpawnLandingOrHeavyImpact(isHeavy);

            if (_spawnDashDust && groundedNow && !_wasDodging && dodgingNow)
                SpawnDashEffects();

            if (_spawnRunDust && groundedNow && !dodgingNow && Mathf.Abs(velocity.x) >= _runDustMinSpeed)
            {
                float currentX = ResolveBounds().center.x;
                float strideDistance = isHeavy ? _runDustStrideDistanceHeavy : _runDustStrideDistanceSmall;
                if (!_hasRunDustSample)
                {
                    _lastRunDustX = currentX;
                    _hasRunDustSample = true;
                }
                else if (Mathf.Abs(currentX - _lastRunDustX) >= strideDistance && Time.time >= _nextRunDustTime)
                {
                    SpawnRunDust(isHeavy);
                    float interval = isHeavy ? _runDustIntervalHeavy : _runDustIntervalSmall;
                    _nextRunDustTime = Time.time + Mathf.Max(0.01f, interval);
                    _lastRunDustX = currentX;
                }
            }
            else
            {
                _hasRunDustSample = false;
            }

            if (groundedNow)
                _airborneTime = 0f;
            else
                _airborneTime += Time.deltaTime;

            _wasGrounded = groundedNow;
            _wasDodging = dodgingNow;
            _previousVerticalSpeed = velocity.y;
        }

        private void SpawnLandingOrHeavyImpact(bool isHeavy)
        {
            Sprite[] frames = isHeavy ? ResolveHeavyImpactFrames() : _dustLandSmall;
            SpawnGroundEffect(
                frames,
                DurationMsToSeconds(isHeavy ? _heavyImpactDurationMs : _landingDustDurationMs),
                ResolveGroundEffectPosition(0f),
                false,
                1f,
                ResolveLandingDustSortingOrder(),
                ResolveEffectWidth(isHeavy ? _heavyImpactWidthRelativeToBody : _landingDustWidthRelativeToBody));
        }

        private void SpawnDashEffects()
        {
            float direction = ResolveFacingDirection();
            SpawnGroundEffect(
                _dustDashSmall,
                DurationMsToSeconds(_dashDustDurationMs),
                ResolveGroundEffectPosition(-direction * 0.08f),
                direction < 0f,
                1f,
                ResolveDustSortingOrder(),
                ResolveEffectWidth(_dashDustWidthRelativeToBody));

            if (_spawnGroundScrape)
            {
                SpawnGroundEffect(
                    _groundScrape,
                    DurationMsToSeconds(_groundScrapeDurationMs),
                    ResolveGroundEffectPosition(-direction * 0.12f),
                    direction < 0f,
                    1f,
                    ResolveDustSortingOrder(),
                    ResolveEffectWidth(_groundScrapeWidthRelativeToBody));
            }
        }

        private void SpawnRunDust(bool isHeavy)
        {
            Sprite[] frames = isHeavy && _dustRunHeavy.Length > 0 ? _dustRunHeavy : _dustRunSmall;
            if (frames == null || frames.Length == 0)
                return;

            Bounds bounds = ResolveBounds();
            float footOffset = Mathf.Max(0.05f, bounds.extents.x * 0.32f) * _stepSide;
            _stepSide *= -1;

            SpawnGroundEffect(
                frames,
                DurationMsToSeconds(isHeavy ? _runDustHeavyDurationMs : _runDustSmallDurationMs),
                ResolveGroundEffectPosition(footOffset),
                false,
                1f,
                ResolveDustSortingOrder(),
                ResolveEffectWidth(isHeavy ? _runDustHeavyWidthRelativeToBody : _runDustSmallWidthRelativeToBody));
        }

        private void SpawnGroundEffect(Sprite[] frames, float duration, Vector3 position, bool flipX, float alpha, int orderOffset, float targetWorldWidth)
        {
            if (frames == null || frames.Length == 0 || _primaryRenderer == null)
                return;

            GameObject effect = new("GroundingFx");
            Transform parent = transform.parent;
            if (parent != null)
                effect.transform.SetParent(parent, true);

            effect.transform.position = position;
            effect.transform.rotation = Quaternion.identity;
            effect.transform.position = ResolveAlignedGroundEffectPosition(position, frames, targetWorldWidth);

            string layerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.GameplayVfx);
            int sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.GameplayVfx, position.y, orderOffset);

            GroundingSpriteSheetVfx overlay = effect.AddComponent<GroundingSpriteSheetVfx>();
            overlay.Initialize(frames, duration, alpha, SortingLayer.NameToID(layerName), sortingOrder, targetWorldWidth, flipX);
        }

        private int ResolveDustSortingOrder()
        {
            return _dustOrderOffset;
        }

        private int ResolveLandingDustSortingOrder()
        {
            return _landingDustOrderOffset;
        }

        private Vector3 ResolveGroundEffectPosition(float xOffset)
        {
            if (TryResolveGroundAnchor(out Vector2 anchor, out _))
                return new Vector3(anchor.x + xOffset, anchor.y + 0.01f, 0f);

            Bounds bounds = ResolveBounds();
            return new Vector3(bounds.center.x + xOffset, bounds.min.y, 0f);
        }

        private Sprite ResolveShadowSprite()
        {
            ShadowPreset preset = _shadowPreset == ShadowPreset.Auto ? ResolveAutoShadowPreset() : _shadowPreset;
            return preset switch
            {
                ShadowPreset.Small => _shadowSmall,
                ShadowPreset.Medium => _shadowMedium,
                ShadowPreset.Large => _shadowLarge,
                _ => _shadowMedium ?? _shadowSmall
            };
        }

        private ShadowPreset ResolveAutoShadowPreset()
        {
            if (_playerMovement != null)
                return ShadowPreset.Medium;

            Bounds bounds = ResolveVisualBounds();
            if (bounds.size.x >= 1.15f || bounds.size.y >= 1.65f)
                return ShadowPreset.Large;
            if (bounds.size.x >= 0.6f || bounds.size.y >= 0.95f)
                return ShadowPreset.Medium;
            return ShadowPreset.Small;
        }

        private bool ResolveIsHeavy()
        {
            ShadowPreset preset = _shadowPreset == ShadowPreset.Auto ? ResolveAutoShadowPreset() : _shadowPreset;
            return preset == ShadowPreset.Large;
        }

        private bool ResolveGroundedState()
        {
            if (_playerMovement != null)
                return _playerMovement.IsGrounded;
            if (_enemyLocomotion != null)
                return _enemyLocomotion.IsGrounded;

            if (_collider == null)
                return false;

            Bounds bounds = _collider.bounds;
            Vector2 origin = new(bounds.center.x, bounds.min.y + 0.02f);
            Vector2 size = new(Mathf.Max(0.05f, bounds.size.x * 0.8f), 0.05f);
            return Physics2D.BoxCast(origin, size, 0f, Vector2.down, 0.08f, _groundMask).collider != null;
        }

        private Vector2 ResolveVelocity()
        {
            if (_playerMovement != null)
                return _playerMovement.CurrentVelocity;

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            return rb != null ? rb.linearVelocity : Vector2.zero;
        }

        private float ResolveFacingDirection()
        {
            if (_enemyLocomotion != null)
                return _enemyLocomotion.FacingDirection;

            return transform.localScale.x >= 0f ? 1f : -1f;
        }

        private Bounds ResolveBounds()
        {
            if (_collider != null)
                return _collider.bounds;
            if (_primaryRenderer != null)
                return _primaryRenderer.bounds;

            return new Bounds(transform.position, Vector3.one);
        }

        private Bounds ResolveVisualBounds()
        {
            if (_primaryRenderer != null)
                return _primaryRenderer.bounds;

            return ResolveBounds();
        }

        private bool TryResolveGroundAnchor(out Vector2 anchor, out float heightAboveGround)
        {
            Bounds bounds = ResolveBounds();
            Vector2 shadowOffset = ResolveShadowOffset();
            float x = ResolveShadowAnchorX() + shadowOffset.x;

            if (ResolveGroundedState())
            {
                float groundedY = bounds.min.y;
                if (TryResolveOpaqueGroundY(out float opaqueGroundY))
                    groundedY = opaqueGroundY;

                anchor = new Vector2(x, groundedY + shadowOffset.y);
                heightAboveGround = 0f;
                return true;
            }

            Vector2 origin = new(x, bounds.max.y + 0.1f);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 8f, _groundMask);
            if (hit.collider != null)
            {
                anchor = new Vector2(x, hit.point.y + shadowOffset.y);
                heightAboveGround = Mathf.Max(0f, bounds.min.y - hit.point.y);
                return true;
            }

            anchor = default;
            heightAboveGround = 0f;
            return false;
        }

        private float ResolveShadowAnchorX()
        {
            if (TryResolveOpaqueFootprint(out float centerX, out _))
                return centerX;

            Bounds visualBounds = ResolveVisualBounds();
            Bounds bodyBounds = ResolveBounds();

            float visualCenterX = visualBounds.center.x;
            float bodyCenterX = bodyBounds.center.x;
            float bodyHalfWidth = Mathf.Max(0.01f, bodyBounds.extents.x);

            return Mathf.Clamp(visualCenterX, bodyCenterX - bodyHalfWidth, bodyCenterX + bodyHalfWidth);
        }

        private Sprite[] ResolveHeavyImpactFrames()
        {
            if (_dustHeavyImpact.Length > 0)
                return _dustHeavyImpact;
            if (_dustRunHeavy.Length > 0)
                return _dustRunHeavy;
            return _dustLandSmall;
        }

        private float ResolveEffectWidth(float relativeToBody)
        {
            Bounds bounds = ResolveBounds();
            return Mathf.Max(0.08f, bounds.size.x * Mathf.Max(0.1f, relativeToBody));
        }

        private float ResolveShadowFootprintWidth()
        {
            if (TryResolveOpaqueFootprint(out _, out float opaqueWidth))
                return Mathf.Max(0.08f, opaqueWidth) * ResolveShadowWidthScale();

            Bounds visualBounds = ResolveVisualBounds();
            Bounds bodyBounds = ResolveBounds();

            float visualWidth = Mathf.Max(0.08f, visualBounds.size.x);
            float visualHeight = Mathf.Max(0.08f, visualBounds.size.y);
            float bodyWidth = Mathf.Max(0.08f, bodyBounds.size.x);

            float spriteBasedFootprint = Mathf.Min(visualWidth, visualHeight * 0.9f);
            return Mathf.Max(bodyWidth, spriteBasedFootprint) * ResolveShadowWidthScale();
        }

        private bool TryResolveOpaqueFootprint(out float centerX, out float width)
        {
            centerX = 0f;
            width = 0f;

            if (_primaryRenderer == null || _primaryRenderer.sprite == null)
                return false;

            if (!TryCollectSpriteShapeWorldPoints(_primaryRenderer, _shapeBuffer))
                return false;

            if (_shapeBuffer.Count == 0)
                return false;

            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            for (int i = 0; i < _shapeBuffer.Count; i++)
            {
                Vector2 point = _shapeBuffer[i];
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            float height = Mathf.Max(0.01f, maxY - minY);
            float bandTop = minY + height * 0.28f;

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            bool foundBandPoint = false;
            for (int i = 0; i < _shapeBuffer.Count; i++)
            {
                Vector2 point = _shapeBuffer[i];
                if (point.y > bandTop)
                    continue;

                foundBandPoint = true;
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
            }

            if (!foundBandPoint)
            {
                for (int i = 0; i < _shapeBuffer.Count; i++)
                {
                    Vector2 point = _shapeBuffer[i];
                    minX = Mathf.Min(minX, point.x);
                    maxX = Mathf.Max(maxX, point.x);
                }
            }

            if (!float.IsFinite(minX) || !float.IsFinite(maxX))
                return false;

            width = Mathf.Max(0.01f, maxX - minX);
            centerX = (minX + maxX) * 0.5f;
            return true;
        }

        private bool TryResolveOpaqueGroundY(out float groundY)
        {
            groundY = 0f;

            if (_primaryRenderer == null || _primaryRenderer.sprite == null)
                return false;

            if (!TryCollectSpriteShapeWorldPoints(_primaryRenderer, _shapeBuffer) || _shapeBuffer.Count == 0)
                return false;

            float minY = float.PositiveInfinity;
            for (int i = 0; i < _shapeBuffer.Count; i++)
                minY = Mathf.Min(minY, _shapeBuffer[i].y);

            if (!float.IsFinite(minY))
                return false;

            groundY = minY;
            return true;
        }

        private float ResolveShadowTopAnchorOffset(Sprite shadowSprite, float scale)
        {
            if (shadowSprite == null)
                return 0f;

            float topY = shadowSprite.bounds.max.y;
            if (TryResolveSpriteLocalVerticalExtents(shadowSprite, _localShapeBuffer, out _, out float opaqueTopY))
                topY = opaqueTopY;

            return topY * scale;
        }

        private float ResolveShadowContactOverlapWorld(Sprite shadowSprite, float scale)
        {
            if (shadowSprite == null)
                return 0f;

            float pixelsPerUnit = Mathf.Max(1f, shadowSprite.pixelsPerUnit);
            return (_shadowContactOverlapPixels / pixelsPerUnit) * scale;
        }

        private static bool TryResolveSpriteLocalVerticalExtents(Sprite sprite, List<Vector2> buffer, out float minY, out float maxY)
        {
            minY = 0f;
            maxY = 0f;

            if (!TryCollectSpriteShapeLocalPoints(sprite, buffer) || buffer.Count == 0)
                return false;

            minY = float.PositiveInfinity;
            maxY = float.NegativeInfinity;
            for (int i = 0; i < buffer.Count; i++)
            {
                Vector2 point = buffer[i];
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            return float.IsFinite(minY) && float.IsFinite(maxY);
        }

        private static bool TryCollectSpriteShapeLocalPoints(Sprite sprite, List<Vector2> output)
        {
            output.Clear();

            if (sprite == null)
                return false;

            if (sprite.GetPhysicsShapeCount() > 0)
            {
                int shapeCount = sprite.GetPhysicsShapeCount();
                var physicsBuffer = new List<Vector2>();
                for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
                {
                    physicsBuffer.Clear();
                    sprite.GetPhysicsShape(shapeIndex, physicsBuffer);
                    for (int i = 0; i < physicsBuffer.Count; i++)
                        output.Add(physicsBuffer[i]);
                }
            }
            else
            {
                Vector2[] vertices = sprite.vertices;
                if (vertices == null || vertices.Length == 0)
                    return false;

                for (int i = 0; i < vertices.Length; i++)
                    output.Add(vertices[i]);
            }

            return output.Count > 0;
        }

        private static bool TryCollectSpriteShapeWorldPoints(SpriteRenderer renderer, List<Vector2> output)
        {
            if (renderer == null || renderer.sprite == null)
                return false;

            Sprite sprite = renderer.sprite;
            Transform visualTransform = renderer.transform;
            if (!TryCollectSpriteShapeLocalPoints(sprite, output))
                return false;

            for (int i = 0; i < output.Count; i++)
                output[i] = TransformSpritePointToWorld(visualTransform, renderer, output[i]);

            return output.Count > 0;
        }

        private static Vector2 TransformSpritePointToWorld(Transform visualTransform, SpriteRenderer renderer, Vector2 localPoint)
        {
            if (renderer.flipX)
                localPoint.x = -localPoint.x;
            if (renderer.flipY)
                localPoint.y = -localPoint.y;

            return visualTransform.TransformPoint(localPoint);
        }

        private Vector2 ResolveShadowOffset()
        {
            if (_enemyEntity != null && _enemyEntity.Data != null && _enemyEntity.Data.Animation != null)
                return _enemyEntity.Data.Animation.GroundShadowOffset;

            return Vector2.zero;
        }

        private float ResolveShadowWidthScale()
        {
            if (_enemyEntity != null && _enemyEntity.Data != null && _enemyEntity.Data.Animation != null)
                return Mathf.Max(0.1f, _enemyEntity.Data.Animation.GroundShadowWidthScale);

            return 1f;
        }

        private Vector3 ResolveAlignedGroundEffectPosition(Vector3 groundPosition, Sprite[] frames, float targetWorldWidth)
        {
            if (frames == null || frames.Length == 0)
                return groundPosition;

            float maxFrameWidth = 0.01f;
            float maxFrameHeight = 0.01f;
            for (int i = 0; i < frames.Length; i++)
            {
                Sprite frame = frames[i];
                if (frame == null)
                    continue;

                Vector2 size = frame.bounds.size;
                maxFrameWidth = Mathf.Max(maxFrameWidth, size.x);
                maxFrameHeight = Mathf.Max(maxFrameHeight, size.y);
            }

            float scale = Mathf.Max(0.01f, targetWorldWidth) / maxFrameWidth;
            float worldHeight = maxFrameHeight * scale;
            return new Vector3(groundPosition.x, groundPosition.y + (worldHeight * 0.5f) + _groundEffectSurfaceLift, groundPosition.z);
        }

        private static float DurationMsToSeconds(int milliseconds)
        {
            return Mathf.Max(0.04f, milliseconds / 1000f);
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites != null && sprites.Length > 0)
            {
                Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
                return sprites[0];
            }

            return Resources.Load<Sprite>(resourcePath);
        }

        private static Sprite[] LoadSprites(string resourcePath)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites != null && sprites.Length > 0)
            {
                Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
                return sprites;
            }

            Sprite single = Resources.Load<Sprite>(resourcePath);
            return single != null ? new[] { single } : Array.Empty<Sprite>();
        }
    }

    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GroundingSpriteSheetVfx : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private float _duration;
        private float _elapsed;

        public void Initialize(Sprite[] frames, float duration, float alpha, int sortingLayerId, int sortingOrder, float targetWorldWidth, bool flipX)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _frames = frames;
            _duration = Mathf.Max(0.01f, duration);
            _elapsed = 0f;

            if (_renderer == null)
                _renderer = gameObject.AddComponent<SpriteRenderer>();

            _renderer.sortingLayerID = sortingLayerId;
            _renderer.sortingOrder = sortingOrder;
            _renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));

            if (_frames != null && _frames.Length > 0)
            {
                _renderer.sprite = _frames[0];
                ApplyScale(targetWorldWidth, flipX);
            }
        }

        private void Update()
        {
            if (_renderer == null || _frames == null || _frames.Length == 0)
            {
                Destroy(gameObject);
                return;
            }

            _elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(_elapsed / _duration);
            int frameIndex = Mathf.Clamp(Mathf.FloorToInt(normalized * _frames.Length), 0, _frames.Length - 1);
            _renderer.sprite = _frames[frameIndex];

            if (_elapsed >= _duration)
                Destroy(gameObject);
        }

        private void ApplyScale(float targetWorldWidth, bool flipX)
        {
            if (_renderer == null || _renderer.sprite == null)
                return;

            float spriteWidth = Mathf.Max(0.01f, _renderer.sprite.bounds.size.x);
            float scale = Mathf.Max(0.01f, targetWorldWidth) / spriteWidth;
            transform.localScale = new Vector3(flipX ? -scale : scale, scale, 1f);
        }
    }
}
