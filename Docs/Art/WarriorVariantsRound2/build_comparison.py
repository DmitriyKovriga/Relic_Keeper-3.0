"""Preview packaging only; generated source pixels remain unchanged."""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent
jobs = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))
font_path = 'C:/Windows/Fonts/segoeui.ttf'
title_font = ImageFont.truetype(font_path, 26)
label_font = ImageFont.truetype(font_path, 21)
small_font = ImageFont.truetype(font_path, 16)
sheet = Image.new('RGBA', (960, 1240), (28, 34, 34, 255))
draw = ImageDraw.Draw(sheet)
draw.text((28, 18), 'WARRIOR | Round 2 - original heads', font=title_font, fill='#eadcc5')
draw.text((28, 64), 'Original head', font=label_font, fill='#c1b7a4')
original = Image.open(root / 'OriginalReference.png').convert('RGBA')
sheet.alpha_composite(original.resize((128, 128), Image.Resampling.NEAREST), (30, 88))
draw.text((220, 64), 'Round 1 - option 06 head', font=label_font, fill='#c1b7a4')
six = Image.open(root / 'Variant06Reference.png').convert('RGBA')
sheet.alpha_composite(six.resize((128, 128), Image.Resampling.NEAREST), (225, 88))
draw.text((470, 101), 'Heads protected by an inpainting mask.', font=label_font, fill='#c1b7a4')
draw.text((470, 136), 'Outfits vary | same native pixel scale', font=small_font, fill='#8f9992')
for j in jobs:
    i = j['number'] - 1
    x, y = 12 + i % 3 * 316, 246 + i // 3 * 326
    draw.rounded_rectangle((x, y, x + 304, y + 314), radius=10, fill='#252e2c', outline='#404c45')
    draw.text((x + 14, y + 9), f"{j['number']:02}  {j['name']}", font=label_font, fill='#eadcc5')
    draw.text((x + 14, y + 37), 'Original head' if j['source'] == 'OriginalReference.png' else 'Head from round 1 / 06', font=small_font, fill='#8f9992')
    im = Image.open(root / f"Warrior-{j['number']:02}.png").convert('RGBA')
    source = Image.open(root / j['source']).convert('RGBA')
    mask = Image.open(root / 'OutfitMask.png').convert('L')
    for py in range(32):
        for px in range(32):
            if mask.getpixel((px, py)) == 0:
                assert im.getpixel((px, py)) == source.getpixel((px, py)), f"Protected pixel changed: option {j['number']} ({px},{py})"
    assert im.size == (32, 32) and im.getbbox() is not None
    assert im.getextrema()[3][0] == 0
    sheet.alpha_composite(im.resize((256, 256), Image.Resampling.NEAREST), (x + 24, y + 48))
    sheet.alpha_composite(im, (x + 264, y + 271))
sheet.convert('RGB').save(root / 'Comparison.png')
print('Comparison ready: nine transparent sprites and all protected head pixels verified.')
