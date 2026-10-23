"""Check the local install, graph coverage, and configured stdio MCP connection."""
import asyncio
import importlib.metadata
import json
from pathlib import Path
import tomllib

from mcp.client.session import ClientSession
from mcp.client.stdio import StdioServerParameters, stdio_client

ROOT = Path(__file__).resolve().parents[2]


def text(result):
    if getattr(result, "is_error", getattr(result, "isError", False)):
        raise AssertionError(str(result))
    return "\n".join(item.text for item in result.content if hasattr(item, "text"))


async def main():
    config = tomllib.loads((ROOT / ".codex/config.toml").read_text(encoding="utf-8"))["mcp_servers"]["graphify"]
    allowed = {"query_graph", "get_node", "get_neighbors", "shortest_path", "graph_stats"}
    assert set(config["enabled_tools"]) == allowed
    assert Path(config["command"]).is_file()
    assert Path(config["cwd"]).resolve() == ROOT
    graph_path = ROOT / ".graphify/index/graph.json"
    assert Path(config["args"][-1]).resolve() == graph_path
    graph = json.loads(graph_path.read_text(encoding="utf-8"))
    assert graph["graph"]["graphify_version"] == importlib.metadata.version("graphifyy")
    sources = {node.get("source_file", "").replace("\\", "/") for node in graph["nodes"]}
    for source in ["Assets/Scripts/Combat/DamageCalculator.cs", "Assets/Scripts/Skills/Logic/SkillStepRunner.cs",
                   "Assets/Scripts/Skills/Logic/SkillStepRunner.IcyPalisade.cs", "Assets/Scripts/Systems/FrameRateLimiter.cs"]:
        assert source in sources, f"Missing C# coverage: {source}"
    assert not any(source.startswith(("Library/", ".graphify/", "Packages/", "Assets/TextMesh Pro/")) for source in sources)
    print(f"Graphify {importlib.metadata.version('graphifyy')}: {len(graph['nodes'])} nodes, {len(graph['links'])} edges; C# coverage verified.")
    params = StdioServerParameters(command=config["command"], args=config["args"], cwd=config["cwd"], env=config["env"])
    async with stdio_client(params) as (reader, writer):
        async with ClientSession(reader, writer, read_timeout_seconds=30) as session:
            await session.initialize()
            tools = await session.list_tools()
            assert allowed <= {tool.name for tool in tools.tools}
            stats = text(await session.call_tool("graph_stats", {}))
            assert str(len(graph["nodes"])) in stats
            context = text(await session.call_tool("query_graph", {"question": "FrameRateLimiter", "depth": 1, "token_budget": 700}))
            assert "Assets/Scripts/Systems/FrameRateLimiter.cs" in context
            focused = text(await session.call_tool("query_graph", {"question": "ExecuteIcyPalisadeGroup", "depth": 1, "token_budget": 900, "context_filter": ["call"]}))
            assert "SkillStepRunner.IcyPalisade.cs" in focused
            node = next(node for node in graph["nodes"] if node.get("label") == "FrameRateLimiter")
            assert "FrameRateLimiter.cs" in text(await session.call_tool("get_node", {"node_id": node["id"]}))
            assert "Apply" in text(await session.call_tool("get_neighbors", {"node_id": node["id"], "token_budget": 500}))
            method = next(node for node in graph["nodes"] if node.get("source_file") == "Assets/Scripts/Systems/FrameRateLimiter.cs" and "Apply" in node.get("label", ""))
            path = text(await session.call_tool("shortest_path", {"source": node["id"], "target": method["id"], "max_hops": 3}))
            assert "Apply" in path
            print("MCP stdio: initialized, tool discovery and all five configured read tools passed.")
            print(f"Focused frame-pacing context: {len(context)} characters; terrain/skill context: {len(focused)} characters.")


if __name__ == "__main__":
    asyncio.run(asyncio.wait_for(main(), timeout=60))
