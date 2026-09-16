using UnityEngine;
using UnityEngine.UIElements;

namespace Scripts.Skills.PassiveTree.UI
{
    public class PassiveTreeViewport
    {
        private readonly VisualElement _viewport;
        private readonly VisualElement _content;

        private bool _isDragging;
        private Vector2 _dragStartPos;
        private Vector2 _contentStartPos;
        private Vector2 _contentPos;

        private float _currentZoom = 1.0f;
        private const float MinZoom = 0.3f;
        private const float MaxZoom = 2.0f;
        private const float ZoomSpeed = 0.1f;

        public float CurrentZoom => _currentZoom;

        public PassiveTreeViewport(VisualElement viewport, VisualElement content)
        {
            _viewport = viewport;
            _content = content;

            _viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _viewport.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            _viewport.RegisterCallback<WheelEvent>(OnWheel);
        }

        public void Cleanup()
        {
            _viewport.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            _viewport.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            _viewport.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            _viewport.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            _viewport.UnregisterCallback<WheelEvent>(OnWheel);
        }

        public void CenterOnPosition(Vector2 position)
        {
            float viewWidth = _viewport.resolvedStyle.width;
            float viewHeight = _viewport.resolvedStyle.height;
            if (float.IsNaN(viewWidth)) viewWidth = 0;
            if (float.IsNaN(viewHeight)) viewHeight = 0;
            SetContentPos(new Vector2(
                -position.x * _currentZoom + viewWidth / 2f,
                -position.y * _currentZoom + viewHeight / 2f));
        }

        /// <summary>
        /// Подогнать вид так, чтобы заданный rect в координатах дерева был виден целиком (как Frame All в редакторе).
        /// Вызывать только когда у viewport уже есть реальные размеры (например, по GeometryChangedEvent).
        /// </summary>
        public void FrameContentRect(UnityEngine.Rect contentRect, float padding = 40f)
        {
            float vw = _viewport.resolvedStyle.width;
            float vh = _viewport.resolvedStyle.height;
            if (float.IsNaN(vw) || vw < 100f || float.IsNaN(vh) || vh < 100f) return;
            if (contentRect.width <= 0 || contentRect.height <= 0) return;

            float fitZoomX = (vw - padding * 2f) / contentRect.width;
            float fitZoomY = (vh - padding * 2f) / contentRect.height;
            _currentZoom = Mathf.Clamp(Mathf.Min(fitZoomX, fitZoomY), MinZoom, MaxZoom);

            Vector2 contentCenter = new Vector2(contentRect.x + contentRect.width * 0.5f, contentRect.y + contentRect.height * 0.5f);
            SetContentPos(new Vector2(
                vw * 0.5f - contentCenter.x * _currentZoom,
                vh * 0.5f - contentCenter.y * _currentZoom));
            _content.transform.scale = Vector3.one * _currentZoom;
        }

        private void OnWheel(WheelEvent evt)
        {
            float oldZoom = _currentZoom;
            _currentZoom = PassiveTreeViewportMath.StepZoom(oldZoom, evt.delta.y, ZoomSpeed, MinZoom, MaxZoom);
            if (Mathf.Approximately(oldZoom, _currentZoom))
            {
                evt.StopPropagation();
                return;
            }

            Vector2 mousePosInViewport = GetWheelPositionInViewport(evt);
            Vector2 newContainerPos = PassiveTreeViewportMath.ZoomToward(
                _contentPos,
                oldZoom,
                _currentZoom,
                mousePosInViewport);

            _content.transform.scale = Vector3.one * _currentZoom;
            SetContentPos(newContainerPos);
            ResyncDragAfterZoom(evt.mousePosition);

            evt.StopPropagation();
            evt.PreventDefault();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0 || evt.button == 2)
            {
                _isDragging = true;
                _dragStartPos = evt.position;
                _contentStartPos = _contentPos;
                _viewport.CapturePointer(evt.pointerId);
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging)
                return;

            SetContentPos(PassiveTreeViewportMath.Pan(_contentStartPos, _dragStartPos, evt.position));
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            EndDrag(evt.pointerId);
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            _isDragging = false;
        }

        private void EndDrag(int pointerId)
        {
            if (!_isDragging)
                return;

            _isDragging = false;
            if (_viewport.HasPointerCapture(pointerId))
                _viewport.ReleasePointer(pointerId);
        }

        private void ResyncDragAfterZoom(Vector2 currentPointerPos)
        {
            if (!_isDragging)
                return;

            _contentStartPos = _contentPos;
            _dragStartPos = currentPointerPos;
        }

        private void SetContentPos(Vector2 pos)
        {
            _contentPos = pos;
            _content.style.left = pos.x;
            _content.style.top = pos.y;
        }

        private Vector2 GetWheelPositionInViewport(WheelEvent evt)
        {
            Vector2 local = evt.localMousePosition;
            if (IsUsableViewportPosition(local))
                return local;

            Vector2 world = _viewport.WorldToLocal(evt.mousePosition);
            if (IsUsableViewportPosition(world))
                return world;

            return local;
        }

        private bool IsUsableViewportPosition(Vector2 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y))
                return false;

            float width = _viewport.resolvedStyle.width;
            float height = _viewport.resolvedStyle.height;
            if (width <= 0f || height <= 0f)
                return true;

            const float tolerance = 64f;
            return position.x >= -tolerance &&
                   position.y >= -tolerance &&
                   position.x <= width + tolerance &&
                   position.y <= height + tolerance;
        }
    }

    /// <summary>
    /// Pan/zoom math for the passive tree. Keep content position tracked in code;
    /// do not mix a zoomed position with a pan that still uses the pre-zoom origin.
    /// </summary>
    public static class PassiveTreeViewportMath
    {
        public static float StepZoom(float currentZoom, float wheelDeltaY, float zoomSpeed, float minZoom, float maxZoom)
        {
            float factor = -wheelDeltaY > 0f ? 1f + zoomSpeed : 1f - zoomSpeed;
            return Mathf.Clamp(currentZoom * factor, minZoom, maxZoom);
        }

        public static Vector2 ZoomToward(Vector2 contentPos, float oldZoom, float newZoom, Vector2 mousePosInViewport)
        {
            if (oldZoom <= 0f || Mathf.Approximately(oldZoom, newZoom))
                return contentPos;

            Vector2 mouseInContent = (mousePosInViewport - contentPos) / oldZoom;
            return mousePosInViewport - mouseInContent * newZoom;
        }

        public static Vector2 Pan(Vector2 contentStartPos, Vector2 dragStartPos, Vector2 currentPointerPos)
        {
            return contentStartPos + (currentPointerPos - dragStartPos);
        }
    }
}
