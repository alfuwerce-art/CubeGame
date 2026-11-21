#!/usr/bin/env python3
"""COMFY-STYLE-07 — build reference-guided character workflows for ComfyUI.

Outputs (into --out):
  FACTORY_REFERENCE_IPADAPTER.json            SDXL + IPAdapter (reference image -> CLIP ViT-H)
  FACTORY_REFERENCE_CONTROLNET.json           SDXL + union ControlNet (Canny silhouette lock)
  FACTORY_REFERENCE_IMG2IMG.json              SDXL + Load Image -> VAE Encode -> KSampler -> VAE Decode
  FACTORY_REFERENCE_IPADAPTER_CONTROLNET.json IPAdapter model + ControlNet conditioning together
  reference_test_matrix_v1.json               first comparison matrix (human warrior)

Why this stage: COMFY-STYLE-05/06 generated characters from text only; the owner
rejected the style drift. Here the character is anchored to the prepared CubeGame
references (references/characters_v1). All graphs are built from the live
/object_info contract, not hand-edited JSON (widgets are stored positionally).

Plain SDXL sampling: 28 steps, CFG 6.0, dpmpp_2m/karras. Turbo presets are NOT used.

Usage:
  python build_reference_workflows.py \
      --object-info http://127.0.0.1:8190/object_info \
      --config workflows/factory_reference/reference_characters_v1.json \
      --out workflows/factory_reference \
      --report measurements/reference_workflows_build.json
"""
from __future__ import annotations

import argparse
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_factoria_voxel_workflows import Graph, load_object_info, _validate_count  # noqa: E402


def _readme(cfg: dict, mode: str) -> str:
    s = cfg["sampling"]
    m = cfg["model"]
    ip = cfg["ipadapter"]
    cn = cfg["controlnet"]
    lines = [
        f"# FACTORY REFERENCE — {mode}",
        "",
        f"Checkpoint `{m['checkpoint']}` ({m['family']}, SHA-256 `{m['sha256'][:8]}…`, openrail++).",
        f"Sampling: **{s['steps_default']} шагов, CFG {s['cfg_default']}**, `{s['sampler_name']}` / `{s['scheduler']}`, "
        f"seed `{s['seed']}`. Настройки Turbo (CFG 2 / 4–8 шагов) здесь **не используются**.",
        "",
        "Референс: подготовленная вырезка персонажа из Unity-скриншота "
        "(`references/characters_v1/…_reference_768x1024.png`, нейтральный фон, пропорции 1:1).",
        "",
    ]
    if mode in ("IPADAPTER", "IPADAPTER+CONTROLNET"):
        lines += [
            f"IPAdapter: `{ip['model']}` + CLIP ViT-H, weight по умолчанию **1.0**, "
            f"weight_type **{ip['weight_type_default']}** (проверяются 0.6 / 0.8 / 1.0 и `style transfer`).",
            "",
        ]
    if mode in ("CONTROLNET", "IPADAPTER+CONTROLNET"):
        lines += [
            f"ControlNet: `{cn['model']}` (union, type `{cn['union_type']}`), "
            f"Canny {cn['canny_low']}/{cn['canny_high']}, strength по умолчанию **0.9** "
            f"(проверяются 0.6 / 0.9). Контур берётся с того же референса.",
            "",
        ]
    if mode == "IMG2IMG":
        lines += [
            "Img2img: `Load Image → VAE Encode → KSampler → VAE Decode`, "
            f"denoise по умолчанию **{cfg['img2img']['denoise_default']}** "
            f"(проверяются {', '.join(str(d) for d in cfg['img2img']['denoise_set'])}).",
            "",
        ]
    lines += [
        "ComfyUI input: референсы копируются в `input/factoria_refs/` (см. run-скрипт).",
        "Лимиты: RAM ≤ 15 ГБ, VRAM — 768×1024; фактические пики пишет `reference_run_batch.py`.",
        "Художественную приёмку делает владелец; граф лишь доказывает, что режим исполняется.",
    ]
    return "\n".join(lines)


