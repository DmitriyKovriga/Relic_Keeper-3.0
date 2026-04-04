using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Skills.PassiveTree
{
    [CreateAssetMenu(menuName = "RPG/Passive Tree/Cluster Template", fileName = "NewPassiveClusterTemplate")]
    public class PassiveClusterTemplateSO : ScriptableObject
    {
        public Color EditorColor = new Color(0.5f, 0.5f, 1f, 0.3f);
        public List<PassiveOrbitDefinition> Orbits = new List<PassiveOrbitDefinition>();

        public void CaptureFrom(PassiveClusterDefinition cluster)
        {
            if (cluster == null)
                return;

            EditorColor = cluster.EditorColor;
            Orbits = CloneOrbits(cluster.Orbits);
        }

        public void ApplyTo(PassiveClusterDefinition cluster)
        {
            if (cluster == null)
                return;

            cluster.EditorColor = EditorColor;
            cluster.Orbits = CloneOrbits(Orbits);
        }

        private static List<PassiveOrbitDefinition> CloneOrbits(List<PassiveOrbitDefinition> source)
        {
            var result = new List<PassiveOrbitDefinition>();
            if (source == null)
                return result;

            foreach (var orbit in source)
            {
                if (orbit == null)
                    continue;

                result.Add(new PassiveOrbitDefinition
                {
                    Radius = orbit.Radius,
                    IsPartialArc = orbit.IsPartialArc,
                    ArcStartAngle = orbit.ArcStartAngle,
                    ArcEndAngle = orbit.ArcEndAngle
                });
            }

            return result;
        }
    }
}
