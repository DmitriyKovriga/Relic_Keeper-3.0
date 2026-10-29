using Scripts.Hub;
using Scripts.Items.World;
using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Dungeon
{
    /// <summary>Creates temporary stash and market access points owned by the current room.</summary>
    public static class DungeonRoomServiceSpawner
    {
        public static HubServiceNpc SpawnStash(Vector2 position, Transform roomParent, GameObject prefab)
        {
            return Spawn("DungeonStashNPC", HubService.Stash, position, roomParent, prefab);
        }

        public static HubServiceNpc SpawnMerchant(Vector2 position, Transform roomParent, GameObject prefab)
        {
            var merchant = Spawn("DungeonMerchantNPC", HubService.Market, position, roomParent, prefab);
            if (merchant != null && Application.isPlaying)
                Scripts.Economy.MarketManager.EnsureInstance().RefreshStock();
            return merchant;
        }

        private static HubServiceNpc Spawn(
            string objectName,
            HubService service,
            Vector2 position,
            Transform parent,
            GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError($"[DungeonRoomServiceSpawner] Cannot spawn {service}: service prefab is not assigned.");
                return null;
            }

            GameObject host = Object.Instantiate(prefab, parent);
            host.name = objectName;
            var settings = Resources.Load<DungeonServiceArrivalSettings>(DungeonServiceArrivalSettings.ResourcePath);
            Vector2 ground = WorldItemDropService.TrySnapToGround(position, out Vector2 surface, 0) ? surface : position;
            host.transform.position = ground + Vector2.up * (settings != null ? settings.NpcRootHeight : 0.5f);

            HubServiceNpc interactable = host.GetComponent<HubServiceNpc>();
            if (interactable == null)
            {
                Debug.LogError($"[DungeonRoomServiceSpawner] Prefab '{prefab.name}' has no {nameof(HubServiceNpc)} component.");
                Object.Destroy(host);
                return null;
            }

            if (interactable.Service != service)
                interactable.ConfigureRuntimeService(service);

            WorldRenderSorting.ConfigureSorter(
                host,
                RenderDepthCategory.Enemy,
                host.transform.position.y,
                localOffset: 2,
                staticAnchor: true);
            if (settings != null)
            {
                var renderer = host.GetComponent<SpriteRenderer>();
                if (renderer != null && settings.SpriteMaterial != null) renderer.sharedMaterial = settings.SpriteMaterial;
                if (renderer != null && settings.PortalSprite != null)
                    host.AddComponent<DungeonServiceArrival>().Initialize(interactable, settings, host.transform.position);
            }
            return interactable;
        }
    }
}
