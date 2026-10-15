using System;
using System.IO;
using System.Linq;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class WinterStormPreview
    {
        public static void Capture()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var root = new GameObject("Temporary Winter Storm verification");
            var target = new RenderTexture(480, 270, 24);
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            Material bodyMaterial = null;
            PlayerStasisController stasis = null;
            PlayerWinterStormController storm = null;
            try
            {
                var live = Object.FindFirstObjectByType<PlayerStats>();
                var sprite = live.GetComponentsInChildren<SpriteRenderer>().Where(s => s.sprite != null).OrderByDescending(s => s.sprite.bounds.size.y).First().sprite;
                Object.Instantiate(Resources.Load<GameObject>("Prefabs/Dungeon/MineDungeon/MortfallRoom_001"), new Vector3(2400, 0, 0), Quaternion.identity, root.transform);
                Physics2D.SyncTransforms();
                var ground = Physics2D.Raycast(new Vector2(2396, 50), Vector2.down, 100, 1 << 6);
                Vector2 center = new Vector2(2396, ground.collider != null ? ground.point.y + .5f : 2);
                var owner = new GameObject("Frost caster"); owner.transform.SetParent(root.transform); owner.transform.position = center;
                var stats = owner.AddComponent<PlayerStats>();
                stats.GetStat(StatType.MaxHealth).BaseValue = 100; stats.Health.SetCurrent(10);
                stats.GetStat(StatType.MaxMana).BaseValue = 200; stats.Mana.RestoreFull();
                stats.GetStat(StatType.DamagePhysical).BaseValue = 40; stats.GetStat(StatType.CritChance).BaseValue = 0;
                var collider = owner.AddComponent<BoxCollider2D>(); collider.size = new Vector2(.5f, 1);
                var body = owner.AddComponent<Rigidbody2D>(); body.gravityScale = 2.5f; body.constraints = RigidbodyConstraints2D.FreezeRotation;
                var movement = owner.AddComponent<PlayerMovement>(); movement.enabled = false;
                var visual = new GameObject("Hero sprite").AddComponent<SpriteRenderer>(); visual.transform.SetParent(owner.transform, false);
                visual.sprite = sprite; visual.transform.localPosition = -sprite.bounds.center;
                visual.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.Player);
                visual.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.Player, center.y);
                bodyMaterial = new Material(Resources.Load<Material>("Skills/1HWeapon/Wand/ElectricBeam")); visual.sharedMaterial = bodyMaterial;
                var receiver = owner.AddComponent<PlayerDamageReceiver>();
                var runnerHost = new GameObject("Frost skill runner"); runnerHost.transform.SetParent(root.transform);
                var runner = runnerHost.AddComponent<SkillStepRunner>(); runner.Initialize(stats, AssetDatabase.LoadAssetAtPath<SkillDataSO>(WinterStormBuilder.SkillPath));
                var camera = new GameObject("Winter Storm preview camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                camera.orthographic = true; camera.orthographicSize = 5.625f; camera.aspect = 480f / 270;
                camera.transform.position = (Vector3)center + new Vector3(0, 1, -10);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                target.Create(); Directory.CreateDirectory("Temp/WinterStormPreview");
                var molds = new EnemyHealth[2]; var before = new float[2];
                var moldData = Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
                for (int i = 0; i < 2; i++)
                {
                    var mold = Object.Instantiate(moldData.Prefab, root.transform);
                    mold.transform.position = center + new Vector2(i == 0 ? -2.2f : 2.4f, 0);
                    mold.Setup(moldData, 60); molds[i] = mold.GetComponent<EnemyHealth>(); before[i] = molds[i].CurrentHealth;
                }
                Physics2D.SyncTransforms(); runner.TryCast();
                storm = owner.GetComponent<PlayerWinterStormController>();
                if (storm == null || !storm.IsActive || stats.Mana.Current != 150 || runner.CooldownRemaining < 14.9f)
                    throw new Exception("Storm cast/mana/cooldown failed.");
                storm.Tick(1); Save("Storm");
                var frostHost = new GameObject("Concurrent Anabiosis"); frostHost.transform.SetParent(root.transform);
                var frost = frostHost.AddComponent<SkillStepRunner>(); frost.Initialize(stats, AssetDatabase.LoadAssetAtPath<SkillDataSO>(FrostAnabiosisBuilder.SkillPath));
                frost.TryCast(); stasis = owner.GetComponent<PlayerStasisController>();
                if (stasis == null || !stasis.IsActive || stats.Mana.Current != 100 || !storm.IsActive)
                    throw new Exception("Storm + Anabiosis simultaneous casting failed.");
                if (!receiver.TakeDamageDetailed(new DamageSnapshot(owner) { Physical = 100 }).WasImmune)
                    throw new Exception("Anabiosis was not immune during storm.");
                float hpAfterFirstSecond = molds[0].CurrentHealth;
                storm.Tick(1); stasis.Tick(1); Save("StormAndAnabiosis");
                if (molds[0].CurrentHealth >= hpAfterFirstSecond) throw new Exception("Storm stopped damaging during Anabiosis.");
                storm.Tick(3); stasis.Tick(3);
                if (storm.IsActive || !stasis.IsActive || storm.PulseCount != 10 || body.constraints != RigidbodyConstraints2D.FreezeAll)
                    throw new Exception("Overlapping expiry failed.");
                stasis.Tick(1);
                if (stasis.IsActive || body.constraints != RigidbodyConstraints2D.FreezeRotation || body.gravityScale != 2.5f)
                    throw new Exception("Controls were not restored after both skills.");
                File.WriteAllText("Temp/WinterStormPreview/Report.txt", $"Play Mode: Storm -> Anabiosis succeeded; mana 200 -> 150 -> 100; both CD 15s; 10 cold pulses over 5s; Mold HP {before[0]} -> {molds[0].CurrentHealth}, {before[1]} -> {molds[1].CurrentHealth}; damage continued inside crystal; independent expiry and original gravity/constraints restored.");
                runner.Cancel(); frost.Cancel();

                void Save(string name)
                {
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Temp/WinterStormPreview/{name}.png", image.EncodeToPNG());
                }
            }
            finally
            {
                if (stasis != null) stasis.End();
                if (storm != null) storm.End();
                foreach (var effect in Object.FindObjectsByType<FrostAnabiosisVisual>(FindObjectsSortMode.None))
                    if (effect.transform.position.x > 2300) Object.DestroyImmediate(effect.gameObject);
                foreach (var effect in Object.FindObjectsByType<WinterStormVisual>(FindObjectsSortMode.None))
                    if (effect.transform.position.x > 2300) Object.DestroyImmediate(effect.gameObject);
                RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(root);
                Object.DestroyImmediate(target); Object.DestroyImmediate(image); if (bodyMaterial != null) Object.DestroyImmediate(bodyMaterial);
            }
        }
    }
}
