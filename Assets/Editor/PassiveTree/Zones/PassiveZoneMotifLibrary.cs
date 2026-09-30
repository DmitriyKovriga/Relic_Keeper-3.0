using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor.PassiveTree
{
    [Serializable]
    public sealed class ZoneMotifNode
    {
        public int orbit;
        public float angle;
        public bool notable;
        public bool port;
    }

    [Serializable]
    public sealed class ZoneMotifEdge
    {
        public int a;
        public int b;
    }

    [Serializable]
    public sealed class ZoneMotif
    {
        public string id;
        public float[] radii;
        public ZoneMotifNode[] nodes;
        public ZoneMotifEdge[] edges;
    }

    public static class PassiveZoneMotifLibrary
    {
        public const string AssetPath = "Assets/Editor/PassiveTree/Zones/PassiveZoneMotifs.json";

        [Serializable]
        sealed class Catalog
        {
            public int schema;
            public int sourceGroups;
            public int sourceComponents;
            public ZoneMotif[] motifs;
        }

        static Catalog _catalog;
        static string _text;

        public static IReadOnlyList<ZoneMotif> Motifs
        {
            get
            {
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
                if (asset == null)
                    return Array.Empty<ZoneMotif>();
                if (_catalog == null || _text != asset.text)
                {
                    _text = asset.text;
                    _catalog = JsonUtility.FromJson<Catalog>(_text);
                }
                return _catalog != null && _catalog.schema == 1 && _catalog.motifs != null
                    ? _catalog.motifs : Array.Empty<ZoneMotif>();
            }
        }

        public static bool Matches(ZoneMotif motif, ZonePieceKind kind)
        {
            int count = motif.nodes.Length;
            int orbits = 0;
            foreach (float radius in motif.radii)
                if (radius > 0f) orbits++;
            return kind switch
            {
                ZonePieceKind.Small => count >= 3 && count <= 5 && orbits == 1,
                ZonePieceKind.Medium => count >= 4 && count <= 8,
                ZonePieceKind.Large => count >= 7 && count <= 12,
                ZonePieceKind.Complex => count >= 5 && count <= 12 && orbits >= 2,
                _ => false
            };
        }
    }
}
