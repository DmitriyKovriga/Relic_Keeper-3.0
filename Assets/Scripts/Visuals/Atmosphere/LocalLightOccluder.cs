using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Scripts.Visuals.Atmosphere
{
    // Render geometry only. It never adds or changes gameplay colliders.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class LocalLightOccluder : MonoBehaviour
    {
        [SerializeField] private Vector3[] _outline = Array.Empty<Vector3>();
        private static readonly HashSet<LocalLightOccluder> Active = new HashSet<LocalLightOccluder>();
        private static readonly List<ShadowCaster2D> Hidden = new List<ShadowCaster2D>();
        private ShadowCaster2D _caster;
        public Vector3[] Outline => _outline;
        public void Configure(Vector3[] outline) { _outline = (Vector3[])outline.Clone(); }

        private void OnEnable()
        {
            if (Active.Count == 0)
            {
                RenderPipelineManager.beginCameraRendering += BeginCamera;
                RenderPipelineManager.endCameraRendering += EndCamera;
            }
            Active.Add(this);
        }
        private void OnDisable()
        {
            Active.Remove(this);
            if (Active.Count == 0)
            {
                RenderPipelineManager.beginCameraRendering -= BeginCamera;
                RenderPipelineManager.endCameraRendering -= EndCamera;
                Hidden.Clear();
            }
        }
        private static void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            Restore();
            var scene = camera.gameObject.scene;
#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView && Camera.main != null) scene = Camera.main.gameObject.scene;
#endif
            // URP's shadow-caster registry is global. Preview-room geometry must
            // not shadow a different preview, or the real game at the same XY.
            foreach (var blocker in Active)
            {
                if (blocker == null || blocker.gameObject.scene == scene) continue;
                if (blocker._caster == null) blocker._caster = blocker.GetComponent<ShadowCaster2D>();
                if (blocker._caster == null || !blocker._caster.enabled) continue;
                Hidden.Add(blocker._caster); blocker._caster.enabled = false;
            }
        }
        private static void EndCamera(ScriptableRenderContext context, Camera camera) => Restore();
        private static void Restore()
        {
            foreach (var caster in Hidden)
                if (caster != null && caster.gameObject.activeInHierarchy)
                { caster.enabled = true; caster.Update(); }
            Hidden.Clear();
        }
    }

    [Serializable]
    public sealed class LocalLightOccluderShape : ShadowShape2DProvider
    {
        public override bool IsShapeSource(Component source) => source is LocalLightOccluder;
        public override int Priority() => 100;
        public override void OnPersistantDataCreated(Component source, ShadowShape2D shape) => Build(source, shape);
        public override void Enabled(Component source, ShadowShape2D shape) => Build(source, shape);

        private static void Build(Component source, ShadowShape2D shape)
        {
            if (!(source is LocalLightOccluder blocker) || blocker.Outline.Length < 3) return;
            var indices = new int[blocker.Outline.Length * 2];
            for (int i = 0; i < blocker.Outline.Length; i++)
            { indices[i * 2] = i; indices[i * 2 + 1] = (i + 1) % blocker.Outline.Length; }
            using (var vertices = new NativeArray<Vector3>(blocker.Outline, Allocator.Temp))
            using (var edges = new NativeArray<int>(indices, Allocator.Temp))
            {
                // Interior geometry is essential: light must stop inside solid
                // ground as well as in the projected shadow behind it.
                shape.SetShape(vertices, edges, ShadowShape2D.OutlineTopology.Lines,
                    ShadowShape2D.WindingOrder.CounterClockwise, false, true);
            }
        }
    }
}
