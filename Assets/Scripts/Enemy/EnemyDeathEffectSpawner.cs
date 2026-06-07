using System.Collections.Generic;
using UnityEngine;
using Scripts.Visuals;

namespace Scripts.Enemies
{
    public static class EnemyDeathEffectSpawner
    {
        private const int EnemyLayer = 7;
        private const int GroundLayerMask = 1 << 6;
        private const float ImpactMarkChance = 0.06f;
        private const float ChunkMaskOverlap = 1.03f;
        private const float BurstForceMultiplier = 1.28f;
        private const float PixelStep = 1f / EnemyDeathVisualFactory.PixelsPerUnit;

        public static void Spawn(EnemyEntity entity, SpriteRenderer sourceRenderer)
        {
            if (entity == null || sourceRenderer == null || sourceRenderer.sprite == null)
                return;

            EnemyDataSO data = entity.Data;
            EnemyDeathEffectConfig config = data != null ? data.DeathEffect : null;
            if (config == null || !config.Enabled)
                return;

            Bounds spriteBounds = sourceRenderer.sprite.bounds;
            Bounds visualWorldBounds = sourceRenderer.bounds;
            Collider2D bodyCollider = entity.GetComponent<Collider2D>();
            Bounds anchorBounds = bodyCollider != null ? bodyCollider.bounds : visualWorldBounds;
            Vector2 deathBasePosition = ResolveGroundAnchor(anchorBounds, visualWorldBounds, entity.transform.position);
            Vector2 burstCenter = new Vector2(anchorBounds.center.x, Mathf.Lerp(anchorBounds.center.y, visualWorldBounds.center.y, 0.35f));
            Vector2 burstBias = new Vector2(Random.Range(-0.28f, 0.28f), Random.Range(0.08f, 0.22f));
            float bloodRadius = Mathf.Max(
                Mathf.Max(anchorBounds.size.x * 1.1f, visualWorldBounds.size.x * 0.9f),
                0.7f);
            Transform roomRoot = entity.transform.parent;
            float remainsAnchorY = deathBasePosition.y;

            SpawnBodyChunks(config, sourceRenderer, spriteBounds, remainsAnchorY, roomRoot, burstCenter, burstBias);
            SpawnGroundMeat(config, deathBasePosition, remainsAnchorY, roomRoot, burstCenter, burstBias);
            SpawnGroundBloodLine(config, deathBasePosition, remainsAnchorY, roomRoot, bloodRadius);
            SpawnGroundDrips(config, deathBasePosition, remainsAnchorY, roomRoot, bloodRadius);
            SpawnWallSpatter(config, burstCenter, deathBasePosition, remainsAnchorY, roomRoot, bloodRadius);
        }

        public static void SpawnSurfacePixelMark(Vector2 position, Vector2 surfaceNormal, EnemyDeathEffectConfig config, float anchorY, Transform parent = null, int localOffset = 0)
        {
            if (config == null || Random.value > ImpactMarkChance)
                return;

            GameObject mark = new GameObject("EnemyImpactPixelMark");
            mark.layer = EnemyLayer;
            bool isWall = Mathf.Abs(surfaceNormal.x) > Mathf.Abs(surfaceNormal.y);
            float outwardOffset = isWall ? 0.008f : 0.004f;
            float downwardBias = isWall ? -0.012f : -0.004f;
            mark.transform.position = SnapToPixelGrid(new Vector3(
                position.x + surfaceNormal.x * outwardOffset,
                position.y + surfaceNormal.y * outwardOffset + downwardBias,
                0f));
            if (parent != null)
                mark.transform.SetParent(parent, true);

            float rotation = isWall
                ? (surfaceNormal.x > 0f ? 90f : -90f) + Random.Range(-8f, 8f)
                : Random.Range(-4f, 4f);
            mark.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            mark.transform.localScale = Vector3.one;

            SpriteRenderer renderer = mark.AddComponent<SpriteRenderer>();
            renderer.sprite = isWall ? EnemyDeathVisualFactory.GetRandomWallDripSprite() : EnemyDeathVisualFactory.GetRandomGroundPuddleSprite();
            renderer.color = GetBloodPixelColor(config.BloodColor, 1f);
            ConfigureRemainsSorter(mark, anchorY, localOffset + (isWall ? 2 : 0));

            EnemyDeathDecal decal = mark.AddComponent<EnemyDeathDecal>();
            decal.Initialize(config.Lifetime, Mathf.Min(config.FadeDuration + 1.5f, config.Lifetime));
        }

