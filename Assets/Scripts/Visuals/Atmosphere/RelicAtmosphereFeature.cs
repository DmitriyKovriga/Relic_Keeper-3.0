using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Scripts.Visuals.Atmosphere
{
    [DisallowMultipleRendererFeature("Relic Atmosphere")]
    public sealed class RelicAtmosphereFeature : ScriptableRendererFeature
    {
        [SerializeField, Tooltip("Serialized reference prevents build-time shader stripping.")]
        private Shader _shader;
        private Material _material;
        private AtmospherePass _pass;

        public void SetShader(Shader shader) { _shader = shader; Create(); }

        public override void Create()
        {
            CoreUtils.Destroy(_material);
            _material = _shader != null ? CoreUtils.CreateEngineMaterial(_shader) : null;
            _pass = new AtmospherePass { Material = _material, renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (_material == null || camera.cameraType != CameraType.Game || !camera.orthographic ||
                renderingData.cameraData.renderType != CameraRenderType.Base ||
                !camera.TryGetComponent<AtmosphereController>(out var controller) || !controller.isActiveAndEnabled ||
                controller.Profile == null || !controller.Profile.HasVisibleEffect ||
                Mathf.Abs(camera.transform.forward.z) < 0.0001f) return;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(_material);

        private sealed class AtmospherePass : ScriptableRenderPass
        {
            private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
            private static readonly int InverseVP = Shader.PropertyToID("_AtmosphereInverseVP");
            private static readonly int Reveal = Shader.PropertyToID("_AtmosphereReveal");
            private static readonly int World = Shader.PropertyToID("_AtmosphereWorld");
            private static readonly int Fog = Shader.PropertyToID("_AtmosphereFog");
            private static readonly int Noise = Shader.PropertyToID("_AtmosphereNoise");
            private static readonly int Grid = Shader.PropertyToID("_AtmosphereGrid");
            private static readonly int Shape = Shader.PropertyToID("_AtmosphereShape");

            private sealed class PassData
            {
                public Material material;
                public MaterialPropertyBlock properties;
                public Matrix4x4 inverseVP;
                public Vector4 reveal, world, fog, noise, grid, shape;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var resources = frameData.Get<UniversalResourceData>();
                var controller = cameraData.camera.GetComponent<AtmosphereController>();
                var profile = controller.Profile;
                // The feature owns one material; snapshot all camera values in pass data, then bind at execution.
                if (Material == null) return;
                using var builder = renderGraph.AddRasterRenderPass<PassData>("Relic Atmosphere", out var data);
                data.material = Material;
                data.properties = _properties;
                bool renderIntoTexture = !resources.isActiveTargetBackBuffer || cameraData.targetTexture != null;
                data.inverseVP = (GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), renderIntoTexture) * cameraData.GetViewMatrix()).inverse;
                data.reveal = controller.RevealParameters;
                data.world = new Vector4(Mathf.Clamp01(profile.darkness), Mathf.Clamp01(profile.fogOpacity),
                    Mathf.Clamp01(profile.vignette), Mathf.Clamp01(profile.intensity));
                data.fog = QualitySettings.activeColorSpace == ColorSpace.Linear ? profile.fogTint.linear : profile.fogTint;
                float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
                float pulse = 1f + Mathf.Clamp(profile.pulseAmount, 0f, 0.5f) *
                    Mathf.Sin(Mathf.Floor(time * 12f) / 12f * Mathf.Max(0f, profile.pulseFrequency) * Mathf.PI * 2f);
                data.noise = new Vector4(profile.driftSpeed.x * time, profile.driftSpeed.y * time,
                    Mathf.Max(0.001f, profile.noiseScale), Mathf.Clamp01(profile.noiseAmount));
                data.grid = new Vector4(Mathf.Max(1, profile.referenceResolution.x), Mathf.Max(1, profile.referenceResolution.y),
                    Mathf.Max(1f, profile.pixelsPerUnit), Mathf.Clamp01(profile.dither));
                data.shape = new Vector4(Mathf.Max(0.001f, profile.revealSoftness), controller.WorldPlaneZ, (float)profile.quality, pulse);
                // Fixed-function blending reads the destination. No source sampling, blit or color copy.
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                {
                    // Draw commands snapshot MPB values, so later camera passes cannot overwrite them.
                    var properties = pass.properties;
                    properties.SetMatrix(InverseVP, pass.inverseVP);
                    properties.SetVector(Reveal, pass.reveal);
                    properties.SetVector(World, pass.world);
                    properties.SetVector(Fog, pass.fog);
                    properties.SetVector(Noise, pass.noise);
                    properties.SetVector(Grid, pass.grid);
                    properties.SetVector(Shape, pass.shape);
                    context.cmd.DrawProcedural(Matrix4x4.identity, pass.material, 0, MeshTopology.Triangles, 3, 1, properties);
                });
            }

            // Set by the owning feature; not global state and not shared between renderer assets.
            internal Material Material;
        }
    }
}
