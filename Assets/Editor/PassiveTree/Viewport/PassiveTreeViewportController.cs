using UnityEngine;
using UnityEngine.UIElements;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Pan и zoom канваса. Хранит zoom и позицию content, преобразует координаты viewport ↔ content.
    /// Не подписывается на pan (вызов StartPan/UpdatePan/EndPan снаружи); подписывается на Wheel сам.
    /// </summary>
    public class PassiveTreeViewportController
    {
        public const float MinZoom = 0.2f;
        public const float MaxZoom = 2f;
        public const float ZoomSpeed = 0.1f;

        private readonly VisualElement _viewport;
        private readonly VisualElement _content;

        private float _zoom = 1f;
        private bool _isPanning;
        private Vector2 _panStartPos;
        private Vector2 _contentStartPos;

        public float Zoom => _zoom;

        public PassiveTreeViewportController(VisualElement viewport, VisualElement content)
        {
            _viewport = viewport;
            _content = content;
        }

        /// <summary>
        /// Подписать только zoom по колёсику. Pan вызывается снаружи (StartPan/UpdatePan/EndPan).
        /// </summary>
        public void RegisterWheelZoom()
        {
            _viewport.RegisterCallback<WheelEvent>(OnWheel);
        }

        public void UnregisterWheelZoom()
        {
            _viewport.UnregisterCallback<WheelEvent>(OnWheel);
        }

        /// <summary>
        /// Точка в координатах viewport → координаты content (мира дерева).
        /// </summary>
        public Vector2 ViewportToContentPosition(Vector2 viewportPos)
        {
            Vector2 contentPos = new Vector2(_content.resolvedStyle.left, _content.resolvedStyle.top);
            return (viewportPos - contentPos) / _zoom;
        }

        public void SetZoom(float zoom)
        {
            _zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            _content.transform.scale = Vector3.one * _zoom;
        }

        /// <summary>
        /// Начать pan (вызвать при PointerDown по фону). Захватывает pointer.
        /// </summary>
        public void StartPan(int pointerId, Vector2 position)
        {
            _isPanning = true;
            _panStartPos = position;
            _contentStartPos = new Vector2(_content.resolvedStyle.left, _content.resolvedStyle.top);
            _viewport.CapturePointer(pointerId);
        }

        public void UpdatePan(Vector2 position)
        {
            if (!_isPanning) return;
            Vector2 delta = position - _panStartPos;
            _content.style.left = _contentStartPos.x + delta.x;
            _content.style.top = _contentStartPos.y + delta.y;
        }

        public void EndPan(int pointerId)
        {
            if (_isPanning)
            {
                _isPanning = false;
                _viewport.ReleasePointer(pointerId);
            }
        }

        /// <summary>
        /// Сбросить pan без pointer (например при PointerLeave).
        /// </summary>
        public void CancelPan()
        {
            _isPanning = false;
        }

        public bool IsPanning => _isPanning;

        /// <summary>
        /// Смещение курсора в viewport (пиксели) → смещение в content (единицы дерева).
        /// </summary>
        public Vector2 ViewportDeltaToContentDelta(Vector2 viewportDelta)
        {
            return viewportDelta / _zoom;
        }

        private void OnWheel(WheelEvent evt)
        {
            float zoomDelta = -evt.delta.y > 0 ? 1 + ZoomSpeed : 1 - ZoomSpeed;
            float oldZoom = _zoom;
            _zoom = Mathf.Clamp(_zoom * zoomDelta, MinZoom, MaxZoom);
            if (Mathf.Approximately(oldZoom, _zoom)) return;

            Vector2 mousePosInViewport = evt.localMousePosition;
            Vector2 oldContainerPos = new Vector2(_content.resolvedStyle.left, _content.resolvedStyle.top);
            Vector2 mousePosInContainer = (mousePosInViewport - oldContainerPos) / oldZoom;

            _content.transform.scale = Vector3.one * _zoom;
            Vector2 newContainerPos = mousePosInViewport - (mousePosInContainer * _zoom);
            _content.style.left = newContainerPos.x;
            _content.style.top = newContainerPos.y;

            evt.StopPropagation();
        }
    }
}