def build(cfg: dict, oi: dict, mode: str) -> dict:
    m, s = cfg["model"], cfg["sampling"]
    res = cfg["resolution"]["main"]
    ref = cfg["references"]["human_warrior"]["image"]
    char = cfg["characters"]["human_warrior"]
    neg = cfg["blocks"]["NEGATIVE"]

    g = Graph(oi)
    g.add("MarkdownNote", {"text": _readme(cfg, mode)}, pos=(-1150, 40), title="FACTORY REFERENCE - read me")

    ckpt = g.add("CheckpointLoaderSimple", {"ckpt_name": m.get("default_checkpoint_for_ui") or m["checkpoint"]}, pos=(0, 60),
                 title="A. MODEL - SDXL Base 1.0")
    lora_cfg = cfg.get("style_module") or {}
    if lora_cfg.get("file"):
        lora_n = g.add("LoraLoader", {
            "lora_name": lora_cfg["file"],
            "strength_model": lora_cfg.get("strength_default", 0.6),
            "strength_clip": lora_cfg.get("strength_default", 0.6),
        }, pos=(380, 60), title="STYLE LoRA - VoxelXL (strength editable, 0.0 = off)")
        g.connect(ckpt, 0, lora_n, "model")
        g.connect(ckpt, 1, lora_n, "clip")
        model_src, clip_src = lora_n, lora_n
    else:
        model_src, clip_src = ckpt, ckpt

    pos_n = g.add("CLIPTextEncode", {"text": char["prompt"]}, pos=(760, 240),
                  title="B. POSITIVE")
    neg_n = g.add("CLIPTextEncode", {"text": neg}, pos=(760, 560),
                  title="C. NEGATIVE")
    g.connect(clip_src, 1, pos_n, "clip")
    g.connect(clip_src, 1, neg_n, "clip")

    img_n = g.add("LoadImage", {"image": ref}, pos=(0, 300),
                  extra_widgets=["image"], title="REFERENCE. Load Image (owner cutout)")
    model_out, model_slot = model_src, 0
    pos_out, pos_slot = pos_n, 0
    neg_out, neg_slot = neg_n, 0
    latent = None

    if mode in ("IPADAPTER", "IPADAPTER+CONTROLNET"):
        ipa_n = g.add("IPAdapterModelLoader", {"ipadapter_file": cfg["ipadapter"]["model"]},
                      pos=(0, 700), title="IPAdapter model (plus SDXL vit-h)")
        cv_n = g.add("CLIPVisionLoader", {"clip_name": cfg["ipadapter"]["clip_vision"]},
                     pos=(0, 820), title="CLIP Vision ViT-H")
        adv_n = g.add("IPAdapterAdvanced", {
            "weight": 1.0,
            "weight_type": cfg["ipadapter"]["weight_type_default"],
            "combine_embeds": cfg["ipadapter"]["combine_embeds"],
            "start_at": cfg["ipadapter"]["start_at"],
            "end_at": cfg["ipadapter"]["end_at"],
            "embeds_scaling": cfg["ipadapter"]["embeds_scaling"],
        }, pos=(380, 700), title="IPAdapter Advanced (weight editable)")
        g.connect(model_src, 0, adv_n, "model")
        g.connect(ipa_n, 0, adv_n, "ipadapter")
        g.connect(img_n, 0, adv_n, "image")
        g.connect(cv_n, 0, adv_n, "clip_vision")
        model_out, model_slot = adv_n, 0

    if mode in ("CONTROLNET", "IPADAPTER+CONTROLNET"):
        scale_n = g.add("ImageScale", {"upscale_method": "nearest-exact", "width": res["width"],
                                       "height": res["height"], "crop": "disabled"},
                        pos=(0, 1000), title="Scale reference to latent size")
        canny_n = g.add("Canny", {"low_threshold": cfg["controlnet"]["canny_low"],
                                  "high_threshold": cfg["controlnet"]["canny_high"]},
                        pos=(380, 1000), title="Canny contour")
        cnet_n = g.add("ControlNetLoader", {"control_net_name": cfg["controlnet"]["model"]},
                       pos=(0, 1180), title="ControlNet union SDXL")
        union_n = g.add("SetUnionControlNetType", {"type": cfg["controlnet"]["union_type"]},
                        pos=(380, 1180), title="Union type: canny/lineart")
        apply_n = g.add("ControlNetApplyAdvanced", {
            "strength": 0.9,
            "start_percent": cfg["controlnet"]["start_percent"],
            "end_percent": cfg["controlnet"]["end_percent"],
        }, pos=(760, 1000), title="ControlNet Apply (strength editable)")
        g.connect(img_n, 0, scale_n, "image")
        g.connect(scale_n, 0, canny_n, "image")
        g.connect(cnet_n, 0, union_n, "control_net")
        g.connect(union_n, 0, apply_n, "control_net")
        g.connect(canny_n, 0, apply_n, "image")
        g.connect(pos_n, 0, apply_n, "positive")
        g.connect(neg_n, 0, apply_n, "negative")
        pos_out, pos_slot = apply_n, 0
        neg_out, neg_slot = apply_n, 1

    if mode == "IMG2IMG":
        enc_n = g.add("VAEEncode", pos=(380, 300), title="VAE Encode reference")
        g.connect(img_n, 0, enc_n, "pixels")
        g.connect(ckpt, 2, enc_n, "vae")
        latent = enc_n
    else:
        lat_n = g.add("EmptyLatentImage", {"width": res["width"], "height": res["height"],
                                           "batch_size": s["batch_size"]},
                      pos=(760, 1300), title=f"F. latent {res['width']}x{res['height']}")
        latent = lat_n

    ks_n = g.add("KSampler", {
        "seed": s["seed"],
        "control_after_generate": "fixed",
        "steps": s["steps_default"],
        "cfg": s["cfg_default"],
        "sampler_name": s["sampler_name"],
        "scheduler": s["scheduler"],
        "denoise": cfg["img2img"]["denoise_default"] if mode == "IMG2IMG" else s["denoise_txt2img"],
    }, pos=(1180, 240), title="F. KSampler (28 steps / CFG 6)")
    g.connect(model_out, model_slot, ks_n, "model")
    g.connect(pos_out, pos_slot, ks_n, "positive")
    g.connect(neg_out, neg_slot, ks_n, "negative")
    g.connect(latent, 0, ks_n, "latent_image")

    vd_n = g.add("VAEDecode", pos=(1520, 240))
    g.connect(ks_n, 0, vd_n, "samples")
    g.connect(ckpt, 2, vd_n, "vae")
    save_n = g.add("SaveImage", {"filename_prefix": f"factory/reference_{mode.lower().replace('+', '_')}"},
                   pos=(1760, 240), title="G. OUTPUT")
    g.connect(vd_n, 0, save_n, "images")
    return g.to_workflow(f"reference_{mode.lower().replace('+', '_')}")


