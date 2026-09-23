using UnityEngine;
using Scripts.Visuals;

namespace Scripts.Enemies
{
    /// <summary>Budgeted, pooled death effects. Static remains use one composite renderer, not masked pieces.</summary>
    public static class EnemyDeathEffectSpawner
    {
        private const int EnemyLayer = 7;
        private const int GroundLayerMask = 1 << 6;
        private const float ChunkOutlinePixels = 1f;
        private const float MaxChunkWorldWidth = 0.62f;
        private const float MaxChunkWorldHeight = 0.58f;
        // 24 px is one world unit. Large enemies should not create screen-dominating blood pools.
        private const float MaxPuddleWorldWidth = 1.1f;
        private static bool s_collisionRulesConfigured;
        private static readonly Vector2[][] s_chunkCenters =
        {
            new[] { new Vector2(0.5f, 0.54f) },
            new[] { new Vector2(0.33f, 0.56f), new Vector2(0.67f, 0.44f) },
            new[] { new Vector2(0.3f, 0.65f), new Vector2(0.7f, 0.65f), new Vector2(0.5f, 0.3f) },
            new[] { new Vector2(0.3f, 0.7f), new Vector2(0.7f, 0.7f), new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.3f) },
            new[] { new Vector2(0.28f, 0.72f), new Vector2(0.72f, 0.72f), new Vector2(0.25f, 0.3f), new Vector2(0.75f, 0.3f), new Vector2(0.5f, 0.5f) },
            new[] { new Vector2(0.25f, 0.73f), new Vector2(0.5f, 0.73f), new Vector2(0.75f, 0.73f), new Vector2(0.25f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(0.75f, 0.3f) }
        };

        public static void Spawn(EnemyEntity entity, SpriteRenderer sourceRenderer)
        {
            if (entity == null || sourceRenderer == null || sourceRenderer.sprite == null)
                return;
            EnemyDeathEffectConfig config = entity.Data != null ? entity.Data.DeathEffect : null;
            if (config == null || !config.Enabled)
                return;

            EnsureFragmentCollisionRules();
            EnemyDeathRemainsSheet sheet = EnemyDeathRemainsSheet.GetOrCreate(entity.transform.parent);
            DeathEffectQuality quality = sheet.GetQuality();
            Bounds visualBounds = sourceRenderer.bounds;
            Collider2D bodyCollider = entity.GetComponent<Collider2D>();
            Bounds anchorBounds = bodyCollider != null ? bodyCollider.bounds : visualBounds;
            Vector2 ground = ResolveGroundAnchor(anchorBounds, visualBounds, entity.transform.position);
            Vector2 burst = visualBounds.center;

            int chunkCount = quality == DeathEffectQuality.Full ? Mathf.Clamp(config.ChunkCount, 1, 6) :
                quality == DeathEffectQuality.Medium ? Mathf.Clamp(config.ChunkCount, 1, 4) :
                quality == DeathEffectQuality.Low ? Mathf.Clamp(config.ChunkCount, 1, 2) : 0;
            for (int i = 0; i < chunkCount && sheet.TryReserveFragment(); i++)
                SpawnFlyingChunk(sheet, config, sourceRenderer, burst, i, chunkCount);

            // Preserve a rich blood silhouette: one central puddle, two flanking puddles and three small drips.
            if (sheet.TryReserveDecal())
                SpawnCompositeRemains(sheet, config, ground, visualBounds.size, quality);

            float spread = Mathf.Max(0.18f, config.BloodHorizontalSpread);
            for (int i = 0; i < 2 && sheet.TryReserveDecal(); i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector2 position = ground + new Vector2(side * Random.Range(spread * 0.28f, spread * 0.62f), Random.Range(-0.01f, 0.025f));
                SpawnDecal(sheet, config, position, EnemyDeathVisualFactory.GetRandomGroundPuddleSprite(), Random.Range(0.42f, 0.62f), Random.Range(-8f, 8f), "EnemyBloodPuddle");
            }
            for (int i = 0; i < 3 && sheet.TryReserveDecal(); i++)
            {
                Vector2 position = ground + new Vector2(Random.Range(-spread, spread), Random.Range(0.0f, 0.07f));
                SpawnDecal(sheet, config, position, EnemyDeathVisualFactory.GetRandomWallDripSprite(), Random.Range(0.32f, 0.5f), Random.Range(-10f, 10f), "EnemyBloodDrip");
            }

            TrySpawnWallSpatter(sheet, config, burst, ground, Mathf.Max(0.7f, visualBounds.size.x));
        }

