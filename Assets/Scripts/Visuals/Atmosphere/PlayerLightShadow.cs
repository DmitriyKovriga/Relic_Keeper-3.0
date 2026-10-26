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
        private SpriteRenderer _groundProjection;
        private Vector3 _previewSurface;
        private bool _hasPreviewSurface;
        private readonly RaycastHit2D[] _lightHits = new RaycastHit2D[4];
        private static readonly HashSet<PlayerLightShadow> Active = new HashSet<PlayerLightShadow>();
        private static readonly Vector4[] Sources = new Vector4[8];
        private static readonly int BodyId = Shader.PropertyToID("_RKPlayerShadowBody");
        private static readonly int SelfId = Shader.PropertyToID("_RKPlayerShadowSelf");
        private static readonly int ShapeId = Shader.PropertyToID("_RKPlayerShadowShape");
        private static readonly int SourcesId = Shader.PropertyToID("_RKPlayerShadowSources");
        private static readonly int CountId = Shader.PropertyToID("_RKPlayerShadowSourceCount");

        public void ConfigureBody(Bounds worldBounds)
        {
            _center = transform.InverseTransformPoint(worldBounds.center);
            var scale = transform.lossyScale;
            _size = new Vector2(worldBounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                worldBounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)));
            _bodyConfigured = true;
        }
        public void ConfigurePreviewGround(float height, float minX, float maxX)
        { _previewSurface = new Vector3(height, minX, maxX); _hasPreviewSurface = true; }
        public SpriteRenderer GroundProjection => _groundProjection;
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
            _grounding = GetComponent<GroundingVisualController>();
            if (!_bodyConfigured && TryGetComponent<Collider2D>(out var body)) ConfigureBody(body.bounds);
            if (Active.Count == 0)
            {
                RenderPipelineManager.beginCameraRendering += BeginCamera;
                RenderPipelineManager.endCameraRendering += EndCamera;
            }
            Active.Add(this);
        }
        private void OnDisable()
        {
            if (_groundProjection != null)
            {
                _groundProjection.enabled = false;
                if (Application.isPlaying) Destroy(_groundProjection.gameObject); else DestroyImmediate(_groundProjection.gameObject);
                _groundProjection = null;
            }
            Active.Remove(this); Shader.SetGlobalInt(CountId, 0);
            if (Active.Count != 0) return;
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
        }
        private static void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            Shader.SetGlobalInt(CountId, 0);
            foreach (var actor in Active) if (actor != null && actor._groundProjection != null) actor._groundProjection.enabled = false;
            if (WorldColorGrading.BindingsSuspended || camera.cameraType != CameraType.Game ||
                !camera.TryGetComponent<AtmosphereController>(out var owner) || !owner.isActiveAndEnabled) return;
            var profile = owner.ActiveDepthMistProfile;
            if (profile == null || !profile.playerLightShadow) return;
            PlayerLightShadow player = null;
            foreach (var actor in Active)
                if (actor != null && actor.isActiveAndEnabled && actor.gameObject.scene == camera.gameObject.scene)
                { player = actor; break; }
            if (player == null) return;
            player.DrawGroundProjection(profile);
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
            }
            if (count == 0) return;
            Shader.SetGlobalVector(BodyId, body);
            Shader.SetGlobalVector(SelfId, original);
            Shader.SetGlobalVector(ShapeId, new Vector4(Mathf.Clamp01(profile.playerLightShadowStrength),
                Mathf.Clamp(profile.playerLightShadowReach, 0.1f, 30), Mathf.Clamp(profile.playerLightShadowSoftness, 0, 2), 0));
            Shader.SetGlobalVectorArray(SourcesId, Sources); Shader.SetGlobalInt(CountId, count);
        }
        private static void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            Shader.SetGlobalInt(CountId, 0);
            foreach (var actor in Active) if (actor != null && actor._groundProjection != null) actor._groundProjection.enabled = false;
        }

        private void DrawGroundProjection(AtmosphereProfileSO profile)
        {
            if (profile.playerGroundShadowOpacity <= 0 || profile.playerGroundShadowLength <= 0) return;
            Vector4 body = Body;
            Vector2 anchor, normal; float height;
            if (_grounding != null)
            {
                if (_grounding.ContactShadowRenderer == null || !_grounding.ContactShadowRenderer.enabled ||
                    !_grounding.TryGetLightShadowSurface(out anchor, out height, out normal)) return;
            }
            else
            {
                if (!_hasPreviewSurface || body.x < _previewSurface.y || body.x > _previewSurface.z) return;
                anchor = new Vector2(body.x, _previewSurface.x); normal = Vector2.up;
                height = Mathf.Max(0, body.y - body.w - anchor.y);
            }
            float airborne = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(height / 2.5f));
            if (airborne <= 0) return;
            Vector2 tangent = new Vector2(normal.y, -normal.x);
            LocalLight2DSource strongest = null; float weight = 0;
            foreach (var light in LocalLight2DSource.ActiveSources)
            {
                if (light == null || light.IsRoomFill || light.gameObject.scene != gameObject.scene) continue;
                float sample = light.ParticleIllumination(profile, new Vector3(body.x, body.y));
                if (sample <= weight) continue;
#if UNITY_EDITOR
                if (_grounding == null && PreviewLightVisibility != null && !PreviewLightVisibility(light.transform.position, new Vector2(body.x, body.y))) continue;
#endif
                if (_grounding != null)
                {
                    var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(_grounding.ShadowGroundMask);
                    if (Physics2D.Linecast(light.transform.position, new Vector2(body.x, body.y), filter, _lightHits) > 0) continue;
                }
                strongest = light; weight = sample;
            }
            if (strongest == null) return;
            float direction = Vector2.Dot(anchor - (Vector2)strongest.transform.position, tangent);
            float side = direction >= 0 ? 1 : -1;
            float sideFade = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Abs(direction) / 0.35f));
            float length = profile.playerGroundShadowLength * sideFade;
            float width = Mathf.Max(0.2f, body.z * 2) + length;
            Vector2 support = anchor;
            anchor += tangent * (side * length * 0.5f);
            if (_grounding != null)
            {
                float supportWidth = width + length;
                _grounding.ClipLightShadow(ref support, normal, ref supportWidth);
                float center = Vector2.Dot(anchor - support, tangent);
                float left = Mathf.Max(-supportWidth * 0.5f, center - width * 0.5f), right = Mathf.Min(supportWidth * 0.5f, center + width * 0.5f);
                width = Mathf.Max(0, right - left); anchor = support + tangent * ((left + right) * 0.5f);
            }
            else
            {
                float left = Mathf.Max(_previewSurface.y, anchor.x - width * 0.5f), right = Mathf.Min(_previewSurface.z, anchor.x + width * 0.5f);
                width = Mathf.Max(0, right - left); anchor.x = (left + right) * 0.5f;
            }
            if (width < 0.05f) return;
            if (_groundProjection == null)
            {
                var sprite = Resources.Load<Sprite>("VFX/Grounds/Shadow_Medium");
                var material = Resources.Load<Material>("VFX/Grounds/LightGroundShadow");
                if (sprite == null || material == null) return;
                var go = new GameObject("PlayerLightContactShadow") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                _groundProjection = go.AddComponent<SpriteRenderer>(); _groundProjection.sprite = sprite;
                _groundProjection.sharedMaterial = material;
                go.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Unaffected);
            }
            _groundProjection.sortingLayerName = "Default";
            _groundProjection.sortingOrder = _grounding != null ? _grounding.ContactShadowRenderer.sortingOrder + 1 :
                WorldRenderSorting.ResolveOrder(RenderDepthCategory.Environment, anchor.y, -1);
            _groundProjection.transform.position = anchor - normal * 0.015f;
            _groundProjection.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(-normal.x, normal.y) * Mathf.Rad2Deg);
            _groundProjection.transform.localScale = new Vector3(width / _groundProjection.sprite.bounds.size.x,
                profile.playerGroundShadowThickness / _groundProjection.sprite.bounds.size.y, 1);
            _groundProjection.color = new Color(1, 1, 1, Mathf.Clamp01(profile.playerGroundShadowOpacity * weight * airborne));
            _groundProjection.enabled = true;
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
        public static float Occlusion(Vector2 light, Vector2 receiver, Vector4 body, float reach, float softness = 0.25f, Vector4? actualBody = null)
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
            return edge * fade * nearLight;
        }
        private static float Smooth(float a, float b, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, value));
    }
}
