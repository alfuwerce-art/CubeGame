# M0-00 transfer preparation tools

Two deterministic tools support the M0-00 preparation package:

## 1. Confirmed-table registry → C# constants

`data/alpha_confirmed_tables.json` is the evidence data distilled from
`docs/research/alpha2013`. It contains identifiers and documented values for items,
weapon subtypes, 13 equipment slots, 16 abilities and their costs, starter kits, regions,
factions, movement constants, spawn parameters, quest types and combat constants.

```powershell
python tools/m0_transfer/gen_alpha_tables.py           # regenerate
python tools/m0_transfer/gen_alpha_tables.py --check   # fail if the C# file is stale
```

The generator validates uniqueness/ranges, writes
`tools/m0_pilots/Generated/AlphaTables.g.cs` and embeds a canonical SHA-256 of the JSON.
It does not contain game logic. Changing an Alpha value requires changing the JSON
evidence table (with a source citation) and regenerating the C# constants.

## 2. Coverage matrix → machine-readable registry

`build_transfer_registry.py` extracts all 176 mechanic rows from
`docs/research/alpha2013/M0_00B_ALPHA_GAMEPLAY_COVERAGE_MATRIX.md` and writes
`registry/transfer_registry.json` with id, domain, confidence, implementation status and
M0 substage. The source matrix remains the evidence document.

```powershell
python tools/m0_transfer/build_transfer_registry.py
python tools/m0_transfer/build_transfer_registry.py --check
```

Domains cover the seven requested systems plus a cross-cutting `content_ui` bucket
(UI/content rows that belong to M0-08/M0-09 rather than to one of the seven gameplay
systems). Counts are printed by the tool and recorded in the M0-00 report.

## Boundary

These tools read repository documents and write only under `tools/`. They do not read or
modify Alpha files, do not start Unity, and do not authorise M0-01+.
