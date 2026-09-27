"""Generate Unity-ready UI icons, a top-down wasteland map skin and Terrain heightmaps."""
from __future__ import annotations

import math
import random
import struct
import zlib
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parent / "Export"
ICONS = ROOT / "Icons"
ICONS.mkdir(parents=True, exist_ok=True)
ROOT.mkdir(parents=True, exist_ok=True)

INK = (49, 53, 49, 255)
PAPER = (218, 211, 189, 255)
AMBER = (208, 119, 59, 255)
MUTED = (143, 133, 109, 255)


def path_icon(name: str, draw_fn) -> None:
    im = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # Ink silhouette on a dark enamel plate; thin inset rule stays legible at UI scale.
    d.rounded_rectangle((5, 5, 123, 123), radius=19, fill=INK, outline=AMBER, width=4)
    d.rounded_rectangle((11, 11, 117, 117), radius=15, outline=(112, 116, 101, 255), width=2)
    draw_fn(d)
    im.save(ICONS / f"{name}.png", optimize=True)


def line(d, pts, fill=PAPER, width=8):
    d.line(pts, fill=fill, width=width, joint="curve")


def badge(d, box, label=None):
    d.ellipse(box, fill=AMBER, outline=PAPER, width=4)
    if label:
        x, y = (box[0] + box[2]) // 2, (box[1] + box[3]) // 2
        d.ellipse((x - 5, y - 5, x + 5, y + 5), fill=INK)


def icons():
    path_icon("UI_Icon_Party", lambda d: (d.ellipse((42, 25, 86, 69), outline=PAPER, width=7), d.arc((28, 57, 100, 117), 180, 360, fill=PAPER, width=8), line(d, [(31, 91), (46, 79), (63, 87), (81, 78), (98, 93)], AMBER, 5)))
    path_icon("UI_Icon_Items", lambda d: (d.rounded_rectangle((37, 43, 91, 105), 10, outline=PAPER, width=7), d.arc((46, 22, 82, 63), 180, 360, fill=AMBER, width=7), line(d, [(46, 68), (82, 68)], MUTED, 5), d.rectangle((55, 79, 73, 96), outline=PAPER, width=5)))
    path_icon("UI_Icon_Tank", lambda d: (d.rounded_rectangle((27, 39, 101, 93), 12, fill=MUTED, outline=PAPER, width=5), d.rounded_rectangle((43, 49, 85, 81), 8, fill=INK, outline=AMBER, width=4), line(d, [(63, 65), (63, 27), (104, 27)], PAPER, 8), d.ellipse((34, 89, 54, 109), fill=INK, outline=AMBER, width=4), d.ellipse((74, 89, 94, 109), fill=INK, outline=AMBER, width=4)))
    path_icon("UI_Icon_Quests", lambda d: (d.rounded_rectangle((35, 24, 95, 105), 6, outline=PAPER, width=7), d.rounded_rectangle((49, 17, 81, 33), 5, fill=AMBER), line(d, [(48, 52), (57, 61), (74, 43)], AMBER, 6), line(d, [(48, 77), (57, 86), (74, 68)], PAPER, 6), line(d, [(80, 54), (88, 54)], MUTED, 4), line(d, [(80, 80), (88, 80)], MUTED, 4)))
    path_icon("UI_Icon_System", lambda d: (d.ellipse((38, 38, 90, 90), outline=PAPER, width=8), d.ellipse((53, 53, 75, 75), fill=AMBER), *[line(d, [p1, p2], PAPER, 8) for p1, p2 in [((64, 20), (64, 36)), ((64, 92), (64, 108)), ((20, 64), (36, 64)), ((92, 64), (108, 64)), ((32, 32), (43, 43)), ((85, 85), (96, 96)), ((32, 96), (43, 85)), ((85, 43), (96, 32))]]))
    path_icon("UI_Icon_Map", lambda d: (d.polygon([(28, 35), (52, 25), (77, 35), (101, 25), (101, 91), (77, 102), (52, 92), (28, 102)], outline=PAPER), line(d, [(52, 27), (52, 92)], AMBER, 5), line(d, [(77, 36), (77, 101)], AMBER, 5), line(d, [(35, 69), (47, 60), (57, 67), (72, 52), (88, 57)], PAPER, 4), badge(d, (80, 43, 94, 57))))
    path_icon("UI_Icon_HP", lambda d: (d.polygon([(64, 104), (23, 66), (23, 47), (33, 34), (49, 34), (64, 49), (79, 34), (95, 34), (105, 47), (105, 66)], fill=AMBER, outline=PAPER), line(d, [(64, 48), (64, 87)], PAPER, 8), line(d, [(45, 67), (83, 67)], PAPER, 8)))
    path_icon("UI_Icon_SP", lambda d: (d.polygon([(70, 20), (38, 69), (58, 69), (48, 106), (91, 54), (69, 54)], fill=AMBER, outline=PAPER), line(d, [(42, 104), (85, 28)], PAPER, 3)))
    path_icon("UI_Icon_Foot", lambda d: (d.ellipse((34, 23, 68, 76), fill=PAPER), d.ellipse((59, 55, 94, 108), fill=AMBER), *[d.ellipse((x, y, x + 11, y + 11), fill=PAPER) for x, y in [(27, 18), (40, 13), (54, 18), (57, 91), (72, 105), (87, 98)]]))
    path_icon("UI_Icon_Interact", lambda d: (d.rounded_rectangle((27, 27, 101, 81), 14, outline=PAPER, width=7), d.polygon([(42, 78), (35, 96), (59, 80)], fill=PAPER), d.text((51, 36), "E", fill=AMBER, stroke_width=1, stroke_fill=AMBER)) )
    path_icon("UI_Icon_Cannon", lambda d: (d.rounded_rectangle((40, 51, 87, 83), 9, fill=MUTED, outline=PAPER, width=5), line(d, [(73, 62), (109, 62)], PAPER, 11), d.ellipse((30, 48, 54, 86), fill=INK, outline=AMBER, width=5), line(d, [(64, 84), (54, 103), (83, 103), (73, 84)], AMBER, 5)))
    path_icon("UI_Icon_MachineGun", lambda d: (line(d, [(29, 61), (93, 61)], PAPER, 10), line(d, [(71, 61), (103, 61)], AMBER, 6), d.rectangle((51, 49, 74, 74), fill=MUTED, outline=PAPER, width=4), line(d, [(57, 72), (48, 97)], AMBER, 6), line(d, [(38, 45), (38, 33), (53, 33)], PAPER, 5)))
    path_icon("UI_Icon_Ammo", lambda d: (d.polygon([(43, 40), (64, 22), (85, 40), (85, 92), (43, 92)], fill=AMBER, outline=PAPER), line(d, [(50, 92), (50, 103), (78, 103), (78, 92)], PAPER, 6), line(d, [(49, 64), (79, 64)], INK, 5), d.ellipse((56, 47, 72, 62), outline=INK, width=4)))
    path_icon("UI_Icon_Armor", lambda d: (d.polygon([(64, 22), (99, 37), (94, 74), (64, 105), (34, 74), (29, 37)], fill=MUTED, outline=PAPER), line(d, [(64, 34), (64, 89)], AMBER, 7), line(d, [(46, 61), (82, 61)], AMBER, 7)))
    path_icon("UI_Icon_Repair", lambda d: (line(d, [(40, 91), (86, 45)], PAPER, 14), d.ellipse((70, 25, 105, 60), outline=AMBER, width=8), d.arc((24, 57, 69, 105), 30, 265, fill=PAPER, width=10), line(d, [(40, 30), (52, 43)], AMBER, 7)))


