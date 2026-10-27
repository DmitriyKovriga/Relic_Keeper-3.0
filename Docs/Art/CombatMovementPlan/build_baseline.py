"""Package actual Unity baseline frames; never generates or edits game artwork."""
import csv
import argparse
import hashlib
import json
import math
from pathlib import Path
import subprocess
from PIL import Image, ImageDraw

PROJECT = Path(__file__).resolve().parents[3]
SOURCE = PROJECT / 'Logs/CombatBaseline'
DEST = Path(__file__).resolve().parent / 'Baseline'
EXPECTED = ['miss', 'direct-hit', 'evaded', 'zero-damage', 'lethal', 'dot', 'attacking-target']


def main():
    global SOURCE, DEST, EXPECTED
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--confirmation', action='store_true', help='Export the step 2 validation without overwriting the original baseline.')
    parser.add_argument('--poses', action='store_true', help='Export the step 3 body/recoil comparisons.')
    args = parser.parse_args()
    if args.confirmation:
        SOURCE = PROJECT / 'Logs/CombatHitConfirmation'
        DEST = Path(__file__).resolve().parent / 'HitConfirmation'
    if args.poses:
        SOURCE = PROJECT / 'Logs/CombatPoseComparison'
        DEST = Path(__file__).resolve().parent / 'PoseComparison'
        EXPECTED = ['cleave-off', 'cleave-on', 'heavy-off', 'heavy-on', 'attacking-target', 'zombie-recoil', 'rogue-left', 'mage-air']
    audit = json.loads((SOURCE / 'audit.json').read_text(encoding='utf-8'))
    if audit['status'] != 'complete':
        raise RuntimeError('Export requires a completed live audit: ' + str(audit.get('error')))
    cases = audit['cases']
    if [c['name'] for c in cases] != EXPECTED or any(c.get('error') for c in cases):
        raise RuntimeError('All expected live cases must pass before export.')
    DEST.mkdir(exist_ok=True)
    sheet = Image.new('RGB', (1080, len(cases) * 238), '#20292c')
    sheet_draw = ImageDraw.Draw(sheet)
    summaries = []
    reference_phase = next(e['phase'] for c in cases if c['name'] == ('cleave-on' if args.poses else 'direct-hit')
                           for e in c['events'] if e['type'] == 'DamageTaken')
    for row, case in enumerate(cases):
        name = case['name']
        files = sorted((SOURCE / name).glob('frame-*.png'))
        if len(files) != len(case['frames']):
            raise RuntimeError(name + ': image/log frame counts disagree')
        images = [Image.open(path).convert('RGB') for path in files]
        if any(im.size != (480, 270) for im in images):
            raise RuntimeError('Expected native 480x270 camera output')
        for frame, im in enumerate(images):
            ImageDraw.Draw(im).text((8, 7), f'{name} | frame {frame:03} | native 480x270', fill='#e1e9df')
        palette_source = Image.new('RGB', (480, 270 * 8))
        impacts = [e['frame'] for e in case['events'] if e['type'] == 'DamageTaken']
        impact = impacts[0] if impacts else len(images) // 3
        for i, frame in enumerate([0, 12, len(images)//4, len(images)//2, impact, impact+2, len(images)*3//4, len(images)-1]):
            palette_source.paste(images[min(frame, len(images) - 1)], (0, i * 270))
        palette = palette_source.quantize(colors=256)
        frames = [im.quantize(palette=palette, dither=Image.Dither.NONE) for im in images]
        for suffix, timing in [('normal', [20, 10, 20]), ('slow-4x', [70, 60, 70])]:
            frames[0].save(DEST / f'{name}-{suffix}.gif', save_all=True, append_images=frames[1:],
                           duration=[timing[i % 3] for i in range(len(frames))], loop=0, optimize=False)
        strikes = [e['frame'] for e in case['events'] if e['type'].startswith('TakeDamage returned') or e['type'] == 'DamageTaken']
        contact = strikes[0] if strikes else min(range(len(case['frames'])), key=lambda i: abs(case['frames'][i]['phase'] - reference_phase))
        recovery_phase = .9 if args.poses and name.startswith('heavy') else .65
        recovery = min(range(len(case['frames'])), key=lambda i: abs(case['frames'][i]['phase'] - recovery_phase))
        preparation = min(range(len(case['frames'])), key=lambda i: abs(case['frames'][i]['phase'] -
                          (.6 if name.startswith('heavy') else .12))) if args.poses else 11
        strike_label = 'DoT tick' if name == 'dot' else 'contact / strike'
        for column, (label, frame) in enumerate([('preparation' if args.poses else 'before', preparation), (strike_label, contact), ('recovery', recovery)]):
            crop = images[frame].crop((160, 80, 340, 185)).resize((360, 210), Image.Resampling.NEAREST)
            sheet.paste(crop, (column * 360, row * 238 + 25))
            sheet_draw.text((column * 360 + 5, row * 238 + 5), f'{name}: {label}, f{frame}', fill='#dce8df')
        with (DEST / f'{name}-poses.csv').open('w', newline='', encoding='utf-8') as output:
            writer = csv.writer(output)
            writer.writerow(['frame', 'cast_token', 'phase', 'duration', 'time_scale', 'casting', 'enemy_busy', 'body_sprite',
                             'body_x', 'body_y', 'body_angle', 'hand_x', 'hand_y', 'hand_angle', 'weapon_visible', 'target_hp'])
            for f in case['frames']:
                writer.writerow([f['frame'], case['castToken'], f['phase'], f['duration'], f['timeScale'], f['casting'],
                                 f['enemyBusy'], f['bodySprite'], f['bodyPosition']['x'], f['bodyPosition']['y'],
                                 f['bodyRotation']['z'], f['handPosition']['x'], f['handPosition']['y'], f['handRotation']['z'],
                                 f['weaponVisible'], f['health']])
        summary = {key: value for key, value in case.items() if key != 'frames'}
        summary['frozen_frames'] = sum(f['timeScale'] == 0 for f in case['frames'])
        summary['frame_count'] = len(case['frames'])
        summary['resolution_return'] = 'not-called' if not case['resolutionCount'] else str(case['returnedTrue']).lower()
        summary['body_sprite_samples'] = sorted({f['bodySprite'] for f in case['frames']})
        summary['target_sprite_samples'] = sorted({f['targetSprite'] for f in case['frames']})
        summary['body_local_x_range'] = [min(f['bodyPosition']['x'] for f in case['frames']), max(f['bodyPosition']['x'] for f in case['frames'])]
        summary['body_rotation_range'] = [min(f['bodyRotation']['z'] for f in case['frames']), max(f['bodyRotation']['z'] for f in case['frames'])]
        if args.poses:
            signed = [(f['bodyRotation']['z'] + 180) % 360 - 180 for f in case['frames']]
            summary['body_signed_degrees'] = [min(signed), max(signed)]
            summary['recoil_visible_frames'] = sum(f.get('enemyRecoilVisible', False) for f in case['frames'])
        summaries.append(summary)
    sheet.save(DEST / 'contact-sheet.png')
    audit['cases'] = summaries
    audit['revision'] = subprocess.check_output(['git', 'rev-parse', '--short', 'HEAD'], cwd=PROJECT, text=True).strip()
    paths = [audit['skill'], audit['recipe'], 'Assets/Scripts/Enemy/EnemyHealth.cs',
             'Assets/Scripts/Skills/Logic/SkillBehaviour.cs', 'Assets/Scripts/Skills/Logic/SkillStepRunner.cs',
             'Assets/Scripts/Diagnostics/CombatBaselineCapture.cs',
             'Assets/Scripts/Visuals/PlayerMovementVisual.cs']
    if args.confirmation or args.poses:
        paths += ['Assets/Scripts/Combat/AttackImpactContext.cs', 'Assets/Scripts/Combat/ConfirmedHit.cs',
                  'Assets/Scripts/Skills/Projectiles/SkillProjectile.cs',
                  'Assets/Scripts/Skills/Logic/SkillsModules/SkillDamageDealer.cs']
    if args.poses:
        paths += ['Assets/Scripts/Visuals/AttackBodyFollow.cs', 'Assets/Scripts/Visuals/CombatPoseProfile.cs',
                  'Assets/Scripts/Visuals/EnemyHitRecoil.cs', 'Assets/Scripts/Enemy/EnemyEntity.cs',
                  'Assets/Resources/Visuals/CombatPoseProfile.asset',
                  'Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/WeightOfAGiant/WeightOfAGiantSkill.asset',
                  'Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/WeightOfAGiant/Recipe_WeightOfAGiant.asset']
    audit['source_sha256'] = {p: hashlib.sha256((PROJECT / p).read_bytes()).hexdigest() for p in paths}
    audit['capture_note'] = 'Actual Play Mode camera frames, fixed capture rate 60. Slow GIF changes playback only. Poses are sampled after visual LateUpdate, before render rounding. GIF quantization is for review; native PNGs remain under Logs.'
    (DEST / 'audit.json').write_text(json.dumps(audit, indent=2), encoding='utf-8')
    if args.poses:
        comparisons = []
        by_name = {case['name']: case for case in cases}
        for label in ['cleave', 'heavy']:
            off, on = by_name[label + '-off'], by_name[label + '-on']
            max_position, max_rotation = 0, 0
            for frame in on['frames']:
                reference = min(off['frames'], key=lambda f: (abs(f['phase'] - frame['phase']), abs(f['frame'] - frame['frame'])))
                for field in ['handPosition', 'weaponPosition']:
                    max_position = max(max_position, math.dist([frame[field][k] for k in 'xyz'], [reference[field][k] for k in 'xyz']))
                for field in ['handRotation', 'weaponRotation']:
                    max_rotation = max(max_rotation, *[abs((frame[field][k] - reference[field][k] + 180) % 360 - 180) for k in 'xyz'])
            comparisons.append({'skill': label, 'maximum_socket_position_delta': max_position,
                                'maximum_socket_degrees_delta': max_rotation,
                                'damage_phases_off': [e['phase'] for e in off['events'] if e['type'] == 'DamageTaken'],
                                'damage_phases_on': [e['phase'] for e in on['events'] if e['type'] == 'DamageTaken']})
            comparison_frames = []
            for i in range(min(len(off['frames']), len(on['frames']))):
                panel = Image.new('RGB', (840, 254), '#20292c')
                for col, case in enumerate([off, on]):
                    original = Image.open(SOURCE / case['name'] / f'frame-{i:03}.png').convert('RGB')
                    panel.paste(original.crop((170, 95, 310, 173)).resize((420, 234), Image.Resampling.NEAREST), (col * 420, 20))
                    ImageDraw.Draw(panel).text((col * 420 + 8, 5), 'BEFORE' if col == 0 else 'BODY FOLLOW + RECOIL', fill='#e1e9df')
                comparison_frames.append(panel)
            palette_source = Image.new('RGB', (840, 254 * 6))
            for i, index in enumerate([0, 18, len(comparison_frames)//2, int(len(comparison_frames)*.7), int(len(comparison_frames)*.8), len(comparison_frames)-1]):
                palette_source.paste(comparison_frames[index], (0, i * 254))
            palette = palette_source.quantize(colors=256)
            comparison_frames = [im.quantize(palette=palette, dither=Image.Dither.NONE) for im in comparison_frames]
            comparison_frames[0].save(DEST / f'{label}-comparison.gif', save_all=True, append_images=comparison_frames[1:],
                                      duration=[[20, 10, 20][i % 3] for i in range(len(comparison_frames))], loop=0, optimize=False)
        (DEST / 'weapon-comparison.json').write_text(json.dumps(comparisons, indent=2), encoding='utf-8')
    print(json.dumps([{'case': c['name'], 'taken': c['damageTakenEvents'], 'dealt': c['damageDealtEvents'],
                       'evaded': c['evadeEvents'], 'returned_true': c['returnedTrue'], 'frozen_frames': c['frozen_frames'],
                       'busy_at_damage': c['busyAtDamage']} for c in summaries], indent=2))


if __name__ == '__main__':
    main()
