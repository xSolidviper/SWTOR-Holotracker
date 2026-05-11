"""Scrape SWTOR ability icons from Fandom wiki and build name -> filename map.

Run from the project root:
    python scripts/scrape_ability_icons.py

Writes:
    data/images/abilities/*.png         downloaded icons
    data/abilities-icons.json           {"Vital Shot": "Vitalshot.png", ...}
"""

from __future__ import annotations

import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

API = "https://swtor.fandom.com/api.php"
HEADERS = {
    "User-Agent": "SwtorHolotracker/1.0 (ability icon cache build script)"
}
PAUSE = 0.1


def api_get(params: dict) -> dict:
    query = urllib.parse.urlencode({**params, "format": "json"})
    req = urllib.request.Request(f"{API}?{query}", headers=HEADERS)
    with urllib.request.urlopen(req, timeout=20) as r:
        return json.load(r)


def get_category_pages(category: str) -> list[str]:
    titles: list[str] = []
    cont: str | None = None
    while True:
        params = {
            "action": "query",
            "list": "categorymembers",
            "cmtitle": category,
            "cmlimit": "500",
            "cmtype": "page",
        }
        if cont:
            params["cmcontinue"] = cont
        data = api_get(params)
        members = data.get("query", {}).get("categorymembers", [])
        titles.extend(m["title"] for m in members)
        cont = data.get("continue", {}).get("cmcontinue")
        if not cont:
            break
        time.sleep(PAUSE)
    return titles


def get_category_subcategories(category: str) -> list[str]:
    cats: list[str] = []
    cont: str | None = None
    while True:
        params = {
            "action": "query",
            "list": "categorymembers",
            "cmtitle": category,
            "cmlimit": "500",
            "cmtype": "subcat",
        }
        if cont:
            params["cmcontinue"] = cont
        data = api_get(params)
        members = data.get("query", {}).get("categorymembers", [])
        cats.extend(m["title"] for m in members)
        cont = data.get("continue", {}).get("cmcontinue")
        if not cont:
            break
        time.sleep(PAUSE)
    return cats


def get_ability_titles() -> list[str]:
    seen: set[str] = set()
    # Pages directly under Category:Abilities
    seen.update(get_category_pages("Category:Abilities"))
    # Pages under each class/role subcategory
    for sub in get_category_subcategories("Category:Abilities"):
        seen.update(get_category_pages(sub))
    return sorted(seen)


def get_images_for_titles(titles: list[str]) -> dict[str, list[str]]:
    # MediaWiki applies imlimit globally across the whole batch, not per page,
    # so 50 titles * imlimit=20 returns only ~20 images TOTAL. Use imlimit=max
    # and a smaller batch so each page gets a fair share.
    result: dict[str, list[str]] = {}
    BATCH = 20
    for i in range(0, len(titles), BATCH):
        batch = titles[i:i + BATCH]
        params = {
            "action": "query",
            "titles": "|".join(batch),
            "prop": "images",
            "imlimit": "500",
        }
        data = api_get(params)
        pages = data.get("query", {}).get("pages", {})
        for page in pages.values():
            title = page.get("title")
            if not title:
                continue
            images = [img["title"] for img in page.get("images", [])]
            existing = result.setdefault(title, [])
            for img in images:
                if img not in existing:
                    existing.append(img)

        # Some images can spill into continuation — follow once if present.
        cont = data.get("continue", {}).get("imcontinue")
        while cont:
            params["imcontinue"] = cont
            data = api_get(params)
            pages = data.get("query", {}).get("pages", {})
            for page in pages.values():
                title = page.get("title")
                if not title:
                    continue
                images = [img["title"] for img in page.get("images", [])]
                existing = result.setdefault(title, [])
                for img in images:
                    if img not in existing:
                        existing.append(img)
            cont = data.get("continue", {}).get("imcontinue")
            time.sleep(PAUSE)

        time.sleep(PAUSE)
    return result


