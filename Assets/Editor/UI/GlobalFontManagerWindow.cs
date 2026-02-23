using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RelicKeeper.Editor.UI
{
    public class GlobalFontManagerWindow : EditorWindow
    {
        private const string MenuPath = "Tools/UI/Font Manager";
        private const string TmpExamplesPrefix = "Assets/TextMesh Pro/Examples & Extras/";

        [SerializeField] private UIFontProfile _profile;
        [SerializeField] private bool _scanPrefabs = true;
        [SerializeField] private bool _scanScenes = true;
        [SerializeField] private bool _skipTmpExamples = true;

        private Vector2 _scroll;
        private string _report = "";

        private GUIStyle _previewStyle;
        private GUIStyle _previewHeaderStyle;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<GlobalFontManagerWindow>();
            window.titleContent = new GUIContent("Font Manager");
            window.minSize = new Vector2(720f, 560f);
        }

        private void OnEnable()
        {
            if (_profile == null)
                _profile = AssetDatabase.LoadAssetAtPath<UIFontProfile>(EditorPaths.UIFontProfile);

            if (_profile == null)
            {
                var guid = AssetDatabase.FindAssets("t:UIFontProfile").FirstOrDefault();
                if (!string.IsNullOrEmpty(guid))
                    _profile = AssetDatabase.LoadAssetAtPath<UIFontProfile>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawProfileBlock();
            EditorGUILayout.Space(8f);
            DrawPreviewBlock();
            EditorGUILayout.Space(8f);
            DrawValidationBlock();
            EditorGUILayout.Space(8f);
            DrawApplyBlock();

            if (!string.IsNullOrEmpty(_report))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Отчет", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_report, GUILayout.MinHeight(180f));
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawProfileBlock()
        {
            EditorGUILayout.LabelField("Профиль шрифта", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _profile = (UIFontProfile)EditorGUILayout.ObjectField("UIFontProfile", _profile, typeof(UIFontProfile), false);
                if (GUILayout.Button("Создать", GUILayout.Width(100f)))
                    CreateProfileAssetIfMissing();
            }

            if (_profile == null)
            {
                EditorGUILayout.HelpBox(
                    "Профиль не выбран. Нажми 'Создать' или назначь существующий UIFontProfile.",
                    MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            _profile.uiToolkitFont = (Font)EditorGUILayout.ObjectField("UI Toolkit Font", _profile.uiToolkitFont, typeof(Font), false);
            _profile.tmpFontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField("TMP Font Asset", _profile.tmpFontAsset, typeof(TMP_FontAsset), false);
            _profile.previewEnglish = EditorGUILayout.TextField("Preview EN", _profile.previewEnglish);
            _profile.previewRussian = EditorGUILayout.TextField("Preview RU", _profile.previewRussian);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_profile);
                AssetDatabase.SaveAssets();
                UIFontResolver.InvalidateCache();
            }
        }

        private void DrawPreviewBlock()
        {
            EditorGUILayout.LabelField("Превью до применения", EditorStyles.boldLabel);
            if (_profile == null)
                return;

            var previewEn = string.IsNullOrWhiteSpace(_profile.previewEnglish)
                ? "The quick brown fox jumps over 13 lazy dogs."
                : _profile.previewEnglish;
            var previewRu = string.IsNullOrWhiteSpace(_profile.previewRussian)
                ? "Съешь ещё этих мягких французских булок, да выпей чаю."
                : _profile.previewRussian;

            DrawFontPreviewSection("UI Toolkit Font", _profile.uiToolkitFont, previewEn, previewRu);

            var tmpSourceFont = ResolveTmpSourceFont(_profile.tmpFontAsset);
            DrawFontPreviewSection("TMP Font (по source TTF, не по атласу)", tmpSourceFont, previewEn, previewRu);
            if (_profile.tmpFontAsset != null)
            {
                var mode = IsDynamicTmpFont(_profile.tmpFontAsset) ? "Dynamic" : "Static";
                EditorGUILayout.HelpBox($"TMP Asset Mode: {mode}", MessageType.None);
            }
        }

        private void DrawValidationBlock()
        {
            EditorGUILayout.LabelField("Проверка глифов (EN/RU)", EditorStyles.boldLabel);
            if (_profile == null)
                return;

            var previewEn = string.IsNullOrWhiteSpace(_profile.previewEnglish)
                ? "The quick brown fox jumps over 13 lazy dogs."
                : _profile.previewEnglish;
            var previewRu = string.IsNullOrWhiteSpace(_profile.previewRussian)
                ? "Съешь ещё этих мягких французских булок, да выпей чаю."
                : _profile.previewRussian;

            var uiEnMissing = FindMissingCharsInFont(_profile.uiToolkitFont, previewEn);
            var uiRuMissing = FindMissingCharsInFont(_profile.uiToolkitFont, previewRu);
            var tmpEnMissing = FindMissingCharsInTmp(_profile.tmpFontAsset, previewEn);
            var tmpRuMissing = FindMissingCharsInTmp(_profile.tmpFontAsset, previewRu);

            DrawValidationLine("UI Toolkit EN", uiEnMissing);
            DrawValidationLine("UI Toolkit RU", uiRuMissing);
            DrawValidationLine("TMP EN", tmpEnMissing);
            DrawValidationLine("TMP RU", tmpRuMissing);

            if (_profile.tmpFontAsset != null)
            {
                var mode = IsDynamicTmpFont(_profile.tmpFontAsset) ? "Dynamic" : "Static";
                var charsetInfo = GetTmpCharacterSetSummary(_profile.tmpFontAsset);
                EditorGUILayout.HelpBox($"TMP режим: {mode}. Charset: {charsetInfo}", MessageType.None);

                if (!IsDynamicTmpFont(_profile.tmpFontAsset) && tmpRuMissing != null && tmpRuMissing.Count > 0)
                {
                    EditorGUILayout.HelpBox(
                        "Это похоже на STATIC TMP-ассет с ограниченным набором символов (часто ASCII). " +
                        "Для RU нужно либо запекать кириллицу в Character Set, либо использовать Dynamic + fallback.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawApplyBlock()
        {
            EditorGUILayout.LabelField("Применение", EditorStyles.boldLabel);
            _scanPrefabs = EditorGUILayout.ToggleLeft("Применять к префабам (TMP_Text)", _scanPrefabs);
            _scanScenes = EditorGUILayout.ToggleLeft("Применять к сценам (TMP_Text)", _scanScenes);
            _skipTmpExamples = EditorGUILayout.ToggleLeft("Пропустить TMP Examples", _skipTmpExamples);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Dry Run"))
                    _report = RunApply(applyChanges: false);
                if (GUILayout.Button("Применить к проекту"))
                    _report = RunApply(applyChanges: true);
            }
        }

        private string RunApply(bool applyChanges)
        {
            if (_profile == null)
                return "Профиль не выбран.";
            if (_profile.tmpFontAsset == null)
                return "TMP Font Asset не назначен. Применение к TMP отменено.";

            if ((_scanScenes || applyChanges) && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return "Операция отменена пользователем (несохраненные сцены).";

            var stats = new FontApplyStats();
            var setup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                stats.tmpSettingsUpdated = ApplyToTmpSettings(_profile.tmpFontAsset, applyChanges);

                if (_scanPrefabs)
                    ProcessPrefabs(_profile.tmpFontAsset, applyChanges, stats);
                if (_scanScenes)
                    ProcessScenes(_profile.tmpFontAsset, applyChanges, stats);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            if (applyChanges)
            {
                UIFontResolver.InvalidateCache();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return BuildReport(stats, applyChanges);
        }

        private int ApplyToTmpSettings(TMP_FontAsset fontAsset, bool applyChanges)
        {
            if (TMP_Settings.defaultFontAsset == fontAsset)
                return 0;

            if (!applyChanges)
                return 1;

            TMP_Settings.defaultFontAsset = fontAsset;
            return 1;
        }

        private void ProcessPrefabs(TMP_FontAsset fontAsset, bool applyChanges, FontApplyStats stats)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!ShouldProcessPath(path))
                    continue;

                stats.prefabsScanned++;
                var root = PrefabUtility.LoadPrefabContents(path);
                var changed = false;

                try
                {
                    var texts = root.GetComponentsInChildren<TMP_Text>(true);
                    foreach (var text in texts)
                    {
                        stats.tmpTextsScanned++;
                        if (text.font == fontAsset)
                            continue;

                        stats.tmpTextsNeedUpdate++;
                        if (!applyChanges)
                            continue;

                        text.font = fontAsset;
                        EditorUtility.SetDirty(text);
                        stats.tmpTextsUpdated++;
                        changed = true;
                    }

                    if (applyChanges && changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        stats.prefabsChanged++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private void ProcessScenes(TMP_FontAsset fontAsset, bool applyChanges, FontApplyStats stats)
        {
            var guids = AssetDatabase.FindAssets("t:Scene");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!ShouldProcessPath(path))
                    continue;

                stats.scenesScanned++;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var sceneChanged = false;

                foreach (var rootGo in scene.GetRootGameObjects())
                {
                    var texts = rootGo.GetComponentsInChildren<TMP_Text>(true);
                    foreach (var text in texts)
                    {
                        stats.tmpTextsScanned++;
                        if (text.font == fontAsset)
                            continue;

                        stats.tmpTextsNeedUpdate++;
                        if (!applyChanges)
                            continue;

                        text.font = fontAsset;
                        EditorUtility.SetDirty(text);
                        stats.tmpTextsUpdated++;
                        sceneChanged = true;
                    }
                }

                if (applyChanges && sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    stats.scenesChanged++;
                }
            }
        }

        private bool ShouldProcessPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return false;
            if (_skipTmpExamples && path.StartsWith(TmpExamplesPrefix, StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }

        private string BuildReport(FontApplyStats stats, bool applyChanges)
        {
            var mode = applyChanges ? "APPLY" : "DRY RUN";
            return
                $"[{mode}]\n" +
                $"TMP Settings обновлен: {stats.tmpSettingsUpdated}\n" +
                $"Префабы: просмотрено {stats.prefabsScanned}, изменено {stats.prefabsChanged}\n" +
                $"Сцены: просмотрено {stats.scenesScanned}, изменено {stats.scenesChanged}\n" +
                $"TMP_Text: просмотрено {stats.tmpTextsScanned}, требуется обновить {stats.tmpTextsNeedUpdate}, обновлено {stats.tmpTextsUpdated}";
        }

        private void CreateProfileAssetIfMissing()
        {
            if (_profile != null)
                return;

            var folder = Path.GetDirectoryName(EditorPaths.UIFontProfile);
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                EnsureFolderHierarchy(folder);
            }

            _profile = CreateInstance<UIFontProfile>();
            AssetDatabase.CreateAsset(_profile, EditorPaths.UIFontProfile);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(_profile);
        }

        private static void EnsureFolderHierarchy(string targetFolder)
        {
            var parts = targetFolder.Split('/');
            if (parts.Length == 0)
                return;

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private void DrawFontPreviewSection(string title, Font font, string en, string ru)
        {
            EditorGUILayout.LabelField(title, _previewHeaderStyle);
            DrawSampleLine($"EN: {en}", font);
            DrawSampleLine($"RU: {ru}", font);
        }

        private void DrawSampleLine(string text, Font font)
        {
            _previewStyle.font = font;
            EditorGUILayout.LabelField(text, _previewStyle, GUILayout.MinHeight(26f));
        }

        private void DrawValidationLine(string caption, HashSet<char> missing)
        {
            if (missing == null)
            {
                EditorGUILayout.HelpBox($"{caption}: шрифт не назначен.", MessageType.Warning);
                return;
            }

            if (missing.Count == 0)
            {
                EditorGUILayout.HelpBox($"{caption}: OK", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox($"{caption}: отсутствуют символы -> {FormatMissingChars(missing)}", MessageType.Error);
        }

        private static HashSet<char> FindMissingCharsInFont(Font font, string text)
        {
            if (font == null)
                return null;

            var missing = new HashSet<char>();
            if (string.IsNullOrEmpty(text))
                return missing;

            foreach (var c in text)
            {
                if (char.IsWhiteSpace(c))
                    continue;
                if (!font.HasCharacter(c))
                    missing.Add(c);
            }

            return missing;
        }

        private static HashSet<char> FindMissingCharsInTmp(TMP_FontAsset font, string text)
        {
            if (font == null)
                return null;

            var missing = new HashSet<char>();
            if (string.IsNullOrEmpty(text))
                return missing;

            foreach (var c in text)
            {
                if (char.IsWhiteSpace(c))
                    continue;
                if (!TmpCanRenderChar(font, c))
                    missing.Add(c);
            }

            return missing;
        }

        private static bool TmpCanRenderChar(TMP_FontAsset font, char c)
        {
            if (font == null)
                return false;

            var visited = new HashSet<TMP_FontAsset>();
            if (TmpCanRenderCharRecursive(font, c, visited))
                return true;

            // Учитываем глобальные fallback-шрифты TMP Settings.
            var globalFallbacks = TMP_Settings.fallbackFontAssets;
            if (globalFallbacks != null)
            {
                foreach (var fallback in globalFallbacks)
                {
                    if (TmpCanRenderCharRecursive(fallback, c, visited))
                        return true;
                }
            }

            return false;
        }

        private static bool TmpCanRenderCharRecursive(TMP_FontAsset font, char c, HashSet<TMP_FontAsset> visited)
        {
            if (font == null || !visited.Add(font))
                return false;

            // 1) Уже есть в самом TMP-ассете (статический атлас / уже добавленные dynamic-глифы).
            if (font.HasCharacter(c))
                return true;

            // 2) Есть в fallback TMP-ассетах.
            var fallbacks = font.fallbackFontAssetTable;
            if (fallbacks != null)
            {
                foreach (var fallback in fallbacks)
                {
                    if (TmpCanRenderCharRecursive(fallback, c, visited))
                        return true;
                }
            }

            // 3) Dynamic TMP может дорисовать символ из source font во время работы.
            var sourceFont = ResolveTmpSourceFont(font);
            if (IsDynamicTmpFont(font) && sourceFont != null && sourceFont.HasCharacter(c))
                return true;

            return false;
        }

        private static bool IsDynamicTmpFont(TMP_FontAsset font)
        {
            if (font == null)
                return false;
            var mode = font.atlasPopulationMode.ToString();
            return mode.IndexOf("Dynamic", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetTmpCharacterSetSummary(TMP_FontAsset font)
        {
            if (font == null)
                return "n/a";

            try
            {
                var so = new SerializedObject(font);
                var setMode = so.FindProperty("m_CreationSettings.characterSetSelectionMode");
                var seq = so.FindProperty("m_CreationSettings.characterSequence");
                var modeText = setMode != null ? setMode.intValue.ToString() : "?";
                var seqText = seq != null ? seq.stringValue : "?";
                return $"mode={modeText}, sequence={seqText}";
            }
            catch
            {
                return "unavailable";
            }
        }

        private static Font ResolveTmpSourceFont(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return null;
            if (fontAsset.sourceFontFile != null)
                return fontAsset.sourceFontFile;

            try
            {
                var so = new SerializedObject(fontAsset);
                var guidProp = so.FindProperty("m_SourceFontFileGUID");
                var guid = guidProp != null ? guidProp.stringValue : null;
                if (string.IsNullOrEmpty(guid))
                    return null;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    return null;
                return AssetDatabase.LoadAssetAtPath<Font>(path);
            }
            catch
            {
                return null;
            }
        }

        private static string FormatMissingChars(IEnumerable<char> chars)
        {
            var ordered = chars.OrderBy(c => c).ToArray();
            return string.Join(", ", ordered.Select(c => $"{c}(U+{(int)c:X4})"));
        }

        private void EnsureStyles()
        {
            if (_previewStyle == null)
            {
                _previewStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    fontSize = 14,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (_previewHeaderStyle == null)
            {
                _previewHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12
                };
            }
        }

        private sealed class FontApplyStats
        {
            public int tmpSettingsUpdated;
            public int prefabsScanned;
            public int prefabsChanged;
            public int scenesScanned;
            public int scenesChanged;
            public int tmpTextsScanned;
            public int tmpTextsNeedUpdate;
            public int tmpTextsUpdated;
        }
    }
}
