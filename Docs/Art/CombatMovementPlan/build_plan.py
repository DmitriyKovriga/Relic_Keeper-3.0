"""Generate documentation only. Never writes Unity source, assets, scenes or settings."""
from pathlib import Path
import collections, hashlib, json, re, subprocess
from plan_data import OPTIONS, PACKAGES, SOURCES

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
SOURCE_PATHS=[
 'Assets/Scripts/Visuals/PlayerCharacterAnimationController.cs',
 'Assets/Scripts/Visuals/PlayerMovementVisual.cs','Assets/Scripts/Visuals/PlayerRenderAnchor.cs',
 'Assets/Scripts/Visuals/WeaponHandAnimationAnchor.cs','Assets/Scripts/PlayerBasicScripts/PlayerMovement.cs',
 'Assets/Scripts/PlayerBasicScripts/PlayerAttackInput.cs','Assets/Scripts/Skills/PlayerSkillManager.cs',
 'Assets/Scripts/Skills/Logic/SkillStepRunner.cs','Assets/Scripts/Skills/Logic/SkillStepRunner.SpinAround.cs',
 'Assets/Scripts/Skills/Logic/SkillsModules/SkillHandAnimation.cs','Assets/Scripts/Skills/Steps/SkillRecipeSO.cs',
 'Assets/Scripts/Enemy/EnemyHealth.cs','Assets/Scripts/Enemy/EnemyEntity.cs','Assets/Scripts/Enemy/EnemyAnimationBridge.cs',
 'Assets/Scripts/Combat/DamageSnapshot.cs','Assets/Scripts/GameplayEvents/GameplayEventContext.cs',
 'Assets/Scripts/GameplayEvents/GameplayEventBus.cs','Assets/Scripts/Skills/SkillHitStop.cs',
 'Assets/Scripts/Visuals/GroundingVisualController.cs','Assets/Scripts/Visuals/GroundSurfaceProfileSO.cs',
 'Assets/Scripts/Enemy/EnemyDeathEffectSpawner.cs','Assets/Scripts/Enemy/EnemyDeathRemainsSheet.cs',
 'Assets/Scripts/Enemy/EnemyDeathVfxPool.cs','Assets/Scripts/Systems/PoolManager.cs',
 'Assets/Tests/EditMode/Editor/HitStopTests.cs','Assets/Scenes/HubScene.unity','Packages/manifest.json']

