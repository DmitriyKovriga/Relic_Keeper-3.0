using UnityEngine;
using UnityEditor;
using Scripts.Dungeon;

namespace Scripts.Dungeon.Editor
{
    [CustomEditor(typeof(DungeonDataSO))]
    public class DungeonDataSOEditor : UnityEditor.Editor
    {
        private static string[] _roomPrefabPaths;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Найти префабы комнат в Resources"))
            {
                RefreshRoomPrefabList();
            }

            if (_roomPrefabPaths != null && _roomPrefabPaths.Length > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "Доступные префабы в Resources:\n" + string.Join("\n", _roomPrefabPaths) +
                    "\n\nСкопируй нужный путь в Normal Room Prefab Paths.",
                    MessageType.Info);
            }
        }

        private static void RefreshRoomPrefabList()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" });
            var paths = new System.Collections.Generic.List<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".prefab"))
                {
                    var resourcesPath = GetResourcesPath(path);
                    if (!string.IsNullOrEmpty(resourcesPath) && path.IndexOf("Dungeon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        paths.Add(resourcesPath);
                }
            }
            _roomPrefabPaths = paths.ToArray();
        }

        private static string GetResourcesPath(string fullPath)
        {
            const string resources = "Resources/";
            int idx = fullPath.IndexOf(resources, System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var sub = fullPath.Substring(idx + resources.Length);
            return sub.Replace(".prefab", "");
        }
    }
}
