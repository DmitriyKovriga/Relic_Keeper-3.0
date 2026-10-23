"""Rebuild the offline review and coding handoff. Never writes Unity assets."""
from pathlib import Path
import collections, csv, hashlib, html, json, math, re, subprocess

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
SOURCE = ROOT / 'Assets/Resources/PassiveTrees/RoguePassiveSkillTree/RoguePassiveSkillTree.asset'
TRANSPORT_TEMPLATE = ROOT / 'Assets/Resources/PassiveTrees/Templates/Misc/NewPassiveNode 11/transportRogue.asset'
TRANSPORT_IDS = 'n10 n11 n14 n15 n16 n17 n18 n19 n20 n21 n43 n49 n58 n59 n61 n62 n64 n65 n66 n68 n70 n76 n80 n111'.split()
# Free placement is not a role: these hand-built groups remain specialized.
SPECIALIZED_FREE_SMALL_IDS = 'n72 n82 n83 n84 n100 n102 n103 n104 n151 n162'.split()
TYPE = {0:'Small', 1:'Notable', 2:'Keystone', 3:'Start'}
MOD = {'Flat':100, 'PercentAdd':200, 'PercentSub':210, 'PercentMult':300, 'PercentLess':310}
FAMILIES = {
 'speed':('Скорость атаки','#e9bb64'), 'crit':('Критические удары','#bb9cf3'),
 'poison':('Яд','#a4cf71'), 'elemental':('Конверсия и стихии','#7bb8f6'),
 'range':('Снаряды / дальний бой','#ec9b78'), 'physical':('Физический урон','#e68eaa'),
 'defense':('Защита','#66c6b8'), 'hybrid':('Гибрид','#d2d8d3'),
 'transport':('Транспорт · +3 HP / +5 уклонения','#929a94'), 'start':('Старт','#faf2d8')}
STATS = {
 'AttackSpeed':'скорость атаки', 'CritChance':'шанс критического удара', 'CritMultiplier':'множитель критического урона',
 'PoisonChance':'шанс отравления', 'PoisonDamage':'увеличение урона яда', 'PoisonDamageMult':'коэффициент урона яда',
 'PoisonDuration':'длительность яда', 'DamagePhysical':'физический урон', 'MeleeDamage':'урон в ближнем бою',
 'ElementalDamage':'стихийный урон', 'DamageFire':'урон огнём', 'DamageCold':'урон холодом',
 'DamageLightning':'урон молнией', 'PhysicalToFire':'конверсия физического урона в огонь',
 'PhysicalToCold':'конверсия физического урона в холод', 'PhysicalToLightning':'конверсия физического урона в молнию',
 'RangeDamage':'урон с тегом Range', 'ProjectileCount':'дополнительные снаряды',
 'ProjectilePierce':'пробитые цели снаряда', 'ProjectileChain':'переходы цепи', 'ProjectileFork':'разветвления снаряда',
 'ProjectileSpeed':'скорость снарядов', 'ReturningProjectileDamage':'урон возвратного прохода',
 'MaxHealth':'максимальное здоровье', 'HealthRegen':'восстановление здоровья / с',
 'HealthRegenPercent':'восстановление максимального здоровья / с', 'HealthOnHit':'здоровье за попадание',
 'MaxMana':'максимальная мана', 'ManaRegen':'восстановление маны / с', 'ManaOnHit':'мана за попадание',
 'Evasion':'рейтинг уклонения', 'Armor':'броня', 'BlockChance':'шанс блока',
 'FireResist':'сопротивление огню', 'ColdResist':'сопротивление холоду',
 'LightningResist':'сопротивление молнии', 'PhysicalResist':'сопротивление физическому урону',
 'MoveSpeed':'скорость передвижения', 'EffectDuration':'длительность эффектов',
 'AreaOfEffect':'площадь действия', 'PushbackResist':'сопротивление отбрасыванию'}
PERCENT_FLAT = {'CritChance','CritMultiplier','PoisonChance','PoisonDamage','PoisonDamageMult',
 'PhysicalToFire','PhysicalToCold','PhysicalToLightning','ReturningProjectileDamage',
 'HealthRegenPercent','BlockChance','FireResist','ColdResist','LightningResist','PhysicalResist',
 'MoveSpeed','AreaOfEffect','ProjectileSpeed','PushbackResist'}

