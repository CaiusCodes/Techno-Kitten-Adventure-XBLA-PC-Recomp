"""Render original TKA social artwork. Requires Pillow; no game assets."""
from __future__ import annotations

import math
import re
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image, ImageColor, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "docs" / "social-preview.png"
SIZE = (1280, 640)
S = 2


def rgb(value: str):
    return ImageColor.getrgb(value)


def pt(x: float, y: float):
    return round(x * S), round(y * S)


def font(name: str, size: int):
    return ImageFont.truetype(str(Path("C:/Windows/Fonts") / name), size * S)


def cat_paths(draw: ImageDraw.ImageDraw):
    """Rasterize only the M/L/C/Z paths in this project's original cat SVG."""
    tree = ET.parse(ROOT / "src" / "Tka.Host" / "Assets" / "game-kitty.svg")
    for node in tree.getroot():
        tokens = re.findall(r"[MLCZ]|-?\d+(?:\.\d+)?", node.attrib["d"])
        segments: list[list[tuple[int, int]]] = []
        current: list[tuple[int, int]] = []
        raw = (0.0, 0.0)
        index = 0
        while index < len(tokens):
            command = tokens[index]
            index += 1
            if command == "M":
                if current:
                    segments.append(current)
                raw = float(tokens[index]), float(tokens[index + 1])
                index += 2
                current = [pt(902 + raw[0] * 1.08, 147 + raw[1] * 1.08)]
            elif command == "L":
                raw = float(tokens[index]), float(tokens[index + 1])
                index += 2
                current.append(pt(902 + raw[0] * 1.08, 147 + raw[1] * 1.08))
            elif command == "C":
                values = list(map(float, tokens[index:index + 6]))
                index += 6
                begin = raw
                a, b, end = (values[0], values[1]), (values[2], values[3]), (values[4], values[5])
                for step in range(1, 17):
                    t = step / 16
                    u = 1 - t
                    x = u**3 * begin[0] + 3*u*u*t*a[0] + 3*u*t*t*b[0] + t**3*end[0]
                    y = u**3 * begin[1] + 3*u*u*t*a[1] + 3*u*t*t*b[1] + t**3*end[1]
                    current.append(pt(902 + x * 1.08, 147 + y * 1.08))
                raw = end
            elif command == "Z":
                current.append(current[0])
            else:
                raise ValueError("Unexpected cat SVG command")
        if current:
            segments.append(current)
        fill = node.attrib.get("fill")
        stroke = node.attrib.get("stroke")
        for points in segments:
            if fill and fill != "none":
                draw.polygon(points, fill=rgb(fill))
            if stroke:
                width = round(float(node.attrib["stroke-width"]) * 1.08 * S)
                draw.line(points, fill=rgb(stroke), width=width, joint="curve")
                if node.attrib.get("stroke-linecap") == "round":
                    radius = width // 2
                    for end in (points[0], points[-1]):
                        draw.ellipse((end[0]-radius, end[1]-radius, end[0]+radius, end[1]+radius), fill=rgb(stroke))


def main():
    canvas = Image.new("RGB", (SIZE[0] * S, SIZE[1] * S), "#11152e")
    draw = ImageDraw.Draw(canvas)
    for y in range(SIZE[1] * S):
        t = y / (SIZE[1] * S)
        color = tuple(round((1-t)*a+t*b) for a, b in zip(rgb("#1b173c"), rgb("#090f23")))
        draw.line((0, y, SIZE[0]*S, y), fill=color)
    # Soft neon light remains behind the typography and cat.
    glow = Image.new("RGBA", canvas.size)
    light = ImageDraw.Draw(glow)
    light.ellipse((pt(680,-95), pt(1430,670)), fill=(255, 53, 166, 72))
    light.ellipse((pt(-320,360), pt(770,810)), fill=(66, 233, 243, 35))
    glow = glow.filter(ImageFilter.GaussianBlur(100*S))
    canvas = Image.alpha_composite(canvas.convert("RGBA"), glow)
    draw = ImageDraw.Draw(canvas)
    for x in range(0, SIZE[0]+1, 40):
        draw.line((pt(x,0),pt(x,SIZE[1])), fill=(108, 105, 157, 35), width=S)
    for y in range(0, SIZE[1]+1, 40):
        draw.line((pt(0,y),pt(SIZE[0],y)), fill=(108, 105, 157, 35), width=S)
    for y in (82,589):
        for x in range(SIZE[0]):
            t=x/SIZE[0]
            color = (255,round(61+172*t),round(168+77*t),255)
            draw.line((pt(x,y),pt(x+1,y)), fill=color, width=4*S)
    draw.line((pt(96,109),pt(414,109)),fill=rgb("#45e9f5"),width=7*S)
    for x,color in [(439,"#ff3da8"),(455,"#a16dff"),(471,"#45e9f5")]:
        draw.ellipse((pt(x-4,105),pt(x+4,113)),fill=rgb(color))
    bold=font("ariblk.ttf",87)
    for word,baseline,color in [("TECHNO",210,"#ffffff"),("KITTEN",310,"#ff3da8"),("ADVENTURE",410,"#ffffff")]:
        draw.text(pt(86,baseline),word,font=bold,fill=rgb(color),anchor="ls",stroke_width=0)
    draw.line((pt(87,447),pt(684,447)),fill=rgb("#a16dff"),width=6*S)
    draw.text(pt(86,508),"XBLA PC PORT",font=font("segoeuib.ttf",31),fill=rgb("#45e9f5"),anchor="ls",spacing=7*S)
    draw.ellipse((pt(862,107),pt(1218,463)),fill=rgb("#21163e"),outline=rgb("#804ce3"),width=5*S)
    draw.arc((pt(881,126),pt(1199,444)),start=265,end=145,fill=rgb("#ff3da8"),width=9*S)
    draw.arc((pt(897,142),pt(1183,428)),start=50,end=260,fill=rgb("#45e9f5"),width=6*S)
    cat_paths(draw)
    for n,x in enumerate(range(846,1260,17)):
        heights=(22,45,75,31,96,42,21,71,111,47,26,76,41,61,21,75,42,22)
        h=heights[n%len(heights)]
        color=rgb("#ff3da8") if n<8 else rgb("#45e9f5")
        draw.line((pt(x,498),pt(x,498-h)),fill=color,width=8*S)
    draw.line((pt(101,553),pt(451,553)),fill=(255,255,255,70),width=2*S)
    for x,y,r,color in [(776,169,5,"#45e9f5"),(791,158,2,"#ff3da8"),(1200,112,4,"#ff3da8"),(1179,526,3,"#45e9f5")]:
        draw.ellipse((pt(x-r,y-r),pt(x+r,y+r)),fill=rgb(color))
    canvas = canvas.convert("RGB").resize(SIZE,Image.Resampling.LANCZOS)
    canvas.save(OUT,format="PNG",optimize=True)
    assert Image.open(OUT).size == SIZE
    print(f"Rendered {OUT} ({SIZE[0]}x{SIZE[1]})")


if __name__ == "__main__":
    main()