def smoothstep(a: float, b: float, v: float) -> float:
    t = max(0.0, min(1.0, (v - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def point_line_distance(x, z, ax, az, bx, bz):
    dx, dz = bx - ax, bz - az
    den = dx * dx + dz * dz
    t = 0 if den == 0 else max(0.0, min(1.0, ((x - ax) * dx + (z - az) * dz) / den))
    return math.hypot(x - (ax + t * dx), z - (az + t * dz))


def terrain_height(x: float, z: float) -> float:
    # Broad broken salt ridges and shallow drainage basins; never scatter rock/tree props.
    coarse = (0.32 * math.sin(x * .17 + z * .08) * math.cos(z * .19 - x * .055)
              + .20 * math.sin(x * .095 - z * .12)
              + .10 * math.cos(z * .33 + x * .24))
    ridge_a = 2.9 * math.exp(-((x + 17) / 7.5) ** 2 - ((z - 8) / 20) ** 2)
    ridge_b = 3.2 * math.exp(-((x - 14) / 8.5) ** 2 - ((z + 5) / 13) ** 2)
    ridge_c = 2.2 * math.exp(-((x + 3) / 12) ** 2 - ((z - 26) / 6) ** 2)
    road = min(point_line_distance(x, z, -15, -14, -7, -7),
               point_line_distance(x, z, -7, -7, 0, 2),
               point_line_distance(x, z, 0, 2, 11, 17),
               point_line_distance(x, z, 11, 17, 22, 24))
    low_route = 1.0 - smoothstep(2.3, 6.0, road)
    playable_edge = max(abs(x), abs(z))
    barrier = 7.8 * smoothstep(27.0, 39.0, playable_edge)
    barrier += 1.3 * math.exp(-((playable_edge - 35.0) / 3.4) ** 2)
    raw = max(0.0, .30 + coarse + ridge_a + ridge_b + ridge_c + barrier)
    raw *= 1.0 - .67 * low_route

    # All interface-designated interaction and encounter pads remain flat and safely walkable.
    pads = [(-15, -14, 6.0), (22, 24, 6.0), (-9, -14, 6.0), (0, -10, 6.0), (17, 19, 6.0)]
    # Blend the 30x20 encounter basin smoothly into the surrounding landform.
    basin_blend = .78 * math.exp(-.72 * ((x / 17.0) ** 2 + ((z - 12.0) / 12.5) ** 2))
    raw = raw * (1.0 - basin_blend) + (0.24 + coarse * .16) * basin_blend
    blend = 1.0
    for px, pz, radius in pads:
        blend = min(blend, smoothstep(radius, radius + 3.2, math.hypot(x - px, z - pz)))
    return max(0.03, .12 + (raw - .12) * blend)


def write_heightmaps():
    resolution = 513
    size = 88.0
    minimum = -size / 2.0
    max_height = 12.0
    vals = []
    preview = Image.new("L", (resolution, resolution))
    pixels = preview.load()
    for row in range(resolution):
        z = minimum + size * row / (resolution - 1)
        for col in range(resolution):
            x = minimum + size * col / (resolution - 1)
            h = min(max_height, terrain_height(x, z))
            normalized = max(0, min(65535, round(h / max_height * 65535)))
            vals.append(normalized)
            pixels[col, resolution - 1 - row] = round(normalized / 257)
    (ROOT / "Field_Wasteland_513.raw").write_bytes(struct.pack("<" + "H" * len(vals), *vals))
    preview.save(ROOT / "Field_Wasteland_HeightPreview.png", optimize=True)
    # Human-readable PGM preserves 16-bit values for tools that can import portable graymaps.
    with (ROOT / "Field_Wasteland_513.pgm").open("wb") as f:
        f.write(f"P5\n{resolution} {resolution}\n65535\n".encode("ascii"))
        f.write(b"".join(struct.pack(">H", n) for n in vals))


def map_skin():
    n = 1024
    im = Image.new("RGB", (n, n))
    px = im.load()
    rng = random.Random(230927)
    for y in range(n):
        z = -29 + 58 * (n - 1 - y) / (n - 1)
        for xpix in range(n):
            x = -29 + 58 * xpix / (n - 1)
            h = terrain_height(x, z)
            grain = rng.randrange(-5, 6)
            wash = 3 * math.sin(x * .55 + z * .22) + 2 * math.cos(z * .7 - x * .35)
            stops = [(0.0, (169, 168, 146)), (.7, (194, 177, 139)), (2.0, (174, 150, 113)), (4.5, (148, 129, 103)), (9.0, (126, 116, 100))]
            for (h0, c0), (h1, c1) in zip(stops, stops[1:]):
                if h <= h1:
                    t = smoothstep(h0, h1, h)
                    col = tuple(round(a * (1 - t) + b * t) for a, b in zip(c0, c1))
                    break
            else:
                col = stops[-1][1]
            px[xpix, y] = tuple(max(0, min(255, int(c + grain + wash))) for c in col)
    d = ImageDraw.Draw(im, "RGBA")
    # Road trace follows the walkable corridor but deliberately contains no location markers.
    def mapxy(wx, wz):
        return (int((wx + 29) / 58 * n), int((29 - wz) / 58 * n))
    route = [mapxy(-15, -14), mapxy(-7, -7), mapxy(0, 2), mapxy(11, 17), mapxy(22, 24)]
    d.line(route, fill=(204, 187, 148, 175), width=17, joint="curve")
    d.line(route, fill=(127, 111, 86, 185), width=3, joint="curve")
    # Fine contour rings are graphic ink lines, not labels or state markers.
    for i in range(16):
        center = (90 + i * 53) % n, (140 + i * 71) % n
        radius = 20 + (i * 17) % 55
        d.ellipse((center[0] - radius, center[1] - radius * .7, center[0] + radius, center[1] + radius * .7), outline=(88, 84, 69, 48), width=2)
    # Atlas frame and sparse corner registration marks leave room for service-drawn icons/text.
    d.rectangle((8, 8, n - 9, n - 9), outline=(54, 57, 51, 220), width=7)
    d.rectangle((19, 19, n - 20, n - 20), outline=(223, 208, 171, 180), width=2)
    for cx, cy in [(34, 34), (n - 34, 34), (34, n - 34), (n - 34, n - 34)]:
        d.ellipse((cx - 7, cy - 7, cx + 7, cy + 7), fill=(208, 119, 59, 230))
    im.save(ROOT / "MAP_Wasteland_Base.png", optimize=True)


def icon_sheet():
    files = sorted(ICONS.glob("*.png"))
    cell = 150
    columns = 5
    rows = math.ceil(len(files) / columns)
    sheet = Image.new("RGB", (columns * cell, rows * cell), (39, 42, 39))
    d = ImageDraw.Draw(sheet)
    for i, path in enumerate(files):
        x, y = (i % columns) * cell, (i // columns) * cell
        icon = Image.open(path).convert("RGBA").resize((108, 108), Image.Resampling.LANCZOS)
        sheet.paste(icon, (x + 21, y + 5), icon)
        d.text((x + 8, y + 119), path.stem.replace("UI_Icon_", ""), fill=(218, 211, 189))
    sheet.save(ROOT / "UI_Icons_Preview.png", optimize=True)


def main():
    icons()
    icon_sheet()
    write_heightmaps()
    map_skin()
    print(f"Generated {len(list(ICONS.glob('*.png')))} icons, a 513x513 16-bit heightmap set, and a 1024x1024 map base in {ROOT}")


if __name__ == "__main__":
    main()
