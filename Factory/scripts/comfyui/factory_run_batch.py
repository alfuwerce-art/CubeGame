#!/usr/bin/env python3
"""Run Factory character test matrices and record metrics + manifests.

Supports two config shapes:
  * workflow set A (COMFY-STYLE-05): characters_v1.json + FACTORY_CHARACTER_{BASE|FICTIVERSE|RAL}.json
  * workflow set B (COMFY-STYLE-06): sdxl_base_v2.json + FACTORY_CHARACTER_SDXL_BASE_V2[_DIAG].json

Per run it can override: workflow file, LoRA strength, seed, resolution preset, steps, cfg,
sampler, scheduler and the prompt mode (short diagnostic vs structured long prompt).
It measures process RSS and whole-card VRAM while the prompt runs, saves the PNG and writes a
JSON manifest with every fact needed for review (model, LoRA, licence status, params, timings).

Nothing here judges the art - it only proves the graph executes and records measurements.

Usage:
  python factory_run_batch.py --url http://127.0.0.1:8190 --server-pid 12345 \
      --config workflows/factory_characters/sdxl_base_v2.json \
      --matrix workflows/factory_characters/factory_test_matrix_v2.json \
      --workflow-dir workflows/factory_characters \
      --out outputs/factory/characters_v2 \
      --report measurements/factory_batch_report_v2.json [--only id1,id2] [--skip-optional]
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from validate_factoria_workflows import ui_to_api  # noqa: E402
from build_factory_character_workflow import assemble_prompt  # noqa: E402
from build_factory_v2_workflow import long_prompt  # noqa: E402

try:
    import psutil
    import pynvml
    pynvml.nvmlInit()
except Exception:  # pragma: no cover
    psutil = None
    pynvml = None

WORKFLOW_BY_MODE = {
    "BASE": "FACTORY_CHARACTER_BASE.json",
    "FICTIVERSE": "FACTORY_CHARACTER_FICTIVERSE.json",
    "RAL": "FACTORY_CHARACTER_RAL.json",
}


def http_json(url: str, payload: dict | None = None, timeout: float = 60.0):
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    req = urllib.request.Request(url, data=data,
                                 headers={"Content-Type": "application/json"} if data else {})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        body = r.read()
    return json.loads(body.decode("utf-8")) if body else {}


def gpu_used_mb() -> int:
    if pynvml is None:
        return -1
    try:
        handle = pynvml.nvmlDeviceGetHandleByIndex(0)
        return int(pynvml.nvmlDeviceGetMemoryInfo(handle).used // (1024 * 1024))
    except Exception:
        return -1


class Sampler(threading.Thread):
    def __init__(self, pid: int, interval: float = 0.5):
        super().__init__(daemon=True)
        self.pid = pid
        self.interval = interval
        self.stop_flag = threading.Event()
        self.peak_rss_mb = 0
        self.peak_gpu_mb = -1
        self.min_free_ram_gb = 10 ** 9
        self.samples = 0

    def run(self):
        proc = psutil.Process(self.pid) if (psutil and self.pid) else None
        while not self.stop_flag.is_set():
            if proc is not None:
                try:
                    self.peak_rss_mb = max(self.peak_rss_mb, proc.memory_info().rss // (1024 * 1024))
                except Exception:
                    pass
            if psutil is not None:
                try:
                    self.min_free_ram_gb = min(self.min_free_ram_gb,
                                               psutil.virtual_memory().available / 1073741824)
                except Exception:
                    pass
            self.peak_gpu_mb = max(self.peak_gpu_mb, gpu_used_mb())
            self.samples += 1
            time.sleep(self.interval)


def resolution_of(cfg: dict, preset: str) -> dict:
    for key in ("sampling", "resolution"):
        block = cfg.get(key) or {}
        if preset in block and isinstance(block[preset], dict) and "width" in block[preset]:
            return block[preset]
    raise KeyError(f"resolution preset '{preset}' not found in config")


def prompt_for(cfg: dict, run: dict) -> tuple[str, str]:
    """Returns (positive, negative) for the workflow-set the config belongs to."""
    mode = run.get("prompt_mode", "long")
    if "diagnostic_prompt" in cfg and mode == "short":
        return cfg["diagnostic_prompt"]["positive"], cfg["diagnostic_prompt"]["negative"]
    if "prompt_long_template" in cfg:
        return long_prompt(cfg), cfg["blocks"]["NEGATIVE"]
    mode_name = run.get("mode", "BASE")
    positive = assemble_prompt(cfg, run["character"], mode_name, run.get("view", "three_quarter"))
    negative = cfg["blocks"]["NEGATIVE"]
    return positive, negative


def apply_overrides(prompt: dict, run: dict, positive: str, negative: str,
                    width: int, height: int, prefix: str, steps: int, cfg_value: float,
                    sampler_name: str, scheduler: str, strength):
    for n in prompt.values():
        title = (n.get("_meta") or {}).get("title", "").upper()
        ct = n["class_type"]
        if ct == "CLIPTextEncode":
            if "POSITIVE" in title:
                n["inputs"]["text"] = positive
            elif "NEGATIVE" in title:
                n["inputs"]["text"] = negative
        elif ct == "LoraLoader" and strength is not None:
            n["inputs"]["strength_model"] = float(strength)
            n["inputs"]["strength_clip"] = float(strength)
        elif ct == "EmptyLatentImage":
            n["inputs"]["width"] = width
            n["inputs"]["height"] = height
            n["inputs"]["batch_size"] = 1
        elif ct == "KSampler":
            n["inputs"]["seed"] = int(run["seed"])
            n["inputs"]["steps"] = int(steps)
            n["inputs"]["cfg"] = float(cfg_value)
            n["inputs"]["sampler_name"] = sampler_name
            n["inputs"]["scheduler"] = scheduler
        elif ct == "SaveImage":
            n["inputs"]["filename_prefix"] = prefix
    return prompt


def sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--url", default="http://127.0.0.1:8190")
    ap.add_argument("--server-pid", type=int, default=0)
    ap.add_argument("--config", required=True)
    ap.add_argument("--matrix", required=True)
    ap.add_argument("--workflow-dir", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--report", required=True)
    ap.add_argument("--only", default="")
    ap.add_argument("--skip-optional", action="store_true")
    ap.add_argument("--min-free-vram-mb", type=int, default=3800)
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

    def load_wf(fname: str) -> dict:
        if fname not in wf_cache:
            with open(os.path.join(args.workflow_dir, fname), "r", encoding="utf-8") as f:
                wf_cache[fname] = json.load(f)
        return wf_cache[fname]

    defaults = cfg.get("sampling") or cfg.get("sampling_sweep") or {}
    report = {
        "id": matrix.get("id", "FACTORY_TEST_MATRIX"),
        "url": args.url,
        "server_pid": args.server_pid,
        "config": args.config,
        "runs": [],
    }
    failed = 0
    for run in runs:
        if run.get("optional") and args.skip_optional:
            report["runs"].append({"id": run["id"], "status": "SKIPPED", "reason": "--skip-optional"})
            print(f"SKIP {run['id']} (optional)")
            continue
        if run.get("optional"):
            free = -1
            if pynvml is not None:
                try:
                    handle = pynvml.nvmlDeviceGetHandleByIndex(0)
                    free = int(pynvml.nvmlDeviceGetMemoryInfo(handle).free // (1024 * 1024))
                except Exception:
                    pass
            if 0 <= free < args.min_free_vram_mb:
                report["runs"].append({"id": run["id"], "status": "SKIPPED",
                                       "reason": f"free VRAM {free} MB < {args.min_free_vram_mb} MB"})
                print(f"SKIP {run['id']} (free VRAM {free} MB)")
                continue

        wf_name = run.get("workflow") or WORKFLOW_BY_MODE[run["mode"]]
        wf = load_wf(wf_name)
        res = resolution_of(cfg, run["resolution"])
        positive, negative = prompt_for(cfg, run)
        if run.get("positive_extra"):
            positive += ", " + run["positive_extra"]
        if run.get("negative_extra"):
            negative += ", " + run["negative_extra"]

        steps = int(run.get("steps") or defaults.get("steps_default") or defaults.get("steps") or 28)
        cfg_value = float(run.get("cfg") or defaults.get("cfg_default") or defaults.get("cfg") or 6.0)
        sampler_name = run.get("sampler") or defaults.get("sampler_name") or \
            ((defaults.get("samplers") or [{}])[0].get("sampler_name")) or "dpmpp_2m"
        scheduler = run.get("scheduler") or defaults.get("scheduler") or \
            ((defaults.get("samplers") or [{}])[0].get("scheduler")) or "karras"

        prompt, notes = ui_to_api(wf)
        prompt = apply_overrides(prompt, run, positive, negative, res["width"], res["height"],
                                 f"factory/{run['id']}", steps, cfg_value, sampler_name, scheduler,
                                 run.get("strength"))

        sampler = Sampler(args.server_pid)
        sampler.start()
        t0 = time.time()
        try:
            resp = http_json(f"{args.url}/prompt",
                             {"prompt": prompt, "client_id": str(uuid.uuid4())}, timeout=180)
        except urllib.error.HTTPError as e:
            sampler.stop_flag.set(); sampler.join(timeout=3)
            body = e.read().decode("utf-8", "replace")[:1500]
            failed += 1
            report["runs"].append({"id": run["id"], "status": "REJECTED", "http": e.code, "body": body})
            print(f"REJECTED {run['id']}: {body[:200]}")
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

        status = (hist.get("status") or {}) if hist else {}
        completed = bool(status.get("completed"))
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

        mode = run.get("mode") or ("FICTIVERSE" if run.get("strength") is not None else "BASE")
        manifest = {
            "run_id": run["id"],
            "status": "PASS" if completed else "FAIL",
            "workflow": wf_name,
            "prompt_mode": run.get("prompt_mode", "long"),
            "character": run.get("character"),
            "mode": mode,
            "checkpoint": wf and (cfg.get("model", {}).get("checkpoint")),
            "checkpoint_sha256": cfg.get("model", {}).get("sha256"),
            "lora": (cfg.get("style_module") or {}).get("file"),
            "lora_trigger": (cfg.get("style_module") or {}).get("trigger"),
            "lora_strength": run.get("strength"),
            "lora_license": (cfg.get("style_module") or {}).get("license"),
            "seed": run["seed"],
            "resolution": {"width": res["width"], "height": res["height"], "preset": run["resolution"]},
            "steps": steps,
            "cfg": cfg_value,
            "sampler": sampler_name,
            "scheduler": scheduler,
            "denoise": defaults.get("denoise", 1.0),
            "batch_size": 1,
            "positive_prompt": positive,
            "negative_prompt": negative,
            "seconds": round(elapsed, 1),
            "peak_rss_mb": sampler.peak_rss_mb,
            "peak_gpu_used_mb_whole_card": sampler.peak_gpu_mb,
            "min_free_ram_gb": round(sampler.min_free_ram_gb, 2) if sampler.samples else None,
            "samples": sampler.samples,
            "image": image,
            "image_sha256": sha256_file(image) if image and os.path.exists(image) else None,
            "production_use": "OK" if mode == "BASE" else "REVIEW_REQUIRED",
        }
        with open(os.path.join(args.out, f"{run['id']}.manifest.json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(manifest, f, ensure_ascii=False, indent=2)
            f.write("\n")
        report["runs"].append(manifest)
        if not completed:
            failed += 1
        print(f"{manifest['status']} {run['id']} {res['width']}x{res['height']} {steps}st cfg{cfg_value} "
              f"{sampler_name}/{scheduler} lora={run.get('strength')} {elapsed:.1f}s "
              f"peakVRAM={manifest['peak_gpu_used_mb_whole_card']}MB peakRSS={manifest['peak_rss_mb']}MB")
        if not completed:
            print(f"    status={status}")
            break

    report["summary"] = {
        "runs": len(report["runs"]),
        "passed": sum(1 for r in report["runs"] if r.get("status") == "PASS"),
        "failed": failed,
        "skipped": sum(1 for r in report["runs"] if r.get("status") == "SKIPPED"),
        "total_seconds": round(sum(r.get("seconds", 0) for r in report["runs"]), 1),
    }
    os.makedirs(os.path.dirname(os.path.abspath(args.report)), exist_ok=True)
    with open(args.report, "w", encoding="utf-8", newline="\n") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("SUMMARY", json.dumps(report["summary"]))
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
