#!/usr/bin/env python3
"""Compile the MANNEQUIN V3 graphs (COMFY-STYLE-12 STYLE LOCK) and validate them live.

The owner approved a new visual standard (HUMAN MALE - MANNEQUIN V3, HUMAN FEMALE - MANNEQUIN
V3). The style stack is NOT changed - only the technical guide and the prompt wording that
describes the approved anatomy:

  checkpoint  sd_xl_base_1.0.safetensors
  LoRA        VoxelXL_v1.safetensors (Fictiverse) 0.60 / 0.60
  ControlNet  controlnet-union-sdxl-1.0.safetensors, union canny/lineart, strength 0.60
  latent      EmptyLatentImage 768x1024 batch 1
  sampler     dpmpp_2m / karras, 28 steps, CFG 6.0, seed 20261117, denoise 1.0
  no IPAdapter, no img2img, no ral-vxl

Graph format is ComfyUI *API* JSON: it is submitted verbatim, so no positional widget order
can be silently broken.

Usage:
  python_embeded\\python.exe scripts\\comfyui\\compile_mannequin_v3.py --figure male \
      --out workflows/character_factory/mannequin/MANNEQUIN_V3_MALE.json \
      --guide HUMAN_MALE_MANNEQUIN_GUIDE.png --validate http://127.0.0.1:8188
"""
from __future__ import annotations

import argparse
import json
import os
import urllib.request

CHECKPOINT = "sd_xl_base_1.0.safetensors"
LORA = "VoxelXL_v1.safetensors"
CONTROLNET = "controlnet-union-sdxl-1.0.safetensors"
UNION_TYPE = "canny/lineart/anime_lineart/mlsd"

COMMON_POSITIVE = (
    "one single full-body {sex} humanoid game mannequin, exact straight-on orthographic front "
    "view, symmetric T-pose, both arms horizontal at shoulder height, straight legs, full hands "
    "and feet visible, "
)
COMMON_NEGATIVE = (
    "eyes, pupils, eyebrows, eyelashes, mouth, lips, teeth, nose detail, facial expression, "
    "facial features, hair, fringe, hair block, topknot, bun, ponytail, plate on top of the "
    "head, second layer above the head, helmet, cap, hat, hood, brim, headband, band around the "
    "head, rim, crown ornament, ears detail, beard, moustache, helmet, clothes, shirt, tunic, "
    "trousers, underwear, belt, armor, gloves, boots, shoes, weapon, sword, staff, accessory, "
    "jewelry, skin texture, realistic skin, muscles, veins, anatomy markings, nipples, genitals, "
    "photorealistic, organic sculpt, rounded body, smooth toy, chibi, anime, large eyes, tiny "
    "noisy cubes, excessive bevels, multiple characters, character sheet, collage, extra limbs, "
    "missing limbs, bent arms, A-pose, bent knees, perspective camera, three-quarter view, "
    "cropped hands, cropped feet, floor props, scenery, text, watermark, logo"
)

