using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

namespace Scripts.Visuals.Atmosphere
{
    /// <summary>Shared, reversible material variants; no sprite, renderer color or property-block writes.</summary>
    public static class WorldColorGrading
    {
        private sealed class Binding
        {
            public Material Source, Variant;
            public RenderDepthCategory Category;
        }

        private static readonly HashSet<AtmosphereController> Controllers = new HashSet<AtmosphereController>();
        private static readonly Dictionary<Renderer, Binding> Bindings = new Dictionary<Renderer, Binding>();
        private static readonly Dictionary<Material, Material> Sources = new Dictionary<Material, Material>();
        private static readonly Dictionary<(Material, WorldColorRole, float, bool), Material> Variants =
            new Dictionary<(Material, WorldColorRole, float, bool), Material>();
        private static readonly Vector4[] Parameters = new Vector4[4];
        private static readonly Vector4[] Tints = new Vector4[4];
        private static readonly List<Renderer> DeadRenderers = new List<Renderer>();
        private static readonly List<(Material, WorldColorRole, float, bool)> DeadVariants = new List<(Material, WorldColorRole, float, bool)>();
        private static readonly HashSet<Scene> OwnedScenes = new HashSet<Scene>();
        private static readonly int EnabledId = Shader.PropertyToID("_RKColorEnabled");
        private static readonly int ReliefId = Shader.PropertyToID("_RKStoneReliefStrength");
        private static readonly int ParamsId = Shader.PropertyToID("_RKColorParameters");
        private static readonly int TintsId = Shader.PropertyToID("_RKColorTints");
        private static readonly int EffectsId = Shader.PropertyToID("_RKColorEffectsInfluence");
        private static int _lastPruneFrame = -1;
        public static int UnsupportedRendererCount { get; private set; }
        public static int BoundRendererCount => Bindings.Count;
        public static bool BindingsSuspended { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            RestoreMaterials();
            Controllers.Clear();
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Shader.SetGlobalFloat(EnabledId, 0f);
            Shader.SetGlobalFloat(ReliefId, 0f);
        }

        public static void Register(AtmosphereController controller)
        {
            Controllers.Add(controller);
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshScene(controller);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterLoadedControllers()
        {
            foreach (var controller in Object.FindObjectsByType<AtmosphereController>(FindObjectsSortMode.None))
                if (controller.isActiveAndEnabled) Register(controller);
        }

        // Editor save/reload guards temporarily remove non-persistent variants.
        public static void SuspendBindings() => RestoreMaterials();
        // Unlike a synchronous asset save, entering/leaving Play Mode spans many Editor updates.
        // Keep preview windows, scene callbacks and sorting updates from rebinding during that gap.
        public static void SetBindingsSuspended(bool suspended)
        {
            BindingsSuspended = suspended;
            if (suspended)
            {
                RestoreMaterials();
                Shader.SetGlobalFloat(EnabledId, 0f);
                Shader.SetGlobalFloat(ReliefId, 0f);
                return;
            }
            _lastPruneFrame = -1;
            RegisterLoadedControllers();
            RefreshRegisteredScenes();
        }
        public static void RefreshRegisteredScenes()
        {
            foreach (var controller in Controllers) if (controller != null) RefreshScene(controller);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshRegisteredScenes();

        public static void Unregister(AtmosphereController controller)
        {
            Controllers.Remove(controller);
            Controllers.RemoveWhere(x => x == null);
            var scene = controller != null ? controller.gameObject.scene : default;
            bool stillOwned = false;
            foreach (var owner in Controllers)
                if (owner.isActiveAndEnabled && owner.gameObject.scene == scene) { stillOwned = true; break; }
            if (!stillOwned) RestoreSceneMaterials(scene);
            if (Controllers.Count != 0) return;
            RestoreMaterials();
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Shader.SetGlobalFloat(EnabledId, 0f);
            Shader.SetGlobalFloat(ReliefId, 0f);
        }

        private static void RestoreSceneMaterials(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
                foreach (var root in scene.GetRootGameObjects())
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials; bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                        if (materials[i] != null && Sources.TryGetValue(materials[i], out var source) && source != null)
                        { materials[i] = source; changed = true; }
                    if (changed) renderer.sharedMaterials = materials;
                }
            DeadRenderers.Clear();
            foreach (var pair in Bindings)
                if (pair.Key == null || pair.Key.gameObject.scene == scene) DeadRenderers.Add(pair.Key);
            foreach (var renderer in DeadRenderers) Bindings.Remove(renderer);
        }

        public static void RefreshScene(AtmosphereController controller)
        {
            if (BindingsSuspended || controller == null || !controller.gameObject.scene.IsValid() || !controller.gameObject.scene.isLoaded) return;
            // An artist may have edited a source material since its variant was cached.
            foreach (var pair in Variants)
            {
                if (pair.Key.Item1 == null || pair.Value == null) continue;
                pair.Value.CopyPropertiesFromMaterial(pair.Key.Item1);
                pair.Value.SetFloat("_RKGradeGroup", (float)pair.Key.Item2);
                pair.Value.SetFloat("_RKGradeInfluence", pair.Key.Item3);
                pair.Value.SetFloat("_RKGradeIsEffect", pair.Key.Item4 ? 1f : 0f);
            }
            UnsupportedRendererCount = 0;
            foreach (GameObject root in controller.gameObject.scene.GetRootGameObjects())
                RefreshRoot(root.transform);
        }

        public static void RefreshRoot(Transform root)
        {
            if (BindingsSuspended || Controllers.Count == 0 || root == null || !root.gameObject.scene.isLoaded) return;
            if (!Application.isPlaying || _lastPruneFrame != Time.frameCount)
            {
                _lastPruneFrame = Time.frameCount;
                PruneDestroyedBindings();
            }
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) Bind(renderer, force: true);
        }

