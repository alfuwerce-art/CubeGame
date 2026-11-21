#!/usr/bin/env python3
"""Compile the MANNEQUIN_V1 graph (COMFY-STYLE-12) and verify it against the live server.

Stack fixed by the task (docs/tasks/COMFY-STYLE-12_MANNEQUIN_V1.md §3, §6):
  checkpoint  sd_xl_base_1.0.safetensors
  LoRA        VoxelXL_v1.safetensors (Fictiverse) 0.60 / 0.60
  ControlNet  controlnet-union-sdxl-1.0.safetensors, union canny/lineart mode, strength 0.60
  latent      EmptyLatentImage 768x1024 batch 1
  sampler     dpmpp_2m / karras, 28 steps, CFG 6.0, seed 20261117, denoise 1.0
  no IPAdapter, no img2img, no ral-vxl

The contract file is written in the ComfyUI *API* format: it is submitted verbatim, so no
positional widget order can be silently broken (the known F15 trap of the UI JSON format).

Usage:
  python_embeded\\python.exe scripts\\comfyui\\compile_mannequin_v1.py \
      --out workflows/character_factory/mannequin/MANNEQUIN_V1.json \
      --validate http://127.0.0.1:8188 [--lora 0.7] [--control 0.75] [--tag DIAG2]
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

POSITIVE = (
    "one single full-body adult male humanoid game mannequin, exact straight-on orthographic "
    "front view, symmetric T-pose, both arms horizontal at shoulder height, straight legs, "
    "full hands and feet visible, compact stylized voxel proportions, hard-edged cuboid body "
    "volumes, large coherent block surfaces, visible front and side planes, stepped voxel "
    "silhouette, distinct shoulder upper-arm forearm hand volumes, distinct thigh shin foot "
    "volumes, short neck, large simple blank block head, completely featureless face, neutral "
    "matte light-gray material over the entire body, soft even studio lighting, plain neutral "
    "gray background, centered, full body"
)
NEGATIVE = (
    "eyes, pupils, eyebrows, eyelashes, mouth, lips, teeth, nose detail, ears detail, facial "
    "expression, hair, beard, moustache, helmet, hat, clothes, shirt, tunic, trousers, "
    "underwear, belt, armor, gloves, boots, shoes, weapon, sword, staff, accessory, jewelry, "
    "skin texture, realistic skin, muscles, veins, anatomy markings, nipples, genitals, "
    "photorealistic, organic sculpt, rounded body, smooth toy, chibi, anime, large eyes, tiny "
    "noisy cubes, excessive bevels, multiple characters, character sheet, collage, extra limbs, "
    "missing limbs, bent arms, A-pose, bent knees, perspective camera, three-quarter view, "
    "cropped hands, cropped feet, floor props, scenery, text, watermark, logo"
)


def api_graph(*, guide: str, width: int, height: int, seed: int, steps: int, cfg: float,
              sampler: str, scheduler: str, denoise: float, lora: float, control: float,
              control_start: float, control_end: float, canny_low: float, canny_high: float,
              prefix: str) -> dict:
    return {
        "checkpoint": {
            "class_type": "CheckpointLoaderSimple",
            "_meta": {"title": "SDXL Base 1.0"},
            "inputs": {"ckpt_name": CHECKPOINT},
        },
        "lora": {
            "class_type": "LoraLoader",
            "_meta": {"title": "Fictiverse VoxelXL (frozen strengths)"},
            "inputs": {
                "model": ["checkpoint", 0],
                "clip": ["checkpoint", 1],
                "lora_name": LORA,
                "strength_model": lora,
                "strength_clip": lora,
            },
        },
        "positive": {
            "class_type": "CLIPTextEncode",
            "_meta": {"title": "POSITIVE mannequin contract"},
            "inputs": {"clip": ["lora", 1], "text": POSITIVE},
        },
        "negative": {
            "class_type": "CLIPTextEncode",
            "_meta": {"title": "NEGATIVE mannequin contract"},
            "inputs": {"clip": ["lora", 1], "text": NEGATIVE},
        },
        "latent": {
            "class_type": "EmptyLatentImage",
            "_meta": {"title": "Empty latent - no img2img"},
            "inputs": {"width": width, "height": height, "batch_size": 1},
        },
        "guide": {
            "class_type": "LoadImage",
            "_meta": {"title": "Technical FRONT T-pose guide (control input only)"},
            "inputs": {"image": guide},
        },
        "guide_scale": {
            "class_type": "ImageScale",
            "_meta": {"title": "Fixed framing, no crop"},
            "inputs": {
                "image": ["guide", 0],
                "upscale_method": "nearest-exact",
                "width": width,
                "height": height,
                "crop": "disabled",
            },
        },
        "edges": {
            "class_type": "Canny",
            "_meta": {"title": "Silhouette + block plane edges"},
            "inputs": {
                "image": ["guide_scale", 0],
                "low_threshold": canny_low,
                "high_threshold": canny_high,
            },
        },
        "controlnet": {
            "class_type": "ControlNetLoader",
            "_meta": {"title": "ControlNet Union SDXL"},
            "inputs": {"control_net_name": CONTROLNET},
        },
        "union_mode": {
            "class_type": "SetUnionControlNetType",
            "_meta": {"title": "Canny / lineart union mode"},
            "inputs": {"control_net": ["controlnet", 0], "type": UNION_TYPE},
        },
        "guide_conditioning": {
            "class_type": "ControlNetApplyAdvanced",
            "_meta": {"title": "Pose and proportion conditioning"},
            "inputs": {
                "positive": ["positive", 0],
                "negative": ["negative", 0],
                "control_net": ["union_mode", 0],
                "image": ["edges", 0],
                "strength": control,
                "start_percent": control_start,
                "end_percent": control_end,
            },
        },
        "sampler": {
            "class_type": "KSampler",
            "_meta": {"title": "Single mannequin frame"},
            "inputs": {
                "model": ["lora", 0],
                "positive": ["guide_conditioning", 0],
                "negative": ["guide_conditioning", 1],
                "latent_image": ["latent", 0],
                "seed": seed,
                "steps": steps,
                "cfg": cfg,
                "sampler_name": sampler,
                "scheduler": scheduler,
                "denoise": denoise,
            },
        },
        "decode": {
            "class_type": "VAEDecode",
            "_meta": {"title": "Decoded RGB frame"},
            "inputs": {"samples": ["sampler", 0], "vae": ["checkpoint", 2]},
        },
        "save": {
            "class_type": "SaveImage",
            "_meta": {"title": "Mannequin frame"},
            "inputs": {"images": ["decode", 0], "filename_prefix": prefix},
        },
    }


def fetch_object_info(url: str) -> dict:
    with urllib.request.urlopen(f"{url.rstrip('/')}/object_info", timeout=120) as r:
        return json.loads(r.read().decode("utf-8"))


def validate(graph: dict, info: dict) -> list[str]:
    """Every class, every input key and every COMBO value must exist on this server."""
    problems: list[str] = []
    for node_id, node in graph.items():
        cls = node["class_type"]
        spec = info.get(cls)
        if spec is None:
            problems.append(f"{node_id}: class {cls} missing on server")
            continue
        order = spec.get("input_order", {})
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
    ap.add_argument("--out", default="workflows/character_factory/mannequin/MANNEQUIN_V1.json")
    ap.add_argument("--guide", default="MANNEQUIN_GUIDE_V1.png")
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
    ap.add_argument("--prefix", default="MANNEQUIN_V1")
    ap.add_argument("--validate", help="ComfyUI base URL; checks the graph against /object_info")
    args = ap.parse_args()

    graph = api_graph(
        guide=args.guide, width=args.width, height=args.height, seed=args.seed,
        steps=args.steps, cfg=args.cfg, sampler=args.sampler, scheduler=args.scheduler,
        denoise=args.denoise, lora=args.lora, control=args.control,
        control_start=args.control_start, control_end=args.control_end,
        canny_low=args.canny_low, canny_high=args.canny_high, prefix=args.prefix,
    )

    if args.validate:
        problems = validate(graph, fetch_object_info(args.validate))
        print(f"validation_nodes={len(graph)} problems={len(problems)}")
        for p in problems:
            print("  PROBLEM " + p)
        if problems:
            return 2

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(graph, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"written={args.out} nodes={len(graph)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
