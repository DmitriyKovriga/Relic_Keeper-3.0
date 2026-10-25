using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Scripts.Visuals.Atmosphere
{
    // Reconciles authored torch tiles/sprites with source lights. No scene scan
    // every frame: refresh on room creation, child changes and tile painting.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class TorchLightBinding : MonoBehaviour
    {
        [Serializable]
        private sealed class Entry
        {
            public Tilemap map;
            public Vector3Int cell;
            public SpriteRenderer sprite;
            public LocalLight2DSource light;
        }
        [SerializeField, HideInInspector] private List<Entry> _lights = new List<Entry>();
        private bool _dirty, _refreshing;
        public int LightCount => _lights.Count;
        public static TorchLightBinding Ensure(GameObject root)
        {
            var binding = root.GetComponent<TorchLightBinding>() ?? root.AddComponent<TorchLightBinding>();
            binding.Refresh(); return binding;
        }
        private void OnEnable()
        {
            Tilemap.tilemapTileChanged -= TilesChanged;
            Tilemap.tilemapTileChanged += TilesChanged;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
            UnityEditor.EditorApplication.update += EditorTick;
#endif
            Refresh();
        }
        private void OnDisable()
        {
            Tilemap.tilemapTileChanged -= TilesChanged;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
#endif
            foreach (var entry in _lights) if (entry.light != null) entry.light.enabled = false;
        }
        private void OnTransformChildrenChanged() => _dirty = true;
        private void TilesChanged(Tilemap map, Tilemap.SyncTile[] changes)
        {
            if (map != null && map.transform.IsChildOf(transform))
            {
                _dirty = true;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
            }
        }
        private void LateUpdate() { if (_dirty) { _dirty = false; Refresh(); } }
#if UNITY_EDITOR
        private void EditorTick() { if (!Application.isPlaying) LateUpdate(); }
#endif
        private static bool IsTorch(string name) => !string.IsNullOrEmpty(name) && name.StartsWith("torch", StringComparison.OrdinalIgnoreCase);
        public void Refresh()
        {
            if (_refreshing || !isActiveAndEnabled || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return;
            _refreshing = true;
            try
            {
                var used = new HashSet<Entry>();
                foreach (var map in GetComponentsInChildren<Tilemap>(true))
                {
                    if (map.GetComponentInParent<TorchLightBinding>() != this) continue;
                    if (!map.gameObject.activeInHierarchy || !map.TryGetComponent<TilemapRenderer>(out var renderer) || !renderer.enabled) continue;
                    foreach (var cell in map.cellBounds.allPositionsWithin)
                    {
                        var tile = map.GetTile(cell);
                        var art = map.GetSprite(cell);
                        if (tile == null || art == null || !(IsTorch(tile.name) || IsTorch(art.name))) continue;
                        var tip = new Vector3(art.bounds.center.x, art.bounds.max.y - 2f / art.pixelsPerUnit);
                        Vector3 local = map.CellToLocalInterpolated((Vector3)cell + map.tileAnchor) + map.GetTransformMatrix(cell).MultiplyPoint3x4(tip);
                        Place(map, cell, null, map.transform.TransformPoint(local), used);
                    }
                }
                foreach (var sprite in GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sprite.GetComponentInParent<TorchLightBinding>() != this) continue;
                    if (!sprite.gameObject.activeInHierarchy || !sprite.enabled || sprite.sprite == null || !IsTorch(sprite.sprite.name)) continue;
                    if (sprite.GetComponentInParent<LocalLight2DSource>() != null) continue; // A standalone torch prefab already owns its source.
                    var art = sprite.sprite;
                    var tip = new Vector3(art.bounds.center.x, art.bounds.max.y - 2f / art.pixelsPerUnit);
                    if (sprite.flipY) tip.y = -tip.y;
                    Place(null, default, sprite, sprite.transform.TransformPoint(tip), used);
                }
                for (int i = _lights.Count - 1; i >= 0; i--)
                {
                    if (used.Contains(_lights[i])) continue;
                    var light = _lights[i].light;
                    if (light != null)
                    {
                        light.enabled = false;
                        if (Application.isPlaying) Destroy(light.gameObject); else DestroyImmediate(light.gameObject);
                    }
                    _lights.RemoveAt(i);
                }
            }
            finally { _refreshing = false; }
        }
        private void Place(Tilemap map, Vector3Int cell, SpriteRenderer sprite, Vector3 position, HashSet<Entry> used)
        {
            Entry entry = _lights.Find(x => x.map == map && x.cell == cell && x.sprite == sprite);
            if (entry == null) { entry = new Entry { map = map, cell = cell, sprite = sprite }; _lights.Add(entry); }
            if (entry.light == null)
            {
                var source = new GameObject(map != null ? $"Torch {cell.x}, {cell.y}" : "Torch " + sprite.name);
                SceneManager.MoveGameObjectToScene(source, gameObject.scene);
                source.transform.SetParent(map != null ? map.transform : sprite.transform, false);
                entry.light = source.AddComponent<LocalLight2DSource>();
            }
            entry.light.enabled = true;
            entry.light.transform.position = position;
            entry.light.Configure(0.32f, 3.6f);
            // Cell/position-derived phase stays stable across loads and previews.
            float phase = Mathf.Repeat(position.x * 13.371f + position.y * 31.147f, 101f) + 0.123f;
            entry.light.ConfigureFlame(phase);
            used.Add(entry);
        }
    }
}
