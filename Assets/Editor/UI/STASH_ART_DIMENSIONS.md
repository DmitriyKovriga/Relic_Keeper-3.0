# Stash Art Dimensions (Current Runtime Contract)

All values are in pixels. Target screen is fixed at `480x270`.

## Global Companion Layout

- `Screen`: `480x270`
- `Inventory dock (right window)`: `275x270`
- `Gap between stash and inventory`: `5`
- `Stash panel (left window)`: `200x270`

Source: `Assets/UI/BasicUIScripts/InventoryCompanionLayout.cs`

## Stash Panel

- Element: `.stash-panel`
- Size: `200x270`
- Position: `left: 0`, `top: 0`, `bottom: 0`
- Border: `1px` left + `1px` right
- Padding: `top 8`, `right 8`, `bottom 20`, `left 8`
- Inner content area (after padding): `184x242`

Source: `Assets/UI/Inventory/InventoryStyles.uss`

## Tabs Area

- Element: `.stash-tabs-row`
- Size: `184x18` (height fixed to `18`, width follows stash inner content)

- Element: `.stash-tabs-scroll`
- Size: `184x18` (height fixed to `18`, width fills row)

- Element: `.stash-tab`
- Min size: `20x14`
- Horizontal padding: `4 + 4`
- Right margin: `1`

- Element: `.stash-tab-delete`
- Size: `12x12`

- Element: `.stash-tab-add`
- Min size: `20x14`
- Horizontal padding: `4 + 4`

- Element: `.stash-tabs-carousel`
- Height: `16`

## Grid Area

- Element: `.stash-grid-container`
- Size: `180x220`

- Grid dimensions: `9 columns x 11 rows`
- Slot size: `20x20`
- Total slots per tab: `99`

- Element: `.stash-row`
- Size per row: `180x20`

- Element: `.slot` (shared slot class)
- Size: `20x20`

Source:
- `Assets/UI/Inventory/InventoryStyles.uss`
- `Assets/UI/Inventory/Logic/StashManager.cs` (`STASH_COLS = 9`, `STASH_ROWS = 11`)

## Layers Inside Grid

- `StashSlotsLayer`: `180x220` (absolute layer at `0,0`)
- `StashItemsLayer`: `180x220` (fills parent)
- `StashFrameArt`: `180x220` (fills parent)

## Helpful Export Targets For Art

- `stash_window_bg`: `200x270` (opaque background of the stash panel)
- `stash_grid_frame`: `180x220`
- `stash_tabs_strip` (optional): `180x18`

