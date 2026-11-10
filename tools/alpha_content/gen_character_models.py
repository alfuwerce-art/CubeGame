"""M0-F2A: hero appearance + item model tables for the Unity runtime, and local staging of their models.

Two evidence sources, both produced from the local Alpha copy by already-reviewed tools (nothing new is
decided here, the script only re-encodes their output):

* ``.scratch/catalog/asset_catalog.json`` (D64 ``catalog/asset_catalog.py``): registry index -> model name and
  the load flag of its loader call site. The hero editor stores face/haircut as an index inside the
  per-species registry range of switch table 0x42C41C (C1, ``AlphaHeroCreation``), so registry index ->
  model name is the missing step between the saved appearance and a drawable head/hair.
* ``.scratch/catalog/item_models.json`` (M0-00B ``catalog/item_models.py``): the executed selector
  ``GameController_item_icon_id`` 0x4EC400 over type/subtype/material(+0x0D)/b0c(+0x0C) and the seed
  period. One leaf = one rule; ``models[seed % period]`` is the drawn model.

Outputs:

* ``Assets/CubeGame/Content/Generated/AlphaCharacterModels.g.cs`` — names and numbers only (no geometry).
* ``--stage``: decoded payloads (``.cubdec``) of every referenced model into the local content cache
  (``.scratch/content/appearance`` and ``.scratch/content/items``). The cache is git-ignored; Alpha
  assets are never committed.

    python -m tools.alpha_content.gen_character_models            # regenerate the C# table
    python -m tools.alpha_content.gen_character_models --stage    # + stage the models locally
"""

from __future__ import annotations

import argparse
import json
import sqlite3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSET_CATALOG = ROOT / ".scratch/catalog/asset_catalog.json"
ITEM_MODELS = ROOT / ".scratch/catalog/item_models.json"
ALPHA_STATIC = ROOT / ".scratch/catalog/alpha_static.json"
APPEARANCE_FIELDS = ("scale_head", "scale_body", "scale_hand", "scale_foot", "scale_weapon")
APPEARANCE_VECTORS = ("offset_body", "offset_head", "offset_hand", "offset_foot", "size")
DEFAULT_DB = ROOT / ".scratch/alphatest2/data1.db"
OUT_CS = ROOT / "Assets/CubeGame/Content/Generated/AlphaCharacterModels.g.cs"

# C1 switch table 0x42C41C (copy of AlphaHeroCreation.Models; checked against the C# file below).
SPECIES_RANGES = {
    0x00: (0x4D4, 0x4D7, 0x500, 0x509), 0x01: (0x4D8, 0x4DD, 0x50A, 0x513),
    0x02: (0x4DE, 0x4E3, 0x4E4, 0x4F2), 0x03: (0x4F3, 0x4F8, 0x4F9, 0x4FF),
    0x04: (0x04B, 0x04F, 0x050, 0x055), 0x05: (0x056, 0x05A, 0x05B, 0x060),
    0x07: (0x062, 0x063, 0x064, 0x069), 0x08: (0x06A, 0x06E, 0x064, 0x069),
    0x09: (0x11A, 0x11E, 0x11F, 0x121), 0x0A: (0x122, 0x126, 0x127, 0x12B),
    0x0B: (0x514, 0x518, 0x51E, 0x527), 0x0C: (0x519, 0x51D, 0x528, 0x52B),
    0x0D: (0x52C, 0x530, 0x531, 0x535), 0x0E: (0x536, 0x539, 0x53A, 0x53D),
    0x0F: (0x12F, 0x134, 0x135, 0x13A), 0x10: (0x13B, 0x140, 0x141, 0x146),
}
DOMAIN = {"subtype": 64, "material": 64, "b0c": 8}


def _values(v, field):
    if v is None:
        return list(range(DOMAIN[field]))
    if isinstance(v, str):
        a, b = v.split("..")
        return list(range(int(a), int(b) + 1))
    return list(v)


def _mask(values):
    m = 0
    for x in values:
        m |= 1 << x
    return m


def registry_table():
    catalog = json.loads(ASSET_CATALOG.read_text(encoding="utf-8"))
    by_index = {}
    for name, rec in catalog["models"].items():
        for loader in rec.get("loaders", []):
            if loader.get("kind") == "registry":
                by_index.setdefault(loader["index"], set()).add((name, loader.get("load_flag", 1)))
    rows, problems = {}, []
    for kind, (h0, h1, r0, r1) in SPECIES_RANGES.items():
        for idx in list(range(h0, h1 + 1)) + list(range(r0, r1 + 1)):
            found = by_index.get(idx, set())
            if len(found) != 1:
                problems.append(f"registry 0x{idx:X} kind 0x{kind:02X}: {sorted(found)}")
                continue
            rows[idx] = next(iter(found))
    return catalog, rows, problems


