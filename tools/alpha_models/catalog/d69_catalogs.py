"""S05B-2 / D69: the two registries the task requires, generated from evidence (never hand-typed).

1. ALPHA_ORIGINAL_MODEL_ANIMATION_CATALOG — what the ORIGINAL Alpha 2013 has, per creature kind:
   models (executed creature_generateAppearance), Alpha role (Server.exe spawn faction, spawn_roles),
   animations (executed drawModel walk/idle; AI action launches executed in CombatBehavior; weapon-only
   launches; kind-specific branches), live evidence. Provenance columns are separate:
     LIVE_ALPHA_OBSERVED  seen in the running original game (census 2026-11-08, earlier pose captures)
     ALPHA_CODE           proven by executing / reading the original code
     ALPHA_ASSET          original data only (.cub / database)
     LIVE_UNOBSERVED      not seen in the running game (never "absent")
   The original facts do not depend on our Unity result.
2. UNITY_TRANSFER_DISPOSITION_MATRIX — our Unity transfer (D65 + D69 fixes) against (1): one verdict per
   model / walk / idle / action: KEEP | DISABLE_IN_GAME | FIX | ADD_MISSING | UNKNOWN_NEEDS_RESEARCH, and the
   D69 outcome FIXED | TRANSFERRED | DISABLED_IN_GAME | DEFERRED_TO_M0 | BLOCKED | KEPT.

    python -m tools.alpha_models.catalog.d69_catalogs            # -> .scratch/d69/catalog/*.json + docs/*.md
"""
from __future__ import annotations

import argparse
import collections
import glob
import json
from pathlib import Path

from tools.alpha_models.catalog import spawn_roles as sr
from tools.alpha_models.universal import contract as ct
from tools.alpha_models.universal import creature_rig as cr

ROOT = Path(__file__).resolve().parents[3]
SCR = ROOT / ".scratch"
STATIC = SCR / "catalog" / "alpha_static.json"
ASSETS = SCR / "catalog" / "asset_catalog.json"
TRIGGERS = Path(__file__).with_name("npc_trigger_evidence.json")
WALK_ORACLE = Path(__file__).with_name("walk_oracle_evidence.json")
CENSUS_GLOB = str(SCR / "d69" / "live" / "census" / "*.json")
BATCH = SCR / "universal" / "batch"
D69_VERIFY = SCR / "d69" / "verify" / "state.json"
OUT = SCR / "d69" / "catalog"
DOC_A = ROOT / "docs" / "research" / "alpha2013" / "S05B2_ALPHA_ORIGINAL_MODEL_ANIMATION_CATALOG.md"
DOC_B = ROOT / "docs" / "research" / "alpha2013" / "S05B2_UNITY_TRANSFER_DISPOSITION_MATRIX.md"

BOSS_KINDS = {0x65, 0x6B, 0x6C, 0x6D, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77}   # creature_isBossType 0x4445D0
PLANT_MONSTERS = {0x45, 0x46, 0x47, 0x48, 0x49}                             # creatures with plant models
# earlier live pose captures (pose_capture, S05A2-TRANSFER / PARAMETRIC-WALK / D60): walk drawn by Alpha
LIVE_WALK_CAPTURED = {0x02: "player walk/idle/attack captured (S05A2-TRANSFER)",
                      0x20: "cat walk captured (PARAMETRIC-WALK)", 0x3F: "runner walk captured (PARAMETRIC-WALK)",
                      0x03: "city NPC walk/head lean captured (PARAMETRIC-WALK, D60 full dump)"}
OBJECT_CLASS = {**{k: "decorative plant" for k in range(0x78, 0x83)},
                **{k: "ore deposit (resource)" for k in range(0x83, 0x8C)},
                0x8C: "training target", 0x8D: "training target", 0x8E: "training target",
                0x8F: "vase (breakable object)", 0x90: "bomb (object)", 0x18: "egg"}
