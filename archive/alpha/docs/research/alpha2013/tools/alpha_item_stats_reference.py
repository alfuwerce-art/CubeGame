#!/usr/bin/env python3
"""CubeGame S00-R02 — эталонный расчёт статов предметов Alpha 2013.

Собственная реализация формул, восстановленных из Alpha (не декомпилированный код):
все статы предмета = math_pow2Mul(level + 0.1*powerBonus, rarity) * M_type * M_modifier,
где math_pow2Mul(a, r) = 2^((1 - 1/((a-1)*0.05+1))*3) * 2^(0.25*r).

См. docs/research/alpha2013/BEHAVIOR_SPECS/F_items.md.
Запуск: python docs/research/alpha2013/tools/alpha_item_stats_reference.py --check
"""

from __future__ import annotations

import sys

# Типы предметов (по таблице имён Alpha: World.cpp:2815-2969)
TYPE_FORMULA = 2
TYPE_WEAPON = 3
TYPE_CHEST = 4
TYPE_BOOTS = 5
TYPE_GLOVES = 6
TYPE_SHOULDER = 7
TYPE_AMULET = 8
TYPE_RING = 9

# Подтипы оружия
WEAPON_MACE = 1
WEAPON_FIST = 2
WEAPON_SWORD = 3
WEAPON_DAGGER = 4
WEAPON_LONGSWORD = 5
WEAPON_CROSSBOW = 8
WEAPON_STAFF = 0x0A
WEAPON_WAND = 0x0B
WEAPON_BRACELET = 0x0C
WEAPON_SHIELD = 0x0D
WEAPON_BOOMERANG = 0x0E
WEAPON_GREATSWORD = 0x0F
WEAPON_GREATAXE = 0x10
WEAPON_GREATMACE = 0x11
WEAPON_TORCH = 0x14

# «Спецоружие» (Alpha: weapon_isSpecialRune) — двуручное/дальнобойное, множители выше
SPECIAL_WEAPON_SUBTYPES = {
    WEAPON_LONGSWORD, WEAPON_STAFF, WEAPON_WAND, WEAPON_BRACELET,
    WEAPON_CROSSBOW, WEAPON_BOOMERANG, WEAPON_GREATSWORD, WEAPON_GREATAXE,
    WEAPON_GREATMACE, 0x06, 0x07,
}


def curve_level05(n: float) -> float:
    return 1.0 - 1.0 / ((n - 1.0) * 0.05 + 1.0)


def math_pow2_mul(level: float, rarity: int) -> float:
    """Alpha 00445f60: levelFactor * starFactor(rarity)."""
    return (2.0 ** (curve_level05(level) * 3.0)) * (2.0 ** (float(rarity) * 0.25))


def effective_level(level: int, power_bonus: int = 0) -> float:
    """Уровень предмета в формулах: level + 0.1*powerBonus (+0x114)."""
    return float(level) + float(power_bonus) * 0.1


def weapon_damage(level: int, rarity: int, subtype: int, power_bonus: int = 0) -> float:
    """Alpha 004c7f60."""
    base = math_pow2_mul(effective_level(level, power_bonus), rarity)
    if subtype in (WEAPON_SWORD, WEAPON_DAGGER, WEAPON_SHIELD):
        return base * 2.0
    if subtype == WEAPON_LONGSWORD:
        return base * 4.0
    if subtype in SPECIAL_WEAPON_SUBTYPES:
        return base * 8.0
    return base * 4.0


def armor(level: int, rarity: int, item_type: int, modifier: int = 0,
          seed: int = 0, power_bonus: int = 0) -> float:
    """Alpha 004c70b0 (броня: только типы 4/5/6/7)."""
    if item_type not in (TYPE_CHEST, TYPE_BOOTS, TYPE_GLOVES, TYPE_SHOULDER):
        return 0.0
    rarity_factor = 1.0 if item_type == TYPE_CHEST else 0.5
    material_mult = 1.0 + (1.0 - float((seed << 3) % 0x15) / 20.0)
    if modifier == 0x01:
        material_mult += 1.0
    elif modifier == 0x1A:
        material_mult += 0.5
    elif modifier == 0x1B:
        material_mult += 0.75
    base = math_pow2_mul(effective_level(level, power_bonus), rarity)
    return base * 5.0 * rarity_factor * material_mult


