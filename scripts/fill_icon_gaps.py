"""For abilities NOT yet in data/abilities-icons.json, try a few naming conventions
to find their icon directly on Fandom and download it.

Naming variants probed (in order):
    "<lowercase no spaces>.png"
    "<exact no spaces>.png"
    "<underscored>.png"
    "<lowercase no spaces>.jpg"
    "<exact no spaces>.jpg"

Run:
    python scripts/fill_icon_gaps.py
"""

from __future__ import annotations

import json
import os
import re
import time
import urllib.parse
import urllib.request
from collections import Counter
from io import BytesIO
from pathlib import Path

API = "https://swtor.fandom.com/api.php"
HEADERS = {
    "User-Agent": "SwtorHolotracker/1.0 (icon gap filler)"
}
LOG_DIR = Path(os.path.expanduser("~/Documents/Star Wars - The Old Republic/CombatLogs"))
LINE_RE = re.compile(
    r"^\[(?P<time>\d+:\d+:\d+\.\d+)\]\s+"
    r"\[(?P<source>[^\]]*)\]\s+"
    r"\[(?P<target>[^\]]*)\]\s+"
    r"\[(?P<ability>[^\]]*)\]\s+"
    r"\[(?P<effect>[^\]]*)\]"
)
MAPPING_PATH = Path("data/abilities-icons.json")
ICONS_DIR = Path("data/images/abilities")


def collect_used_abilities() -> Counter[str]:
    counter: Counter[str] = Counter()
    for log in LOG_DIR.glob("combat_*.txt"):
        try:
            text = log.read_text(encoding="latin-1", errors="ignore")
        except Exception:
            continue
        for line in text.splitlines():
            m = LINE_RE.match(line)
            if not m:
                continue
            effect = m.group("effect").lower()
            if "damage" not in effect and "heal" not in effect:
                continue
            source = m.group("source")
            if not source.lstrip().startswith("@"):
                continue
            ability_raw = m.group("ability")
            brace = ability_raw.find("{")
            ability = (ability_raw[:brace] if brace > 0 else ability_raw).strip()
            if ability:
                counter[ability] += 1
    return counter


def name_variants(ability: str) -> list[str]:
    # Strip trailing "(suffix)" for naming purposes — try the bare name first.
    bare = re.sub(r"\s*\([^)]*\)\s*$", "", ability).strip()
    if not bare:
        bare = ability

    candidates: list[str] = []
    for base in {bare, ability}:
        no_space = base.replace(" ", "")
        underscore = base.replace(" ", "_")
        candidates += [
            no_space.lower() + ".png",
            no_space + ".png",
            underscore + ".png",
            no_space.capitalize() + ".png",
            no_space.lower() + ".jpg",
            no_space + ".jpg",
        ]
    # de-dup preserving order
    seen: set[str] = set()
    out: list[str] = []
    for c in candidates:
        if c not in seen:
            seen.add(c)
            out.append(c)
    return out


def api_get(params: dict) -> dict:
    query = urllib.parse.urlencode({**params, "format": "json"})
    req = urllib.request.Request(f"{API}?{query}", headers=HEADERS)
    with urllib.request.urlopen(req, timeout=20) as r:
        import json as _json
        return _json.load(r)


def probe_file(filename: str) -> str | None:
    """Return the canonical URL if the file exists on the wiki, else None."""
    title = "File:" + filename
    data = api_get({"action": "query", "titles": title, "prop": "imageinfo", "iiprop": "url"})
    pages = data.get("query", {}).get("pages", {})
    for page in pages.values():
        ii = page.get("imageinfo", [])
        if ii and ii[0].get("url"):
            return ii[0]["url"]
    return None


