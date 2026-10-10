---
name: ecc-unity-research
description: Research existing implementations and Unity 6 APIs before adding a Relic Keeper feature, utility, dependency, or integration. Use when the best implementation path is uncertain or a new dependency is being considered.
license: MIT
metadata:
  origin: affaan-m/ECC search-first, adapted for Relic Keeper
---

# Research before implementation

Read [the project context](../../references/unity-context.md).

Start with the requested behavior and a concrete acceptance example. Search the
relevant domain under `Assets/Scripts`, its data assets, UI controllers and
`Assets/Tests/EditMode/Editor` using targeted `rg` queries. Trace callers and
runtime calculations before proposing another helper or service.

For uncertain/version-sensitive APIs, inspect installed package code and consult
official Unity documentation matching the Editor and package versions. Use
maintainer documentation for third-party packages. A web/npm example is not
evidence that its API, thread model or test runner works inside Unity.

When considering an external package, assess the actual fit: Unity/C# and
IL2CPP compatibility if relevant to the target, URP 2D support for rendering,
dependencies, maintenance, licensing, serialized-data implications and removal
cost. Prefer an existing project facility or installed Unity package when it
already solves the need. Use npm/PyPI only for an actual external tooling task.
Do not install a tool just because an upstream ECC catalog mentions it.

Decide whether to reuse, extend, adopt or build. Explain the relevant tradeoff
briefly, identify the affected files and the check that will validate the
decision, then proceed within the authorized implementation scope. Small known
fixes need only the relevant local search. Use available search tools directly;
this skill does not require a researcher subagent or extra MCP server.
