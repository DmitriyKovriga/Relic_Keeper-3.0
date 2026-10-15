using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Visuals;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class PlasmaFieldPreview
    {
        public static void Capture()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = new GameObject("Temporary Plasma Field verification");
            var camera = new GameObject("Plasma preview camera").AddComponent<Camera>();
            var target = new RenderTexture(480, 270, 24);
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            SkillStepRunner runner = null;
            try
            {
                Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab"), new Vector3(2400, 0, 0), Quaternion.identity, root.transform);
                Physics2D.SyncTransforms();
                var ground = Physics2D.Raycast(new Vector2(2396, 50), Vector2.down, 100, 1 << 6);
                Vector2 center = new Vector2(2396, ground.collider != null ? ground.point.y + .75f : 2);
                var owner = new GameObject("Plasma preview caster"); owner.SetActive(false); owner.transform.SetParent(root.transform); owner.transform.position = center;
                var stats = owner.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", flags).Invoke(stats, null);
                stats.GetStat(StatType.DamagePhysical).BaseValue = 8; stats.GetStat(StatType.CritChance).BaseValue = 0;
                var body = new GameObject("Caster visual").AddComponent<SpriteRenderer>(); body.transform.SetParent(root.transform);
                var live = Object.FindFirstObjectByType<PlayerStats>();
                body.sprite = live != null ? live.GetComponentsInChildren<SpriteRenderer>().Where(s => s.sprite != null).OrderByDescending(s => s.sprite.bounds.size.y).First().sprite
                    : Resources.LoadAll<Sprite>("Heroes/WarriorResources/Warrioir_v0.1").First();
                body.transform.position = (Vector3)center - body.sprite.bounds.center;
                body.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.Player);
                body.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.Player, center.y);
                var data = Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
                var enemies = new EnemyHealth[2]; var before = new float[2];
                for (int i = 0; i < 2; i++)
                {
                    var enemy = Object.Instantiate(data.Prefab, root.transform);
                    enemy.transform.position = center + new Vector2(i == 0 ? -2.5f : 3.3f, -.5f);
                    enemy.Setup(data, 60); enemies[i] = enemy.GetComponent<EnemyHealth>(); before[i] = enemies[i].CurrentHealth;
                }
                Physics2D.SyncTransforms();
                runner = owner.AddComponent<SkillStepRunner>(); var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(PlasmaFieldBuilder.SkillPath);
                typeof(SkillBehaviour).GetField("_ownerStats", flags).SetValue(runner, stats);
                typeof(SkillBehaviour).GetField("_data", flags).SetValue(runner, skill);
                typeof(SkillStepRunner).GetField("_ctx", flags).SetValue(runner, new SkillStepContext { OwnerStats = stats, AoeScale = 1 });
                camera.orthographic = true; camera.orthographicSize = 5.625f; camera.aspect = 480f / 270;
                camera.transform.position = (Vector3)center + new Vector3(0, 0, -10);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                target.Create(); Directory.CreateDirectory("Temp/PlasmaFieldPreview");
                var apex = new float[2];
                for (int frame = 0; frame <= 20; frame++)
                {
                    typeof(SkillStepRunner).GetMethod("ExecutePlasmaField", flags).Invoke(runner, new object[] { 0, skill.Recipe.Steps[0], frame / 20f });
                    if (frame == 10) for (int i = 0; i < 2; i++) apex[i] = enemies[i].CurrentHealth;
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Temp/PlasmaFieldPreview/Plasma-{frame:D2}.png", image.EncodeToPNG());
                }
                string report = "Live recipe, native 480x270, outward + returning field on real Molds:\n";
                for (int i = 0; i < 2; i++)
                {
                    report += $"Mold {i}: {before[i]} => apex {apex[i]} => return {enemies[i].CurrentHealth}\n";
                    if (apex[i] >= before[i] || enemies[i].CurrentHealth >= apex[i]) throw new InvalidOperationException("A real Mold did not receive both passes.");
                }
                File.WriteAllText("Temp/PlasmaFieldPreview/Report.txt", report);
            }
            finally
            {
                if (runner != null) typeof(SkillStepRunner).GetMethod("Cleanup", flags).Invoke(runner, null);
                RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(root); Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
