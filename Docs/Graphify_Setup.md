# Project-local Graphify

Graphify 0.9.94 (`graphifyy[mcp]`) is installed in `.graphify/venv`, separate from Unity and the shared Python runtime. The package/version is pinned in `Tools/Graphify/requirements.txt`. Generated indexes and the environment are ignored by Git. No game assets or runtime assemblies are changed.

## Use

From the project root in PowerShell:

```powershell
./Tools/Graphify/Invoke-Graphify.ps1 --version
./Tools/Graphify/Update-Graphify.ps1
./Tools/Graphify/Invoke-Graphify.ps1 query "FrameRateLimiter" --budget 1200
./Tools/Graphify/Invoke-Graphify.ps1 query "ExecuteIcyPalisadeGroup" --context call --budget 1200
./Tools/Graphify/Invoke-Graphify.ps1 explain "FrameRateLimiter"
./Tools/Graphify/Invoke-Graphify.ps1 affected "FrameRateLimiter" --depth 1
./.graphify/venv/Scripts/python.exe Tools/Graphify/Verify-Graphify.py
```

The launcher fixes the working directory, UTF-8 encoding, local output path, and disabled query logging. Update runs `extract . --code-only --max-workers 4`, reusing Graphify's AST cache/manifest for unchanged files. Update explicitly after source changes or switching branches; the graph is a cache and must not override current source. If extraction refuses a smaller graph, check whether files were intentionally removed before considering an explicit `extract . --code-only --force`.

## Assistant integration

`.codex/config.toml` enables a project-local stdio MCP server for trusted checkouts. It exposes only `query_graph`, `get_node`, `get_neighbors`, `shortest_path`, and `graph_stats`. Query/node outputs are capped. The server uses the local graph with no listening network port. Reload the MCP connection or reopen the project/session after first installation so the client loads the new configuration.

For discovery, start with one exact symbol and a small query (`depth: 1`, `token_budget: 1200` over MCP). Narrow large results by `context_filter: ["call"]` or inspect a specific node. Read the returned source locations before editing. Broad words can retrieve namespaces, tests, and unrelated helpers; use `rg` when that is cheaper or the graph misses a relationship. Never paste the entire graph/report into context.

The MCP paths are absolute for this Windows checkout. If the repository moves, update `command`, graph argument, `cwd`, and `GRAPHIFY_OUT` in `.codex/config.toml`. The CLI scripts themselves resolve paths relative to their location.

## Scope and limits

`.graphifyignore` excludes Unity caches, builds, third-party TextMesh Pro content, dependencies, images, generated data, and serialized Unity assets. Code-only mode skips semantic document/media extraction, so no API key or model calls are needed. Existing Markdown design docs remain available through ordinary file reads.

The C# graph maps declared types, methods, imports, and some call/reference edges. Unity serialized GUID links, prefabs/scenes, animation events, runtime-loaded assets, reflection, and dynamically bound calls still require direct inspection and Unity verification. Partial classes and overloaded method names can produce ambiguous graph results. There is no measured token-saving percentage for this project; reduced discovery context is the intended benefit.

No upstream always-on skill, git hooks, background watcher, cloud service, automatic memory/learning, or Obsidian export is enabled. Obsidian is optional and not needed to query this graph.

## Installation verification (2026-10-23)

The local index contains 10,530 nodes and 38,336 edges across 688 scanned code files. An incremental refresh reused 687 unchanged files. The offline reinstall and dependency check passed, as did PowerShell syntax checks and the stdio MCP handshake, discovery, and calls to all five enabled tools. Focused queries located both frame-pacing code and the Icy Palisade handler. Unity compile/tests were not run because this integration changes only tooling, configuration, and documentation.

## Reinstall and remove

```powershell
./Tools/Graphify/Install-Graphify.ps1
# Or supply a Python 3.10+ executable:
./Tools/Graphify/Install-Graphify.ps1 -Python C:\Python312\python.exe
./Tools/Graphify/Update-Graphify.ps1
```

For removal, disable/remove only `[mcp_servers.graphify]` and its subtables from `.codex/config.toml`, remove the Graphify section of `AGENTS.md`, and delete the project-local `.graphify` directory after confirming its absolute path is inside this checkout. The shared Python runtime and other projects are unaffected.

Upstream: [Graphify source](https://github.com/Graphify-Labs/graphify), [supported inputs](https://docs.graphify.com/concepts/supported-inputs), [extract reference](https://docs.graphify.com/reference/extract). MCP configuration follows [official OpenAI documentation](https://learn.chatgpt.com/docs/extend/mcp?surface=cli).