FIGURES = {
    "male": {
        "positive": COMMON_POSITIVE.format(sex="adult male") + (
            "oversized cubic head occupying one third of the total body height, perfectly flat "
            "smooth crown, nothing above the crown, small separate cube ears attached at the "
            "sides of the head, stepped chin block under the head, completely blank featureless "
            "face, broad shoulder blocks, compact chest box, moderately narrow torso, narrow "
            "small pelvis, short massive legs, small blocky hands, separate arm and leg "
            "segments, hard-edged cuboid body volumes, large coherent block surfaces, visible "
            "front and side planes, stepped voxel silhouette, neutral matte light-gray material "
            "over the entire body, soft even studio lighting, plain neutral gray background, "
            "centered, full body"
        ),
        "prefix": "MANNEQUIN_V3_MALE",
        "default_guide": "HUMAN_MALE_MANNEQUIN_GUIDE.png",
    },    "female": {
        "positive": COMMON_POSITIVE.format(sex="adult female") + (
            "cubic head of the same size and type as the male mannequin occupying one third of "
            "the total body height, perfectly flat smooth crown, nothing above the crown, small "
            "separate cube ears attached at the sides of the head, stepped chin block under the "
            "head, completely blank featureless face, shoulders slightly narrower than the male "
            "mannequin, compact chest with two small flat rectangular chest blocks, gently "
            "tapered waist, compact narrow pelvis, thighs starting directly under the pelvis, "
            "short legs, small blocky hands, the same modular arm and leg segments as the male "
            "mannequin, hard-edged cuboid body volumes, large coherent block surfaces, visible "
            "front and side planes, stepped voxel silhouette, neutral matte light-gray material "
            "over the entire body, soft even studio lighting, plain neutral gray background, "
            "centered, full body"
        ),
        "prefix": "MANNEQUIN_V3_FEMALE",
        "default_guide": "HUMAN_FEMALE_MANNEQUIN_GUIDE.png",
    },
}


# Prompt profiles. `v3` spells out the approved anatomy; `v1` is the wording of the accepted
# MANNEQUIN_V1 / DIAG3 stack, kept byte-identical so a diagnostic run can isolate the guide
# from the prompt (only one variable changes per run).
PROMPT_PROFILES = {
    "v3": None,
    "v1": (
        "one single full-body {sex} humanoid game mannequin, exact straight-on orthographic "
        "front view, symmetric T-pose, both arms horizontal at shoulder height, straight legs, "
        "full hands and feet visible, compact stylized voxel proportions, hard-edged cuboid body "
        "volumes, large coherent block surfaces, visible front and side planes, stepped voxel "
        "silhouette, distinct shoulder upper-arm forearm hand volumes, distinct thigh shin foot "
        "volumes, short neck, large simple blank block head, completely featureless face, neutral "
        "matte light-gray material over the entire body, soft even studio lighting, plain neutral "
        "gray background, centered, full body"
    ),
}