ACTION_NAMES = {3: "melee hit", 4: "melee hit 2", 6: "special/heavy", 22: "weapon skill", 25: "weapon skill",
                37: "class action", 40: "class action", 42: "class action", 43: "class action",
                51: "breath/flight state (dragon/winged)", 54: "attack (humanoid default)", 55: "skill",
                69: "mammoth attack", 70: "saurian attack", 72: "turtle attack", 73: "mammoth attack",
                74: "mammoth attack", 75: "creature attack", 76: "golem attack", 108: "throw (golem/rockling)"}


def _load(p, default=None):
    p = Path(p)
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else default


def live_census():
    kinds = collections.defaultdict(lambda: {"count": 0, "actions_seen": set(), "captures": []})
    for f in sorted(glob.glob(CENSUS_GLOB)):
        d = _load(f)
        for k, v in d.get("kinds", {}).items():
            s = kinds[k]
            s["count"] += v["count"]
            s["actions_seen"].update(v["actions_seen"])
            s["captures"].append(d.get("label") or Path(f).stem)
    return {k: {"count": v["count"], "actions_seen": sorted(v["actions_seen"]), "captures": v["captures"]}
            for k, v in kinds.items()}


def unity_results():
    """kind -> {'runs','pass','actions': {code: status/reason}} from every batch (D59..D65) + D69 verify."""
    out = collections.defaultdict(lambda: {"runs": 0, "pass": 0, "actions": {}, "d69": {}})
    for scope in ("s05b1", "s05b2", "s05ba", "d63_full", "d63_actions"):
        s = _load(BATCH / scope / "summary.json", {})
        for e in s.get("entries", []):
            r = out[e["kind"]]
            r["runs"] += 1
            r["pass"] += e.get("verdict") == "PASS"
        for lib in glob.glob(str(BATCH / scope / "libraries" / "*" / "actions.json")):
            m = _load(lib)
            k = f"0x{m['kind']:02x}"
            for a in m.get("actions", []):
                out[k]["actions"][a["action"]] = {"status": a["status"], "reason": a.get("reason"),
                                                  "library": scope}
    # D69 merged libraries (D60 + D64 conditional launches): actions new versus the earlier library
    for lib in glob.glob(str(SCR / "d69" / "verify" / "libraries" / "*" / "actions.json")):
        m = _load(lib)
        k = f"0x{m['kind']:02x}"
        for a in m.get("actions", []):
            if a["action"] not in out[k]["actions"]:
                out[k]["actions"][a["action"]] = {"status": a["status"], "reason": a.get("reason"),
                                                  "library": "d69_merged", "added_d69": True}
    st = _load(D69_VERIFY, {"entries": {}})
    for key, rec in st.get("entries", {}).items():
        if key.startswith("0x"):
            out[key.split("_")[0]]["d69"][key.split("_", 1)[1]] = rec["verdict"]
    return out