def section(t, name):
    m = re.search(r'^  '+name+r':\n(.*?)(?=^  \w|\Z)',t,re.M|re.S)
    return m[1] if m else ''

def fields(t, indent=4):
    return dict(re.findall(r'^'+' '*indent+r'(\w+): ?(.*)$',t,re.M))

def vec(s):
    a,b = re.search(r'x: ([\d.eE+-]+), y: ([\d.eE+-]+)',s).groups()
    return {'x':float(a),'y':float(b)}

def read_layout():
    text = SOURCE.read_text(encoding='utf-8-sig')
    clusters = []
    for block in re.split(r'^  - ID: ',section(text,'Clusters'),flags=re.M)[1:]:
        f=fields(block); clusters.append({'id':block.splitlines()[0], 'center':vec(f['Center']),
            'radii':[float(r) for r in re.findall(r'^    - Radius: (.+)',block,re.M)]})
    cs={c['id']:c for c in clusters}; nodes=[]
    for block in re.split(r'^  - ID: ',section(text,'Nodes'),flags=re.M)[1:]:
        f=fields(block); raw=vec(f['Position']); cid=f['ClusterID']; orbit=int(f['OrbitIndex']); angle=float(f['OrbitAngle'])
        mode=int(f['PlacementMode']); pos=raw.copy()
        if mode==1:
            c=cs[cid]; rad=math.radians(angle); r=c['radii'][orbit]
            pos={'x':c['center']['x']+math.cos(rad)*r, 'y':c['center']['y']+math.sin(rad)*r}
        con=re.search(r'^    ConnectionIDs:\n(.*?)(?=^    \w|\Z)',block,re.M|re.S)
        nodes.append({'id':block.splitlines()[0], 'nodeType':TYPE[int(f['NodeType'])], 'nodeTypeValue':int(f['NodeType']),
            'placementMode':mode, 'rawPosition':raw, 'position':pos, 'clusterId':cid,
            'orbitIndex':orbit, 'orbitAngle':angle, 'isBackbone':f.get('IsBackbone')=='1',
            'originalConnections':re.findall(r'^    - (.+)',con[1],re.M) if con else []})
    curves=[]
    for block in re.split(r'^  - NodeIdA: ',section(text,'BezierConnections'),flags=re.M)[1:]:
        f=fields(block); curves.append({'a':block.splitlines()[0], 'b':f['NodeIdB'], 'anchorPercent':float(f['AnchorPercent']),
          'inOffset':vec(f['InHandleOffset']), 'outOffset':vec(f['OutHandleOffset'])})
    return nodes, clusters, curves

def m(stat,value,typ='Flat'):
    return {'Stat':stat, 'Value':value, 'Type':typ, 'TypeValue':MOD[typ]}
def inc(stat,value): return m(stat,value,'PercentAdd')
def more(stat,value): return m(stat,value,'PercentMult')
def less(stat,value): return m(stat,value,'PercentLess')
def res(value): return [m(s,value) for s in ['FireResist','ColdResist','LightningResist']]

spec={}
def put(ids, ru, en, family, mods, defense=(), note='', scaling=None):
    for node_id in ids.split():
        assert node_id not in spec, node_id
        spec[node_id]={'nameRu':ru,'nameEn':en, 'family':family, 'modifiers':mods,
          'defenseFamilies':list(defense), 'rationaleEn':note,
          'statScalingRules':scaling or [],'conditionalModifiers':[], 'triggeredEffects':[], 'hitScalingRules':[]}

from content_design import populate
from design_audit import minimum_allocation
DESIGN_GROUPS=populate(put,m,inc,more,less,res,TRANSPORT_IDS)

def effect_lines(n):
    out=[]
    for z in n['modifiers']:
        st,v,ty=z['Stat'],z['Value'],z['Type']; name=STATS[st]
        if ty=='Flat': out.append(f'{v:+g}'+(' п.п.' if st in PERCENT_FLAT else '')+f' — {name}')
        else:
            prefix={'PercentAdd':'увеличение','PercentSub':'уменьшение','PercentMult':'больше','PercentLess':'меньше'}[ty]
            out.append(f'{v:g}% {prefix} — {name}')
    for r in n['statScalingRules']:
        out.append(f"+{r['TargetValuePerStep']:g} п.п. множителя крита за каждые полные {r['SourceAmountPerStep']:g} итогового уклонения")
    return out or ['Старт; автоматически, без бонуса и без стоимости.']

