using System;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Визуализация кластера (орбит) в редакторе дерева пассивок.
    /// Отрисовывает окружности орбит, центральный маркер, дуги.
    /// </summary>
    public class PassiveTreeClusterView : VisualElement
    {
        public PassiveClusterDefinition Data { get; private set; }
        private PassiveSkillTreeSO _tree;
        
        public event Action<PointerDownEvent> OnPointerDown;
        public event Action<ContextualMenuPopulateEvent> OnContextMenu;

        /// <summary> Маркер центра кластера — добавляется в отдельный слой поверх нодов, чтобы кликался. </summary>
        public VisualElement CenterMarker => _centerMarker;

        private VisualElement _centerMarker;
        private bool _isSelected;

        public PassiveTreeClusterView(PassiveClusterDefinition data, PassiveSkillTreeSO tree, VisualElement markerParent)
        {
            Data = data;
            _tree = tree;

            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            pickingMode = PickingMode.Ignore;

            name = $"Cluster_{data.ID}";

            generateVisualContent += OnGenerateVisualContent;

            _centerMarker = CreateCenterMarker();
            if (markerParent != null)
                markerParent.Add(_centerMarker);
        }

        private VisualElement CreateCenterMarker()
        {
            var marker = new VisualElement
            {
                name = "ClusterCenter",
                pickingMode = PickingMode.Position
            };

            float markerSize = 24f;
            float radius = markerSize / 2f;
            marker.style.position = Position.Absolute;
            marker.style.width = markerSize;
            marker.style.height = markerSize;
            marker.style.left = Data.Center.x - radius;
            marker.style.top = Data.Center.y - radius;

            marker.style.borderTopLeftRadius = radius;
            marker.style.borderTopRightRadius = radius;
            marker.style.borderBottomLeftRadius = radius;
            marker.style.borderBottomRightRadius = radius;
            marker.style.backgroundColor = Data.EditorColor;
            marker.style.borderTopWidth = marker.style.borderBottomWidth =
                marker.style.borderLeftWidth = marker.style.borderRightWidth = 2;
            marker.style.borderTopColor = marker.style.borderBottomColor =
                marker.style.borderLeftColor = marker.style.borderRightColor = Color.white;

            marker.RegisterCallback<PointerDownEvent>(evt => OnPointerDown?.Invoke(evt));
            marker.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
            {
                OnContextMenu?.Invoke(evt);
                evt.StopPropagation();
            });

            return marker;
        }

        public void UpdatePosition()
        {
            if (_centerMarker != null)
            {
                float markerSize = 24f;
                float radius = markerSize / 2f;
                _centerMarker.style.left = Data.Center.x - radius;
                _centerMarker.style.top = Data.Center.y - radius;
            }
            MarkDirtyRepaint();
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (_centerMarker != null)
            {
                if (selected)
                {
                    _centerMarker.style.borderTopWidth = 4;
                    _centerMarker.style.borderBottomWidth = 4;
                    _centerMarker.style.borderLeftWidth = 4;
                    _centerMarker.style.borderRightWidth = 4;
                    _centerMarker.style.borderTopColor = Color.yellow;
                    _centerMarker.style.borderBottomColor = Color.yellow;
                    _centerMarker.style.borderLeftColor = Color.yellow;
                    _centerMarker.style.borderRightColor = Color.yellow;
                }
                else
                {
                    _centerMarker.style.borderTopWidth = 2;
                    _centerMarker.style.borderBottomWidth = 2;
                    _centerMarker.style.borderLeftWidth = 2;
                    _centerMarker.style.borderRightWidth = 2;
                    _centerMarker.style.borderTopColor = Color.white;
                    _centerMarker.style.borderBottomColor = Color.white;
                    _centerMarker.style.borderLeftColor = Color.white;
                    _centerMarker.style.borderRightColor = Color.white;
                }
            }
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (Data == null) return;
            if (Data.Orbits == null || Data.Orbits.Count == 0) return;

            var painter = ctx.painter2D;
            painter.lineWidth = _isSelected ? 3f : 2f;
            
            // Рисуем орбиты
            for (int i = 0; i < Data.Orbits.Count; i++)
            {
                var orbit = Data.Orbits[i];
                
                // Цвет орбиты - полупрозрачный цвет кластера, при выделении ярче
                Color orbitColor = Data.EditorColor;
                if (_isSelected)
                {
                    orbitColor.a = Mathf.Min(1f, orbitColor.a * 2f);
                }
                painter.strokeColor = orbitColor;

                if (orbit.IsPartialArc && !Mathf.Approximately(orbit.ArcStartAngle, orbit.ArcEndAngle))
                {
                    DrawArc(painter, Data.Center, orbit.Radius, orbit.ArcStartAngle, orbit.ArcEndAngle);
                }
                else
                {
                    painter.BeginPath();
                    painter.Arc(Data.Center, orbit.Radius, Angle.Degrees(0), Angle.Degrees(360), ArcDirection.Clockwise);
                    painter.Stroke();
                }
            }
            
            // Рисуем дороги к другим кластерам (если есть)
            if (Data.RoadConnections != null && _tree != null)
            {
                painter.lineWidth = 4f;
                painter.strokeColor = new Color(0.8f, 0.6f, 0.2f, 0.6f); // Золотистый цвет дорог
                
                foreach (var targetClusterID in Data.RoadConnections)
                {
                    var targetCluster = _tree.GetCluster(targetClusterID);
                    if (targetCluster != null)
                    {
                        painter.BeginPath();
                        painter.MoveTo(Data.Center);
                        painter.LineTo(targetCluster.Center);
                        painter.Stroke();
                    }
                }
            }
        }

        private void DrawArc(Painter2D painter, Vector2 center, float radius, float startAngle, float endAngle)
        {
            float startRad = startAngle * Mathf.Deg2Rad;
            float endRad = endAngle * Mathf.Deg2Rad;
            if (endAngle < startAngle) endAngle += 360f;

            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(startAngle), Angle.Degrees(endAngle), ArcDirection.Clockwise);
            painter.Stroke();

            Vector2 startPoint = center + new Vector2(Mathf.Cos(startRad) * radius, Mathf.Sin(startRad) * radius);
            Vector2 endPoint = center + new Vector2(Mathf.Cos(endRad) * radius, Mathf.Sin(endRad) * radius);

            float markerSize = 6f;
            painter.fillColor = painter.strokeColor;
            painter.BeginPath();
            painter.Arc(startPoint, markerSize, Angle.Degrees(0), Angle.Degrees(360), ArcDirection.Clockwise);
            painter.Fill();
            painter.BeginPath();
            painter.Arc(endPoint, markerSize, Angle.Degrees(0), Angle.Degrees(360), ArcDirection.Clockwise);
            painter.Fill();
        }

        public void RefreshVisuals()
        {
            if (_centerMarker != null)
            {
                _centerMarker.style.backgroundColor = Data.EditorColor;
            }
            MarkDirtyRepaint();
        }
    }
}
