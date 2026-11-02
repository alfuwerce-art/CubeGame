#!/usr/bin/env python3
"""CubeGame S00-R02 — эталонные проверки боевых формул Alpha 2013 (исправление FIX-01).

Собственная реализация формул, восстановленных из Alpha (не декомпилированный код):
  * calcMaxHealth  — бывшая `stat_calcAttackDamage` (имя в реконструкции ошибочно), §E2;
  * calcWeaponDamage — `stat_calcWeaponDamage`, §E2;
  * block roll      — `combat_rollBlockSuccess`, §E6;
  * hit roll        — `combat_rollHitChance`, §E6.

Зачем скрипт: старые тесты `VALIDATION_CASES.md` `VC-CMB-01…04` трактовали
`stat_calcAttackDamage` как урон. Здесь зафиксированы правильные входы/выходы:
класс меняет максимум HP, а не урон; `+0x060 == 0` — роль «игрок», а не форма.

Запуск: python docs/research/alpha2013/tools/alpha_combat_reference.py --check
"""

from __future__ import annotations

import sys

from alpha_curves_reference import curve_level05, level_factor, star_factor


CLASS_MULT = {1: 1.3, 2: 1.1, 3: 1.0, 4: 1.2}  # Warrior, Ranger, Mage, Rogue
ROLE_PLAYER = 0


def calc_max_health(base_hp: float, level: int, stars: int, role: int,
                    class_id: int = 0, spec: int = 0,
                    equipment_bonus: float = 0.0) -> float:
    """E2: base(0x178) * levelFactor * starOr2(role) * classMult * specMult + items.

    role == 0 (игрок) => starFactor жёстко 2.0; role != 0 (моб/NPC/питомец) =>
    2^(0.25*stars). Класс задаёт множитель HP; Mage — 1.0.
    """
    star = 2.0 if role == ROLE_PLAYER else star_factor(stars)
    class_mult = CLASS_MULT.get(class_id, 1.0)
    spec_mult = 1.25 if (class_id == 1 and spec == 1) else 1.0
    return base_hp * level_factor(level) * star * class_mult * spec_mult + equipment_bonus


def calc_weapon_damage(base_weapon: float, level: int, stars: int = 0,
                       attack_id: int = 0) -> float:
    """E2: base(0x180) * levelFactor (без классового множителя).

    Учитывается только ветка «без апгрейдов»; флаги/списки апгрейдов и
    множители по id атаки (0x57/0x5b/0x5d/0x60) добавляются отдельными тестами.
    """
    damage = base_weapon * level_factor(level)
    multipliers = {0x57: 50.0, 0x5B: 2.0, 0x5D: 5.0, 0x60: 3.0}
    return damage * multipliers.get(attack_id, 1.0)


def block_threshold(level: int) -> int:
    """E6: int((1 - 1/((L-1)*0.05+1))*100 + 1)."""
    return int(curve_level05(float(level)) * 100.0 + 1.0)


def block_succeeds(level: int, roll: int) -> bool:
    """E6: roll 0..100; провал, если roll > порог."""
    return roll <= block_threshold(level)


def hit_succeeds(chance: float, roll: int) -> bool:
    """E6: roll 0..32767; hit, если roll/32767 < chance."""
    return roll / 32767.0 < chance


def effective_hit_chance(chance: float, material: int, subtype: int) -> float:
    """E6: модификаторы «материала» оружия (0xaa8==3)."""
    if material == 3 and subtype in (0, 1, 2, 0x0C):
        return chance * 0.5
    if material == 3 and subtype in (8, 10):
        return chance * 0.3
    return chance


def _close(a: float, b: float, eps: float = 1e-4) -> bool:
    return abs(a - b) <= eps


