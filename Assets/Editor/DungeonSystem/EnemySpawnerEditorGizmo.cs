using Scripts.Dungeon;
using Scripts.Enemies;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.EditorTools.DungeonSystem
{
    public static class EnemySpawnerEditorGizmo
    {
        private readonly struct PreviewSpriteInfo
        {
            public PreviewSpriteInfo(Sprite sprite, Matrix4x4 localToRootMatrix)
            {
                Sprite = sprite;
                LocalToRootMatrix = localToRootMatrix;
            }

            public Sprite Sprite { get; }
            public Matrix4x4 LocalToRootMatrix { get; }
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.InSelectionHierarchy)]
        private static void DrawEnemyPreview(EnemySpawner spawner, GizmoType gizmoType)
        {
            if (spawner == null)
                return;

            EnemyDataSO data = spawner.GetPreviewEnemyData();
            if (data == null)
                return;

            PreviewSpriteInfo? previewInfo = ResolvePreviewSprite(data);
            if (!previewInfo.HasValue || previewInfo.Value.Sprite == null || previewInfo.Value.Sprite.texture == null)
                return;

            Sprite sprite = previewInfo.Value.Sprite;
            Matrix4x4 localToRootMatrix = previewInfo.Value.LocalToRootMatrix;

            Bounds spriteBounds = sprite.bounds;
            Vector3[] localCorners =
            {
                new Vector3(spriteBounds.min.x, spriteBounds.min.y, 0f),
                new Vector3(spriteBounds.min.x, spriteBounds.max.y, 0f),
                new Vector3(spriteBounds.max.x, spriteBounds.min.y, 0f),
                new Vector3(spriteBounds.max.x, spriteBounds.max.y, 0f),
            };

            Vector3 worldMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
            Vector3 worldMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);
            for (int i = 0; i < localCorners.Length; i++)
            {
                Vector3 rootLocalPoint = localToRootMatrix.MultiplyPoint3x4(localCorners[i]);
                Vector3 worldPoint = spawner.transform.position + rootLocalPoint;
                worldMin = Vector3.Min(worldMin, worldPoint);
                worldMax = Vector3.Max(worldMax, worldPoint);
            }

            Vector2 guiBottomLeft = HandleUtility.WorldToGUIPoint(worldMin);
            Vector2 guiTopRight = HandleUtility.WorldToGUIPoint(worldMax);
            if (float.IsNaN(guiBottomLeft.x) || float.IsNaN(guiBottomLeft.y) || float.IsNaN(guiTopRight.x) || float.IsNaN(guiTopRight.y))
                return;

            float x = Mathf.Min(guiBottomLeft.x, guiTopRight.x);
            float y = Mathf.Min(guiBottomLeft.y, guiTopRight.y);
            float width = Mathf.Abs(guiTopRight.x - guiBottomLeft.x);
            float height = Mathf.Abs(guiTopRight.y - guiBottomLeft.y);
            if (width < 1f || height < 1f)
                return;

            Rect drawRect = new Rect(x, y, width, height);

            Handles.BeginGUI();
            Color previousColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, ToTextureCoords(sprite), true);
            GUI.color = previousColor;
            Handles.EndGUI();
        }

        private static PreviewSpriteInfo? ResolvePreviewSprite(EnemyDataSO data)
        {
            if (data == null)
                return null;

            if (data.Prefab != null)
            {
                SpriteRenderer[] renderers = data.Prefab.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var renderer in renderers)
                {
                    if (renderer != null && renderer.sprite != null)
                    {
                        Matrix4x4 localToRootMatrix = data.Prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                        return new PreviewSpriteInfo(renderer.sprite, localToRootMatrix);
                    }
                }
            }

            if (data.Animation != null && data.Animation.UsesSpriteSheets && !string.IsNullOrWhiteSpace(data.Animation.IdleSpritesResourcePath))
            {
                Sprite[] frames = Resources.LoadAll<Sprite>(data.Animation.IdleSpritesResourcePath);
                if (frames != null && frames.Length > 0)
                    return new PreviewSpriteInfo(frames[0], Matrix4x4.identity);
            }

            return null;
        }

        private static Rect ToTextureCoords(Sprite sprite)
        {
            Rect rect = sprite.textureRect;
            Texture texture = sprite.texture;
            return new Rect(
                rect.x / texture.width,
                rect.y / texture.height,
                rect.width / texture.width,
                rect.height / texture.height);
        }

    }
}
