#!/usr/bin/env python3
"""Build the Factoria DREAMSHAPER XL workflows (COMFY-STYLE-04).

Outputs:
  DREAMSHAPER_XL_BASE.json             SDXL txt2img, no style modules
  DREAMSHAPER_XL_PIXEL_STYLE.json      + VoxelXL LoRA ("voxel style")
  DREAMSHAPER_XL_REFERENCE_LOCK.json   + SDXL IPAdapter style reference
  DREAMSHAPER_XL_POSE_CONTROL.json     + bypassed ControlNet (union) branch

Same approach as the SD1.5 stage: the UI graph is generated from the prompt contract and
the live /api/object_info, because saved UI graphs store widget values positionally.

Usage:
  python build_dreamshaper_xl_workflows.py \
      --object-info measurements/comfyui_object_info_2026-11-13_full.json \
      --config workflows/dreamshaper_xl/prompt_orc_v1.json \
      --out workflows/dreamshaper_xl \
      --report measurements/dreamshaper_xl_build_report.json
"""
from __future__ import annotations

import argparse
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_factoria_voxel_workflows import Graph, load_object_info, _validate_count  # noqa: E402


def build(cfg: dict, oi: dict):
    gd = cfg["generation_defaults"]
    ip = cfg["ipadapter_reference"]
    cn = cfg["controlnet"]
    ckpt = cfg["model"]["checkpoint"]
    lora = cfg["style_module"]["lora"]
    positive = cfg["first_test"]["positive_prompt"]
    negative = cfg["first_test"]["negative_prompt"]

    common_note = (
        "**SDXL / DreamShaper XL Turbo v2.1**\n\n"
        f"Рабочие настройки turbo: **{gd['steps']} шагов, CFG {gd['cfg']}** "
        "(классический DSXL: 25–30 шагов, CFG 7).\n\n"
        f"Разрешение по умолчанию **{gd['width']}x{gd['height']}**, batch 1. "
        "Варианты: 768x768 (быстрее), 1024x1024 (только после замера памяти).\n\n"
        "Лимиты этапа: RAM <= 15 GB, дополнительная VRAM <= 2 GB над baseline.\n"
        "Ресурсы смотреть в панели **Crystools** (верхняя строка меню).\n\n"
        "Промпт: нода **POSITIVE - first Orc test** и **NEGATIVE**.\n"
        "Стиль **экспериментальный** — не утверждён."
    )
    base_note = common_note + "\n\n**BASE**: без LoRA, без IPAdapter, без ControlNet."
    lora_note = common_note + (
        f"\n\n**PIXEL_STYLE**: LoRA `{lora}` (триггер **{cfg['style_module']['trigger']}**, "
        f"weight {cfg['style_module']['strength_model']}).\n"
        "Триггер уже стоит первым в позитивном промпте — не удалять.\n"
        "Если LoRA не установлена, нода LoraLoader даст ошибку при Queue: это намеренно, "
        "тихого отката на BASE нет."
    )
    ref_note = common_note + (
        "\n\n**REFERENCE_LOCK**: стиль через SDXL IPAdapter "
        f"(`{ip['file']}` + `{ip['clip_vision']}`).\n"
        "1. Положить локальный референс в `ComfyUI/input/` под именем "
        f"`{ip['input_file']}` и выбрать его в ноде **LoadImage**.\n"
        f"2. Style weight по умолчанию {ip['weight']}, weight_type `{ip['weight_type']}`.\n"
        "3. Референс не публикуется в git. IPAdapter переносит стиль, не личность.\n"
        "Если весов нет — Queue упадёт с ошибкой (тихого отката нет)."
    )
    pose_note = common_note + (
        "\n\n**POSE_CONTROL**: ветка ControlNet по умолчанию выключена "
        "(ControlNetApplyAdvanced — BYPASS, препроцессоры и ControlNetLoader — MUTE).\n"
        f"Файл: `{cn['file']}` (union: canny, lineart, pose/dwpose, depth).\n"
        "Включение: выбрать файл в ControlNetLoader -> включить ОДИН препроцессор -> "
        "снять BYPASS с ControlNetApplyAdvanced (Ctrl+B) -> strength "
        f"{cn['strength']}, start {cn['start_percent']}/end {cn['end_percent']}."
    )

    def common(g, note_text):
        g.add("MarkdownNote", {"text": note_text}, pos=(-1000, 40), title="FACTORIA XL - read me")
        ckpt_n = g.add("CheckpointLoaderSimple", {"ckpt_name": ckpt}, pos=(0, 60),
                       title="CHECKPOINT DreamShaper XL Turbo v2.1")
        pos_n = g.add("CLIPTextEncode", {"text": positive}, pos=(0, 240),
                      title="POSITIVE - first Orc test")
        neg_n = g.add("CLIPTextEncode", {"text": negative}, pos=(0, 620), title="NEGATIVE")
        lat_n = g.add("EmptyLatentImage", {"width": gd["width"], "height": gd["height"],
                                           "batch_size": gd["batch_size"]}, pos=(0, 980),
                      title=f"{gd['width']}x{gd['height']} batch 1")
        return ckpt_n, pos_n, neg_n, lat_n

    def tail(g, model_src, pos_src, neg_src, lat_src, save_prefix):
        ks_n = g.add("KSampler", {"seed": gd["seed"], "control_after_generate": "fixed",
                                  "steps": gd["steps"], "cfg": gd["cfg"],
                                  "sampler_name": gd["sampler_name"],
                                  "scheduler": gd["scheduler"], "denoise": gd["denoise"]},
                     pos=(1000, 240), title="KSampler (turbo: 8 steps / CFG 2)")
        vd_n = g.add("VAEDecode", pos=(1350, 240))
        save_n = g.add("SaveImage", {"filename_prefix": save_prefix}, pos=(1620, 240))
        g.connect(model_src[0], model_src[1], ks_n, "model")
        g.connect(pos_src[0], pos_src[1], ks_n, "positive")
        g.connect(neg_src[0], neg_src[1], ks_n, "negative")
        g.connect(lat_src[0], lat_src[1], ks_n, "latent_image")
        g.connect(ks_n, 0, vd_n, "samples")
        g.connect(vd_n, 0, save_n, "images")
        return ks_n, vd_n, save_n

    results = {}

    # ---------------- BASE ----------------
    g = Graph(oi)
    ckpt_n, pos_n, neg_n, lat_n = common(g, base_note)
    ks_n, vd_n, save_n = tail(g, (ckpt_n, 0), (pos_n, 0), (neg_n, 0), (lat_n, 0), "factoria/xl_base")
    g.connect(ckpt_n, 1, pos_n, "clip")
    g.connect(ckpt_n, 1, neg_n, "clip")
    g.connect(ckpt_n, 2, vd_n, "vae")
    wf = g.to_workflow("dreamshaper_xl_base")
    results["DREAMSHAPER_XL_BASE.json"] = (wf, *_validate_count(wf, oi))

    # ---------------- PIXEL_STYLE (LoRA) ----------------
    g = Graph(oi)
    ckpt_n, pos_n, neg_n, lat_n = common(g, lora_note)
    lora_n = g.add("LoraLoader", {"lora_name": lora,
                                  "strength_model": cfg["style_module"]["strength_model"],
                                  "strength_clip": cfg["style_module"]["strength_clip"]},
                   pos=(420, 60), title="VoxelXL LoRA (voxel style)")
    ks_n, vd_n, save_n = tail(g, (lora_n, 0), (pos_n, 0), (neg_n, 0), (lat_n, 0),
                              "factoria/xl_pixel_style")
    g.connect(ckpt_n, 0, lora_n, "model")
    g.connect(ckpt_n, 1, lora_n, "clip")
    g.connect(lora_n, 1, pos_n, "clip")
    g.connect(lora_n, 1, neg_n, "clip")
    g.connect(ckpt_n, 2, vd_n, "vae")
    wf = g.to_workflow("dreamshaper_xl_pixel_style")
    results["DREAMSHAPER_XL_PIXEL_STYLE.json"] = (wf, *_validate_count(wf, oi))

    # ---------------- REFERENCE_LOCK (IPAdapter) ----------------
    g = Graph(oi)
    ckpt_n, pos_n, neg_n, lat_n = common(g, ref_note)
    load_n = g.add("LoadImage", {"image": ip["input_file"]}, pos=(0, 1300),
                   extra_widgets=["image"], title="STYLE REFERENCE (local only)")
    uni_n = g.add("IPAdapterUnifiedLoader", {"preset": ip["preset"]}, pos=(420, 60),
                  title="IPAdapter loader (SDXL weights required)")
    ipa_n = g.add("IPAdapterAdvanced", {"weight": ip["weight"], "weight_type": ip["weight_type"],
                                        "combine_embeds": ip["combine_embeds"],
                                        "start_at": ip["start_at"], "end_at": ip["end_at"],
                                        "embeds_scaling": ip["embeds_scaling"]}, pos=(420, 240),
                   title="IPAdapter apply - STYLE, not identity")
    ks_n, vd_n, save_n = tail(g, (ipa_n, 0), (pos_n, 0), (neg_n, 0), (lat_n, 0),
                              "factoria/xl_reference_lock")
    g.connect(ckpt_n, 0, uni_n, "model")
    g.connect(uni_n, 0, ipa_n, "model")
    g.connect(uni_n, 1, ipa_n, "ipadapter")
    g.connect(load_n, 0, ipa_n, "image")
    g.connect(ckpt_n, 1, pos_n, "clip")
    g.connect(ckpt_n, 1, neg_n, "clip")
    g.connect(ckpt_n, 2, vd_n, "vae")
    wf = g.to_workflow("dreamshaper_xl_reference_lock")
    results["DREAMSHAPER_XL_REFERENCE_LOCK.json"] = (wf, *_validate_count(wf, oi))

    # ---------------- POSE_CONTROL (ControlNet union, bypassed) ----------------
    g = Graph(oi)
    ckpt_n, pos_n, neg_n, lat_n = common(g, pose_note)
    ctl_load_n = g.add("LoadImage", {"image": "factoria_xl_pose_source.png"}, pos=(0, 1300),
                       extra_widgets=["image"], title="POSE / SILHOUETTE SOURCE")
    canny_n = g.add("Canny", {"low_threshold": 0.4, "high_threshold": 0.8}, pos=(420, 1300),
                    mode=2, title="Canny (MUTED by default)")
    line_n = g.add("LineArtPreprocessor", {"coarse": "disable", "resolution": 1024}, pos=(420, 1480),
                   mode=2, title="Lineart (MUTED by default)")
    pose_n = g.add("DWPreprocessor", {"detect_hand": "enable", "detect_body": "enable",
                                      "detect_face": "enable", "resolution": 1024,
                                      "bbox_detector": "yolox_l.onnx",
                                      "pose_estimator": "dw-ll_ucoco_384.onnx",
                                      "scale_stick_for_xinsr_cn": "disable"},
                   pos=(420, 1660), mode=2, title="DW-Pose (MUTED by default)")
    cn_load_n = g.add("ControlNetLoader", {"control_net_name": cn["file"]}, pos=(420, 1900),
                      mode=2, title="ControlNet union SDXL (muted until enabled)")
    cn_apply_n = g.add("ControlNetApplyAdvanced", {"strength": cn["strength"],
                                                   "start_percent": cn["start_percent"],
                                                   "end_percent": cn["end_percent"]},
                       pos=(1000, 700), mode=4, title="ControlNet apply (BYPASS by default)")
    ks_n, vd_n, save_n = tail(g, (ckpt_n, 0), (cn_apply_n, 0), (cn_apply_n, 1), (lat_n, 0),
                              "factoria/xl_pose_control")
    g.connect(ckpt_n, 1, pos_n, "clip")
    g.connect(ckpt_n, 1, neg_n, "clip")
    g.connect(ckpt_n, 2, vd_n, "vae")
    g.connect(ctl_load_n, 0, canny_n, "image")
    g.connect(ctl_load_n, 0, line_n, "image")
    g.connect(ctl_load_n, 0, pose_n, "image")
    g.connect(cn_load_n, 0, cn_apply_n, "control_net")
    g.connect(canny_n, 0, cn_apply_n, "image")
    g.connect(pos_n, 0, cn_apply_n, "positive")
    g.connect(neg_n, 0, cn_apply_n, "negative")
    wf = g.to_workflow("dreamshaper_xl_pose_control")
    results["DREAMSHAPER_XL_POSE_CONTROL.json"] = (wf, *_validate_count(wf, oi))

    return results


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
    results = build(cfg, oi)
    report = {"object_info_source": args.object_info, "config": args.config, "workflows": {}}
    rc = 0
    for fname, (wf, problems, warnings) in results.items():
        path = os.path.join(args.out, fname)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
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
    if args.report:
        os.makedirs(os.path.dirname(os.path.abspath(args.report)), exist_ok=True)
        with open(args.report, "w", encoding="utf-8", newline="\n") as f:
            json.dump(report, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"report: {args.report}")
    return rc


if __name__ == "__main__":
    sys.exit(main())