        private static void SpawnBodyChunks(EnemyDeathEffectConfig config, SpriteRenderer sourceRenderer, Bounds spriteBounds, float remainsAnchorY, Transform parent, Vector2 burstCenter, Vector2 burstBias)
        {
            int pieceCount = Mathf.Max(1, config.ChunkCount);
            List<Rect> rects = GenerateChunkLayout(pieceCount);
            bool flipX = sourceRenderer.flipX;

            for (int i = 0; i < rects.Count; i++)
            {
                Rect rect = rects[i];
                int localOffset = i * 4;
                Vector2 localSize = new Vector2(rect.width * spriteBounds.size.x, rect.height * spriteBounds.size.y);
                Vector2 localCenter = new Vector2(
                    spriteBounds.min.x + (rect.x + rect.width * 0.5f) * spriteBounds.size.x,
                    spriteBounds.min.y + (rect.y + rect.height * 0.5f) * spriteBounds.size.y);

                localCenter.x += Random.Range(-localSize.x * 0.06f, localSize.x * 0.06f);
                localCenter.y += Random.Range(-localSize.y * 0.06f, localSize.y * 0.06f);

                if (flipX)
                    localCenter.x = -localCenter.x;

                Vector3 worldPosition = sourceRenderer.transform.position + (Vector3)localCenter;
                GameObject fragment = CreateRoot("EnemyBodyChunk", worldPosition, parent);

                Rigidbody2D rb = fragment.AddComponent<Rigidbody2D>();
                ConfigurePhysics(rb, config);

                BoxCollider2D collider = fragment.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(Mathf.Max(0.05f, localSize.x * 0.76f), Mathf.Max(0.05f, localSize.y * 0.76f));

                CreateMask(fragment.transform, localSize * ChunkMaskOverlap, remainsAnchorY, localOffset);
                CreateMaskedSprite(fragment.transform, sourceRenderer, -localCenter, remainsAnchorY, localOffset, SpriteMaskInteraction.VisibleInsideMask);

                if (Random.value <= 0.58f)
                {
                    Color overlayColor = Color.Lerp(config.GoreColor, config.BloodColor, Random.Range(0.18f, 0.34f));
                    overlayColor.a = Random.Range(0.22f, 0.38f);
                    CreateMaskedSprite(fragment.transform, sourceRenderer, -localCenter, remainsAnchorY, localOffset + 1, SpriteMaskInteraction.VisibleInsideMask, overlayColor);
                }

                EnemyDeathFragment deathFragment = fragment.AddComponent<EnemyDeathFragment>();
                deathFragment.Initialize(config, remainsAnchorY, localOffset, 1f, 1f, false);

                Vector2 launch = BuildBurstVector(worldPosition, burstCenter, burstBias, config.ChunkHorizontalForce, config.ChunkVerticalForce, BurstForceMultiplier, 0.42f);
                rb.AddForce(launch, ForceMode2D.Impulse);
            }
        }

        private static void SpawnGroundMeat(EnemyDeathEffectConfig config, Vector2 basePosition, float remainsAnchorY, Transform parent, Vector2 burstCenter, Vector2 burstBias)
        {
            int chunkCount = Mathf.Clamp(Mathf.RoundToInt(config.ChunkCount * 0.45f), 2, 4);
            for (int i = 0; i < chunkCount; i++)
            {
                Vector2 offset = new Vector2(Random.Range(-0.18f, 0.18f), Random.Range(-0.03f, 0.04f));
                GameObject fragment = CreateRoot("EnemyGroundMeat", basePosition + offset, parent);
                fragment.transform.localScale = Vector3.one * Random.Range(0.3f, 0.56f);

                SpriteRenderer renderer = fragment.AddComponent<SpriteRenderer>();
                renderer.sprite = EnemyDeathVisualFactory.GetRandomGoreSprite();
                renderer.color = Color.Lerp(config.GoreColor, config.BloodColor, Random.Range(0.25f, 0.52f));
                ConfigureRemainsSorter(fragment, remainsAnchorY, 10 + i);

                Rigidbody2D rb = fragment.AddComponent<Rigidbody2D>();
                ConfigurePhysics(rb, config);

                CircleCollider2D collider = fragment.AddComponent<CircleCollider2D>();
                collider.radius = 0.18f;

                EnemyDeathFragment deathFragment = fragment.AddComponent<EnemyDeathFragment>();
                deathFragment.Initialize(config, remainsAnchorY, 10 + i, 0.75f, 1.35f, false);

                Vector2 force = BuildBurstVector(fragment.transform.position, burstCenter, burstBias, config.ChunkHorizontalForce * 0.68f, config.ChunkVerticalForce * 0.54f, 1.12f, 0.34f);
                rb.AddForce(force, ForceMode2D.Impulse);
            }
        }