def selector_rules():
    data = json.loads(ITEM_MODELS.read_text(encoding="utf-8"))
    names = {int(k): v["name"] or "" for k, v in data["models"].items()}  # "" = registry slot without a model
    rules = []
    for t, tdef in sorted(data["types"].items(), key=lambda kv: int(kv[0])):
        for leaf in tdef["selectors"]:
            models = [names[m] for m in leaf["models"]]
            period = leaf.get("seed_period") or 1
            if len(models) != period:
                raise SystemExit(f"type {t}: {len(models)} models for period {period}")
            for row in leaf["when"]:
                types = _values(row.get("type"), "subtype")
                for ty in types:
                    rules.append((ty, _mask(_values(row.get("subtype"), "subtype")),
                                  _mask(_values(row.get("material"), "material")),
                                  _mask(_values(row.get("b0c"), "b0c")), models))
    return data, rules


def species_appearance():
    """Per-species appearance block (creature+0x78, alpha_static.py, ALPHA_CODE_STATIC) of the 16 playable kinds."""
    data = json.loads(ALPHA_STATIC.read_text(encoding="utf-8"))
    rows = {}
    for kind in SPECIES_RANGES:
        rec = data["kinds"][f"0x{kind:02x}"]
        if rec.get("uses_rand"):
            raise SystemExit(f"kind 0x{kind:02x}: appearance uses rand(), not a constant")
        rows[kind] = rec["appearance"]
    return data, rows


def _f(x):
    return f"{float(x)!r}f"


def write_cs(catalog, registry, item_data, rules, static_data, species):
    model_names = []
    index = {}

    def ref(n):
        if n not in index:
            index[n] = len(model_names)
            model_names.append(n)
        return index[n]

    reg_lines = [f"            {{ 0x{i:03X}, new AlphaAppearanceModelDef(0x{i:03X}, \"{n}\", {str(flag == 0).lower()}) }},"
                 for i, (n, flag) in sorted(registry.items())]
    rule_lines = []
    for ty, sub, mat, b0c, models in rules:
        ids = ", ".join(str(ref(m)) for m in models)
        rule_lines.append(f"            new AlphaItemModelRule({ty}, 0x{sub:016X}UL, 0x{mat:016X}UL, 0x{b0c:02X}, new[] {{ {ids} }}),")
    species_lines = []
    for kind, ap in sorted(species.items()):
        scalars = ", ".join(_f(ap[f]) for f in APPEARANCE_FIELDS)
        vectors = ", ".join("new[] { " + ", ".join(_f(v) for v in ap[f]) + " }" for f in APPEARANCE_VECTORS)
        species_lines.append(f"            {{ 0x{kind:02X}, new AlphaSpeciesAppearanceDef(0x{kind:02X}, {ap['flags']}, {scalars}, {vectors}) }},")
    name_lines = []
    for i in range(0, len(model_names), 4):
        name_lines.append("            " + " ".join(f"\"{n}\"," for n in model_names[i:i + 4]))
    text = f"""// <auto-generated> tools/alpha_content/gen_character_models.py — do not edit by hand.
// Names and numbers only; geometry stays in the local content cache (never committed).
// Sources: asset catalog exe {catalog['exe_sha256'][:16]} db {catalog['db_sha256'][:16]};
//          {item_data['format']} selector {item_data['selector_va']} runs {item_data['metrics']['runs']} errors {item_data['metrics']['errors']}.
using System.Collections.Generic;

namespace CubeGame.Content
{{
    /// <summary>One head/hair model of the Alpha registry (index → model, load flag 0 = marker voxels transparent).</summary>
    public readonly struct AlphaAppearanceModelDef
    {{
        public readonly int RegistryIndex;
        public readonly string Model;
        public readonly bool MarkerTransparent;

        public AlphaAppearanceModelDef(int registryIndex, string model, bool markerTransparent)
        {{
            RegistryIndex = registryIndex;
            Model = model;
            MarkerTransparent = markerTransparent;
        }}
    }}

    /// <summary>
    /// Appearance block of a species (creature+0x78; alpha_static.py, ALPHA_CODE_STATIC): Creature_drawModel scales
    /// the head block by ScaleHead, the weapon by ScaleWeapon, and places parts with the offsets.
    /// </summary>
    public readonly struct AlphaSpeciesAppearanceDef
    {{
        public readonly int Kind;
        public readonly int Flags;
        public readonly float ScaleHead, ScaleBody, ScaleHand, ScaleFoot, ScaleWeapon;
        public readonly float[] OffsetBody, OffsetHead, OffsetHand, OffsetFoot, Size;

        public AlphaSpeciesAppearanceDef(int kind, int flags, float scaleHead, float scaleBody, float scaleHand, float scaleFoot,
            float scaleWeapon, float[] offsetBody, float[] offsetHead, float[] offsetHand, float[] offsetFoot, float[] size)
        {{
            Kind = kind;
            Flags = flags;
            ScaleHead = scaleHead;
            ScaleBody = scaleBody;
            ScaleHand = scaleHand;
            ScaleFoot = scaleFoot;
            ScaleWeapon = scaleWeapon;
            OffsetBody = offsetBody;
            OffsetHead = offsetHead;
            OffsetHand = offsetHand;
            OffsetFoot = offsetFoot;
            Size = size;
        }}
    }}

    /// <summary>One leaf of the executed item_icon_id selector: masks over subtype/material/b0c, models per seed.</summary>
    public readonly struct AlphaItemModelRule
    {{
        public readonly int Type;
        public readonly ulong Subtypes;
        public readonly ulong Materials;
        public readonly byte Rarities;
        public readonly int[] Models;

        public AlphaItemModelRule(int type, ulong subtypes, ulong materials, byte rarities, int[] models)
        {{
            Type = type;
            Subtypes = subtypes;
            Materials = materials;
            Rarities = rarities;
            Models = models;
        }}
    }}

    public static partial class AlphaCharacterModels
    {{
        public const string AppearanceEvidence =
            "asset_catalog registry loaders (ALPHA_CODE_STATIC) x C1 switch table 0x42C41C";

        public const string ItemSelectorEvidence =
            "{item_data['format']} executed GameController_item_icon_id {item_data['selector_va']} (EMULATED_ALPHA_CODE)";

        public static readonly Dictionary<int, AlphaAppearanceModelDef> AppearanceModels = new Dictionary<int, AlphaAppearanceModelDef>
        {{
{chr(10).join(reg_lines)}
        }};

        public const string SpeciesEvidence =
            "alpha_static {static_data['format']} exe {static_data['exe_sha256'][:16]}: creature appearance block +0x24..+0xA0 (ALPHA_CODE_STATIC)";

        public static readonly Dictionary<int, AlphaSpeciesAppearanceDef> SpeciesAppearance = new Dictionary<int, AlphaSpeciesAppearanceDef>
        {{
{chr(10).join(species_lines)}
        }};

        public static readonly string[] ItemModelNames =
        {{
{chr(10).join(name_lines)}
        }};

        public static readonly AlphaItemModelRule[] ItemModelRules =
        {{
{chr(10).join(rule_lines)}
        }};
    }}
}}
"""
    OUT_CS.write_text(text, encoding="utf-8", newline="\n")
    return model_names