def original_catalog():
    static = _load(STATIC)
    trig = _load(TRIGGERS)["kinds"]
    wk = _load(WALK_ORACLE)["kinds"]
    walk = wk if isinstance(wk, dict) else {r["kind"]: r for r in wk}
    reg = static["registry"]
    census = live_census()
    rows = []
    for k, e in static["kinds"].items():
        kind = int(k, 16)
        app = e.get("appearance") or {}
        res = cr.resolve(kind, app) if app else None
        parts = res.parts if res else []
        models = sorted({reg.get(str(res.models[p]), f"#{res.models[p]}") for p in parts}) if res else []
        policy, basis, reason = sr.game_policy(kind)
        t = trig.get(k, {})
        unarmed = sorted(int(a) for a in (t.get("unarmed_actions") or {}))
        weapon_only = sorted(set(int(a) for a in (t.get("weapon_actions") or [])) - set(unarmed))
        if policy == "STATIC_OBJECT":
            role = "PASSIVE_OBJECT"
            klass = OBJECT_CLASS.get(kind, "object")
        else:
            role = "CREATURE"
            klass = ("plant monster" if kind in PLANT_MONSTERS else
                     "boss" if kind in BOSS_KINDS else
                     ct.structural_category(parts, app).lower().replace("_", " ") if app else "?")
        w = walk.get(k, {})
        live = census.get(k)
        if policy == "STATIC_OBJECT" and basis == "ALPHA_CODE_SPAWN":
            anim_walk = "NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE)"
            anim_actions = ("NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> action timer skipped 0x53E2A8); "
                            f"the AI region would select {unarmed} if it ran")
        elif policy == "STATIC_OBJECT":
            anim_walk = f"drawModel formula exists (ALPHA_CODE); game use: {basis}"
            anim_actions = f"AI region selects {unarmed} (ALPHA_CODE emulation); game use: {basis}"
        else:
            anim_walk = f"drawModel walk/idle (ALPHA_CODE, oracle {w.get('status', '?')})"
            anim_actions = (f"AI launches {unarmed} (ALPHA_CODE, {t.get('status', '?')})" if unarmed
                            else f"no unarmed AI launch proven ({t.get('status', '?')})")
        live_txt = "LIVE_UNOBSERVED"
        if live:
            live_txt = (f"LIVE_ALPHA_OBSERVED: rendered x{live['count']} ({', '.join(live['captures'])}), "
                        f"action bytes {live['actions_seen']}")
        if kind in LIVE_WALK_CAPTURED:
            live_txt += f"; earlier capture: {LIVE_WALK_CAPTURED[kind]}"
        special = []
        if kind == 0x65:
            special += ["head anchor (sx/2, sy*0.2, sz/2) — drawModel kind branch (ALPHA_CODE)",
                        "dragon-wing.cub (registry 428) is LOADED but never drawn (no reference outside the loader 0x4F7869; ALPHA_CODE) -> the Alpha dragon is wingless",
                        "action 51 (state 70): static pose; the visible part is an effect (R-3)"]
        if kind in (0x57, 0x60):
            special.append("kind-specific drawModel branches (executed by the oracle)")
        if kind == 0x76:
            special.append("attack branch drives ANOTHER creature (R-9): actions not reconstructed")
        rows.append({"kind": k, "name": e.get("name_hint") or "?", "alpha_role": role, "class": klass,
                     "spawn_basis": basis or "", "parts": parts, "models": models,
                     "walk_idle": anim_walk, "actions": anim_actions, "unarmed_actions": unarmed,
                     "weapon_only_actions": weapon_only, "special": special, "live": live_txt,
                     "boss": kind in BOSS_KINDS})
    assets = _load(ASSETS, {}).get("metrics", {})
    return {"format": "cubegame.d69_alpha_original_catalog/1", "kinds": rows, "asset_routes": assets.get("by_route", {}),
            "asset_metrics": {k: v for k, v in assets.items() if k != "by_route"}, "live_census": census}