def api_graph(*, guide: str, positive: str, negative: str, prefix: str, width: int,
              height: int, seed: int, steps: int, cfg: float, sampler: str, scheduler: str,
              denoise: float, lora: float, control: float, control_start: float,
              control_end: float, canny_low: float, canny_high: float,
              depth: str | None = None, depth_strength: float = 0.55,
              depth_end: float = 0.85) -> dict:
    graph = {
        "checkpoint": {"class_type": "CheckpointLoaderSimple",
                       "_meta": {"title": "SDXL Base 1.0"},
                       "inputs": {"ckpt_name": CHECKPOINT}},
        "lora": {"class_type": "LoraLoader",
                 "_meta": {"title": "Fictiverse VoxelXL (frozen strengths)"},
                 "inputs": {"model": ["checkpoint", 0], "clip": ["checkpoint", 1],
                            "lora_name": LORA, "strength_model": lora, "strength_clip": lora}},
        "positive": {"class_type": "CLIPTextEncode",
                     "_meta": {"title": "POSITIVE MANNEQUIN V3 contract"},
                     "inputs": {"clip": ["lora", 1], "text": positive}},
        "negative": {"class_type": "CLIPTextEncode",
                     "_meta": {"title": "NEGATIVE MANNEQUIN V3 contract"},
                     "inputs": {"clip": ["lora", 1], "text": negative}},
        "latent": {"class_type": "EmptyLatentImage",
                   "_meta": {"title": "Empty latent - no img2img"},
                   "inputs": {"width": width, "height": height, "batch_size": 1}},
        "guide": {"class_type": "LoadImage",
                  "_meta": {"title": "MANNEQUIN V3 technical guide (control input only)"},
                  "inputs": {"image": guide}},
        "guide_scale": {"class_type": "ImageScale",
                        "_meta": {"title": "Fixed framing, no crop"},
                        "inputs": {"image": ["guide", 0], "upscale_method": "nearest-exact",
                                   "width": width, "height": height, "crop": "disabled"}},
        "edges": {"class_type": "Canny",
                  "_meta": {"title": "Silhouette + block plane edges"},
                  "inputs": {"image": ["guide_scale", 0], "low_threshold": canny_low,
                             "high_threshold": canny_high}},
        "controlnet": {"class_type": "ControlNetLoader",
                       "_meta": {"title": "ControlNet Union SDXL"},
                       "inputs": {"control_net_name": CONTROLNET}},
        "union_mode": {"class_type": "SetUnionControlNetType",
                       "_meta": {"title": "Canny / lineart union mode"},
                       "inputs": {"control_net": ["controlnet", 0], "type": UNION_TYPE}},
        "guide_conditioning": {"class_type": "ControlNetApplyAdvanced",
                               "_meta": {"title": "Pose and proportion conditioning"},
                               "inputs": {"positive": ["positive", 0],
                                          "negative": ["negative", 0],
                                          "control_net": ["union_mode", 0],
                                          "image": ["edges", 0], "strength": control,
                                          "start_percent": control_start,
                                          "end_percent": control_end}},
        "sampler": {"class_type": "KSampler",
                    "_meta": {"title": "Single mannequin frame"},
                    "inputs": {"model": ["lora", 0], "positive": ["guide_conditioning", 0],
                               "negative": ["guide_conditioning", 1],
                               "latent_image": ["latent", 0], "seed": seed, "steps": steps,
                               "cfg": cfg, "sampler_name": sampler, "scheduler": scheduler,
                               "denoise": denoise}},
        "decode": {"class_type": "VAEDecode", "_meta": {"title": "Decoded RGB frame"},
                   "inputs": {"samples": ["sampler", 0], "vae": ["checkpoint", 2]}},
        "save": {"class_type": "SaveImage", "_meta": {"title": "Mannequin V3 frame"},
                 "inputs": {"images": ["decode", 0], "filename_prefix": prefix}},
    }
    if depth:
        # Optional second union branch: the canny plate fixes pose and proportions, the depth
        # plate gives the volume of the V3 block stack. Needed because a silhouette-only plate
        # produced a flat cardboard cutout (recorded in the COMFY-STYLE-12 V3 run report).
        graph["depth_image"] = {"class_type": "LoadImage",
                                "_meta": {"title": "MANNEQUIN V3 depth plate (control only)"},
                                "inputs": {"image": depth}}
        graph["depth_scale"] = {"class_type": "ImageScale",
                                "_meta": {"title": "Fixed framing, no crop"},
                                "inputs": {"image": ["depth_image", 0],
                                           "upscale_method": "nearest-exact",
                                           "width": width, "height": height,
                                           "crop": "disabled"}}
        graph["depth_controlnet"] = {"class_type": "ControlNetLoader",
                                     "_meta": {"title": "ControlNet Union SDXL (depth branch)"},
                                     "inputs": {"control_net_name": CONTROLNET}}
        graph["depth_mode"] = {"class_type": "SetUnionControlNetType",
                               "_meta": {"title": "Depth union mode"},
                               "inputs": {"control_net": ["depth_controlnet", 0],
                                          "type": "depth"}}
        graph["depth_conditioning"] = {
            "class_type": "ControlNetApplyAdvanced",
            "_meta": {"title": "Block volume conditioning"},
            "inputs": {"positive": ["guide_conditioning", 0],
                       "negative": ["guide_conditioning", 1],
                       "control_net": ["depth_mode", 0],
                       "image": ["depth_scale", 0],
                       "strength": depth_strength,
                       "start_percent": 0.0,
                       "end_percent": depth_end}}
        graph["sampler"]["inputs"]["positive"] = ["depth_conditioning", 0]
        graph["sampler"]["inputs"]["negative"] = ["depth_conditioning", 1]
    return graph


def fetch_object_info(url: str) -> dict:
    with urllib.request.urlopen(f"{url.rstrip('/')}/object_info", timeout=120) as r:
        return json.loads(r.read().decode("utf-8"))


