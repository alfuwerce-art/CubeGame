#!/usr/bin/env python3
"""COMFY-STYLE-07 — run the reference-guided matrices and record every fact.

Unlike factory_run_batch.py this runner also drives the reference branch of the graph:

  * LoadImage            -> which prepared character reference is used
  * IPAdapterAdvanced    -> weight and weight_type (0.6/0.8/1.0, style transfer)
  * ControlNetApplyAdvanced -> strength (0.6/0.9) and start/end percent
  * KSampler             -> seed, steps, cfg, sampler, scheduler, denoise (img2img)
  * ImageScale / EmptyLatentImage -> resolution

For every run it stores a manifest (all parameters, seed, reference sha256, timings,
peak RAM/VRAM) and two closeness metrics against the reference image (mean absolute
difference and silhouette IoU). The metrics are heuristics, not an art judgement.

Usage:
  python reference_run_batch.py \
      --url http://127.0.0.1:8190 --server-pid <pid> \
      --config workflows/factory_reference/reference_characters_v1.json \
      --matrix workflows/factory_reference/reference_test_matrix_v1.json \
      --workflow-dir workflows/factory_reference \
      --comfy-input D:/programs/ComfyUI/ComfyUI/input \
      --out outputs/factory/reference_v1 \
      --report measurements/reference_batch_report_v1.json
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from factory_run_batch import http_json, Sampler, sha256_file  # noqa: E402
from validate_factoria_workflows import ui_to_api  # noqa: E402

NEUTRAL = np.array([158, 158, 158], dtype=np.float32)


def resolution_of(cfg: dict, preset: str) -> dict:
    return cfg["resolution"][preset]


def ensure_input_copy(cfg: dict, key: str, comfy_input: str) -> str:
    ref = cfg["references"][key]
    name = os.path.basename(ref["image"])
    dst = os.path.join(comfy_input, name)
    src = ref["local_reference"]
    if not os.path.exists(src):
        raise FileNotFoundError(f"reference not prepared: {src}")
    if (not os.path.exists(dst)) or sha256_file(dst) != sha256_file(src):
        os.makedirs(comfy_input, exist_ok=True)
        shutil.copyfile(src, dst)
    return name


def closeness(out_png: str, ref_png: str) -> dict:
    a = Image.open(out_png).convert("RGB").resize((256, 256), Image.BILINEAR)
    b = Image.open(ref_png).convert("RGB").resize((256, 256), Image.BILINEAR)
    aa, bb = np.asarray(a, np.float32), np.asarray(b, np.float32)
    l1 = float(np.abs(aa - bb).mean())
    da = np.abs(aa - NEUTRAL).max(axis=2) > 30
    db = np.abs(bb - NEUTRAL).max(axis=2) > 30
    inter = float((da & db).sum())
    union = float((da | db).sum()) or 1.0
    return {
        "l1_mean_0_255": round(l1, 2),
        "silhouette_iou": round(inter / union, 3),
        "fg_pixels_output": int(da.sum()),
        "fg_pixels_reference": int(db.sum()),
        "note": "heuristic only: mean |RGB| difference and IoU of non-grey silhouettes",
    }


def apply(prompt: dict, run: dict, cfg: dict, res: dict, image_name: str, prefix: str) -> dict:
    char = cfg["characters"][run["character"]]
    negative = cfg["blocks"]["NEGATIVE"]
    positive = char["prompt"]
    if run.get("positive_extra"):
        positive += ", " + run["positive_extra"]
    if run.get("negative_extra"):
        negative += ", " + run["negative_extra"]

    for n in prompt.values():
        ct, title = n["class_type"], (n.get("_meta") or {}).get("title", "").upper()
        if ct == "CLIPTextEncode":
            if "POSITIVE" in title:
                n["inputs"]["text"] = positive
            elif "NEGATIVE" in title:
                n["inputs"]["text"] = negative
        elif ct == "CheckpointLoaderSimple":
            n["inputs"]["ckpt_name"] = cfg["model"]["checkpoint"]
        elif ct == "LoadImage":
            n["inputs"]["image"] = image_name
        elif ct == "ImageScale":
            n["inputs"]["width"], n["inputs"]["height"] = res["width"], res["height"]
        elif ct == "EmptyLatentImage":
            n["inputs"]["width"], n["inputs"]["height"] = res["width"], res["height"]
            n["inputs"]["batch_size"] = 1
        elif ct == "LoraLoader":
            strength = float(run.get("lora_strength", (cfg.get("style_module") or {}).get("strength_default", 0.6)))
            n["inputs"]["strength_model"] = strength
            n["inputs"]["strength_clip"] = strength
        elif ct == "IPAdapterAdvanced":
            n["inputs"]["weight"] = float(run.get("ipadapter_weight", 1.0))
            n["inputs"]["weight_type"] = run.get("ipadapter_weight_type",
                                                cfg["ipadapter"]["weight_type_default"])
        elif ct == "ControlNetApplyAdvanced":
            n["inputs"]["strength"] = float(run.get("controlnet_strength", 0.9))
            n["inputs"]["start_percent"] = cfg["controlnet"]["start_percent"]
            n["inputs"]["end_percent"] = cfg["controlnet"]["end_percent"]
        elif ct == "KSampler":
            n["inputs"]["seed"] = int(run["seed"])
            n["inputs"]["steps"] = int(run.get("steps", cfg["sampling"]["steps_default"]))
            n["inputs"]["cfg"] = float(run.get("cfg", cfg["sampling"]["cfg_default"]))
            n["inputs"]["sampler_name"] = run.get("sampler", cfg["sampling"]["sampler_name"])
            n["inputs"]["scheduler"] = run.get("scheduler", cfg["sampling"]["scheduler"])
            if run.get("denoise") is not None:
                n["inputs"]["denoise"] = float(run["denoise"])
            elif ct == "KSampler" and run.get("workflow", "").endswith("IMG2IMG.json"):
                n["inputs"]["denoise"] = float(cfg["img2img"]["denoise_default"])
        elif ct == "SaveImage":
            n["inputs"]["filename_prefix"] = prefix
    return prompt


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--url", default="http://127.0.0.1:8190")
    ap.add_argument("--server-pid", type=int, default=0)
    ap.add_argument("--config", required=True)
    ap.add_argument("--matrix", required=True)
    ap.add_argument("--workflow-dir", required=True)
    ap.add_argument("--comfy-input", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--report", required=True)
    ap.add_argument("--only", default="")
    ap.add_argument("--timeout", type=float, default=3600.0)
    args = ap.parse_args()

    with open(args.config, "r", encoding="utf-8") as f:
        cfg = json.load(f)
    with open(args.matrix, "r", encoding="utf-8") as f:
        matrix = json.load(f)

    os.makedirs(args.out, exist_ok=True)
    only = {s.strip() for s in args.only.split(",") if s.strip()}
    runs = [r for r in matrix["runs"] if not only or r["id"] in only]

    wf_cache: dict[str, dict] = {}

    def load_wf(name: str) -> dict:
        if name not in wf_cache:
            with open(os.path.join(args.workflow_dir, name), "r", encoding="utf-8") as f:
                wf_cache[name] = json.load(f)
        return wf_cache[name]

    report = {"id": matrix.get("id"), "url": args.url, "server_pid": args.server_pid,
              "config": args.config, "runs": []}
    failed = 0
    for run in runs:
        wf = load_wf(run["workflow"])
        res = resolution_of(cfg, run["resolution"])
        image_name = ensure_input_copy(cfg, run["character"], args.comfy_input)
        ref_local = cfg["references"][run["character"]]["local_reference"]

        prompt, notes = ui_to_api(wf)
        prompt = apply(prompt, run, cfg, res, image_name, f"factory/ref_{run['id']}")

        sampler = Sampler(args.server_pid)
        sampler.start()
        t0 = time.time()
        try:
            resp = http_json(f"{args.url}/prompt", {"prompt": prompt, "client_id": str(uuid.uuid4())},
                             timeout=180)
        except urllib.error.HTTPError as e:
            sampler.stop_flag.set(); sampler.join(timeout=3)
            body = e.read().decode("utf-8", "replace")[:1500]
            failed += 1
            report["runs"].append({"id": run["id"], "status": "REJECTED", "http": e.code, "body": body})
            print(f"REJECTED {run['id']}: {body[:300]}")
            continue
        prompt_id = resp["prompt_id"]

        hist, deadline = {}, t0 + args.timeout
        while time.time() < deadline:
            time.sleep(1.0)
            try:
                h = http_json(f"{args.url}/history/{prompt_id}", timeout=30)
            except Exception:
                continue
            if prompt_id in h:
                hist = h[prompt_id]
                break
        elapsed = time.time() - t0
        sampler.stop_flag.set(); sampler.join(timeout=3)

        completed = bool(((hist.get("status") or {}).get("completed")))
        image = None
        for node_out in (hist.get("outputs") or {}).values():
            for img in node_out.get("images", []):
                q = urllib.parse.urlencode({"filename": img["filename"],
                                            "subfolder": img.get("subfolder", ""),
                                            "type": img.get("type", "output")})
                local = os.path.join(args.out, f"{run['id']}.png")
                try:
                    with urllib.request.urlopen(f"{args.url}/view?{q}", timeout=120) as r:
                        with open(local, "wb") as f:
                            f.write(r.read())
                    image = local
                except Exception as e:
                    print(f"    download error: {e}")

        manifest = {
            "run_id": run["id"],
            "status": "PASS" if completed else "FAIL",
            "workflow": run["workflow"],
            "mode": run.get("purpose", ""),
            "character": run["character"],
            "checkpoint": cfg["model"]["checkpoint"],
            "checkpoint_sha256": cfg["model"]["sha256"],
            "lora": (cfg.get("style_module") or {}).get("file"),
            "lora_trigger": (cfg.get("style_module") or {}).get("trigger"),
            "lora_strength": float(run.get("lora_strength", (cfg.get("style_module") or {}).get("strength_default", 0.6))),
            "reference_image": image_name,
            "reference_path": os.path.abspath(ref_local),
            "reference_sha256": sha256_file(ref_local),
            "seed": run["seed"],
            "resolution": {"width": res["width"], "height": res["height"], "preset": run["resolution"]},
            "steps": run.get("steps", cfg["sampling"]["steps_default"]),
            "cfg": run.get("cfg", cfg["sampling"]["cfg_default"]),
            "sampler": run.get("sampler", cfg["sampling"]["sampler_name"]),
            "scheduler": run.get("scheduler", cfg["sampling"]["scheduler"]),
            "denoise": run.get("denoise"),
            "ipadapter_weight": run.get("ipadapter_weight"),
            "ipadapter_weight_type": run.get("ipadapter_weight_type"),
            "controlnet_strength": run.get("controlnet_strength"),
            "seconds": round(elapsed, 1),
            "peak_rss_mb": sampler.peak_rss_mb,
            "peak_gpu_used_mb_whole_card": sampler.peak_gpu_mb,
            "min_free_ram_gb": round(sampler.min_free_ram_gb, 2) if sampler.samples else None,
            "image": image,
            "image_sha256": sha256_file(image) if image and os.path.exists(image) else None,
            "production_use": "REVIEW_REQUIRED",
            "notes": notes,
        }
        if image and os.path.exists(image):
            manifest["closeness"] = closeness(image, ref_local)
        with open(os.path.join(args.out, f"{run['id']}.manifest.json"), "w", encoding="utf-8",
                  newline="\n") as f:
            json.dump(manifest, f, ensure_ascii=False, indent=2)
            f.write("\n")
        report["runs"].append(manifest)
        if not completed:
            failed += 1
            print(f"FAIL {run['id']}: {hist.get('status')}")
            break
        cl = manifest.get("closeness", {})
        print(f"PASS {run['id']} {res['width']}x{res['height']} seed {run['seed']} {elapsed:.1f}s "
              f"peakVRAM={manifest['peak_gpu_used_mb_whole_card']}MB peakRSS={manifest['peak_rss_mb']}MB "
              f"L1={cl.get('l1_mean_0_255')} IoU={cl.get('silhouette_iou')}")

    report["summary"] = {
        "runs": len(report["runs"]),
        "passed": sum(1 for r in report["runs"] if r.get("status") == "PASS"),
        "failed": failed,
        "total_seconds": round(sum(r.get("seconds", 0) for r in report["runs"]), 1),
        "peak_vram_mb": max((r.get("peak_gpu_used_mb_whole_card") or 0 for r in report["runs"]), default=0),
        "peak_rss_mb": max((r.get("peak_rss_mb") or 0 for r in report["runs"]), default=0),
    }
    os.makedirs(os.path.dirname(os.path.abspath(args.report)), exist_ok=True)
    with open(args.report, "w", encoding="utf-8", newline="\n") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("SUMMARY", json.dumps(report["summary"]))
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
