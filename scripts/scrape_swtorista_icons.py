"""Scrape ability icon → name mappings from swtorista.com class basics guides.

Each guide lists abilities with their icons (hosted on swtorista's CDN) followed
by the ability name in a <strong> tag. We walk the HTML sequentially and pair
icons with the next ability name, skipping group labels like "X & Y".

Run:
    python scripts/scrape_swtorista_icons.py
"""

from __future__ import annotations

import json
import re
import time
import urllib.parse
import urllib.request
from io import BytesIO
from pathlib import Path

HEADERS = {"User-Agent": "Mozilla/5.0 (compatible; HolotrackerScraper/1.0)"}
ICONS_DIR = Path("data/images/abilities")
MAPPING_PATH = Path("data/abilities-icons.json")
SWTORISTA_INDEX = "https://swtorista.com/articles/"

# We probe each known guide URL. swtorista lists guides on /articles/ and
# the URLs follow predictable patterns like <class>-<advanced>-<spec>-basics-guide.
GUIDE_URLS = [
    "bounty-hunter-mercenary-arsenal-basics-guide",
    "bounty-hunter-mercenary-bodyguard-basics-guide",
    "bounty-hunter-mercenary-innovative-ordnance-basics-guide",
    "bounty-hunter-powertech-advanced-prototype-basics-guide",
    "bounty-hunter-powertech-pyrotech-basics-guide",
    "bounty-hunter-powertech-shield-tech-basics-guide",
    "combat-sentinel-jedi-knight-basics-guide",
    "concentration-sentinel-jedi-knight-basics-guide",
    "watchman-sentinel-jedi-knight-basics-guide",
    "defense-guardian-jedi-knight-basics-guide",
    "focus-guardian-jedi-knight-basics-guide",
    "vigilance-guardian-jedi-knight-basics-guide",
    "imperial-agent-operative-concealment-basics-guide",
    "imperial-agent-operative-lethality-basics-guide",
    "imperial-agent-operative-medicine-basics-guide",
    "imperial-agent-sniper-engineering-basics-guide",
    "imperial-agent-sniper-marksmanship-basics-guide",
    "imperial-agent-sniper-virulence-basics-guide",
    "jedi-consular-shadow-infiltration-basics-guide",
    "jedi-consular-shadow-kinetic-combat-basics-guide",
    "jedi-consular-shadow-serenity-basics-guide",
    "jedi-consular-sage-balance-basics-guide",
    "jedi-consular-sage-seer-basics-guide",
    "jedi-consular-sage-telekinetics-basics-guide",
    "sith-inquisitor-assassin-darkness-basics-guide",
    "sith-inquisitor-assassin-deception-basics-guide",
    "sith-inquisitor-assassin-hatred-basics-guide",
    "sith-inquisitor-sorcerer-corruption-basics-guide",
    "sith-inquisitor-sorcerer-lightning-basics-guide",
    "sith-inquisitor-sorcerer-madness-basics-guide",
    "sith-warrior-juggernaut-immortal-basics-guide",
    "sith-warrior-juggernaut-rage-basics-guide",
    "sith-warrior-juggernaut-vengeance-basics-guide",
    "sith-warrior-marauder-annihilation-basics-guide",
    "sith-warrior-marauder-carnage-basics-guide",
    "sith-warrior-marauder-fury-basics-guide",
    "smuggler-gunslinger-dirty-fighting-basics-guide",
    "smuggler-gunslinger-saboteur-basics-guide",
    "smuggler-gunslinger-sharpshooter-basics-guide",
    "smuggler-scoundrel-ruffian-basics-guide",
    "smuggler-scoundrel-sawbones-basics-guide",
    "smuggler-scoundrel-scrapper-basics-guide",
    "trooper-commando-assault-specialist-basics-guide",
    "trooper-commando-combat-medic-basics-guide",
    "trooper-commando-gunnery-basics-guide",
    "trooper-vanguard-plasmatech-basics-guide",
    "trooper-vanguard-shield-specialist-basics-guide",
    "trooper-vanguard-tactics-basics-guide",
]

GROUP_SPLIT_RE = re.compile(r"\s*[&,]\s*|\s+and\s+", re.IGNORECASE)


