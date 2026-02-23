# Inventory UI Regression Checklist

Date: 2026-02-23
Build gate: `dotnet build Relic_Keeper.slnx -nologo` must pass before and after checks.

## Drag & Drop
1. Pick item from backpack and drop to empty backpack area.
Expected: item is placed under pointer target with correct anchor.
2. Drop item onto occupied area with exactly one overlapping item.
Expected: swap behavior, no duplication/loss.
3. Drop item to invalid area.
Expected: item returns to source slot.
4. Drag item from stash to inventory and back.
Expected: transfer is atomic, no ghost copy remains.

## Ctrl+Click Transfers
1. Ctrl+Click backpack item when stash is open.
Expected: item moves to stash current tab.
2. Ctrl+Click stash item when inventory is open.
Expected: item moves to backpack/equipment if valid.
3. Ctrl+Click craft-slot item.
Expected: item transfers via shared quick-transfer endpoint.

## Craft Slot / Orbs
1. Switch to craft tab and move rare item into craft slot.
Expected: item renders and stays after refresh.
2. Right-click orb with positive count and apply to rare item.
Expected: orb count decrements by 1 and item rerolls.
3. Press `Escape` while apply-orb mode is active.
Expected: mode cancels and ghost orb cursor is removed.

## Window Composition
1. Open inventory without stash.
Expected: inventory layout remains in fixed position.
2. Open stash with inventory.
Expected: both windows are visible, independent, no inventory shift.
3. Close inventory while holding an item.
Expected: item safely returns to source/fallback inventory.

## Save/Load
1. Save game with items in backpack/equipment/craft slot/stash.
2. Reload scene or game state.
Expected: full item state restored with no missing anchors.

## Result Log
- [ ] Drag & Drop
- [ ] Ctrl+Click Transfers
- [ ] Craft Slot / Orbs
- [ ] Window Composition
- [ ] Save/Load