def bfs(adj, sources, target):
    q=collections.deque(sources); prev={x:None for x in sources}
    while q:
        a=q.popleft()
        if a==target:
            path=[]
            while a is not None:path.append(a);a=prev[a]
            return path[::-1]
        for b in sorted(adj[a]):
            if b not in prev:prev[b]=a;q.append(b)
    return None

PATCHES=[{'a':'n30','b':'n74','reason':'Connect isolated small life leaf.'},
         {'a':'n33','b':'n73','reason':'Connect isolated small evasion leaf.'}]
ARCHETYPES=[
 {'id':'speed','goal':'n55','extendedGoals':['n178','n85'],
  'defenseOptions':[('life',['n37']),('resist',['n172']),('evasion',['n33'])]},
 {'id':'crit','goal':'n60','extendedGoals':['n149','n4'],
  'defenseOptions':[('evasion',['n38']),('life',['n67']),('resist',['n173'])]},
 {'id':'poison','goal':'n118','extendedGoals':['n87','n9'],
  'defenseOptions':[('evasion',['n75']),('life',['n129'])]},
 {'id':'range','goal':'n161','extendedGoals':['n117','n7'],
  'defenseOptions':[('life',['n37']),('resist',['n172']),('evasion',['n33'])]},
 {'id':'physical','goal':'n96','extendedGoals':['n63','n8'],
  'defenseOptions':[('evasion',['n33']),('life',['n37'])]},
 {'id':'elemental','goal':'n160','extendedGoals':['n128','n168','n2'],
  'defenseOptions':[('life',['n37']),('resist',['n172']),('evasion',['n33'])]}]
PORTFOLIOS={'speed':['n55','n178'],'crit':['n60','n149'],'poison':['n87','n118'],
 'range':['n161','n117'],'physical':['n63','n96'],'elemental':['n160','n128','n168']}