        private static void SpawnFlyingChunk(EnemyDeathRemainsSheet sheet, EnemyDeathEffectConfig config, SpriteRenderer source, Vector2 burst, int index, int count)
        {
            GameObject fragment = sheet.Pool.GetFragment(sheet.transform, "EnemyBodyChunk");
            fragment.layer = EnemyLayer;
            Bounds spriteBounds = source.sprite.bounds;
            Vector2 normalizedCenter = ResolveChunkCenter(index, count);
            Vector2 normalizedSize = ResolveChunkSize(count);
            Vector2 sourceLocalCenter = new Vector2(
                Mathf.Lerp(spriteBounds.min.x, spriteBounds.max.x, normalizedCenter.x),
                Mathf.Lerp(spriteBounds.min.y, spriteBounds.max.y, normalizedCenter.y));
            Vector2 displayedLocalCenter = sourceLocalCenter;
            if (source.flipX)
                displayedLocalCenter.x = -displayedLocalCenter.x;
            if (source.flipY)
                displayedLocalCenter.y = -displayedLocalCenter.y;

            Vector3 worldPosition = source.transform.TransformPoint(displayedLocalCenter);
            fragment.transform.position = SnapToPixelGrid(worldPosition);
            fragment.transform.rotation = source.transform.rotation * Quaternion.Euler(0f, 0f, Random.Range(-16f, 16f));
            Vector3 sourceScale = source.transform.lossyScale;
            fragment.transform.localScale = new Vector3(Mathf.Abs(sourceScale.x), Mathf.Abs(sourceScale.y), 1f);

            Vector2 localSize = Vector2.Scale(spriteBounds.size, normalizedSize);
            float sizeMultiplier = config.ChunkSizeMultiplier > 0f ? config.ChunkSizeMultiplier : 1f;
            localSize.x = Mathf.Min(localSize.x, MaxChunkWorldWidth * sizeMultiplier / Mathf.Max(0.01f, fragment.transform.localScale.x));
            localSize.y = Mathf.Min(localSize.y, MaxChunkWorldHeight * sizeMultiplier / Mathf.Max(0.01f, fragment.transform.localScale.y));
            int spriteOrder = sheet.AllocateSpriteOrder();
            Sprite chunkSprite = EnemyDeathVisualFactory.GetRandomChunkMaskSprite();

            SpriteRenderer legacyRenderer = fragment.GetComponent<SpriteRenderer>();
            if (legacyRenderer != null)
                legacyRenderer.enabled = false;

            Transform outlineTransform = GetOrCreateChild(fragment.transform, "ChunkOutline");
            ConfigureChunkShapeTransform(outlineTransform, chunkSprite, localSize);
            SpriteRenderer outline = GetOrAdd<SpriteRenderer>(outlineTransform.gameObject);
            outline.enabled = true;
            outline.sprite = chunkSprite;
            outline.sharedMaterial = source.sharedMaterial;
            outline.flipX = false;
            outline.flipY = false;
            outline.maskInteraction = SpriteMaskInteraction.None;
            outline.color = Color.Lerp(config.GoreColor, Color.black, 0.62f);
            ApplyRemainsRenderer(outline, spriteOrder);

            Transform maskTransform = GetOrCreateChild(fragment.transform, "ChunkMask");
            float outlineInset = ChunkOutlinePixels / EnemyDeathVisualFactory.PixelsPerUnit;
            Vector2 maskedDetailSize = new Vector2(
                Mathf.Max(0.04f, localSize.x - outlineInset * 2f),
                Mathf.Max(0.04f, localSize.y - outlineInset * 2f));
            ConfigureChunkShapeTransform(maskTransform, chunkSprite, maskedDetailSize);
            SpriteMask mask = GetOrAdd<SpriteMask>(maskTransform.gameObject);
            mask.enabled = true;
            mask.sprite = chunkSprite;
            mask.isCustomRangeActive = true;
            mask.frontSortingLayerID = SortingLayer.NameToID(WorldRenderSorting.LayerWorld);
            mask.backSortingLayerID = mask.frontSortingLayerID;
            mask.frontSortingOrder = EnemyDeathRemainsLayer.MaskFrontOrder(spriteOrder);
            mask.backSortingOrder = spriteOrder;

            Transform detailTransform = GetOrCreateChild(fragment.transform, "ChunkDetail");
            detailTransform.localPosition = new Vector3(-displayedLocalCenter.x, -displayedLocalCenter.y, 0f);
            detailTransform.localRotation = Quaternion.identity;
            detailTransform.localScale = Vector3.one;
            SpriteRenderer detail = GetOrAdd<SpriteRenderer>(detailTransform.gameObject);
            detail.enabled = true;
            detail.sprite = source.sprite;
            detail.sharedMaterial = source.sharedMaterial;
            detail.flipX = source.flipX;
            detail.flipY = source.flipY;
            detail.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            detail.color = Color.Lerp(config.GoreColor, source.color, 0.5f);
            ApplyRemainsRenderer(detail, EnemyDeathRemainsLayer.OverlayOrder(spriteOrder));

            Rigidbody2D rb = fragment.GetComponent<Rigidbody2D>();
            if (rb == null)
                rb = fragment.AddComponent<Rigidbody2D>();
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = config.GravityScale;
            rb.linearDamping = config.ChunkLinearDamping;
            rb.angularDamping = config.ChunkAngularDamping;
            rb.angularVelocity = Random.Range(-240f, 240f);
            rb.excludeLayers |= (1 << EnemyLayer) | 1;
            BoxCollider2D collider = fragment.GetComponent<BoxCollider2D>();
            if (collider == null)
                collider = fragment.AddComponent<BoxCollider2D>();
            collider.enabled = true;
            collider.size = localSize * 0.78f;
            EnemyDeathFragment controller = fragment.GetComponent<EnemyDeathFragment>();
            if (controller == null)
                controller = fragment.AddComponent<EnemyDeathFragment>();
            controller.Initialize(config, sheet);
            sheet.RegisterFragment(controller);
            Vector2 direction = ((Vector2)fragment.transform.position - burst).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Random.insideUnitCircle.normalized;
            rb.AddForce(new Vector2(direction.x * config.ChunkHorizontalForce, Mathf.Abs(direction.y) * config.ChunkVerticalForce + config.ChunkVerticalForce * 0.45f), ForceMode2D.Impulse);
        }

