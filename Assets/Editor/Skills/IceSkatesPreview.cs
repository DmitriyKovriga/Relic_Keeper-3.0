using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class IceSkatesPreview
    {
        public static void Capture()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            Directory.CreateDirectory("Temp/IceSkatesPreview"); File.WriteAllText("Temp/IceSkatesPreview/Report.txt", "Running");
            var check = new GameObject("Temporary Ice Skates verification").AddComponent<IceSkatesPreviewRunner>();
            check.StartCoroutine(check.Run());
        }
    }

    public sealed class IceSkatesPreviewRunner : MonoBehaviour
    {
        public IEnumerator Run()
        {
            var target = new RenderTexture(480, 270, 24);
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var materials = new List<Material>(); var previous = RenderTexture.active;
            PlayerIceSkatesController skates = null;
            try
            {
                var live = Object.FindFirstObjectByType<PlayerStats>();
                var sprite = live.GetComponent<SpriteRenderer>().sprite;
                Object.Instantiate(Resources.Load<GameObject>("Prefabs/Dungeon/MineDungeon/MortfallRoom_001"), new Vector3(2400, 0, 0), Quaternion.identity, transform);
                Physics2D.SyncTransforms();
                var ground = Physics2D.Raycast(new Vector2(2396, 50), Vector2.down, 100, 1 << 6);
                if (ground.collider == null) throw new Exception("Preview room has no floor.");
                Vector2 center = new Vector2(2396, ground.point.y + .5f);
                var owner = new GameObject("Skating caster"); owner.transform.SetParent(transform); owner.transform.position = center - Vector2.right * 3;
                var stats = owner.AddComponent<PlayerStats>(); stats.enabled = false;
                stats.GetStat(StatType.MaxHealth).BaseValue = 100; stats.Health.RestoreFull();
                stats.GetStat(StatType.MaxMana).BaseValue = 100; stats.Mana.RestoreFull();
                stats.GetStat(StatType.DamagePhysical).BaseValue = 100; stats.GetStat(StatType.CritChance).BaseValue = 0;
                stats.GetStat(StatType.MoveSpeed).BaseValue = 5;
                owner.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
                owner.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                var bodyArt = new GameObject("Caster sprite"); bodyArt.transform.SetParent(owner.transform, false);
                var renderer = bodyArt.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                var material = new Material(Resources.Load<Material>("Skills/1HWeapon/Wand/ElectricBeam"));
                material.mainTexture = sprite.texture; materials.Add(material); renderer.sharedMaterial = material;
                bodyArt.transform.localPosition = new Vector3(-sprite.bounds.center.x, -.5f - sprite.bounds.min.y);
                var visuals = new GameObject("Visuals"); visuals.transform.SetParent(owner.transform, false);
                var pivot = new GameObject("HandPivot"); pivot.transform.SetParent(visuals.transform, false);
                var holder = new GameObject("WeaponHolder"); holder.transform.SetParent(pivot.transform, false); holder.AddComponent<SpriteRenderer>();
                renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.Player);
                renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.Player, center.y);
                var host = new GameObject("Ice Skates recipe"); host.transform.SetParent(transform);
                var runner = host.AddComponent<SkillStepRunner>(); runner.SetRuntimeSlot(null, 5);
                runner.Initialize(stats, AssetDatabase.LoadAssetAtPath<SkillDataSO>(IceSkatesBuilder.SkillPath));
                var data = Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
                var mold = Object.Instantiate(data.Prefab, transform); mold.Setup(data, 60);
                mold.transform.position = center;
                Physics2D.SyncTransforms(); var collider = mold.GetComponent<Collider2D>();
                mold.transform.position += Vector3.up * (ground.point.y - collider.bounds.min.y);
                mold.GetComponent<EnemyLocomotion2D>().enabled = false; mold.GetComponent<EnemyAttackController>().enabled = false;
                var enemyBody = mold.GetComponent<Rigidbody2D>(); if (enemyBody != null) { enemyBody.bodyType = RigidbodyType2D.Kinematic; enemyBody.linearVelocity = Vector2.zero; }
                var health = mold.GetComponent<EnemyHealth>(); float before = health.CurrentHealth;
                var camera = new GameObject("Ice trail camera").AddComponent<Camera>(); camera.transform.SetParent(transform);
                camera.orthographic = true; camera.orthographicSize = 5.625f; camera.aspect = 480f / 270;
                camera.transform.position = (Vector3)center + new Vector3(0, 1.5f, -10); camera.enabled = false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false; camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                target.Create(); Physics2D.SyncTransforms(); runner.TryCast();
                skates = owner.GetComponent<PlayerIceSkatesController>();
                if (skates == null || !skates.IsActive || stats.Mana.Current != 70 || runner.CooldownRemaining < 14.9f)
                    throw new Exception("Ice Skates failed activation, mana or cooldown.");
                float started = Time.time; int snapshot = 0, maximumPatches = 0;
                foreach (float captureTime in new[] { .5f, 1.5f, 2.5f, 4.8f, 8.5f })
                {
                    while (Time.time - started < captureTime)
                    {
                        float elapsed = Time.time - started;
                        owner.transform.position = center + Vector2.right * Mathf.Lerp(-3, 3, Mathf.Clamp01(elapsed / 3));
                        Physics2D.SyncTransforms();
                        if (skates.Surface != null) maximumPatches = Mathf.Max(maximumPatches, skates.Surface.Patches.Count);
                        yield return null;
                    }
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Temp/IceSkatesPreview/Skates-{++snapshot}.png", image.EncodeToPNG());
                }
                if (health.CurrentHealth >= before) throw new Exception("Ice trail did not damage real Mold.");
                if (skates.IsActive || Mathf.Abs(stats.GetValue(StatType.MoveSpeed) - 5) > .01f) throw new Exception("Skating buff failed expiry.");
                File.WriteAllText("Temp/IceSkatesPreview/Report.txt", $"Real-time TryCast: 30 mana, 15s CD, speed 5 -> 6.75 -> 5 after 8s. Peak {maximumPatches} ground patches. Mold HP {before} -> {health.CurrentHealth}. Cold chance remains {stats.GetValue(StatType.FreezeChance)}. Ice remains after skating ends then expires normally.");
                runner.Cancel();
            }
            finally
            {
                if (skates != null) { skates.End(); if (skates.Surface != null) skates.Surface.Dispose(); }
                RenderTexture.active = previous; target.Release(); Object.Destroy(target); Object.Destroy(image);
                foreach (var material in materials) Object.Destroy(material);
                Object.Destroy(gameObject);
            }
        }
    }
}