def resolve_image_urls(filenames: set[str]) -> dict[str, str]:
    result: dict[str, str] = {}
    files = sorted(filenames)
    for i in range(0, len(files), 50):
        batch = files[i:i + 50]
        params = {
            "action": "query",
            "titles": "|".join(batch),
            "prop": "imageinfo",
            "iiprop": "url",
        }
        data = api_get(params)
        pages = data.get("query", {}).get("pages", {})
        for page in pages.values():
            title = page.get("title")
            ii = page.get("imageinfo", [])
            if title and ii:
                url = ii[0].get("url")
                if url:
                    result[title] = url
        time.sleep(PAUSE)
    return result


def safe_filename(title: str) -> str:
    name = title.removeprefix("File:").replace(" ", "_")
    return name


def download_icons(url_map: dict[str, str], target_dir: Path) -> dict[str, str]:
    target_dir.mkdir(parents=True, exist_ok=True)
    # Fandom serves icons through their CDN which may convert PNGs to WebP under a
    # .png filename. GDI+ in WinForms can't decode WebP, so we re-encode every
    # download as a real PNG before saving.
    try:
        from PIL import Image as PILImage
        from io import BytesIO
        have_pil = True
    except ImportError:
        have_pil = False
        print("  (Pillow not available — saving raw bytes; run convert_icons_to_png.py afterward)")

    downloaded: dict[str, str] = {}
    for idx, (title, url) in enumerate(url_map.items(), start=1):
        fname = safe_filename(title)
        local = target_dir / fname
        if local.exists() and local.stat().st_size > 0:
            downloaded[title] = fname
            continue
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=20) as r:
                raw = r.read()

            if have_pil:
                with PILImage.open(BytesIO(raw)) as img:
                    img.convert("RGBA").save(local, format="PNG")
            else:
                local.write_bytes(raw)

            downloaded[title] = fname
            if idx % 25 == 0:
                print(f"  downloaded {idx}/{len(url_map)}")
        except urllib.error.HTTPError as e:
            print(f"  HTTP {e.code} for {title}")
        except Exception as e:
            print(f"  fail {title}: {e}")
        time.sleep(0.04)
    return downloaded


def main() -> int:
    target_dir = Path("data/images/abilities")
    titles = get_ability_titles()
    print(f"got {len(titles)} ability page titles")

    images_map = get_images_for_titles(titles)
    print(f"got images for {len(images_map)} pages")

    # For each ability, the primary icon is the first non-badge image.
    skip_substrings = ("jedipedia", "wiki", "logo", "icon_logo", "swtor_logo",
                       "republic", "empire", "force_user", "tech_user")
    ability_to_icon: dict[str, str] = {}
    needed_icons: set[str] = set()
    for ability, images in images_map.items():
        for img in images:
            lower = img.lower()
            if any(s in lower for s in skip_substrings):
                continue
            ability_to_icon[ability] = img
            needed_icons.add(img)
            break

    print(f"mapped {len(ability_to_icon)} abilities to {len(needed_icons)} unique icon files")

    url_map = resolve_image_urls(needed_icons)
    print(f"resolved {len(url_map)} icon URLs")

    downloaded = download_icons(url_map, target_dir)
    print(f"downloaded/cached {len(downloaded)} icon files")

    mapping = {}
    for ability, icon_title in sorted(ability_to_icon.items()):
        local_name = downloaded.get(icon_title)
        if local_name:
            mapping[ability] = local_name

    # Combat logs use the bare ability name with no class disambiguator. For each
    # "Foo (ClassName)" mapping, also seed an alias "Foo" that points to the same
    # icon (the first variant we processed). This way "Vital Shot" hits even if the
    # wiki only has "Vital Shot (Smuggler)".
    alias_added = 0
    import re
    for ability, file_name in list(mapping.items()):
        m = re.match(r"^(.*?)\s*\(.+\)$", ability)
        if m:
            base = m.group(1).strip()
            if base and base not in mapping:
                mapping[base] = file_name
                alias_added += 1

    print(f"added {alias_added} bare-name aliases")

    out_path = Path("data/abilities-icons.json")
    out_path.write_text(
        json.dumps(mapping, indent=2, sort_keys=True, ensure_ascii=False),
        encoding="utf-8",
    )
    print(f"wrote {len(mapping)} mappings to {out_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
