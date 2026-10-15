using System.Collections.Generic;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills
{
    public sealed class GroundSurfacePatch : MonoBehaviour
    {
        private GroundSurfaceSource _source;
        private Collider2D _support;
        private SpriteRenderer _renderer;
        private readonly HashSet<IDamageable> _contacts = new HashSet<IDamageable>();
        private readonly HashSet<IDamageable> _next = new HashSet<IDamageable>();
        private Collider2D[] _hits = new Collider2D[16];
        private static readonly ContactFilter2D EnemyFilter = new ContactFilter2D { useLayerMask = true, layerMask = 1 << 7, useTriggers = true };
        public float Remaining { get; private set; }
        public float Width { get; private set; }
        public bool Retired { get; private set; }
        public Collider2D Support => _support;

        public static GroundSurfacePatch Create(GroundSurfaceSource source, RaycastHit2D support)
        {
            GroundSurfaceGeometry.Footprint(support, source.Definition.PatchWidth, out var center, out float width);
            var host = new GameObject(source.Definition.Id + " surface");
            host.transform.SetParent(support.collider.transform, true);
            host.transform.position = center;
            host.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(-support.normal.x, support.normal.y) * Mathf.Rad2Deg);
            var patch = host.AddComponent<GroundSurfacePatch>(); patch._source = source; patch._support = support.collider;
            patch.Width = width; patch.RefreshLifetime();
            patch._renderer = host.AddComponent<SpriteRenderer>(); patch._renderer.sprite = source.Definition.Sprite;
            patch._renderer.sharedMaterial = source.Material;
            patch._renderer.sortingLayerName = WorldRenderSorting.GetSortingLayer(RenderDepthCategory.GameplayVfx);
            if (patch._renderer.sprite != null)
                host.transform.localScale = new Vector3(width / patch._renderer.sprite.bounds.size.x / Mathf.Abs(support.collider.transform.lossyScale.x),
                    1 / Mathf.Abs(support.collider.transform.lossyScale.y), 1);
            return patch;
        }

        public void RefreshLifetime() => Remaining = _source.Definition.Lifetime;

        public void RefreshContacts()
        {
            if (Retired) return;
            if (_support == null || !_support.enabled || !_support.gameObject.activeInHierarchy) { Retire(); return; }
            _next.Clear();
            Vector2 normal = transform.up;
            int count;
            while ((count = Physics2D.OverlapBox((Vector2)transform.position + normal * .12f, new Vector2(Width, .32f), transform.eulerAngles.z, EnemyFilter, _hits)) == _hits.Length)
                System.Array.Resize(ref _hits, _hits.Length * 2);
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                var enemy = hit.GetComponentInParent<EnemyStats>();
                var target = hit.GetComponentInParent<IDamageable>();
                if (enemy == null || target == null || !enemy.gameObject.activeInHierarchy) continue;
                var health = enemy.GetComponent<EnemyHealth>(); if (health != null && health.IsDead) continue;
                var body = enemy.GetComponent<Collider2D>(); Bounds bounds = body != null ? body.bounds : hit.bounds;
                Vector2 feet = new Vector2(bounds.center.x, bounds.min.y);
                float height = Vector2.Dot(feet - (Vector2)transform.position, normal);
                if (height < -.08f || height > .12f || !GroundSurfaceGeometry.TrySupport(feet, out var ground) || ground.collider != _support) continue;
                if (_next.Add(target)) _source.Enter(this, target);
            }
            System.Array.Clear(_hits, 0, count);
            foreach (var target in _contacts) if (!_next.Contains(target)) _source.Exit(this, target);
            _contacts.Clear(); foreach (var target in _next) _contacts.Add(target);
        }

        public void Advance(float deltaTime)
        {
            if (Retired) return;
            Remaining = Mathf.Max(0, Remaining - Mathf.Max(0, deltaTime));
            _renderer.color = new Color(1, 1, 1, Mathf.Clamp01(Remaining / .35f));
            _renderer.sortingOrder = WorldRenderSorting.ResolveOrder(RenderDepthCategory.GameplayVfx, transform.position.y);
            if (Remaining <= 0) Retire();
        }

        public void Retire()
        {
            if (Retired) return;
            Retired = true;
            if (_source != null) foreach (var target in _contacts) _source.Exit(this, target);
            _contacts.Clear();
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        private void OnDisable()
        {
            if (Retired) return;
            Retired = true;
            if (_source != null) foreach (var target in _contacts) _source.Exit(this, target);
            _contacts.Clear();
        }
    }
}
