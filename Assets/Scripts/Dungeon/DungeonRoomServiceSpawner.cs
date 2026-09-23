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
            return Spawn("DungeonMerchantNPC", HubService.Market, position, roomParent, prefab);
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
            host.transform.position = WorldItemDropService.ProjectToGroundUnder(position);

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
                RenderDepthCategory.Environment,
                host.transform.position.y,
                localOffset: 2,
                staticAnchor: true);
            return interactable;
        }
    }
}