        private static void SpawnGroundBloodLine(EnemyDeathEffectConfig config, Vector2 basePosition, float remainsAnchorY, Transform parent, float bloodRadius)
        {
            float clampedRadius = Mathf.Max(0.42f, bloodRadius);
            float[] offsets =
            {
                0f,
                -clampedRadius * 0.55f,
                clampedRadius * 0.55f,
                -clampedRadius * 1.05f,
                clampedRadius * 1.05f
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 spawnPosition = basePosition + new Vector2(offsets[i], 0.036f + Random.Range(-0.004f, 0.012f));
                GameObject mark = CreateRoot("EnemyGroundBlood", spawnPosition, parent);
                mark.transform.localScale = Vector3.one;
                mark.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-2f, 2f));

                SpriteRenderer renderer = mark.AddComponent<SpriteRenderer>();
                renderer.sprite = EnemyDeathVisualFactory.GetRandomGroundPuddleSprite();
                renderer.color = GetBloodPixelColor(config.BloodColor, i == 0 ? 0.98f : Random.Range(0.88f, 0.96f));
                ConfigureRemainsSorter(mark, remainsAnchorY, 20 + i);

                EnemyDeathDecal decal = mark.AddComponent<EnemyDeathDecal>();
                decal.Initialize(config.Lifetime, Mathf.Min(config.FadeDuration + 1.5f, config.Lifetime));
            }
        }

        private static void SpawnGroundDrips(EnemyDeathEffectConfig config, Vector2 basePosition, float remainsAnchorY, Transform parent, float bloodRadius)
        {
            float clampedRadius = Mathf.Max(0.42f, bloodRadius);
            float[] offsets =
            {
                -clampedRadius * 0.86f,
                -clampedRadius * 0.34f,
                0f,
                clampedRadius * 0.34f,
                clampedRadius * 0.86f
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 pos = basePosition + new Vector2(offsets[i], Random.Range(-0.018f, 0.002f));
                GameObject mark = CreateRoot("EnemyGroundDrip", pos, parent);
                mark.transform.localScale = Vector3.one;
                mark.transform.rotation = Quaternion.identity;

                SpriteRenderer renderer = mark.AddComponent<SpriteRenderer>();
                renderer.sprite = EnemyDeathVisualFactory.GetRandomWallDripSprite();
                renderer.color = GetBloodPixelColor(config.BloodColor, Random.Range(0.86f, 0.98f));
                ConfigureRemainsSorter(mark, remainsAnchorY, 30 + i);

                EnemyDeathDecal decal = mark.AddComponent<EnemyDeathDecal>();
                decal.Initialize(config.Lifetime, Mathf.Min(config.FadeDuration + 1.5f, config.Lifetime));
            }
        }

        private static void SpawnWallSpatter(EnemyDeathEffectConfig config, Vector2 burstCenter, Vector2 basePosition, float remainsAnchorY, Transform parent, float bloodRadius)
        {
            TrySpawnWallSide(config, burstCenter, basePosition, remainsAnchorY, parent, bloodRadius, -1f);
            TrySpawnWallSide(config, burstCenter, basePosition, remainsAnchorY, parent, bloodRadius, 1f);
        }

        private static void TrySpawnWallSide(EnemyDeathEffectConfig config, Vector2 burstCenter, Vector2 basePosition, float remainsAnchorY, Transform parent, float bloodRadius, float direction)
        {
            Vector2 origin = burstCenter + new Vector2(0f, 0.08f);
            float castDistance = Mathf.Max(bloodRadius * 1.8f, 1.2f);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * direction, castDistance, GroundLayerMask);
            if (hit.collider == null)
                return;

            int count = Random.Range(4, 8);
            for (int i = 0; i < count; i++)
            {
                float verticalOffset = Random.Range(0.02f, 0.42f);
                Vector2 pos = new Vector2(
                    hit.point.x + hit.normal.x * 0.008f,
                    Mathf.Max(basePosition.y + 0.02f, hit.point.y + verticalOffset));

                GameObject mark = CreateRoot("EnemyWallBlood", pos, parent);
                mark.transform.rotation = Quaternion.Euler(0f, 0f, direction > 0f ? 90f : -90f);
                mark.transform.localScale = Vector3.one;

                SpriteRenderer renderer = mark.AddComponent<SpriteRenderer>();
                renderer.sprite = EnemyDeathVisualFactory.GetRandomWallDripSprite();
                renderer.color = GetBloodPixelColor(config.BloodColor, Random.Range(0.88f, 1f));
                ConfigureRemainsSorter(mark, remainsAnchorY, 40 + i);

                EnemyDeathDecal decal = mark.AddComponent<EnemyDeathDecal>();
                decal.Initialize(config.Lifetime, Mathf.Min(config.FadeDuration + 1.5f, config.Lifetime));
            }
        }

        private static GameObject CreateRoot(string name, Vector2 position, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.layer = EnemyLayer;
            go.transform.position = SnapToPixelGrid(new Vector3(position.x, position.y, 0f));
            if (parent != null)
                go.transform.SetParent(parent, true);
            return go;
        }

        private static void ConfigurePhysics(Rigidbody2D rb, EnemyDeathEffectConfig config)
        {
            rb.gravityScale = config.GravityScale;
            rb.freezeRotation = false;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.linearDamping = config.ChunkLinearDamping;
            rb.angularDamping = config.ChunkAngularDamping;
            rb.angularVelocity = Random.Range(-240f, 240f);
        }

        private static SpriteMask CreateMask(Transform parent, Vector2 localSize, float anchorY, int localOffset)
        {
            int sortingLayerId = RemainsLayerId;
            int sortingOrder = ResolveRemainsOrder(anchorY, localOffset);
            GameObject maskObject = new GameObject("Mask");
            maskObject.transform.SetParent(parent, false);
            maskObject.transform.localScale = new Vector3(Mathf.Max(0.06f, localSize.x), Mathf.Max(0.06f, localSize.y), 1f);

            SpriteMask mask = maskObject.AddComponent<SpriteMask>();
            mask.sprite = EnemyDeathVisualFactory.GetRandomChunkMaskSprite();
            mask.isCustomRangeActive = true;
            mask.frontSortingLayerID = sortingLayerId;
            mask.backSortingLayerID = sortingLayerId;
            mask.frontSortingOrder = sortingOrder + 2;
            mask.backSortingOrder = sortingOrder - 1;
            maskObject.transform.localPosition = new Vector3(Random.Range(-0.03f, 0.03f), Random.Range(-0.03f, 0.03f), 0f);
            maskObject.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-28f, 28f));
            return mask;
        }

        private static void CreateMaskedSprite(
            Transform parent,
            SpriteRenderer sourceRenderer,
            Vector2 visualLocalOffset,
            float anchorY,
            int sortingOffset,
            SpriteMaskInteraction maskInteraction,
            Color? tintOverride = null)
        {
            GameObject visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.localPosition = new Vector3(visualLocalOffset.x, visualLocalOffset.y, 0f);

            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sourceRenderer.sprite;
            renderer.flipX = sourceRenderer.flipX;
            renderer.flipY = sourceRenderer.flipY;
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            ApplyRemainsRenderer(renderer, anchorY, sortingOffset);
            renderer.maskInteraction = maskInteraction;
            renderer.color = tintOverride ?? sourceRenderer.color;
        }

        private static Vector2 BuildBurstVector(Vector2 worldPosition, Vector2 burstCenter, Vector2 burstBias, float horizontalStrength, float verticalStrength, float multiplier, float randomFactor)
        {
            Vector2 radial = (worldPosition - burstCenter) + burstBias;
            if (radial.sqrMagnitude < 0.0001f)
                radial = new Vector2(Random.Range(-1f, 1f), Random.Range(0.28f, 1f));
            radial.Normalize();

            float horizontal = radial.x * Random.Range(horizontalStrength * 0.72f, horizontalStrength) * multiplier;
            horizontal += Random.Range(-randomFactor * 0.22f, randomFactor * 0.22f);

            float upwardBias = Mathf.Max(0.42f, radial.y + 0.92f);
            float vertical = upwardBias * Random.Range(verticalStrength * 0.78f, verticalStrength) * multiplier;
            vertical += Random.Range(0f, randomFactor * 0.18f);
            return new Vector2(horizontal, vertical);
        }

        private static List<Rect> GenerateChunkLayout(int pieceCount)
        {
            int targetCount = Mathf.Clamp(pieceCount, 4, 6);
            float xMid = Random.Range(0.43f, 0.57f);
            float yMid = Random.Range(0.44f, 0.58f);

            List<Rect> rects = new List<Rect>
            {
                new Rect(0.02f, yMid - 0.08f, xMid + 0.05f, 0.98f - (yMid - 0.08f)),
                new Rect(xMid - 0.08f, yMid - 0.1f, 0.98f - (xMid - 0.08f), 0.98f - (yMid - 0.1f)),
                new Rect(0.02f, 0.02f, xMid + 0.02f, yMid + 0.02f),
                new Rect(xMid - 0.04f, 0.02f, 0.98f - (xMid - 0.04f), yMid - 0.02f)
            };

            if (targetCount >= 5)
                rects.Add(new Rect(Mathf.Clamp01(xMid - 0.12f), Mathf.Clamp01(yMid - 0.16f), 0.28f, 0.26f));
            if (targetCount >= 6)
                rects.Add(new Rect(Mathf.Clamp01(xMid - 0.21f), Mathf.Clamp01(yMid - 0.03f), 0.18f, 0.18f));

            return rects;
        }

        private static Color GetBloodPixelColor(Color baseColor, float alpha)
        {
            Color tint = Random.value < 0.6f
                ? Color.Lerp(baseColor, new Color(0.18f, 0.02f, 0.03f, 1f), Random.Range(0.12f, 0.28f))
                : Color.Lerp(baseColor, new Color(0.08f, 0.01f, 0.015f, 1f), Random.Range(0.3f, 0.55f));
            tint.a = alpha;
            return tint;
        }

        private static Vector2 ResolveGroundAnchor(Bounds anchorBounds, Bounds visualWorldBounds, Vector3 fallbackPosition)
        {
            float centerX = anchorBounds.center.x;
            float castOriginY = Mathf.Max(anchorBounds.max.y, visualWorldBounds.max.y) + 0.25f;
            Vector2 castOrigin = new Vector2(centerX, castOriginY);
            RaycastHit2D hit = Physics2D.Raycast(castOrigin, Vector2.down, 6f, GroundLayerMask);
            if (hit.collider != null)
                return new Vector2(centerX, hit.point.y + 0.02f);

            return new Vector2(centerX, anchorBounds.min.y + 0.02f);
        }

        private static int RemainsLayerId => SortingLayer.NameToID(WorldRenderSorting.LayerWorld);

        private static int ResolveRemainsOrder(float anchorY, int localOffset = 0)
        {
            return WorldRenderSorting.ResolveOrder(RenderDepthCategory.EnemyRemains, anchorY, localOffset);
        }

        private static void ConfigureRemainsSorter(GameObject root, float anchorY, int localOffset)
        {
            WorldRenderSorting.ConfigureSorter(root, RenderDepthCategory.EnemyRemains, anchorY, localOffset, staticAnchor: true);
        }

        private static void ApplyRemainsRenderer(SpriteRenderer renderer, float anchorY, int localOffset)
        {
            if (renderer == null)
                return;

            renderer.sortingLayerName = WorldRenderSorting.LayerWorld;
            renderer.sortingOrder = ResolveRemainsOrder(anchorY, localOffset);
        }

        private static Vector3 SnapToPixelGrid(Vector3 worldPosition)
        {
            worldPosition.x = Mathf.Round(worldPosition.x / PixelStep) * PixelStep;
            worldPosition.y = Mathf.Round(worldPosition.y / PixelStep) * PixelStep;
            return worldPosition;
        }
    }
}
