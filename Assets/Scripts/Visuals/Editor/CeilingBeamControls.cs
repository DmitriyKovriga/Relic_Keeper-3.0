using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.Visuals
{
    // One control layout and one set of units for the room preview and shaft Inspector.
    internal static class CeilingBeamControls
    {
        internal static LocalLightShaftSource.Appearance AimAt(Transform pose, LocalLightShaftSource.Appearance value, Vector3 direction)
        {
            Vector3 local = pose.InverseTransformVector(direction);
            return LocalLightShaftSource.Aim(value, local.magnitude, Mathf.Atan2(local.x, -local.y) * Mathf.Rad2Deg);
        }
        internal static bool Draw(ref LocalLightShaftSource.Appearance value, ref Vector3 point, Transform pose)
        {
            EditorGUI.BeginChangeCheck();
            value.visible = EditorGUILayout.Toggle("Show this beam", value.visible);
            value.followEnvironment = EditorGUILayout.Toggle(new GUIContent("Follow environment", "Use the environment's overall brightness and dust multipliers. Disable to tune this beam independently; Show beams remains a master switch."), value.followEnvironment);
            Vector2 position = EditorGUILayout.Vector2Field("Source position", new Vector2(point.x, point.y));
            Vector3 direction = pose.TransformVector(Quaternion.Euler(0, 0, value.angle) * (Vector3)LocalLightShaftSource.Axis(value));
            EditorGUI.BeginChangeCheck();
            float radius = EditorGUILayout.Slider(new GUIContent("Radius", "Distance from the light source to the visible beam tip, in world units."), direction.magnitude, 0.25f, 30);
            float rotation = EditorGUILayout.Slider(new GUIContent("Rotation (degrees)", "0 points down; positive angles turn toward the right. The Scene guide follows this direction."), Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg, -180, 180);
            if (EditorGUI.EndChangeCheck()) value = AimAt(pose, value, Quaternion.Euler(0, 0, rotation) * Vector3.down * radius);
            value.strength = EditorGUILayout.Slider(new GUIContent("Strength", "Brightness of the beam's color; opacity controls transparency separately."), value.strength, 0, 4);
            value.opacity = EditorGUILayout.Slider("Opacity (%)", value.opacity * 100, 0, 100) / 100;
            value.tint = EditorGUILayout.ColorField(new GUIContent("Beam color"), value.tint, true, false, false);
            value.softness = EditorGUILayout.Slider("Edge softness", value.softness, 0.5f, 3);
            value.endFade = EditorGUILayout.Slider("End fade (%)", value.endFade * 100, 5, 80) / 100;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Dust", EditorStyles.boldLabel);
            value.dustEnabled = EditorGUILayout.Toggle("Enable dust", value.dustEnabled);
            using (new EditorGUI.DisabledScope(!value.dustEnabled))
            {
                value.dust = EditorGUILayout.Slider("Amount (%)", value.dust * 100, 0, 100) / 100;
                value.dustOpacity = EditorGUILayout.Slider("Dust opacity (%)", value.dustOpacity * 100, 0, 100) / 100;
                value.dustSpeed = EditorGUILayout.Slider("Drift speed (%)", value.dustSpeed * 100, 0, 300) / 100;
            }
            point = new Vector3(position.x, position.y, point.z);
            return EditorGUI.EndChangeCheck();
        }
        internal static Vector3 AnchorOffset(LocalLightShaftSource source) => source.transform.localRotation *
            Vector3.Scale(source.transform.InverseTransformPoint(source.SourcePosition), source.transform.localScale);

        internal static void Commit(LocalLightShaftSource source, LocalLightShaftSource.Appearance value, Vector3 point)
        {
            Undo.RecordObjects(new Object[] { source, source.transform }, "Edit ceiling beam");
            source.transform.localPosition = point - AnchorOffset(source); source.ApplyAppearance(value);
            LocalLightShaftSetup.Reclip(source);
            EditorUtility.SetDirty(source); EditorUtility.SetDirty(source.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            PrefabUtility.RecordPrefabInstancePropertyModifications(source.transform);
            SceneView.RepaintAll();
        }
    }
}
