#!/usr/bin/env python3
"""Build the COMFY-STYLE-06 workflows: SDXL Base 1.0 + Fictiverse Voxel XL.

Outputs (into --out):
  FACTORY_CHARACTER_SDXL_BASE_V2.json        long, structured character prompt (CFG 6 / 28 steps)
  FACTORY_CHARACTER_SDXL_BASE_V2_DIAG.json   short diagnostic prompt (one orc, no lists)
  factory_test_matrix_v2.json                sampler / CFG / LoRA-strength sweep

Why a separate stage: COMFY-STYLE-05 proved that on SDXL-Turbo (CFG 2.0) the Fictiverse
LoRA has no visible effect. Here the same style block runs on plain SDXL Base 1.0 at
CFG 5-7 and 25-30 steps, where the LoRA is supposed to work.

Usage:
  python build_factory_v2_workflow.py \
      --object-info measurements/comfyui_object_info_2026-11-14_full.json \
      --config workflows/factory_characters/sdxl_base_v2.json \
      --out workflows/factory_characters \
      --report measurements/factory_v2_build_report.json
"""
from __future__ import annotations

import argparse
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_factoria_voxel_workflows import Graph, load_object_info, _validate_count  # noqa: E402


def long_prompt(cfg: dict) -> str:
    ch = cfg["character_prompt"]
    blocks = cfg["blocks"]
    return cfg["prompt_long_template"].format(
        BODY_STYLE_LOCK=blocks["BODY_STYLE_LOCK"],
        FACE_STYLE_LOCK=blocks["FACE_STYLE_LOCK"],
        COMPOSITION=blocks["COMPOSITION"],
        race_species=ch["race_species"],
        body_type=ch["body_type"],
        head_face=ch["head_face"],
        clothing_armor=ch["clothing_armor"],
        palette=ch["palette"],
        pose=ch["pose"],
        camera=ch["camera"],
    )


def build(cfg: dict, oi: dict, short: bool):
    s = cfg["sampling_sweep"]
    res = cfg["resolution"]["diagnostic" if short else "main"]
    positive = cfg["diagnostic_prompt"]["positive"] if short else long_prompt(cfg)
    negative = cfg["diagnostic_prompt"]["negative"] if short else cfg["blocks"]["NEGATIVE"]
    title_suffix = "DIAGNOSTIC (short prompt)" if short else "CHARACTER (structured prompt)"

    note = (
        f"**SDXL BASE 1.0 + Fictiverse Voxel XL — {title_suffix}**\n\n"
        f"Checkpoint `{cfg['model']['checkpoint']}` (openrail++, SHA-256 `31e35c80…7e5b`), "
        f"LoRA `{cfg['style_module']['file']}` (trigger `{cfg['style_module']['trigger']}`).\n\n"
        f"Сэмплинг по умолчанию: **{s['steps_default']} шагов, CFG {s['cfg_default']}**, "
        f"`{s['samplers'][0]['sampler_name']}` / `{s['samplers'][0]['scheduler']}`, seed `{s['seed']}`.\n"
        "Диапазон проверки: steps 25–30, CFG 5–7, LoRA 0.4 / 0.6 / 0.8 (см. `factory_test_matrix_v2.json`).\n\n"
        + ("**Короткий диагностический промпт** — намеренно без длинных списков: проверяем, "
           "не появляются ли несколько фигур/раскладка «character sheet».\n\n"
           if short else
           "**Структурный промпт**: BODY_STYLE_LOCK + FACE_STYLE_LOCK + описание персонажа + COMPOSITION "
           "одним текстом; стилевые блоки при смене персонажа не переписывать.\n\n")
        + "**RAL LoRA (`ral-vxl`) здесь НЕ используется** — лицензия запрещает производственное "
          "использование и производные (FACT-D21).\n\n"
          "Лимиты: RAM ≤ 15 ГБ; VRAM — рабочий режим 768×1024 (1024×1024 на 8 ГБ не проверяется).\n"
          "Ресурсы: панель **Crystools**."
    )

    g = Graph(oi)
    g.add("MarkdownNote", {"text": note}, pos=(-1100, 40), title="FACTORY SDXL BASE V2 - read me")
    ckpt_n = g.add("CheckpointLoaderSimple", {"ckpt_name": cfg["model"]["checkpoint"]}, pos=(0, 60),
                   title="A. MODEL - SDXL Base 1.0")
    lora_n = g.add("LoraLoader", {"lora_name": cfg["style_module"]["file"],
                                  "strength_model": 0.6, "strength_clip": 0.6}, pos=(420, 60),
                   title="STYLE LoRA - Fictiverse VoxelXL (strength editable)")
    pos_n = g.add("CLIPTextEncode", {"text": positive}, pos=(420, 240),
                  title=f"B/C/D/E. POSITIVE - {title_suffix}")
    neg_n = g.add("CLIPTextEncode", {"text": negative}, pos=(420, 900),
                  title="NEGATIVE (sheet/duplicates/style + anatomy)")
    lat_n = g.add("EmptyLatentImage", {"width": res["width"], "height": res["height"],
                                       "batch_size": s["batch_size"]}, pos=(420, 1320),
                  title=f"F. latent {res['width']}x{res['height']} batch 1")
    ks_n = g.add("KSampler", {"seed": s["seed"], "control_after_generate": "fixed",
                              "steps": s["steps_default"], "cfg": s["cfg_default"],
                              "sampler_name": s["samplers"][0]["sampler_name"],
                              "scheduler": s["samplers"][0]["scheduler"],
                              "denoise": s["denoise"]}, pos=(1000, 240),
                 title="F. KSampler (28 steps / CFG 6)")
    vd_n = g.add("VAEDecode", pos=(1350, 240))
    save_n = g.add("SaveImage", {"filename_prefix": f"factory/v2_{'diag' if short else 'char'}"},
                   pos=(1620, 240), title="G. OUTPUT")

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
    return g.to_workflow(f"factory_v2_{'diag' if short else 'char'}")


