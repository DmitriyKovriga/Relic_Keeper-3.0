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
        private Quaternion _bodyLocalRotation;
        private Quaternion _weaponsLocalRotation;
        private Camera _renderingCamera;
        private bool _applied;
        public Vector3 WeaponPresentationOffset { get; set; }
        public float PresentationLeanDegrees { get; set; }

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
            // A facing flip mirrors the local lean. Rotate body, hand offset and held weapon
            // around the same physical centre, only for this render.
            Quaternion lean = Quaternion.AngleAxis(PresentationLeanDegrees * Mathf.Sign(transform.lossyScale.x), transform.forward);
            Vector3 centre = transform.position;
            _renderingCamera = camera;
            _applied = true;
            if (_body != null && _body != transform)
            {
                _bodyLocalPosition = _body.localPosition;
                _bodyLocalRotation = _body.localRotation;
                _body.position = centre + lean * (_body.position - centre) + offset;
                _body.rotation = lean * _body.rotation;
            }
            if (_weapons != null && _weapons != transform && _weapons != _body)
            {
                _weaponsLocalPosition = _weapons.localPosition;
                _weaponsLocalRotation = _weapons.localRotation;
                _weapons.position = centre + lean * (_weapons.position - centre + transform.TransformVector(WeaponPresentationOffset)) + offset;
                _weapons.rotation = lean * _weapons.rotation;
            }
        }

        public void EndRender(Camera camera)
        {
            if (camera == _renderingCamera) Restore();
        }

        public void Restore()
        {
            if (!_applied) return;
            if (_body != null && _body != transform)
            {
                _body.localPosition = _bodyLocalPosition;
                _body.localRotation = _bodyLocalRotation;
            }
            if (_weapons != null && _weapons != transform && _weapons != _body)
            {
                _weapons.localPosition = _weaponsLocalPosition;
                _weapons.localRotation = _weaponsLocalRotation;
            }
            _renderingCamera = null;
            _applied = false;
        }
    }
}
