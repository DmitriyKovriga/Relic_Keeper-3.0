using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RelicKeeper.Editor.Visuals
{
    // Trace from the source to each visible artwork pixel. Transparent sprite
    // margins cannot cast shadows, and light can pass underneath an overhang.
    internal static class CeilingBeamVisibility
    {
        private sealed class Artwork
        {
            internal Hash128 hash;
            internal Color32[] pixels;
            internal int width;
        }
        private static readonly Dictionary<string, Artwork> ArtCache = new Dictionary<string, Artwork>();
        private static Artwork ReadArtwork(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path))
                return new Artwork { pixels = sprite.texture.GetPixels32(), width = sprite.texture.width };
            var hash = AssetDatabase.GetAssetDependencyHash(path);
            if (ArtCache.TryGetValue(path, out var cached) && cached.hash == hash) return cached;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot read the shaft artwork: " + path);
                cached = new Artwork { hash = hash, pixels = texture.GetPixels32(), width = texture.width };
                ArtCache[path] = cached; return cached;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private sealed class Terrain
        {
            private readonly HashSet<Vector2Int> _solid = new HashSet<Vector2Int>();
            private readonly Matrix4x4 _worldToCell;
            internal Terrain(Tilemap map)
            {
                Vector3 origin = map.CellToWorld(Vector3Int.zero);
                Vector3 x = map.CellToWorld(Vector3Int.right) - origin, y = map.CellToWorld(Vector3Int.up) - origin;
                _worldToCell = new Matrix4x4(new Vector4(x.x, x.y, x.z, 0), new Vector4(y.x, y.y, y.z, 0),
                    new Vector4(0, 0, 1, 0), new Vector4(origin.x, origin.y, origin.z, 1)).inverse;
                var bounds = map.cellBounds; var tiles = map.GetTilesBlock(bounds); int i = 0;
                foreach (var cell in bounds.allPositionsWithin)
                {
                    if (tiles[i++] == null || map.GetColliderType(cell) == Tile.ColliderType.None) continue;
                    _solid.Add(new Vector2Int(cell.x, cell.y));
                }
            }
            internal bool Blocks(Vector3 from, Vector3 to)
            {
                Vector2 start = _worldToCell.MultiplyPoint3x4(from), end = _worldToCell.MultiplyPoint3x4(to);
                Vector2 delta = end - start;
                // A source exactly on a ceiling edge belongs to the side into
                // which this ray travels, rather than the tile it just left.
                start += delta.normalized * 0.0001f; delta = end - start;
                var cell = new Vector2Int(Mathf.FloorToInt(start.x), Mathf.FloorToInt(start.y));
                var destination = new Vector2Int(Mathf.FloorToInt(end.x), Mathf.FloorToInt(end.y));
                int stepX = Math.Sign(delta.x), stepY = Math.Sign(delta.y);
                float intervalX = stepX == 0 ? float.PositiveInfinity : 1 / Mathf.Abs(delta.x);
                float intervalY = stepY == 0 ? float.PositiveInfinity : 1 / Mathf.Abs(delta.y);
                float nextX = stepX == 0 ? float.PositiveInfinity : ((stepX > 0 ? cell.x + 1 : cell.x) - start.x) / delta.x;
                float nextY = stepY == 0 ? float.PositiveInfinity : ((stepY > 0 ? cell.y + 1 : cell.y) - start.y) / delta.y;
                int limit = Math.Abs(destination.x - cell.x) + Math.Abs(destination.y - cell.y) + 3;
                for (int i = 0; i < limit; i++)
                {
                    if (_solid.Contains(cell)) return true;
                    if (cell == destination) return false;
                    if (Mathf.Abs(nextX - nextY) < 0.000001f)
                    {
                        if (_solid.Contains(cell + new Vector2Int(stepX, 0)) || _solid.Contains(cell + new Vector2Int(0, stepY))) return true;
                        cell += new Vector2Int(stepX, stepY); nextX += intervalX; nextY += intervalY;
                    }
                    else if (nextX < nextY) { cell.x += stepX; nextX += intervalX; }
                    else { cell.y += stepY; nextY += intervalY; }
                }
                return true;
            }
        }

        internal static byte[] Bake(Tilemap[] maps, LocalLightShaftSource source)
        {
            var sprite = source.Artwork; if (sprite == null) return Array.Empty<byte>();
            int width = (int)sprite.rect.width, height = (int)sprite.rect.height;
            var result = new byte[width * height]; var art = ReadArtwork(sprite);
            var terrain = maps.Where(x => x != null).Select(x => new Terrain(x)).ToArray();
            Vector3 origin = source.SourcePosition;
            for (int row = source.SourcePixel.y; row < height; row++)
                for (int x = 0; x < width; x++)
                {
                    int pixel = (height - 1 - row) * width + x;
                    int artworkPixel = ((int)sprite.rect.y + height - 1 - row) * art.width + (int)sprite.rect.x + x;
                    if (art.pixels[artworkPixel].a == 0) continue;
                    var point = source.transform.TransformPoint(source.PixelToLocal(new Vector2(x + 0.5f, row + 0.5f)));
                    bool blocked = false;
                    foreach (var map in terrain) if (map.Blocks(origin, point)) { blocked = true; break; }
                    if (!blocked) result[pixel] = 255;
                }
            return result;
        }
        internal static void Apply(Tilemap[] maps, LocalLightShaftSource source) => source.ConfigureVisibility(Bake(maps, source));
    }
}
