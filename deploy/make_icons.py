"""Draws the app icon into assets/smoothzoom.ico (run once; the .ico is committed).

SmoothZoom = the yellow cursor ring it draws, on a dark tile. (The drawing app's pencil
icon moved with it to the SmoothDraw repo on 2026-09-28.)

Each .ico holds 16-256 px sizes, every size drawn at 4x and downsampled, so the
16 px tray / Start-menu icon stays crisp. Needs Pillow:  py deploy/make_icons.py
"""
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
SS = 4  # supersampling factor


def tile(size, colour):
    img = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    s = size * SS
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=s * 7 // 32, fill=colour)
    return img, d, s


def ring(size):
    img, d, s = tile(size, (30, 30, 46, 255))
    w = max(s * 3 // 32, SS * 2)  # stroke never thinner than 2 px at 16 px
    pad = s * 6 // 32
    # ImageDraw replaces pixels rather than blending, so the faint fill is pre-mixed
    # (24% yellow over the tile) instead of drawn translucent — that would punch a hole
    d.ellipse([pad, pad, s - pad, s - pad], fill=(84, 78, 47, 255),
              outline=(255, 230, 50, 255), width=w)
    return img.resize((size, size), Image.LANCZOS)


def save(draw, name):
    out = ROOT / "assets" / name
    out.parent.mkdir(exist_ok=True)
    frames = [draw(n) for n in SIZES]
    frames[-1].save(out, format="ICO", sizes=[(n, n) for n in SIZES],
                    append_images=frames[:-1])
    print("wrote", out.relative_to(ROOT))


if __name__ == "__main__":
    save(ring, "smoothzoom.ico")
