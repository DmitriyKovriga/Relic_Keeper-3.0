using System;
using System.Linq;
using System.IO;
using System.Reflection;
using Scripts.Skills.Projectiles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    public static class GroundShakeVisualBuilder
    {
        private const string ArtRoot = "Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/GroundShake/";
        private const string PrefabPath = ArtRoot + "GroundShakeVFXPrefub.prefab";
        private const int FrameWidth = 31;
        private const int FrameHeight = 28;
        private const int FrameCount = 8;
        private const float Fps = 24;

        [MenuItem("Tools/Skills/Apply Refined Ground Shake VFX")]
        public static void Apply()
        {
            string path = ArtRoot + "GroundShakeRefined-Sheet.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spritesheet = Enumerable.Range(0, FrameCount).Select(i => new SpriteMetaData
            {
                name = "GroundShakeRefined_" + i.ToString("D2"),
                rect = new Rect((i % 3) * FrameWidth, (2 - i / 3) * FrameHeight, FrameWidth, FrameHeight),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            }).ToArray();
            importer.SaveAndReimport();
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name).ToArray();
            if (frames.Length != FrameCount) throw new InvalidOperationException("Ground Shake frame import failed.");

            // Eight frames retain the original four-frame, 12 FPS loop duration.
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtRoot + "GroundShakeAnimation.anim");
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / Fps, value = sprite }).ToList();
            clip.frameRate = 24;
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);

            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                prefab.GetComponent<SpriteRenderer>().sprite = frames[0];
                var debris = prefab.GetComponent<GroundProjectileDebris>() ?? prefab.AddComponent<GroundProjectileDebris>();
                string materialPath = ArtRoot + "GroundShakeDebris.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Sprites/Default"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                string texturePath = ArtRoot + "GroundShakeDebrisTexture.asset";
                var debrisTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (debrisTexture == null)
                {
                    debrisTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    debrisTexture.name = "Ground Shake pixel clod";
                    debrisTexture.filterMode = FilterMode.Point;
                    debrisTexture.wrapMode = TextureWrapMode.Clamp;
                    string[] rows = { ".dd.", "wwwd", "wwww", ".ww." };
                    var pixels = new Color[16];
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 4; x++)
                            pixels[y * 4 + x] = rows[y][x] == '.' ? Color.clear :
                                rows[y][x] == 'd' ? new Color(0.65f, 0.65f, 0.65f) : Color.white;
                    debrisTexture.SetPixels(pixels);
                    debrisTexture.Apply(false, false);
                    AssetDatabase.CreateAsset(debrisTexture, texturePath);
                }
                material.mainTexture = debrisTexture;
                EditorUtility.SetDirty(material);
                var serializedDebris = new SerializedObject(debris);
                serializedDebris.FindProperty("_particleMaterial").objectReferenceValue = material;
                serializedDebris.FindProperty("_groundLayer").intValue = LayerMask.GetMask("Ground", "OneWayPlatform");
                serializedDebris.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
        }

        public static void CaptureRuntimeViews()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var root = new GameObject("Temporary Ground Shake verification");
            root.transform.position = new Vector3(2000, 0, 0);
            var camera = new GameObject("Ground Shake verification camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 270f / 48f;
            camera.aspect = 480f / 270f;
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.15f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(2000, 0, -10);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
            var target = new RenderTexture(480, 270, 24);
            var texture = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            Sprite floorSprite = null;
            var debrisSystems = new System.Collections.Generic.List<ParticleSystem>();
            try
            {
                var floor = new GameObject("Isolated floor");
                floor.transform.SetParent(root.transform, false);
                floor.layer = 6;
                floor.transform.localPosition = new Vector3(0, -0.1f, 0);
                floor.AddComponent<BoxCollider2D>().size = new Vector2(30, 0.2f);
                var floorVisual = new GameObject("Floor surface preview");
                floorVisual.transform.SetParent(floor.transform, false);
                floorVisual.transform.localScale = new Vector3(30, 0.2f, 1);
                var floorRenderer = floorVisual.AddComponent<SpriteRenderer>();
                floorSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
                floorRenderer.sprite = floorSprite;
                floorRenderer.color = new Color(0.28f, 0.3f, 0.25f);
                Physics2D.SyncTransforms();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtRoot + "GroundShakeAnimation.anim");
                if (!clip.isLooping || Mathf.Abs(clip.length - 1f / 3f) > 0.001f)
                    throw new InvalidOperationException("Ground Shake must retain its travelling loop duration.");
                var actors = new GameObject[2];
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var initialize = typeof(SkillProjectile).GetMethod("Initialize", flags);
                var snap = typeof(SkillProjectile).GetMethod("SnapToGroundOrDespawn", flags);
                for (int side = 0; side < 2; side++)
                {
                    int facing = side == 0 ? -1 : 1;
                    var actor = Object.Instantiate(prefab, root.transform);
                    actors[side] = actor;
                    actor.transform.localPosition = new Vector3(facing * 2, 0.4f, 0);
                    actor.GetComponent<AutoDestroyVFX>().enabled = false;
                    var projectile = actor.AddComponent<SkillProjectile>();
                    initialize.Invoke(projectile, new object[]
                    {
                        new SkillProjectileLaunchData { ProjectilePrefab = prefab, GroundMotion = true,
                            GroundLayer = 1 << 6, GroundYOffset = 0, TargetLayer = 0, Speed = 20, HitRadius = 1,
                            InfinitePierce = true, IgnoreFork = true, IgnoreChain = true },
                        new Vector2(facing, 0), false, prefab
                    });
                    actor.GetComponent<Animator>().enabled = false;
                    if (actor.GetComponent<SpriteRenderer>().flipX != (facing < 0))
                        throw new InvalidOperationException("Ground Shake facing does not mirror.");
                }
                target.Create();
                string folder = Path.Combine(Path.GetTempPath(), "codex-ground-shake-refresh");
                Directory.CreateDirectory(folder);
                for (int i = 0; i < FrameCount * 2; i++)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        var actor = actors[side];
                        clip.SampleAnimation(actor, (i % FrameCount) / Fps);
                        int facing = side == 0 ? -1 : 1;
                        actor.transform.localPosition = new Vector3(facing * (2 + i * 20f / Fps), 0.4f, 0);
                        Physics2D.SyncTransforms();
                        if (!(bool)snap.Invoke(actor.GetComponent<SkillProjectile>(), null))
                            throw new InvalidOperationException("Ground Shake lost the isolated floor.");
                        if (Mathf.Abs(actor.GetComponent<SpriteRenderer>().bounds.min.y) > 0.001f)
                            throw new InvalidOperationException("Ground Shake bottom alignment changed at frame " + i +
                                ": " + actor.GetComponent<SpriteRenderer>().bounds.min.y);
                        var debris = actor.GetComponent<GroundProjectileDebris>();
                        if (debris.RuntimeParticles != null)
                            debris.RuntimeParticles.Simulate(1f / Fps, true, false, true);
                        debris.EmitAfterMovement();
                        if (debris.RuntimeParticles != null && !debrisSystems.Contains(debris.RuntimeParticles))
                            debrisSystems.Add(debris.RuntimeParticles);
                    }
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, "GroundShake-" + i.ToString("D2") + ".png"), texture.EncodeToPNG());
                }
                foreach (var actor in actors)
                {
                    var cloud = actor.GetComponent<GroundProjectileDebris>().RuntimeParticles;
                    if (cloud == null || cloud.particleCount == 0)
                        throw new InvalidOperationException("Ground Shake emitted no world debris.");
                    actor.SetActive(false);
                    if (cloud == null || cloud.particleCount == 0 || cloud.transform.parent != null)
                        throw new InvalidOperationException("Ground Shake cleared its world debris when disabled.");
                }
                for (int i = FrameCount * 2; i < FrameCount * 3; i++)
                {
                    foreach (var particles in debrisSystems)
                        if (particles != null) particles.Simulate(1f / Fps, true, false, true);
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, "GroundShake-" + i.ToString("D2") + ".png"), texture.EncodeToPNG());
                }
                Debug.Log("Ground Shake: mirrored projectile frames, two loops and ground alignment verified: " + folder);
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camera.gameObject);
                foreach (var particles in debrisSystems)
                    if (particles != null) Object.DestroyImmediate(particles.gameObject);
                Object.DestroyImmediate(root);
                if (floorSprite != null) Object.DestroyImmediate(floorSprite);
            }
        }
    }
}

