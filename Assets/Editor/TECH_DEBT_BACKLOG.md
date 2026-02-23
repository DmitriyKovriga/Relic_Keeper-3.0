# Technical Debt Backlog

## UI Architecture
- Inventory UI is partially generated in runtime (`InventoryUI`) and partially described in UXML/USS, causing design/runtime mismatch.
- Tavern UI is fully code-generated in `TavernUI.BuildUI`, mixing layout, style, localization, and gameplay actions.
- UI logic and visual concerns are tightly coupled in large classes (`InventoryUI`, `TavernUI`).

## Runtime Systems
- Input event subscription bug in `PlayerAttackInput` (lambda subscribe/unsubscribe mismatch).
- Multiple global singleton dependencies increase coupling (`InventoryManager`, `DungeonController`, `CharacterPartyManager`, `SceneLoader`).
- Repeated `FindObjectOfType` usage and deprecated APIs in runtime/UI code.

## Domain & Stability
- `InventoryManager` is a large god-class combining backpack, equipment, craft slot, and orb economy.
- `InventoryUI` and `InventoryManager` have broad side effects and complex swap/drop branches with low test coverage.
- No automated tests for critical flows (inventory swap, stash transfer, save migration, passive tree allocation).

## Content Pipeline
- Inventory art exists but is not yet fully wired as first-class visual layout assets.
- `_AI_Source_Dump` can create confusion/noise and duplicate source-of-truth risk.

## Current Refactor Track (started)
1. Decompose `InventoryManager`, `InventoryUI`, `TavernUI` into partial/modules by responsibility.
2. Move Inventory visual structure to UXML/USS-first approach.
3. Keep runtime code focused on binding/events/state only.
4. Introduce inventory art mapping layer for slot/background/frame sprites.
5. Add smoke tests/checklists for inventory drag-drop and stash transfer.