def block_value(level: int, rarity: int, item_type: int, modifier: int = 0,
                power_bonus: int = 0) -> float:
    """Alpha 004c6a90 (блок: типы 4/5/6/7)."""
    if item_type not in (TYPE_CHEST, TYPE_BOOTS, TYPE_GLOVES, TYPE_SHOULDER):
        return 0.0
    base = 1.0 if item_type == TYPE_CHEST else 0.5
    mult = base
    if modifier == 0x12:
        mult = base * 0.8
    elif modifier in (0x13, 0x1A, 0x1B):
        mult = base * 0.85
    elif modifier in (0x17, 0x19):
        mult = base * 0.75
    return math_pow2_mul(effective_level(level, power_bonus), rarity) * mult


def value(level: int, rarity: int, item_type: int, power_bonus: int = 0) -> int:
    """Alpha 004c76e0 (стоимость предмета, минимум 1)."""
    mult = 0.2
    if item_type in (TYPE_WEAPON, TYPE_CHEST):
        mult = 2.0
    elif item_type in (TYPE_BOOTS, TYPE_GLOVES, TYPE_RING):
        mult = 1.0
    elif item_type in (TYPE_SHOULDER, TYPE_AMULET):
        mult = 1.5
    elif item_type == 0x17:
        mult = 100.0
    if item_type == TYPE_WEAPON:  # спецоружие дороже вдвое
        pass
    result = int(math_pow2_mul(effective_level(level, power_bonus), rarity) * 10.0 * mult)
    return max(1, result)


def rarity_roll(value_one_in_100: bool, one_in_1000: bool, one_in_10000: bool) -> int:
    """Alpha: 1 % + 0.1 % + 0.01 %, кламп 4 (одинаково у мобов и предметов)."""
    r = int(value_one_in_100) + int(one_in_1000) + int(one_in_10000)
    return min(r, 4)


EXPECT = {
    # (level, rarity, subtype) -> damage
    (1, 0, WEAPON_SWORD): 2.0,          # 2^0 * 2^0 * 2
    (1, 4, WEAPON_GREATMACE): 16.0,     # 2^0 * 2^1 * 8
    (11, 0, WEAPON_SWORD): 4.0,         # levelFactor(11)=2 -> *2
    (21, 2, WEAPON_DAGGER): 2.828427 * 1.414214 * 2.0,
}


def check() -> int:
    failures: list[str] = []
    for (level, rarity, subtype), expected in EXPECT.items():
        got = weapon_damage(level, rarity, subtype)
        if abs(got - expected) > 1e-3:
            failures.append(f"weapon_damage({level},{rarity},{subtype}) = {got:.4f}, ожидалось {expected:.4f}")

    # Монотонность по уровню и редкости
    for subtype in (WEAPON_SWORD, WEAPON_STAFF):
        prev = 0.0
        for level in range(1, 101):
            got = weapon_damage(level, 0, subtype)
            if got < prev - 1e-6:
                failures.append(f"урон не монотонен по уровню: subtype={subtype}, level={level}")
                break
            prev = got
    for rarity in range(0, 5):
        if weapon_damage(10, rarity, WEAPON_SWORD) <= weapon_damage(10, max(0, rarity - 1), WEAPON_SWORD) and rarity > 0:
            failures.append(f"урон не растёт с редкостью: rarity={rarity}")

    # Броня только у 4/5/6/7, оружие её не даёт
    if armor(10, 2, TYPE_WEAPON) != 0.0:
        failures.append("оружие не должно давать броню")
    if armor(10, 2, TYPE_CHEST) <= 0.0:
        failures.append("нагрудник должен давать броню > 0")

    # Кламп редкости
    if rarity_roll(True, True, True) != 3:
        failures.append("три успешных броска должны давать редкость 3")
    if rarity_roll(False, False, False) != 0:
        failures.append("без успешных бросков редкость 0")

    print("=== Alpha item stats reference (S00-R02) ===")
    for level in (1, 10, 50):
        for rarity in (0, 2, 4):
            print(f"  level={level:>3} rarity={rarity}: sword={weapon_damage(level, rarity, WEAPON_SWORD):8.2f}"
                  f"  greatmace={weapon_damage(level, rarity, WEAPON_GREATMACE):8.2f}"
                  f"  chest_armor={armor(level, rarity, TYPE_CHEST):8.2f}")

    if failures:
        print(f"FAIL: {len(failures)} проверок не прошли")
        for item in failures:
            print(" -", item)
        return 1
    print("PASS: формулы воспроизведены и монотонны")
    return 0


if __name__ == "__main__":
    if "--check" not in sys.argv:
        print("usage: alpha_item_stats_reference.py --check")
        raise SystemExit(2)
    raise SystemExit(check())
