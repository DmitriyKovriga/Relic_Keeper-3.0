using System.Linq;
using System.IO;
using System.Reflection;
using Scripts.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Enemy
{
    public static class MoldVisualBuilder
    {
        public const string ArtRoot = "Assets/Resources/Enemy/MoldResources/Refined";
        public const int FrameSize = 36;

        [MenuItem("Tools/Enemies/Apply Refined Mold Visuals")]
        public static void Apply()
        {
            Sprite[] idle = Import("MoldIdle", 5);
            Import("MoldWalk", 9);
            Import("MoldAttack", 13);
            Import("MoldHit", 5);
            var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>("Assets/Resources/Enemy/SO_Mold.asset");
            data.Animation.IdleSpritesResourcePath = "Enemy/MoldResources/Refined/MoldIdle";
            data.Animation.MoveSpritesResourcePath = "Enemy/MoldResources/Refined/MoldWalk";
            data.Animation.AttackSpritesResourcePath = "Enemy/MoldResources/Refined/MoldAttack";
            data.Animation.HitSpritesResourcePath = "Enemy/MoldResources/Refined/MoldHit";
            data.Animation.IdleFps = 5;
            data.Animation.MoveFps = 8;
            data.Animation.AttackFps = 14;
            // Play the cast at 70% speed; the projectile remains tied to the flash pose.
            data.Animation.AttackImpactFrame = 8;
            data.Animation.HitFps = 12.5f;
            EditorUtility.SetDirty(data);
            var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Enemy/MoldEnemyPF.prefab");
            try
            {
                prefab.GetComponent<SpriteRenderer>().sprite = idle[0];
                PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Prefabs/Enemy/MoldEnemyPF.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
        }

        private static Sprite[] Import(string name, int count)
        {
            string path = ArtRoot + "/" + name + ".png";
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
            importer.spritesheet = Enumerable.Range(0, count).Select(i => new SpriteMetaData
            {
                name = name + "_" + i.ToString("D2"), rect = new Rect(i * FrameSize, 0, FrameSize, FrameSize),
                alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f)
            }).ToArray();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(x => x.name).ToArray();
        }

        public static void CaptureRuntimeViews()
        {
            if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Enter Play Mode first.");
            var data = Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
            var original = Object.Instantiate(data);
            original.Animation.IdleSpritesResourcePath = "Enemy/MoldResources/mold_idle-Sheet";
            original.Animation.AttackSpritesResourcePath = "Enemy/MoldResources/mold_attack-Sheet";
            var root = new GameObject("Temporary Mold visual verification");
            root.transform.position = new Vector3(2000, 0, 0);
            var old = Object.Instantiate(data.Prefab, root.transform);
            var current = Object.Instantiate(data.Prefab, root.transform);
            var camera = new GameObject("Mold verification camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 270f / 48;
            camera.aspect = 480f / 270;
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.15f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(2000, 3, -10);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
            var target = new RenderTexture(480, 270, 24);
            var texture = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var projectile = new GameObject("Mold shot alignment preview").AddComponent<SpriteRenderer>();
            projectile.transform.SetParent(root.transform, false);
            var projectileFrames = Resources.LoadAll<Sprite>(data.Attack.ProjectileVisualResourcePath).OrderBy(x => x.name).ToArray();
            projectile.sortingLayerID = current.GetComponent<SpriteRenderer>().sortingLayerID;
            projectile.sortingOrder = 100;
            var previous = RenderTexture.active;
            try
            {
                old.Setup(original, 1);
                current.Setup(data, 1);
                old.transform.localPosition = new Vector3(-2, 0.75f, 0);
                current.transform.localPosition = new Vector3(2, 0.75f, 0);
                foreach (var actor in new[] { old, current })
                {
                    foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
                    actor.GetComponent<Rigidbody2D>().simulated = false;
                }
                string folder = Path.Combine(Path.GetTempPath(), "codex-mold-refresh");
                Directory.CreateDirectory(folder);
                target.Create();
                var oldIdle = Resources.LoadAll<Sprite>(original.Animation.IdleSpritesResourcePath).OrderBy(x => x.name).ToArray();
                var oldAttack = Resources.LoadAll<Sprite>(original.Animation.AttackSpritesResourcePath).OrderBy(x => x.name).ToArray();
                foreach (string state in new[] { "Idle", "Walk", "Attack", "Hit" })
                {
                    var frames = Resources.LoadAll<Sprite>("Enemy/MoldResources/Refined/Mold" + state).OrderBy(x => x.name).ToArray();
                    for (int i = 0; i < frames.Length; i++)
                    {
                        current.VisualRenderer.sprite = frames[i];
                        old.VisualRenderer.sprite = state == "Attack" ? oldAttack[Mathf.Min(i, oldAttack.Length - 1)] : oldIdle[0];
                        // Use the actual attack controller's origin and projectile art to review
                        // the release pose without firing at players or starting a dungeon run.
                        bool released = state == "Attack" && i >= data.Animation.AttackImpactFrame;
                        projectile.enabled = released && projectileFrames.Length > 0;
                        if (projectile.enabled)
                        {
                            var controller = current.GetComponent<EnemyAttackController>();
                            var resolve = typeof(EnemyAttackController).GetMethod("ResolveProjectileOrigin", BindingFlags.Instance | BindingFlags.NonPublic);
                            Vector2 origin = (Vector2)resolve.Invoke(controller, null);
                            float elapsed = (i - data.Animation.AttackImpactFrame) / data.Animation.AttackFps;
                            projectile.transform.position = origin + Vector2.right * (elapsed * data.Attack.ProjectileSpeed);
                            projectile.sprite = projectileFrames[Mathf.FloorToInt(elapsed * data.Attack.ProjectileAnimationFps) % projectileFrames.Length];
                        }
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target;
                        texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
                        texture.Apply();
                        File.WriteAllBytes(Path.Combine(folder, state + "-" + i.ToString("D2") + ".png"), texture.EncodeToPNG());
                    }
                }
                Debug.Log("Native-scale Mold runtime views captured: " + folder);
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(original);
            }
        }
    }
}
