"""D86 M0-02C: extract the climate -> object scatter table from the decompiled world code.

M0-00 recorded W-18 ("what grows where", ~660 lines) as UNKNOWN because the table had not been
extracted. The decompiled ``WorldInfo_scatterObjectsInArea`` @ 0x005f56c0 (local Ghidra output,
read-only) does contain it: it samples the humidity/temperature grids and pushes object-kind ids into
a vector per branch. This script turns the branch structure into committed evidence data
(condition text + id groups). Conditions are the decompiler's own text; the runtime selector is a
separate, later step (the function also uses goto-based branch selection).

    python -m tools.alpha_content.extract_scatter_table
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / ".scratch/CubeWorld-Reversal/cube/world/WorldInfo.cpp"
OUT_JSON = ROOT / "tools/alpha_content/data/scatter_evidence.json"
OUT_CS = ROOT / "Assets/CubeGame/Content/Generated/AlphaScatterEvidence.g.cs"
FUNC = "WorldInfo_scatterObjectsInArea"
ADDR = "0x005f56c0"
CLIMATE_MARKERS = ("local_80", "local_84", "fVar6", "level")


def cs(value: str) -> str:
    return '"' + str(value).replace("\\", "\\\\").replace('"', '\\"') + '"'


def condition_text(line: str) -> str:
    s = line.strip()
    if s.startswith("else if ("):
        body = s[len("else if ("):]
    elif s.startswith("if ("):
        body = s[len("if ("):]
    else:
        return "else"
    body = body.rstrip()
    if body.endswith("{"):
        body = body[:-1].rstrip()
    if body.endswith(")"):
        body = body[:-1].rstrip()
    return body


def parse_function(lines: list[str], start: int, end: int) -> list[dict]:
    stack: list[tuple[int, str]] = []
    groups: dict[tuple, dict] = {}
    order: list[tuple] = []
    pending_assign: int | None = None

    def record(kind: int):
        key = tuple(stack)
        g = groups.get(key)
        if g is None:
            g = {"conditions": [c[1] for c in stack], "gotos": [], "ids": []}
            groups[key] = g
            order.append(key)
        if not g["ids"] or g["ids"][-1] != kind:
            g["ids"].append(kind)

    for i in range(start, end):
        raw = lines[i]
        s = raw.strip()
        if not s:
            continue
        indent = len(raw) - len(raw.lstrip(" "))
        if s.startswith("if (") or s.startswith("else if ("):
            while stack and stack[-1][0] >= indent:
                stack.pop()
            text = condition_text(s)
            if any(m in text for m in CLIMATE_MARKERS):
                stack.append((indent, text))
            continue
        if s == "else {" or s.startswith("else {"):
            while stack and stack[-1][0] >= indent:
                stack.pop()
            continue
        if s.startswith("}"):
            while stack and stack[-1][0] >= indent:
                stack.pop()
            continue
        if "goto LAB_" in s:
            # Goto-based branch selection: keep it as a marker of the branch, do not invent semantics.
            if stack:
                key = tuple(stack)
                g = groups.get(key)
                if g is None:
                    g = {"conditions": [c[1] for c in stack], "gotos": [], "ids": []}
                    groups[key] = g
                    order.append(key)
                g["gotos"].append(s.rstrip(";"))
            continue
        m = re.match(r"param_4 = (0x[0-9a-fA-F]+);", s)
        if m:
            pending_assign = int(m.group(1), 16)
            continue
        if s.startswith("std_vector_push_back_4byte(&param_4)") and pending_assign is not None:
            if stack:
                record(pending_assign)
            pending_assign = None
            continue
        m = re.match(r"\*cur = \(void \*\)(0x[0-9a-fA-F]+);", s)
        if m and stack:
            record(int(m.group(1), 16))
    result = []
    for key in order:
        g = groups[key]
        if g["ids"] or g["gotos"]:
            result.append(g)
    return result


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args(argv)
    if not SRC.exists():
        print("SCATTER_EVIDENCE=FAIL source missing " + str(SRC))
        return 1
    lines = SRC.read_text(encoding="utf-8", errors="replace").splitlines()
    start = next(i for i, l in enumerate(lines) if f"Global::{FUNC}" in l)
    end = next(i for i in range(start + 5, len(lines)) if lines[i].startswith("/* Global::") or lines[i].startswith("void "))
    groups = parse_function(lines, start, end)
    payload = {
        "format": "cubegame.alpha_scatter_evidence/1",
        "status": "PARTIAL: branch conditions/ids and goto markers extracted from the decompiled function",
        "source": {
            "file": ".scratch/CubeWorld-Reversal/cube/world/WorldInfo.cpp",
            "symbol": f"{FUNC} @ {ADDR}",
            "sha256": hashlib.sha256(SRC.read_bytes()).hexdigest(),
            "extractor": "tools/alpha_content/extract_scatter_table.py",
        },
        "note": "Object ids are Alpha creature/object kinds pushed into the scatter vector; condition "
                "text is the decompiler's condition verbatim. Runtime evaluation is NOT implemented.",
        "groups": groups,
    }
    text = json.dumps(payload, indent=1, ensure_ascii=True) + "\n"
    if a.check:
        ok = OUT_JSON.exists() and OUT_JSON.read_text(encoding="utf-8") == text
        print(("SCATTER_EVIDENCE=PASS" if ok else "SCATTER_EVIDENCE=FAIL drift") + f" groups={len(groups)}")
        return 0 if ok else 1
    OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUT_JSON.write_text(text, encoding="utf-8", newline="\n")

    cs_lines = ["// <auto-generated>",
                "// Generated by tools/alpha_content/extract_scatter_table.py from the decompiled",
                f"// {FUNC} @ {ADDR}. Conditions are the decompiler's text; runtime evaluation is not implemented.",
                f"// groups={len(groups)}", "// </auto-generated>", "namespace CubeGame.Content", "{",
                "    /// <summary>One branch of the Alpha climate->object scatter table (W-18).</summary>",
                "    public readonly struct AlphaScatterGroupDef",
                "    {",
                "        public readonly string[] Conditions; public readonly string[] Gotos; public readonly int[] ObjectKinds;",
                "        public AlphaScatterGroupDef(string[] conditions, string[] gotos, int[] kinds)",
                "        { Conditions = conditions; Gotos = gotos; ObjectKinds = kinds; }",
                "    }",
                "",
                "    public static class AlphaScatterEvidence",
                "    {",
                '        public const string Format = "cubegame.alpha_scatter_evidence/1";',
                '        public const string Symbol = "' + FUNC + ' @ ' + ADDR + '";',
                '        public const string Status = "PARTIAL: conditions/ids extracted; runtime evaluation not implemented";',
                "",
                "        public static readonly AlphaScatterGroupDef[] Groups =",
                "        {",
                ]
    for g in groups:
        conds = ", ".join(cs(c) for c in g["conditions"])
        gotos = ", ".join(cs(x) for x in g["gotos"])
        ids = ", ".join(str(i) for i in g["ids"])
        cond_arr = "new string[0]" if not g["conditions"] else "new[] { " + conds + " }"
        goto_arr = "new string[0]" if not g["gotos"] else "new[] { " + gotos + " }"
        id_arr = "new int[0]" if not g["ids"] else "new[] { " + ids + " }"
        cs_lines.append(f"            new AlphaScatterGroupDef({cond_arr}, {goto_arr}, {id_arr}),")
    cs_lines += ["        };", "    }", "}"]
    OUT_CS.parent.mkdir(parents=True, exist_ok=True)
    OUT_CS.write_text("\n".join(cs_lines) + "\n", encoding="utf-8", newline="\n")
    ids = sorted({i for g in groups for i in g["ids"]})
    print(f"SCATTER_EVIDENCE=PASS groups={len(groups)} distinctKinds={len(ids)} out={OUT_CS}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
