#!/usr/bin/env python3
"""Run one Factoria workflow through the ComfyUI HTTP API and measure real RAM/VRAM peaks.

Purpose (COMFY-STYLE-04 §8): a *technical* verification that the graph loads, the SDXL
checkpoint is actually used and the node links are connected - plus the only honest way to
record peak RAM / VRAM. It is not an artistic test: prompts/steps can be overridden with a
neutral throwaway value so the owner's Orc test stays the owner's.

Measures (every --interval seconds while the prompt runs):
  * process RSS of the ComfyUI server (psutil)
  * system free RAM
  * GPU used VRAM of the whole card (pynvml) - WDDM does not expose per-process VRAM

Usage:
  python xl_run_workflow.py --url http://127.0.0.1:8188 \
      --workflow workflows/dreamshaper_xl/DREAMSHAPER_XL_BASE.json \
      --server-pid 12345 --out outputs/comfyui/style04_tech_test \
      --report measurements/style04_technical_run.json \
      --override-positive "a plain grey cube on a white background" --steps 4
"""
from __future__ import annotations

import argparse
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

try:
    import psutil
except Exception:  # pragma: no cover
    psutil = None

try:
    import pynvml
    pynvml.nvmlInit()
except Exception:  # pragma: no cover
    pynvml = None


def http_json(url: str, payload: dict | None = None, timeout: float = 30.0):
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
        mem = pynvml.nvmlDeviceGetMemoryInfo(handle)
        return int(mem.used // (1024 * 1024))
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
                    rss = proc.memory_info().rss // (1024 * 1024)
                    self.peak_rss_mb = max(self.peak_rss_mb, rss)
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


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--url", default="http://127.0.0.1:8188")
    ap.add_argument("--workflow", required=True)
    ap.add_argument("--server-pid", type=int, default=0)
    ap.add_argument("--out", required=True)
    ap.add_argument("--report", required=True)
    ap.add_argument("--timeout", type=float, default=1800.0)
    ap.add_argument("--override-positive")
    ap.add_argument("--override-negative")
    ap.add_argument("--steps", type=int, default=0)
    ap.add_argument("--width", type=int, default=0)
    ap.add_argument("--height", type=int, default=0)
    ap.add_argument("--interval", type=float, default=0.5)
    args = ap.parse_args()

    with open(args.workflow, "r", encoding="utf-8") as f:
        wf = json.load(f)
    prompt, notes = ui_to_api(wf)

    # technical-test overrides (do not modify the workflow file itself).
    # Node titles are used as the stable selector: "POSITIVE ..." / "NEGATIVE".
    for n in prompt.values():
        title = (n.get("_meta") or {}).get("title", "").upper()
        if args.override_positive and title.startswith("POSITIVE"):
            n["inputs"]["text"] = args.override_positive
        if args.override_negative and title.startswith("NEGATIVE"):
            n["inputs"]["text"] = args.override_negative
    if args.steps:
        for n in prompt.values():
            if n["class_type"] == "KSampler":
                n["inputs"]["steps"] = args.steps
    if args.width or args.height:
        for n in prompt.values():
            if n["class_type"] == "EmptyLatentImage":
                if args.width: n["inputs"]["width"] = args.width
                if args.height: n["inputs"]["height"] = args.height

    os.makedirs(args.out, exist_ok=True)
    client_id = str(uuid.uuid4())
    sampler = Sampler(args.server_pid, args.interval)
    sampler.start()

    t0 = time.time()
    payload = {"prompt": prompt, "client_id": client_id}
    try:
        resp = http_json(f"{args.url}/prompt", payload, timeout=120)
    except urllib.error.HTTPError as e:
        err = e.read().decode("utf-8", "replace")
        sampler.stop_flag.set()
        print(f"PROMPT_REJECTED http={e.code} {err[:1500]}")
        return 2
    prompt_id = resp.get("prompt_id")
    print(f"prompt_id={prompt_id} nodes={len(prompt)} notes={notes}")

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
    gen_seconds = time.time() - t0
    sampler.stop_flag.set()
    sampler.join(timeout=3)

    status = (hist.get("status") or {}) if hist else {}
    outputs = hist.get("outputs", {}) if hist else {}
    images = []
    for node_id, node_out in outputs.items():
        for img in node_out.get("images", []):
            q = urllib.parse.urlencode({"filename": img["filename"],
                                        "subfolder": img.get("subfolder", ""),
                                        "type": img.get("type", "output")})
            local = os.path.join(args.out, img["filename"])
            try:
                with urllib.request.urlopen(f"{args.url}/view?{q}", timeout=120) as r:
                    with open(local, "wb") as f:
                        f.write(r.read())
                images.append({"file": local, "bytes": os.path.getsize(local)})
            except Exception as e:
                images.append({"file": img["filename"], "error": str(e)})

    report = {
        "workflow": args.workflow,
        "prompt_id": prompt_id,
        "status": status.get("status_str", "unknown"),
        "completed": status.get("completed", False),
        "prompt_nodes": len(prompt),
        "generation_seconds": round(gen_seconds, 1),
        "peak_rss_mb": sampler.peak_rss_mb,
        "peak_gpu_used_mb_whole_card": sampler.peak_gpu_mb,
        "min_free_ram_gb": round(sampler.min_free_ram_gb, 2) if sampler.samples else None,
        "samples": sampler.samples,
        "images": images,
        "overrides": {"positive": args.override_positive, "negative": args.override_negative,
                      "steps": args.steps or None, "width": args.width or None,
                      "height": args.height or None},
        "messages": status.get("messages", []),
    }
    os.makedirs(os.path.dirname(os.path.abspath(args.report)), exist_ok=True)
    with open(args.report, "w", encoding="utf-8", newline="\n") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(json.dumps({k: report[k] for k in ("status", "completed", "generation_seconds",
                                             "peak_rss_mb", "peak_gpu_used_mb_whole_card",
                                             "min_free_ram_gb", "images")}, ensure_ascii=False))
    return 0 if report["completed"] else 1


if __name__ == "__main__":
    sys.exit(main())
