# Inventory VTA Refactor Plan

## Goal
Move Inventory UI to a VTA-first architecture:
- Visual layout and styling are defined in UXML/USS.
- C# handles binding, events, state updates, and gameplay actions only.

## Current Phase
- **Phase 4 (started 2026-02-23)**.
- Phase 3 decomposition is completed with green `dotnet build` after module extraction.

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
   - Done (inventory/stash layout): removed runtime sizing/positioning in `InventoryUI.ApplyCompanionWindowConstraints`; stash/inventory window geometry is class-driven in `InventoryStyles.uss`.
3. Replace hardcoded pixel constants with centralized layout config where needed.
4. Introduce shared item transfer layer (`ItemQuickTransferService`) so windows can exchange items without hard-coding each other.
5. Migrate Ctrl+Click transfers to shared transfer endpoints (inventory/stash done; craft-slot endpoint added as next adopter).
6. Introduce shared pointer drop layer (`ItemDragDropService`) and register inventory/stash drop endpoints as first adopters.
   - Craft-slot drop endpoint added (accepts drops when craft tab is open and slot is empty).

## Phase 3
1. Split `InventoryManager` into modules:
- backpack/equipment placement
- craft slot interactions
- orb economy
- save/load adapters
  - Done: save/load methods moved into `InventoryManager.SaveLoad.cs`.
  - Done: craft-slot operations moved into `InventoryManager.Craft.cs`.
  - Done: backpack/equipment move/swap logic moved into `InventoryManager.MoveLogic.cs`.
  - Done: placement/query methods moved into `InventoryManager.Placement.cs`.
  - Done: orb methods moved into `InventoryManager.Orbs.cs`.
2. Split `TavernUI` into:
- layout/presenter
- localization adapter
- party actions service
  - Done: presenter/layout methods moved into `TavernUI.Presenter.cs`.
  - Done: party actions moved into `TavernUI.PartyActions.cs`.
  - Done: cards/tree-preview logic moved into `TavernUI.CardsAndPreview.cs`.
  - Done: localization adapter lives in `TavernUI.Localization.cs`.
3. Split `InventoryUI` into focused modules:
- layout/art binding: `InventoryUI.LayoutBinding.cs`
- transfer/drop endpoints: `InventoryUI.QuickTransfer.cs`
- stash view/panel: `InventoryUI.Stash.cs`
- item rendering: `InventoryUI.Rendering.cs`
- craft/tabs/orbs: `InventoryUI.Crafting.cs`
- pointer/drag-drop interactions: `InventoryUI.DragDrop.cs`
- lifecycle/orchestration shell: `InventoryUI.cs`

## Phase 4
1. Add regression tests/checklists:
- drag/drop return-to-origin
- swap-if-one behavior
- stash <-> inventory transfer
- craft slot + orb apply flow
- save/load integrity for inventory
  - Started: manual checklist created in `Assets/Editor/UI/INVENTORY_REGRESSION_CHECKLIST.md`.

## Definition of Done for this track
- Inventory can be visually edited in UXML/USS with minimal runtime visual mutations.
- Runtime code no longer builds primary visual tree.
- Core inventory interactions remain behavior-compatible.
- Item quick-transfer (Ctrl+Click) is routed via shared endpoints, not direct window coupling.
- Item pointer drop can be routed via shared drop endpoints (inventory/stash implemented).