def disposition(original, unity):
    rows = []
    for o in original["kinds"]:
        k, kind = o["kind"], int(o["kind"], 16)
        u = unity.get(k, {"runs": 0, "pass": 0, "actions": {}, "d69": {}})
        static = o["alpha_role"] == "PASSIVE_OBJECT"
        d69 = u["d69"]

        def row(item, alpha, unity_txt, verdict, outcome, severity, reason, owner="Claude (D69)"):
            rows.append({"kind": k, "name": o["name"], "item": item, "alpha": alpha, "unity": unity_txt,
                         "verdict": verdict, "outcome": outcome, "severity": severity, "reason": reason,
                         "owner": owner})
        geom = f"D65 {u['pass']}/{u['runs']} runs PASS" if u["runs"] else "not imported"
        if kind == 0x65:
            row("model/rig", "head anchored at sy*0.2 (ALPHA_CODE)", geom + "; head mesh used the centre anchor",
                "FIX", "FIXED" if d69.get("actions") == "PASS" else "FIX_NOT_VERIFIED", "HIGH",
                "head shifted ~13 grid voxels (0.58 world units) into the body in every pose; fixed: anchor rule "
                "head_0x65 in pipeline/contract/AlphaRig; end-to-end test vs executed drawModel")
            row("wings", "dragon-wing.cub never drawn (ALPHA_CODE)", "no wing part", "KEEP", "KEPT", "-",
                "adding wings would contradict the original")
        else:
            row("model/rig", "appearance models (ALPHA_CODE)", geom, "KEEP" if u["pass"] else "UNKNOWN_NEEDS_RESEARCH",
                "KEPT" if u["pass"] else "BLOCKED", "-", "geometry/rig verified in D65 batches")
        if static:
            row("walk", o["walk_idle"], "D65: parametric walk active in the controller", "DISABLE_IN_GAME",
                "DISABLED_IN_GAME" if d69.get("static") == "PASS" else "DISABLE_NOT_VERIFIED", "HIGH",
                "STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data")
            row("idle", "standing pose", "standing pose (formula amp 0)", "KEEP", "KEPT", "-", "only pose of the object")
        else:
            row("walk/idle", o["walk_idle"], "parametric walk formula = executed drawModel (D64/D65)", "KEEP",
                "FIXED" if kind == 0x65 else "KEPT", "-",
                "RECONSTRUCTED_FROM_EVIDENCE; live_check per original catalog")
        for a in sorted(set(o["unarmed_actions"]) | set(u["actions"])):
            ua = u["actions"].get(a)
            st = ua["status"] if ua else "NOT_BAKED"
            alpha = f"{ACTION_NAMES.get(a, 'action')} — {'AI launch (ALPHA_CODE)' if a in o['unarmed_actions'] else 'library'}"
            if static:
                row(f"action {a}", alpha + "; NOT used by the Alpha game (passive)", st, "DISABLE_IN_GAME",
                    "DISABLED_IN_GAME" if d69.get("static") == "PASS" else "DISABLE_NOT_VERIFIED", "HIGH",
                    "decorative/passive object never acts; research clip kept in .scratch library")
            elif st == "EMULATED_ALPHA_CODE" and ua.get("added_d69"):
                row(f"action {a}", alpha, "D69: baked with the merged profile, played by the Unity controller",
                    "ADD_MISSING", "TRANSFERRED" if d69.get("merged") == "PASS" else "ADD_NOT_VERIFIED", "MEDIUM",
                    "launch proven by the executed AI region in other NPC class/spec states (D64); was missing "
                    "from the D60 single-state profile; game trigger DEFERRED_TO_M0", "Claude (D69)")
            elif st == "EMULATED_ALPHA_CODE":
                row(f"action {a}", alpha, "EMULATED_ALPHA_CODE clip, played by the Unity controller", "KEEP",
                    ("FIXED" if kind == 0x65 else "TRANSFERRED"), "-",
                    "game trigger (AI/combat) DEFERRED_TO_M0; live_check NOT_CAPTURED", "M0 (DeepSeek)")
            elif ua and "ACTION_STATIC" in (ua.get("reason") or ""):
                row(f"action {a}", alpha + "; pose static", "BLOCKED ACTION_STATIC", "ADD_MISSING", "BLOCKED", "MEDIUM",
                    "the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer",
                    "next S05/M0 effects task")
            elif ua and "PART_SET_CHANGED" in (ua.get("reason") or ""):
                row(f"action {a}", alpha + "; needs a dynamic part", "BLOCKED ACTION_PART_SET_CHANGED", "ADD_MISSING",
                    "BLOCKED", "MEDIUM", "thrown/held object model is not in the kind's rig (R-8)", "next S05 task")
            elif kind == 0x02 and st == "NOT_BAKED":
                row(f"action {a}", alpha, "not in the Unity library (0x02 carries the player-input profile)",
                    "ADD_MISSING", "DEFERRED_TO_M0", "MEDIUM",
                    "NPC HumanMale (city NPCs are 0x02) needs a creature-AI profile next to the player profile of "
                    "the same kind: a controller decision for M0-06 (poses: same family as the other humanoids)",
                    "M0-06 (DeepSeek)")
            else:
                row(f"action {a}", alpha, st, "UNKNOWN_NEEDS_RESEARCH", "BLOCKED", "LOW", str(ua and ua.get("reason")))
        if o["weapon_only_actions"] and not static:
            row("weapon-only actions", f"{o['weapon_only_actions']} launched only with a weapon (ALPHA_CODE)",
                "not transferred (no NPC weapon layer on this rig)", "ADD_MISSING", "DEFERRED_TO_M0", "MEDIUM",
                "NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system",
                "M0 (DeepSeek)")
        if kind == 0x76:
            row("actions", "AI drives another creature (R-9)", "no actions", "UNKNOWN_NEEDS_RESEARCH", "BLOCKED", "LOW",
                "world creature list is an input of the AI region", "M0 AI research")
    counts = collections.Counter(r["verdict"] for r in rows)
    outcomes = collections.Counter(r["outcome"] for r in rows)
    return {"format": "cubegame.d69_unity_disposition/1", "rows": rows, "verdicts": dict(counts),
            "outcomes": dict(outcomes)}


