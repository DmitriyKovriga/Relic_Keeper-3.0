using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.Visuals
{
    public sealed class RelicSpriteFxShaderGUI : ShaderGUI
    {
        private readonly bool[] _open = { true, false, false, false, false, false };
        private static readonly string[] Titles = { "Color", "Flash", "Outline / inner shadow", "Emission", "Dissolve", "Alpha / quality" };
        private static readonly string[][] Fields =
        {
            new[] { "_FxTint", "_FxBrightness", "_FxContrast", "_FxDesaturate" },
            new[] { "_FxFlashColor", "_FxFlash" },
            new[] { "_FxOutline", "_FxOutlineColor", "_FxOutlineWidth", "_FxInnerShadow", "_FxInnerColor" },
            new[] { "_FxEmission", "_FxEmissionColor" },
            new[] { "_FxDissolve", "_FxNoiseTex", "_FxNoiseScale", "_FxEdgeWidth", "_FxEdgeColor" },
            new[] { "_FxFade", "_FxClip", "_FxQuality" }
        };

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUILayout.HelpBox("Per-sprite effects: add SpriteFxController. Outline needs a Full Rect sprite with transparent padding; use rectangular atlas packing. Emission is crisp additive color, without automatic bloom.", MessageType.Info);
            for (int i = 0; i < Fields.Length; i++)
            {
                _open[i] = EditorGUILayout.BeginFoldoutHeaderGroup(_open[i], Titles[i]);
                if (_open[i])
                {
                    EditorGUI.indentLevel++;
                    foreach (string name in Fields[i])
                    {
                        MaterialProperty property = FindProperty(name, properties);
                        editor.ShaderProperty(property, property.displayName);
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }
    }
}
