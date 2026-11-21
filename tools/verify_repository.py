"""Validate active documentation and the Factoria import without dependencies."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
from urllib.parse import unquote, urlsplit

ACTIVE = (
    "AGENTS.md", "PROJECT_MEMORY.md", "README.md", "docs/GDD.md",
    "docs/ROADMAP.md", "docs/ACTIVE_STAGE.md", "docs/ENGINEERING.md",
    "docs/planning/README.md", "docs/reports/REPOSITORY_CONSOLIDATION.md",
    "docs/reports/FACTORIA_MIGRATION.md", "tools/character-assets/README.md",
    "docs/engineering/EXPERIENCE.md", "docs/design/DECISIONS.md",
)
RETIRED = (
    "docs/stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md",
    "docs/planning/M0_ALPHA_PARITY_BACKLOG.md",
    "docs/planning/ROADMAP_DRAFT.md",
    "02_FACTORY_OPERATIONAL_DESIGN.md", "CHAT_ROUTING.md",
)
MEMORY = ["Current State", "Last Completed Work", "Current Task", "Next Action", "Blockers", "Handoff"]
LINK = re.compile(r"\[[^\]\n]*\]\((?:<([^>\n]+)>|([^\s)]+))(?:\s+\"[^\"]*\")?\)")
HEADING = re.compile(r"^#{1,6} (.+?)\s*#*\s*$", re.MULTILINE)
OLD_RULE = re.compile(
    r"FIND ORIGINAL CODE\s*→|M0\s*=\s*\*\*полный перенос|"
    r"Alpha parity.*(?:обязателен|блокирует)|"
    r"(?:canonical branch|каноническая ветка|интеграционная ветка)"
    r"\s*[:—]\s*\x60(?!integration/m0-all\x60)[^\x60\n]+\x60", re.IGNORECASE
)


def markdown(path):
    text = path.read_text(encoding="utf-8-sig")
    return re.sub(r"(?ms)^(\x60{3,}|~{3,})[^\n]*\n.*?^\1[ \t]*$", "", text)


def heading_ids(path):
    seen = set()
    for heading in HEADING.findall(markdown(path)):
        base = re.sub(r"[^\w -]", "", heading.lower()).replace(" ", "-")
        anchor = base
        count = 0
        while anchor in seen:
            count += 1
            anchor = f"{base}-{count}"
        seen.add(anchor)
    return seen


def links(root, path):
    for match in LINK.finditer(markdown(path)):
        href = match[1] or match[2]
        parsed = urlsplit(href)
        if parsed.scheme or parsed.netloc:
            continue
        target = (path.parent / unquote(parsed.path)).resolve() if parsed.path else path
        if target != root and root not in target.parents:
            yield f"{path.relative_to(root)}: link leaves checkout: {href}"
        elif not target.exists():
            yield f"{path.relative_to(root)}: missing target: {href}"
        elif target.suffix == ".md" and parsed.fragment and unquote(parsed.fragment) not in heading_ids(target):
            yield f"{path.relative_to(root)}: missing heading: {href}"


def verify_glb(data):
    if len(data) < 20 or struct.unpack_from("<4sII", data) != (b"glTF", 2, len(data)):
        raise ValueError("invalid GLB 2 header or length")
    offset = 12
    model = None
    while offset < len(data):
        length, chunk = struct.unpack_from("<I4s", data, offset)
        start = offset + 8
        offset = start + length
        if length % 4 or offset > len(data):
            raise ValueError("invalid GLB chunk length")
        if model is None:
            if chunk != b"JSON":
                raise ValueError("GLB does not start with JSON")
            model = json.loads(data[start:offset])
    if model.get("asset", {}).get("version") != "2.0" or not model.get("meshes"):
        raise ValueError("missing glTF 2.0 metadata or meshes")
    if any("uri" in item for name in ("buffers", "images") for item in model.get(name, [])):
        raise ValueError("GLB has an external dependency")


def verify_source(root):
    package = root / "tools/character-assets"
    manifest = json.loads((package / "provenance.json").read_text(encoding="utf-8"))
    print(f"Factoria: {len(manifest['sha256'])} manifest entries")
    for name, digest in manifest["sha256"].items():
        path = (package / name).resolve()
        try:
            if package not in path.parents:
                raise ValueError("manifest path leaves character-assets")
            data = path.read_bytes()
            if hashlib.sha256(data).hexdigest() != digest:
                raise ValueError("SHA-256 differs from the Factoria source")
            if path.suffix == ".glb":
                verify_glb(data)
            elif path.suffix == ".json":
                json.loads(data.decode("utf-8-sig"))
        except (OSError, ValueError, struct.error) as error:
            yield f"{name}: {error}"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--docs-only", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    errors = [f"missing document: {name}" for name in ACTIVE if not (root / name).is_file()]
    errors.extend(f"retired active path: {name}" for name in RETIRED if (root / name).exists())

    skills = sorted(root.glob(".agents/skills/cubegame-*/SKILL.md"))
    active = [root / name for name in ACTIVE] + skills
    for path in active:
        if not path.is_file():
            continue
        errors.extend(links(root, path))
        if path.name in ("AGENTS.md", "ACTIVE_STAGE.md", "PROJECT_MEMORY.md", "ROADMAP.md"):
            if OLD_RULE.search(markdown(path)):
                errors.append(f"{path.relative_to(root)}: conflicting workflow rule")
    if (root / "PROJECT_MEMORY.md").is_file():
        memory = markdown(root / "PROJECT_MEMORY.md")
        if re.findall(r"^## (.*)$", memory, re.MULTILINE) != MEMORY:
            errors.append("PROJECT_MEMORY.md: unexpected handoff headings")
    if len(skills) != 4:
        errors.append(f"expected four CubeGame skill entry points, found {len(skills)}")

    if not args.docs_only:
        try:
            errors.extend(verify_source(root))
        except (OSError, KeyError, ValueError) as error:
            errors.append(f"Factoria manifest: {error}")
        if subprocess.run(["git", "diff", "--check", "HEAD"], cwd=root, check=False).returncode:
            errors.append("git diff --check HEAD failed")
    if errors:
        print("\n".join(errors))
        return 1
    print(f"Repository checks passed ({len(active)} active documents).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
