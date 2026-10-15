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
    public static class FrostAnabiosisPreview
    {
        public static void Capture()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var root = new GameObject("Temporary Frost Anabiosis verification");
            var target = new RenderTexture(480, 270, 24);
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            Material bodyMaterial = null;
            PlayerStasisController stasis = null;
            FrostAnabiosisVisual crystal = null;
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
                stats.GetStat(StatType.MaxMana).BaseValue = 100; stats.Mana.RestoreFull();
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
                var runner = runnerHost.AddComponent<SkillStepRunner>(); runner.Initialize(stats, AssetDatabase.LoadAssetAtPath<SkillDataSO>(FrostAnabiosisBuilder.SkillPath));
                var camera = new GameObject("Frost preview camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                camera.orthographic = true; camera.orthographicSize = 5.625f; camera.aspect = 480f / 270;
                camera.transform.position = (Vector3)center + new Vector3(0, 1, -10);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                target.Create(); Directory.CreateDirectory("Temp/FrostAnabiosisPreview");
                Physics2D.SyncTransforms(); runner.TryCast();
                stasis = owner.GetComponent<PlayerStasisController>();
                if (stasis == null || !stasis.IsActive || stats.Mana.Current != 50 || runner.CooldownRemaining < 14.9f)
                    throw new Exception("Frost cast/mana/cooldown failed.");
                crystal = owner.GetComponentInChildren<FrostAnabiosisVisual>();
                var damage = new DamageSnapshot(owner) { Physical = 100, Fire = 100, Cold = 100, Lightning = 100 };
                if (!receiver.TakeDamageDetailed(damage).WasImmune) throw new Exception("Crystal was not immune.");
                receiver.ApplyPureDamage(100, owner, "Ignite");
                if (stats.Health.Current != 10) throw new Exception("Damage over time bypassed crystal.");
                stasis.Tick(1); Save("Crystal");
                if (stats.Health.Current != 20 || body.constraints != RigidbodyConstraints2D.FreezeAll) throw new Exception("Healing/immobility failed.");
                runner.Cancel();
                if (!stasis.IsActive) throw new Exception("Cancelling the source removed stasis.");
                stasis.Tick(4);
                if (stasis.IsActive || stats.Health.Current != 60 || body.constraints != RigidbodyConstraints2D.FreezeRotation || body.gravityScale != 2.5f)
                    throw new Exception("Stasis expiry/physics restoration failed.");
                crystal.SampleRelease(.5f);
                Save("Release");
                File.WriteAllText("Temp/FrostAnabiosisPreview/Report.txt", "Play Mode TryCast: 50 mana spent, 15s cooldown; hit and Ignite immunity; HP 10 -> 20 at 1s -> 60 at 5s; source cancellation preserved crystal; original gravity/constraints restored.");

                void Save(string name)
                {
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Temp/FrostAnabiosisPreview/{name}.png", image.EncodeToPNG());
                }
            }
            finally
            {
                if (stasis != null) stasis.End();
                if (crystal != null) Object.DestroyImmediate(crystal.gameObject);
                RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(root);
                Object.DestroyImmediate(target); Object.DestroyImmediate(image); if (bodyMaterial != null) Object.DestroyImmediate(bodyMaterial);
            }
        }
    }
}
