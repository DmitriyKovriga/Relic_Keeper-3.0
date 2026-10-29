using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Dungeon;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.World;
using Scripts.Items.Affixes;
using Scripts.Visuals.Atmosphere;
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
                        var flights = room.GetComponentsInChildren<WorldLootCacheFlightVisual>(true);
                        Require(flights.Length == 2, "Each visible drop must spawn its own flight to the cache.");
                        foreach (var flight in flights)
                        {
                            Require(flight.GetComponent<TrailRenderer>() != null, "Loot flight needs its golden trail.");
                            Require(flight.GetComponents<Collider2D>().Length == 0, "Flight visuals must not block interactions.");
                        }
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
                        report.AppendLine($"PASS {prefab.name}: cache {cache.transform.position}; exit {exit.transform.position}; 2 items stored immediately, 2 golden flights, no loose drops, exit interaction intact.");
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

        public static void CaptureFlight()
        {
            CaptureFlightCore(1, false);
        }

        public static void CaptureIntakeBurst()
        {
            CaptureFlightCore(20, false);
        }

        public static void CaptureDiscard()
        {
            CaptureFlightCore(1, true);
        }

        public static void CaptureOpenUiArrival()
        {
            CaptureFlightCore(2, false, true);
        }

        public static void CaptureRareArrival() => CaptureFlightCore(1, false, false, 1);
        public static void CaptureUniqueArrival() => CaptureFlightCore(1, false, false, 2);
        public static void CaptureRareBurst() => CaptureFlightCore(20, false, false, 1);
        public static void CaptureUniqueDiscard() => CaptureFlightCore(1, true, false, 2);

        private static void CaptureFlightCore(int lootCount, bool discarded, bool openUi = false, int quality = 0)
        {
            if (!EditorApplication.isPlaying || Object.FindFirstObjectByType<RoomController>() != null)
                throw new InvalidOperationException("Run from the hub in Play Mode.");
            var manager = Object.FindFirstObjectByType<WindowManager>();
            if (openUi) Require(manager != null && !manager.HasOpenWindow, "Close game windows before the isolated cache UI check.");
            string folder = Path.Combine(Path.GetTempPath(), "room-loot-check"); Directory.CreateDirectory(folder);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab");
            var instance = Object.Instantiate(prefab, new Vector3(2000, 0), Quaternion.identity);
            foreach (var spawner in instance.GetComponentsInChildren<EnemySpawner>(true)) spawner.enabled = false;
            var room = instance.GetComponent<RoomController>(); room.enabled = false;
            var portal = room.GetComponentsInChildren<DungeonPortal>(true).First(p => p.Type == PortalType.NextRoom);
            portal.SetActive(true); Physics2D.SyncTransforms();
            var collector = room.gameObject.AddComponent<RoomLootCollector>();
            Vector3 source = collector.ResolveCachePosition() + new Vector2(-4, 1.5f);
            bool hadFilter = PlayerPrefs.HasKey(LootFilterSettings.PlayerPrefsKey);
            int filter = PlayerPrefs.GetInt(LootFilterSettings.PlayerPrefsKey, 0);
            PlayerPrefs.SetInt(LootFilterSettings.PlayerPrefsKey, (int)LootFilterThreshold.None);
            var data = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(MysticShieldRepriseBuilder.ItemPath);
            ItemAffixSO fixtureAffix = null;
            if (quality > 0)
            {
                data = Object.Instantiate(data); data.IsUnique = quality == 2;
                if (quality == 1) fixtureAffix = ScriptableObject.CreateInstance<ItemAffixSO>();
            }
            InventoryItem NewItem()
            {
                var item = new InventoryItem(data);
                if (fixtureAffix != null)
                    for (int i = 0; i < 4; i++) item.Affixes.Add(new AffixInstance(fixtureAffix, item));
                return item;
            }
            for (int i = 0; i < lootCount; i++)
            {
                if (discarded) Require(WorldItemDropService.TryDropAtPlayer(NewItem()), "Discard must be stored.");
                else WorldItemDropService.Spawn(NewItem(), source + Vector3.up * (i % 3) * 0.2f);
            }
            Require(collector.Cache.Count == lootCount && collector.Cache.CanInteract(), "All loot must be available before flight completion.");
            if (lootCount == 20) VerifyFullInventory(collector.Cache);
            float flightDuration = room.GetComponentsInChildren<WorldLootCacheFlightVisual>().Select(f => f.Duration)
                .DefaultIfEmpty(WorldLootCacheFlightVisual.FlightDuration).Max();
            float checkDuration = flightDuration + 1.05f;
            Vector3 cachePosition = collector.Cache.transform.position;
            float colliderRadius = collector.Cache.GetComponent<CircleCollider2D>().radius;
            float start = Time.time;
            double wallStart = EditorApplication.timeSinceStartup;
            bool background = Application.runInBackground; Application.runInBackground = true;
            int frame = 0; bool moved = false, trailSeen = false, intakeSeen = false;
            string prefix = openUi ? "OpenUiIntake" : discarded ? "Discard" : lootCount > 1 ? "IntakeBurst" : "LootFlight";
            if (quality > 0) prefix = (quality == 2 ? "Unique" : "Rare") + (discarded ? "Discard" : lootCount > 1 ? "Burst" : "Arrival");
            int peakStrength = 0, peakWaves = 0;
            float peakLight = 0;
            bool riseSeen = false, revealSeen = false, travelSeen = false, bubbleSeen = false;
            WindowView ownedWindow = null;
            bool uiOpened = false, uiVerified = false;
            double uiStart = 0;
            void Tick()
            {
                try
                {
                    Require(EditorApplication.isPlaying && instance != null, "Flight check interrupted.");
                    Require(EditorApplication.timeSinceStartup - wallStart < 15, "Flight check timed out.");
                    EditorApplication.QueuePlayerLoopUpdate();
                    float age = Time.time - start;
                    if (openUi && !uiOpened && age >= .12f)
                    {
                        var toggle = Object.FindFirstObjectByType<StashPanelToggle>(FindObjectsInactive.Include);
                        Require(toggle != null, "Missing real cache UI toggle.");
                        toggle.OpenLootCache(collector.Cache); ownedWindow = manager.TopWindow;
                        Require(ownedWindow != null, "Cache UI must open while flights are running.");
                        collector.TryStore(new InventoryItem(data), source); lootCount++;
                        Require(collector.Cache.Count == lootCount && manager.TopWindow == ownedWindow, "Simultaneous loot must remain available in the open cache.");
                        ScreenCapture.CaptureScreenshot(Path.Combine(folder, "OpenUiGrid.png"));
                        uiOpened = true; uiStart = EditorApplication.timeSinceStartup;
                    }
                    if (ownedWindow != null)
                    {
                        Require(Mathf.Approximately(Time.timeScale, 0f) && collector.Cache.Count == lootCount, "Existing cache UI pause/storage rules must remain intact.");
                        if (EditorApplication.timeSinceStartup - uiStart < .25) return;
                        manager.CloseWindow(ownedWindow); ownedWindow = null; uiVerified = true;
                    }
                    var flight = room.GetComponentInChildren<WorldLootCacheFlightVisual>();
                    var visual = collector.Cache.transform.Find("Visual");
                    intakeSeen |= visual.localScale.y < 0.98f;
                    peakStrength = Mathf.Max(peakStrength, collector.Cache.ValuableArrivalStrength);
                    var valuable = collector.Cache.transform.Find("ValuableArrival");
                    if (valuable != null)
                    {
                        int visibleWaves = valuable.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name.StartsWith("WaveAndStars") && r.enabled);
                        peakWaves = Mathf.Max(peakWaves, visibleWaves);
                        var light = valuable.GetComponentInChildren<LocalLight2DSource>();
                        if (light != null) peakLight = Mathf.Max(peakLight, new SerializedObject(light).FindProperty("_strength").floatValue);
                    }
                    Require(collector.Cache.transform.position == cachePosition && collector.Cache.transform.localScale == Vector3.one &&
                        collector.Cache.GetComponent<CircleCollider2D>().radius == colliderRadius, "Intake must not move or resize interaction.");
                    if (flight != null)
                    {
                        var icon = flight.transform.Find("Icon")?.GetComponent<SpriteRenderer>();
                        bubbleSeen |= flight.GetComponent<SpriteRenderer>().enabled && icon != null && icon.enabled && icon.sprite != null;
                        moved |= Vector3.Distance(flight.transform.position, source) > 0.1f;
                        trailSeen |= flight.GetComponent<TrailRenderer>().positionCount > 1;
                        riseSeen |= flight.transform.position.y - source.y > 2.5f;
                        revealSeen |= flight.transform.Find("RarityReveal").GetComponent<SpriteRenderer>().enabled;
                        travelSeen |= age > WorldLootCacheFlightVisual.TravelStart + .1f &&
                            Vector3.Distance(flight.transform.position, source + Vector3.up * WorldLootCacheFlightVisual.RiseHeight) > .2f;
                    }
                    if (frame < Mathf.CeilToInt(checkDuration / .06f) && age >= 0.02f + frame * 0.06f)
                    {
                        Render(collector.Cache, portal, Path.Combine(folder, $"{prefix}-{frame:D2}.png"), quality > 0); frame++;
                    }
                    if (age < checkDuration) return;
                    if (!discarded) Require(moved && trailSeen && intakeSeen && riseSeen && revealSeen && travelSeen && bubbleSeen, "An item bubble must rise, reveal rarity, travel with a trail and trigger intake on arrival.");
                    else Require(!moved && !trailSeen && !intakeSeen, "A player discard must not create flight or intake feedback.");
                    Require(flight == null, "Flight must clean itself up after reaching the cache.");
                    Require(visual.localScale == Vector3.one, "Burst feedback must settle without a queue of twenty animations.");
                    Require(collector.Cache.Count == lootCount, "Presentation must not change stored item ownership.");
                    if (openUi) Require(uiVerified, "Open-cache simultaneous arrival case was not exercised.");
                    if (quality > 0)
                    {
                        int expected = discarded ? 0 : quality;
                        Require(peakStrength == expected && peakWaves == expected, "Rare must have one wave; unique two; discards none.");
                        Require(collector.Cache.ValuableArrivalStrength == 0, "Rarity effects must finish without queued waves.");
                    }
                    Require(room.GetComponentsInChildren<WorldDroppedItem>(true).Length == 0, "No loose item objects allowed.");
                    File.WriteAllText(Path.Combine(folder, prefix + "Report.txt"), $"PASS: {lootCount} items stored immediately; discarded={discarded}; bubble={bubbleSeen}; rise={riseSeen}; reveal={revealSeen}; travel={travelSeen}; intake={intakeSeen}; flight={moved}; trail={trailSeen}; openUI={uiVerified}; fullInventoryChecked={lootCount == 20}; rarityStrength={peakStrength}; waves={peakWaves}; peakLight={peakLight}; fixed interaction; settled pulse and automatic cleanup after real frames.");
                    Debug.Log("Room loot flight Play Mode check passed.");
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Path.Combine(folder, prefix + "Report.txt"), "FAIL: " + ex.Message); Debug.LogException(ex);
                }
                EditorApplication.update -= Tick;
                if (ownedWindow != null && manager != null) manager.CloseWindow(ownedWindow);
                Application.runInBackground = background;
                if (instance != null) Object.Destroy(instance);
                if (quality > 0) Object.Destroy(data);
                if (fixtureAffix != null) Object.Destroy(fixtureAffix);
                if (hadFilter) PlayerPrefs.SetInt(LootFilterSettings.PlayerPrefsKey, filter);
                else PlayerPrefs.DeleteKey(LootFilterSettings.PlayerPrefsKey);
            }
            EditorApplication.update += Tick; EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void VerifyFullInventory(WorldLootCache cache)
        {
            // One synchronous check with a disposable inventory. Never fill the
            // user's inventory, or leave the singleton swapped across a game frame.
            var singleton = typeof(InventoryManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = InventoryManager.Instance;
            var oldBanner = Object.FindFirstObjectByType<PlayerNoticeBanner>();
            var host = new GameObject("Temporary full-inventory check");
            var filler = ScriptableObject.CreateInstance<ArmorItemSO>();
            filler.ID = "intake_full_inventory_filler"; filler.Width = filler.Height = 1;
            try
            {
                singleton.SetValue(null, null);
                var inventory = host.AddComponent<InventoryManager>();
                for (int i = 0; i < inventory.EquipmentItems.Length; i++) inventory.EquipmentItems[i] = new InventoryItem(filler);
                for (int i = 0; i < inventory.BackpackSlotCount; i++)
                    Require(inventory.AddItem(new InventoryItem(filler)), "Could not fill disposable backpack.");
                int before = cache.Count; var item = cache.Items[0];
                Require(!cache.TryTake(item), "Full inventory must reject pickup.");
                Require(cache.Count == before && cache.Items.Contains(item) && cache.CanInteract(), "Rejected pickup must keep the same loot accessible.");
            }
            finally
            {
                Object.DestroyImmediate(host); singleton.SetValue(null, previous);
                Object.DestroyImmediate(filler);
                var banner = Object.FindFirstObjectByType<PlayerNoticeBanner>();
                if (oldBanner == null && banner != null) Object.DestroyImmediate(banner.gameObject);
            }
        }

        private static void Render(WorldLootCache cache, DungeonPortal portal, string path, bool lighting = false)
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
                if (lighting && Camera.main != null && Camera.main.TryGetComponent<AtmosphereController>(out var mainOwner))
                {
                    var owner = camera.gameObject.AddComponent<AtmosphereController>();
                    EditorUtility.CopySerialized(mainOwner, owner); owner.Profile = null;
                    owner.ApplyDungeonPresentation(null, AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>("Assets/Visuals/RelicFX/MortfallDepthMist.asset"));
                    owner.RefreshColorGroups();
                }
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
