using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scripts.Visuals.Atmosphere
{
    // One stable body, evaluated inside each local point light's existing pass.
    // No sprite tracing, extra light, gameplay collider or terrain caster is added.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class PlayerLightShadow : MonoBehaviour
    {
#if UNITY_EDITOR
        public System.Func<Vector2, Vector2, bool> PreviewLightVisibility { get; set; }
#endif
        private Vector2 _center, _size = new Vector2(0.6f, 1.2f);
        private bool _bodyConfigured;
        private GroundingVisualController _grounding;
        private Vector3 _previewSurface;
        private bool _hasPreviewSurface;
        private readonly RaycastHit2D[] _lightHits = new RaycastHit2D[4];
        private MaterialPropertyBlock _bodyProperties;
        private SpriteRenderer _shadedBody;
        private float _previousBodyShade;
        private static readonly int CoverId = Shader.PropertyToID("_RKPlayerOverlapShade");
        private static readonly int CoverAmountId = Shader.PropertyToID("_RKPlayerOverlapAmount");
        private static readonly HashSet<PlayerLightShadow> Active = new HashSet<PlayerLightShadow>();
        private static readonly Vector4[] Sources = new Vector4[8];
        private static readonly Vector4[] GroundSources = new Vector4[8];
        private static readonly int BodyId = Shader.PropertyToID("_RKPlayerShadowBody");
        private static readonly int SelfId = Shader.PropertyToID("_RKPlayerShadowSelf");
        private static readonly int ShapeId = Shader.PropertyToID("_RKPlayerShadowShape");
        private static readonly int SourcesId = Shader.PropertyToID("_RKPlayerShadowSources");
        private static readonly int CountId = Shader.PropertyToID("_RKPlayerShadowSourceCount");
        private static readonly int GroundPlaneId = Shader.PropertyToID("_RKPlayerShadowGroundPlane");
        private static readonly int GroundSpanId = Shader.PropertyToID("_RKPlayerShadowGroundSpan");
        private static readonly int GroundSourcesId = Shader.PropertyToID("_RKPlayerShadowGroundSources");

        public void ConfigureBody(Bounds worldBounds)
        {
            _center = transform.InverseTransformPoint(worldBounds.center);
            var scale = transform.lossyScale;
            _size = new Vector2(worldBounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                worldBounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)));
            _bodyConfigured = true;
            BindBodyReceiver();
        }
        public void ConfigurePreviewGround(float height, float minX, float maxX)
        { _previewSurface = new Vector3(height, minX, maxX); _hasPreviewSurface = true; }
        // Kept for callers of the earlier decal implementation. Shadows now
        // remove source light directly and never create an overlay renderer.
        public SpriteRenderer GroundProjection => null;
        public Vector4 Body
        {
            get
            {
                Vector3 center = transform.TransformPoint(_center), scale = transform.lossyScale;
                return new Vector4(center.x, center.y, Mathf.Max(0.1f, _size.x * Mathf.Abs(scale.x) * 0.5f),
                    Mathf.Max(0.1f, _size.y * Mathf.Abs(scale.y) * 0.5f));
            }
        }
        private void OnEnable()
        {
            if (_bodyProperties == null) _bodyProperties = new MaterialPropertyBlock();
            _grounding = GetComponent<GroundingVisualController>();
            if (!_bodyConfigured && TryGetComponent<Collider2D>(out var body)) ConfigureBody(body.bounds);
            BindBodyReceiver();
            if (Active.Count == 0)
            {
                RenderPipelineManager.beginCameraRendering += BeginCamera;
                RenderPipelineManager.endCameraRendering += EndCamera;
            }
            Active.Add(this);
        }
        private void OnDisable()
        {
            RestoreBodyShade(); Shader.SetGlobalFloat(CoverAmountId, 0);
            Active.Remove(this); Shader.SetGlobalInt(CountId, 0);
            if (Active.Count != 0) return;
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
        }
        private static void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            Shader.SetGlobalInt(CountId, 0); Shader.SetGlobalFloat(CoverAmountId, 0);
            if (WorldColorGrading.BindingsSuspended || camera.cameraType != CameraType.Game ||
                !camera.TryGetComponent<AtmosphereController>(out var owner) || !owner.isActiveAndEnabled) return;
            var profile = owner.ActiveDepthMistProfile;
            if (profile == null || !profile.playerLightShadow) return;
            PlayerLightShadow player = null;
            foreach (var actor in Active)
                if (actor != null && actor.isActiveAndEnabled && actor.gameObject.scene == camera.gameObject.scene)
                { player = actor; break; }
            if (player == null) return;
            player.ApplyOverlapShade(profile);
            bool supported = player.PrepareGroundProjection(profile, out var anchor, out var normal, out float airborne);
            Vector4 original = player.Body, body = ShapeBody(original, profile.playerLightShadowWidth, profile.playerLightShadowHeight);
            int count = 0;
            foreach (var source in LocalLight2DSource.ActiveSources)
            {
                if (source == null || source.gameObject.scene != player.gameObject.scene || source.IsRoomFill ||
                    !source.isActiveAndEnabled || !source.Light.enabled || !source.CastsPlayerShadow(profile)) continue;
                Vector2 point = source.transform.position;
                float radius = source.ShadowRadius(profile);
                if (Vector2.Distance(point, new Vector2(body.x, body.y)) > radius + Mathf.Max(body.z, body.w)) continue;
                // The nearest eight sources keep the pilot bounded in large rooms.
                int slot = count;
                if (count == Sources.Length)
                {
                    slot = 0;
                    for (int i = 1; i < count; i++) if (Sources[i].w > Sources[slot].w) slot = i;
                    if (Vector2.Distance(point, new Vector2(body.x, body.y)) >= Sources[slot].w) continue;
                }
                else count++;
                Sources[slot] = new Vector4(point.x, point.y, radius, Vector2.Distance(point, new Vector2(body.x, body.y)));
                GroundSources[slot] = Vector4.zero;
                if (supported && player.VisibleInfluence(source, profile, original) > 0)
                {
                    Vector2 tangent = new Vector2(normal.y, -normal.x);
                    float distance = Vector2.Dot(anchor - point, tangent);
                    float sourceHeight = Vector2.Dot(point - anchor, normal);
                    float length = ProjectedGroundLength(distance, sourceHeight, original.w * 2,
                        profile.playerGroundShadowLength, profile.playerGroundShadowDistanceResponse, profile.playerGroundShadowGrowthDistance);
                    float crossing = GroundResponse(distance, original.z, 0, profile.playerLightCrossingFade).y;
                    GroundSources[slot] = new Vector4(length, profile.playerGroundShadowOpacity * airborne * crossing,
                        distance >= 0 ? 1 : -1, original.z * 0.65f);
                }
            }
            if (count == 0) return;
            Shader.SetGlobalVector(BodyId, body);
            Shader.SetGlobalVector(SelfId, original);
            Shader.SetGlobalVector(ShapeId, new Vector4(Mathf.Clamp01(profile.playerLightShadowStrength),
                Mathf.Clamp(profile.playerLightShadowReach, 0.1f, 30), Mathf.Clamp(profile.playerLightShadowSoftness, 0, 2), profile.playerLightCrossingFade));
            Shader.SetGlobalVectorArray(SourcesId, Sources); Shader.SetGlobalVectorArray(GroundSourcesId, GroundSources);
            Shader.SetGlobalInt(CountId, count);
        }
        private static void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            Shader.SetGlobalInt(CountId, 0); Shader.SetGlobalFloat(CoverAmountId, 0);
        }

        private void LateUpdate() => BindBodyReceiver();

        private void BindBodyReceiver()
        {
            if (_bodyProperties == null) return;
            var renderer = _grounding != null ? _grounding.LightShadowBodyRenderer : GetComponent<SpriteRenderer>();
            if (renderer == _shadedBody) return;
            RestoreBodyShade();
            // Copied materials can keep an old property table after a shader
            // reimport; shader metadata describes the currently compiled pass.
            if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.shader.FindPropertyIndex("_RKPlayerOverlapShade") < 0) return;
            renderer.GetPropertyBlock(_bodyProperties); _previousBodyShade = _bodyProperties.GetFloat(CoverId);
            _bodyProperties.SetFloat(CoverId, 1); renderer.SetPropertyBlock(_bodyProperties); _shadedBody = renderer;
        }

        private float VisibleInfluence(LocalLight2DSource light, AtmosphereProfileSO profile, Vector4 body)
        {
            if (light == null || light.IsRoomFill || light.gameObject.scene != gameObject.scene) return 0;
            var center = new Vector2(body.x, body.y);
            float sample = light.ParticleIllumination(profile, center);
            if (sample <= 0) return 0;
#if UNITY_EDITOR
            if (_grounding == null && PreviewLightVisibility != null && !PreviewLightVisibility(light.transform.position, center)) return 0;
#endif
            if (_grounding != null)
            {
                var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(_grounding.ShadowGroundMask);
                if (Physics2D.Linecast(light.transform.position, center, filter, _lightHits) > 0) return 0;
            }
            return sample;
        }

        private void ApplyOverlapShade(AtmosphereProfileSO profile)
        {
            if (profile.playerLightOverlapDarkness <= 0) return;
            BindBodyReceiver(); var renderer = _shadedBody;
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sharedMaterial == null ||
                renderer.sharedMaterial.shader.FindPropertyIndex("_RKPlayerOverlapShade") < 0) return;
            Vector4 body = Body; float cover = 0;
            foreach (var light in LocalLight2DSource.ActiveSources)
            {
                float influence = VisibleInfluence(light, profile, body);
                if (influence <= 0) continue;
                Vector2 delta = (Vector2)light.transform.position - new Vector2(body.x, body.y);
                float horizontal = 1 - Smooth(body.z * 0.35f, body.z + 0.1f, Mathf.Abs(delta.x));
                float vertical = 1 - Smooth(body.w * 0.75f, body.w + 0.15f, Mathf.Abs(delta.y));
                cover = Mathf.Max(cover, horizontal * vertical * influence);
            }
            if (cover <= 0) return;
            Shader.SetGlobalFloat(CoverAmountId, Mathf.Clamp01(profile.playerLightOverlapDarkness * cover));
        }

        private void RestoreBodyShade()
        {
            if (_shadedBody == null) return;
            _shadedBody.GetPropertyBlock(_bodyProperties); _bodyProperties.SetFloat(CoverId, _previousBodyShade);
            _shadedBody.SetPropertyBlock(_bodyProperties); _shadedBody = null;
        }

        // Lateral extent grows away from an elevated source. Visibility reaches
        // zero before the side flips; neither curve depends on frame frequency.
        // Evaluation is continuous and independent of frame rate/render frequency.
        public static Vector2 GroundResponse(float signedDistance, float bodyHalfWidth, float response, float fadeDistance)
        {
            float distance = Mathf.Abs(signedDistance);
            float core = Mathf.Max(0.06f, bodyHalfWidth * 0.6f);
            float visibility = Smooth(core, Mathf.Max(core + 0.05f, fadeDistance), distance);
            float scale = Mathf.Clamp(Mathf.Pow(distance / 1.25f, Mathf.Clamp(response, 0, 2)), 0, 4);
            return new Vector2(scale, visibility);
        }

        public static float ProjectedGroundLength(float distance, float sourceHeight, float bodyHeight, float maximumLength, float response, float growthDistance)
        {
            if (sourceHeight <= 0.05f || maximumLength <= 0) return 0;
            // A side-view torch can sit below the head. Project the lower body
            // instead of allowing the head-height ray to approach infinity.
            float casterHeight = Mathf.Min(bodyHeight * 0.55f, sourceHeight * 0.65f);
            float slope = casterHeight / Mathf.Max(0.05f, sourceHeight - casterHeight);
            float rayLength = Mathf.Abs(distance) * Mathf.Pow(slope, Mathf.Clamp(response, 0, 2));
            return maximumLength * (1 - Mathf.Exp(-rayLength / (maximumLength + Mathf.Max(0, growthDistance) * 0.5f)));
        }

        private bool PrepareGroundProjection(AtmosphereProfileSO profile, out Vector2 anchor, out Vector2 normal, out float airborne)
        {
            anchor = default; normal = Vector2.up; airborne = 0;
            if (profile.playerGroundShadowOpacity <= 0 || profile.playerGroundShadowLength <= 0) return false;
            Vector4 body = Body;
            float height;
            if (_grounding != null)
            {
                if (_grounding.ContactShadowRenderer == null || !_grounding.ContactShadowRenderer.enabled ||
                    !_grounding.TryGetLightShadowSurface(out anchor, out height, out normal)) return false;
            }
            else
            {
                if (!_hasPreviewSurface || body.x < _previewSurface.y || body.x > _previewSurface.z) return false;
                anchor = new Vector2(body.x, _previewSurface.x); normal = Vector2.up;
                height = Mathf.Max(0, body.y - body.w - anchor.y);
            }
            airborne = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(height / 2.5f));
            if (airborne <= 0) return false;
            Vector2 tangent = new Vector2(normal.y, -normal.x);
            Vector2 support = anchor; float width = (profile.playerGroundShadowLength + body.z) * 2;
            if (_grounding != null)
            {
                _grounding.ClipLightShadow(ref support, normal, ref width);
            }
            else
            {
                float left = Mathf.Max(_previewSurface.y, anchor.x - width * 0.5f), right = Mathf.Min(_previewSurface.z, anchor.x + width * 0.5f);
                width = Mathf.Max(0, right - left); support.x = (left + right) * 0.5f;
            }
            float center = Vector2.Dot(support - anchor, tangent);
            Shader.SetGlobalVector(GroundPlaneId, new Vector4(anchor.x, anchor.y, normal.x, normal.y));
            Shader.SetGlobalVector(GroundSpanId, new Vector4(center - width * 0.5f, center + width * 0.5f,
                profile.playerGroundShadowThickness, 0));
            return width >= 0.05f;
        }

        public static Vector4 ShapeBody(Vector4 original, float width, float height)
        {
            float feet = original.y - original.w;
            original.z *= Mathf.Clamp(width, 0.1f, 4);
            original.w *= Mathf.Clamp(height, 0.1f, 3);
            original.y = feet + original.w;
            return original;
        }

        // Same soft ellipse/segment test as PlayerLightShadow.hlsl. Used by the
        // scene study to choose useful receiver points, and by geometry checks.
        public static float Occlusion(Vector2 light, Vector2 receiver, Vector4 body, float reach, float softness = 0.25f, Vector4? actualBody = null, float crossingFade = 0.65f)
        {
            Vector2 radii = new Vector2(body.z, body.w), center = new Vector2(body.x, body.y);
            Vector2 a = (light - center) / radii, b = (receiver - center) / radii;
            var original = actualBody ?? body;
            Vector2 originalCenter = new Vector2(original.x, original.y), originalRadii = new Vector2(original.z, original.w);
            Vector2 self = (receiver - originalCenter) / originalRadii, sourceToSelf = (light - originalCenter) / originalRadii;
            if (sourceToSelf.magnitude <= 1.15f || Mathf.Abs(self.x) <= 1.05f && self.y > -0.95f && self.y <= 1.05f) return 0;
            Vector2 ray = b - a;
            float t = -Vector2.Dot(a, ray) / Mathf.Max(0.0001f, ray.sqrMagnitude);
            if (t <= 0 || t >= 1) return 0;
            float distance = (a + ray * t).magnitude;
            float soft = Mathf.Clamp(softness, 0, 2);
            float edge = 1 - Smooth(Mathf.Max(0.05f, 1 - soft * 1.2f), 1 + Mathf.Max(0.001f, soft), distance);
            float fade = 1 - Smooth(reach * 0.45f, reach, Vector2.Distance(receiver, center));
            float nearLight = Smooth(1.15f, 2, sourceToSelf.magnitude);
            float crossing = GroundResponse(light.x - original.x, original.z, 0, crossingFade).y;
            return edge * fade * nearLight * crossing;
        }
        private static float Smooth(float a, float b, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, value));
    }
}
