# ECC for Relic Keeper

Installed on 2026-10-10 as the native Codex plugin
`ecc-unity@relic-keeper`, version `2.2.3-unity.1`.
This is a curated, locally maintained adaptation of ECC, not the complete
upstream plugin or an upstream Unity preset.

## Why this selection

Relic Keeper already has useful project instructions, NUnit EditMode tests,
Unity Bridge and a pixel UI skill. ECC adds value when it helps trace dependencies,
define observable behavior, catch regressions and verify the result. Installing
every workflow would also expose unrelated web, database, cloud and agent-runtime
guidance. The selected package keeps the useful engineering ideas and replaces
their tooling assumptions with the actual Unity project workflow.

The reviewed baseline is Unity `6000.3.11f1`, URP `17.3.0`, Test Framework `1.6.0`,
Input System, Cinemachine 3, Localization EN/RU and uGUI/UI Toolkit. No custom
assembly definitions are present. The inventory panel confirms a 480x270
reference resolution. These values should be rechecked when upgrading Unity.

ECC's **Standard hooks** option belongs to its Claude setup. Its Codex-native
manifest has a different hook definition and no equivalent Standard preset.
This installation uses normal automatic skill selection and proportionate
engineering checks; it does not enable a Claude hook profile.
See [ECC's Codex setup](https://github.com/affaan-m/ECC/tree/v2.2.3#codex-app-and-cli)
and [Codex plugin packaging](https://developers.openai.com/plugins/build/plugins).

## Included workflows

| Skill | ECC source | Adaptation and practical benefit |
|---|---|---|
| `ecc-unity-research` | `search-first` | Search existing game code and installed packages first; check official Unity APIs before introducing helpers or dependencies. |
| `ecc-unity-plan` | `planner` agent | Plan substantial changes around runtime, serialized assets and save data; proceed with already authorized implementation. |
| `ecc-unity-tests` | `tdd-workflow` | Meaningful NUnit EditMode regression tests, exact class filters, deterministic fixtures and actual red/green evidence when appropriate. |
| `ecc-unity-review` | `code-reviewer` agent | Review concrete failures in gameplay, serialization, lifecycle, pooling, localization and relevant hot paths. |
| `ecc-unity-verify` | `verification-loop` | Real Editor compile/tests/console checks, diff review and Play Mode visual checks when required. |
| `unity-bridge` | Locally authored CLI guidance | Make the already installed bridge discoverable as the skill named by `AGENTS.md`; no bridge/package update required. |

The planner and reviewer are ordinary Codex skills, not installed Claude agent
personas. They do not change models or automatically create subagents.
The existing `relic-keeper-pixel-ui` skill remains the runtime UI workflow.

## Defaults adapted or excluded

| Upstream behavior | Choice here | Reason |
|---|---|---|
| Mandatory TDD for all changes and 80% coverage | Behavior-driven regression tests; explicit TDD requests retain red/green | The project has EditMode tests, no configured coverage gate and no PlayMode suite. Docs and small reversible presentation edits need different checks. |
| Git checkpoint commits after each TDD stage | No automatic staging or commits | Preserve the user's Git workflow and unrelated edits. |
| npm builds, web type checks, Playwright and generic security greps | Unity Bridge and relevant NUnit classes | Verify the actual game instead of invoking unrelated tools. |
| Blanket immutability and arbitrary size thresholds | Follow Unity ownership and measured hot paths | Immutable copies can create avoidable allocations during gameplay. |
| Full catalog and broad always-loaded rule packs | Five focused ECC-derived skills plus the bridge skill | Keep routing/context relevant and preserve existing project conventions. |
| Browser MCP bundled by the upstream native manifest | No bundled MCP servers | Browser automation already exists; it adds no necessary Unity capability here. |
| Native session bootstrap hook and learning/memory workflows | No bundled hooks or automatic learning/memory writes | Keep session context and memory behavior under the existing Codex/user rules. |

No gameplay, Unity packages, scenes, prefabs, materials, balances or save data
were changed for this installation. Hook trust was not changed. The plugin is
skills-only and contains no executable upstream hook runtime.

## How to use it

Skills may be selected automatically when a matching Relic Keeper task needs
them. You can also request one explicitly, for example:

- `Use $ecc-unity-plan to plan a new enemy affix system.`
- `Use $ecc-unity-tests to add a regression test for this cooldown bug.`
- `Use $ecc-unity-review to review the current gameplay diff.`
- `Use $ecc-unity-verify to validate the completed change.`

The root `AGENTS.md` links the workflows so future chats in this project can
also read their source files if the plugin has not loaded. A routine small fix
does not need all six skills. The plugin is installed in the active user's Codex
home; its descriptions and shared guidance restrict it to this project.

Start with the next turn. If the new skills are absent from the picker/context,
restart Codex and reopen this project. Installation/discovery is verified below;
the already-running turn does not prove the next turn's automatic routing.

## Installation, source pin and updates

Marketplace source: the project root, with catalog
`.agents/plugins/marketplace.json`. Plugin source:
`.agents/plugins/ecc-unity/`. Codex loads its installed cache copy, not a live
link to every source edit. On this machine that copy is at:

`C:\Users\Дмитрий\.codex\plugins\cache\relic-keeper\ecc-unity\2.2.3-unity.1`

Native installation commands, run from the project root:

```powershell
codex plugin marketplace add 'D:\WORK\GameDev\Relic_Keeper-3.0' --json
codex plugin add ecc-unity@relic-keeper --json
codex plugin list --marketplace relic-keeper --json
```

ECC is pinned to tag `v2.2.3`, commit
`c05b2d6614f62f6db0047669aa4eefb223d478f9`. The original MIT notice is retained
in the plugin's `LICENSE`. `upstream-lock.json` records the selected upstream
file paths and SHA-256 hashes of their raw bytes at that commit. The working
skills are adapted versions; the hashes identify their source material.

The Skill Installer fetched the upstream workflow sources into a review area,
then the adapted package was installed using Codex's native plugin lifecycle.
The deprecated ECC sync installer was not run. No full `ecc@ecc` install was
layered on top. The reviewed native manifest, hook and MCP definitions informed
the exclusions above.

To update, edit the source skills, review the changes and validate their metadata
and references. Increment the adaptation version consistently in the plugin
manifest, marketplace catalog and lock file, then rerun `codex plugin add` and
verify the new installed copy. Review upstream diffs before changing the pin;
an ECC release is not permission to import additional hooks or defaults.

## Verification and limits

- All six skills pass the bundled Skill Creator `quick_validate.py` check.
- Plugin/catalog/lock JSON and skill UI YAML parse; internal reference paths
  resolve and the installed cache matches the source package.
- `codex plugin list --marketplace relic-keeper --json` reports
  `installed: true`, `enabled: true`, version `2.2.3-unity.1`.
- The Codex configuration comparison confirms only the new marketplace/plugin
  entries were added. Existing values, including model and permission settings,
  were preserved. A pre-install config backup is retained at
  `C:\Users\Дмитрий\.codex\vendor_imports\ecc-unity-backups\2026-10-10\config.before-ecc-unity.toml`.
- Unity Bridge `get-status --timeout 15` timed out, and no Unity Editor process
  was detected. No live compile, EditMode or Play Mode result is claimed.
  This patch changes only agent/plugin/documentation files; no new Unity test
  is required to validate the installation.

The expected benefit is better task routing and Unity-specific evidence. It has
not yet been measured on a new gameplay task, and structural checks do not prove
automatic skill selection or improved runtime behavior. Evaluate it on the next
real task before adding more ECC modules.

## Disable or remove

Disable **ECC for Relic Keeper** in Codex's plugin settings to stop native skill
loading. For complete removal of this integration, first run:

```powershell
codex plugin remove ecc-unity@relic-keeper --json
codex plugin marketplace remove relic-keeper --json
```

Then remove only the `ECC Unity workflows` section added to `AGENTS.md` and the
new `.agents/plugins/marketplace.json`, `.agents/plugins/ecc-unity/` and this
setup guide if no longer wanted. The AGENTS section otherwise continues to
route to the source workflows even after the native plugin is removed.
Review local modifications before deleting files. Do not restore the entire old
Codex config over later unrelated changes; use the native remove commands.