def validate(graph: dict, info: dict) -> list[str]:
    problems: list[str] = []
    for node_id, node in graph.items():
        cls = node["class_type"]
        spec = info.get(cls)
        if spec is None:
            problems.append(f"{node_id}: class {cls} missing on server")
            continue
        allowed = {}
        for group in ("required", "optional"):
            for name, s in (spec.get("input", {}).get(group) or {}).items():
                allowed[name] = s
        for name, value in node["inputs"].items():
            if name not in allowed:
                problems.append(f"{node_id}.{cls}: unknown input '{name}'")
                continue
            spec_value = allowed[name]
            if not spec_value:
                continue
            kind = spec_value[0]
            if isinstance(kind, list) and kind and kind[0] == "COMBO":
                options = kind[1].get("options", [])
                if isinstance(value, str) and value not in options:
                    problems.append(f"{node_id}.{name}: '{value}' not in server options")
    return problems


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--figure", choices=("male", "female"), required=True)
    ap.add_argument("--out")
    ap.add_argument("--guide")
    ap.add_argument("--width", type=int, default=768)
    ap.add_argument("--height", type=int, default=1024)
    ap.add_argument("--seed", type=int, default=20261117)
    ap.add_argument("--steps", type=int, default=28)
    ap.add_argument("--cfg", type=float, default=6.0)
    ap.add_argument("--sampler", default="dpmpp_2m")
    ap.add_argument("--scheduler", default="karras")
    ap.add_argument("--denoise", type=float, default=1.0)
    ap.add_argument("--lora", type=float, default=0.60)
    ap.add_argument("--control", type=float, default=0.60)
    ap.add_argument("--control-start", type=float, default=0.0)
    ap.add_argument("--control-end", type=float, default=1.0)
    ap.add_argument("--canny-low", type=float, default=0.35)
    ap.add_argument("--canny-high", type=float, default=0.75)
    ap.add_argument("--prefix")
    ap.add_argument("--prompt-profile", choices=("v3", "v1"), default="v3",
                    help="v3 = MANNEQUIN V3 anatomy wording, v1 = accepted V1/DIAG3 wording")
    ap.add_argument("--depth", help="depth plate for the union depth branch (optional)")
    ap.add_argument("--depth-strength", type=float, default=0.55)
    ap.add_argument("--depth-end", type=float, default=0.85)
    ap.add_argument("--validate")
    args = ap.parse_args()

    fig = FIGURES[args.figure]
    sex = "adult male" if args.figure == "male" else "adult female"
    if args.prompt_profile == "v1":
        positive = PROMPT_PROFILES["v1"].format(sex=sex)
        prefix = args.prefix or (fig["prefix"] + "_PV1")
    else:
        positive = fig["positive"]
        prefix = args.prefix or fig["prefix"]
    out = args.out or f"workflows/character_factory/mannequin/MANNEQUIN_V3_{args.figure.upper()}.json"
    graph = api_graph(
        guide=args.guide or fig["default_guide"], positive=positive,
        negative=COMMON_NEGATIVE, prefix=prefix,
        width=args.width, height=args.height, seed=args.seed, steps=args.steps, cfg=args.cfg,
        sampler=args.sampler, scheduler=args.scheduler, denoise=args.denoise, lora=args.lora,
        control=args.control, control_start=args.control_start, control_end=args.control_end,
        canny_low=args.canny_low, canny_high=args.canny_high,
        depth=args.depth, depth_strength=args.depth_strength, depth_end=args.depth_end,
    )

    if args.validate:
        problems = validate(graph, fetch_object_info(args.validate))
        print(f"validation_nodes={len(graph)} problems={len(problems)}")
        for p in problems:
            print("  PROBLEM " + p)
        if problems:
            return 2

    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(graph, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"written={out} nodes={len(graph)} figure={args.figure} guide={args.guide or fig['default_guide']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
