using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Enemies;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class ArrowArtilleryPreview
    {
        public static void Capture()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            Directory.CreateDirectory("Temp/ArrowArtilleryPreview");
            File.WriteAllText("Temp/ArrowArtilleryPreview/Report.txt", "Running");
            var check = new GameObject("Temporary Arrow Artillery verification").AddComponent<ArrowArtilleryPreviewRunner>();
            check.StartCoroutine(check.Run());
        }
    }

    public sealed class ArrowArtilleryPreviewRunner : MonoBehaviour
    {
        public IEnumerator Run()
        {
            var materials = new List<Material>();
            var target = new RenderTexture(480, 270, 24);
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            PlayerStats stats = null;
            var previous = RenderTexture.active;
            try
            {
                var live = Object.FindFirstObjectByType<PlayerStats>();
                var sprite = live.GetComponentsInChildren<SpriteRenderer>().Where(s => s.sprite != null).OrderByDescending(s => s.sprite.bounds.size.y).First().sprite;
                Object.Instantiate(Resources.Load<GameObject>("Prefabs/Dungeon/MineDungeon/MortfallRoom_001"), new Vector3(2400, 0, 0), Quaternion.identity, transform);
                Physics2D.SyncTransforms();
                var ground = Physics2D.Raycast(new Vector2(2396, 50), Vector2.down, 100, 1 << 6);
                Vector2 center = new Vector2(2396, ground.collider != null ? ground.point.y + .5f : 2);
                var owner = new GameObject("Artillery caster"); owner.transform.SetParent(transform); owner.transform.position = center;
                stats = owner.AddComponent<PlayerStats>(); stats.enabled = false;
                stats.GetStat(StatType.MaxHealth).BaseValue = 100; stats.Health.RestoreFull();
                stats.GetStat(StatType.MaxMana).BaseValue = 100; stats.Mana.RestoreFull();
                stats.GetStat(StatType.DamagePhysical).BaseValue = 40; stats.GetStat(StatType.CritChance).BaseValue = 0;
                owner.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
                var rb = owner.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic; rb.gravityScale = 0;
                var movement = owner.AddComponent<PlayerMovement>(); movement.enabled = false;
                var visuals = new GameObject("Visuals"); visuals.transform.SetParent(owner.transform, false);
                AddSprite(visuals, sprite, center.y, 0).transform.localPosition = -sprite.bounds.center;
                WeaponStancePoseTableSO.LoadDefault().TryGetById("bow_stance", out var pose);
                var pivot = new GameObject("HandPivot"); pivot.transform.SetParent(visuals.transform, false);
                var holder = new GameObject("WeaponHolder"); holder.transform.SetParent(pivot.transform, false);
                holder.transform.localPosition = pose.LocalPosition; holder.transform.localRotation = Quaternion.Euler(0, 0, pose.LocalEulerZ);
                AddSprite(holder, Resources.Load<WeaponItemSO>("Items/2HWeapon/Bow/Adventurer's Bow").InHandSprite, center.y, 1);
                var runnerHost = new GameObject("Artillery skill"); runnerHost.transform.SetParent(transform);
                var runner = runnerHost.AddComponent<SkillStepRunner>(); runner.SetRuntimeSlot(null, 1);
                runner.Initialize(stats, AssetDatabase.LoadAssetAtPath<SkillDataSO>(ArrowArtilleryBuilder.SkillPath));
                var enemies = new EnemyHealth[2]; var before = new float[2];
                var data = Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
                for (int i = 0; i < 2; i++)
                {
                    var mold = Object.Instantiate(data.Prefab, transform); mold.transform.position = center + new Vector2(i == 0 ? -2 : 2, 0);
                    mold.Setup(data, 60); mold.GetComponent<EnemyLocomotion2D>().enabled = false; mold.GetComponent<EnemyAttackController>().enabled = false;
                    var body = mold.GetComponent<Rigidbody2D>(); if (body != null) { body.bodyType = RigidbodyType2D.Kinematic; body.linearVelocity = Vector2.zero; }
                    enemies[i] = mold.GetComponent<EnemyHealth>(); before[i] = enemies[i].CurrentHealth;
                }
                var camera = new GameObject("Artillery camera").AddComponent<Camera>(); camera.transform.SetParent(transform);
                camera.orthographic = true; camera.orthographicSize = 5.625f; camera.aspect = 480f / 270;
                camera.transform.position = (Vector3)center + new Vector3(0, 1.5f, -10); camera.enabled = false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false; camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                target.Create(); Physics2D.SyncTransforms(); float started = Time.time; runner.TryCast();
                if (stats.Mana.Current != 80 || runner.CooldownRemaining < 7.9f) throw new Exception("Artillery mana/cooldown failed.");
                foreach (float captureTime in new[] { .15f, .5f, .75f, 1f, 2.3f })
                {
                    while (Time.time - started < captureTime) yield return null;
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Temp/ArrowArtilleryPreview/Artillery-{captureTime:0.00}.png", image.EncodeToPNG());
                }
                for (int i = 0; i < 2; i++) if (enemies[i].CurrentHealth >= before[i]) throw new Exception("Arrow rain missed a nearby Mold.");
                File.WriteAllText("Temp/ArrowArtilleryPreview/Report.txt", $"Real-time TryCast with bow draw and projectile collision: 20 mana, 8s CD; Mold HP {before[0]} -> {enemies[0].CurrentHealth}, {before[1]} -> {enemies[1].CurrentHealth}.");
                runner.Cancel();

                SpriteRenderer AddSprite(GameObject go, Sprite art, float y, int offset)
                {
                    var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = art;
                    var material = new Material(Resources.Load<Material>("Skills/1HWeapon/Wand/ElectricBeam")); materials.Add(material); renderer.sharedMaterial = material;
                    renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.Player);
                    renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.Player, y, offset); return renderer;
                }
            }
            finally
            {
                if (stats != null) SkillProjectile.DespawnAllForOwner(stats, false);
                foreach (var barrage in Object.FindObjectsByType<ArrowArtilleryBarrage>(FindObjectsSortMode.None))
                    if (barrage.transform.position.x > 2300) Object.Destroy(barrage.gameObject);
                RenderTexture.active = previous; target.Release(); Object.Destroy(target); Object.Destroy(image);
                foreach (var material in materials) Object.Destroy(material);
                Object.Destroy(gameObject);
            }
        }
    }
}
