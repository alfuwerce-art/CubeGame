#!/usr/bin/env python3
"""Build the Civitai "Voxel Style [SDXL]" workflows (COMFY-STYLE-04B).

Outputs (into --out):
  DREAMSHAPER_XL_CIVITAI_VOXEL.json         Orc (canonical first test)
  DREAMSHAPER_XL_CIVITAI_VOXEL_HUMAN.json   comparative race test
  DREAMSHAPER_XL_CIVITAI_VOXEL_ELF.json     comparative race test

Every graph: CheckpointLoaderSimple (DreamShaper XL Turbo) -> LoraLoader (ral-vxl, strength
0.6, editable) -> CLIPTextEncode(+) / (-) -> EmptyLatentImage 768x1024 -> KSampler (8 steps,
CFG 2.0) -> VAEDecode -> SaveImage. Prompts are assembled from prompt_voxel_face_lock_v1.json:
BODY_STYLE_LOCK + FACE_STYLE_LOCK + race specifics, so the comparative test only changes the
race block.

Usage:
  python build_civitai_voxel_workflows.py \
      --object-info measurements/comfyui_object_info_2026-11-13_full.json \
      --config workflows/dreamshaper_xl/prompt_voxel_face_lock_v1.json \
      --out workflows/dreamshaper_xl \
      --report measurements/civitai_voxel_build_report.json
"""
from __future__ import annotations

import argparse
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_factoria_voxel_workflows import Graph, load_object_info, _validate_count  # noqa: E402

FILE_NAMES = {
    "orc": "DREAMSHAPER_XL_CIVITAI_VOXEL.json",
    "human": "DREAMSHAPER_XL_CIVITAI_VOXEL_HUMAN.json",
    "elf": "DREAMSHAPER_XL_CIVITAI_VOXEL_ELF.json",
}


