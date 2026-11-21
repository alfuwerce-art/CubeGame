"""D86 M0-02C: translate the decompiled WorldInfo_scatterObjectsInArea branch tree into C# (v2).

Parser v2 handles the decompiler's real shapes:
- multi-line `if (COND) {` / `else if (COND) {` / `else {` blocks with `}` on their own line,
- single-statement `if (COND) goto LAB_x;` (conditional early exit),
- vector boilerplate treated as transparent (its body is inlined),
- `param_4 = 0xNN` + push and `*cur = (void*)0xNN` become ids.Add.

    python -m tools.alpha_content.scatter_tree_translate2
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / ".scratch/CubeWorld-Reversal/cube/world/WorldInfo.cpp"
OUT = ROOT / "Assets/CubeGame/Content/Generated/AlphaScatterTree.g.cs"
FUNC = "WorldInfo_scatterObjectsInArea"
ADDR = "0x005f56c0"

IF_RE = re.compile(r"^if \((.*)\) \{$")
ELSEIF_RE = re.compile(r"^else if \((.*)\) \{$")
IFGOTO_RE = re.compile(r"^if \((.*)\) goto (LAB_\w+);$")
BOILER_TOKENS = ("cur", "begin", "end)", "reserve", "iVar3", "local_2c", "local_30", "local_3",
                 "local_4", "local_5", "local_6", "local_7", "ppvVar", "puVar", "std_vector")


def translate_condition(cond: str):
    c = cond.strip()
    for token in BOILER_TOKENS:
        if token in c:
            return None
    c = c.replace("local_80", "h").replace("local_84", "t")
    c = c.replace("(float)fVar5", "cell").replace("fVar5", "cell")
    c = c.replace("(float)fVar6", "spacing").replace("fVar6", "spacing")
    c = c.replace("(char)param_4 == '\\0'", "mode == 0")
    c = c.replace("param_4", "mode")
    return c


def translate(lines, start, end):
    out = []
    stack = []          # dict(kind, emitted, transparent)
    last_closed = None  # dict of the last closed meaningful frame
    pending_id = None
    indent = 0

    def pad(n):
        return "    " * n

    for i in range(start, end):
        s = lines[i].strip()
        if not s:
            continue

        m = IFGOTO_RE.match(s)
        if m:
            cond = translate_condition(m.group(1))
            if cond is not None:
                out.append(pad(indent) + "if (%s) goto End;" % cond)
            continue

        m = IF_RE.match(s)
        if m:
            cond = translate_condition(m.group(1))
            if cond is None:
                stack.append({"kind": "transparent", "emitted": False})
                continue
            out.append(pad(indent) + "if (%s)" % cond)
            out.append(pad(indent) + "{")
            indent += 1
            stack.append({"kind": "if", "emitted": True})
            last_closed = None
            continue

        m = ELSEIF_RE.match(s)
        if m:
            cond = translate_condition(m.group(1))
            if cond is None:
                stack.append({"kind": "transparent", "emitted": False})
                continue
            out.append(pad(indent) + "else if (%s)" % cond)
            out.append(pad(indent) + "{")
            indent += 1
            stack.append({"kind": "elseif", "emitted": True})
            last_closed = None
            continue

        if s == "else {":
            if last_closed is not None and not last_closed["emitted"]:
                stack.append({"kind": "transparent", "emitted": False})
                continue
            out.append(pad(indent) + "else")
            out.append(pad(indent) + "{")
            indent += 1
            stack.append({"kind": "else", "emitted": True})
            last_closed = None
            continue

        if s.startswith("}"):
            if not stack:
                continue
            frame = stack.pop()
            if frame["emitted"]:
                indent -= 1
                out.append(pad(indent) + "}")
                last_closed = frame
            else:
                last_closed = frame
            continue

        m = re.match(r"param_4 = (0x[0-9a-fA-F]+);", s)
        if m:
            pending_id = int(m.group(1), 16)
            continue
        m = re.match(r"\*cur = \(void \*\)(0x[0-9a-fA-F]+);", s)
        if m:
            out.append(pad(indent) + "ids.Add(0x%02x);" % int(m.group(1), 16))
            continue
        if s.startswith("std_vector_push_back_4byte(&param_4)") and pending_id is not None:
            out.append(pad(indent) + "ids.Add(0x%02x);" % pending_id)
            pending_id = None
            continue

    while stack:
        frame = stack.pop()
        if frame["emitted"]:
            indent -= 1
            out.append(pad(indent) + "}")
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args(argv)
    if not SRC.exists():
        print("SCATTER_TREE=FAIL source missing")
        return 1
    lines = SRC.read_text(encoding="utf-8", errors="replace").splitlines()
    start = next(i for i, l in enumerate(lines) if ("Global::" + FUNC) in l)
    end = next(i for i in range(start + 5, len(lines))
               if lines[i].startswith("/* Global::") or lines[i].startswith("void "))
    body = translate(lines, start, end)
    ids = sorted({int(m.group(1), 16) for line in body for m in [re.search(r"0x([0-9a-f]+)\)", line)] if m})
    header = [
        "// <auto-generated>",
        "// Mechanical translation of the decompiled " + FUNC + " @ " + ADDR + ".",
        "// Conditions keep the decompiler's comparisons; vector boilerplate is transparent.",
        "// ids=%d" % len(ids),
        "// </auto-generated>",
        "using System.Collections.Generic;",
        "namespace CubeGame.Content",
        "{",
        "    /// <summary>Full W-18 climate selection tree translated from the Alpha code (needs review).</summary>",
        "    public static class AlphaScatterTree",
        "    {",
        '        public const string Symbol = "%s @ %s";' % (FUNC, ADDR),
        '        public const string Status = "TRANSLATED_FROM_CODE: conditions/ids from the decompiled function; not yet verified against Alpha";',
        "",
        '        /// <param name="h">humidity (local_80)</param>',
        '        /// <param name="t">temperature (local_84)</param>',
        '        /// <param name="cell">region cell value (fVar5 after the grids)</param>',
        '        /// <param name="spacing">World_placeObjectWithSpacing (fVar6)</param>',
        '        /// <param name="level">level parameter</param>',
        '        /// <param name="mode">(char)param_4 flag</param>',
        "        public static int[] Select(double h, double t, double cell, double spacing, int level, int mode)",
        "        {",
        "            var ids = new List<int>();",
    ]
    footer = [
        "        End:",
        "            return ids.ToArray();",
        "        }",
        "    }",
        "}",
    ]
    text = "\n".join(header + body + footer) + "\n"
    if a.check:
        ok = OUT.exists() and OUT.read_text(encoding="utf-8") == text
        print(("SCATTER_TREE=PASS" if ok else "SCATTER_TREE=FAIL drift") + " ids=%d" % len(ids))
        return 0 if ok else 1
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print("SCATTER_TREE=PASS ids=%d lines=%d out=%s" % (len(ids), len(body), OUT))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
