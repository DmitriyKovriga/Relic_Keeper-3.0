using System;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Anchor + two Bezier whiskers for the selected free connection.
    /// Handle modifiers follow Illustrator Direct Selection: paired opposite handles,
    /// Alt to break a pair, Shift to snap 45°, Ctrl/Cmd to force symmetric handles.
    /// </summary>
    public sealed class PassiveBezierHandleOverlay : VisualElement
    {
        private const float HandleSize = 10f;
        private const float AnchorSize = 12f;
        private const float ClickSlop = 4f;
        private static readonly Color WhiskerLineColor = new Color(0.98f, 0.86f, 0.42f, 0.85f);
        private static readonly Color HandleFill = new Color(0.12f, 0.11f, 0.08f, 0.95f);
        private static readonly Color HandleStroke = new Color(0.98f, 0.86f, 0.42f, 1f);
        private static readonly Color AnchorFill = new Color(0.98f, 0.82f, 0.28f, 0.95f);

        public enum HandleKind
        {
            None,
            Anchor,
            In,
            Out
        }

        private readonly VisualElement _inHandle;
        private readonly VisualElement _outHandle;
        private readonly VisualElement _anchorHandle;
        private Vector2 _localC1;
        private Vector2 _localAnchor;
        private Vector2 _localC2;
        private HandleKind _dragKind;
        private Vector2 _posA;
        private Vector2 _posB;
        private Vector2 _startIn;
        private Vector2 _startOut;
        private Vector2 _startInWorld;
        private Vector2 _startOutWorld;
        private Vector2 _pointerDownPanel;
        private bool _wasSmoothAtDragStart;
        private bool _dragMoved;

        public PassiveSkillTreeSO Tree { get; private set; }
        public PassiveBezierConnection Connection { get; private set; }
        public event Action Changed;

        public PassiveBezierHandleOverlay()
        {
            name = "BezierHandleOverlay";
            style.position = Position.Absolute;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;

            _inHandle = CreateHandle("BezierInHandle", HandleSize, HandleFill, true);
            _outHandle = CreateHandle("BezierOutHandle", HandleSize, HandleFill, true);
            _anchorHandle = CreateHandle("BezierAnchorHandle", AnchorSize, AnchorFill, false);
            Add(_inHandle);
            Add(_outHandle);
            Add(_anchorHandle);

            HookHandle(_inHandle, HandleKind.In);
            HookHandle(_outHandle, HandleKind.Out);
            HookHandle(_anchorHandle, HandleKind.Anchor);
        }

        public void Bind(PassiveSkillTreeSO tree, PassiveBezierConnection connection)
        {
            Tree = tree;
            Connection = connection;
            RefreshPositions();
        }

        public bool IsHandle(IEventHandler target)
        {
            return target == _inHandle || target == _outHandle || target == _anchorHandle;
        }

        public void RefreshPositions()
        {
            if (Tree == null || Connection == null)
                return;

            var nodeA = Tree.GetNode(Connection.NodeIdA);
            var nodeB = Tree.GetNode(Connection.NodeIdB);
            if (nodeA == null || nodeB == null)
                return;

            _posA = nodeA.GetWorldPosition(Tree);
            _posB = nodeB.GetWorldPosition(Tree);
            Vector2 anchor = Connection.GetAnchor(_posA, _posB);
            Vector2 c1 = anchor + Connection.InHandleOffset;
            Vector2 c2 = anchor + Connection.OutHandleOffset;

            const float padding = 20f;
            Rect bounds = PassiveBezierMath.Bounds(c1, anchor, c2, anchor, padding);
            style.left = bounds.xMin;
            style.top = bounds.yMin;
            style.width = Mathf.Max(1f, bounds.width);
            style.height = Mathf.Max(1f, bounds.height);

            Vector2 origin = new Vector2(bounds.xMin, bounds.yMin);
            _localC1 = c1 - origin;
            _localAnchor = anchor - origin;
            _localC2 = c2 - origin;

            PlaceHandle(_inHandle, _localC1, HandleSize);
            PlaceHandle(_outHandle, _localC2, HandleSize);
            PlaceHandle(_anchorHandle, _localAnchor, AnchorSize);
            MarkDirtyRepaint();
        }

        private void OnHandlePointerDown(HandleKind kind, PointerDownEvent evt)
        {
            if (evt.button != 0 || Connection == null || Tree == null)
                return;

            _dragKind = kind;
            _dragMoved = false;
            _pointerDownPanel = (Vector2)evt.position;
            _startIn = Connection.InHandleOffset;
            _startOut = Connection.OutHandleOffset;
            Vector2 anchor = Connection.GetAnchor(_posA, _posB);
            _startInWorld = anchor + _startIn;
            _startOutWorld = anchor + _startOut;
            _wasSmoothAtDragStart = PassiveBezierMath.AreSmoothOpposite(_startIn, _startOut);
            (evt.currentTarget as VisualElement)?.CapturePointer(evt.pointerId);
            UnityEditor.Undo.RecordObject(Tree, "Edit Bezier Connection");
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            var handle = evt.currentTarget as VisualElement;
            if (_dragKind == HandleKind.None || Connection == null || handle == null || !handle.HasPointerCapture(evt.pointerId))
                return;

            if (!_dragMoved && Vector2.Distance((Vector2)evt.position, _pointerDownPanel) >= ClickSlop)
                _dragMoved = true;

            if (!_dragMoved)
                return;

            Vector2 content = parent != null
                ? (Vector2)parent.WorldToLocal(evt.position)
                : (Vector2)this.WorldToLocal(evt.position) + new Vector2(resolvedStyle.left, resolvedStyle.top);
            ApplyDrag(content, evt.altKey, evt.shiftKey, evt.ctrlKey || evt.commandKey);
            RefreshPositions();
            Changed?.Invoke();
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            var handle = evt.currentTarget as VisualElement;
            if (_dragKind == HandleKind.None)
                return;

            if (!_dragMoved && evt.altKey && (_dragKind == HandleKind.In || _dragKind == HandleKind.Out))
                ConvertClickedHandleToSmooth();

            _dragKind = HandleKind.None;
            handle?.ReleasePointer(evt.pointerId);
            PassiveTreeAssetPersistence.SetDirty(Tree);
            Changed?.Invoke();
            evt.StopPropagation();
        }

        private void ApplyDrag(Vector2 content, bool alt, bool shift, bool ctrl)
        {
            Vector2 liveAnchor = Connection.GetAnchor(_posA, _posB);

            if (_dragKind == HandleKind.Anchor)
            {
                float percent = PassiveBezierMath.PercentAlongSegment(_posA, _posB, content);
                if (shift)
                    percent = PassiveBezierMath.SnapPercent(percent);

                Connection.AnchorPercent = percent;
                if (alt)
                {
                    Vector2 newAnchor = Connection.GetAnchor(_posA, _posB);
                    Connection.InHandleOffset = _startInWorld - newAnchor;
                    Connection.OutHandleOffset = _startOutWorld - newAnchor;
                }

                return;
            }

            Vector2 offset = content - liveAnchor;
            if (shift)
                offset = PassiveBezierMath.ConstrainTo45Degrees(offset);

            bool editingIn = _dragKind == HandleKind.In;
            if (alt && Connection.MirrorHandles)
                Connection.MirrorHandles = false;

            if (ctrl || (Connection.MirrorHandles && !alt))
            {
                Connection.InHandleOffset = editingIn ? offset : PassiveBezierMath.MirrorHandle(offset);
                Connection.OutHandleOffset = editingIn ? PassiveBezierMath.MirrorHandle(offset) : offset;
                return;
            }

            if (editingIn)
                Connection.InHandleOffset = offset;
            else
                Connection.OutHandleOffset = offset;

            bool keepPaired = !alt && _wasSmoothAtDragStart;
            if (!keepPaired)
                return;

            if (editingIn)
                Connection.OutHandleOffset = PassiveBezierMath.AlignOppositeHandle(offset, _startOut.magnitude);
            else
                Connection.InHandleOffset = PassiveBezierMath.AlignOppositeHandle(offset, _startIn.magnitude);
        }

        private void ConvertClickedHandleToSmooth()
        {
            if (Connection == null)
                return;

            if (_dragKind == HandleKind.In)
                Connection.OutHandleOffset = PassiveBezierMath.AlignOppositeHandle(Connection.InHandleOffset, Connection.OutHandleOffset.magnitude);
            else
                Connection.InHandleOffset = PassiveBezierMath.AlignOppositeHandle(Connection.OutHandleOffset, Connection.InHandleOffset.magnitude);
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            painter.lineWidth = 1.5f;
            painter.strokeColor = WhiskerLineColor;
            painter.BeginPath();
            painter.MoveTo(_localC1);
            painter.LineTo(_localAnchor);
            painter.MoveTo(_localC2);
            painter.LineTo(_localAnchor);
            painter.Stroke();
        }

        private void HookHandle(VisualElement handle, HandleKind kind)
        {
            handle.RegisterCallback<PointerDownEvent>(evt => OnHandlePointerDown(kind, evt));
            handle.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            handle.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private static void PlaceHandle(VisualElement handle, Vector2 localCenter, float size)
        {
            handle.style.left = localCenter.x - (size * 0.5f);
            handle.style.top = localCenter.y - (size * 0.5f);
        }

        private static VisualElement CreateHandle(string name, float size, Color fill, bool round)
        {
            var handle = new VisualElement { name = name, pickingMode = PickingMode.Position };
            handle.style.position = Position.Absolute;
            handle.style.width = size;
            handle.style.height = size;
            handle.style.backgroundColor = fill;
            handle.style.borderTopWidth = handle.style.borderBottomWidth = handle.style.borderLeftWidth = handle.style.borderRightWidth = 1.5f;
            handle.style.borderTopColor = handle.style.borderBottomColor = handle.style.borderLeftColor = handle.style.borderRightColor = HandleStroke;
            if (round)
            {
                handle.style.borderTopLeftRadius = handle.style.borderTopRightRadius =
                    handle.style.borderBottomLeftRadius = handle.style.borderBottomRightRadius = size * 0.5f;
            }
            else
            {
                handle.style.borderTopLeftRadius = handle.style.borderTopRightRadius =
                    handle.style.borderBottomLeftRadius = handle.style.borderBottomRightRadius = 2f;
            }

            return handle;
        }
    }
}
