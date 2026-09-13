# Relic Keeper 3.0 — agent notes

2D action roguelike. **Unity 6.3 (6000.3.11f1)**, URP 2D, Input System, Addressables,
Localization (en/ru), TextMeshPro, uGUI + UI Toolkit (UXML/USS), Cinemachine,
Test Framework 1.6. There are **no `.asmdef` files**: gameplay code compiles into
`Assembly-CSharp`, editor and test code into `Assembly-CSharp-Editor`.

## Layout

| Path | Contents |
|---|---|
| `Assets/Scripts/<Domain>/` | Gameplay code by domain: Combat, Damage, Dungeon, Economy, Enemy, GameplayEvents, Hub, Inventory, Items, PlayerBasicScripts, Saving, Skills, Stats, StatusEffects, Systems, UI, VFX, Visuals |
| `Assets/UI/<Screen>/` | UI Toolkit per screen — `*.uxml` + `*.uss` + C# controllers (MainMenuUI, SettingsUI, FastMenuUI, PlayerHUD, Inventory, PassiveTree, CharacterWindow, SkillSlots, Tavern, …) |
| `Assets/UI Toolkit/` | `PanelSettings.asset`, `UnityDefaultRuntimeTheme.tss` |
| `Assets/Prefabs/`, `Assets/Scenes/` | Only `HubScene.unity` and `MainMenuScene.unity` are real scenes; the rest are templates or `_Recovery` |
| `Assets/Tests/EditMode/Editor/` | EditMode tests only, namespace `RelicKeeper.Tests.EditMode`. There are no PlayMode tests |
| `Assets/Localization/`, `Assets/AddressableAssetsData/` | Localization tables and Addressables groups |
| `Docs/*.md` | Design notes: stats, affixes, movement tuning, visual/UI roadmap |

## Working with the live Editor

The Editor is driven through the `agents-unity-bridge` CLI (load the `unity-bridge`
skill) from the project root — prefer a real check over reasoning about what Unity
would do:

```powershell
agents-unity-bridge get-status
agents-unity-bridge compile
agents-unity-bridge run-tests --mode EditMode --filter RelicKeeper.Tests.EditMode
agents-unity-bridge get-console-logs --limit 20 --filter Error
```

Keep bridge output small: always pass `--limit`, `--filter`, or a test `--filter`.
`dump-asset` on a `.unity` path only works for the **currently open** scene.

## Conventions

- Match the surrounding file's style. Serialized field names and public APIs are
  stable — renaming a `[SerializeField]` silently breaks prefabs and scenes.
- UI work follows the `pixel-ui` skill: 480×270 reference, functional UXML/USS
  skeleton first, pixel sprites wired in later.
- Gameplay numbers and balance live in data or tests, not hardcoded in `Update()`.
- After a change run the narrowest relevant EditMode filter; a green suite is the
  definition of done.
