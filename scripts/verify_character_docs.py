"""Validate local documentation links for the Factoria/CubeGame handoff."""
import argparse
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit

FACTORIA = [
    "README.md", "AGENTS.md", "docs/CHARACTER_PRODUCTION_WORKSPACE.md",
    "docs/HUMAN_MALE_BASE_V1_REVIEW.md",
]
CUBEGAME = [
    "README.md", "AGENTS.md", "PROJECT_MEMORY.md", "docs/GDD.md",
    "docs/ROADMAP.md", "docs/ACTIVE_STAGE.md", "docs/architecture/CHARACTER_ASSET_HANDOFF.md",
    "docs/design/DECISIONS.md", "docs/reports/CHARACTER_WORKSPACE_REVIEW.md",
]
LINK = re.compile(r"\[[^\]\n]*\]\((?:<([^>\n]+)>|([^\s)]+))(?:\s+\"[^\"]*\")?\)")


def validate(root, active_paths):
    root = root.resolve()
    for name in active_paths:
        path = root / name
        if not path.is_file():
            yield f"Missing required document: {path}"
            continue
        contents = path.read_text(encoding="utf-8-sig")
        contents = re.sub(r"(?ms)^(\x60{3,}|~{3,})[^\n]*\n.*?^\1[ \t]*$", "", contents)
        for match in LINK.finditer(contents):
            href = match[1] or match[2]
            url = urlsplit(href)
            if url.scheme or url.netloc or not url.path:
                continue
            target = (path.parent / unquote(url.path)).resolve()
            if target != root and root not in target.parents:
                yield f"{name}: link leaves repository: {href}"
            elif not target.exists():
                yield f"{name}: missing target: {href}"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--factoria", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--cubegame", type=Path)
    args = parser.parse_args()
    errors = list(validate(args.factoria, FACTORIA))
    if args.cubegame:
        errors.extend(validate(args.cubegame, CUBEGAME))
    if errors:
        print("\n".join(errors))
        return 1
    print(f"Validated {len(FACTORIA) + (len(CUBEGAME) if args.cubegame else 0)} active documents.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
