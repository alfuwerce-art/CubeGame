#!/usr/bin/env python3
"""Validate the Factoria VOXEL_STYLE workflows with ComfyUI's own prompt validator.

The UI (litegraph) workflow JSON is converted here into ComfyUI's API prompt format
(the same conversion the frontend does when you press Queue) and then checked with
``execution.validate_prompt`` - i.e. the exact code path the server uses before
queueing a prompt. Nothing is sampled and no image is generated: model files are only
listed, not loaded.

Bypassed nodes (mode 4) are resolved through their pass-through input, muted nodes
(mode 2) are dropped - same semantics as the frontend.

Usage:
  python validate_factoria_workflows.py --comfy "D:\\programs\\ComfyUI\\ComfyUI" \
      --workflow-dir "D:\\Projects\\CubeGame Factory\\workflows\\factoria_voxel_style" \
      --report "D:\\Projects\\CubeGame Factory\\measurements\\factoria_voxel_style_validation.json"
"""
from __future__ import annotations

import argparse
import asyncio
import json
import os
import sys

FRONTEND_ONLY = {"Note", "MarkdownNote", "PrimitiveNode", "Reroute"}
EXPECTED_GAPS = {
    # node type -> explanation, used only to explain a FAIL to the owner
    "LoadImage": "style/control reference image is not in ComfyUI/input yet (owner step)",
}


def parse_args():
    ap = argparse.ArgumentParser()
    ap.add_argument("--comfy", required=True, help="ComfyUI source dir containing nodes.py/execution.py")
    ap.add_argument("--workflow-dir", required=True)
    ap.add_argument("--report", required=True)
    ap.add_argument("--substitute-ckpt", default=None,
                    help="validate with this checkpoint name instead of the one stored in the graph "
                         "(used while the intended checkpoint is still downloading)")
    return ap.parse_args()


def ui_to_api(wf: dict) -> tuple[dict, list[str]]:
    nodes = {n["id"]: n for n in wf["nodes"]}
    links = {l[0]: l for l in wf["links"]}
    notes: list[str] = []

    def resolve(node_id: int, slot: int, depth: int = 0):
        if depth > 20:
            raise RuntimeError(f"bypass chain too deep at node {node_id}")
        n = nodes[node_id]
        if n.get("mode") == 4:  # bypass -> pass matching type through
            out_type = n["outputs"][slot]["type"]
            for inp in n["inputs"]:
                if inp["type"] == out_type and inp["link"] is not None:
                    link = links[inp["link"]]
                    return resolve(link[1], link[2], depth + 1)
            raise RuntimeError(f"bypassed node {node_id} ({n['type']}) has no {out_type} pass-through")
        return [str(node_id), slot]

    prompt: dict[str, dict] = {}
    for n in wf["nodes"]:
        if n["type"] in FRONTEND_ONLY or n.get("mode") in (2, 4):
            continue
        inputs = dict(n.get("widgets_values_named") or {})
        inputs.pop("control_after_generate", None)  # frontend-only widget
        for inp in n["inputs"]:
            if inp["link"] is None:
                continue
            link = links[inp["link"]]
            inputs[inp["name"]] = resolve(link[1], link[2])
        prompt[str(n["id"])] = {
            "class_type": n["type"],
            "inputs": inputs,
            "_meta": {"title": n.get("title", n["type"])},
        }
    muted = [f"{n['type']}#{n['id']}" for n in wf["nodes"] if n.get("mode") == 2]
    bypassed = [f"{n['type']}#{n['id']}" for n in wf["nodes"] if n.get("mode") == 4]
    if muted:
        notes.append("muted (not queued): " + ", ".join(muted))
    if bypassed:
        notes.append("bypassed (pass-through): " + ", ".join(bypassed))
    return prompt, notes


def main() -> int:
    args = parse_args()
    # Resolve paths before chdir(COMFY): the validator import step changes the CWD.
    args.report = os.path.abspath(args.report)
    args.workflow_dir = os.path.abspath(args.workflow_dir)
    args.comfy = os.path.abspath(args.comfy)
    workflow_files = sorted(f for f in os.listdir(args.workflow_dir)
                            if (f.startswith("FACTORIA_") or f.startswith("DREAMSHAPER_")
                                or f.startswith("FACTORY_"))
                            and f.endswith(".json"))
    if not workflow_files:
        print("no workflows found")
        return 2

    loaded = {}
    for f in workflow_files:
        with open(os.path.join(args.workflow_dir, f), "r", encoding="utf-8") as fh:
            loaded[f] = json.load(fh)

    # ComfyUI modules parse comfy.cli_args from sys.argv: hide our own arguments.
    argv_backup = sys.argv[:]
    sys.argv = [argv_backup[0]]
    os.chdir(args.comfy)
    sys.path.insert(0, args.comfy)
    import nodes  # noqa: E402
    import execution  # noqa: E402

    asyncio.run(nodes.init_extra_nodes())
    sys.argv = argv_backup

    report = {"workflows": {}, "ckpt_substituted": args.substitute_ckpt}
    rc = 0
    for fname, wf in loaded.items():
        if args.substitute_ckpt:
            # Structural validation only: the intended checkpoint is still downloading.
            for n in wf["nodes"]:
                if n.get("type") == "CheckpointLoaderSimple":
                    named = n.setdefault("widgets_values_named", {})
                    named["ckpt_name"] = args.substitute_ckpt
                    if n.get("widgets_values"):
                        n["widgets_values"][0] = args.substitute_ckpt
        prompt, notes = ui_to_api(wf)
        valid, error, outputs, node_errors = asyncio.run(
            execution.validate_prompt("factoria-validate", prompt, None))
        entry = {
            "status": "PASS" if valid else "FAIL",
            "prompt_nodes": len(prompt),
            "outputs": sorted(outputs),
            "notes": notes,
            "error": error if not valid else None,
            "node_errors": node_errors if not valid else None,
        }
        gate = []
        if not valid and node_errors:
            for nid, nd in node_errors.items():
                ctype = nd.get("class_type", "?")
                gate.append(f"{ctype}#{nid}")
        if gate:
            entry["needs_owner_action"] = sorted(set(gate))
            entry["expected_gap_hint"] = {g: EXPECTED_GAPS.get(g.split("#")[0], "") for g in gate}
        report["workflows"][fname] = entry
        print(f"{entry['status']} {fname}: prompt_nodes={len(prompt)} outputs={sorted(outputs)}")
        for n in notes:
            print(f"    ~ {n}")
        if not valid:
            print(f"    ! {error.get('message') if isinstance(error, dict) else error}")
            for nid, nd in (node_errors or {}).items():
                for e in nd.get("errors", []):
                    print(f"    ! {nd.get('class_type')}#{nid}: {e.get('message')} "
                          f"({e.get('details')})")
            # A FAIL caused only by owner-supplied files is reported, not treated as a build defect.
            needs = {g.split("#")[0] for g in entry.get("needs_owner_action", [])}
            if needs and needs <= set(EXPECTED_GAPS):
                entry["status"] = "PASS_WITH_OWNER_ACTION"
                print("    ~ classified as PASS_WITH_OWNER_ACTION (missing local file only)")
            else:
                rc = 1

    os.makedirs(os.path.dirname(args.report) or ".", exist_ok=True)
    with open(args.report, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(report, fh, ensure_ascii=False, indent=2)
        fh.write("\n")
    print(f"report: {args.report}")
    return rc


if __name__ == "__main__":
    sys.exit(main())
