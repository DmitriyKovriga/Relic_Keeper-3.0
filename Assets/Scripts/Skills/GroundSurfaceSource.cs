using System.Collections.Generic;
using Scripts.Combat;
using Scripts.Skills.Steps;
using Scripts.StatusEffects;
using UnityEngine;

namespace Scripts.Skills
{
    // One damage clock per target and source, regardless of how many overlapping patches touch it.
    public sealed class GroundSurfaceSource : MonoBehaviour
    {
        private sealed class Contact
        {
            public readonly HashSet<GroundSurfacePatch> Patches = new HashSet<GroundSurfacePatch>();
            public float Seconds;
            public StatusEffectController.RuntimeStatusHandle Modifier;
        }
        private readonly List<GroundSurfacePatch> _patches = new List<GroundSurfacePatch>();
        private readonly Dictionary<IDamageable, Contact> _contacts = new Dictionary<IDamageable, Contact>();
        private readonly List<KeyValuePair<IDamageable, Contact>> _contactBuffer = new List<KeyValuePair<IDamageable, Contact>>();
        private SkillStepRunner _emitter;
        private PlayerStats _owner;
        private StepEntry _damage;
        private bool _disposed, _sealed;
        public GroundSurfaceEffectSO Definition { get; private set; }
        public Material Material { get; private set; }
        public IReadOnlyList<GroundSurfacePatch> Patches => _patches;

        public static GroundSurfaceSource Create(PlayerStats owner, SkillDataSO skill, StepEntry step, int slot, GroundSurfaceEffectSO definition)
        {
            var source = new GameObject(definition.Id + " ground source").AddComponent<GroundSurfaceSource>();
            source._owner = owner; source.Definition = definition;
            source._damage = new StepEntry { DamageConversions = new List<DamageConversionRule>(definition.DamageConversions),
                ScopedStatModifiers = new List<Scripts.Stats.SerializableStatModifier>(step.ScopedStatModifiers),
                OnHitEffects = new List<SkillOnHitEffectRule>(step.OnHitEffects) };
            source._emitter = source.gameObject.AddComponent<SkillStepRunner>();
            source._emitter.InitializePulseEmitter(owner, skill, slot);
            var template = Resources.Load<Material>("Skills/1HWeapon/Wand/ElectricBeam");
            if (template != null)
            {
                source.Material = new Material(template) { name = definition.Id + " ground material" };
                if (definition.Sprite != null) source.Material.mainTexture = definition.Sprite.texture;
            }
            return source;
        }

        public void SetSealed(bool value) => _sealed = value;

        public GroundSurfacePatch Spawn(RaycastHit2D support)
        {
            foreach (var patch in _patches)
                if (patch != null && !patch.Retired && patch.Support == support.collider &&
                    Vector2.Distance(patch.transform.position, support.point) < patch.Width * .35f)
                { patch.RefreshLifetime(); return patch; }
            var created = GroundSurfacePatch.Create(this, support); _patches.Add(created); return created;
        }

        public void Enter(GroundSurfacePatch patch, IDamageable target)
        {
            if (_disposed) return;
            if (!_contacts.TryGetValue(target, out var contact))
            {
                contact = new Contact(); _contacts.Add(target, contact);
                if (Definition.ContactModifiers.Count > 0 && target is Component component &&
                    StatusEffectController.TryResolve(component.transform, out var statuses))
                    contact.Modifier = statuses.ApplyRuntimeStatusEffect(Definition.ContactModifiers, 0, StatusEffectKind.Debuff, this);
            }
            contact.Patches.Add(patch);
        }

        public void Exit(GroundSurfacePatch patch, IDamageable target)
        {
            if (!_contacts.TryGetValue(target, out var contact)) return;
            contact.Patches.Remove(patch);
            if (contact.Patches.Count != 0) return;
            _contacts.Remove(target); contact.Modifier?.Dispose();
            if (!_disposed && contact.Seconds > .00001f) Hit(target, contact.Seconds);
        }

        private void Hit(IDamageable target, float seconds)
        {
            if (target is Object obj && obj == null) return;
            if (target is Component component && !component.gameObject.activeInHierarchy) return;
            _damage.SetOverrideFloat("DamageMultiplier", Mathf.Max(0, Definition.WeaponDamagePerSecond) * seconds);
            _emitter.EmitTargetPulse(_damage, target);
        }

        private void FixedUpdate() => Tick(Time.fixedDeltaTime);
        public void Tick(float deltaTime)
        {
            if (_disposed) return;
            if (_owner == null || !_owner.gameObject.activeInHierarchy || _owner.Health == null || _owner.Health.Current <= 0)
            { Dispose(); return; }
            float dt = Mathf.Max(0, deltaTime);
            int patchCount = _patches.Count;
            for (int i = 0; !_disposed && i < Mathf.Min(patchCount, _patches.Count); i++)
                if (_patches[i] != null && !_patches[i].Retired) _patches[i].RefreshContacts();
            if (_disposed) return;
            _contactBuffer.Clear(); _contactBuffer.AddRange(_contacts);
            foreach (var pair in _contactBuffer)
            {
                float remaining = 0;
                foreach (var patch in pair.Value.Patches) if (patch != null && !patch.Retired) remaining = Mathf.Max(remaining, patch.Remaining);
                var contact = pair.Value; contact.Seconds += Mathf.Min(dt, remaining);
                float interval = Mathf.Max(.05f, Definition.DamageInterval);
                while (!_disposed && contact.Seconds >= interval - .000001f)
                { contact.Seconds = Mathf.Max(0, contact.Seconds - interval); Hit(pair.Key, interval); }
                if (_disposed) return;
            }
            for (int i = 0; !_disposed && i < Mathf.Min(patchCount, _patches.Count); i++)
                if (_patches[i] != null && !_patches[i].Retired) _patches[i].Advance(dt);
            if (_disposed) return;
            _patches.RemoveAll(patch => patch == null || patch.Retired);
            if (_sealed && _patches.Count == 0) Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var contact in _contacts.Values) contact.Modifier?.Dispose();
            _contacts.Clear();
            foreach (var patch in _patches) if (patch != null) patch.Retire();
            _patches.Clear();
            if (Material != null) { if (Application.isPlaying) Destroy(Material); else DestroyImmediate(Material); }
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
        private void OnDisable() { if (!_disposed) Dispose(); }
    }
}
