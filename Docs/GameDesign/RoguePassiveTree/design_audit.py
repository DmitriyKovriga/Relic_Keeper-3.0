"""Exact node-weighted Steiner costs for the small review goal sets."""
import heapq

def minimum_allocation(adj, goals):
    terminals=['start']+list(goals)
    ids=list(adj); weight={v:0 if v=='start' else 1 for v in ids}
    dp={}; parent={}
    for mask in range(1,1<<len(terminals)):
        cost={v:10**6 for v in ids}; prev={}
        if mask & (mask-1)==0:
            v=terminals[mask.bit_length()-1];cost[v]=weight[v];prev[v]=('terminal',)
        else:
            sub=(mask-1)&mask
            while sub:
                other=mask^sub
                if sub<other:
                    for v in ids:
                        candidate=dp[sub][v]+dp[other][v]-weight[v]
                        if candidate<cost[v]:cost[v]=candidate;prev[v]=('merge',sub,other)
                sub=(sub-1)&mask
        queue=[(c,v) for v,c in cost.items() if c<10**6];heapq.heapify(queue)
        while queue:
            c,v=heapq.heappop(queue)
            if c!=cost[v]:continue
            for to in sorted(adj[v]):
                nc=c+weight[to]
                if nc<cost[to]:cost[to]=nc;prev[to]=('step',v);heapq.heappush(queue,(nc,to))
        dp[mask]=cost;parent[mask]=prev
    full=(1<<len(terminals))-1;root=min(ids,key=lambda v:dp[full][v])
    allocated=set();visited=set()
    def trace(mask,v):
        if (mask,v) in visited:return
        visited.add((mask,v));allocated.add(v);p=parent[mask][v]
        if p[0]=='step':trace(mask,p[1])
        elif p[0]=='merge':trace(p[1],v);trace(p[2],v)
    trace(full,root)
    assert len(allocated)-1==dp[full][root]
    reachable={'start'};queue=['start']
    for v in queue:
        for to in adj[v]&allocated:
            if to not in reachable:reachable.add(to);queue.append(to)
    assert reachable==allocated and set(terminals)<=allocated
    return {'minimumPoints':len(allocated)-1,'allocatedIds':sorted(allocated)}
