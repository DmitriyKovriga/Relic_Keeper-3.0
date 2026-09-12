using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Visuals;

namespace Scripts.Skills.Visuals
{
    [DisallowMultipleComponent]
    public sealed class SkillChainVisualEffects : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(0f)] private float _segmentDelay = 0.06f;
        [SerializeField, Min(0.01f)] private float _segmentLifetime = 0.18f;
        [SerializeField, Min(0f)] private float _fadeOutDuration = 0.12f;
        [SerializeField] private bool _playSequentially = true;

        [Header("Jagged beam")]
        [SerializeField] private bool _jaggedBeamEnabled = true;
        [SerializeField] private Color _coreColor = new Color(0.88f, 0.98f, 1f, 1f);
        [SerializeField] private Color _glowColor = new Color(0.25f, 0.72f, 1f, 0.55f);
        [SerializeField, Min(0.001f)] private float _coreWidth = 0.035f;
        [SerializeField, Min(0.001f)] private float _glowWidth = 0.12f;
        [SerializeField, Min(0f)] private float _jitterAmount = 0.13f;
        [SerializeField, Range(2, 16)] private int _jitterPointCount = 7;
        [SerializeField, Min(0)] private int _branchCount = 2;
        [SerializeField, Min(0f)] private float _branchLength = 0.28f;

        [Header("Impact bursts")]
        [SerializeField] private bool _impactBurstsEnabled = true;
        [SerializeField] private GameObject _impactPrefab;
        [SerializeField, Min(0.01f)] private float _impactScale = 0.45f;
        [SerializeField, Min(0.01f)] private float _impactLifetime = 0.16f;
        [SerializeField] private Color _impactColor = new Color(0.7f, 0.95f, 1f, 0.85f);

        [Header("Sorting")]
        [SerializeField] private int _sortingOrderOffset;

        private static Material _lineMaterial;
        private readonly List<LineRenderer> _activeLines = new List<LineRenderer>();
        private readonly List<GameObject> _activeObjects = new List<GameObject>();

        public void Play(IReadOnlyList<Vector3> points, float segmentDelayOverride = -1f, float lifetimeOverride = -1f)
        {
            StopAllCoroutines();
            ClearRuntimeObjects();

            if (points == null || points.Count < 2)
                return;

            float delay = segmentDelayOverride >= 0f ? segmentDelayOverride : _segmentDelay;
            float lifetime = lifetimeOverride > 0f ? lifetimeOverride : _segmentLifetime;
            StartCoroutine(PlayRoutine(points, delay, lifetime));
        }

        private IEnumerator PlayRoutine(IReadOnlyList<Vector3> points, float segmentDelay, float segmentLifetime)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                SpawnSegment(points[i], points[i + 1], segmentLifetime);
                if (_impactBurstsEnabled)
                    SpawnImpact(points[i + 1]);

                if (_playSequentially && segmentDelay > 0f)
                    yield return new WaitForSeconds(segmentDelay);
            }

            float totalLifetime = segmentLifetime + Mathf.Max(0f, _fadeOutDuration);
            if (!_playSequentially)
                totalLifetime += Mathf.Max(0f, segmentDelay);
            yield return new WaitForSeconds(totalLifetime);
            ClearRuntimeObjects();
        }

        private void SpawnSegment(Vector3 start, Vector3 end, float lifetime)
        {
            if (!_jaggedBeamEnabled)
                return;

            Vector3[] points = BuildJaggedPoints(start, end);
            LineRenderer glow = CreateLine("Glow", _glowColor, _glowWidth);
            LineRenderer core = CreateLine("Core", _coreColor, _coreWidth);
            ApplyLineSorting(glow, start, end);
            ApplyLineSorting(core, start, end);
            ApplyLinePoints(glow, points);
            ApplyLinePoints(core, points);
            StartCoroutine(FadeAndDestroyLine(glow, lifetime));
            StartCoroutine(FadeAndDestroyLine(core, lifetime));

            for (int i = 0; i < _branchCount; i++)
                SpawnBranch(points, lifetime);
        }

        private void SpawnBranch(IReadOnlyList<Vector3> sourcePoints, float lifetime)
        {
            if (sourcePoints == null || sourcePoints.Count < 3 || _branchLength <= 0f)
                return;

            int index = Random.Range(1, sourcePoints.Count - 1);
            Vector3 origin = sourcePoints[index];
            Vector2 tangent = ((Vector2)(sourcePoints[Mathf.Min(index + 1, sourcePoints.Count - 1)] - sourcePoints[Mathf.Max(index - 1, 0)])).normalized;
            if (tangent.sqrMagnitude <= 0.0001f)
                tangent = Vector2.right;

            Vector2 normal = new Vector2(-tangent.y, tangent.x) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 end = origin + (Vector3)(normal * Random.Range(_branchLength * 0.35f, _branchLength));
            LineRenderer branch = CreateLine("Branch", _glowColor, Mathf.Max(0.005f, _coreWidth * 0.7f));
            ApplyLineSorting(branch, origin, end);
            ApplyLinePoints(branch, new[] { origin, end });
            StartCoroutine(FadeAndDestroyLine(branch, lifetime * 0.75f));
        }

        private Vector3[] BuildJaggedPoints(Vector3 start, Vector3 end)
        {
            int count = Mathf.Max(2, _jitterPointCount);
            var points = new Vector3[count];
            Vector2 direction = end - start;
            Vector2 normal = direction.sqrMagnitude > 0.0001f
                ? new Vector2(-direction.y, direction.x).normalized
                : Vector2.up;

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 1f : i / (float)(count - 1);
                Vector3 point = Vector3.Lerp(start, end, t);
                if (i > 0 && i < count - 1)
                    point += (Vector3)(normal * Random.Range(-_jitterAmount, _jitterAmount));

                points[i] = point;
            }

            return points;
        }

        private LineRenderer CreateLine(string suffix, Color color, float width)
        {
            GameObject child = new GameObject($"ChainLine_{suffix}");
            child.transform.SetParent(transform, false);
            _activeObjects.Add(child);

            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.widthMultiplier = width;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            Material material = GetLineMaterial();
            if (material != null)
                line.material = material;
            line.startColor = color;
            line.endColor = color;
            _activeLines.Add(line);
            return line;
        }

        private void ApplyLineSorting(LineRenderer line, Vector3 start, Vector3 end)
        {
            if (line == null)
                return;

            float anchorY = (start.y + end.y) * 0.5f;
            line.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.HeroAttackVfx);
            line.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.HeroAttackVfx, anchorY, _sortingOrderOffset);
        }

        private static void ApplyLinePoints(LineRenderer line, IReadOnlyList<Vector3> points)
        {
            if (line == null || points == null)
                return;

            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                line.SetPosition(i, points[i]);
        }

        private IEnumerator FadeAndDestroyLine(LineRenderer line, float lifetime)
        {
            if (line == null)
                yield break;

            yield return new WaitForSeconds(Mathf.Max(0.01f, lifetime));

            float fade = Mathf.Max(0f, _fadeOutDuration);
            Color start = line.startColor;
            if (fade > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fade && line != null)
                {
                    elapsed += Time.deltaTime;
                    float alpha = Mathf.Lerp(start.a, 0f, Mathf.Clamp01(elapsed / fade));
                    Color c = new Color(start.r, start.g, start.b, alpha);
                    line.startColor = c;
                    line.endColor = c;
                    yield return null;
                }
            }

            if (line != null)
                Destroy(line.gameObject);
        }

        private void SpawnImpact(Vector3 position)
        {
            GameObject impact = _impactPrefab != null
                ? Instantiate(_impactPrefab, position, Quaternion.identity, transform)
                : CreateDefaultImpact(position);

            if (impact == null)
                return;

            _activeObjects.Add(impact);
            impact.transform.localScale = Vector3.one * _impactScale;
            WorldRenderSorting.ConfigureSorter(impact, RenderDepthCategory.HeroAttackVfx, position.y, _sortingOrderOffset, staticAnchor: true);
            StartCoroutine(DestroyAfter(impact, _impactLifetime));
        }

        private GameObject CreateDefaultImpact(Vector3 position)
        {
            GameObject impact = new GameObject("ChainImpact");
            impact.transform.SetParent(transform, false);
            impact.transform.position = position;
            LineRenderer line = impact.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 5;
            line.loop = true;
            line.widthMultiplier = Mathf.Max(0.005f, _coreWidth);
            Material material = GetLineMaterial();
            if (material != null)
                line.material = material;
            line.startColor = _impactColor;
            line.endColor = _impactColor;
            ApplyLineSorting(line, position, position);

            float radius = 0.12f;
            for (int i = 0; i < 5; i++)
            {
                float angle = i / 5f * Mathf.PI * 2f;
                line.SetPosition(i, position + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }

            _activeLines.Add(line);
            return impact;
        }

        private IEnumerator DestroyAfter(GameObject target, float lifetime)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, lifetime));
            if (target != null)
                Destroy(target);
        }

        private void ClearRuntimeObjects()
        {
            for (int i = _activeObjects.Count - 1; i >= 0; i--)
            {
                if (_activeObjects[i] != null)
                    Destroy(_activeObjects[i]);
            }

            _activeObjects.Clear();
            _activeLines.Clear();
        }

        private static Material GetLineMaterial()
        {
            if (_lineMaterial != null)
                return _lineMaterial;

            Shader shader =
                Shader.Find("Sprites/Default") ??
                Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Legacy Shaders/Particles/Alpha Blended");

            if (shader == null)
                return null;

            _lineMaterial = new Material(shader);
            _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            return _lineMaterial;
        }
    }
}