        private static Vector2 ResolveChunkCenter(int index, int count)
        {
            Vector2[] centers = s_chunkCenters[Mathf.Clamp(count, 1, s_chunkCenters.Length) - 1];
            return centers[Mathf.Clamp(index, 0, centers.Length - 1)];
        }

        private static Vector2 ResolveChunkSize(int count)
        {
            return count switch
            {
                <= 1 => new Vector2(0.48f, 0.52f),
                2 => new Vector2(0.48f, 0.56f),
                3 => new Vector2(0.44f, 0.48f),
                4 => new Vector2(0.4f, 0.44f),
                5 => new Vector2(0.36f, 0.4f),
                _ => new Vector2(0.33f, 0.38f)
            };
        }

        private static void ConfigureChunkShapeTransform(Transform target, Sprite shape, Vector2 localSize)
        {
            Vector2 shapeSize = shape != null ? shape.bounds.size : Vector2.one;
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = new Vector3(
                Mathf.Max(0.01f, localSize.x / Mathf.Max(0.01f, shapeSize.x)),
                Mathf.Max(0.01f, localSize.y / Mathf.Max(0.01f, shapeSize.y)),
                1f);
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(true);
                return child;
            }

            var childObject = new GameObject(childName);
            childObject.layer = parent.gameObject.layer;
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void SpawnCompositeRemains(EnemyDeathRemainsSheet sheet, EnemyDeathEffectConfig config, Vector2 position, Vector2 visualSize, DeathEffectQuality quality)
        {
            float scale = quality == DeathEffectQuality.Full ? 1f : 0.78f;
            // Keep the puddle responsive to enemy size up to 24 px, then use a soft cap.
            float sourceWidth = Mathf.Max(0.7f, visualSize.x);
            float cappedWidth = Mathf.Min(sourceWidth, MaxPuddleWorldWidth);
            SpawnDecal(sheet, config, position, EnemyDeathVisualFactory.GetRandomGroundPuddleSprite(), cappedWidth * scale, Random.Range(-3f, 3f), "EnemyCompositeRemains");
        }

