using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Scripts.Skills.Projectiles
{
    [DisallowMultipleComponent]
    public sealed class ProjectileVisualEffects : MonoBehaviour
    {
        public enum ParticlePreset
        {
            Sparks = 0,
            Smoke = 1,
            SparksAndSmoke = 2
        }

        public enum DistortionAxis
        {
            Horizontal = 0,
            Vertical = 1,
            Both = 2
        }

        [Serializable]
        public sealed class TrailSettings
        {
            public bool Enabled;
            [Range(0f, 3f)] public float Intensity = 1f;
            [Min(0.01f)] public float Time = 0.18f;
            [Min(0.001f)] public float StartWidth = 0.16f;
            [Min(0.001f)] public float EndWidth = 0.02f;
            [Min(0f)] public float MinVertexDistance = 0.01f;
            public Gradient Color = CreateGradient(new Color(1f, 0.55f, 0.12f, 0.75f), new Color(0.35f, 0.08f, 0.02f, 0f));
            public Material Material;
            public int SortingOrderOffset = -1;
        }

        [Serializable]
        public sealed class ParticleSettings
        {
            public bool Enabled;
            public ParticlePreset Preset = ParticlePreset.Sparks;
            [Range(0f, 3f)] public float Intensity = 1f;
            [Min(0f)] public float Rate = 18f;
            [Min(0.01f)] public float Lifetime = 0.28f;
            [Min(0f)] public float Speed = 0.8f;
            [Min(0.001f)] public float Size = 0.035f;
            [Range(0f, 180f)] public float ConeAngle = 55f;
            [Range(0.1f, 2f)] public float SpawnRadiusMultiplier = 0.75f;
            [Range(0f, 1f)] public float SpawnRadiusJitter = 0.15f;
            [Range(0f, 1f)] public float DirectionRandomness = 0.35f;
            public Color StartColor = new Color(1f, 0.72f, 0.2f, 0.9f);
            public Color EndColor = new Color(0.25f, 0.08f, 0.03f, 0f);
            public Material Material;
            public int SortingOrderOffset = 1;
        }

        [Serializable]
        public sealed class RibbonSettings
        {
            public bool Enabled;
            [Range(0f, 3f)] public float Intensity = 1f;
            [Range(2, 64)] public int MaxPoints = 14;
            [Min(0.001f)] public float SampleDistance = 0.035f;
            [Min(0.001f)] public float Width = 0.07f;
            public Gradient Color = CreateGradient(new Color(1f, 0.9f, 0.45f, 0.75f), new Color(1f, 0.2f, 0.05f, 0f));
            public Material Material;
            public int SortingOrderOffset = -2;
        }

        [Serializable]
        public sealed class DynamicGlowSettings
        {
            public bool Enabled;
            [Range(0f, 3f)] public float Intensity = 1f;
            public Color Color = new Color(1f, 0.42f, 0.08f, 1f);
            [Min(0f)] public float Radius = 1.6f;
            [Range(0f, 1f)] public float PulseAmount = 0.2f;
            [Min(0f)] public float PulseSpeed = 7f;
            public bool AdditiveSpriteMaterial = true;
        }

        [Serializable]
        public sealed class ColorOverLifetimeSettings
        {
            public bool Enabled;
            [Min(0.01f)] public float Lifetime = 1f;
            public bool Loop;
            [Range(0f, 3f)] public float Intensity = 1f;
            public Gradient Color = CreateGradient(new Color(1f, 0.22f, 0.02f, 1f), new Color(0.12f, 0.1f, 0.1f, 0.25f));
        }

        [Serializable]
        public sealed class ColorFlickerSettings
        {
            public bool Enabled;
            [Range(0f, 3f)] public float Intensity = 1f;
            public Color ColorA = new Color(1f, 0.72f, 0.18f, 1f);
            public Color ColorB = new Color(1f, 0.25f, 0.05f, 1f);
            [Min(0f)] public float Frequency = 24f;
        }

        [Serializable]
        public sealed class RippleSettings
        {
            public bool Enabled;
            [Range(0f, 3f)] public float Intensity = 1f;
            [Min(0.01f)] public float Interval = 0.12f;
            [Min(0.01f)] public float Lifetime = 0.28f;
            [Min(0.01f)] public float StartRadius = 0.06f;
            [Min(0.01f)] public float EndRadius = 0.45f;
            [Range(8, 48)] public int Segments = 18;
            [Min(0.001f)] public float Width = 0.02f;
            public Color Color = new Color(1f, 0.55f, 0.16f, 0.45f);
            public Material Material;
            public int SortingOrderOffset = -3;
        }

        [Serializable]
        public sealed class AirSparkSettings
        {
            public bool Enabled;
            [Range(0f, 3f)] public float Intensity = 1f;
            [Min(0f)] public float MinSpeed = 2.5f;
            [Min(0f)] public float RateAtMinSpeed = 8f;
            [Min(0f)] public float RateAtHighSpeed = 40f;
            [Min(0f)] public float HighSpeedReference = 10f;
            [Min(0.01f)] public float Lifetime = 0.22f;
            [Min(0.001f)] public float Size = 0.025f;
            public Color Color = new Color(1f, 0.8f, 0.25f, 0.9f);
            public Material Material;
            public int SortingOrderOffset = 2;
        }

        [Serializable]
        public sealed class DistortionSettings
        {
            public bool Enabled;
            [Range(0f, 3f)] public float Intensity = 1f;
            public DistortionAxis Axis = DistortionAxis.Horizontal;
            [Min(0f)] public float Amplitude = 0.025f;
            [Min(0f)] public float Frequency = 18f;
            [Range(0f, 1f)] public float Alpha = 0.2f;
            [Min(0.01f)] public float Scale = 1.25f;
            public int SortingOrderOffset = -1;
        }

        [Serializable]
        public sealed class HaloSettings
        {
            public bool Enabled;
            [Range(0f, 3f)] public float Intensity = 1f;
            [Range(1, 3)] public int Layers = 2;
            public Color Color = new Color(1f, 0.38f, 0.08f, 0.28f);
            [Min(1f)] public float BaseScale = 1.35f;
            [Min(0f)] public float ScaleStep = 0.28f;
            [Range(0f, 1f)] public float PulseAmount = 0.08f;
            [Min(0f)] public float PulseSpeed = 6f;
            public Material Material;
            public int SortingOrderOffset = -1;
        }

        [SerializeField] private TrailSettings _trail = new TrailSettings();
        [SerializeField] private ParticleSettings _particles = new ParticleSettings();
        [SerializeField] private RibbonSettings _ribbon = new RibbonSettings();
        [SerializeField] private DynamicGlowSettings _dynamicGlow = new DynamicGlowSettings();
        [SerializeField] private ColorOverLifetimeSettings _colorOverLifetime = new ColorOverLifetimeSettings();
        [SerializeField] private ColorFlickerSettings _colorFlicker = new ColorFlickerSettings();
        [SerializeField] private RippleSettings _ripple = new RippleSettings();
        [SerializeField] private AirSparkSettings _airSparks = new AirSparkSettings();
        [SerializeField] private DistortionSettings _distortion = new DistortionSettings();
        [SerializeField] private HaloSettings _halo = new HaloSettings();

        private readonly List<Vector3> _ribbonPoints = new List<Vector3>(32);
        private readonly List<RippleInstance> _ripples = new List<RippleInstance>(16);
        private readonly List<SpriteRenderer> _haloRenderers = new List<SpriteRenderer>(3);

        private SpriteRenderer _mainRenderer;
        private SpriteRenderer[] _allSpriteRenderers;
        private Color[] _baseSpriteColors;
        private Material _baseMainMaterial;
        private TrailRenderer _trailRenderer;
        private LineRenderer _ribbonRenderer;
        private ParticleSystem _particleSystem;
        private ParticleSystem _airSparkSystem;
        private SpriteRenderer _distortionRenderer;
        private Transform _dynamicLightTransform;
        private Component _dynamicLight;
        private Vector3 _lastPosition;
        private Vector3 _lastRibbonPoint;
        private Vector3 _distortionBaseLocalPosition;
        private float _age;
        private float _nextRippleAt;
        private float _currentSpeed;
        private Material _runtimeLineMaterial;
        private Material _runtimeParticleMaterial;
        private Material _runtimeAdditiveMaterial;
        private bool _resumeTrailEmissionNextLateUpdate;

        private void Awake()
        {
            EnsureSettings();
            CacheRenderers();
            EnsureRuntimeObjects();
            ResetRuntimeState();
        }

        private void OnEnable()
        {
            EnsureSettings();
            CacheRenderers();
            EnsureRuntimeObjects();
            ResetRuntimeState();
        }

        private void LateUpdate()
        {
            if (_mainRenderer == null)
                CacheRenderers();

            float dt = Time.deltaTime;
            _age += dt;
            _currentSpeed = dt > 0f ? (transform.position - _lastPosition).magnitude / dt : 0f;

            SyncRuntimeObjects();
            UpdateSpriteColor();
            UpdateRibbon();
            UpdateHalo();
            UpdateDynamicGlow();
            UpdateDistortion();
            UpdateRipples(dt);
            UpdateParticles(dt);
            UpdateAirSparks();

            _lastPosition = transform.position;

            if (_resumeTrailEmissionNextLateUpdate)
            {
                if (_trailRenderer != null)
                    _trailRenderer.emitting = _trail.Enabled;
                _resumeTrailEmissionNextLateUpdate = false;
            }
        }

        private void OnDisable()
        {
            ClearRipples();
            if (_trailRenderer != null) _trailRenderer.Clear();
            if (_ribbonRenderer != null) _ribbonRenderer.positionCount = 0;
            if (_particleSystem != null) _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_airSparkSystem != null) _airSparkSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ApplyBaseSpriteColors();
            RestoreBaseMaterial();
        }

        public void ResetVisualState()
        {
            EnsureSettings();
            CacheRenderers();
            EnsureRuntimeObjects();
            ResetRuntimeState();

            if (_trailRenderer != null)
            {
                _trailRenderer.emitting = false;
                _trailRenderer.Clear();
                _resumeTrailEmissionNextLateUpdate = _trail.Enabled;
            }

            if (_ribbonRenderer != null)
                _ribbonRenderer.positionCount = 0;

            if (_particleSystem != null)
            {
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (_particles.Enabled)
                    _particleSystem.Play(true);
            }

            if (_airSparkSystem != null)
            {
                _airSparkSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (_airSparks.Enabled)
                    _airSparkSystem.Play(true);
            }
        }

        private void EnsureSettings()
        {
            _trail ??= new TrailSettings();
            _particles ??= new ParticleSettings();
            _ribbon ??= new RibbonSettings();
            _dynamicGlow ??= new DynamicGlowSettings();
            _colorOverLifetime ??= new ColorOverLifetimeSettings();
            _colorFlicker ??= new ColorFlickerSettings();
            _ripple ??= new RippleSettings();
            _airSparks ??= new AirSparkSettings();
            _distortion ??= new DistortionSettings();
            _halo ??= new HaloSettings();

            if (_particles.SpawnRadiusMultiplier <= 0f)
                _particles.SpawnRadiusMultiplier = 0.75f;
        }

        private void CacheRenderers()
        {
            _mainRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>(true);
            if (_mainRenderer != null && _baseMainMaterial == null)
                _baseMainMaterial = _mainRenderer.sharedMaterial;
            _allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            _baseSpriteColors = new Color[_allSpriteRenderers.Length];
            for (int i = 0; i < _allSpriteRenderers.Length; i++)
                _baseSpriteColors[i] = _allSpriteRenderers[i] != null ? _allSpriteRenderers[i].color : Color.white;
        }

        private void EnsureRuntimeObjects()
        {
            _trailRenderer = GetOrCreateChildComponent<TrailRenderer>("ProjectileTrail");
            _ribbonRenderer = GetOrCreateChildComponent<LineRenderer>("ProjectileRibbon");
            _particleSystem = GetOrCreateChildComponent<ParticleSystem>("ProjectileParticles");
            _airSparkSystem = GetOrCreateChildComponent<ParticleSystem>("ProjectileAirSparks");
            _distortionRenderer = GetOrCreateChildComponent<SpriteRenderer>("ProjectileHeatDistortion");
            EnsureHaloRenderers();
            EnsureDynamicLight();
            SyncRuntimeObjects(force: true);
        }

        private void ResetRuntimeState()
        {
            _age = 0f;
            _lastPosition = transform.position;
            _lastRibbonPoint = transform.position;
            _ribbonPoints.Clear();
            _ribbonPoints.Add(transform.position);
            _nextRippleAt = 0f;
            ClearRipples();
            if (_trailRenderer != null) _trailRenderer.Clear();
            if (_ribbonRenderer != null) _ribbonRenderer.positionCount = 0;
            if (_distortionRenderer != null)
                _distortionBaseLocalPosition = _distortionRenderer.transform.localPosition;
        }

        private void SyncRuntimeObjects(bool force = false)
        {
            ConfigureTrail(force);
            ConfigureParticles(_particleSystem, _particles, force);
            ConfigureRibbon(force);
            ConfigureAirSparks(force);
            ConfigureHalo(force);
            ConfigureDistortion(force);
            ConfigureDynamicGlow(force);
        }

        private void ConfigureTrail(bool force)
        {
            if (_trailRenderer == null) return;
            _trailRenderer.enabled = _trail.Enabled;
            if (!_trail.Enabled && !force) return;
            _trailRenderer.emitting = _trail.Enabled && !_resumeTrailEmissionNextLateUpdate;

            _trailRenderer.time = Mathf.Max(0.01f, _trail.Time);
            _trailRenderer.startWidth = Mathf.Max(0.001f, _trail.StartWidth);
            _trailRenderer.endWidth = Mathf.Max(0.001f, _trail.EndWidth);
            _trailRenderer.minVertexDistance = Mathf.Max(0f, _trail.MinVertexDistance);
            _trailRenderer.colorGradient = ScaleGradientAlpha(_trail.Color, _trail.Intensity);
            _trailRenderer.material = _trail.Material != null ? _trail.Material : GetDefaultLineMaterial();
            ApplySorting(_trailRenderer, _trail.SortingOrderOffset);
        }

        private void ConfigureRibbon(bool force)
        {
            if (_ribbonRenderer == null) return;
            _ribbonRenderer.enabled = _ribbon.Enabled;
            if (!_ribbon.Enabled && !force) return;

            _ribbonRenderer.useWorldSpace = true;
            _ribbonRenderer.loop = false;
            _ribbonRenderer.widthMultiplier = Mathf.Max(0.001f, _ribbon.Width);
            _ribbonRenderer.colorGradient = ScaleGradientAlpha(_ribbon.Color, _ribbon.Intensity);
            _ribbonRenderer.material = _ribbon.Material != null ? _ribbon.Material : GetDefaultLineMaterial();
            ApplySorting(_ribbonRenderer, _ribbon.SortingOrderOffset);
        }

        private void ConfigureParticles(ParticleSystem system, ParticleSettings settings, bool force)
        {
            if (system == null) return;
            bool manualRadialEmission = UsesManualRadialParticleEmission(settings);
            var emission = system.emission;
            emission.enabled = settings.Enabled && !manualRadialEmission;
            emission.rateOverTime = manualRadialEmission ? 0f : Mathf.Max(0f, settings.Rate * settings.Intensity);

            if (!settings.Enabled && !force)
            {
                if (system.isPlaying) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = Mathf.Max(0.01f, settings.Lifetime);
            main.startSpeed = manualRadialEmission ? 0f : Mathf.Max(0f, settings.Speed);
            main.startSize = Mathf.Max(0.001f, settings.Size);
            main.startColor = ScaleColorAlpha(settings.StartColor, settings.Intensity);
            main.maxParticles = 256;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.001f, GetSpriteEmissionRadius(settings));

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateGradient(
                ScaleColorAlpha(settings.StartColor, settings.Intensity),
                ScaleColorAlpha(settings.EndColor, settings.Intensity)));

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.material = settings.Material != null ? settings.Material : GetDefaultParticleMaterial();
                ApplySorting(renderer, settings.SortingOrderOffset);
            }

            if (settings.Enabled && !system.isPlaying)
                system.Play(true);
        }

        private void ConfigureAirSparks(bool force)
        {
            if (_airSparkSystem == null) return;
            var emission = _airSparkSystem.emission;
            emission.enabled = _airSparks.Enabled;
            emission.rateOverTime = 0f;

            if (!_airSparks.Enabled && !force)
            {
                if (_airSparkSystem.isPlaying) _airSparkSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            var main = _airSparkSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = Mathf.Max(0.01f, _airSparks.Lifetime);
            main.startSpeed = 0f;
            main.startSize = Mathf.Max(0.001f, _airSparks.Size);
            main.startColor = ScaleColorAlpha(_airSparks.Color, _airSparks.Intensity);
            main.maxParticles = 128;

            var renderer = _airSparkSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.material = _airSparks.Material != null ? _airSparks.Material : GetDefaultParticleMaterial();
                ApplySorting(renderer, _airSparks.SortingOrderOffset);
            }

            if (_airSparks.Enabled && !_airSparkSystem.isPlaying)
                _airSparkSystem.Play(true);
        }

        private void ConfigureHalo(bool force)
        {
            for (int i = 0; i < _haloRenderers.Count; i++)
            {
                SpriteRenderer haloRenderer = _haloRenderers[i];
                if (haloRenderer == null) continue;

                bool active = _halo.Enabled && i < _halo.Layers;
                haloRenderer.enabled = active;
                if (!active && !force) continue;

                haloRenderer.sprite = _mainRenderer != null ? _mainRenderer.sprite : haloRenderer.sprite;
                haloRenderer.color = ScaleColorAlpha(_halo.Color, _halo.Intensity / Mathf.Max(1, i + 1));
                haloRenderer.sharedMaterial = _halo.Material != null ? _halo.Material : (_dynamicGlow.AdditiveSpriteMaterial ? GetDefaultAdditiveMaterial() : null);
                ApplySorting(haloRenderer, _halo.SortingOrderOffset - i);
            }
        }

        private void ConfigureDistortion(bool force)
        {
            if (_distortionRenderer == null) return;
            _distortionRenderer.enabled = _distortion.Enabled;
            if (!_distortion.Enabled && !force) return;

            _distortionRenderer.sprite = _mainRenderer != null ? _mainRenderer.sprite : _distortionRenderer.sprite;
            _distortionRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_distortion.Alpha * _distortion.Intensity));
            _distortionRenderer.sharedMaterial = GetDefaultLineMaterial();
            ApplySorting(_distortionRenderer, _distortion.SortingOrderOffset);
        }

        private void ConfigureDynamicGlow(bool force)
        {
            if (_dynamicLightTransform != null)
                _dynamicLightTransform.gameObject.SetActive(_dynamicGlow.Enabled);

            if (_mainRenderer != null && _dynamicGlow.AdditiveSpriteMaterial)
                _mainRenderer.sharedMaterial = GetDefaultAdditiveMaterial();
            else
                RestoreBaseMaterial();
        }

        private void UpdateSpriteColor()
        {
            if (_allSpriteRenderers == null || _baseSpriteColors == null)
                return;

            Color colorMultiplier = Color.white;
            if (_colorOverLifetime.Enabled)
            {
                float t = _colorOverLifetime.Loop
                    ? Mathf.Repeat(_age / Mathf.Max(0.01f, _colorOverLifetime.Lifetime), 1f)
                    : Mathf.Clamp01(_age / Mathf.Max(0.01f, _colorOverLifetime.Lifetime));
                colorMultiplier *= LerpToWhite(_colorOverLifetime.Color.Evaluate(t), _colorOverLifetime.Intensity);
            }

            if (_colorFlicker.Enabled)
            {
                float flicker = Mathf.PingPong(_age * Mathf.Max(0f, _colorFlicker.Frequency), 1f);
                Color flickerColor = Color.Lerp(_colorFlicker.ColorA, _colorFlicker.ColorB, flicker);
                colorMultiplier *= LerpToWhite(flickerColor, _colorFlicker.Intensity);
            }

            for (int i = 0; i < _allSpriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = _allSpriteRenderers[i];
                if (renderer == null || IsManagedRenderer(renderer))
                    continue;

                Color baseColor = i < _baseSpriteColors.Length ? _baseSpriteColors[i] : Color.white;
                renderer.color = MultiplyPreserveBaseAlpha(baseColor, colorMultiplier);
            }
        }

        private void UpdateRibbon()
        {
            if (!_ribbon.Enabled || _ribbonRenderer == null)
                return;

            Vector3 current = transform.position;
            if (_ribbonPoints.Count == 0 || Vector3.Distance(current, _lastRibbonPoint) >= Mathf.Max(0.001f, _ribbon.SampleDistance))
            {
                _ribbonPoints.Add(current);
                _lastRibbonPoint = current;
            }

            int maxPoints = Mathf.Max(2, _ribbon.MaxPoints);
            while (_ribbonPoints.Count > maxPoints)
                _ribbonPoints.RemoveAt(0);

            _ribbonRenderer.positionCount = _ribbonPoints.Count;
            for (int i = 0; i < _ribbonPoints.Count; i++)
                _ribbonRenderer.SetPosition(i, _ribbonPoints[i]);
        }

        private void UpdateHalo()
        {
            if (!_halo.Enabled || _mainRenderer == null)
                return;

            float pulse = 1f + Mathf.Sin(_age * Mathf.Max(0f, _halo.PulseSpeed)) * Mathf.Clamp01(_halo.PulseAmount);
            for (int i = 0; i < _haloRenderers.Count; i++)
            {
                SpriteRenderer haloRenderer = _haloRenderers[i];
                if (haloRenderer == null || !haloRenderer.enabled) continue;

                haloRenderer.sprite = _mainRenderer.sprite;
                float scale = (_halo.BaseScale + _halo.ScaleStep * i) * pulse;
                haloRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void UpdateDynamicGlow()
        {
            if (!_dynamicGlow.Enabled || _dynamicLight == null)
                return;

            float pulse = 1f + Mathf.Sin(_age * Mathf.Max(0f, _dynamicGlow.PulseSpeed)) * Mathf.Clamp01(_dynamicGlow.PulseAmount);
            SetLightProperty("color", _dynamicGlow.Color);
            SetLightProperty("intensity", Mathf.Max(0f, _dynamicGlow.Intensity) * pulse);
            SetLightProperty("pointLightOuterRadius", Mathf.Max(0f, _dynamicGlow.Radius) * pulse);
        }

        private void UpdateDistortion()
        {
            if (!_distortion.Enabled || _distortionRenderer == null)
                return;

            float wave = Mathf.Sin(_age * Mathf.Max(0f, _distortion.Frequency)) * Mathf.Max(0f, _distortion.Amplitude) * _distortion.Intensity;
            Vector3 offset = Vector3.zero;
            if (_distortion.Axis == DistortionAxis.Horizontal || _distortion.Axis == DistortionAxis.Both) offset.x = wave;
            if (_distortion.Axis == DistortionAxis.Vertical || _distortion.Axis == DistortionAxis.Both) offset.y = wave;
            _distortionRenderer.transform.localPosition = _distortionBaseLocalPosition + offset;
            _distortionRenderer.transform.localScale = new Vector3(_distortion.Scale, _distortion.Scale, 1f);
            if (_mainRenderer != null)
                _distortionRenderer.sprite = _mainRenderer.sprite;
        }

        private void UpdateRipples(float dt)
        {
            if (_ripple.Enabled && _age >= _nextRippleAt)
            {
                SpawnRipple();
                _nextRippleAt = _age + Mathf.Max(0.01f, _ripple.Interval);
            }

            for (int i = _ripples.Count - 1; i >= 0; i--)
            {
                RippleInstance ripple = _ripples[i];
                if (ripple == null || ripple.Renderer == null)
                {
                    _ripples.RemoveAt(i);
                    continue;
                }

                ripple.Age += dt;
                float t = Mathf.Clamp01(ripple.Age / Mathf.Max(0.01f, _ripple.Lifetime));
                float radius = Mathf.Lerp(_ripple.StartRadius, _ripple.EndRadius, Smooth01(t));
                Color color = ScaleColorAlpha(_ripple.Color, _ripple.Intensity * (1f - t));
                ripple.Renderer.startColor = color;
                ripple.Renderer.endColor = color;
                SetCirclePositions(ripple.Renderer, radius, Mathf.Max(8, _ripple.Segments));

                if (t >= 1f)
                {
                    Destroy(ripple.Renderer.gameObject);
                    _ripples.RemoveAt(i);
                }
            }
        }

        private void UpdateParticles(float dt)
        {
            if (!_particles.Enabled || _particleSystem == null || !UsesManualRadialParticleEmission(_particles))
                return;

            float rate = Mathf.Max(0f, _particles.Rate * _particles.Intensity);
            int emitCount = Mathf.FloorToInt(rate * dt);
            if (UnityEngine.Random.value < rate * dt - emitCount)
                emitCount++;
            if (emitCount <= 0)
                return;

            float radius = Mathf.Max(0.001f, GetSpriteEmissionRadius(_particles));
            Color startColor = ScaleColorAlpha(_particles.StartColor, _particles.Intensity);
            float lifetime = Mathf.Max(0.01f, _particles.Lifetime);
            float size = Mathf.Max(0.001f, _particles.Size);
            float speed = Mathf.Max(0f, _particles.Speed);

            for (int i = 0; i < emitCount; i++)
            {
                Vector2 edgeDirection = UnityEngine.Random.insideUnitCircle;
                if (edgeDirection.sqrMagnitude < 0.0001f)
                    edgeDirection = Vector2.up;
                edgeDirection.Normalize();

                float innerRadius = Mathf.Lerp(radius, radius * 0.2f, Mathf.Clamp01(_particles.SpawnRadiusJitter) * UnityEngine.Random.value);
                Vector2 spawnOffset = edgeDirection * innerRadius;

                Vector2 randomDirection = UnityEngine.Random.insideUnitCircle;
                if (randomDirection.sqrMagnitude < 0.0001f)
                    randomDirection = edgeDirection;
                randomDirection.Normalize();

                Vector2 velocityDirection = Vector2.Lerp(edgeDirection, randomDirection, Mathf.Clamp01(_particles.DirectionRandomness));
                if (velocityDirection.sqrMagnitude < 0.0001f)
                    velocityDirection = edgeDirection;
                velocityDirection.Normalize();

                var emitParams = new ParticleSystem.EmitParams
                {
                    position = transform.position + new Vector3(spawnOffset.x, spawnOffset.y, 0f),
                    velocity = new Vector3(velocityDirection.x, velocityDirection.y, 0f) * speed,
                    startColor = startColor,
                    startLifetime = lifetime,
                    startSize = size
                };

                _particleSystem.Emit(emitParams, 1);
            }
        }

        private void UpdateAirSparks()
        {
            if (!_airSparks.Enabled || _airSparkSystem == null || _currentSpeed < _airSparks.MinSpeed)
                return;

            float speedT = Mathf.InverseLerp(_airSparks.MinSpeed, Mathf.Max(_airSparks.MinSpeed + 0.01f, _airSparks.HighSpeedReference), _currentSpeed);
            float rate = Mathf.Lerp(_airSparks.RateAtMinSpeed, _airSparks.RateAtHighSpeed, speedT) * _airSparks.Intensity;
            int emitCount = Mathf.FloorToInt(rate * Time.deltaTime);
            if (UnityEngine.Random.value < rate * Time.deltaTime - emitCount)
                emitCount++;
            if (emitCount <= 0)
                return;

            var emitParams = new ParticleSystem.EmitParams
            {
                position = transform.position,
                startColor = ScaleColorAlpha(_airSparks.Color, _airSparks.Intensity),
                startLifetime = Mathf.Max(0.01f, _airSparks.Lifetime),
                startSize = Mathf.Max(0.001f, _airSparks.Size)
            };

            _airSparkSystem.Emit(emitParams, emitCount);
        }

        private bool UsesManualRadialParticleEmission(ParticleSettings settings)
        {
            return settings != null && (settings.Preset == ParticlePreset.Sparks || settings.Preset == ParticlePreset.SparksAndSmoke);
        }

        private float GetSpriteEmissionRadius(ParticleSettings settings)
        {
            float spriteRadius = 0.12f;
            if (_mainRenderer != null)
            {
                Bounds bounds = _mainRenderer.bounds;
                spriteRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
            }

            return spriteRadius * Mathf.Max(0.1f, settings?.SpawnRadiusMultiplier ?? 0.75f);
        }

        private void SpawnRipple()
        {
            var go = new GameObject("ProjectileRipple");
            go.transform.SetParent(null);
            go.transform.position = transform.position;
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = Mathf.Max(0.001f, _ripple.Width);
            line.material = _ripple.Material != null ? _ripple.Material : GetDefaultLineMaterial();
            ApplySorting(line, _ripple.SortingOrderOffset);
            SetCirclePositions(line, _ripple.StartRadius, Mathf.Max(8, _ripple.Segments));
            _ripples.Add(new RippleInstance { Renderer = line });
        }

        private void ClearRipples()
        {
            for (int i = 0; i < _ripples.Count; i++)
            {
                if (_ripples[i]?.Renderer != null)
                    Destroy(_ripples[i].Renderer.gameObject);
            }
            _ripples.Clear();
        }

        private void EnsureHaloRenderers()
        {
            _haloRenderers.Clear();
            for (int i = 0; i < 3; i++)
            {
                SpriteRenderer renderer = GetOrCreateChildComponent<SpriteRenderer>($"ProjectileHalo_{i + 1}");
                _haloRenderers.Add(renderer);
            }
        }

        private void EnsureDynamicLight()
        {
            Transform child = transform.Find("ProjectileDynamicLight");
            if (child == null)
            {
                child = new GameObject("ProjectileDynamicLight").transform;
                child.SetParent(transform, false);
            }

            _dynamicLightTransform = child;
            Type lightType = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
            if (lightType != null)
            {
                _dynamicLight = child.GetComponent(lightType);
                if (_dynamicLight == null)
                    _dynamicLight = child.gameObject.AddComponent(lightType);
            }
        }

        private T GetOrCreateChildComponent<T>(string childName) where T : Component
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(transform, false);
            }

            T component = child.GetComponent<T>();
            if (component == null)
                component = child.gameObject.AddComponent<T>();
            return component;
        }

        private bool IsManagedRenderer(SpriteRenderer renderer)
        {
            if (renderer == null)
                return false;

            if (renderer == _distortionRenderer)
                return true;

            for (int i = 0; i < _haloRenderers.Count; i++)
            {
                if (renderer == _haloRenderers[i])
                    return true;
            }

            return false;
        }

        private void ApplyBaseSpriteColors()
        {
            if (_allSpriteRenderers == null || _baseSpriteColors == null)
                return;

            for (int i = 0; i < _allSpriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = _allSpriteRenderers[i];
                if (renderer == null || IsManagedRenderer(renderer))
                    continue;

                renderer.color = i < _baseSpriteColors.Length ? _baseSpriteColors[i] : Color.white;
            }
        }

        private void RestoreBaseMaterial()
        {
            if (_mainRenderer != null && _baseMainMaterial != null)
                _mainRenderer.sharedMaterial = _baseMainMaterial;
        }

        private void ApplySorting(Renderer renderer, int offset)
        {
            if (renderer == null)
                return;

            if (_mainRenderer != null)
            {
                renderer.sortingLayerID = _mainRenderer.sortingLayerID;
                renderer.sortingOrder = _mainRenderer.sortingOrder + offset;
            }
            else
            {
                renderer.sortingOrder = offset;
            }
        }

        private Material GetDefaultLineMaterial()
        {
            if (_runtimeLineMaterial != null) return _runtimeLineMaterial;
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            _runtimeLineMaterial = shader != null ? new Material(shader) { hideFlags = HideFlags.HideAndDontSave } : null;
            return _runtimeLineMaterial;
        }

        private Material GetDefaultParticleMaterial()
        {
            if (_runtimeParticleMaterial != null) return _runtimeParticleMaterial;
            _runtimeParticleMaterial = GetDefaultLineMaterial();
            return _runtimeParticleMaterial;
        }

        private Material GetDefaultAdditiveMaterial()
        {
            if (_runtimeAdditiveMaterial != null) return _runtimeAdditiveMaterial;
            Shader shader = Shader.Find("Particles/Additive") ?? Shader.Find("Sprites/Default");
            _runtimeAdditiveMaterial = shader != null ? new Material(shader) { hideFlags = HideFlags.HideAndDontSave } : null;
            return _runtimeAdditiveMaterial;
        }

        private void SetLightProperty(string propertyName, object value)
        {
            if (_dynamicLight == null)
                return;

            PropertyInfo property = _dynamicLight.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite)
                property.SetValue(_dynamicLight, value);
        }

        private static void SetCirclePositions(LineRenderer line, float radius, int segments)
        {
            if (line == null) return;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

        private static Gradient CreateGradient(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) });
            return gradient;
        }

        private static Gradient ScaleGradientAlpha(Gradient source, float intensity)
        {
            if (source == null)
                return CreateGradient(Color.white, Color.clear);

            var gradient = new Gradient();
            GradientColorKey[] colors = source.colorKeys;
            GradientAlphaKey[] alphas = source.alphaKeys;
            float alphaScale = Mathf.Max(0f, intensity);
            for (int i = 0; i < alphas.Length; i++)
                alphas[i].alpha = Mathf.Clamp01(alphas[i].alpha * alphaScale);
            gradient.SetKeys(colors, alphas);
            return gradient;
        }

        private static Color ScaleColorAlpha(Color color, float intensity)
        {
            color.a = Mathf.Clamp01(color.a * Mathf.Max(0f, intensity));
            return color;
        }

        private static Color LerpToWhite(Color color, float intensity)
        {
            return Color.Lerp(Color.white, color, Mathf.Clamp01(intensity));
        }

        private static Color MultiplyPreserveBaseAlpha(Color baseColor, Color multiplier)
        {
            return new Color(
                baseColor.r * multiplier.r,
                baseColor.g * multiplier.g,
                baseColor.b * multiplier.b,
                baseColor.a * multiplier.a);
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private sealed class RippleInstance
        {
            public LineRenderer Renderer;
            public float Age;
        }
    }
}
