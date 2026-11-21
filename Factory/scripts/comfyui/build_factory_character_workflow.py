#!/usr/bin/env python3
"""Build the CubeGame Factory character-generation graphs (COMFY-STYLE-05).

Outputs (into --out):
  FACTORY_CHARACTER_GENERATION_V1.json   main graph: SDXL + FICTIVERSE LoRA, orc, 768x1024
  FACTORY_CHARACTER_BASE.json            comparison mode BASE  (no LoRA)
  FACTORY_CHARACTER_FICTIVERSE.json      comparison mode FICTIVERSE (VoxelXL)
  FACTORY_CHARACTER_RAL.json             comparison mode RAL (ral-vxl, experimental licence)
  FACTORY_CHARACTER_IPADAPTER.json       + optional IPAdapter style block (muted by default)
  FACTORY_CHARACTER_CONTROLNET.json      + optional ControlNet pose/silhouette block (bypassed)
  factory_test_matrix_v1.json            run list for factory_run_batch.py

Graph layout follows the task's functional blocks:
  A MODEL -> B CHARACTER DESCRIPTION (text) -> C BODY_STYLE_LOCK / D FACE_STYLE_LOCK (text)
  -> E COMPOSITION (text) -> F SAMPLING -> G OUTPUT (SaveImage with unique prefix)

Usage:
  python build_factory_character_workflow.py \
      --object-info measurements/comfyui_object_info_2026-11-13_full.json \
      --config workflows/factory_characters/characters_v1.json \
      --out workflows/factory_characters \
      --report measurements/factory_characters_build_report.json
"""
from __future__ import annotations

import argparse
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_factoria_voxel_workflows import Graph, load_object_info, _validate_count  # noqa: E402


def assemble_prompt(cfg: dict, character_key: str, mode: str, view: str = "three_quarter") -> str:
    ch = dict(cfg["characters"][character_key])
    ch["camera"] = cfg["views"][view]
    blocks = cfg["blocks"]
    template = cfg["prompt_template"]["with_lora"] if mode != "BASE" else cfg["prompt_template"]["without_lora"]
    trigger = cfg["loras"][mode]["trigger"] if mode != "BASE" else ""
    return template.format(trigger=trigger, BODY_STYLE_LOCK=blocks["BODY_STYLE_LOCK"],
                           FACE_STYLE_LOCK=blocks["FACE_STYLE_LOCK"],
                           COMPOSITION=blocks["COMPOSITION"], **ch)