def _md_table(head, rows):
    out = ["| " + " | ".join(head) + " |", "|" + "---|" * len(head)]
    for r in rows:
        out.append("| " + " | ".join(str(x).replace("|", "/") for x in r) + " |")
    return "\n".join(out)


def write_docs(orig, disp):
    kinds = orig["kinds"]
    by_role = collections.Counter(r["alpha_role"] for r in kinds)
    by_class = collections.Counter(r["class"] for r in kinds)
    live_n = sum(1 for r in kinds if r["live"].startswith("LIVE_ALPHA_OBSERVED") or "earlier capture" in r["live"])
    a = ["# S05B-2 / D69 — каталог ОРИГИНАЛЬНОЙ Alpha 2013: модели, существа, объекты и их анимации",
         "",
         "**Эталон — оригинальная Alpha**, не Unity и не D65. Сгенерировано `python -m tools.alpha_models.catalog.d69_catalogs` "
         "из доказательств: исполненный код `Cube.exe`/`Server.exe` (`ALPHA_CODE`), данные `.cub` (`ALPHA_ASSET`), "
         "одна живая перепись существ в запущенной игре (`LIVE_ALPHA_OBSERVED`, 2026-11-08, город Арулон) и прежние "
         "живые захваты поз. **Запуск игры остановлен по указанию продюсера (2026-11-08)** — поэтому почти все виды "
         "`LIVE_UNOBSERVED`; это НЕ означает отсутствия поведения.",
         "",
         "## Сводка",
         "",
         f"* Видов существ/объектов (`creature+0x64`): **{len(kinds)}**; роль в Alpha: " +
         ", ".join(f"{k} {v}" for k, v in by_role.items()) + ".",
         f"* Классы: " + ", ".join(f"{k} {v}" for k, v in sorted(by_class.items())) + ".",
         f"* Живое наблюдение в оригинальной игре: **{live_n}** видов (перепись + прежние захваты), остальные `LIVE_UNOBSERVED`.",
         "* Моделей `.cub` (все маршруты, D64 `asset_catalog`): " +
         ", ".join(f"{k} {v}" for k, v in orig["asset_routes"].items()) + ".",
         "",
         "## Ключевые оригинальные факты, найденные в D69 (ALPHA_CODE)",
         "",
         "1. **Пассивные объекты не ходят и не действуют в самой Alpha.** Генератор мира `Server.exe` пишет в запись "
         "спавна фракцию 6 (пассивный) для всех растений, руд, чучела, вазы и бомбы; при спавне фракция 6 пропускает "
         "создание `cube::RandomWalkBehavior` (`0x5360AE`, RTTI конструктора `0x41CB90`), а в тике существа — обновление "
         "таймера действия (`0x53E2A8`). Доказательства: `tools/alpha_models/catalog/spawn_role_evidence.json` (байты по адресам).",
         "2. **Дракон 0x65 бескрылый в оригинале**: `dragon-wing.cub` (индекс 428) загружается (`0x4F7869`), но ни одна "
         "инструкция вне загрузчика его не рисует. Голова — собственный якорь `(sx/2, sy·0.2, sz/2)`.",
         "3. **Растительные монстры** (PlantCreature 0x45, RadishCreature 0x46, Onionling 0x47/0x48, Devourer 0x49) — "
         "обычные существа (не фракция 6): ходьба и атаки у них оригинальные.",
         "",
         "## Живая перепись (запущенная оригинальная игра)",
         "",
         _md_table(["kind", "отрисовано", "байты действия", "захват"],
                   [(k, v["count"], v["actions_seen"], ", ".join(v["captures"])) for k, v in sorted(orig["live_census"].items())]),
         "",
         "## Каталог по видам",
         "",
         _md_table(["kind", "имя", "роль Alpha", "класс", "модели", "ходьба/покой (оригинал)", "действия (оригинал)",
                    "только с оружием", "особое", "живое"],
                   [(r["kind"], r["name"], r["alpha_role"] + (f" ({r['spawn_basis']})" if r["spawn_basis"] else ""),
                     r["class"] + (" · boss" if r["boss"] and r["class"] != "boss" else ""), ", ".join(r["models"]),
                     r["walk_idle"], r["actions"], r["weapon_only_actions"] or "-", "; ".join(r["special"]) or "-",
                     r["live"]) for r in kinds]),
         "",
         "Неживые модели (оружие, броня, предметы, постройки, мебель) — статичные модели без анимации модели "
         "(их движение — игровая симуляция: подбор, снаряды, эффекты); маршруты и статусы каждой из 2878 моделей — "
         "D64 `asset_catalog` (`.scratch/catalog/asset_catalog.json`, локально).",
         ""]
    DOC_A.write_text("\n".join(a), encoding="utf-8")
    rows = disp["rows"]
    b = ["# S05B-2 / D69 — матрица переноса Alpha → Unity (KEEP / DISABLE_IN_GAME / FIX / ADD_MISSING / UNKNOWN)",
         "",
         "Сравнение **нашего** Unity-переноса (D65 + исправления D69) с [каталогом оригинала]"
         "(S05B2_ALPHA_ORIGINAL_MODEL_ANIMATION_CATALOG.md). Сгенерировано `d69_catalogs`; результаты Unity — из "
         "сводок пакетов D59–D65 и проверки D69 (`.scratch/d69/verify`).",
         "",
         "## Сводка",
         "",
         "Вердикты: " + ", ".join(f"`{k}` {v}" for k, v in sorted(disp["verdicts"].items())) + ".",
         "",
         "Итог D69: " + ", ".join(f"`{k}` {v}" for k, v in sorted(disp["outcomes"].items())) + ".",
         "",
         "## Не KEEP (всё, что требует действия)",
         "",
         _md_table(["kind", "имя", "что", "оригинал", "Unity", "вердикт", "итог D69", "серьёзность", "причина/исправление", "исполнитель"],
                   [(r["kind"], r["name"], r["item"], r["alpha"], r["unity"], r["verdict"], r["outcome"], r["severity"],
                     r["reason"], r["owner"]) for r in rows if r["verdict"] != "KEEP"]),
         "",
         "## KEEP (перенесено верно)",
         "",
         _md_table(["kind", "имя", "что", "Unity", "итог"],
                   [(r["kind"], r["name"], r["item"], r["unity"], r["outcome"]) for r in rows if r["verdict"] == "KEEP"]),
         ""]
    DOC_B.write_text("\n".join(b), encoding="utf-8")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--no-docs", action="store_true")
    a = ap.parse_args()
    orig = original_catalog()
    disp = disposition(orig, unity_results())
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "alpha_original_catalog.json").write_text(json.dumps(orig, indent=1), encoding="utf-8")
    (OUT / "unity_disposition.json").write_text(json.dumps(disp, indent=1), encoding="utf-8")
    if not a.no_docs:
        write_docs(orig, disp)
    print(f"ORIGINAL kinds={len(orig['kinds'])} roles={collections.Counter(r['alpha_role'] for r in orig['kinds'])}")
    print(f"DISPOSITION rows={len(disp['rows'])} verdicts={disp['verdicts']} outcomes={disp['outcomes']}")


if __name__ == "__main__":
    main()
