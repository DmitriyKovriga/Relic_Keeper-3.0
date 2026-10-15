using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Dungeon;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    /// <summary>Checks real room prefabs without entering a run, changing inventory, or saving progress.</summary>
    public static class RoomLootRuntimeCheck
    {
        public static void Capture()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            if (Object.FindFirstObjectByType<RoomController>() != null)
                throw new InvalidOperationException("Run this check from the hub, outside an active dungeon room.");

            string folder = Path.Combine(Path.GetTempPath(), "room-loot-check"); Directory.CreateDirectory(folder);
            string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Prefabs/Dungeon/MineDungeon" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
            var itemData = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(MysticShieldRepriseBuilder.ItemPath);
            int checkedRooms = 0;
            bool hadFilter = PlayerPrefs.HasKey(LootFilterSettings.PlayerPrefsKey);
            int filter = PlayerPrefs.GetInt(LootFilterSettings.PlayerPrefsKey, 0);
            PlayerPrefs.SetInt(LootFilterSettings.PlayerPrefsKey, (int)LootFilterThreshold.None);
            var report = new System.Text.StringBuilder();
            try
            {
                foreach (string path in paths)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab.GetComponent<RoomController>() == null) continue;
                    var instance = Object.Instantiate(prefab, new Vector3(2000, 0, 0), Quaternion.identity);
                    try
                    {
                        var room = instance.GetComponent<RoomController>();
                        var exit = room.GetComponentsInChildren<DungeonPortal>(true).FirstOrDefault(p => p.Type == PortalType.NextRoom)
                            ?? room.GetComponentsInChildren<DungeonPortal>(true).FirstOrDefault(p => p.Type == PortalType.ReturnToHub);
                        if (exit == null) throw new InvalidOperationException("No exit in " + path);
                        exit.SetActive(false);
                        Physics2D.SyncTransforms();
                        var first = new InventoryItem(itemData);
                        WorldItemDropService.SpawnOnGround(first, (Vector2)room.transform.position + Vector2.left * 20);
                        WorldItemDropService.SpawnOnGround(new InventoryItem(itemData), (Vector2)room.transform.position + Vector2.right * 20);
                        var cache = room.GetComponent<RoomLootCollector>().Cache;
                        Require(cache.Count == 2 && cache.Items[0] == first, "Drops must retain both actual items in one cache.");
                        Require(room.GetComponentsInChildren<WorldDroppedItem>(true).Length == 0, "No ground item visuals allowed.");
                        Require(room.GetComponentsInChildren<WorldLootCacheFlightVisual>(true).Length == 0, "No cross-room loot flight allowed.");
                        Require(cache.CanInteract(), "Cache must work before the exit unlocks.");
                        Require(cache.transform.position.x < exit.transform.position.x, "Cache must be left of exit.");
                        var floor = Physics2D.Raycast((Vector2)cache.transform.position, Vector2.down, 1.5f, (1 << 6) | LayerMask.GetMask("OneWayPlatform"));
                        if (floor.collider == null || floor.normal.y <= 0.55f)
                        {
                            exit.SetActive(true);
                            Render(cache, exit, Path.Combine(folder, prefab.name + "-Placement.png"));
                            throw new InvalidOperationException($"Cache must have a floor beneath it in {path}: cache={cache.transform.position}, exit={exit.transform.position}.");
                        }
                        exit.SetActive(true);
                        var probe = new GameObject("Temporary interaction probe"); probe.SetActive(false);
                        try
                        {
                            probe.transform.position = exit.transform.position;
                            var interaction = probe.AddComponent<PlayerInteractController>();
                            Physics2D.SyncTransforms();
                            var selected = typeof(PlayerInteractController).GetMethod("FindNearbyInteractable", BindingFlags.Instance | BindingFlags.NonPublic)
                                .Invoke(interaction, null);
                            Require(ReferenceEquals(selected, exit), "Loot must not steal exit interaction in " + path);
                        }
                        finally { Object.DestroyImmediate(probe); }
                        if (checkedRooms == 0) Render(cache, exit, Path.Combine(folder, "ExitLoot.png"));
                        report.AppendLine($"PASS {prefab.name}: cache {cache.transform.position}; exit {exit.transform.position}; 2 items, no loose drops, exit interaction intact.");
                        checkedRooms++;
                    }
                    finally { Object.DestroyImmediate(instance); }
                }
                Require(checkedRooms > 0, "No room prefabs were checked.");
                File.WriteAllText(Path.Combine(folder, "Report.txt"), report.ToString());
                Debug.Log($"Room loot check passed in {checkedRooms} real room prefabs. Preview: {folder}");
            }
            finally
            {
                if (hadFilter) PlayerPrefs.SetInt(LootFilterSettings.PlayerPrefsKey, filter);
                else PlayerPrefs.DeleteKey(LootFilterSettings.PlayerPrefsKey);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Render(WorldLootCache cache, DungeonPortal portal, string path)
        {
            var camera = new GameObject("Temporary room loot camera").AddComponent<Camera>();
            var target = new RenderTexture(480, 270, 24);
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                if (Camera.main != null) camera.CopyFrom(Camera.main);
                camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = 270f / 48f; camera.aspect = 480f / 270f;
                camera.transform.position = (cache.transform.position + portal.transform.position) * 0.5f + new Vector3(0, 2, -10);
                camera.ResetWorldToCameraMatrix(); camera.ResetProjectionMatrix();
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; target.Release();
                Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
