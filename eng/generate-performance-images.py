"""Reproducible original image-size fixtures; requires Pillow, no external artwork."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1] / 'src/GnazzosWorld.App/Resources/Raw'
root.mkdir(parents=True, exist_ok=True)
image = Image.new('RGBA', (1200, 1400))
pixels = image.load()
for y in range(1400):
    for x in range(1200):
        pixels[x, y] = (25 + x * 180 // 1200, 40 + y * 160 // 1400, 120 + ((x // 30 + y // 30) % 2) * 60, 255)
draw = ImageDraw.Draw(image)
for n in range(80, 1160, 120):
    draw.rectangle((n, 140, n + 45, 1250), fill=(240, 240, 230, 255))
files = []
for name, item in [('perf_large.png', image), ('perf_thumb.png', image.resize((96, 112), Image.Resampling.LANCZOS))]:
    path = root / name
    item.save(path, optimize=True)
    data = path.read_bytes()
    files.append({'name': name, 'width': item.width, 'height': item.height,
                  'encodedBytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(),
                  'rgbaPlaneBytes': item.width * item.height * 4})
(root / 'performance-images.json').write_text(json.dumps({'schema': 1, 'fixture': 'original-image-perf-v1', 'images': files}, indent=2) + '\n')
print('Generated two original size variants and their integrity/dimension manifest.')
