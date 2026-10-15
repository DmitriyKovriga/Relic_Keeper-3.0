using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class ElectricBeamPreview
    {
        public static void Capture()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = new GameObject("Temporary electric beam verification");
            var camera = new GameObject("Beam preview camera").AddComponent<Camera>();
            var renderTarget = new RenderTexture(480, 270, 24);
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab"),
                    new Vector3(2000, 0, 0), Quaternion.identity, root.transform);
                Physics2D.SyncTransforms();
                var floor = Physics2D.Raycast(new Vector2(1996, 50), Vector2.down, 100, 1 << 6);
                Vector2 position = new Vector2(1996, floor.collider != null ? floor.point.y + 0.75f : 2f);
                var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(ElectricBeamBuilder.SkillPath);
                var step = skill.Recipe.Steps.Single(s => s.StepDefinition.Id == "InstantBeam");
                var owner = new GameObject("Inactive beam preview caster"); owner.SetActive(false);
                owner.transform.SetParent(root.transform); owner.transform.position = position;
                var stats = owner.AddComponent<PlayerStats>();
                typeof(PlayerStats).GetMethod("Awake", flags).Invoke(stats, null);
                var visuals = new GameObject("Caster visuals");
                visuals.transform.SetParent(root.transform); visuals.transform.position = position;
                var body = visuals.AddComponent<SpriteRenderer>();
                var livePlayer = Object.FindFirstObjectByType<PlayerStats>();
                body.sprite = livePlayer != null
                    ? livePlayer.GetComponentsInChildren<SpriteRenderer>().Where(s => s.sprite != null)
                        .OrderByDescending(s => s.sprite.bounds.size.y).First().sprite
                    : Resources.LoadAll<Sprite>("Heroes/WarriorResources/Warrioir_v0.1").First();
                body.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.Player);
                body.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.Player, position.y);
                var wand = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(
                    "Assets/Resources/Items/1H Weapon/1HWand/MysticAdventurer'sWand.asset");
                var hand = new GameObject("Wand"); hand.transform.SetParent(visuals.transform, false);
                hand.transform.localPosition = new Vector3(0.38f, 0.13f);
                hand.transform.localRotation = Quaternion.Euler(0, 0, -35);
                var held = hand.AddComponent<SpriteRenderer>(); held.sprite = wand.InHandSprite;
                held.sortingLayerName = body.sortingLayerName; held.sortingOrder = body.sortingOrder + 1;
                var runner = owner.AddComponent<SkillStepRunner>();
                typeof(SkillBehaviour).GetField("_ownerStats", flags).SetValue(runner, stats);
                typeof(SkillBehaviour).GetField("_data", flags).SetValue(runner, skill);
                typeof(SkillStepRunner).GetField("_ctx", flags).SetValue(runner, new SkillStepContext { OwnerStats = stats });
                typeof(SkillStepRunner).GetMethod("ExecuteInstantBeam", flags).Invoke(runner, new object[] { 3, step });
                var beam = Object.FindObjectsByType<ElectricBeamVisual>(FindObjectsSortMode.None)
                    .FirstOrDefault(v => v.transform.position.x > 1500f);
                if (beam == null) throw new InvalidOperationException("Beam failed to spawn in room preview.");
                beam.transform.SetParent(root.transform, true);
                if (Camera.main != null) camera.CopyFrom(Camera.main);
                camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = 270f / 48f; camera.aspect = 480f / 270f;
                camera.transform.position = (Vector3)position + new Vector3(5.8f, 1.2f, -10);
                camera.ResetWorldToCameraMatrix(); camera.ResetProjectionMatrix();
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                renderTarget.Create();
                string folder = Path.Combine(Path.GetTempPath(), "electric-beam-check"); Directory.CreateDirectory(folder);
                for (int frame = 0; frame < 7; frame++)
                {
                    beam.Sample(frame / 7f);
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = renderTarget });
                    RenderTexture.active = renderTarget;
                    image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(folder, $"ElectricBeam-{frame:D2}.png"), image.EncodeToPNG());
                }
                File.WriteAllText(Path.Combine(folder, "Report.txt"),
                    "Runtime recipe beam rendered at 480x270, full initial length and seven distinct dissipation frames.\n" +
                    "Beam width: " + beam.GetComponent<SpriteRenderer>().bounds.size.x + " world units.\n" +
                    "Material supported: " + beam.GetComponent<SpriteRenderer>().sharedMaterial.shader.isSupported);
            }
            finally
            {
                RenderTexture.active = previous; renderTarget.Release();
                Object.DestroyImmediate(root); Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(renderTarget); Object.DestroyImmediate(image);
            }
        }
    }
}