def check() -> int:
    failures: list[str] = []

    # --- FIX-01: HP, а не урон; классы масштабируют HP ---------------------
    cases = [
        ("Mage", 100.0, 1, 0, 0, 3, 0, 200.0),
        ("Warrior", 100.0, 1, 0, 0, 1, 0, 260.0),
        ("Warrior/Guardian", 100.0, 1, 0, 0, 1, 1, 325.0),
        ("Rogue", 100.0, 1, 0, 0, 4, 0, 240.0),
        ("Ranger", 100.0, 1, 0, 0, 2, 0, 220.0),
    ]
    for name, base, level, stars, role, cls, spec, expected in cases:
        got = calc_max_health(base, level, stars, role, cls, spec)
        if not _close(got, expected):
            failures.append(f"maxHP {name} = {got:.4f}, ожидалось {expected:.4f}")

    # --- 0x060: роль, а не форма ------------------------------------------
    player_s2 = calc_max_health(100.0, 1, 2, ROLE_PLAYER)
    monster_s2 = calc_max_health(100.0, 1, 2, 1)
    if not _close(player_s2, 200.0):
        failures.append(f"player S=2 maxHP = {player_s2:.4f}, ожидалось 200.0")
    if not _close(monster_s2, 100.0 * 2.0 ** 0.5):
        failures.append(f"monster S=2 maxHP = {monster_s2:.4f}, ожидалось {100.0 * 2.0 ** 0.5:.4f}")

    # --- Урон оружия: класс не участвует ----------------------------------
    for cls in (1, 2, 3, 4):
        # Урон оружия не принимает класс: проверяем, что формула не зависит от него.
        got = calc_weapon_damage(1.0, 1)
        if not _close(got, 1.0):
            failures.append(f"weaponDamage base=1 L=1 (class context={cls}) = {got:.4f}, ожидалось 1.0")
    if not _close(calc_weapon_damage(2.0, 5), 2.0 * level_factor(5)):
        failures.append("weaponDamage base=2 L=5 не совпал с base*levelFactor")
    if not _close(calc_weapon_damage(1.0, 1, attack_id=0x57), 50.0):
        failures.append("weaponDamage id=0x57 должен давать x50")

    # --- Блок и попадание -------------------------------------------------
    expected_thresholds = {1: 1, 2: 5, 5: 17, 10: 32, 20: 49, 50: 72}
    for level, expected in expected_thresholds.items():
        got = block_threshold(level)
        if got != expected:
            failures.append(f"block_threshold({level}) = {got}, ожидалось {expected}")
    if not block_succeeds(1, 1) or block_succeeds(1, 2):
        failures.append("block roll на L=1 должен проходить только при roll <= 1")
    if not hit_succeeds(0.5, 16383):
        failures.append("hit roll 16383/32767 должен попадать при chance=0.5")
    if hit_succeeds(0.5, 16384):
        failures.append("hit roll 16384/32767 не должен попадать при chance=0.5")
    if not _close(effective_hit_chance(0.5, 3, 8), 0.15):
        failures.append("rune subtype 8 должен давать chance x0.3 = 0.15")
    if not _close(effective_hit_chance(0.5, 3, 0x0C), 0.25):
        failures.append("rune subtype 0x0c должен давать chance x0.5 = 0.25")

    print("=== Alpha combat reference (S00-R02, FIX-01) ===")
    print(f"  maxHP L1 base100: Mage={calc_max_health(100,1,0,0,3):.1f}"
          f" Warrior={calc_max_health(100,1,0,0,1):.1f}"
          f" Warrior/Guardian={calc_max_health(100,1,0,0,1,1):.1f}"
          f" Rogue={calc_max_health(100,1,0,0,4):.1f}"
          f" Ranger={calc_max_health(100,1,0,0,2):.1f}")
    print(f"  maxHP 0x060: player S2={player_s2:.1f}, monster S2={monster_s2:.3f}")
    print(f"  weapon base1 L1={calc_weapon_damage(1,1):.2f}; base2 L5={calc_weapon_damage(2,5):.4f}")
    print(f"  block thresholds={ {l: block_threshold(l) for l in expected_thresholds} }")

    if failures:
        print(f"FAIL: {len(failures)} проверок не прошли")
        for item in failures:
            print(" -", item)
        return 1
    print("PASS: HP/урон/блок/попадание воспроизведены по E2/E6")
    return 0


if __name__ == "__main__":
    if "--check" not in sys.argv:
        print("usage: alpha_combat_reference.py --check")
        raise SystemExit(2)
    raise SystemExit(check())