        private static void PruneDestroyedBindings()
        {
            DeadRenderers.Clear();
            foreach (var pair in Bindings) if (pair.Key == null) DeadRenderers.Add(pair.Key);
            foreach (Renderer renderer in DeadRenderers) Bindings.Remove(renderer);
            DeadVariants.Clear();
            foreach (var pair in Variants) if (pair.Key.Item1 == null) DeadVariants.Add(pair.Key);
            foreach (var key in DeadVariants)
            {
                if (Variants[key] != null)
                {
                    Sources.Remove(Variants[key]);
                    if (Application.isPlaying) Object.Destroy(Variants[key]); else Object.DestroyImmediate(Variants[key]);
                }
                Variants.Remove(key);
            }
        }

        public static WorldColorRole ResolveRole(Renderer renderer, RenderDepthCategory category,
            out float influence, out bool effect)
        {
            influence = 1f;
            effect = category == RenderDepthCategory.GameplayVfx || category == RenderDepthCategory.HeroAttackVfx ||
                renderer.GetComponentInParent<global::AutoDestroyVFX>(true) != null ||
                renderer.GetComponentInParent<Scripts.Skills.Projectiles.SkillProjectile>(true) != null;
            // UI and contact shadows deliberately remain independent of world grading.
            if (renderer.GetComponentInParent<Canvas>(true) != null || renderer.name.Contains("ContactShadow") || renderer.name == "GroundShadow")
                return WorldColorRole.Unaffected;
            for (Transform node = renderer.transform; node != null; node = node.parent)
            {
                if (node.TryGetComponent(out WorldColorGroup group) && group.enabled)
                {
                    influence = group.Influence; effect |= group.CombatEffect;
                    return group.Role;
                }
                // Actor identity takes precedence over an enclosing room's environment group.
                if (node.GetComponent<global::PlayerStats>() != null || node.GetComponent<Scripts.Enemies.EnemyEntity>() != null)
                    return WorldColorRole.Characters;
            }
            if (category == RenderDepthCategory.Player || category == RenderDepthCategory.PlayerOverlay ||
                category == RenderDepthCategory.Enemy || category == RenderDepthCategory.EnemyRemains || effect)
                return WorldColorRole.Characters;
            if (category == RenderDepthCategory.Background) return WorldColorRole.Background;
            if (category == RenderDepthCategory.Environment) return WorldColorRole.Environment;
            // Legacy hub Aseprite layers share a sorting layer. Use their authored visual roles.
            switch (renderer.name)
            {
                case "Ground": case "Buildings": case "TreesAndPlants": return WorldColorRole.Environment;
                case "Sky": case "Mountains": case "Castle": case "DistantForest":
                case "Fog": case "Forest": case "DungeonSharedBackground": return WorldColorRole.Background;
            }
            string layer = renderer.sortingLayerName;
            if (layer == "Hero" || layer == "World") return WorldColorRole.Characters;
            if (layer == "VFX" || layer == "SFX") { effect = true; return WorldColorRole.Characters; }
            if (layer == "Background") return WorldColorRole.Background;
            return WorldColorRole.Environment;
        }