FILES = {
    "IPADAPTER": "FACTORY_REFERENCE_IPADAPTER.json",
    "CONTROLNET": "FACTORY_REFERENCE_CONTROLNET.json",
    "IMG2IMG": "FACTORY_REFERENCE_IMG2IMG.json",
    "IPADAPTER+CONTROLNET": "FACTORY_REFERENCE_IPADAPTER_CONTROLNET.json",
}


def matrix(cfg: dict) -> dict:
    s = cfg["sampling"]
    seed = s["seed"]
    res = "main"
    common = {"character": "human_warrior", "resolution": res, "seed": seed,
              "steps": s["steps_default"], "cfg": s["cfg_default"],
              "sampler": s["sampler_name"], "scheduler": s["scheduler"]}
    runs = []

    def add(rid, wf, **kw):
        run = dict(common)
        run.update(kw)
        run["id"] = rid
        run["workflow"] = wf
        runs.append(run)

    for w in cfg["ipadapter"]["weights"]:
        add(f"ref_ipa_w{int(round(w * 10)):02d}", FILES["IPADAPTER"],
            ipadapter_weight=w, ipadapter_weight_type=cfg["ipadapter"]["weight_type_default"],
            purpose="IPAdapter weight sweep")
    add("ref_ipa_style_transfer", FILES["IPADAPTER"],
        ipadapter_weight=1.0, ipadapter_weight_type=cfg["ipadapter"]["weight_type_style"],
        purpose="IPAdapter style transfer mode (SDXL)")

    for st in cfg["controlnet"]["strengths"]:
        add(f"ref_cn_canny_{int(round(st * 100)):03d}", FILES["CONTROLNET"],
            controlnet_strength=st, purpose="ControlNet Canny strength sweep")

    for d in cfg["img2img"]["denoise_set"]:
        add(f"ref_i2i_d{int(round(d * 100)):03d}", FILES["IMG2IMG"],
            denoise=d, purpose="img2img denoise sweep (closeness to reference)")

    add("ref_ipa_cn_combo", FILES["IPADAPTER+CONTROLNET"],
        ipadapter_weight=0.8, ipadapter_weight_type=cfg["ipadapter"]["weight_type_default"],
        controlnet_strength=0.8, purpose="IPAdapter + ControlNet together (VRAM test)")

    return {"id": "FACTORY_REFERENCE_MATRIX_V1", "runs": runs}


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
    for mode, fname in FILES.items():
        wf = build(cfg, oi, mode)
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
    with open(os.path.join(args.out, "reference_test_matrix_v1.json"), "w", encoding="utf-8",
              newline="\n") as f:
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