        private static void TrySpawnWallSpatter(EnemyDeathRemainsSheet sheet, EnemyDeathEffectConfig config, Vector2 burst, Vector2 ground, float radius)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                RaycastHit2D hit = Physics2D.Raycast(burst, Vector2.right * side, Mathf.Max(1.2f, radius * 1.5f), GroundLayerMask);
                if (hit.collider == null || !sheet.TryReserveDecal()) continue;
                Vector2 pos = hit.point + hit.normal * 0.008f + Vector2.up * Random.Range(0.08f, Mathf.Max(0.1f, config.BloodVerticalSpread * 0.35f));
                SpawnDecal(sheet, config, pos, EnemyDeathVisualFactory.GetRandomWallDripSprite(), Random.Range(0.75f, 1.1f), side > 0 ? 90f : -90f, "EnemyWallBlood");
            }
        }

        private static void SpawnDecal(EnemyDeathRemainsSheet sheet, EnemyDeathEffectConfig config, Vector2 position, Sprite sprite, float scale, float rotation, string objectName)
        {
            GameObject mark = sheet.Pool.GetDecal(sheet.transform, objectName);
            mark.layer = EnemyLayer;
            mark.transform.position = SnapToPixelGrid(position);
            mark.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            mark.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = mark.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = mark.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = config.BloodColor;
            ApplyRemainsRenderer(renderer, sheet.AllocateSpriteOrder());
            EnemyDeathDecal decal = mark.GetComponent<EnemyDeathDecal>();
            if (decal == null)
                decal = mark.AddComponent<EnemyDeathDecal>();
            decal.Initialize(config.Lifetime, config.FadeDuration, sheet);
            sheet.RegisterDecal(decal);
        }

        private static void EnsureFragmentCollisionRules()
        {
            if (s_collisionRulesConfigured) return;
            Physics2D.IgnoreLayerCollision(EnemyLayer, EnemyLayer, true);
            Physics2D.IgnoreLayerCollision(0, EnemyLayer, true);
            s_collisionRulesConfigured = true;
        }

        private static Vector2 ResolveGroundAnchor(Bounds anchorBounds, Bounds visualBounds, Vector3 fallback)
        {
            Vector2 origin = new Vector2(anchorBounds.center.x, Mathf.Max(anchorBounds.max.y, visualBounds.max.y) + 0.25f);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 6f, GroundLayerMask);
            return hit.collider != null ? hit.point + Vector2.up * 0.02f : new Vector2(fallback.x, anchorBounds.min.y + 0.02f);
        }

        private static void ApplyRemainsRenderer(SpriteRenderer renderer, int spriteOrder)
        {
            renderer.sortingLayerName = WorldRenderSorting.LayerWorld;
            renderer.sortingOrder = spriteOrder;
        }

        private static Vector3 SnapToPixelGrid(Vector2 position)
        {
            const float pixelStep = 1f / EnemyDeathVisualFactory.PixelsPerUnit;
            return new Vector3(Mathf.Round(position.x / pixelStep) * pixelStep, Mathf.Round(position.y / pixelStep) * pixelStep, 0f);
        }
    }
}
