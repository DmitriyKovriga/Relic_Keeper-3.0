using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Scripts.Visuals
{
    /// <summary>Moves body and held-weapon presentation together for a camera render only.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRenderAnchor : MonoBehaviour
    {
        private Transform _body;
        private Transform _weapons;
        private Vector3 _bodyLocalPosition;
        private Vector3 _weaponsLocalPosition;
        private Camera _renderingCamera;
        private bool _applied;

        public void Initialize(Transform body, Transform weapons)
        {
            Restore();
            _body = body;
            _weapons = weapons;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            Restore();
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera) => BeginRender(camera);
        private void EndCamera(ScriptableRenderContext context, Camera camera) => EndRender(camera);

        public void BeginRender(Camera camera)
        {
            if (!isActiveAndEnabled || camera == null || !camera.orthographic ||
                !camera.TryGetComponent<PixelPerfectCamera>(out var pixelCamera) || !pixelCamera.isActiveAndEnabled)
                return;

            Restore();
            // One rounding decision for the whole character. Rounding each sprite's
            // fractional socket offset independently causes relative one-pixel wobble.
            Vector3 offset = pixelCamera.RoundToPixel(transform.position) - transform.position;
            offset.z = 0;
            _renderingCamera = camera;
            _applied = true;
            if (_body != null && _body != transform)
            {
                _bodyLocalPosition = _body.localPosition;
                _body.position += offset;
            }
            if (_weapons != null && _weapons != transform && _weapons != _body)
            {
                _weaponsLocalPosition = _weapons.localPosition;
                _weapons.position += offset;
            }
        }

        public void EndRender(Camera camera)
        {
            if (camera == _renderingCamera) Restore();
        }

        public void Restore()
        {
            if (!_applied) return;
            if (_body != null && _body != transform) _body.localPosition = _bodyLocalPosition;
            if (_weapons != null && _weapons != transform && _weapons != _body) _weapons.localPosition = _weaponsLocalPosition;
            _renderingCamera = null;
            _applied = false;
        }
    }
}
