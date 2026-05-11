"""Scan combat logs for abilities used by the player, and report which lack icons.

Usage:
    python scripts/find_missing_icons.py
"""

from __future__ import annotations

import json
import os
import re
from collections import Counter
from pathlib import Path

LOG_DIR = Path(os.path.expanduser("~/Documents/Star Wars - The Old Republic/CombatLogs"))
LINE_RE = re.compile(
    r"^\[(?P<time>\d+:\d+:\d+\.\d+)\]\s+"
    r"\[(?P<source>[^\]]*)\]\s+"
    r"\[(?P<target>[^\]]*)\]\s+"
    r"\[(?P<ability>[^\]]*)\]\s+"
    r"\[(?P<effect>[^\]]*)\]"
)


def main() -> int:
    mapping_path = Path("data/abilities-icons.json")
    mapping = json.loads(mapping_path.read_text(encoding="utf-8"))
    print(f"loaded {len(mapping)} known mappings")

    counter: Counter[str] = Counter()
    for log in LOG_DIR.glob("combat_*.txt"):
        try:
            text = log.read_text(encoding="latin-1", errors="ignore")
        except Exception as e:
            print(f"  cant read {log.name}: {e}")
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

    total = len(counter)
    missing = [(name, n) for name, n in counter.most_common() if name not in mapping]
    matched = total - len(missing)
    print(f"unique abilities used: {total}")
    print(f"  matched: {matched}")
    print(f"  missing: {len(missing)}")

    if missing:
        print("\nmost-used abilities lacking icons:")
        for name, n in missing[:50]:
            print(f"  {n:6}  {name}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
