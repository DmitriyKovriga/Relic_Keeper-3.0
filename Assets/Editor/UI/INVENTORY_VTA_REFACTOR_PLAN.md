# Inventory VTA Refactor Plan

## Goal
Move Inventory UI to a VTA-first architecture:
- Visual layout and styling are defined in UXML/USS.
- C# handles binding, events, state updates, and gameplay actions only.

## Scope
- `Assets/UI/Inventory/InventoryLayout.uxml`
- `Assets/UI/Inventory/InventoryStyles.uss`
- `Assets/UI/Inventory/InventoryUI.cs`
- `Assets/UI/Inventory/Logic/InventoryManager.cs`
- `Assets/UI/Tavern/TavernUI.cs`

## Phase 1 (in progress)
1. Add static UXML slot layers for backpack and stash (named slots).
2. Keep runtime fallback generation for compatibility (if legacy layout loaded).
3. Add decorative visual elements in UXML for inventory art composition.
4. Bind UXML slots in code instead of creating the visual tree by default.
5. Extract layout/art binding logic from `InventoryUI` into partial module.

## Phase 2
1. Build a dedicated Inventory visual theme mapper (sprite -> named element/class).
2. Move remaining inline style mutations from C# to USS classes.
3. Replace hardcoded pixel constants with centralized layout config where needed.

## Phase 3
1. Split `InventoryManager` into modules:
- backpack/equipment placement
- craft slot interactions
- orb economy
- save/load adapters
2. Split `TavernUI` into:
- layout/presenter
- localization adapter
- party actions service

## Phase 4
1. Add regression tests/checklists:
- drag/drop return-to-origin
- swap-if-one behavior
- stash <-> inventory transfer
- craft slot + orb apply flow
- save/load integrity for inventory

## Definition of Done for this track
- Inventory can be visually edited in UXML/USS with minimal runtime visual mutations.
- Runtime code no longer builds primary visual tree.
- Core inventory interactions remain behavior-compatible.