def stage(names, out, db):
    from tools.alpha_models.cub_format import deobfuscate, parse_decoded

    out.mkdir(parents=True, exist_ok=True)
    written, missing, problems = 0, [], []
    con = sqlite3.connect(f"file:{db}?mode=ro&immutable=1", uri=True)
    try:
        for name in names:
            if not name:
                continue
            target = out / (Path(name).stem + ".cubdec")
            row = con.execute("select value from blobs where key=?", (name,)).fetchone()
            if row is None:
                missing.append(name)
                continue
            try:
                decoded = deobfuscate(row[0])
                parse_decoded(decoded)
            except Exception as exc:  # noqa: BLE001 - report the converter failure as is
                problems.append(f"{name}: {exc}")
                continue
            if not target.exists() or target.read_bytes() != decoded:
                target.write_bytes(decoded)
            written += 1
    finally:
        con.close()
    return written, missing, problems


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--stage", action="store_true")
    ap.add_argument("--db", default=str(DEFAULT_DB))
    ap.add_argument("--content", default=str(ROOT / ".scratch/content"))
    a = ap.parse_args(argv)

    cs = (ROOT / "Assets/CubeGame/Features/Session/AlphaHeroCreation.cs").read_text(encoding="utf-8")
    for kind, (h0, h1, r0, r1) in SPECIES_RANGES.items():
        row = f"{{ 0x{kind:02X}, (0x{h0:03X}, 0x{h1:03X}, 0x{r0:03X}, 0x{r1:03X}) }}"
        if row not in cs:
            print(f"GEN_CHARACTER_MODELS=FAIL range table drifted from AlphaHeroCreation: {row}")
            return 1

    catalog, registry, problems = registry_table()
    item_data, rules = selector_rules()
    static_data, species = species_appearance()
    item_names = write_cs(catalog, registry, item_data, rules, static_data, species)
    for p in problems:
        print("  REGISTRY_PROBLEM", p)
    print(f"  appearance models={len(registry)} item rules={len(rules)} item models={len(item_names)} -> {OUT_CS}")
    if a.stage:
        content = Path(a.content)
        w1, m1, p1 = stage(sorted({n for n, _ in registry.values()}), content / "appearance", Path(a.db))
        w2, m2, p2 = stage(item_names, content / "items", Path(a.db))
        for n in m1 + m2:
            print("  ABSENT_IN_DB", n)
        for p in p1 + p2:
            print("  PROBLEM", p)
        print(f"  staged appearance={w1} items={w2}")
        problems += p1 + p2
    verdict = "PASS" if not problems else "FAIL"
    print(f"GEN_CHARACTER_MODELS={verdict}")
    return 0 if verdict == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
