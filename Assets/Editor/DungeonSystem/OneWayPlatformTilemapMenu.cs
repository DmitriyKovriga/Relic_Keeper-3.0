using Scripts.Dungeon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RelicKeeper.EditorTools.DungeonSystem
{
    public static class OneWayPlatformTilemapMenu
    {
        [MenuItem("GameObject/2D Object/Relic Keeper/One-Way Platform Tilemap", false, 10)]
        private static void CreateOneWayPlatformTilemap(MenuCommand menuCommand)
        {
            GameObject parent = ResolveParent(menuCommand.context as GameObject);

            GameObject go = new GameObject("Platforms");
            Undo.RegisterCreatedObjectUndo(go, "Create One-Way Platform Tilemap");

            GameObjectUtility.SetParentAndAlign(go, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();
            OneWayPlatformTilemap setup = go.AddComponent<OneWayPlatformTilemap>();
            setup.ApplySetup();

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        private static GameObject ResolveParent(GameObject context)
        {
            if (context != null)
            {
                Grid contextGrid = context.GetComponent<Grid>();
                if (contextGrid != null)
                    return contextGrid.gameObject;

                Grid parentGrid = context.GetComponentInParent<Grid>();
                if (parentGrid != null)
                    return parentGrid.gameObject;
            }

            if (Selection.activeGameObject != null)
            {
                Grid activeGrid = Selection.activeGameObject.GetComponent<Grid>();
                if (activeGrid != null)
                    return activeGrid.gameObject;

                Grid parentGrid = Selection.activeGameObject.GetComponentInParent<Grid>();
                if (parentGrid != null)
                    return parentGrid.gameObject;
            }

            return null;
        }
    }
}
