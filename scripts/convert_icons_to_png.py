"""Convert any non-PNG ability icons (WebP / JPEG / etc) to real PNG bytes.

Fandom's CDN sometimes serves WebP under a .png filename. GDI+ in WinForms can't
decode WebP, so we re-encode every file as PNG before bundling.
"""

from __future__ import annotations

from pathlib import Path
from PIL import Image

DIRS = [
    Path("data/images/abilities"),
    Path("dist/data/images/abilities"),
    Path("bin/Debug/net10.0-windows/data/images/abilities"),
    Path("bin/Release/net10.0-windows/win-x64/publish/data/images/abilities"),
]


def convert_one(path: Path) -> bool:
    try:
        with Image.open(path) as img:
            if img.format == "PNG":
                return False
            converted = img.convert("RGBA")
        converted.save(path, format="PNG")
        return True
    except Exception as e:
        print(f"  fail {path.name}: {e}")
        return False


def main() -> int:
    converted = 0
    skipped = 0
    failed = 0
    for d in DIRS:
        if not d.exists():
            continue
        for png in d.glob("*.png"):
            try:
                with Image.open(png) as img:
                    fmt = img.format
                if fmt == "PNG":
                    skipped += 1
                    continue
                if convert_one(png):
                    converted += 1
                else:
                    failed += 1
            except Exception as e:
                print(f"  fail {png.name}: {e}")
                failed += 1
    print(f"converted: {converted}, skipped (already PNG): {skipped}, failed: {failed}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