def fetch(url: str) -> str:
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=30) as r:
        return r.read().decode("utf-8", errors="ignore")


def normalize_label(label: str) -> str:
    label = re.sub(r"&amp;", "&", label)
    label = label.replace("’", "'").replace("‘", "'")
    return label.strip()


def looks_like_ability_name(name: str) -> bool:
    if not name or len(name) < 3 or len(name) > 50:
        return False
    if any(stop in name.lower() for stop in (
        "extra tips", "rotation", "table of contents", "highlight",
        "level ", "tip:", "open with", "use these", "your debuffs",
        "ability priority", "credits", "rewards", "set bonus",
        "tactical item", "implant", "your priority")):
        return False
    return True


def extract_pairs(html: str) -> list[tuple[str, str]]:
    # Collect tokens in order: ('img', filename) or ('strong', label)
    tokens: list[tuple[str, str]] = []
    for m in re.finditer(
        r'(?:data-src|src)="[^"]*?icons/([a-zA-Z0-9_-]+)\.png"|<strong>([^<]+)</strong>',
        html,
        re.DOTALL,
    ):
        if m.group(1):
            tokens.append(("img", m.group(1)))
        else:
            label = normalize_label(m.group(2))
            if looks_like_ability_name(label):
                tokens.append(("strong", label))

    # Walk: for each img, find the next strong; if intervening other imgs share
    # the same prefix, treat them as a group whose names come from "X & Y".
    pairs: list[tuple[str, str]] = []
    i = 0
    while i < len(tokens):
        kind, value = tokens[i]
        if kind != "img":
            i += 1
            continue
        # Collect consecutive imgs (group of icons), then the next strong holds the label.
        group_icons: list[str] = []
        while i < len(tokens) and tokens[i][0] == "img":
            if tokens[i][1] not in group_icons:
                group_icons.append(tokens[i][1])
            i += 1
        if i >= len(tokens) or tokens[i][0] != "strong":
            continue
        label = tokens[i][1]
        i += 1
        # Split "X & Y" labels into individual names.
        names = [s.strip() for s in GROUP_SPLIT_RE.split(label) if s.strip()]
        # If counts match, pair up. If only one name and one icon, still good.
        if len(names) == len(group_icons):
            for icon, name in zip(group_icons, names):
                pairs.append((icon, name))
        elif len(names) == 1 and len(group_icons) == 1:
            pairs.append((group_icons[0], names[0]))
        # Otherwise skip — ambiguous mapping.
    return pairs


def download_icon(filename: str, dest: Path) -> bool:
    url = f"https://swtorista.com/articles/wp-content/themes/blankslate/icons/{filename}.png"
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
        print(f"  download fail {filename}: {e}")
        return False


def main() -> int:
    ICONS_DIR.mkdir(parents=True, exist_ok=True)
    mapping = json.loads(MAPPING_PATH.read_text(encoding="utf-8")) if MAPPING_PATH.exists() else {}
    print(f"existing mappings: {len(mapping)}")

    all_pairs: dict[str, str] = {}
    for slug in GUIDE_URLS:
        url = SWTORISTA_INDEX + slug + "/"
        try:
            html = fetch(url)
        except Exception as e:
            print(f"  fetch fail {slug}: {e}")
            continue
        pairs = extract_pairs(html)
        for icon, name in pairs:
            # Don't overwrite if we already saw this name.
            if name not in all_pairs:
                all_pairs[name] = icon
        time.sleep(0.2)
    print(f"new ability names found in guides: {len(all_pairs)}")

    added = 0
    failed = 0
    for name, icon_filename in all_pairs.items():
        if name in mapping:
            continue
        local = ICONS_DIR / f"swtorista_{icon_filename}.png"
        if not local.exists() or local.stat().st_size == 0:
            ok = download_icon(icon_filename, local)
            if not ok:
                failed += 1
                continue
            time.sleep(0.05)
        mapping[name] = local.name
        added += 1

    MAPPING_PATH.write_text(
        json.dumps(mapping, indent=2, sort_keys=True, ensure_ascii=False),
        encoding="utf-8",
    )
    print(f"added {added} new mappings ({failed} download failures)")
    print(f"total mappings now: {len(mapping)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
