using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    public class PassiveNodeTemplatePickerWindow : EditorWindow
    {
        private static Action<PassiveNodeTemplateSO> _onTemplatePicked;

        private readonly Dictionary<string, bool> _groupFoldouts = new Dictionary<string, bool>();
        private IReadOnlyList<PassiveNodeTemplateSO> _templates = Array.Empty<PassiveNodeTemplateSO>();
        private PassiveNodeTemplateSO _currentSelection;
        private Vector2 _scroll;
        private string _search = string.Empty;

        public static void Open(PassiveNodeTemplateSO currentSelection, Action<PassiveNodeTemplateSO> onTemplatePicked)
        {
            _onTemplatePicked = onTemplatePicked;
            var window = CreateInstance<PassiveNodeTemplatePickerWindow>();
            window._currentSelection = currentSelection;
            window.titleContent = new GUIContent("Passive Node Picker");
            window.minSize = new Vector2(460f, 520f);
            window.ShowAuxWindow();
        }

        private void OnEnable()
        {
            RefreshTemplates();
        }

        private void OnDisable()
        {
            _onTemplatePicked = null;
        }

        private void OnGUI()
        {
            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            string search = (_search ?? string.Empty).Trim().ToLowerInvariant();
            var groups = _templates
                .Where(template => template != null)
                .Where(template => MatchesSearch(template, search))
                .GroupBy(PassiveNodeTemplateLibrary.GetCategory)
                .OrderBy(group => group.Key);

            foreach (var group in groups)
            {
                if (!_groupFoldouts.ContainsKey(group.Key))
                    _groupFoldouts[group.Key] = true;

                _groupFoldouts[group.Key] = EditorGUILayout.Foldout(_groupFoldouts[group.Key], $"{group.Key} ({group.Count()})", true);
                if (!_groupFoldouts[group.Key])
                    continue;

                EditorGUI.indentLevel++;
                foreach (var template in group.OrderBy(PassiveNodeTemplateLibrary.GetDisplayName))
                    DrawTemplateRow(template);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(6f);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Search", GUILayout.Width(44f));
            string newSearch = GUILayout.TextField(_search ?? string.Empty, EditorStyles.toolbarTextField, GUILayout.ExpandWidth(true));
            if (newSearch != _search)
                _search = newSearch;

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                RefreshTemplates();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTemplateRow(PassiveNodeTemplateSO template)
        {
            Rect rowRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            DrawTemplateIcon(template);

            EditorGUILayout.BeginVertical();
            string displayName = PassiveNodeTemplateLibrary.GetDisplayName(template);
            string summary = PassiveNodeTemplateLibrary.GetSummary(template, 3);
            EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedMiniLabel, GUILayout.MaxHeight(40f));
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(template == _currentSelection))
            {
                if (GUILayout.Button(template == _currentSelection ? "Selected" : "Use", GUILayout.Width(72f), GUILayout.Height(24f)))
                {
                    _onTemplatePicked?.Invoke(template);
                    Close();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            string tooltip = $"{displayName}\n\n{PassiveNodeTemplateLibrary.GetSummary(template, 6)}";
            if (rowRect.Contains(Event.current.mousePosition))
            {
                GUI.Label(rowRect, new GUIContent(string.Empty, tooltip));
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    _onTemplatePicked?.Invoke(template);
                    Close();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private static void DrawTemplateIcon(PassiveNodeTemplateSO template)
        {
            Rect rect = GUILayoutUtility.GetRect(34f, 34f, GUILayout.Width(34f), GUILayout.Height(34f));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

            Texture texture = null;
            if (template != null && template.Icon != null)
                texture = AssetPreview.GetAssetPreview(template.Icon) ?? AssetPreview.GetMiniThumbnail(template.Icon);

            if (texture != null)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.Label(rect, "?", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void RefreshTemplates()
        {
            _templates = PassiveNodeTemplateLibrary.LoadAllTemplates();
        }

        private static bool MatchesSearch(PassiveNodeTemplateSO template, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            string haystack = $"{PassiveNodeTemplateLibrary.GetDisplayName(template)} {PassiveNodeTemplateLibrary.GetCategory(template)} {PassiveNodeTemplateLibrary.GetSummary(template, 5)}".ToLowerInvariant();
            return haystack.Contains(search);
        }
    }
}
