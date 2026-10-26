using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RelicKeeper.Editor.Visuals
{
    // Appearance-window drafts belong to a room, never to the disposable preview scene.
    internal sealed class CeilingBeamEditing
    {
        internal sealed class Draft
        {
            internal int[] indices;
            internal string[] names;
            internal LocalLightShaftSource.Appearance original, appearance;
            internal Vector3 originalPosition, position;
            internal bool Dirty => !appearance.Equals(original) || position != originalPosition;
        }
        private readonly Dictionary<string, List<Draft>> _rooms = new Dictionary<string, List<Draft>>();
        internal IReadOnlyList<Draft> Get(string path, WorldColorPreviewStage stage)
        {
            if (_rooms.TryGetValue(path, out var drafts)) return drafts;
            drafts = new List<Draft>();
            foreach (var source in stage.Shafts)
            {
                var indices = new List<int>(); var names = new List<string>();
                for (var t = source.transform; t.parent != null; t = t.parent)
                { indices.Add(t.GetSiblingIndex()); names.Add(t.name); }
                indices.Reverse(); names.Reverse();
                drafts.Add(new Draft { indices = indices.ToArray(), names = names.ToArray(),
                    original = source.ReadAppearance(), appearance = source.ReadAppearance(),
                    originalPosition = source.transform.localPosition, position = source.transform.localPosition });
            }
            _rooms[path] = drafts; return drafts;
        }
        internal void Apply(string path, WorldColorPreviewStage stage, bool saved)
        {
            var drafts = Get(path, stage);
            for (int i = 0; i < drafts.Count && i < stage.Shafts.Count; i++)
            {
                var source = stage.Shafts[i]; var draft = drafts[i];
                var appearance = saved ? draft.original : draft.appearance;
                var position = saved ? draft.originalPosition : draft.position;
                if (source.ReadAppearance().Equals(appearance) && source.transform.localPosition == position) continue;
                source.ApplyAppearance(appearance); source.transform.localPosition = position; stage.ReclipShaft(source);
            }
        }
        internal void Discard(string path) => _rooms.Remove(path);
        internal void ForgetIfClean(string path)
        { if (_rooms.TryGetValue(path, out var drafts) && !drafts.Any(x => x.Dirty)) _rooms.Remove(path); }
        internal bool HasChanges(string path) => _rooms.TryGetValue(path, out var drafts) && drafts.Any(x => x.Dirty);
        private static LocalLightShaftSource Resolve(GameObject room, Draft draft)
        {
            Transform t = room.transform;
            for (int i = 0; i < draft.indices.Length; i++)
            {
                if (draft.indices[i] >= t.childCount) throw new InvalidOperationException("The room's beam placement changed. Discard the preview changes to reload this room.");
                t = t.GetChild(draft.indices[i]);
                if (t.name != draft.names[i]) throw new InvalidOperationException("The room's beam placement changed. Discard the preview changes to reload this room.");
            }
            var source = t.GetComponent<LocalLightShaftSource>();
            if (source == null) throw new InvalidOperationException("This beam was removed from the room. Discard the preview changes to reload it.");
            return source;
        }
        internal void Save(string path)
        {
            if (!HasChanges(path)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode to save beam placement.");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool live = stage != null && stage.assetPath == path;
            var room = live ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
            try
            {
                var changed = _rooms[path].Where(x => x.Dirty).ToArray();
                var sources = changed.Select(x => Resolve(room, x)).ToArray();
                for (int i = 0; i < changed.Length; i++)
                {
                    var source = sources[i]; var draft = changed[i];
                    if ((!source.ReadAppearance().Equals(draft.original) || source.transform.localPosition != draft.originalPosition) &&
                        (!source.ReadAppearance().Equals(draft.appearance) || source.transform.localPosition != draft.position))
                        throw new InvalidOperationException("A beam was changed in the room after this preview opened. Discard the preview changes to reload it.");
                }
                var maps = room.GetComponentsInChildren<Tilemap>(true).Where(x => x.GetComponent<TilemapCollider2D>() != null).ToArray();
                for (int i = 0; i < changed.Length; i++)
                {
                    var source = sources[i]; var draft = changed[i];
                    if (live) Undo.RecordObjects(new UnityEngine.Object[] { source, source.transform }, "Save ceiling beam");
                    source.ApplyAppearance(draft.appearance); source.transform.localPosition = draft.position;
                    CeilingBeamVisibility.Apply(maps, source); source.PrepareForSave();
                    EditorUtility.SetDirty(source); EditorUtility.SetDirty(source.transform);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(source);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(source.transform);
                }
                PrefabUtility.SaveAsPrefabAsset(room, path, out bool success);
                if (!success) throw new InvalidOperationException("Unity could not save this room's beams. The preview changes are still available.");
                foreach (var draft in changed) { draft.original = draft.appearance; draft.originalPosition = draft.position; }
            }
            finally { if (!live) PrefabUtility.UnloadPrefabContents(room); }
        }
    }

    [InitializeOnLoad]
    internal static class CeilingBeamScenePreview
    {
        static CeilingBeamScenePreview() { LocalLightShaftSource.EditorPreviewProfile = Resolve; }
        internal static AtmosphereProfileSO Resolve(LocalLightShaftSource source, Camera camera)
        {
            if (camera.cameraType != CameraType.SceneView || EditorApplication.isPlayingOrWillChangePlaymode) return null;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.scene != source.gameObject.scene || stage.prefabContentsRoot.GetComponent<Scripts.Dungeon.RoomController>() == null) return null;
            var window = Resources.FindObjectsOfTypeAll<WorldColorProfilesWindow>().FirstOrDefault(x => x.MortfallBeamProfile != null);
            return window != null ? window.MortfallBeamProfile : AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>(DepthMistSetup.DungeonPath);
        }
    }
}
