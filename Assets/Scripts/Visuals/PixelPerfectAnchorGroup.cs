using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Visuals
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PixelPerfectAnchorGroup : MonoBehaviour
    {
        private const float DefaultPixelsPerUnit = 24f;

        [Header("Pixel Grid")]
        [SerializeField, Min(1f)] private float _pixelsPerUnit = DefaultPixelsPerUnit;
        [SerializeField] private bool _snapEveryFrame;

        [Header("Anchors")]
        [SerializeField] private Transform[] _anchors = Array.Empty<Transform>();

        [ContextMenu("Snap Anchors Now")]
        public void SnapAnchorsNow()
        {
            if (_anchors == null || _anchors.Length == 0)
                return;

            float pixelSize = 1f / Mathf.Max(1f, _pixelsPerUnit);
            for (int i = 0; i < _anchors.Length; i++)
            {
                Transform anchor = _anchors[i];
                if (anchor == null)
                    continue;

                Vector3 localPosition = anchor.localPosition;
                localPosition.x = Snap(localPosition.x, pixelSize);
                localPosition.y = Snap(localPosition.y, pixelSize);
                anchor.localPosition = localPosition;
            }
        }

        [ContextMenu("Collect Common Anchors")]
        private void CollectCommonAnchors()
        {
            var anchors = new List<Transform>();
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child == transform)
                    continue;

                string name = child.name;
                if (name.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Pivot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Holder", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    anchors.Add(child);
                }
            }

            _anchors = anchors.ToArray();
            SnapAnchorsNow();
        }

        private void OnEnable()
        {
            SnapAnchorsNow();
        }

        private void LateUpdate()
        {
            if (_snapEveryFrame)
                SnapAnchorsNow();
        }

        private void OnValidate()
        {
            if (_pixelsPerUnit < 1f)
                _pixelsPerUnit = DefaultPixelsPerUnit;

            SnapAnchorsNow();
        }

        private static float Snap(float value, float pixelSize)
        {
            return Mathf.Round(value / pixelSize) * pixelSize;
        }
    }
}
