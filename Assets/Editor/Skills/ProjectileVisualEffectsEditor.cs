using Scripts.Skills.Projectiles;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor.Skills
{
    [CustomEditor(typeof(ProjectileVisualEffects))]
    public sealed class ProjectileVisualEffectsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Projectile visual stack. All blocks are opt-in: enable only the effects you need, then unfold the block to tune it.",
                MessageType.Info);

            DrawEffectBlock("Trail", "_trail", DrawTrail);
            DrawEffectBlock("Particles / sparks / smoke", "_particles", DrawParticles);
            DrawEffectBlock("Ribbon tail", "_ribbon", DrawRibbon);
            DrawEffectBlock("Dynamic glow / additive look", "_dynamicGlow", DrawDynamicGlow);
            DrawEffectBlock("Color over lifetime", "_colorOverLifetime", DrawColorOverLifetime);
            DrawEffectBlock("Color vibration", "_colorFlicker", DrawColorFlicker);
            DrawEffectBlock("Ripple shockwave", "_ripple", DrawRipple);
            DrawEffectBlock("Air collision sparks", "_airSparks", DrawAirSparks);
            DrawEffectBlock("Pixel distortion / heat shimmer", "_distortion", DrawDistortion);
            DrawEffectBlock("Halo layers", "_halo", DrawHalo);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEffectBlock(string title, string propertyName, System.Action<SerializedProperty> drawBody)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            SerializedProperty enabled = property.FindPropertyRelative("Enabled");
            string key = $"{target.GetInstanceID()}.{propertyName}.foldout";
            bool foldout = SessionState.GetBool(key, false);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            if (enabled != null)
                enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(18));

            using (new EditorGUI.DisabledScope(enabled != null && !enabled.boolValue))
            {
                foldout = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);
            }
            SessionState.SetBool(key, foldout);
            EditorGUILayout.EndHorizontal();

            if (foldout && (enabled == null || enabled.boolValue))
            {
                EditorGUI.indentLevel++;
                drawBody(property);
                EditorGUI.indentLevel--;
            }
            else if (enabled != null && !enabled.boolValue)
            {
                EditorGUILayout.LabelField("Disabled", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawTrail(SerializedProperty p)
        {
            Draw(p, "Intensity", "Intensity");
            Draw(p, "Time", "Length time");
            Draw(p, "StartWidth", "Start width");
            Draw(p, "EndWidth", "End width");
            Draw(p, "MinVertexDistance", "Min vertex distance");
            Draw(p, "Color", "Color");
            Draw(p, "Material", "Material override");
            Draw(p, "SortingOrderOffset", "Sorting order offset");
        }

        private static void DrawParticles(SerializedProperty p)
        {
            Draw(p, "Preset", "Preset");
            Draw(p, "Intensity", "Intensity");
            Draw(p, "Rate", "Rate");
            Draw(p, "Lifetime", "Lifetime");
            Draw(p, "Speed", "Speed");
            Draw(p, "Size", "Size");
            Draw(p, "ConeAngle", "Cone angle (smoke only)");
            Draw(p, "SpawnRadiusMultiplier", "Spawn radius multiplier");
            Draw(p, "SpawnRadiusJitter", "Spawn radius jitter");
            Draw(p, "DirectionRandomness", "Direction randomness");
            Draw(p, "StartColor", "Start color");
            Draw(p, "EndColor", "End color");
            Draw(p, "Material", "Material override");
            Draw(p, "SortingOrderOffset", "Sorting order offset");
            EditorGUILayout.HelpBox(
                "Sparks emit manually from random points around the projectile sprite edge and fly outward in all directions. Smoke keeps normal particle emission.",
                MessageType.None);
        }

        private static void DrawRibbon(SerializedProperty p)
        {
            Draw(p, "Intensity", "Intensity");
            Draw(p, "MaxPoints", "Max points");
            Draw(p, "SampleDistance", "Sample distance");
            Draw(p, "Width", "Width");
            Draw(p, "Color", "Color");
            Draw(p, "Material", "Material override");
            Draw(p, "SortingOrderOffset", "Sorting order offset");
        }

        private static void DrawDynamicGlow(SerializedProperty p)
        {
            Draw(p, "Intensity", "Light intensity");
            Draw(p, "Color", "Color");
            Draw(p, "Radius", "Light radius");
            Draw(p, "PulseAmount", "Pulse amount");
            Draw(p, "PulseSpeed", "Pulse speed");
            Draw(p, "AdditiveSpriteMaterial", "Use additive sprite material");
            EditorGUILayout.HelpBox("Uses URP Light2D when available. Additive sprite material is a cheap visual glow fallback.", MessageType.None);
        }

        private static void DrawColorOverLifetime(SerializedProperty p)
        {
            Draw(p, "Lifetime", "Lifetime");
            Draw(p, "Loop", "Loop");
            Draw(p, "Intensity", "Intensity");
            Draw(p, "Color", "Gradient");
        }

        private static void DrawColorFlicker(SerializedProperty p)
        {
            Draw(p, "Intensity", "Intensity");
            Draw(p, "ColorA", "Color A");
            Draw(p, "ColorB", "Color B");
            Draw(p, "Frequency", "Frequency");
        }

        private static void DrawRipple(SerializedProperty p)
        {
            Draw(p, "Intensity", "Intensity");
            Draw(p, "Interval", "Spawn interval");
            Draw(p, "Lifetime", "Lifetime");
            Draw(p, "StartRadius", "Start radius");
            Draw(p, "EndRadius", "End radius");
            Draw(p, "Segments", "Segments");
            Draw(p, "Width", "Line width");
            Draw(p, "Color", "Color");
            Draw(p, "Material", "Material override");
            Draw(p, "SortingOrderOffset", "Sorting order offset");
        }

        private static void DrawAirSparks(SerializedProperty p)
        {
            Draw(p, "Intensity", "Intensity");
            Draw(p, "MinSpeed", "Min projectile speed");
            Draw(p, "RateAtMinSpeed", "Rate at min speed");
            Draw(p, "RateAtHighSpeed", "Rate at high speed");
            Draw(p, "HighSpeedReference", "High speed reference");
            Draw(p, "Lifetime", "Lifetime");
            Draw(p, "Size", "Size");
            Draw(p, "Color", "Color");
            Draw(p, "Material", "Material override");
            Draw(p, "SortingOrderOffset", "Sorting order offset");
        }

        private static void DrawDistortion(SerializedProperty p)
        {
            Draw(p, "Intensity", "Intensity");
            Draw(p, "Axis", "Axis");
            Draw(p, "Amplitude", "Pixel offset amplitude");
            Draw(p, "Frequency", "Frequency");
            Draw(p, "Alpha", "Alpha");
            Draw(p, "Scale", "Scale");
            Draw(p, "SortingOrderOffset", "Sorting order offset");
            EditorGUILayout.HelpBox("This is a cheap heat-shimmer sprite layer. True background distortion needs a shader/render feature later.", MessageType.None);
        }

        private static void DrawHalo(SerializedProperty p)
        {
            Draw(p, "Intensity", "Intensity");
            Draw(p, "Layers", "Layers");
            Draw(p, "Color", "Color");
            Draw(p, "BaseScale", "Base scale");
            Draw(p, "ScaleStep", "Scale per layer");
            Draw(p, "PulseAmount", "Pulse amount");
            Draw(p, "PulseSpeed", "Pulse speed");
            Draw(p, "Material", "Material override");
            Draw(p, "SortingOrderOffset", "Sorting order offset");
        }

        private static void Draw(SerializedProperty parent, string relativeName, string label)
        {
            SerializedProperty property = parent.FindPropertyRelative(relativeName);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }
    }
}
