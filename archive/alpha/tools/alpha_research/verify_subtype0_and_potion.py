#!/usr/bin/env python3
"""Fail-closed replay of the original 43-check local DeepSeek evidence script.

This tracked entrypoint is NOT a replacement for original disassembly-based tests.
Without the real original verifier in .scratch it reports BLOCKED, never PASS.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import re
import subprocess
import sys


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--source",
        type=Path,
        default=root / ".scratch" / "research" / "m0_00b_final" / "verify_two_questions.py",
        help="Original local 43-check script (must be supplied from DeepSeek's research).",
    )
    source = parser.parse_args().source.resolve()
    if not source.is_file():
        print("VERIFY_TWO_QUESTIONS_REPLAY=BLOCKED original_script_missing=" + str(source))
        return 2
    try:
        completed = subprocess.run(
            [sys.executable, str(source)],
            cwd=str(root),
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            timeout=300,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        print("VERIFY_TWO_QUESTIONS_REPLAY=BLOCKED cause=" + str(exc))
        return 2
    print(completed.stdout, end="" if completed.stdout.endswith("\n") else "\n")
    valid = (
        completed.returncode == 0
        and re.search(r"VERIFY_TWO_QUESTIONS_RESULT=PASS\s+checks=43\s+failed=0", completed.stdout)
        and "SUBTYPE_0=VALID_WEAPON" in completed.stdout
        and "AI_POTION_DAMAGE_SOURCE=SELF" in completed.stdout
        and "AI_POTION_VALUE=SELF_MAX_HP" in completed.stdout
    )
    print("VERIFY_TWO_QUESTIONS_REPLAY=" + ("PASS" if valid else "FAIL"))
    return 0 if valid else 1


if __name__ == "__main__":
    raise SystemExit(main())
