using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RK.EditorTools.DungeonSystem
{
    public static class GameplayPrefabsMenu
    {
        private const string EnemySpawnerPrefabPath = "Assets/Prefabs/Spawner/EnemySpawner.prefab";
        private const string NextRoomPortalPrefabPath = "Assets/Prefabs/Spawner/NextRoomPortal.prefab";
        private const string PlayerSpawnPointPrefabPath = "Assets/Prefabs/Spawner/PlayerSpawnPoint.prefab";

        [MenuItem("GameObject/Игровые префабы/Спавнер противников", false, 10)]
        private static void CreateEnemySpawner(MenuCommand command)
        {
            CreatePrefab(command, EnemySpawnerPrefabPath);
        }

        [MenuItem("GameObject/Игровые префабы/Портал в следующую комнату", false, 11)]
        private static void CreateNextRoomPortal(MenuCommand command)
        {
            CreatePrefab(command, NextRoomPortalPrefabPath);
        }

        [MenuItem("GameObject/Игровые префабы/Точка спавна игрока", false, 12)]
        private static void CreatePlayerSpawnPoint(MenuCommand command)
        {
            CreatePrefab(command, PlayerSpawnPointPrefabPath);
        }

        private static void CreatePrefab(MenuCommand command, string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[GameplayPrefabsMenu] Prefab not found: {prefabPath}");
                return;
            }

            Transform parent = GetTargetParent(command);
            var instance = parent != null
                ? PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject
                : PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene()) as GameObject;
            if (instance == null)
            {
                Debug.LogError($"[GameplayPrefabsMenu] Failed to instantiate prefab: {prefabPath}");
                return;
            }

            if (parent != null)
            {
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
            }
            else
            {
                SceneView.lastActiveSceneView?.MoveToView(instance.transform);
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Create {prefab.name}");
            Selection.activeGameObject = instance;
            MarkTargetDirty(instance);
        }

        private static Transform GetTargetParent(MenuCommand command)
        {
            if (command.context is GameObject contextObject && IsSceneObject(contextObject))
                return contextObject.transform;

            if (Selection.activeGameObject != null && IsSceneObject(Selection.activeGameObject))
                return Selection.activeGameObject.transform;

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot != null)
                return prefabStage.prefabContentsRoot.transform;

            return null;
        }

        private static bool IsSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static void MarkTargetDirty(GameObject instance)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && instance.scene == prefabStage.scene)
            {
                EditorUtility.SetDirty(prefabStage.prefabContentsRoot);
                EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                return;
            }

            EditorSceneManager.MarkSceneDirty(instance.scene);
        }
    }
}