def matrix(cfg: dict) -> dict:
    s = cfg["sampling_sweep"]
    base = {"character": "orc", "prompt_mode": "long", "purpose": "v2"}
    runs = []

    def add(rid, **kw):
        run = dict(base)
        run.update(kw)
        run["id"] = rid
        runs.append(run)

    # phase 1: sampler/scheduler on default CFG and LoRA strength
    for smp in s["samplers"]:
        add(f"v2_smp_{smp['sampler_name']}",
            workflow="FACTORY_CHARACTER_SDXL_BASE_V2.json",
            sampler=smp["sampler_name"], scheduler=smp["scheduler"],
            steps=s["steps_default"], cfg=s["cfg_default"], strength=0.6, seed=s["seed"],
            resolution="main")
    # phase 2: CFG sweep at the first sampler (dpmpp_2m/karras)
    for cfgv in s["cfg"]:
        add(f"v2_cfg{int(cfgv)}",
            workflow="FACTORY_CHARACTER_SDXL_BASE_V2.json",
            sampler=s["samplers"][0]["sampler_name"], scheduler=s["samplers"][0]["scheduler"],
            steps=s["steps_default"], cfg=cfgv, strength=0.6, seed=s["seed"], resolution="main")
    # phase 3: LoRA strength sweep
    for st in cfg["style_module"]["strengths"]:
        add(f"v2_lora{int(round(st * 10))}",
            workflow="FACTORY_CHARACTER_SDXL_BASE_V2.json",
            sampler=s["samplers"][0]["sampler_name"], scheduler=s["samplers"][0]["scheduler"],
            steps=s["steps_default"], cfg=s["cfg_default"], strength=st, seed=s["seed"],
            resolution="main")
    # phase 4: short diagnostic prompt vs long structured prompt
    add("v2_diag_short", workflow="FACTORY_CHARACTER_SDXL_BASE_V2_DIAG.json", prompt_mode="short",
        sampler=s["samplers"][0]["sampler_name"], scheduler=s["samplers"][0]["scheduler"],
        steps=s["steps_default"], cfg=s["cfg_default"], strength=0.6, seed=s["seed"],
        resolution="main", diagnostic=True)
    return {"id": "FACTORY_TEST_MATRIX_V2", "runs": runs}


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

    targets = {
        "FACTORY_CHARACTER_SDXL_BASE_V2.json": False,
        "FACTORY_CHARACTER_SDXL_BASE_V2_DIAG.json": True,
    }
    report = {"object_info_source": args.object_info, "config": args.config, "workflows": {}}
    rc = 0
    for fname, short in targets.items():
        wf = build(cfg, oi, short)
        problems, warnings = _validate_count(wf, oi)
        with open(os.path.join(args.out, fname), "w", encoding="utf-8", newline="\n") as f:
            json.dump(wf, f, ensure_ascii=False, indent=2)
            f.write("\n")
        status = "PASS" if not problems else "FAIL"
        if problems:
            rc = 1
        report["workflows"][fname] = {"status": status, "nodes": len(wf["nodes"]),
                                      "links": len(wf["links"]), "problems": problems,
                                      "warnings": warnings}
        print(f"{status} {fname}: nodes={len(wf['nodes'])} links={len(wf['links'])}")
        for p in problems:
            print(f"    ! {p}")
        for w in warnings:
            print(f"    ~ {w}")

    m = matrix(cfg)
    with open(os.path.join(args.out, "factory_test_matrix_v2.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(m, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"test matrix runs={len(m['runs'])}")

    if args.report:
        os.makedirs(os.path.dirname(os.path.abspath(args.report)), exist_ok=True)
        with open(args.report, "w", encoding="utf-8", newline="\n") as f:
            json.dump(report, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"report: {args.report}")
    return rc


if __name__ == "__main__":
    sys.exit(main())
