using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scripts.UI
{
    /// <summary>One bounded, non-interactive confirmation per open UI. Never moves the target.</summary>
    public sealed class UiTransactionFeedback : IDisposable
    {
        private readonly VisualElement _owner;
        private VisualElement _overlay, _target;
        private IVisualElementScheduledItem _ticker;
        private float _started, _lastRejection = float.NegativeInfinity;
        private bool _accepted;

        public UiTransactionFeedback(VisualElement owner)
        {
            _owner = owner;
            _owner.RegisterCallback<DetachFromPanelEvent>(OnDetached);
        }

        public void Show(VisualElement target, bool accepted)
        {
            if (target == null || target.panel == null || _owner.panel != target.panel) return;
            if (!accepted && !_accepted && _target == target && Time.unscaledTime - _lastRejection < .2f) return;
            Clear();
            if (!accepted) _lastRejection = Time.unscaledTime;
            _target = target; _accepted = accepted; _started = Time.unscaledTime;
            Color color = accepted ? new Color(1f, .83f, .4f) : new Color(1f, .38f, .32f);
            _overlay = new VisualElement { name = "TransactionFeedback", pickingMode = PickingMode.Ignore };
            _overlay.style.position = Position.Absolute;
            _overlay.style.borderTopWidth = _overlay.style.borderBottomWidth = 1;
            _overlay.style.borderLeftWidth = _overlay.style.borderRightWidth = 1;
            _overlay.style.borderTopColor = _overlay.style.borderBottomColor = color;
            _overlay.style.borderLeftColor = _overlay.style.borderRightColor = color;
            _overlay.style.backgroundColor = new Color(color.r, color.g, color.b, .12f);
            var mark = new Label(accepted ? "+" : "!") { pickingMode = PickingMode.Ignore };
            mark.style.position = Position.Absolute; mark.style.right = 1; mark.style.top = 0;
            mark.style.fontSize = 8; mark.style.color = color;
            mark.style.unityFontStyleAndWeight = FontStyle.Bold;
            mark.style.paddingLeft = mark.style.paddingRight = 0;
            mark.style.paddingTop = mark.style.paddingBottom = 0;
            _overlay.Add(mark); _owner.Add(_overlay);
            Tick(); _ticker = _owner.schedule.Execute(Tick).Every(16);
        }

        private void Tick()
        {
            float age = Time.unscaledTime - _started;
            float duration = _accepted ? .6f : .45f;
            if (_target == null || _target.panel != _owner.panel || _target.panel == null ||
                _target.resolvedStyle.display == DisplayStyle.None || age >= duration) { Clear(); return; }
            Rect bounds = _target.worldBound;
            Vector2 min = _owner.WorldToLocal(bounds.min), max = _owner.WorldToLocal(bounds.max);
            _overlay.style.left = Mathf.Round(min.x); _overlay.style.top = Mathf.Round(min.y);
            _overlay.style.width = Mathf.Max(1, Mathf.Round(max.x - min.x));
            _overlay.style.height = Mathf.Max(1, Mathf.Round(max.y - min.y));
            _overlay.style.opacity = _accepted ? 1f - age / duration :
                (age < .12f || age >= .22f ? 1f - age / duration : .15f);
            _overlay.BringToFront();
        }

        public void Clear()
        {
            _ticker?.Pause(); _ticker = null;
            _overlay?.RemoveFromHierarchy(); _overlay = null; _target = null;
        }

        private void OnDetached(DetachFromPanelEvent evt) { if (evt.target == _owner) Clear(); }
        public void Dispose() { Clear(); _owner.UnregisterCallback<DetachFromPanelEvent>(OnDetached); }
    }
}
