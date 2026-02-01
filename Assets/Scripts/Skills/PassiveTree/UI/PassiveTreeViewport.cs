using UnityEngine;
using UnityEngine.UIElements;

namespace Scripts.Skills.PassiveTree.UI
{
    public class PassiveTreeViewport
    {
        private readonly VisualElement _viewport;
        private readonly VisualElement _content; // То, что двигаем (TreeContainer)

        private bool _isDragging;
        private Vector2 _dragStartPos;
        private Vector2 _contentStartPos;
        
        private float _currentZoom = 1.0f;
        private const float MinZoom = 0.3f;
        private const float MaxZoom = 2.0f;
        private const float ZoomSpeed = 0.1f;

        public float CurrentZoom => _currentZoom;

        public PassiveTreeViewport(VisualElement viewport, VisualElement content)
        {
            _viewport = viewport;
            _content = content;

            // Регистрация событий
            _viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _viewport.RegisterCallback<PointerLeaveEvent>(OnPointerUp);
            _viewport.RegisterCallback<WheelEvent>(OnWheel);
        }

        public void Cleanup()
        {
            _viewport.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            _viewport.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            _viewport.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            _viewport.UnregisterCallback<PointerLeaveEvent>(OnPointerUp);
            _viewport.UnregisterCallback<WheelEvent>(OnWheel);
        }

        public void CenterOnPosition(Vector2 position)
        {
            float viewWidth = _viewport.resolvedStyle.width;
            float viewHeight = _viewport.resolvedStyle.height;
            
            // Если стиль еще не разрешен (первый кадр), берем fallback или 0
            if (float.IsNaN(viewWidth)) viewWidth = 0;
            if (float.IsNaN(viewHeight)) viewHeight = 0;

            _content.style.left = -position.x * _currentZoom + viewWidth / 2f;
            _content.style.top = -position.y * _currentZoom + viewHeight / 2f;
        }

        private void OnWheel(WheelEvent evt)
        {
            float zoomDelta = -evt.delta.y > 0 ? 1 + ZoomSpeed : 1 - ZoomSpeed;
            float oldZoom = _currentZoom;
            _currentZoom = Mathf.Clamp(_currentZoom * zoomDelta, MinZoom, MaxZoom);

            if (Mathf.Approximately(oldZoom, _currentZoom)) return;

            // Математика зума к курсору
            Vector2 mousePosInViewport = evt.localMousePosition;
            Vector2 oldContainerPos = new Vector2(_content.resolvedStyle.left, _content.resolvedStyle.top);
            Vector2 mousePosInContainer = (mousePosInViewport - oldContainerPos) / oldZoom;

            _content.transform.scale = Vector3.one * _currentZoom;
            
            Vector2 newContainerPos = mousePosInViewport - (mousePosInContainer * _currentZoom);
            _content.style.left = newContainerPos.x;
            _content.style.top = newContainerPos.y;

            evt.StopPropagation();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0 || evt.button == 2)
            {
                _isDragging = true;
                _dragStartPos = evt.position;
                _contentStartPos = new Vector2(_content.resolvedStyle.left, _content.resolvedStyle.top);
                _viewport.CapturePointer(evt.pointerId);
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_isDragging)
            {
                Vector2 delta = (Vector2)evt.position - _dragStartPos;
                _content.style.left = _contentStartPos.x + delta.x;
                _content.style.top = _contentStartPos.y + delta.y;
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_isDragging)
            {
                _isDragging = false;
                _viewport.ReleasePointer(evt.pointerId);
            }
        }
        
        private void OnPointerUp(PointerLeaveEvent evt)
        {
             if (_isDragging)
             {
                 _isDragging = false;
                 _viewport.ReleasePointer(evt.pointerId);
             }
        }
    }
}