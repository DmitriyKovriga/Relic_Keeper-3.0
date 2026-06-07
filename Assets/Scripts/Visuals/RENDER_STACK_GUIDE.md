# Render Stack Guide (Relic Keeper)

World sprite draw order is managed by **sorting layers** (coarse stack) and **sorting order + Y** (fine depth). UI Canvas is separate and always on top.

## Stack (bottom → top)

| Layer (TagManager) | Category | What goes here |
|---|---|---|
| `Background` | `Background`, `Environment` | Parallax sky, baked shadows, distant props |
| `Default` | *(legacy scene content)* | Tilemaps, hub props, static level art — keep order **≤ 200** |
| `World` | `Enemy`, `EnemyRemains` | Mobs, corpses, blood |
| `VFX` | `GameplayVfx` | Enemy telegraphs, stun, ground dust |
| `Hero` | `Player`, `PlayerOverlay`, `HeroAttackVfx` | Player body, weapon, skills, projectiles |
| `SFX` | — | Optional top FX (legacy) |

**TagManager order must be:** `Background → Default → World → VFX → Hero → SFX`  
Run **Tools → Relic Keeper → Visuals → Validate Render Stack** after changing TagManager.

## Lit sprites (URP Light2D)

Any sprite with **Sprite-Lit-Default** must be on a layer targeted by `Light2D`.  
The project syncs this automatically at runtime; in editor use **Validate Render Stack** to fix scene lights.

If a sprite is **solid black** but the environment is fine → wrong sorting layer for lights.

---

## Adding content

### 1. Static environment (tilemaps, hub props)

- **Sorting layer:** `Default` (or `Background` for sky-only art).
- **Sorting order:** `-50 … 200` (sky ≈ `-50`, floor ≈ `-10`, props `0–100`).
- **Do not** hand-set orders like `20000` — that breaks the stack.
- **No code required** if it stays static.

### 2. Enemies

Handled automatically:

- `EnemyEntity` adds `WorldDepthSort` (`Enemy` on `World`).
- Y-position updates order every frame (no flicker at overlaps).

**Prefab rules:**

- One root; visual on child is OK.
- Do not assign custom sorting layers/orders on the prefab — sorter owns them.

### 3. Player

Handled automatically:

- `PlayerMovement` → `WorldDepthSort` on root (`Player` / `Hero`).
- `WeaponVisualController` → weapon only (`PlayerOverlay` / `Hero`), nested sorter on hand renderer.

**Do not** add a second body sorter on the weapon pivot.

### 4. Runtime VFX (skills, projectiles, spawns)

Use **`WorldRenderSorting.ConfigureSorter`** for moving objects:

```csharp
WorldRenderSorting.ConfigureSorter(
    vfxRoot,
    RenderDepthCategory.HeroAttackVfx,  // or GameplayVfx
    spawnPosition.y,
    localOffset: 0,
    staticAnchor: false);               // true if effect stays at fixed Y
```

Use **`ConfigureOneShotRenderer`** for a single `SpriteRenderer` without updates:

```csharp
WorldRenderSorting.ConfigureOneShotRenderer(renderer, RenderDepthCategory.GameplayVfx, position.y, localOffset: 0);
```

**Category cheat sheet:**

| Effect | Category |
|---|---|
| Hero skill slash, projectile, chain lightning | `HeroAttackVfx` |
| Dodge/flash on player | `PlayerOverlay` |
| Enemy attack telegraph, stun | `GameplayVfx` |
| Run/land dust | `GameplayVfx` |
| Death blood / remains | `EnemyRemains` (static anchor at death Y) |

### 5. VFX prefabs

- Prefer **no hard-coded** sorting layer/order on prefab root.
- Assign in spawn code via `ConfigureSorter`.
- If a prefab must be self-contained, match the table above and document it in the prefab name.

### 6. Nested visuals (parent + child sorters)

`WorldDepthSort` applies to **own renderers + children**, but **stops at nested `WorldDepthSort`**.  
Example: player root sorts body; weapon child sorts itself as overlay.

---

## API reference

| API | When |
|---|---|
| `WorldRenderSorting.ConfigureSorter(go, category, worldY, offset, staticAnchor)` | Moving actors, timed VFX |
| `WorldRenderSorting.ConfigureOneShotRenderer(sr, category, worldY, offset)` | Static decal, one-off sprite |
| `WorldRenderSorting.GetSortingLayer(category)` | LineRenderer / custom render code |
| `WorldRenderSorting.ResolveOrder(category, worldY, offset)` | Manual order (LineRenderer, etc.) |

Settings asset (optional): `Resources/Visuals/RenderStackSettings.asset`  
Create via **Tools → Relic Keeper → Visuals → Create Default Render Stack Settings**.

---

## Checklist before merge

- [ ] Run **Tools → Relic Keeper → Visuals → Validate Render Stack** (0 failures).
- [ ] Play hub + one combat room: sky, player, mobs, skills visible and lit.
- [ ] Overlapping enemies do not flicker.
- [ ] Hero attacks render above mobs.
- [ ] New prefabs do not use `Default` with order `> 200` unless static environment.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Black player/mobs | Light2D missing `World`/`Hero` | Validate Render Stack |
| Hidden under floor | Actor on layer below `Default` content | Check TagManager order |
| Flickering mobs | Same order at same Y | Should not happen with `WorldDepthSort`; check duplicate sorters |
| Weapon wrong depth | Two body sorters fighting | Only root + weapon sorters |
| Sky missing | Parallax moved to wrong layer | Sky on `Default` order `-50` or `Background` |