def build_one(race_key: str, cfg: dict, oi: dict):
    gd = cfg["generation_defaults"]
    blocks = cfg["blocks"]
    race = cfg["races"][race_key]
    positive = cfg["prompt_template"].format(
        trigger=cfg["model"]["trigger"],
        BODY_STYLE_LOCK=blocks["BODY_STYLE_LOCK"],
        FACE_STYLE_LOCK=blocks["FACE_STYLE_LOCK"],
        race_specifics=race["race_specifics"],
    )
    negative = cfg["negative_prompt"]
    strength = cfg["model"]["lora_strength_default"]

    note = (
        "**Voxel Style [SDXL] + DreamShaper XL Turbo — " + race["label"] + "**\n\n"
        f"LoRA `{cfg['model']['lora']}` (Civitai 461862, автор RalFinger), триггер **{cfg['model']['trigger']}**.\n"
        f"Сила LoRA по умолчанию **{strength}**; сравнительные значения "
        f"{', '.join(str(x) for x in cfg['model']['lora_strength_alternatives'])} "
        "(меняется в ноде **Voxel Style LoRA (ral-vxl)**).\n\n"
        f"Turbo-настройки: **{gd['steps']} шагов, CFG {gd['cfg']}**, "
        f"{gd['width']}x{gd['height']}, batch 1.\n\n"
        "**ЛИЦЕНЗИЯ (важно):** Civitai API для этой версии — `allowCommercialUse=RentCivit`, "
        "`allowDerivatives=False`, `allowNoCredit=False`. Коммерческое использование "
        "сгенерированных изображений автором **не разрешено явно** → LoRA годится для "
        "экспериментов, но **не** для производства без письменного разрешения автора "
        "или замены на собственную style-LoRA / IPAdapter-референс.\n\n"
        "**Стиль:** BODY_STYLE_LOCK и FACE_STYLE_LOCK — два блока одного промпта "
        "(независимого управления стилями пока нет).\n\n"
        "Сравнительный тест: орк → человек → эльф с тем же seed и той же силой LoRA "
        "(файлы `..._HUMAN.json`, `..._ELF.json`).\n"
        "Ресурсы: панель **Crystools**; лимиты этапа RAM ≤ 15 GB, VRAM ≤ +2 GB."
    )

    g = Graph(oi)
    g.add("MarkdownNote", {"text": note}, pos=(-1080, 40), title="FACTORIA XL - read me (Civitai voxel)")
    ckpt_n = g.add("CheckpointLoaderSimple", {"ckpt_name": cfg["model"]["checkpoint"]}, pos=(0, 60),
                   title="CHECKPOINT DreamShaper XL Turbo v2.1")
    lora_n = g.add("LoraLoader", {"lora_name": cfg["model"]["lora"],
                                  "strength_model": strength, "strength_clip": strength},
                   pos=(420, 60), title="Voxel Style LoRA (ral-vxl) - strength editable")
    pos_n = g.add("CLIPTextEncode", {"text": positive}, pos=(420, 260),
                  title=f"POSITIVE - {race_key} (body + face style lock)")
    neg_n = g.add("CLIPTextEncode", {"text": negative}, pos=(420, 900), title="NEGATIVE")
    lat_n = g.add("EmptyLatentImage", {"width": gd["width"], "height": gd["height"],
                                       "batch_size": gd["batch_size"]}, pos=(420, 1300),
                  title=f"{gd['width']}x{gd['height']} batch 1")
    ks_n = g.add("KSampler", {"seed": gd["seed"], "control_after_generate": "fixed",
                              "steps": gd["steps"], "cfg": gd["cfg"],
                              "sampler_name": gd["sampler_name"],
                              "scheduler": gd["scheduler"], "denoise": gd["denoise"]},
                 pos=(1000, 260), title="KSampler (turbo: 8 steps / CFG 2)")
    vd_n = g.add("VAEDecode", pos=(1350, 260))
    save_n = g.add("SaveImage", {"filename_prefix": race["output_prefix"]}, pos=(1620, 260))

    g.connect(ckpt_n, 0, lora_n, "model")
    g.connect(ckpt_n, 1, lora_n, "clip")
    g.connect(lora_n, 1, pos_n, "clip")
    g.connect(lora_n, 1, neg_n, "clip")
    g.connect(lora_n, 0, ks_n, "model")
    g.connect(pos_n, 0, ks_n, "positive")
    g.connect(neg_n, 0, ks_n, "negative")
    g.connect(lat_n, 0, ks_n, "latent_image")
    g.connect(ks_n, 0, vd_n, "samples")
    g.connect(ckpt_n, 2, vd_n, "vae")
    g.connect(vd_n, 0, save_n, "images")

    wf = g.to_workflow(f"civitai_voxel_{race_key}")
    return wf, positive


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--object-info", required=True)
    ap.add_argument("--config", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--report", default=None)
    args = ap.parse_args()

    oi = load_object_info(args.object_info)
    with open(args.config, "r", encoding="utf-8") as f:
        cfg = json.load(f)

    os.makedirs(args.out, exist_ok=True)
    report = {"object_info_source": args.object_info, "config": args.config, "workflows": {}}
    rc = 0
    for race_key, fname in FILE_NAMES.items():
        wf, positive = build_one(race_key, cfg, oi)
        problems, warnings = _validate_count(wf, oi)
        path = os.path.join(args.out, fname)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(wf, f, ensure_ascii=False, indent=2)
            f.write("\n")
        status = "PASS" if not problems else "FAIL"
        if problems:
            rc = 1
        report["workflows"][fname] = {
            "status": status, "nodes": len(wf["nodes"]), "links": len(wf["links"]),
            "problems": problems, "warnings": warnings,
            "positive_prompt_chars": len(positive),
        }
        print(f"{status} {fname}: nodes={len(wf['nodes'])} links={len(wf['links'])} "
              f"prompt_chars={len(positive)}")
        for p in problems:
            print(f"    ! {p}")
        for w in warnings:
            print(f"    ~ {w}")

    if args.report:
        os.makedirs(os.path.dirname(os.path.abspath(args.report)), exist_ok=True)
        with open(args.report, "w", encoding="utf-8", newline="\n") as f:
            json.dump(report, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"report: {args.report}")
    return rc


if __name__ == "__main__":
    sys.exit(main())
