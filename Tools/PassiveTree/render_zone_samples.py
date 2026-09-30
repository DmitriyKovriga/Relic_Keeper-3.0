"""Render the real Unity geometry exported by PassiveZoneCompositionTests.

python Tools/PassiveTree/render_zone_samples.py /absolute/path/to/contact-sheet.png
Uses Pillow; never writes gameplay assets.
"""
import argparse
import json
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    paths = sorted((ROOT / 'Library/PassiveZonePreviews').glob('seed-*-zone-0.json'), key=lambda p: int(p.name.split('-')[1]))
    if not paths:
        raise SystemExit('Run PassiveZoneCompositionTests in Unity first.')
    width, height, cols = 560, 420, 3
    canvas = Image.new('RGB', (width * cols, height * math.ceil(len(paths) / cols)), '#303338')
    font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 18)
    for i, path in enumerate(paths):
        data = json.loads(path.read_text(encoding='utf-8-sig'))
        tile = Image.new('RGB', (width, height), '#303338')
        draw = ImageDraw.Draw(tile)
        scale = 0.64
        def point(p):
            return (width / 2 + p['x'] * scale, height - 38 + p['y'] * scale)
        for x in range(0, width, 20):draw.line((x, 0, x, height), fill='#34383d')
        for y in range(0, height, 20):draw.line((0, y, width, y), fill='#34383d')
        for c in data['clusters']:
            x, y = point(c['Center'])
            for orbit in c['Orbits']:
                r = orbit['Radius'] * scale
                if r > 0:draw.ellipse((x-r,y-r,x+r,y+r), outline='#555568', width=1)
        for edge in data['edges']:
            draw.line([point(p) for p in edge['points']], fill='#e9c75a', width=2)
        for node in data['nodes']:
            x, y = point(node['p']);r = [14,20,26,20][node['type']] * scale
            fill = '#303338' if node['type']==0 else '#285966' if node['type']==1 else '#75592e' if node['type']==2 else '#3b744e'
            draw.ellipse((x-r,y-r,x+r,y+r), fill=fill, outline='#eee9dd', width=1)
        count = sum(not n['backbone'] and n['p']['y'] < 0 for n in data['nodes'])
        draw.rectangle((0,0,width,37), fill='#303338')
        draw.text((16,10), f"Seed {data['seed']}  /  5 entities  /  {count} nodes", fill='#eee9dd', font=font)
        canvas.paste(tile, ((i % cols)*width, (i // cols)*height))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(args.output)
    print(f'Rendered {len(paths)} actual Unity layouts to {args.output}')


if __name__ == '__main__':
    main()
