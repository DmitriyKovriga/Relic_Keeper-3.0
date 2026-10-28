#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.GameplayEvents;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Stats;
using Scripts.StatusEffects;
using Scripts.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Combat
{
    // Explicitly started, temporary Play Mode fixture. No menu/button, scene save, or player-build code.
    [DefaultExecutionOrder(32000)]
    [AddComponentMenu("")]
    public sealed partial class CombatBaselineCapture : MonoBehaviour
    {
        private bool _confirmation;
        private bool _poseComparison;
        private bool _meleeCoverage;
        private string Output => _deathReview ? "Logs/CombatDeath" : _defenseReview ? "Logs/CombatDefense" : _hurtReview ? "Logs/CombatPlayerHurt" : _cameraReview ? "Logs/CombatCameraImpulse" : _crowdReview ? CrowdOutput : _flashComparison ? "Logs/CombatHitFlash" : _meleeCoverage ? "Logs/CombatMeleeCoverage" : _poseComparison ? "Logs/CombatPoseComparison" : _confirmation ? "Logs/CombatHitConfirmation" : "Logs/CombatBaseline";
        private const string SkillPath = "Assets/Resources/Skills/2HWeapon/Axe/LeftButton/CleaveLB.asset";
        private const int CaptureFrames = 110;
        private const int CastFrame = 12;
        private static readonly string[] Cases = { "miss", "direct-hit", "evaded", "zero-damage", "lethal", "dot", "attacking-target" };
        private static readonly string[] PoseCases = { "cleave-off", "cleave-on", "heavy-off", "heavy-on", "attacking-target", "zombie-recoil", "rogue-left", "mage-air" };
        private static readonly string[] MeleeCases = { "hook", "retreat", "anvil-ground", "anvil-air", "ground-shake", "lunge", "dexterous", "stunning", "blink", "blink-miss", "charge", "parry" };
        private static readonly string[] MeleeSkills = {
            "2HWeapon/Axe/CrimsonHook/CrimsonHookSkill", "2HWeapon/Sword/Adventurer/RetreatingThrust/RetreatingThrustSkill",
            "2HWeapon/Hammer/Adventurer/FallingAnvil/FallingAnvilSkill", "2HWeapon/Hammer/Adventurer/FallingAnvil/FallingAnvilSkill",
            "2HWeapon/Hammer/Adventurer/GroundShake/GroundShakeSkill", "2HWeapon/Sword/Adventurer/LungeSlash/LungeSlashSkill",
            "1HWeapon/Sword/DexterousLungeSkill", "1HWeapon/Hammer/StunningBlowSkill", "1HWeapon/Dagger/NinjaStubSkill",
            "1HWeapon/Dagger/NinjaStubSkill", "Shields/TowerCharge", "Shields/SpectacularParry" };
        private string[] ActiveCases => _deathReview ? DeathCases : _defenseReview ? DefenseCases : _hurtReview ? HurtCases : _cameraReview ? CameraCases : _crowdReview ? CrowdCases : _flashComparison ? FlashCases : _meleeCoverage ? MeleeCases : _poseComparison ? PoseCases : Cases;
        private string Scenario => _hurtReview ? "hurt-review" : _cameraReview ? "camera-review" : _crowdReview ? "crowd-review" : _flashComparison ? "flash-review" : _meleeCoverage ? "melee-review" : !_poseComparison || _case.name == "attacking-target" ? _case.name
            : _case.name == "mage-air" ? "miss" : "direct-hit";
        private bool HeavyCase => _poseComparison && _case.name.StartsWith("heavy", StringComparison.Ordinal);
        private string CurrentSkillPath => _deathReview && _case.name == "fan" ? "Assets/Resources/Skills/2HWeapon/Bow/NearFan/NearFanSkill.asset" : _cameraReview && _case.name == "bow-on" ? "Assets/Resources/Skills/2HWeapon/Bow/SimpleBowShotSkill.asset" : _crowdReview ? CrowdSkillPath : _flashComparison && _case.name.StartsWith("bow", StringComparison.Ordinal) ? "Assets/Resources/Skills/2HWeapon/Bow/SimpleBowShotSkill.asset"
            : _meleeCoverage ? "Assets/Resources/Skills/" + MeleeSkills[_index] + ".asset"
            : HeavyCase ? "Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/WeightOfAGiant/WeightOfAGiantSkill.asset" : SkillPath;
        private CombatPoseProfile _comparisonProfile;
        private int _frameLimit;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static CombatBaselineCapture _instance;
        private readonly List<CaseResult> _results = new List<CaseResult>();
        private CaseResult _case;
        private GameObject _fixture;
        private PlayerStats _owner;
        private SkillStepRunner _runner;
        private EnemyHealth _health;
        private EnemyEntity _enemy;
        private EnemyDataSO _enemyData;
        private SpriteRenderer _body, _weapon;
        private Camera _camera;
        private RenderTexture _target;
        private Texture2D _image;
        private Material _material;
        private Sprite _floorSprite;
        private int _index, _frame, _attempt = 1;
        private int _previousCaptureRate;
        private bool _previousEditorPause;
        private int _pumpTicks;
        private EditorWindow _gameView, _previousWindow;
        private bool _openedGameView;
        private UnityEngine.Random.State _randomState;
        private bool _finished;
        private float _beforeHealth;

        [Serializable] public sealed class AuditResult
        {
            public string status, error, utc, skill, recipe, fixture;
            public int fps = 60;
            public int currentFrame, pumpTicks, engineFrame;
            public string currentCase, scene;
            public bool playingScene;
            public List<CaseResult> cases;
        }
        [Serializable] public sealed partial class CaseResult
        {
            public string name, castToken, error, weapon;
            public string skill, hero;
            public bool posesEnabled, rootEnemyRenderer;
            public int attempt, acceptedCasts, damageTakenEvents, damageDealtEvents, evadeEvents, kills, resolutionCount;
            public int confirmedHits;
            public bool returnedTrue, enemyAttackStarted, busyAtDamage, dead;
            public float positiveDamage, initialHealth, finalHealth, recipeDuration;
            public List<EventRecord> events = new List<EventRecord>();
            public List<FrameRecord> frames = new List<FrameRecord>();
        }
        [Serializable] public sealed class EventRecord
        {
            public string type, castToken;
            public int frame;
            public float phase, amount;
            public bool direct, busy;
            public Vector3 eventPosition;
        }
        [Serializable] public sealed partial class FrameRecord
        {
            public int frame;
            public float phase, duration, scaledTime, unscaledTime, timeScale, health;
            public bool casting, enemyBusy, weaponVisible;
            public string bodySprite, targetSprite;
            public Vector3 bodyPosition, bodyScale, bodyRotation, handPosition, handRotation, weaponPosition, weaponRotation;
            public Vector3 enemyRecoilPosition, enemyRecoilScale;
            public bool enemyRecoilVisible;
            public float attackBodyDegrees;
            public Vector3 ownerPosition;
            public bool bodyOwned, grounded;
            public float hitFlash, visibleHitFlash, statusEmission;
            public Color targetColor;
            public string targetMaterial;
        }

        public static void Begin() => BeginCapture(false);
        public static void BeginHitConfirmation() => BeginCapture(true);
        public static void BeginPoseComparison() => BeginCapture(true, true);
        public static void BeginMeleeCoverage() => BeginCapture(true, true, true);

        private static void BeginCapture(bool confirmation, bool poseComparison = false, bool meleeCoverage = false, bool flashComparison = false, bool crowdReview = false, bool crowdBefore = false, bool cameraReview = false, bool hurtReview = false)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode before the baseline audit.");
            if (_instance != null) throw new InvalidOperationException("The baseline audit is already running.");
            if (Time.timeScale <= 0f) throw new InvalidOperationException("The baseline audit requires an unpaused game.");
            var host = new GameObject("Temporary combat baseline audit");
            // World Appearance can leave a preview scene active. Runtime fixtures must
            // live in the playing scene, never in that non-simulated visual copy.
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/HubScene.unity");
            if (!scene.IsValid() || !scene.isLoaded) scene = SceneManager.GetActiveScene();
            if (EditorSceneManager.IsPreviewScene(scene)) { Object.DestroyImmediate(host); throw new InvalidOperationException("No playing scene is available for the baseline fixture."); }
            SceneManager.MoveGameObjectToScene(host, scene);
            _instance = host.AddComponent<CombatBaselineCapture>();
            if (_instance == null) { Object.DestroyImmediate(host); throw new InvalidOperationException("The Editor-only runtime capture driver could not be created."); }
            _instance._confirmation = confirmation;
            _instance._poseComparison = poseComparison;
            _instance._meleeCoverage = meleeCoverage;
            _instance._flashComparison = flashComparison;
            _instance._hurtReview = hurtReview; _instance._cameraReview = cameraReview; _instance._crowdReview = crowdReview; _instance._crowdBefore = crowdBefore;
            try { _instance.StartCapture(); }
            catch (Exception e) { _instance.Finish(e.ToString()); throw; }
        }

        public static void Stop()
        {
            if (_instance != null) _instance.Finish("Stopped before completion.");
        }

        public static void RestoreHubAfterTests()
        {
            if (Application.isPlaying) return;
            var scene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path) || scene.isDirty) return;
            if (scene.GetRootGameObjects().Any(x => x.name != "Main Camera" && x.name != "Directional Light")) return;
            // The bridge's EditMode runner can leave its untouched default scene open.
            // Reopen the saved starting scene only when no user scene edits can be lost.
            EditorSceneManager.OpenScene("Assets/Scenes/HubScene.unity");
        }

        private void StartCapture()
        {
            _previousCaptureRate = Time.captureFramerate; _randomState = UnityEngine.Random.state;
            _previousEditorPause = EditorApplication.isPaused;
            Directory.CreateDirectory(Output); Time.captureFramerate = 60;
            _previousWindow = EditorWindow.focusedWindow;
            var gameType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.GameView")).First(t => t != null);
            _gameView = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.GetType() == gameType);
            _openedGameView = _gameView == null;
            if (_gameView == null) _gameView = EditorWindow.GetWindow(gameType);
            _gameView.Show(); _gameView.Focus();
            EditorApplication.update += PumpPlayerLoop;
            _material = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (_hurtReview || _flashComparison || _crowdReview)
            {
                _material = new Material(Shader.Find("RelicKeeper/Sprites/World Color Lit"));
                _material.SetFloat("_RKGradeGroup", 1);
            }
            if (!Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None).Any(x => x.lightType == Light2D.LightType.Global && x.gameObject.scene == gameObject.scene))
            {
                var lightObject = new GameObject("Fixture flat ambient"); lightObject.transform.SetParent(transform, false);
                var light = lightObject.AddComponent<Light2D>(); light.lightType = Light2D.LightType.Global;
                light.intensity = 1; light.targetSortingLayers = SortingLayer.layers.Select(x => x.id).ToArray();
            }
            _floorSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f, Texture2D.whiteTexture.width);
            _camera = new GameObject("Baseline camera").AddComponent<Camera>(); _camera.transform.SetParent(transform);
            _camera.enabled = false; _camera.orthographic = true; _camera.orthographicSize = 270f / 48;
            _camera.aspect = 480f / 270; _camera.transform.position = new Vector3(3000.8f, 2000, -10);
            _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = new Color(.11f, .14f, .15f);
            _camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            _camera.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
            _target = new RenderTexture(480, 270, 24); _target.Create(); _image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            GameplayEventBus.EventRaised += ObserveEvent;
            CombatHitFeedback.HitConfirmed += ObserveConfirmation;
            BuildCase(); WriteStatus("running", null);
        }

        private void PumpPlayerLoop()
        {
            // A visible Game view is required for the real hit-stop end-of-frame coroutine.
            // Fixed capture time supplies the simulation delta; slow review is playback-only.
            if (_finished || !Application.isPlaying || EditorApplication.isPaused) return;
            if (++_pumpTicks % 60 == 0) WriteStatus("running", null);
            EditorApplication.QueuePlayerLoopUpdate();
            if (_gameView != null) _gameView.Repaint();
        }

        private void BuildCase()
        {
            _frame = 0; _frameLimit = CaptureFrames;
            _case = new CaseResult { name = ActiveCases[_index], attempt = _attempt };
            _case.skill = CurrentSkillPath;
            _case.hero = _case.name == "rogue-left" ? "Rogue" : _case.name == "mage-air" ? "Mage" : "Warrior";
            _case.posesEnabled = _cameraReview || _crowdReview || _flashComparison || !_poseComparison || !_case.name.EndsWith("-off", StringComparison.Ordinal);
            if (_cameraReview || _flashComparison || _crowdReview) UnityEngine.Random.InitState(1137);
            Directory.CreateDirectory(Output + "/" + _case.name);
            _fixture = new GameObject("Baseline " + _case.name); _fixture.transform.SetParent(transform);
            Vector3 origin = new Vector3(3000, 2000, 0);
            float facing = _case.name == "rogue-left" ? -1f : 1f;
            Vector3 airborneOffset = _case.name == "mage-air" ? Vector3.up * 1.5f : Vector3.zero;
            var floor = new GameObject("Fixture floor") { layer = 6 }; floor.transform.SetParent(_fixture.transform);
            floor.transform.position = origin + Vector3.down; floor.AddComponent<BoxCollider2D>().size = new Vector2(15, 1);
            var floorDisplay = new GameObject("Floor display"); floorDisplay.transform.SetParent(floor.transform, false);
            floorDisplay.transform.localScale = new Vector3(15, 1, 1);
            var floorArt = floorDisplay.AddComponent<SpriteRenderer>(); floorArt.sprite = _floorSprite; floorArt.sharedMaterial = _material;
            floorArt.color = new Color(.27f, .30f, .28f);

            var owner = new GameObject("Baseline Warrior"); owner.SetActive(false); owner.transform.SetParent(_fixture.transform);
            owner.transform.position = origin + airborneOffset; owner.AddComponent<SpriteRenderer>().sharedMaterial = _material;
            owner.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _owner = owner.AddComponent<PlayerStats>();
            var visuals = new GameObject("Visuals").transform; visuals.SetParent(owner.transform, false);
            var hand = new GameObject("HandPivot").transform; hand.SetParent(visuals, false);
            var holder = new GameObject("WeaponHolder").transform; holder.SetParent(hand, false);
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(CurrentSkillPath);
            if (skill == null) throw new InvalidOperationException("The baseline Cleave skill is missing.");
            string weaponKind = CurrentSkillPath.Contains("Bow") ? "Bow" : HeavyCase || CurrentSkillPath.Contains("Hammer") ? "Hammer"
                : CurrentSkillPath.Contains("Sword") ? "Sword" : CurrentSkillPath.Contains("Dagger") ? "Dagger"
                : CurrentSkillPath.Contains("Shields") ? "Shield" : "Axe";
            var weapon = AssetDatabase.FindAssets("t:WeaponItemSO").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.IndexOf(weaponKind, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(AssetDatabase.LoadAssetAtPath<WeaponItemSO>).FirstOrDefault(x => x.IsTwoHanded == (!_meleeCoverage || CurrentSkillPath.Contains("2HWeapon")) && x.InHandSprite != null);
            // Some skills have no authored matching weapon item yet. Record an explicit existing proxy.
            if (weapon == null && _meleeCoverage) weapon = Resources.Load<WeaponItemSO>("Items/Debug/Debug Blade");
            if (weapon == null || weapon.InHandSprite == null) throw new InvalidOperationException("No review weapon sprite is available.");
            _case.weapon = AssetDatabase.GetAssetPath(weapon);
            string stance = string.IsNullOrEmpty(skill.HoldStanceId) ? WeaponStancePoseTableSO.CanonicalId(skill.HoldStance) : skill.HoldStanceId;
            if (WeaponStancePoseTableSO.LoadDefault().TryGetById(stance, out var pose))
            {
                holder.localPosition = (Vector3)pose.LocalPosition + (Vector3)weapon.InHandSpriteLocalOffset;
                holder.localRotation = Quaternion.Euler(0, 0, pose.LocalEulerZ - weapon.InHandSpriteTiltZ);
            }
            _weapon = holder.gameObject.AddComponent<SpriteRenderer>(); _weapon.sprite = weapon.InHandSprite;
            _weapon.sharedMaterial = _material; _weapon.sortingLayerName = "Hero"; _weapon.sortingOrder = 5;
            owner.AddComponent<PlayerMovement>();
            var manager = owner.AddComponent<PlayerSkillManager>(); manager.enabled = false;
            _runner = owner.AddComponent<SkillStepRunner>();
            owner.SetActive(true);
            var rb = owner.GetComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.constraints = RigidbodyConstraints2D.FreezeAll;
            if (_meleeCoverage) { rb.gravityScale = 1; rb.constraints = RigidbodyConstraints2D.FreezeRotation; }
            var checkpoint = new GameObject("GroundCheck").transform; checkpoint.SetParent(owner.transform, false); checkpoint.localPosition = Vector3.down * .5f;
            var movement = new SerializedObject(owner.GetComponent<PlayerMovement>());
            movement.FindProperty("_groundCheckPoint").objectReferenceValue = checkpoint;
            movement.FindProperty("_groundLayer").intValue = 1 << 6; movement.ApplyModifiedPropertiesWithoutUndo();
            var character = AssetDatabase.FindAssets("t:CharacterDataSO").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.IndexOf(_case.hero, StringComparison.OrdinalIgnoreCase) >= 0).Select(AssetDatabase.LoadAssetAtPath<CharacterDataSO>).First();
            _owner.Initialize(character);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 10000; _owner.GetStat(StatType.MaxMana).BaseValue = 1000;
            _owner.GetStat(StatType.AttackSpeed).BaseValue = 1; _owner.GetStat(StatType.DamagePhysical).BaseValue = 100;
            _owner.GetStat(StatType.CritChance).BaseValue = 0; _owner.GetStat(StatType.Pushback).BaseValue = 0;
            _owner.NotifyChanged(); _owner.Health.RestoreFull(); _owner.Mana.RestoreFull();
            _runner.SetRuntimeSlot(manager, 0); _runner.Initialize(_owner, skill);
            ((Dictionary<int, SkillBehaviour>)typeof(PlayerSkillManager).GetField("_activeSkills", Private).GetValue(manager))[0] = _runner;
            _runner.EditorDamageResolved += ObserveResolution;
            _body = owner.GetComponent<PlayerMovementVisual>().DisplayRenderer;
            owner.transform.localScale = new Vector3(facing, 1, 1);
            if (_poseComparison)
            {
                _comparisonProfile = Object.Instantiate(CombatPoseProfile.LoadDefault());
                _comparisonProfile.EnableBodyFollow = _comparisonProfile.EnableEnemyRecoil = _case.posesEnabled;
                typeof(PlayerMovementVisual).GetField("_combatPoseProfile", Private).SetValue(owner.GetComponent<PlayerMovementVisual>(), _comparisonProfile);
                if (_case.name == "mage-air")
                {
                    rb.linearVelocity = Vector2.up * 2;
                    typeof(PlayerCharacterAnimationController).GetMethod("HandleJumpStarted", Private).Invoke(owner.GetComponent<PlayerCharacterAnimationController>(), null);
                }
            }

            _enemyData = Object.Instantiate(Resources.Load<EnemyDataSO>(_case.name.StartsWith("zombie", StringComparison.Ordinal) ? "Enemy/SO_Zombie" : "Enemy/SO_Mold"));
            _enemyData.XPReward = 0; _enemyData.LootDropMultiplier = 0; _enemyData.LootQuantityMultiplier = 0;
            _enemy = Object.Instantiate(_enemyData.Prefab, _fixture.transform);
            _enemy.transform.position = origin + airborneOffset + Vector3.right * (facing * ((Scenario == "miss" || Scenario == "dot") ? 3.8f : 1.1f));
            if (_meleeCoverage && _case.name == "blink-miss") _enemy.transform.position = origin + Vector3.right * 9;
            _enemy.Setup(_enemyData, 1); _enemy.GetComponent<EnemyBrain>().enabled = false;
            // DoT cases intentionally miss. Ensure their ailment receiver without relying
            // on a previous weapon hit to add it lazily.
            if (Scenario == "dot" && _enemy.GetComponent<AilmentController>() == null)
                _enemy.gameObject.AddComponent<AilmentController>();
            var locomotion = _enemy.GetComponent<EnemyLocomotion2D>(); if (locomotion != null) locomotion.enabled = false;
            var enemyRb = _enemy.GetComponent<Rigidbody2D>(); if (enemyRb != null) enemyRb.constraints = RigidbodyConstraints2D.FreezeAll;
            var stats = _enemy.GetComponent<EnemyStats>();
            Set(stats, StatType.MaxHealth, Scenario == "lethal" ? 1 : 10000);
            Set(stats, StatType.Evasion, Scenario == "evaded" ? 10000000 : 0);
            Set(stats, StatType.DamageTaken, Scenario == "zero-damage" ? -100 : 0);
            Set(stats, StatType.Armor, 0); Set(stats, StatType.PhysicalResist, 0); Set(stats, StatType.StunThreshold, 10000);
            typeof(EnemyStats).GetField("<ExperienceReward>k__BackingField", Private).SetValue(stats, 0f);
            typeof(EnemyStats).GetField("<GoldReward>k__BackingField", Private).SetValue(stats, 0);
            _health = _enemy.GetComponent<EnemyHealth>(); _health.Initialize();
            _enemy.GetComponent<EnemyStunController>()?.RecalculateMaxMeter(true);
            _beforeHealth = _health.CurrentHealth; _case.initialHealth = _beforeHealth;
            foreach (var art in _enemy.GetComponentsInChildren<SpriteRenderer>(true)) art.sharedMaterial = _material;
            _case.rootEnemyRenderer = _enemy.VisualRenderer.transform == _enemy.transform;
            if (_poseComparison)
                typeof(EnemyHitRecoil).GetField("_profile", Private).SetValue(_enemy.GetComponent<EnemyHitRecoil>(), _comparisonProfile);
            if (_flashComparison) ConfigureHitFlashCase();
            if (_crowdReview) ConfigureCrowdCase();
            if (_cameraReview) ConfigureCameraCase();
            if (_hurtReview) ConfigureHurtCase();
            Physics2D.SyncTransforms();
        }

        private static void Set(EnemyStats stats, StatType type, float value)
        {
            if (!stats.TryGetStat(type, out var stat)) { stats.AddModifier(type, new StatModifier(0, StatModType.Flat, typeof(CombatBaselineCapture))); stats.TryGetStat(type, out stat); }
            stat.ClearAllModifiers(); stat.BaseValue = value;
        }

        private void StartCaseCast()
        {
            if (Scenario == "attacking-target") _case.enemyAttackStarted = _enemy.GetComponent<EnemyAttackController>().TryStartAttack(_owner.transform);
            _case.castToken = _case.name + ":" + _attempt + ":" + (_runner.EditorAcceptedCastSequence + 1);
            _runner.TryCast(); _case.acceptedCasts = _runner.EditorAcceptedCastSequence;
            _case.castToken = _case.name + ":" + _attempt + ":" + _runner.EditorAcceptedCastSequence;
            _case.recipeDuration = _runner.EditorRecipeDuration;
            if (_poseComparison && !_crowdReview && !_cameraReview) _frameLimit = Mathf.Max(CaptureFrames, CastFrame + Mathf.CeilToInt(_case.recipeDuration * 60) + 30);
        }

        private void Update()
        {
            if (_hurtReview && !_finished && _fixture != null) { UpdateHurtCase(); return; }
            if (_cameraReview && !_finished && _fixture != null) { UpdateCameraCase(); return; }
            if (_crowdReview && !_finished && _fixture != null) { UpdateCrowdCase(); return; }
            if (_flashComparison && !_finished && _fixture != null)
            {
                if (_frame == CastFrame || _case.name == "rapid-on" && _frame > CastFrame && _frame < 80 && !_runner.IsCasting) StartCaseCast();
                return;
            }
            if (!_meleeCoverage || _finished || _fixture == null) return;
            // Normal input starts casts before visual LateUpdate. A LateUpdate cast can hide
            // an entire fast windup by postponing its first visible pose until the strike frame.
            if (_case.name == "anvil-air" && _frame == CastFrame - 2)
            {
                var rb = _owner.GetComponent<Rigidbody2D>(); rb.position += Vector2.up * 3;
                rb.linearVelocity = Vector2.zero; Physics2D.SyncTransforms();
            }
            if (_frame == CastFrame) StartCaseCast();
        }

        private void LateUpdate()
        {
            if (_finished || _fixture == null) return;
            try
            {
                if (!_hurtReview && !_cameraReview && !_crowdReview && !_meleeCoverage && !_flashComparison && _frame == CastFrame) StartCaseCast();
                if (Scenario == "dot" && _frame == CastFrame + 9)
                    _enemy.GetComponent<AilmentController>().ReceiveSpreadIgnite(_owner.gameObject, _owner, 3, 1.25f);
                RecordFrame();
                if (_cameraReview) RenderCameraFrame();
                else if (_crowdReview) RenderCrowdFrame();
                else
                {
                var previous = RenderTexture.active;
                try
                {
                    RenderPipeline.SubmitRenderRequest(_camera, new RenderPipeline.StandardRequest { destination = _target });
                    RenderTexture.active = _target; _image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); _image.Apply();
                    File.WriteAllBytes(Output + "/" + _case.name + "/frame-" + _frame.ToString("D3") + ".png", _image.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; }
                }
                if (++_frame < _frameLimit) return;
                CompleteCase();
            }
            catch (Exception e) { Finish(e.ToString()); }
        }

        private void RecordFrame()
        {
            if (_health != null) { _case.finalHealth = _health.CurrentHealth; _case.dead = _health.IsDead; }
            var hand = _weapon.transform.parent;
            var recoil = _enemy != null ? _enemy.GetComponent<EnemyHitRecoil>()?.DisplayRenderer : null;
            _case.frames.Add(new FrameRecord
            {
                attackBodyDegrees = _owner.GetComponent<PlayerMovementVisual>().VisibleAttackPose.Degrees,
                bodyOwned = _owner.GetComponent<PlayerMovementVisual>().AttackBodyOwner != null,
                ownerPosition = _owner.transform.position, grounded = _owner.GetComponent<PlayerMovement>().IsGrounded,
                enemyRecoilVisible = recoil != null && recoil.enabled,
                enemyRecoilPosition = recoil != null ? recoil.transform.localPosition : Vector3.zero,
                enemyRecoilScale = recoil != null ? recoil.transform.localScale : Vector3.one,
                frame = _frame, phase = _runner.EditorRecipePhase, duration = _runner.EditorRecipeDuration,
                scaledTime = Time.time, unscaledTime = Time.unscaledTime, timeScale = Time.timeScale, casting = _runner.IsCasting,
                enemyBusy = _enemy != null && _enemy.GetComponent<EnemyAttackController>().IsBusy,
                health = _health != null ? _health.CurrentHealth : 0,
                bodySprite = _body.sprite != null ? _body.sprite.name : "", targetSprite = _enemy != null && _enemy.VisualRenderer.sprite != null ? _enemy.VisualRenderer.sprite.name : "",
                bodyPosition = _body.transform.localPosition, bodyScale = _body.transform.localScale, bodyRotation = _body.transform.localEulerAngles,
                handPosition = hand.localPosition, handRotation = hand.localEulerAngles,
                weaponPosition = _weapon.transform.localPosition, weaponRotation = _weapon.transform.localEulerAngles,
                weaponVisible = _weapon.enabled && !_weapon.forceRenderingOff
            });
            if (_deathReview) RecordDeathFrame(_case.frames[_case.frames.Count - 1]);
            if (_defenseReview) RecordDefenseFrame(_case.frames[_case.frames.Count - 1]);
            if (_hurtReview) _case.frames[_case.frames.Count - 1].hitFlash = _owner.GetComponent<PlayerMovementVisual>().VisibleHurtFlash;
            if (_flashComparison) RecordHitFlashFrame(_case.frames[_case.frames.Count - 1]);
            if (_crowdReview) RecordCrowdFrame(_case.frames[_case.frames.Count - 1]);
        }

        private void ObserveResolution(SkillStepRunner runner, IDamageable target, DamageSnapshot damage, bool accepted)
        {
            _case.resolutionCount++; _case.returnedTrue |= accepted;
            bool busy = _enemy != null && _enemy.GetComponent<EnemyAttackController>().IsBusy;
            _case.busyAtDamage |= busy;
            _case.events.Add(new EventRecord { type = "TakeDamage returned " + accepted, frame = _frame, castToken = _case.castToken,
                phase = runner.EditorRecipePhase, amount = damage.TotalDamage, direct = damage.IsDirectHit, busy = busy });
        }

        private void ObserveEvent(GameplayEventContext context)
        {
            if (_case == null || _owner == null || context.Source != _owner.gameObject) return;
            if (context.Type == GameplayEventType.DamageTaken) { _case.damageTakenEvents++; _case.positiveDamage += context.Amount; }
            if (context.Type == GameplayEventType.DamageDealt) _case.damageDealtEvents++;
            if (context.Type == GameplayEventType.Evaded) _case.evadeEvents++;
            if (context.Type == GameplayEventType.EnemyKilled) { _case.kills++; _case.dead = true; _case.finalHealth = 0; }
            _case.events.Add(new EventRecord { type = context.Type.ToString(), frame = _frame, castToken = _case.castToken,
                phase = _runner != null ? _runner.EditorRecipePhase : 0, amount = context.Amount,
                direct = context.Damage != null && context.Damage.IsDirectHit,
                busy = _enemy != null && _enemy.GetComponent<EnemyAttackController>().IsBusy, eventPosition = context.Position });
        }

        private void ObserveConfirmation(ConfirmedHit hit)
        {
            if (_case == null || _owner == null || hit.Source != _owner.gameObject) return;
            _case.confirmedHits++;
            _case.events.Add(new EventRecord { type = "HitConfirmed", frame = _frame,
                castToken = hit.Contact?.Attack?.CastId.ToString(), phase = _runner.EditorRecipePhase,
                amount = hit.Damage, direct = true, eventPosition = hit.Position,
                busy = _enemy != null && _enemy.GetComponent<EnemyAttackController>().IsBusy });
        }

        private void CompleteCase()
        {
            if (Scenario == "evaded" && _case.evadeEvents == 0 && _attempt < 5)
            {
                DisposeCase(); _attempt++; BuildCase(); return;
            }
            if (!_hurtReview && !_cameraReview && !_crowdReview && _case.acceptedCasts != 1) _case.error = "Expected exactly one accepted cast.";
            else if (Scenario == "miss" && _case.resolutionCount != 0) _case.error = "Miss unexpectedly contacted a target.";
            else if (Scenario == "direct-hit" && _case.damageTakenEvents != 1) _case.error = "Direct hit was not confirmed once.";
            else if (Scenario == "evaded" && (_case.evadeEvents != 1 || _case.returnedTrue)) _case.error = "Evasion outcome not reproduced.";
            else if (Scenario == "zero-damage" && (!_case.returnedTrue || _case.damageTakenEvents != 0 || _case.finalHealth != _beforeHealth)) _case.error = "Zero-damage outcome not reproduced.";
            else if (Scenario == "lethal" && !_case.dead) _case.error = "Lethal outcome not reproduced.";
            else if (Scenario == "dot" && (_case.damageTakenEvents < 1 || _case.events.Any(x => x.type == "DamageTaken" && x.direct))) _case.error = "DoT outcome not reproduced.";
            else if (Scenario == "attacking-target" && (!_case.enemyAttackStarted || !_case.busyAtDamage || _case.damageTakenEvents != 1)) _case.error = "Attacking-enemy outcome not reproduced.";
            if (_confirmation && !_hurtReview && !_cameraReview && !_crowdReview && !_meleeCoverage && !_flashComparison && _case.error == null)
            {
                bool direct = Scenario == "direct-hit" || Scenario == "lethal" || Scenario == "attacking-target";
                if (_case.confirmedHits != (direct ? 1 : 0)) _case.error = "Unexpected direct confirmation count.";
                else if (!direct && _case.frames.Any(x => x.timeScale <= 0f)) _case.error = "Non-damaging contact or DoT triggered hit-stop.";
            }
            if (_poseComparison && !_hurtReview && !_cameraReview && !_crowdReview && !_meleeCoverage && !_flashComparison && _case.error == null)
            {
                bool bodyMoved = _case.frames.Any(x => Mathf.Abs(Mathf.DeltaAngle(0, x.bodyRotation.z)) > .01f);
                if (bodyMoved != _case.posesEnabled) _case.error = "Body pose enable/disable mismatch.";
                else if (_case.frames.Any(x => x.enemyRecoilVisible) != (_case.posesEnabled && Scenario != "miss")) _case.error = "Recoil enable/disable mismatch.";
                else if (_case.name == "zombie-recoil" && !_case.rootEnemyRenderer) _case.error = "Animator-root case did not use a root renderer.";
            }
            if (_meleeCoverage && _case.error == null)
            {
                bool missedBlink = _case.name == "blink-miss";
                if (!_case.frames.Any(x => x.attackBodyDegrees > .01f)) _case.error = "Missing preparation pose.";
                else if (!missedBlink && !_case.frames.Any(x => x.attackBodyDegrees < -.01f)) _case.error = "Missing follow-through pose.";
                else if (missedBlink && (_case.confirmedHits != 0 || _case.frames.Any(x => x.attackBodyDegrees < -.01f))) _case.error = "Missed blink invented a hit/follow-through.";
                else if (_case.frames.Last().bodyOwned || Mathf.Abs(_case.frames.Last().attackBodyDegrees) > .001f) _case.error = "Body did not clean up after cast.";
                else if (_case.name != "parry" && !missedBlink && _case.confirmedHits < 1) _case.error = "Expected a damaging melee hit.";
                else if (_case.name == "anvil-air" && _case.frames.Any(x => x.attackBodyDegrees < -.01f && !x.grounded)) _case.error = "Landing follow-through occurred in the air.";
            }
            if (_flashComparison) ValidateHitFlashCase();
            if (_crowdReview) ValidateCrowdCase();
            if (_cameraReview) ValidateCameraCase();
            if (_hurtReview) ValidateHurtCase();
            _results.Add(_case); File.WriteAllText(Output + "/" + _case.name + "/trace.json", JsonUtility.ToJson(_case, true));
            DisposeCase(); _index++; _attempt = 1; WriteStatus("running", null);
            if (_index == ActiveCases.Length) Finish(_results.Any(x => x.error != null) ? "One or more baseline cases failed validation." : null);
            else BuildCase();
        }

        private void DisposeCase()
        {
            if (_crowdReview) DisposeCrowdCase();
            if (_cameraReview) DisposeCameraCase();
            if (_hurtReview) DisposeHurtCase();
            if (_runner != null) { _runner.EditorDamageResolved -= ObserveResolution; _runner.Cancel(); }
            if (_fixture != null) Object.DestroyImmediate(_fixture);
            if (_flashComparison) DisposeHitFlashCase();
            if (_enemyData != null) Object.DestroyImmediate(_enemyData);
            if (_comparisonProfile != null) Object.DestroyImmediate(_comparisonProfile);
            _comparisonProfile = null;
            _fixture = null; _owner = null; _runner = null; _health = null; _enemy = null;
        }
        private void WriteStatus(string status, string error)
        {
            File.WriteAllText(Output + "/audit.json", JsonUtility.ToJson(new AuditResult
            {
                status = status, error = error, utc = DateTime.UtcNow.ToString("O"), skill = SkillPath,
                currentFrame = _frame, currentCase = _case?.name, pumpTicks = _pumpTicks, engineFrame = Time.frameCount,
                scene = gameObject.scene.name, playingScene = Application.IsPlaying(gameObject),
                recipe = _meleeCoverage ? "Per-case skill recipes" : "Assets/Resources/Skills/2HWeapon/Axe/LeftButton/Recipe_Cleave.asset",
                fixture = _crowdReview ? "12 APS controlled crowd stress; native 480x270, sparse screenshots excluded from warm performance summaries. Per-case frame rate and feedback metrics in traces." : _flashComparison ? "Normal/heavy/bow flash-only on-off pairs; critical, frozen, status tint/emission, local light, Animator-root enemy and repeated fast attacks. Body/recoil remain enabled in both halves. Physical=100, APS=1 except rapid=10; target HP/stun=10000. No scene edits saved."
                    : _meleeCoverage ? "Ten custom melee recipes, including real movement, ground/air Falling Anvil, valid/invalid blink. Warrior versus stationary Mold; base APS=1, physical=100, no crit, HP/stun=10000. Flat review lighting; no scene edits saved."
                    : _poseComparison
                    ? "Controlled standing normal/heavy on-off comparisons; busy Mold; Animator-root Zombie; left-facing Rogue; stationary airborne Mage miss. Physics roots constrained; base physical=100, APS=1, crit=0, target HP/stun=10000, no rewards/loot. Existing hand-stat scope and skill stat links remain active."
                    : "Stationary Warrior; physical=100, base APS=1, crit=0; Mold HP=10000 (lethal=1), no rewards/loot. Existing global hand-stat scope remains active. Poses sampled after visual LateUpdate, before camera rounding.",
                cases = _results
            }, true));
        }
        private void Finish(string error)
        {
            if (_finished) return; _finished = true;
            GameplayEventBus.EventRaised -= ObserveEvent;
            CombatHitFeedback.HitConfirmed -= ObserveConfirmation;
            EditorApplication.update -= PumpPlayerLoop;
            try { WriteStatus(error == null ? "complete" : "failed", error); }
            finally
            {
                try { DisposeCase(); }
                finally
                {
                    Time.captureFramerate = _previousCaptureRate; UnityEngine.Random.state = _randomState;
                    EditorApplication.isPaused = _previousEditorPause;
                    var view = _gameView; var previousWindow = _previousWindow; bool close = _openedGameView;
                    EditorApplication.delayCall += () => { if (close && view != null) view.Close(); if (previousWindow != null) previousWindow.Focus(); };
                    if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); }
                    if (_image != null) Object.DestroyImmediate(_image);
                    if (_floorSprite != null) Object.DestroyImmediate(_floorSprite);
                    if ((_hurtReview || _flashComparison || _crowdReview) && _material != null) Object.DestroyImmediate(_material);
                    _instance = null; _defenseReview = false; _deathReview = false; Object.DestroyImmediate(gameObject);
                }
            }
        }
        private void OnDisable() { if (!_finished) Finish("Capture interrupted by leaving Play Mode or disabling the audit."); }
    }
}
#endif