def build():
    hashes={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in SOURCE_PATHS}
    inventory=collections.Counter();recipe_files=[]
    for p in (ROOT/'Assets/Resources/Skills').rglob('*.asset'):
        t=p.read_text(encoding='utf-8-sig');m=re.search(r'^  HitStopFrames: (\d+)',t,re.M)
        if m:inventory[int(m[1])]+=1;recipe_files.append(p.relative_to(ROOT).as_posix())
    enemies=[]
    for p in (ROOT/'Assets/Resources/Enemy').glob('*.asset'):
        t=p.read_text(encoding='utf-8-sig')
        field=lambda key: (re.search(r'^\s+'+key+r': ?(.*)$',t,re.M) or [None,''])[1]
        enemies.append({'asset':p.relative_to(ROOT).as_posix(),'hitStateName':field('HitStateName'),
          'hitSpritePath':field('HitSpritesResourcePath'),'controller':field('Controller')})
    ids={o['id'] for o in OPTIONS}
    assert len(OPTIONS)==len(ids)==24
    assert all(1<=o[k]<=5 for o in OPTIONS for k in ['gain','setup','maintenance'])
    assert all(set(p['ids'])<=ids for p in PACKAGES)
    parked={'C03','C06','C08'}
    assert all(not set(p['ids'])&parked for p in PACKAGES)
    data={'date':'2026-10-27','status':'planning-only',
      'planRevision':2,'decisions':{'body':'procedural body-only follow; weapon animation remains independent',
        'authoredAttackPoses':'excluded from recommended path','audio':'deferred explicitly by user',
        'targetMaterials':'optional only after measured visual benefit over universal impact'},
      'sourceRevision':subprocess.check_output(['git','rev-parse','--short','HEAD'],cwd=ROOT,text=True).strip(),
      'options':OPTIONS,'packages':PACKAGES,'sources':SOURCES,'sourceHashes':hashes,
      'hitStopInventory':{'counts':dict(sorted(inventory.items())),'assetCount':sum(inventory.values()),
        'paths':recipe_files,'scope':'raw saved assets containing HitStopFrames; not a runtime active-pool count'},
      'enemyHitReferences':enemies,
      'counts':{'combat':sum(o['id'].startswith('C') for o in OPTIONS),'movement':sum(o['id'].startswith('M') for o in OPTIONS),
        'mechanicsChanges':sum(o['changesGameplay'] for o in OPTIONS),'activeProposals':len(OPTIONS)-len(parked)},
      'verificationBoundary':'Source and serialized references only; no new Unity compile/test/Play Mode/performance run.'}
    (HERE/'plan.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    table=['| ID | Proposal | Gain / 5 | Setup / 5 | Maintenance / 5 | Prototype days | Changes mechanics |',
      '|---|---|---:|---:|---:|---|---|']
    details=[]
    for o in OPTIONS:
        table.append(f"| {o['id']} | {o['title']} | {o['gain']} | {o['setup']} | {o['maintenance']} | {o['prototypeDays']} | {'YES' if o['changesGameplay'] else 'No intended rule change'} |")
        details.extend([f"### {o['id']} — {o['title']}",'',o['change'],'',f"- Example: {o['example']}",
          f"- Current state / reuse: {o['currentState']}. {o['reuse']}",f"- Ongoing work: {o['ongoing']}",f"- Main risk: {o['risk']}",''])
    inventory_text=f"{sum(inventory.values())} assets; "+', '.join(f'{v} with value {k}' for k,v in sorted(inventory.items()))
    plan=(HERE/'PLAN.template.md').read_text(encoding='utf-8').replace('__REVISION__',data['sourceRevision'])
    plan=plan.replace('__HITSTOP_INVENTORY__',inventory_text).replace('__OPTIONS_TABLE__','\n'.join(table)).replace('__OPTION_DETAILS__','\n'.join(details))
    (HERE/'PLAN.md').write_text(plan,encoding='utf-8')
    research=['# Combat/movement research notes','', '2026-10-27. Primary sources and official documentation; no third-party summary is used as technical authority.','']
    for s in SOURCES:
        research.extend([f"## {s['id']}: {s['title']}",'',f"Source: [{s['title']}]({s['url']})",'',f"Verification: {s['status']}.",'',s['finding'],'',f"Relic Keeper application: {s['application']}",''])
    research.extend(['## Local evidence','', 'Graphify MCP was used for focused cross-file discovery, followed by direct reads of source, recipes, enemy data and tests. It is not the authority for serialized scene wiring. plan.json records file hashes and source locations.','',
      'No new gameplay video was supplied or captured for this task. Current feel and regressions must be assessed in Play Mode before implementation/tuning conclusions. The HTML schematic is authored explanatory motion, not a captured result.','',
      'Inference: the generic body/weapon phase split and busy-state hit reaction suppression are plausible contributors to the user-reported lack of body participation. This is a source-supported hypothesis, not a measured causal proof.'])
    (HERE/'RESEARCH.md').write_text('\n'.join(research)+'\n',encoding='utf-8')
    template=(HERE/'plan.template.html').read_text(encoding='utf-8')
    (HERE/'combat-movement-plan.html').write_text(template.replace('/*DATA*/',json.dumps(data,ensure_ascii=False).replace('</','<\\/')),encoding='utf-8')
    assert hashes=={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in SOURCE_PATHS}
    print(json.dumps({'options':len(OPTIONS),'counts':data['counts'],'sourceAssetsUnchanged':True,'hitStopRawCounts':dict(inventory)}))

if __name__=='__main__':build()