def build():
    nodes,clusters,curves=read_layout(); by={n['id']:n for n in nodes}
    assert set(by)==set(spec), f'Missing {set(by)-set(spec)}; extra {set(spec)-set(by)}'
    transport_source=TRANSPORT_TEMPLATE.read_text(encoding='utf-8-sig')
    transport_guid=re.search(r'^guid: (\w+)',TRANSPORT_TEMPLATE.with_suffix('.asset.meta').read_text(encoding='utf-8-sig'),re.M)[1]
    transport_mods=[(int(s),float(v),int(t)) for s,v,t in re.findall(r'  - Stat: (\d+)\n    Value: ([\d.]+)\n    Type: (\d+)',section(transport_source,'Modifiers'))]
    assert transport_mods==[(0,3,100),(15,5,100)],transport_mods
    assert len(TRANSPORT_IDS)==24 and len(set(TRANSPORT_IDS))==24
    free_small={n['id'] for n in nodes if n['nodeType']=='Small' and n['placementMode']==0}
    assert free_small==set(TRANSPORT_IDS)|set(SPECIALIZED_FREE_SMALL_IDS)
    assert not set(TRANSPORT_IDS)&set(SPECIALIZED_FREE_SMALL_IDS)
    template_evidence={'path':TRANSPORT_TEMPLATE.relative_to(ROOT).as_posix(),'guid':transport_guid,
      'sha256':hashlib.sha256(TRANSPORT_TEMPLATE.read_bytes()).hexdigest(),
      'nodeIds':TRANSPORT_IDS,'modifiers':[m('MaxHealth',3),m('Evasion',5)],
      'classification':'Explicit design role based on connecting roads, not a Unity NodeType or an IsBackbone rule.',
      'specializedFreeSmallExceptions':SPECIALIZED_FREE_SMALL_IDS}
    stats_source=(ROOT/'Assets/Scripts/Stats/StatType.cs').read_text(encoding='utf-8-sig')
    enum_body=re.sub(r'//[^\n]*','',stats_source)
    stat_names=re.findall(r'^\s*(\w+)\s*,?\s*$',enum_body,re.M)
    assert set(STATS)<=set(stat_names)
    adj={n['id']:set(n['originalConnections']) for n in nodes}
    missing=[]; asymmetric=[]
    for a,neighbors in adj.items():
        for b in neighbors:
            if b not in adj:missing.append((a,b))
            elif a not in adj[b]:asymmetric.append((a,b))
    assert not missing and not asymmetric,(missing,asymmetric)
    original_unreachable=[n for n in adj if bfs(adj,['start'],n) is None]
    for e in PATCHES:adj[e['a']].add(e['b']);adj[e['b']].add(e['a'])
    for n in nodes:
        n.update(spec[n['id']]); n['connections']=sorted(adj[n['id']]);n['cost']=0 if n['id']=='start' else 1
        transport=n['id'] in TRANSPORT_IDS
        n['designRole']='transport' if transport else 'start' if n['id']=='start' else 'specialized'
        visual={
          'speed':('Скорость','»','#f1bd45'), 'crit':('Крит','Кр','#c995ff'),
          'poison':('Яд','Я','#99cc48'), 'physical':('Физический урон','Ф','#ed80b2'),
          'elemental':('Стихии','Э','#629cff'), 'range':('Дальний бой','Д','#f59a55'),
          'hybrid':('Гибрид','Г','#e0d5a2'), 'transport':('Транспорт','', '#929a94'),
          'start':('Старт','S','#faf2d8')}
        if n['family']=='defense':
            f=set(n['defenseFamilies'])
            if 'resist' in f: style=('Сопротивления','С','#e1bf65') if 'life' not in f else ('Жизнь / сопротивления','Ж/С','#e79d76')
            elif 'block' in f: style=('Уклонение / блок','У/Б','#88b9eb')
            elif 'physical_resist' in f: style=('Уклонение / физ. защита','У/Ф','#a5aee8')
            elif 'evasion' in f: style=('Уклонение','У','#44d8cb') if 'life' not in f else ('Уклонение / жизнь','У/Ж','#71d39d')
            elif 'life' in f: style=('Жизнь','Ж','#f0757f')
            else: style=('Восстановление','+','#c0e69a')
        else: style=visual[n['family']]
        for stat,label,symbol,color in [('PhysicalToCold','Конверсия: холод','Х','#69d8ff'),('PhysicalToFire','Конверсия: огонь','О','#ff6b52'),('PhysicalToLightning','Конверсия: молния','М','#93a4ff')]:
            if any(z['Stat']==stat for z in n['modifiers']):style=(label,symbol,color)
        n['visualStyle']={'label':style[0],'symbol':style[1],'color':style[2]}
        n['templateMode']='reuse-existing' if transport else 'none' if n['id']=='start' else 'create-new'
        n['templateKey']='transportRogue' if transport else ('Rogue_Small_'+re.sub(r'[^A-Za-z0-9]+','',n['nameEn']) if n['nodeType']=='Small' else 'Rogue_'+n['id'])
        n['templatePath']=template_evidence['path'] if transport else 'Assets/Resources/PassiveTrees/Templates/Rogue/'+n['templateKey']+'.asset'
        if transport:
            n['existingTemplateGuid']=transport_guid
            assert n['modifiers']==template_evidence['modifiers'] and not n['statScalingRules'] and not n['defenseFamilies']
        n['effectsRu']=effect_lines(n);n['startPath']=bfs(adj,['start'],n['id']);n['startCost']=len(n['startPath'])-1
        n['modifiers']=[dict(z,StatValue=stat_names.index(z['Stat'])) for z in n['modifiers']]
    # Each authored feeder group is one repeated template, including leaf nodes.
    small_covered=[]
    for g in DESIGN_GROUPS:
        signatures={json.dumps([by[i][k] for k in ['nameEn','modifiers','statScalingRules']],sort_keys=True) for i in g['smallIds']}
        assert len(signatures)==1,g['id']
        small_covered.extend(g['smallIds'])
        for i in g['smallIds']+g['notableIds']:by[i]['designGroupId']=g['id']
        g['smallTemplatePath']=by[g['smallIds'][0]]['templatePath']
        g['smallEffectsRu']=by[g['smallIds'][0]]['effectsRu']
    assert len(small_covered)==len(set(small_covered))==72
    assert set(small_covered)=={n['id'] for n in nodes if n['nodeType']=='Small' and n['designRole']=='specialized'}
    registry={}
    for n in nodes:
        if n['designRole']!='specialized':continue
        path=n['templatePath'];payload={k:n[k] for k in ['nameEn','nameRu','modifiers','statScalingRules','conditionalModifiers','triggeredEffects','hitScalingRules']}
        if path in registry:assert registry[path]['content']==payload,path
        else:registry[path]={'path':path,'content':payload,'nodeIds':[]}
        registry[path]['nodeIds'].append(n['id'])
    no_keys={k:{v for v in vals if by[v]['nodeType']!='Keystone'} for k,vals in adj.items() if by[k]['nodeType']!='Keystone'}
    portfolios=[]
    for family,goals in PORTFOLIOS.items():
        safe=minimum_allocation(no_keys,goals);unrestricted=minimum_allocation(adj,goals)
        unrestricted['keystoneIds']=[i for i in unrestricted['allocatedIds'] if by[i]['nodeType']=='Keystone']
        portfolios.append({'family':family,'goals':goals,'withoutKeystones':safe,'withKeystones':unrestricted,
          'pairwiseDistances':[{'a':a,'b':b,'edges':len(bfs(adj,[a],b))-1} for j,a in enumerate(goals) for b in goals[j+1:]]})
    assert all(d['edges']>=7 for p in portfolios for d in p['pairwiseDistances'])
    assert len(registry)==43
    conversion_nodes=[n for n in nodes if any(m['Stat'].startswith('PhysicalTo') for m in n['modifiers'])]
    assert {n['id'] for n in conversion_nodes}==set(PORTFOLIOS['elemental'])
    assert all(sum(m['Value'] for m in n['modifiers'] if m['Stat'].startswith('PhysicalTo'))==40 for n in conversion_nodes)
    damage_defense=[]
    for goal in [n['id'] for n in nodes if n['nodeType'] in ('Notable','Keystone') and n['family']!='defense']:
        core=bfs(adj,['start'],goal);allocated=set(core)
        allowed={k:{v for v in vals if by[v]['nodeType']!='Keystone' or v in allocated} for k,vals in adj.items() if by[k]['nodeType']!='Keystone' or k in allocated}
        choices=[]
        for target in nodes:
            if target['family']!='defense' or target['id'] in allocated:continue
            path=bfs(allowed,core,target['id'])
            if path and len(path)-1<=4:
                choices.append({'target':target['id'],'families':target['defenseFamilies'],'additionalCost':len(path)-1})
        assert any(a['target']!=b['target'] and any(f!=g for f in a['families'] for g in b['families']) for a in choices for b in choices),goal
        damage_defense.append({'goal':goal,'corePath':core,'choicesWithin4':choices})
    audits=[]
    for a in ARCHETYPES:
        core=a.get('core') or bfs(adj,['start'],a['goal']); opts=[]
        assert all(b in adj[c] for c,b in zip(core,core[1:])),core
        for fam,targets in a['defenseOptions']:
            allocated=set(core);extra=[]
            for target in targets:
                path=bfs(adj,sorted(allocated),target)
                for p in path:
                    if p not in allocated:extra.append(p);allocated.add(p)
            assert all(fam in by[t]['defenseFamilies'] for t in targets)
            assert all(by[t]['designRole']=='specialized' for t in targets)
            assert not any(by[p]['nodeType']=='Keystone' for p in extra),extra
            opts.append({'family':fam,'targets':targets,'additionalPath':extra,'additionalCost':len(extra)})
        assert len({x['family'] for x in opts if x['additionalCost']<=4})>=2,(a['id'],opts)
        travel_count=sum(by[x]['designRole']=='transport' for x in core)
        audits.append(dict(a,nameRu=FAMILIES[a['id']][0],corePath=core,coreCost=len(core)-1,
          coreTransportCount=travel_count,coreTransportHealth=3*travel_count,
          coreTransportEvasion=5*travel_count,defenseOptions=opts))
    edges=[{'a':a,'b':b,'proposed':any({a,b}=={e['a'],e['b']} for e in PATCHES)} for a in adj for b in adj[a] if a<b]
    base_hash=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
    revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    data={'schemaVersion':2,'designRevision':4,'status':'proposed','date':'2026-10-23','source':str(SOURCE.relative_to(ROOT)).replace('\\','/'),
      'sourceSha256':base_hash,'sourceRevision':revision,'nodeTypes':TYPE,'modifierTypes':MOD,
      'transportTemplate':template_evidence,
      'designGroups':DESIGN_GROUPS,'templateRegistry':list(registry.values()),'portfolioAudit':portfolios,
      'allDamageGoalDefenseAudit':damage_defense,
      'coordinateSystem':'Unity tree canvas; +X right, +Y down. Preserve rawPosition for orbit nodes; position is resolved.',
      'families':{k:{'label':v[0],'color':v[1]} for k,v in FAMILIES.items()}, 'nodes':nodes,'clusters':clusters,
      'originalBezierConnections':curves,'edges':edges,'proposedConnections':PATCHES,'archetypes':audits,
      'validation':{'counts':dict(collections.Counter(n['nodeType'] for n in nodes)),
       'designRoleCounts':dict(collections.Counter(n['designRole'] for n in nodes)),
       'transportIds':TRANSPORT_IDS,'transportFlatHealth':3,'transportFlatEvasion':5,
       'newTemplateCount':len(registry),'newSmallTemplateCount':len({n['templatePath'] for n in nodes if n['nodeType']=='Small' and n['designRole']=='specialized'}),
       'homogeneousFeederGroups':len(DESIGN_GROUPS),'allSpecializedSmallNodesCovered':True,'existingTransportTemplateReused':True,
       'originalUnreachable':original_unreachable,'proposedUnreachable':[n for n in adj if bfs(adj,['start'],n) is None],
       'originalEdgeCount':len(edges)-len(PATCHES),'proposedEdgeCount':len(edges),
       'allNodesAssigned':True,'sourceAssetModifiedByBuilder':False,
       'defenseCriterion':'For each archetype core route, at least two distinct defense families on separate branch options within 4 additional allocated nodes; start is free. Transport baseline bonuses do not qualify as a defense option.'}}
    (HERE/'rogue-passive-tree.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    with (HERE/'rogue-passive-tree.csv').open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.writer(f);w.writerow(['ID','Type','DesignRole','DesignGroup','Name_RU','Name_EN','Family','X_resolved','Y_resolved','X_serialized','Y_serialized','PlacementMode','ClusterID','OrbitIndex','OrbitAngle','Effects_RU','Modifiers_JSON','StatScalingRules_JSON','Connections_proposed','Connections_original','TemplateMode','TemplatePath'])
        for n in nodes:w.writerow([n['id'],n['nodeType'],n['designRole'],n.get('designGroupId',''),n['nameRu'],n['nameEn'],n['family'],n['position']['x'],n['position']['y'],n['rawPosition']['x'],n['rawPosition']['y'],n['placementMode'],n['clusterId'],n['orbitIndex'],n['orbitAngle'],'; '.join(n['effectsRu']),json.dumps(n['modifiers']),json.dumps(n['statScalingRules']),';'.join(n['connections']),';'.join(n['originalConnections']),n['templateMode'],n['templatePath']])
    lines=['# Rogue passive nodes - proposed v4','', '130 nodes; 96 Small, 25 Notable, 8 Keystone, 1 free Start. Roles: 24 transport, 105 specialized, 1 start. Every transport grants exactly +3 MaxHealth Flat and +5 Evasion Flat by reusing transportRogue. Coordinates use +Y down. JSON is canonical. This is a proposal, not live tree content.','', '| ID | Type / design role | Name | X / Y (resolved) | Effects | Connections (proposed) |','|---|---|---|---|---|---|']
    for n in nodes:lines.append(f"| {n['id']} | {n['nodeType']} / {n['designRole']} | {n['nameRu']} / {n['nameEn']} | {n['position']['x']:.6f} / {n['position']['y']:.6f} | {'; '.join(n['effectsRu'])} | {', '.join(n['connections'])} |")
    (HERE/'NODES.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    (HERE/'validation.json').write_text(json.dumps(dict(data['validation'],designRevision=4,sourceSha256=base_hash,archetypes=audits,portfolioAudit=portfolios,homogeneousGroups=DESIGN_GROUPS,allDamageGoalDefenseAudit=damage_defense),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    template=(HERE/'viewer.template.html').read_text(encoding='utf-8')
    (HERE/'rogue-passive-tree.html').write_text(template.replace('/*__DATA__*/',json.dumps(data,ensure_ascii=False).replace('</','<\\/')),encoding='utf-8')
    defense_lines=['| Archetype / core goal | Core cost | Defense family / target | Additional node IDs | Extra points |',
      '|---|---:|---|---|---:|']
    for a in audits:
        for o in a['defenseOptions']:
            defense_lines.append(f"| {a['id']} / {a['goal']} | {a['coreCost']} | {o['family']} / {', '.join(o['targets'])} | {' -> '.join(o['additionalPath'])} | {o['additionalCost']} |")
    hybrids=[]
    for fam,goal in [('speed','n87'),('elemental','n161'),('poison','n178')]:
        a=next(x for x in audits if x['id']==fam)
        allowed={k:set(v) for k,v in adj.items() if by[k]['nodeType']!='Keystone'}
        allowed={k:{b for b in v if b in allowed} for k,v in allowed.items()}
        path=bfs(allowed,a['corePath'],goal);extra=path[1:]
        hybrids.append({'from':fam,'goal':goal,'extension':extra,'extraCost':len(extra),'totalCost':a['coreCost']+len(extra)})
    data['hybridExamples']=hybrids
    # Include the same hybrids in all machine/visual payloads.
    (HERE/'rogue-passive-tree.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (HERE/'rogue-passive-tree.html').write_text(template.replace('/*__DATA__*/',json.dumps(data,ensure_ascii=False).replace('</','<\\/')),encoding='utf-8')
    hybrid_lines=['| Start with | Additional target | New nodes, no keystones | Extra / total points |','|---|---|---|---:|']
    for h in hybrids:hybrid_lines.append(f"| {h['from']} | {h['goal']} | {' -> '.join(h['extension'])} | {h['extraCost']} / {h['totalCost']} |")
    handoff=(HERE/'HANDOFF.template.md').read_text(encoding='utf-8').replace('__SHA256__',base_hash).replace('__REVISION__',revision)
    cluster_lines=['| Group | Location | Identical small IDs | Notable / keystone IDs | Repeated small effects |','|---|---|---|---|---|']
    for g in DESIGN_GROUPS:cluster_lines.append(f"| {g['id']} | {g['locationRu']} | {', '.join(g['smallIds'])} | {', '.join(g['notableIds'])} | {'; '.join(g['smallEffectsRu'])} |")
    portfolio_lines=['| Direction | Separated goals | Minimum points, no keystones | Minimum points, all edges |','|---|---|---:|---:|']
    for p in portfolios:portfolio_lines.append(f"| {p['family']} | {', '.join(p['goals'])} | {p['withoutKeystones']['minimumPoints']} | {p['withKeystones']['minimumPoints']} |")
    (HERE/'CLUSTERS.md').write_text('# Rogue v4: dispersed clusters and repeated small nodes\n\nEach feeder group repeats one small template. Read one small node, then its notable(s).\n72 specialized small nodes use 10 shared packages. All node geometry/types remain unchanged.\n\n'+ '\n'.join(cluster_lines)+'\n\n## Exact minimum allocation costs for all listed notable goals\n\n'+ '\n'.join(portfolio_lines)+'\n\nCosts include free start and shared prefixes, not a finished build. The cheaper outer-ring route includes all keystone drawbacks. Full in-tree conversion requires all three 40% notables: n160 cold (north), n128 lightning (east), n168 fire (south-west). Skills and gear can reduce the required tree investment.\n',encoding='utf-8')
    handoff=handoff.replace('__DEFENSE_TABLE__','\n'.join(defense_lines)).replace('__HYBRID_TABLE__','\n'.join(hybrid_lines))
    handoff=handoff.replace('__CLUSTER_TABLE__','\n'.join(cluster_lines)).replace('__PORTFOLIO_TABLE__','\n'.join(portfolio_lines)).replace('__TEMPLATE_COUNT__',str(len(registry)))
    (HERE/'HANDOFF.md').write_text(handoff,encoding='utf-8')
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==base_hash
    assert hashlib.sha256(TRANSPORT_TEMPLATE.read_bytes()).hexdigest()==template_evidence['sha256']
    print(json.dumps({'nodes':len(nodes),'edges':len(edges),'counts':data['validation']['counts'],'unreachableOriginal':original_unreachable,
      'defenseCosts':{a['id']:[(o['family'],o['additionalCost']) for o in a['defenseOptions']] for a in audits}},ensure_ascii=False))
    return data

if __name__=='__main__':build()