def build_graph(cfg: dict, oi: dict, mode: str, character: str, view: str,
                ipadapter: bool = False, controlnet: bool = False):
    s = cfg["sampling"]
    positive = assemble_prompt(cfg, character, mode, view)
    negative = cfg["blocks"]["NEGATIVE"]
    strength = 0.6 if mode != "BASE" else None

    mode_note = {
        "BASE": "**MODE BASE** — только DreamShaper XL Turbo, без стилевых LoRA.",
        "FICTIVERSE": "**MODE FICTIVERSE** — DreamShaper XL Turbo + `VoxelXL_v1.safetensors` (trigger `voxel style`). "
                      "Лицензия openrail: коммерческое использование требует отдельной проверки, автоподтверждения нет.",
        "RAL": "**MODE RAL (эксперимент)** — DreamShaper XL Turbo + `ral-vxl.safetensors` (trigger `ral-vxl`). "
               "Лицензия Civitai: `allowCommercialUse=RentCivit`, `allowDerivatives=False` → результаты **нельзя** "
               "использовать коммерчески, обучать на них нельзя, в производственный Tripo-конвейер не передавать.",
    }[mode]

    note = (
        "**FACTORY CHARACTER GENERATION V1**\n\n"
        f"{mode_note}\n\n"
        "Блоки графа:\n"
        "- **A MODEL**: CheckpointLoaderSimple (SDXL Turbo) + LoraLoader (сила отдельно для MODEL и CLIP);\n"
        "- **B CHARACTER**: параметры персонажа в ноде POSITIVE (собираются из "
        "`characters_v1.json`: race/sex/body/face/clothing/armor/accessories/palette/pose/camera);\n"
        "- **C BODY_STYLE_LOCK** и **D FACE_STYLE_LOCK**: два текстовых блока в том же промпте "
        "(независимого регионального управления нет);\n"
        "- **E COMPOSITION**: полный рост, A-pose, 3/4, нейтральный фон, без обрезки;\n"
        f"- **F SAMPLING**: {s['steps']} шагов, CFG {s['cfg']}, {s['sampler_name']}/{s['scheduler']}, "
        "batch 1 (turbo-значения, не универсальные);\n"
        "- **G OUTPUT**: SaveImage с уникальным префиксом + JSON-манифест создаёт `factory_run_batch.py`.\n\n"
        "Смена режима: изменить `lora_name` в ноде **STYLE LoRA** (FICTIVERSE ↔ RAL) или перевести её в BYPASS (BASE).\n"
        "Смена персонажа: править только текст в ноде POSITIVE (блок CHARACTER), стилевые блоки не трогать.\n\n"
        "Лимиты: RAM ≤ 15 GB, дополнительная VRAM ≤ +2 GB; смотреть панель **Crystools**."
    )

    g = Graph(oi)
    g.add("MarkdownNote", {"text": note}, pos=(-1120, 40), title="FACTORY - read me")
    ckpt_n = g.add("CheckpointLoaderSimple", {"ckpt_name": cfg["model"]["checkpoint"]}, pos=(0, 60),
                   title="A. MODEL checkpoint (SDXL Turbo)")

    # A. MODEL: LoRA chain (present in BASE too, but muted there so the graph is comparable)
    lora_n = g.add("LoraLoader",
                   {"lora_name": cfg["loras"][mode]["file"] if mode != "BASE" else cfg["loras"]["FICTIVERSE"]["file"],
                    "strength_model": strength if strength is not None else 1.0,
                    "strength_clip": strength if strength is not None else 1.0},
                   pos=(420, 60), mode=(0 if mode != "BASE" else 4),
                   title=f"STYLE LoRA - {mode}")

    pos_n = g.add("CLIPTextEncode", {"text": positive}, pos=(420, 240),
                  title=f"B/C/D/E. POSITIVE - {character} / {mode}")

    # B. CHARACTER block visibility helper (same text, read-only note for quick editing)
    g.add("MarkdownNote", {
        "text": "**B. CHARACTER (менять только здесь)**\n\n"
                "Поля набора: race/species, sex, body_type, head/face, clothing, armor, "
                "accessories, palette, pose, camera.\n\n"
                "Текущий персонаж: **" + character + "**\n\n"
                "Стилевые блоки **C BODY_STYLE_LOCK** и **D FACE_STYLE_LOCK** находятся в том же промпте — "
                "их не переписывать при смене персонажа."
    }, pos=(-560, 240), title="B. character parameters", size=[500, 220])

    neg_n = g.add("CLIPTextEncode", {"text": negative}, pos=(420, 900), title="NEGATIVE (style + anatomy)")
    lat_n = g.add("EmptyLatentImage", {"width": s["main"]["width"], "height": s["main"]["height"],
                                       "batch_size": s["batch_size"]}, pos=(420, 1320),
                  title=f"F. latent {s['main']['width']}x{s['main']['height']} batch 1")

    model_src = (lora_n, 0)
    clip_src = (lora_n, 1)

    if ipadapter:
        # optional style-reference block, MUTED by default: graph stays valid without a reference
        load_n = g.add("LoadImage", {"image": "factoria_xl_style_reference.png"}, pos=(0, 1500),
                       extra_widgets=["image"], mode=2, title="IPAdapter reference (muted)")
        uni_n = g.add("IPAdapterUnifiedLoader", {"preset": "PLUS (high strength)"}, pos=(0, 1800),
                      mode=4, title="IPAdapter loader (bypass)")
        ipa_n = g.add("IPAdapterAdvanced", {"weight": 0.7, "weight_type": "style transfer",
                                            "combine_embeds": "average", "start_at": 0.0,
                                            "end_at": 1.0, "embeds_scaling": "V only"},
                      pos=(0, 2000), mode=4, title="IPAdapter apply (bypass)")
        g.connect(ckpt_n, 0, uni_n, "model")
        g.connect(uni_n, 0, ipa_n, "model")
        g.connect(uni_n, 1, ipa_n, "ipadapter")
        g.connect(load_n, 0, ipa_n, "image")
        model_src = (ipa_n, 0)

    if controlnet:
        ctl_load_n = g.add("LoadImage", {"image": "factoria_xl_pose_source.png"}, pos=(0, 2300),
                           extra_widgets=["image"], mode=2, title="Control source (muted)")
        canny_n = g.add("Canny", {"low_threshold": 0.4, "high_threshold": 0.8}, pos=(420, 2300),
                        mode=2, title="Canny (muted)")
        line_n = g.add("LineArtPreprocessor", {"coarse": "disable", "resolution": 1024}, pos=(420, 2480),
                       mode=2, title="Lineart (muted)")
        pose_n = g.add("DWPreprocessor", {"detect_hand": "enable", "detect_body": "enable",
                                          "detect_face": "enable", "resolution": 1024,
                                          "bbox_detector": "yolox_l.onnx",
                                          "pose_estimator": "dw-ll_ucoco_384.onnx",
                                          "scale_stick_for_xinsr_cn": "disable"},
                       pos=(420, 2660), mode=2, title="DW-Pose (muted)")
        cn_load_n = g.add("ControlNetLoader", {"control_net_name": "controlnet-union-sdxl-1.0.safetensors"},
                          pos=(420, 2900), mode=2, title="ControlNet union SDXL (muted)")
        cn_apply_n = g.add("ControlNetApplyAdvanced", {"strength": 0.7, "start_percent": 0.0,
                                                       "end_percent": 1.0}, pos=(1000, 700), mode=4,
                           title="ControlNet apply (bypass)")
        g.connect(ctl_load_n, 0, canny_n, "image")
        g.connect(ctl_load_n, 0, line_n, "image")
        g.connect(ctl_load_n, 0, pose_n, "image")
        g.connect(cn_load_n, 0, cn_apply_n, "control_net")
        g.connect(canny_n, 0, cn_apply_n, "image")
        cond_pos_src = (cn_apply_n, 0)
        cond_neg_src = (cn_apply_n, 1)
    else:
        cond_pos_src = (pos_n, 0)
        cond_neg_src = (neg_n, 0)

    ks_n = g.add("KSampler", {"seed": s["seed"], "control_after_generate": "fixed",
                              "steps": s["steps"], "cfg": s["cfg"],
                              "sampler_name": s["sampler_name"], "scheduler": s["scheduler"],
                              "denoise": s["denoise"]}, pos=(1000, 240),
                 title="F. KSampler (turbo: 8 steps / CFG 2)")
    vd_n = g.add("VAEDecode", pos=(1350, 240))
    save_n = g.add("SaveImage", {"filename_prefix": f"factory/{character}_{mode.lower()}"}, pos=(1620, 240),
                   title="G. OUTPUT")

    g.connect(ckpt_n, 0, lora_n, "model")
    g.connect(ckpt_n, 1, lora_n, "clip")
    g.connect(clip_src[0], clip_src[1], pos_n, "clip")
    g.connect(clip_src[0], clip_src[1], neg_n, "clip")
    g.connect(model_src[0], model_src[1], ks_n, "model")
    if controlnet:
        g.connect(pos_n, 0, cn_apply_n, "positive")
        g.connect(neg_n, 0, cn_apply_n, "negative")
    g.connect(cond_pos_src[0], cond_pos_src[1], ks_n, "positive")
    g.connect(cond_neg_src[0], cond_neg_src[1], ks_n, "negative")
    g.connect(lat_n, 0, ks_n, "latent_image")
    g.connect(ks_n, 0, vd_n, "samples")
    g.connect(ckpt_n, 2, vd_n, "vae")
    g.connect(vd_n, 0, save_n, "images")

    suffix = "_ipadapter" if ipadapter else ("_controlnet" if controlnet else "")
    return g.to_workflow(f"factory_character_{mode.lower()}_{character}{suffix}")