def search_image(ability: str) -> tuple[str, str] | None:
    """Search Fandom for an image whose name resembles the ability — catches typos
    like "ForceLighting.jpg" for "Force Lightning". Returns (filename, url)."""
    bare = re.sub(r"\s*\([^)]*\)\s*$", "", ability).strip().lower()
    needle = bare.replace(" ", "")
    if not needle:
        return None

    data = api_get({
        "action": "query",
        "list": "allimages",
        "aiprefix": needle[:6],
        "ailimit": "20",
    })
    images = data.get("query", {}).get("allimages", [])

    # Also look for images that *contain* the needle anywhere — pull more pages
    # from the prefix scan if needed, then text-match below.
    candidates = []
    for img in images:
        name = img.get("name", "")
        url = img.get("url", "")
        if not name or not url:
            continue
        ext = name.rsplit(".", 1)[-1].lower()
        if ext not in ("png", "jpg", "jpeg", "gif"):
            continue
        clean = name.rsplit(".", 1)[0].lower()
        score = 0
        if clean == needle:
            score = 100
        elif clean.startswith(needle):
            score = 80
        elif needle in clean:
            score = 60
        else:
            # Allow 1-character differences (typos like Lighting/Lightning).
            if abs(len(clean) - len(needle)) <= 2 and clean[:5] == needle[:5]:
                score = 40
        if score > 0:
            candidates.append((score, len(clean), name, url))

    if not candidates:
        return None

    # Pick the highest-scoring, then the closest in length to our target.
    candidates.sort(key=lambda c: (-c[0], abs(c[1] - len(needle))))
    return candidates[0][2], candidates[0][3]


def download_as_png(url: str, dest: Path) -> bool:
    try:
        req = urllib.request.Request(url, headers=HEADERS)
        with urllib.request.urlopen(req, timeout=20) as r:
            raw = r.read()
        try:
            from PIL import Image as PILImage
            with PILImage.open(BytesIO(raw)) as img:
                img.convert("RGBA").save(dest, format="PNG")
        except ImportError:
            dest.write_bytes(raw)
        return True
    except Exception as e:
        print(f"    download fail: {e}")
        return False


def main() -> int:
    mapping = json.loads(MAPPING_PATH.read_text(encoding="utf-8"))
    used = collect_used_abilities()
    unmapped = [(name, n) for name, n in used.most_common() if name not in mapping]
    print(f"unmapped abilities: {len(unmapped)}")

    ICONS_DIR.mkdir(parents=True, exist_ok=True)

    found = 0
    failed: list[str] = []
    for ability, _ in unmapped:
        url = None
        chosen_filename = None
        for variant in name_variants(ability):
            try:
                u = probe_file(variant)
            except Exception:
                u = None
                time.sleep(0.5)
            if u:
                url = u
                chosen_filename = variant
                break
            time.sleep(0.05)

        # Fallback: search by image-name prefix to catch typos / inconsistent names.
        if url is None:
            try:
                hit = search_image(ability)
            except Exception:
                hit = None
            if hit:
                chosen_filename, url = hit

        if url is None or chosen_filename is None:
            # Last-resort fallback: for "<DOT> (<Ability>)" reuse the parenthesized
            # ability's icon since DOTs visually inherit the parent's iconography.
            paren = re.search(r"\(([^)]+)\)", ability)
            if paren:
                source_ability = paren.group(1).strip()
                if source_ability in mapping:
                    mapping[ability] = mapping[source_ability]
                    found += 1
                    continue
            failed.append(ability)
            continue

        local = ICONS_DIR / chosen_filename.replace(" ", "_")
        if not local.exists() or local.stat().st_size == 0:
            ok = download_as_png(url, local)
            if not ok:
                failed.append(ability)
                continue
        mapping[ability] = local.name
        found += 1
        if found % 20 == 0:
            print(f"  matched {found}")

    MAPPING_PATH.write_text(
        json.dumps(mapping, indent=2, sort_keys=True, ensure_ascii=False),
        encoding="utf-8",
    )
    print(f"matched {found} new abilities, total mappings now {len(mapping)}")
    print(f"still unmapped: {len(failed)}")
    if failed[:20]:
        print("examples still missing:")
        for name in failed[:20]:
            print(f"  {name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
