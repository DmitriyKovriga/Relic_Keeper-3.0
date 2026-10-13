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
draw.text((28, 18), 'WARRIOR | 9 PixelLab variants', font=title_font, fill='#eadcc5')
draw.text((28, 64), 'Original', font=label_font, fill='#c1b7a4')
original = Image.open(root / 'Reference.png').convert('RGBA')
sheet.alpha_composite(original.resize((128, 128), Image.Resampling.NEAREST), (30, 88))
draw.text((195, 101), 'Same native pixel scale in every option.', font=label_font, fill='#c1b7a4')
draw.text((195, 136), '32 x 32 transparent PNGs | hands free for equipment', font=small_font, fill='#8f9992')
for j in jobs:
    i = j['number'] - 1
    x, y = 12 + i % 3 * 316, 246 + i // 3 * 326
    draw.rounded_rectangle((x, y, x + 304, y + 314), radius=10, fill='#252e2c', outline='#404c45')
    draw.text((x + 14, y + 9), f"{j['number']:02}  {j['name']}", font=label_font, fill='#eadcc5')
    im = Image.open(root / f"Warrior-{j['number']:02}.png").convert('RGBA')
    assert im.size == (32, 32) and im.getbbox() is not None
    assert im.getextrema()[3][0] == 0
    sheet.alpha_composite(im.resize((256, 256), Image.Resampling.NEAREST), (x + 24, y + 48))
    sheet.alpha_composite(im, (x + 264, y + 271))
sheet.convert('RGB').save(root / 'Comparison.png')
print('Comparison ready: nine native-size transparent sprites verified.')
