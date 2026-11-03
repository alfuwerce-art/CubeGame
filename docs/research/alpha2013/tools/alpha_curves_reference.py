#!/usr/bin/env python3
"""CubeGame S00-R02 — эталонные вычисления кривых Cube World Alpha 2013.

Назначение: сделать цитируемые в спецификациях числа **воспроизводимыми**.
Реализация — собственная (не декомпилированный код): только математические
формы, восстановленные из Alpha (см. docs/research/alpha2013/BEHAVIOR_SPECS/E_combat.md).
Запуск:  python docs/research/alpha2013/tools/alpha_curves_reference.py --check
"""

from __future__ import annotations

import sys


def curve_level05(n: float) -> float:
    """1 - 1/((n-1)*0.05+1) — базовая насыщающаяся кривая уровня (Alpha 0043ca60)."""
    return 1.0 - 1.0 / ((n - 1.0) * 0.05 + 1.0)


def curve_diminishing_n1(n: float) -> float:
    """1 - 1/((n+1)*0.1+1) (Alpha 0043e660)."""
    return 1.0 - 1.0 / ((n + 1.0) * 0.1 + 1.0)


def curve_diminishing_x3p1(n: float) -> float:
    """(1 - 1/(n*0.1+1))*3 + 1, 0 при n<1 (Alpha 0043e2c0)."""
    if n < 1:
        return 0.0
    return (1.0 - 1.0 / (n * 0.1 + 1.0)) * 3.0 + 1.0


def curve_diminishing_x3p15(n: float) -> float:
    """(1 - 1/(n*0.1+1))*3 + 1.5, 0 при n<1 (Alpha 0043f770)."""
    if n < 1:
        return 0.0
    return (1.0 - 1.0 / (n * 0.1 + 1.0)) * 3.0 + 1.5


def xp_next_level_cost(level: int) -> int:
    """Стоимость следующего уровня: int((1-1/((L-1)*0.05+1))*1000+50) (Alpha 00444d60)."""
    return int(curve_level05(float(level)) * 1000.0 + 50.0)


def level_factor(level: int) -> float:
    """2^(curve_level05(L)*3) — множитель уровня в уроне/броне (Alpha 00444db0/0043cff0)."""
    return 2.0 ** (curve_level05(float(level)) * 3.0)


def star_factor(stars: int) -> float:
    """2^(S*0.25) — множитель звёздности (Alpha 00444db0)."""
    return 2.0 ** (float(stars) * 0.25)


def form_haste(stars: int) -> float:
    """0.75+0.0625*S — базовая скорость в специальной форме (Alpha 00447700)."""
    return 0.75 + 0.0625 * float(stars)


def power_factor(rank: int) -> float:
    """1 - 1/(rank*0.1+1) — «power factor»/мощность ранга (Alpha 0043ed60)."""
    return 1.0 - 1.0 / (float(rank) * 0.1 + 1.0)


def kill_xp(level: int, stars: int) -> float:
    """max(1, curve_level05(level+stars)*20) — опыт за убийство (Alpha 00595a60, PARTIAL)."""
    return max(1.0, curve_level05(float(level + stars)) * 20.0)


# --- Ожидаемые значения, процитированные в спецификациях -------------------
EXPECT_XP = {1: 50, 2: 97, 5: 216, 10: 360, 20: 537, 50: 760, 100: 881}
EXPECT_LEVEL_FACTOR_4DP = {1: 1.0000, 5: 1.4142, 11: 2.0000, 21: 2.8284, 51: 4.4160, 101: 5.6574}
EXPECT_STAR_3DP = {0: 1.000, 1: 1.189, 2: 1.414, 3: 1.682, 4: 2.000, 5: 2.378, 6: 2.828, 7: 3.364, 8: 4.000}
EXPECT_HASTE_3DP = {0: 0.750, 1: 0.812, 2: 0.875, 3: 0.938, 4: 1.000, 5: 1.062, 6: 1.125, 7: 1.188, 8: 1.250}


def _close(a: float, b: float, eps: float = 5e-4) -> bool:
    return abs(a - b) <= eps


def check() -> int:
    failures: list[str] = []

    for level, expected in EXPECT_XP.items():
        got = xp_next_level_cost(level)
        if got != expected:
            failures.append(f"xp_next_level_cost({level}) = {got}, ожидалось {expected}")

    for level, expected in EXPECT_LEVEL_FACTOR_4DP.items():
        got = round(level_factor(level), 4)
        if not _close(got, expected):
            failures.append(f"level_factor({level}) = {got}, ожидалось {expected}")

    for stars, expected in EXPECT_STAR_3DP.items():
        got = round(star_factor(stars), 3)
        if not _close(got, expected):
            failures.append(f"star_factor({stars}) = {got}, ожидалось {expected}")

    for stars, expected in EXPECT_HASTE_3DP.items():
        got = round(form_haste(stars), 3)
        if not _close(got, expected):
            failures.append(f"form_haste({stars}) = {got}, ожидалось {expected}")

    # Монотонность и пределы — свойства, на которые опирается модель.
    if not all(xp_next_level_cost(l) <= xp_next_level_cost(l + 1) for l in range(1, 500)):
        failures.append("xp_next_level_cost не монотонна")
    if xp_next_level_cost(10**6) != 1049:
        failures.append(f"асимптота XP: {xp_next_level_cost(10**6)} != 1049 (предел 1050)")
    if not _close(level_factor(10**6), 8.0, 1e-3):
        failures.append("предел level_factor должен быть 8.0")
    if not _close(power_factor(0), 0.0) or not _close(power_factor(9), 0.4737, 1e-3):
        failures.append("power_factor не совпадает с ожиданием (0 -> 0.0, 9 -> 0.4737)")
    if kill_xp(1, 0) != 1.0:
        failures.append("kill_xp(1,0) должен быть клампнут в 1.0")
    if not _close(curve_diminishing_x3p1(0), 0.0) or not _close(curve_diminishing_x3p15(0), 0.0):
        failures.append("diminishing-кривые должны возвращать 0 при n<1")

    print("=== Alpha curves reference (S00-R02) ===")
    print("XP cost  L->cost  :", {l: xp_next_level_cost(l) for l in (1, 2, 5, 10, 20, 50, 100)})
    print("level    L->mult  :", {l: round(level_factor(l), 3) for l in (1, 5, 11, 21, 51, 101)})
    print("stars    S->mult  :", {s: round(star_factor(s), 3) for s in range(0, 6)})
    print("haste    S->rate  :", {s: round(form_haste(s), 3) for s in range(0, 6)})
    print("power    R->factor:", {r: round(power_factor(r), 3) for r in (0, 1, 5, 9, 19)})

    if failures:
        print(f"FAIL: {len(failures)} проверок не прошли")
        for item in failures:
            print(" -", item)
        return 1
    print("PASS: все ожидаемые значения воспроизведены")
    return 0


if __name__ == "__main__":
    if "--check" not in sys.argv:
        print("usage: alpha_curves_reference.py --check")
        raise SystemExit(2)
    raise SystemExit(check())
