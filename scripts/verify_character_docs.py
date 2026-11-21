"""Check the active character-production documentation of either repository."""

import argparse
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit

STAGE = "REPOSITORY_CONSOLIDATION_FINAL"
REVIEW = "READY_FOR_OWNER_REVIEW"
RESULT_CODES = {REVIEW, "IN_PROGRESS", "BLOCKED_ON_OWNER_DECISION"}
PHASE_FIELDS = ("Код этапа", "Результат ревизии")
DISPOSITION = "REVIEW_CLOSED / NOT_SELECTED"
OWNER_DECISION = "docs/decisions/2026-11-21-human-male-base-v1.md"
FACTORY_ACTIVE = (
    "README.md", "AGENTS.md", "docs/PRODUCTION_MEMORY.md",
    "docs/CHARACTER_PRODUCTION_WORKSPACE.md", "races/README.md",
    ".agents/skills/factoria-asset-review/SKILL.md",
)
FACTORY_ARCHIVE = (
    "01_DEEPSEEK_FACTORY_IMPLEMENTATION_TZ.md", "02_FACTORY_OPERATIONAL_DESIGN.md",
    "03_FACTORY_PROJECT_MEMORY.md", "CHAT_ROUTING.md", "docs/HUMAN_MALE_BASE_V1_REVIEW.md",
)
GAME_ACTIVE = (
    "README.md", "AGENTS.md", "PROJECT_MEMORY.md", "docs/ACTIVE_STAGE.md", "docs/GDD.md",
    "docs/ROADMAP.md", "docs/architecture/CHARACTER_ASSET_HANDOFF.md", OWNER_DECISION,
    "docs/design/DECISIONS.md", "docs/design/OPEN_DECISIONS.md",
    "docs/reports/REPOSITORY_CONSOLIDATION_FINAL.md",
)
GAME_ARCHIVE = (
    "docs/planning/ROADMAP_DRAFT.md", "docs/stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md",
    "docs/stages/M1_MODEL_ANIMATION_EVOLUTION_STAGE.md", "docs/reports/CHARACTER_WORKSPACE_REVIEW.md",
)
LINK = re.compile(r"\[[^\]\n]*\]\((?:<([^>\n]+)>|([^\s)]+))(?:\s+\"[^\"]*\")?\)")
FENCE = re.compile(r"(?ms)^ {0,3}(\x60{3,}|~{3,})[^\n]*\n.*?^ {0,3}\1[ \t]*$")
HEADING = re.compile(r"^#{1,6}\s+(.+?)\s*#*\s*$", re.MULTILINE)
ARCHIVE_MARKER = re.compile(r"(?m)^> (?:Архив\b|Историческая запись\b)")
ALPHA_MANDATE = re.compile(
    r"FIND ORIGINAL CODE\s*→|M0\s*=\s*\*\*полный перенос|"
    r"Alpha parity\s+(?:обязателен|блокирует|требуется)|"
    r"(?:текущий|активный) этап[\s:—]+\**M0\b|"
    r"^#{1,6}\s+(?:ACTIVE STAGE|АКТИВНЫЙ ЭТАП)[\s:—]+\**M0\b",
    re.IGNORECASE | re.MULTILINE,
)
OPEN_REVIEW = re.compile(
    r"визуальная при[её]мка открыта|владелец оценивает эталон|"
    r"ожидается решение владельца по\s+HUMAN_MALE_BASE_V1",
    re.IGNORECASE,
)
OLD_CANDIDATE_APPROVAL = re.compile(
    r"(?<![A-Z_])ACCEPTED(?![A-Z_])|READY_FOR_OWNER_VISUAL_REVIEW|"
    r"утвержд[её]нн\w* эталон|принят\w* эталон|"
    r"на базе принятого|обязательная основа", re.IGNORECASE,
)


def prose(text):
    return re.sub(r"(?m)^\s*>[^\n]*\n?", "", FENCE.sub("", text))


def slugs(text):
    anchors = set()
    for heading in HEADING.findall(FENCE.sub("", text)):
        base = re.sub(r"[^\w -]", "", heading.lower()).replace(" ", "-")
        anchor = base
        suffix = 0
        while anchor in anchors:
            suffix += 1
            anchor = f"{base}-{suffix}"
        anchors.add(anchor)
    return anchors


def markdown_links(root, name, text):
    path = root / name
    for match in LINK.finditer(prose(text)):
        href = match[1] or match[2]
        url = urlsplit(href)
        if url.scheme or url.netloc:
            continue
        target = (path.parent / unquote(url.path)).resolve() if url.path else path
        if target != root and root not in target.parents:
            yield f"{name}: link leaves repository: {href}"
        elif not target.exists():
            yield f"{name}: missing target: {href}"
        elif target.suffix == ".md" and url.fragment:
            if unquote(url.fragment) not in slugs(target.read_text(encoding="utf-8-sig")):
                yield f"{name}: missing heading: {href}"


def current_statements(name, text):
    current = prose(text)
    if ALPHA_MANDATE.search(current):
        yield f"{name}: active Alpha port mandate"
    if OPEN_REVIEW.search(current):
        yield f"{name}: the previous mannequin review is presented as open"
    for block in re.split(r"\n[ \t]*\n", current):
        if "HUMAN_MALE_BASE_V1" in block and OLD_CANDIDATE_APPROVAL.search(block):
            yield f"{name}: closed mannequin is presented as accepted or a required base"


