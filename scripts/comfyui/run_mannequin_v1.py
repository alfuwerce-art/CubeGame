#!/usr/bin/env python3
"""Run one MANNEQUIN_V1 API graph on the local ComfyUI and record real RAM/VRAM peaks.

COMFY-STYLE-12 requires, per frame: exact API JSON, seed/steps/CFG/sampler/scheduler,
LoRA and ControlNet strengths, time, peak ComfyUI RSS, VRAM before/peak/after, SHA-256 of
the control guide and of the resulting PNG.

VRAM is sampled for the whole card because Windows/WDDM does not expose per-process VRAM
(recorded trap F12). The idle baseline is measured before the prompt is queued.

Usage:
  python_embeded\\python.exe scripts\\comfyui\\run_mannequin_v1.py \
      --url http://127.0.0.1:8188 --api <graph.api.json> --server-pid <pid> \
      --out outputs/factory/mannequin_v1/run1 --report measurements/comfy_style_12/RUN1.manifest.json \
      --guide-local outputs/factory/mannequin_v1/MANNEQUIN_GUIDE_V1.png \
      --guide-server D:\\programs\\ComfyUI\\ComfyUI\\input\\MANNEQUIN_GUIDE_V1.png
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

try:
    import psutil
except Exception:  # pragma: no cover
    psutil = None

try:
    import pynvml
    pynvml.nvmlInit()
except Exception:  # pragma: no cover
    pynvml = None

MODEL_DIRS = {
    "checkpoint": r"D:\programs\ComfyUI\ComfyUI\models\checkpoints",
    "lora": r"D:\programs\ComfyUI\ComfyUI\models\loras",
    "controlnet": r"D:\programs\ComfyUI\ComfyUI\models\controlnet",
}


def sha256(path: str) -> str | None:
    if not path or not os.path.isfile(path):
        return None
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


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
        proc = None
        if psutil and self.pid:
            try:
                proc = psutil.Process(self.pid)
            except Exception:
                proc = None
        while not self.stop_flag.is_set():
            if proc is not None:
                try:
                    self.peak_rss_mb = max(self.peak_rss_mb, proc.memory_info().rss // (1024 * 1024))
                except Exception:
                    proc = None
            if psutil is not None:
                try:
                    self.min_free_ram_gb = min(self.min_free_ram_gb,
                                               psutil.virtual_memory().available / 1073741824)
                except Exception:
                    pass
            self.peak_gpu_mb = max(self.peak_gpu_mb, gpu_used_mb())
            self.samples += 1
            time.sleep(self.interval)


def params_of(graph: dict) -> dict:
    ks = graph["sampler"]["inputs"]
    return {
        "checkpoint": graph["checkpoint"]["inputs"]["ckpt_name"],
        "lora": graph["lora"]["inputs"]["lora_name"],
        "lora_strength_model": graph["lora"]["inputs"]["strength_model"],
        "lora_strength_clip": graph["lora"]["inputs"]["strength_clip"],
        "controlnet": graph["controlnet"]["inputs"]["control_net_name"],
        "controlnet_union_type": graph["union_mode"]["inputs"]["type"],
        "controlnet_strength": graph["guide_conditioning"]["inputs"]["strength"],
        "controlnet_start": graph["guide_conditioning"]["inputs"]["start_percent"],
        "controlnet_end": graph["guide_conditioning"]["inputs"]["end_percent"],
        "canny_low": graph["edges"]["inputs"]["low_threshold"],
        "canny_high": graph["edges"]["inputs"]["high_threshold"],
        "seed": ks["seed"],
        "steps": ks["steps"],
        "cfg": ks["cfg"],
        "sampler": ks["sampler_name"],
        "scheduler": ks["scheduler"],
        "denoise": ks["denoise"],
        "width": graph["latent"]["inputs"]["width"],
        "height": graph["latent"]["inputs"]["height"],
        "batch_size": graph["latent"]["inputs"]["batch_size"],
        "guide_image": graph["guide"]["inputs"]["image"],
        "filename_prefix": graph["save"]["inputs"]["filename_prefix"],
        "ipadapter_nodes": sum(1 for n in graph.values() if "IPAdapter" in n["class_type"]),
        "img2img_nodes": sum(1 for n in graph.values()
                             if n["class_type"] in ("VAEEncode", "LoadImage", "ImageBatch")
                             and n["class_type"] != "LoadImage"),
    }


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--url", default="http://127.0.0.1:8188")
    ap.add_argument("--api", required=True)
    ap.add_argument("--server-pid", type=int, default=0)
    ap.add_argument("--out", required=True)
    ap.add_argument("--report", required=True)
    ap.add_argument("--case", default="MANNEQUIN_V1")
    ap.add_argument("--guide-local")
    ap.add_argument("--guide-server")
    ap.add_argument("--timeout", type=float, default=1800.0)
    ap.add_argument("--interval", type=float, default=0.5)
    args = ap.parse_args()

    with open(args.api, "r", encoding="utf-8") as f:
        graph = json.load(f)
    params = params_of(graph)

    guide_local_sha = sha256(args.guide_local) if args.guide_local else None
    guide_server_sha = sha256(args.guide_server) if args.guide_server else None
    if guide_local_sha and guide_server_sha and guide_local_sha != guide_server_sha:
        print(f"GUIDE_MISMATCH local={guide_local_sha} server={guide_server_sha}")
        return 2

    os.makedirs(args.out, exist_ok=True)
    client_id = str(uuid.uuid4())

    vram_before = gpu_used_mb()
    free_ram_before = round(psutil.virtual_memory().available / 1073741824, 2) if psutil else None
    server_rss_before = None
    if psutil and args.server_pid:
        try:
            server_rss_before = psutil.Process(args.server_pid).memory_info().rss // (1024 * 1024)
        except Exception:
            pass

    sampler = Sampler(args.server_pid, args.interval)
    sampler.start()

    t0 = time.time()
    try:
        resp = http_json(f"{args.url}/prompt", {"prompt": graph, "client_id": client_id}, timeout=180)
    except urllib.error.HTTPError as e:
        err = e.read().decode("utf-8", "replace")
        sampler.stop_flag.set()
        print(f"PROMPT_REJECTED http={e.code} {err[:2000]}")
        return 2
    prompt_id = resp.get("prompt_id")
    print(f"prompt_id={prompt_id} nodes={len(graph)}")

    deadline = t0 + args.timeout
    hist = {}
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
    sampler.stop_flag.set()
    sampler.join(timeout=3)

    vram_after = gpu_used_mb()
    server_rss_after = None
    if psutil and args.server_pid:
        try:
            server_rss_after = psutil.Process(args.server_pid).memory_info().rss // (1024 * 1024)
        except Exception:
            pass

    status = hist.get("status", {}) if hist else {}
    images = []
    for node_out in (hist.get("outputs", {}) if hist else {}).values():
        for img in node_out.get("images", []):
            q = urllib.parse.urlencode({"filename": img["filename"],
                                        "subfolder": img.get("subfolder", ""),
                                        "type": img.get("type", "output")})
            local = os.path.join(args.out, img["filename"])
            with urllib.request.urlopen(f"{args.url}/view?{q}", timeout=120) as r:
                with open(local, "wb") as f:
                    f.write(r.read())
            images.append({"file": local, "bytes": os.path.getsize(local), "sha256": sha256(local)})

    stats = {}
    try:
        stats = http_json(f"{args.url}/system_stats", timeout=30)
    except Exception:
        pass

    models = {}
    for kind, folder in MODEL_DIRS.items():
        name = {"checkpoint": params["checkpoint"], "lora": params["lora"],
                "controlnet": params["controlnet"]}[kind]
        path = os.path.join(folder, name)
        models[kind] = {"file": name, "bytes": os.path.getsize(path) if os.path.isfile(path) else None,
                        "sha256": sha256(path)}

    report = {
        "task": "COMFY-STYLE-12",
        "case": args.case,
        "api_json": args.api,
        "api_sha256": sha256(args.api),
        "prompt_id": prompt_id,
        "status": status.get("status_str", "unknown"),
        "completed": bool(status.get("completed")),
        "params": params,
        "models": models,
        "guide": {
            "local_file": args.guide_local,
            "local_sha256": guide_local_sha,
            "server_file": args.guide_server,
            "server_sha256": guide_server_sha,
            "match": (guide_local_sha == guide_server_sha) if (guide_local_sha and guide_server_sha) else None,
        },
        "runtime": {
            "comfyui": (stats.get("system", {}) or {}).get("comfyui_version"),
            "python": (stats.get("system", {}) or {}).get("python_version", "")[:40],
            "torch": (stats.get("system", {}) or {}).get("pytorch_version"),
            "device": ((stats.get("devices") or [{}])[0]).get("name"),
            "vram_total_mb": round(((stats.get("devices") or [{}])[0]).get("vram_total", 0) / 1048576),
            "url": args.url,
            "server_pid": args.server_pid,
        },
        "measurements": {
            "generation_seconds": round(elapsed, 1),
            "peak_rss_mb": sampler.peak_rss_mb,
            "rss_before_mb": server_rss_before,
            "rss_after_mb": server_rss_after,
            "free_ram_before_gb": free_ram_before,
            "min_free_ram_gb": round(sampler.min_free_ram_gb, 2) if sampler.samples else None,
            "vram_used_before_mb": vram_before,
            "vram_peak_mb": sampler.peak_gpu_mb,
            "vram_after_mb": vram_after,
            "vram_delta_peak_mb": (sampler.peak_gpu_mb - vram_before) if sampler.peak_gpu_mb >= 0 else None,
            "samples": sampler.samples,
            "method": "psutil RSS of the server process; pynvml whole-card VRAM (WDDM: no per-process VRAM)",
        },
        "images": images,
        "messages": status.get("messages", []),
    }
    os.makedirs(os.path.dirname(os.path.abspath(args.report)), exist_ok=True)
    with open(args.report, "w", encoding="utf-8", newline="\n") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
        f.write("\n")

    print(json.dumps({
        "status": report["status"], "completed": report["completed"],
        "seconds": report["measurements"]["generation_seconds"],
        "peak_rss_mb": report["measurements"]["peak_rss_mb"],
        "vram_peak_mb": report["measurements"]["vram_peak_mb"],
        "vram_delta_peak_mb": report["measurements"]["vram_delta_peak_mb"],
        "min_free_ram_gb": report["measurements"]["min_free_ram_gb"],
        "images": images,
    }, ensure_ascii=False, indent=2))
    return 0 if report["completed"] else 1


if __name__ == "__main__":
    sys.exit(main())