        public static void Bind(Renderer renderer, RenderDepthCategory category = RenderDepthCategory.Auto, bool force = false)
        {
            if (BindingsSuspended || Controllers.Count == 0 || renderer == null ||
                !(renderer is SpriteRenderer || renderer is TilemapRenderer || renderer is LineRenderer || renderer is TrailRenderer)) return;
            Bindings.TryGetValue(renderer, out Binding binding);
            Material current = renderer.sharedMaterial;
            if (category == RenderDepthCategory.Auto && binding != null) category = binding.Category;
            if (!force && binding != null && current == binding.Variant && binding.Category == category) return;
            AtmosphereController owner = null;
            foreach (AtmosphereController controller in Controllers)
                if (controller != null && controller.gameObject.scene == renderer.gameObject.scene) { owner = controller; break; }
            if (owner == null) return;
            WorldColorRole role = ResolveRole(renderer, category, out float influence, out bool effect);
            Material source = binding != null && current == binding.Variant ? binding.Source : current;
            if (source != null && Sources.TryGetValue(source, out Material original)) source = original;
            if (source == null) return;
            if (role == WorldColorRole.Unaffected || influence <= 0f)
            {
                if (binding != null && current == binding.Variant) renderer.sharedMaterial = binding.Source;
                Bindings.Remove(renderer);
                return;
            }
            var key = (source, role, influence, effect);
            if (!Variants.TryGetValue(key, out Material variant) || variant == null)
            {
                Shader shader = source.shader;
                switch (shader.name)
                {
                    case "Universal Render Pipeline/2D/Sprite-Unlit-Default":
                    case "Sprites/Default": shader = owner.ColorUnlitShader; break;
                    case "Universal Render Pipeline/2D/Sprite-Lit-Default": shader = owner.ColorLitShader; break;
                }
                if (shader == null || shader.FindPropertyIndex("_RKGradeGroup") < 0)
                {
                    UnsupportedRendererCount++;
                    Bindings.Remove(renderer);
                    return; // Never replace an unknown custom effect with a generic sprite shader.
                }
                variant = new Material(source) { shader = shader, name = source.name + " (" + role + ")", hideFlags = HideFlags.HideAndDontSave };
                variant.SetFloat("_RKGradeGroup", (float)role);
                variant.SetFloat("_RKGradeInfluence", influence);
                variant.SetFloat("_RKGradeIsEffect", effect ? 1f : 0f);
                Variants[key] = variant;
                Sources[variant] = source;
            }
            if (binding == null) { binding = new Binding(); Bindings[renderer] = binding; }
            binding.Source = source; binding.Variant = variant;
            binding.Category = category;
            if (current != variant) renderer.sharedMaterial = variant;
        }

        private static void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            camera.TryGetComponent(out AtmosphereController controller);
#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView && Camera.main != null)
                controller = Camera.main.GetComponent<AtmosphereController>();
#endif
            Upload(!BindingsSuspended && controller != null && controller.isActiveAndEnabled && !controller.PreviewNeutralColors ? controller.ActiveColorProfile : null);
            var lighting = !BindingsSuspended && controller != null && controller.isActiveAndEnabled ? controller.ActiveDepthMistProfile : null;
            Shader.SetGlobalFloat(ReliefId, lighting != null ? Mathf.Clamp01(lighting.stoneReliefStrength) : 0);
        }

        private static void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            Shader.SetGlobalFloat(EnabledId, 0f); Shader.SetGlobalFloat(ReliefId, 0f);
        }

        private static void Upload(WorldColorProfileSO profile)
        {
            Shader.SetGlobalFloat(EnabledId, profile != null ? 1f : 0f);
            if (profile == null) return;
            Parameters[0] = new Vector4(1f, 1f, 1f, 0f); Tints[0] = Vector4.one;
            for (int i = 1; i <= 3; i++)
            {
                WorldColorAdjustment adjustment = profile.GetAdjustment((WorldColorRole)i);
                Parameters[i] = adjustment.Parameters;
                Tints[i] = adjustment.tint.linear;
            }
            Shader.SetGlobalVectorArray(ParamsId, Parameters);
            Shader.SetGlobalVectorArray(TintsId, Tints);
            Shader.SetGlobalFloat(EffectsId, profile.effectsInfluence);
        }

        private static void RestoreMaterials()
        {
            OwnedScenes.Clear();
            foreach (var controller in Controllers)
                if (controller != null) OwnedScenes.Add(controller.gameObject.scene);
            foreach (var pair in Bindings)
                if (pair.Key != null)
                {
                    OwnedScenes.Add(pair.Key.gameObject.scene);
                    if (pair.Key.sharedMaterial == pair.Value.Variant) pair.Key.sharedMaterial = pair.Value.Source;
                }
            // A duplicated object/weapon may inherit a variant before its first sorting update.
            // Restore those references too before destroying the shared temporary material.
            foreach (Scene scene in OwnedScenes)
            {
                if (!scene.IsValid() || !scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] materials = renderer.sharedMaterials;
                        bool changed = false;
                        for (int i = 0; i < materials.Length; i++)
                            if (materials[i] != null && Sources.TryGetValue(materials[i], out Material original))
                            { materials[i] = original; changed = true; }
                        if (changed) renderer.sharedMaterials = materials;
                    }
            }
            Bindings.Clear();
            foreach (Material material in Variants.Values)
                if (material != null)
                {
                    if (Application.isPlaying) Object.Destroy(material); else Object.DestroyImmediate(material);
                }
            Variants.Clear();
            Sources.Clear();
        }
    }
}
