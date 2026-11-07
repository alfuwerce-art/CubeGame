"""D65: the two manifests of the mass run as Markdown tables (metadata only, no Alpha data).

Reads the local artifacts of one mass run and prints the model manifest (every route of the catalog,
which models reached Unity, which are Alpha-empty and why) and the animation manifest (per creature kind:
geometry/rig/walk/idle/actions/equipment/Unity verdict with the reason of every non-transferred item).
Everything it reads stays in ``.scratch``; the output contains names, counts and statuses only.

    python -m tools.alpha_models.catalog.report_tables --out .scratch/catalog/report_tables.md
    python -m tools.alpha_models.catalog.report_tables --full-summary .scratch/universal/batch/d63_full/summary.json \
        --actions-summary .scratch/universal/batch/d63_actions/summary.json
"""
from __future__ import annotations

import argparse
import collections
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCRATCH = ROOT / ".scratch"
CATALOG = SCRATCH / "catalog"


def _load(path, default=None):
    p = Path(path)
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else default


def model_manifest(catalog=None, manifest=None, import_state=None):
    """| route | models | imported | alpha-empty | absent | not imported |"""
    cat = _load(catalog or CATALOG / "asset_catalog.json", {})
    man = _load(manifest or SCRATCH / "catalog/unity/AlphaCatalog/Assets/AlphaCatalog/Data/manifest.json", {})
    state = _load(import_state or SCRATCH / "catalog/unity/AlphaCatalog/CatalogOutput/catalog_import.json", {})
    recs = {r["name"]: r for r in (state or {}).get("records", [])}
    routes = collections.Counter(m["route"] for m in cat.get("models", {}).values())
    imported = collections.Counter()
    failed = collections.Counter()
    for r in (state or {}).get("records", []):
        (imported if r.get("verdict") == "PASS" else failed)[r.get("route")] += 1
    empty = collections.Counter(m["route"] for m in (man or {}).get("empty_visible", []))
    absent = collections.Counter(m["route"] for m in (man or {}).get("absent", []))
    out = ["| Маршрут | Моделей | Импортировано в Unity | Alpha-пусто (маркеры) | Нет блоба | Не импортировано |",
           "|---|---|---|---|---|---|"]
    for route in sorted(routes):
        out.append(f"| {route} | {routes[route]} | {imported.get(route, 0)} | {empty.get(route, 0)} | "
                   f"{absent.get(route, 0)} | {failed.get(route, 0)} |")
    out.append(f"| **Всего** | **{sum(routes.values())}** | **{sum(imported.values())}** | "
               f"**{sum(empty.values())}** | **{sum(absent.values())}** | **{sum(failed.values())}** |")
    return "\n".join(out)


def _actions_by_kind(plan):
    """Kind -> (profile, launched actions, condition summary) from the plan's action channel."""
    from tools.alpha_models.universal import contract as ct
    out = {}
    for r in plan.get("entities", []):
        a = dict(r.get("channels", {}).get("actions", {}))
        prof = ct.ACTION_PROFILES.get(a.get("profile") or "", {})
        a["launched"] = list(prof.get("launched_actions") or [])
        a["weapon_conditional"] = list(prof.get("weapon_conditional_actions") or [])
        out[r["kind"]] = a
    return out


def _equipment_by_kind(static_path=None, seed=(0, 0)):
    """Kind -> weapon-layer status of the rig (SUPPORTED / NONE / BLOCKED) from the entity contract."""
    from tools.alpha_models.universal import contract as ct
    table = _load(static_path or SCRATCH / "universal/static/alpha_static.json", None)
    if not table:
        return {}
    out = {}
    for key in table.get("kinds", {}):
        try:
            e = ct.describe(int(key, 16), table["kinds"][key], table, None, seed_variant=seed)
            out[key] = e["channels"]["equipment"]["weapon"]["status"]
        except Exception:
            out[key] = "UNKNOWN"
    return out


def animation_manifest(plan, full_summary=None, actions_summary=None, static_path=None):
    """One row per creature kind: geometry/rig/walk/idle/actions/equipment and the Unity verdict."""
    acts = _actions_by_kind(plan)
    equip = _equipment_by_kind(static_path)
    ent = {r["kind"]: r for r in plan.get("entities", [])}
    full = {r["kind"]: r for r in ((full_summary or {}).get("entries") or [])}
    action_rows = {}
    for r in ((actions_summary or {}).get("entries") or []):
        action_rows.setdefault(r["kind"], []).append(r)
    out = ["| Вид | Имя | Семейство | Части | Ходьба | Покой | Действия ИИ | Экипировка | Unity | Причина / блокер |",
           "|---|---|---|---|---|---|---|---|---|---|"]
    for kind in sorted(ent, key=lambda k: int(k, 16)):
        e = ent[kind]
        ch = e.get("channels", {})
        walk = "+".join(ch.get("walk", {}).get("sources", [])) or "NONE"
        idle = "+".join(ch.get("idle", {}).get("sources", [])) or "NONE"
        a = acts.get(kind, {})
        n = len(a.get("launched") or [])
        act = f"{a.get('status')}"
        if a.get("status") == "AVAILABLE" and n:
            act += f" ({n} без оружия)"
        wc = len(a.get("weapon_conditional") or [])
        if wc:
            act += f" +{wc} только с оружием: BLOCKED"
        if a.get("status") == "AVAILABLE" and action_rows.get(kind):
            done = [x for x in action_rows[kind] if x.get("verdict") == "PASS"]
            act += f" · Unity {len(done)}/{len(action_rows[kind])}"
        f = full.get(kind)
        unity = "—"
        if f:
            unity = "PASS" if f.get("verdict") == "PASS" else f.get("verdict")
            if f.get("verdict") == "PASS" and f.get("live_check") == "NOT_CAPTURED":
                unity += " (formula)"
        equip_status = equip.get(kind, "—") or "—"
        reasons = []
        if ch.get("actions", {}).get("reasons"):
            reasons += ch["actions"]["reasons"]
        if e.get("reasons"):
            reasons += e["reasons"]
        if f and f.get("problems"):
            reasons += f["problems"]
        out.append(f"| {kind} | {e.get('name_hint') or ''} | {(e.get('family_id') or '').split('/')[-1]} | "
                   f"{len(e.get('parts') or [])} | {walk} | {idle} | {act} | {equip_status} | {unity} | "
                   f"{'; '.join(reasons)[:180]} |")
    return "\n".join(out)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--plan", default=str(SCRATCH / "universal/plan_d65/plan.json"))
    ap.add_argument("--full-summary", default=str(SCRATCH / "universal/batch/d63_full/summary.json"))
    ap.add_argument("--actions-summary", default=str(SCRATCH / "universal/batch/d63_actions/summary.json"))
    ap.add_argument("--static", default=str(SCRATCH / "universal/static/alpha_static.json"))
    ap.add_argument("--out", default=str(SCRATCH / "catalog/report_tables.md"))
    a = ap.parse_args(argv)
    plan = _load(a.plan, {"entities": []})
    full = _load(a.full_summary, None)
    actions = _load(a.actions_summary, None)
    text = "\n\n".join(["## Модельный манифест", model_manifest(),
                        "## Анимационный манифест", animation_manifest(plan, full, actions, a.static)])
    out = Path(a.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(text + "\n", encoding="utf-8")
    print(json.dumps({"REPORT_TABLES": "DONE", "kinds": len(plan.get("entities", [])),
                      "full_entries": len((full or {}).get("entries") or []),
                      "action_entries": len((actions or {}).get("entries") or []), "out": str(out)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
