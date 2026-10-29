#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Dungeon;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Stats;
using Scripts.Visuals;
using Scripts.Visuals.Atmosphere;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RelicKeeper.Diagnostics
{
    public sealed partial class CombatDecorationStressProbe
    {
        private bool _mortfallAudit;
        [Serializable] private sealed class RoomCase
        {
            public string room, skill, mode;
            public int enemies, casts, peakProjectiles, remainingProjectiles, remainingEnemyProjectiles, gpuSamples, frames;
            public float damage, medianMs, p95Ms, p99Ms, medianGpuMs, p95GpuMs;
            public long peakGcBytes;
            public bool passed, pauseStable, roomClean;
            public Vector3 playerPosition;
        }
        [Serializable] private sealed class RoomReport
        {
            public string status, cpu, gpu, api, unity, limits;
            public RoomCase[] cases;
        }
        public static void BeginMortfallAudit()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in the dedicated development audit player.");
            if (Application.isEditor && FindFirstObjectByType<PlayerStats>() != null)
                throw new InvalidOperationException("Use the dedicated audit player so enemy AI cannot target the live Hub character.");
            var probe = new GameObject("Temporary Mortfall combat audit").AddComponent<CombatDecorationStressProbe>();
            probe._mortfallAudit = true;
        }

        private IEnumerator RunMortfallAudit()
        {
            var camera = Keep(new GameObject("Mortfall audit camera")).AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 270f / 48;
            camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.10f, .14f, .15f);
            var atmosphere = camera.gameObject.AddComponent<AtmosphereController>();
            var ambient = Keep(new GameObject("Audit ambient light")).AddComponent<Light2D>();
            ambient.lightType = Light2D.LightType.Global; ambient.intensity = 1;
            ambient.targetSortingLayers = SortingLayer.layers.Select(x => x.id).ToArray();
            var dungeon = Resources.Load<DungeonDataSO>("Dungeons/MortFallDungeonSO");
            atmosphere.ApplyDungeonPresentation(dungeon.ColorProfile, dungeon.DepthMistProfile);
            var background = Keep(new GameObject("Audit dungeon background")).AddComponent<SpriteRenderer>();
            background.sprite = dungeon.LoadBackgroundSprite(); background.drawMode = SpriteDrawMode.Tiled;
            background.size = new Vector2(120, 120); background.sortingLayerName = "Background"; background.sortingOrder = -1000;
            _particleMaterial = new Material(Shader.Find("RelicKeeper/Sprites/World Color Unlit"));
            background.sharedMaterial = _particleMaterial;
            background.gameObject.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Background);
            var layers = LayeredParallaxGroup.ConfigureDungeon(background, dungeon.ParallaxSilhouette);
            _target = new RenderTexture(480, 270, 24); _target.Create();
            var random = UnityEngine.Random.state;
            var results = new List<RoomCase>();
            var timings = new FrameTiming[1];
            string[] skills = { "2HWeapon/Bow/NearFan/NearFanSkill", "2HWeapon/Chakram/rb/CircularSaw/CircularSawSkill", "2HWeapon/Staff/Lightning/LightningRod/LightningRodSkill" };
            foreach (string roomName in new[] { "MortfallRoom_001", "MortfallRoom_006" })
            foreach (string skillPath in skills)
            foreach (bool reduced in new[] { false, true })
            {
                UnityEngine.Random.InitState(1137);
                GameplayPresentationSettings.SetReducedCombatEffects(reduced);
                var roomObject = Keep(Instantiate(Resources.Load<GameObject>("Prefabs/Dungeon/MineDungeon/" + roomName)));
                roomObject.transform.position = Origin;
                var room = roomObject.GetComponent<RoomController>(); room.enabled = false;
                var ownerObject = Keep(CreateRoomAuditPlayer()); var owner = ownerObject.GetComponent<PlayerStats>();
                var source = Resources.Load<SkillDataSO>("Skills/" + skillPath);
                var skill = Instantiate(source); skill.Cooldown = 0; // stress-only clone
                var runner = ownerObject.AddComponent<SkillStepRunner>(); runner.Initialize(owner, skill);
                var spawners = roomObject.GetComponentsInChildren<EnemySpawner>(true);
                Physics2D.SyncTransforms();
                Vector2 supported = default; bool found = false;
                foreach (var spawner in spawners.OrderBy(x => Vector3.SqrMagnitude(x.transform.position - room.PlayerSpawnPosition)))
                    if (TryFindRoomAuditPosition(spawner.transform.position, out supported)) { found = true; break; }
                if (!found) throw new InvalidOperationException("No clear supported audit position in " + roomName);
                Vector3 position = supported;
                ownerObject.transform.position = position;
                atmosphere.BindPlayer(ownerObject.transform); atmosphere.ResetDepthMistAnchor(position);
                camera.transform.position = position + new Vector3(2, 1, -10);
                background.transform.position = position + Vector3.forward * 20; layers.ResetAnchor(camera.transform.position);
                foreach (var spawner in spawners) spawner.Spawn(room.RoomLevel);
                yield return null;
                var enemies = roomObject.GetComponentsInChildren<EnemyEntity>();
                foreach (var enemy in enemies)
                {
                    var stats = enemy.GetComponent<EnemyStats>();
                    if (stats.TryGetStat(StatType.MaxHealth, out var hp)) hp.BaseValue = 1000000;
                    if (stats.TryGetStat(StatType.StunThreshold, out var stun)) stun.BaseValue = 1000000;
                    enemy.GetComponent<EnemyHealth>().Initialize();
                }
                atmosphere.RefreshColorGroups();
                var result = new RoomCase { room = roomName, skill = source.ID, mode = reduced ? "reduced" : "normal", enemies = enemies.Length, playerPosition = position };
                float initialHealth = enemies.Sum(x => x.GetComponent<EnemyHealth>().CurrentHealth);
                var frameMs = new List<float>(); var gpuMs = new List<float>();
                var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
                float started = Time.unscaledTime; bool captured = false;
                while (Time.unscaledTime - started < 6)
                {
                    if (!runner.IsCasting)
                    {
                        var prior = runner.AcceptedAttack; runner.TryCast();
                        if (!ReferenceEquals(prior, runner.AcceptedAttack)) result.casts++;
                    }
                    RenderRoomAuditFrame(camera);
                    FrameTimingManager.CaptureFrameTimings();
                    if (Time.unscaledTime - started > 1)
                    {
                        frameMs.Add(Time.unscaledDeltaTime * 1000);
                        if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                            gpuMs.Add((float)timings[0].gpuFrameTime);
                        if (gc.Valid) result.peakGcBytes = Math.Max(result.peakGcBytes, gc.LastValue);
                    }
                    if (!captured && Time.unscaledTime - started > 2)
                    { SaveRoomAuditImage(roomName + "-" + source.ID + "-" + result.mode); captured = true; }
                    // Sample counts at 5Hz, outside most measured frames.
                    if (Time.frameCount % 12 == 0)
                        result.peakProjectiles = Math.Max(result.peakProjectiles, FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Length);
                    yield return null;
                }
                gc.Dispose();
                result.damage = initialHealth - enemies.Where(x => x != null).Sum(x => x.GetComponent<EnemyHealth>().CurrentHealth);
                frameMs.Sort(); gpuMs.Sort(); result.frames = frameMs.Count; result.gpuSamples = gpuMs.Count;
                result.medianMs = Percentile(frameMs, .5f); result.p95Ms = Percentile(frameMs, .95f); result.p99Ms = Percentile(frameMs, .99f);
                result.medianGpuMs = Percentile(gpuMs, .5f); result.p95GpuMs = Percentile(gpuMs, .95f);
                // Actual pause service freezes scaled effect clocks and rejects casts.
                using (GamePauseService.Acquire(GamePauseReason.PauseMenu))
                {
                    float time = Time.time; var prior = runner.AcceptedAttack;
                    runner.TryCast(); yield return new WaitForSecondsRealtime(.15f);
                    result.pauseStable = Mathf.Approximately(Time.time, time) && ReferenceEquals(prior, runner.AcceptedAttack);
                }
                runner.Cancel();
                SkillProjectile.DespawnAll(); // DungeonController.LoadCurrentRoom uses this same cleanup.
                Destroy(ownerObject); Destroy(roomObject); Destroy(skill);
                HitStopService.CancelCurrent();
                // Real room transitions remove transient roots separately from the room prefab.
                // Allow finite projectiles to expire and check whether owner-owned effects clean up.
                float cleanup = Time.unscaledTime;
                while (Time.unscaledTime - cleanup < 6)
                { RenderRoomAuditFrame(camera); yield return null; }
                result.remainingProjectiles = FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Length;
                result.remainingEnemyProjectiles = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length;
                result.roomClean = roomObject == null && ownerObject == null && result.remainingProjectiles == 0 && result.remainingEnemyProjectiles == 0;
                result.passed = result.enemies > 0 && result.casts > 0 && result.damage > 0 && result.pauseStable && result.roomClean;
                results.Add(result); WriteRoomAudit(results, "running");
            }
            UnityEngine.Random.state = random;
            WriteRoomAudit(results, results.All(x => x.passed) ? "complete" : "failed");
            Destroy(gameObject);
        }

        private GameObject CreateRoomAuditPlayer()
        {
            var go = new GameObject("Mortfall audit stationary player");
            var art = go.AddComponent<SpriteRenderer>();
            var character = Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior");
            art.sprite = character.Portrait;
            var stats = go.AddComponent<PlayerStats>(); stats.Initialize(character);
            go.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            var visuals = new GameObject("Visuals").transform; visuals.SetParent(go.transform, false);
            var hand = new GameObject("HandPivot").transform; hand.SetParent(visuals, false);
            var holder = new GameObject("WeaponHolder").transform; holder.SetParent(hand, false); holder.gameObject.AddComponent<SpriteRenderer>();
            go.AddComponent<PlayerMovement>();
            go.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            stats.GetStat(StatType.MaxHealth).BaseValue = 1000000; stats.GetStat(StatType.MaxMana).BaseValue = 1000000;
            stats.GetStat(StatType.DamagePhysical).BaseValue = 100; stats.GetStat(StatType.AttackSpeed).BaseValue = 4;
            stats.GetStat(StatType.CastSpeed).AddModifier(new StatModifier(300, StatModType.PercentAdd, this));
            stats.GetStat(StatType.ProjectileCount).BaseValue = 12; stats.GetStat(StatType.AreaOfEffect).BaseValue = 200;
            stats.GetStat(StatType.CritChance).BaseValue = 0; stats.NotifyChanged(); stats.Health.RestoreFull(); stats.Mana.RestoreFull();
            return go;
        }
        private static float Percentile(List<float> samples, float fraction) => samples.Count == 0 ? -1 : samples[Mathf.Min(samples.Count - 1, (int)(samples.Count * fraction))];
        public static bool TryFindRoomAuditPosition(Vector2 anchor, out Vector2 position)
        {
            const int ground = 1 << 6;
            position = anchor;
            var reference = Physics2D.Raycast(anchor + Vector2.up, Vector2.down, 3, ground);
            if (reference.collider == null || reference.normal.y < .5f) return false;
            foreach (float x in new[] { -2f, -1f, -.5f, 0f, .5f, 1f, 2f })
            {
                var floor = Physics2D.Raycast(anchor + new Vector2(x, 1), Vector2.down, 3, ground);
                if (floor.collider == null || floor.normal.y < .5f || Mathf.Abs(floor.point.y - reference.point.y) > .2f) continue;
                Vector2 candidate = floor.point + Vector2.up * .55f;
                if (Physics2D.OverlapBoxAll(candidate, new Vector2(.45f, .85f), 0, ground).Any(c => !c.isTrigger)) continue;
                position = candidate; return true;
            }
            return false;
        }
        private void RenderRoomAuditFrame(Camera camera) => RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = _target });
        private void SaveRoomAuditImage(string name)
        {
            var previous = RenderTexture.active; var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            try { RenderTexture.active = _target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; Destroy(image); }
        }
        private void WriteRoomAudit(List<RoomCase> cases, string status) => File.WriteAllText(Path.Combine(Output, "audit.json"), JsonUtility.ToJson(new RoomReport {
            status = status, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceVersion, unity = Application.unityVersion,
            limits = "Authored room/spawners and active AI; stationary stress actor, runtime cloned skill cooldown=0, attack/cast speed 4x, ProjectileCount=12, AoE=200. 480x270 forced offscreen render. GC includes diagnostic count sampling. Not hardware input or a full player camera/HUD benchmark.", cases = cases.ToArray()
        }, true));
    }
}
#endif
