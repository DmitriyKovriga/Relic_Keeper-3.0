using Scripts.Hub;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.Hub
{
    public static class MarketNpcAnimationBuilder
    {
        public const string ArtPath = Hub20Builder.ArtRoot + "/Characters/MarketNpcIdle.png";
        public const string ClipPath = Hub20Builder.ArtRoot + "/Characters/MarketNpcIdle.anim";
        public const string ControllerPath = Hub20Builder.ArtRoot + "/Characters/MarketNpc.controller";
        public const string PrefabPath = "Assets/Prefabs/Spawner/MarketNPC.prefab";
        public const int FrameCount = 5;
        public const float FrameDuration = 0.3f;
        public const int FrameSize = 28;

        [MenuItem("Tools/Hub/Apply Market NPC Animation")]
        public static void Apply()
        {
            TavernNpcAnimationBuilder.ApplyIdle(ArtPath, ClipPath, ControllerPath, PrefabPath,
                "MarketNpcIdle", FrameCount, FrameDuration, FrameSize, new Vector2(0.5f, 0.5f), 0.5f);
        }

        public static void CaptureRuntimeViews()
        {
            WarehouseNpcAnimationBuilder.CaptureRuntimeViews(HubService.Market, FrameCount, FrameDuration, "codex-market-npc");
        }
    }
}