def read_documents(root, active, archive):
    documents = {}
    errors = []
    for name in active + archive:
        path = root / name
        if not path.is_file():
            errors.append(f"Missing required document: {name}")
            continue
        text = path.read_text(encoding="utf-8-sig")
        documents[name] = text
        if name in archive:
            if not ARCHIVE_MARKER.search(text):
                errors.append(f"{name}: missing archive marker")
            continue
        if ARCHIVE_MARKER.search(text):
            errors.append(f"{name}: active document marked as archive")
        errors.extend(markdown_links(root, name, text))
        errors.extend(current_statements(name, text))
    return documents, errors


def required_values(documents, name, values):
    text = documents.get(name, "")
    current = prose(text)
    for value in values:
        if value not in current:
            yield f"{name}: missing required current value: {value}"


def phase_values(text):
    current = prose(text)
    values = []
    for field in PHASE_FIELDS:
        matches = re.findall(r"(?m)^\*\*" + field + r"\*\*\s+`([A-Z][A-Z0-9_]+)`[ \t]*$", current)
        values.append(matches[0] if len(matches) == 1 else None)
    return tuple(values)


def check_phases(entries):
    errors = []
    phases = set()
    for name, content in entries:
        phase, result = phase_values(content)
        if phase is None or result not in RESULT_CODES:
            errors.append(f"{name}: expected exactly one stage and a recognized review status")
            continue
        phases.add((phase, result))
        if phase == STAGE:
            for value in ("HUMAN_MALE_BASE_V1", DISPOSITION):
                if value not in prose(content):
                    errors.append(f"{name}: current audit is missing the closed mannequin decision")
    if len(phases) > 1:
        names = ", ".join(name for name, _ in entries)
        errors.append(f"Stage or review status conflict: {names}")
    return errors


def validate_game(root):
    documents, errors = read_documents(root, GAME_ACTIVE, GAME_ARCHIVE)
    stages = ("docs/ACTIVE_STAGE.md", "PROJECT_MEMORY.md")
    errors.extend(check_phases([(name, documents.get(name, "")) for name in stages]))
    errors.extend(required_values(documents, OWNER_DECISION, ("21.11.2026", DISPOSITION)))
    errors.extend(required_values(documents, "AGENTS.md", ("CubeGame владеет", "Factoria производит")))
    errors.extend(required_values(documents, "docs/GDD.md", ("D63", "D80", "D92", "D94", "UNDECIDED")))
    decisions = documents.get("docs/design/DECISIONS.md", "")
    for decision, reference in (
        ("D63", "D63-race-identity-and-factions.md"),
        ("D80", "D80-hidden-ancient-race.md"),
        ("D92 – first release", "D92-fildor-first-solo-release.md"),
        ("D93", "D93-development-pivot-own-game.md"),
        ("D94", "D94-g3-humanoid-modular-appearance.md"),
    ):
        if not any(reference in (match[1] or match[2]) for match in LINK.finditer(prose(decisions))):
            errors.append(f"docs/design/DECISIONS.md: decision link missing: {decision}")
    stages = re.findall(r"(?m)^\| G(\d+) —", documents.get("docs/ROADMAP.md", ""))
    if stages != [str(n) for n in range(16)]:
        errors.append("docs/ROADMAP.md: expected one ordered G0–G15 stage table")
    return errors


def validate_factoria(root):
    documents, errors = read_documents(root, FACTORY_ACTIVE, FACTORY_ARCHIVE)
    errors.extend(required_values(documents, "AGENTS.md", ("CubeGame владеет", "Factoria производит")))
    errors.extend(check_phases([("docs/PRODUCTION_MEMORY.md", documents.get("docs/PRODUCTION_MEMORY.md", ""))]))
    historical = documents.get("docs/HUMAN_MALE_BASE_V1_REVIEW.md", "")
    if not any(DISPOSITION in line for line in historical.splitlines()[:9]):
        errors.append("docs/HUMAN_MALE_BASE_V1_REVIEW.md: missing final owner disposition near the archive heading")
    memory = documents.get("docs/PRODUCTION_MEMORY.md", "")
    if not any("CubeGame/" in (match[1] or match[2]) and "CHARACTER_ASSET_HANDOFF.md" in (match[1] or match[2])
               for match in LINK.finditer(prose(memory))):
        errors.append("docs/PRODUCTION_MEMORY.md: missing link to the CubeGame handoff contract")
    return errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--factoria", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--cubegame", type=Path)
    parser.add_argument("--cubegame-only", action="store_true")
    args = parser.parse_args()
    if args.cubegame_only and not args.cubegame:
        parser.error("--cubegame-only requires --cubegame")

    errors = [] if args.cubegame_only else validate_factoria(args.factoria.resolve())
    if args.cubegame:
        errors.extend(validate_game(args.cubegame.resolve()))
    if args.cubegame and not args.cubegame_only and not errors:
        paths = (args.cubegame / "docs/ACTIVE_STAGE.md", args.factoria / "docs/PRODUCTION_MEMORY.md")
        errors.extend(check_phases([(str(path), path.read_text(encoding="utf-8-sig")) for path in paths]))
    if errors:
        print("\n".join(errors))
        return 1
    count = 0 if args.cubegame_only else len(FACTORY_ACTIVE) + len(FACTORY_ARCHIVE)
    if args.cubegame:
        count += len(GAME_ACTIVE) + len(GAME_ARCHIVE)
    print(f"Checked {count} required documents. Scope: local links, decisions, stage and status rules.")
    print("Design interpretation and external source approval still require manual review.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
