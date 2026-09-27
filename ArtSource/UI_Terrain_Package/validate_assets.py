"""Offline acceptance checks for the UI / terrain handoff package."""
from __future__ import annotations

import math
import struct
from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "Export"
spec = spec_from_file_location("terrain_assets", ROOT / "generate_assets.py")
generator = module_from_spec(spec)
spec.loader.exec_module(generator)

raw = (OUT / "Field_Wasteland_513.raw").read_bytes()
assert len(raw) == 513 * 513 * 2, f"RAW byte length mismatch: {len(raw)}"
heights = struct.unpack("<" + "H" * (513 * 513), raw)
assert max(heights) > 30000, f"heightmap lacks raised terrain: {max(heights)}"
assert min(heights) >= 0
pgm = (OUT / "Field_Wasteland_513.pgm").read_bytes()
assert pgm.startswith(b"P5\n513 513\n65535\n")
assert len(pgm.split(b"\n", 3)[3]) == 513 * 513 * 2

spacing = 88.0 / 512
for cx, cz in [(-15, -14), (22, 24), (-9, -14), (0, -10), (17, 19)]:
    maximum = 0.0
    for iz in range(513):
        z = -44 + iz * spacing
        if abs(z - cz) > 6:
            continue
        for ix in range(513):
            x = -44 + ix * spacing
            if math.hypot(x - cx, z - cz) <= 6:
                maximum = max(maximum, generator.terrain_height(x, z))
    assert maximum <= 1.0, f"I-23 pad {(cx, cz)} exceeds 0±1m: max {maximum:.3f}m"

max_slope = 0.0
max_encounter_height = 0.0
for iz in range(round(46 / spacing), round(66 / spacing)):
    z = -44 + iz * spacing
    for ix in range(round(29 / spacing), round(59 / spacing)):
        x = -44 + ix * spacing
        max_encounter_height = max(max_encounter_height, generator.terrain_height(x, z))
        dx = (generator.terrain_height(x + spacing, z) - generator.terrain_height(x - spacing, z)) / (2 * spacing)
        dz = (generator.terrain_height(x, z + spacing) - generator.terrain_height(x, z - spacing)) / (2 * spacing)
        max_slope = max(max_slope, math.hypot(dx, dz))
assert math.degrees(math.atan(max_slope)) < 30, f"Encounter basin slope too steep: {max_slope:.3f}"
assert max_encounter_height < 3.0, f"Encounter basin has obstructive high ridge: {max_encounter_height:.2f}m"
for edge in [(-44, 0), (44, 0), (0, -44), (0, 44)]:
    assert generator.terrain_height(*edge) > 5.0, f"Terrain outer rim is too low at {edge}"

icons = sorted((OUT / "Icons").glob("UI_Icon_*.png"))
assert len(icons) == 15, f"expected 15 icon assets, got {len(icons)}"
for path in icons:
    im = Image.open(path)
    assert im.mode == "RGBA" and im.size == (128, 128), f"invalid icon {path.name}: {im.mode}/{im.size}"
    assert im.getpixel((0, 0))[3] == 0, f"{path.name} lost alpha transparency"
map_base = Image.open(OUT / "MAP_Wasteland_Base.png")
assert map_base.size == (1024, 1024)
font = (OUT / "NotoSansSC-VF.ttf").read_bytes()
assert font[:4] == b"\x00\x01\x00\x00" and len(font) > 10_000_000
assert "SIL OPEN FONT LICENSE Version 1.1" in (OUT / "OFL-NotoSansSC.txt").read_text(encoding="utf-8")

print(f"PASS: 513x513 16-bit RAW/PGM, {len(icons)} RGBA icons, 1024x1024 map base, OFL font")
print(f"PASS: all five I-23 6m pads <=1m; encounter max {max_encounter_height:.2f}m and slope <= {math.degrees(math.atan(max_slope)):.1f}deg; outer rim >5m")
