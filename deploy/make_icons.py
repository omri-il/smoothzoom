"""Draws the two app icons into assets/*.ico (run once; the .ico files are committed).

SmoothZoom     = the yellow cursor ring it draws, on a dark tile.
SmoothAnnotate = a white pen on a red tile.

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


def pen(size):
    """A pencil at 45°: point lower-left, body, then an eraser band upper-right."""
    img, d, s = tile(size, (229, 72, 77, 255))
    k = 0.70710678
    ux, uy = k, -k          # along the pencil, point → eraser
    nx, ny = k, k           # across it
    tx, ty = s * 7 / 32, s * 25 / 32   # the point
    h = s * 3.2 / 32        # half width

    def at(along, across):
        return (tx + ux * along + nx * across, ty + uy * along + ny * across)

    cone, body_end, eraser_end = s * 6 / 32, s * 20 / 32, s * 24 / 32
    white, pink = (255, 255, 255, 255), (255, 205, 210, 255)
    d.polygon([at(0, 0), at(cone, h), at(cone, -h)], fill=white)                       # point
    d.polygon([at(cone + s / 64, h), at(body_end, h), at(body_end, -h), at(cone + s / 64, -h)],
              fill=white)                                                                # body
    d.polygon([at(body_end + s / 64, h), at(eraser_end, h), at(eraser_end, -h),
               at(body_end + s / 64, -h)], fill=pink)                                    # eraser
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
    save(pen, "smoothannotate.ico")