def test_matrix(cfg: dict) -> dict:
    runs = []
    seed = cfg["sampling"]["seed"]
    # diagnostic resolution first: cheap connectivity/memory check
    runs.append({"id": "diag_base_512x768", "mode": "BASE", "character": "orc", "view": "three_quarter",
                 "resolution": "diagnostic", "strength": None, "seed": seed, "purpose": "diagnostic"})
    runs.append({"id": "diag_fictiverse06_512x768", "mode": "FICTIVERSE", "character": "orc",
                 "view": "three_quarter", "resolution": "diagnostic", "strength": 0.6, "seed": seed,
                 "purpose": "diagnostic"})
    # main comparison: orc, 768x1024
    runs.append({"id": "orc_base_768x1024", "mode": "BASE", "character": "orc", "view": "three_quarter",
                 "resolution": "main", "strength": None, "seed": seed, "purpose": "comparison"})
    for mode in ("FICTIVERSE", "RAL"):
        for st in cfg["strengths"]:
            key = f"{mode.lower()}{int(round(st * 10))}"
            runs.append({"id": f"orc_{key}_768x1024", "mode": mode, "character": "orc",
                         "view": "three_quarter", "resolution": "main", "strength": st, "seed": seed,
                         "purpose": "comparison"})
    # other races at the primary setting
    runs.append({"id": "human_fictiverse06_768x1024", "mode": "FICTIVERSE", "character": "human",
                 "view": "three_quarter", "resolution": "main", "strength": 0.6, "seed": seed,
                 "purpose": "race_test"})
    runs.append({"id": "elf_fictiverse06_768x1024", "mode": "FICTIVERSE", "character": "elf",
                 "view": "three_quarter", "resolution": "main", "strength": 0.6, "seed": seed,
                 "purpose": "race_test"})
    # 1024x1024 only if VRAM allows - executed last, may be skipped by the runner
    runs.append({"id": "orc_fictiverse06_1024x1024", "mode": "FICTIVERSE", "character": "orc",
                 "view": "three_quarter", "resolution": "hi", "strength": 0.6, "seed": seed,
                 "purpose": "memory_test", "optional": True})
    return {"id": "FACTORY_TEST_MATRIX_V1", "seed_policy": "same seed is NOT proof of equivalent comparison",
            "runs": runs}


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
        "FACTORY_CHARACTER_GENERATION_V1.json": dict(mode="FICTIVERSE", character="orc", view="three_quarter"),
        "FACTORY_CHARACTER_BASE.json": dict(mode="BASE", character="orc", view="three_quarter"),
        "FACTORY_CHARACTER_FICTIVERSE.json": dict(mode="FICTIVERSE", character="orc", view="three_quarter"),
        "FACTORY_CHARACTER_RAL.json": dict(mode="RAL", character="orc", view="three_quarter"),
        "FACTORY_CHARACTER_IPADAPTER.json": dict(mode="FICTIVERSE", character="orc", view="three_quarter",
                                                 ipadapter=True),
        "FACTORY_CHARACTER_CONTROLNET.json": dict(mode="FICTIVERSE", character="orc", view="three_quarter",
                                                  controlnet=True),
    }

    report = {"object_info_source": args.object_info, "config": args.config, "workflows": {}}
    rc = 0
    for fname, kw in targets.items():
        wf = build_graph(cfg, oi, **kw)
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

    matrix = test_matrix(cfg)
    with open(os.path.join(args.out, "factory_test_matrix_v1.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(matrix, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"test matrix runs={len(matrix['runs'])}")

    if args.report:
        os.makedirs(os.path.dirname(os.path.abspath(args.report)), exist_ok=True)
        with open(args.report, "w", encoding="utf-8", newline="\n") as f:
            json.dump(report, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"report: {args.report}")
    return rc


if __name__ == "__main__":
    sys.exit(main())
