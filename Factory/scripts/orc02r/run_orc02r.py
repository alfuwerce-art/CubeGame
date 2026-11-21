#!/usr/bin/env python3
"""ORC-02R: одна команда — измерения → новая геометрия → Blender → .blend/.glb/3 вида → отчёт.

    python scripts\orc02r\run_orc02r.py \
        --reference measurements\orc_reference.json \
        --profile scripts\orc02r\profiles\orc.json \
        --out outputs\prototypes\orc_02r\run1

Стадии:
  1. reference   — если JSON измерений отсутствует/--remeasure, снять числа с существующего Orc (read-only)
  2. generate    — NEW geometry: GLB + descriptor.json
  3. determinism — второй прогон генератора должен дать байт-идентичный GLB
  4. variation   — изменение Orc Profile должно менять геометрию
  5. blender     — headless: валидация, .blend, .glb, front/side/3-4
  6. report      — сводный report.json (+ печать в stdout)

Ручных кликов в Blender нет. Внешние AI/GPU-модели не используются.
"""

from __future__ import annotations

import argparse
import ctypes
import ctypes.wintypes as wintypes
import hashlib
import json
import os
import shutil
import subprocess
import sys
import threading
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
BLENDER = r"D:\programs\Blender\5.2\blender.exe"
BLENDER_CONFIG = os.path.join(ROOT, "environments", "blender", "config")


def sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


# --------------------------------------------------------------- RAM/VRAM ------
class _PMC(ctypes.Structure):
    _fields_ = [("cb", wintypes.DWORD), ("PageFaultCount", wintypes.DWORD),
                ("PeakWorkingSetSize", ctypes.c_size_t), ("WorkingSetSize", ctypes.c_size_t),
                ("QuotaPeakPagedPoolUsage", ctypes.c_size_t), ("QuotaPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
                ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
                ("PagefileUsage", ctypes.c_size_t), ("PeakPagefileUsage", ctypes.c_size_t)]


class MemorySampler(threading.Thread):
    """Считает пик рабочего набора процесса (Windows psapi)."""

    def __init__(self, pid: int, interval: float = 0.5):
        super().__init__(daemon=True)
        self.pid = pid
        self.interval = interval
        self.peak_bytes = 0
        self.peak_pagefile = 0
        self._stop = threading.Event()

    def run(self):
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        psapi = ctypes.WinDLL("psapi", use_last_error=True)
        handle = kernel32.OpenProcess(0x1000 | 0x0400, False, self.pid)
        if not handle:
            return
        try:
            while not self._stop.is_set():
                counters = _PMC()
                counters.cb = ctypes.sizeof(_PMC)
                if psapi.GetProcessMemoryInfo(handle, ctypes.byref(counters), counters.cb):
                    self.peak_bytes = max(self.peak_bytes, counters.PeakWorkingSetSize)
                    self.peak_pagefile = max(self.peak_pagefile, counters.PeakPagefileUsage)
                self._stop.wait(self.interval)
        finally:
            kernel32.CloseHandle(handle)

    def stop(self):
        self._stop.set()


def vram_used_mb():
    try:
        out = subprocess.run(["nvidia-smi", "--query-gpu=memory.used",
                              "--format=csv,noheader,nounits"],
                             capture_output=True, text=True, timeout=20)
        return int(out.stdout.strip().splitlines()[0])
    except Exception:
        return None


# ------------------------------------------------------------------ стадии -----
def run(cmd, log_prefix, env_extra=None, capture=True):
    env = os.environ.copy()
    if env_extra:
        env.update(env_extra)
    t0 = time.time()
    proc = subprocess.run(cmd, cwd=ROOT, capture_output=capture, text=True, env=env)
    dt = round(time.time() - t0, 2)
    out = (proc.stdout or "") + ((proc.stderr or "") if proc.stderr else "")
    print(f"[{log_prefix}] exit={proc.returncode} {dt}s")
    for line in out.splitlines():
        if any(tag in line for tag in ("GENERATE_OK", "GENERATE_ERROR", "REFERENCE_OK",
                                       "ORC02R_VALIDATION", "ORC02R_STAGE", "Error", "Traceback")):
            print("   " + line.strip())
    return proc.returncode, out, dt


def read_json(path):
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="ORC-02R one-command pipeline (new Orc geometry)")
    ap.add_argument("--reference", default=os.path.join("measurements", "orc_reference.json"))
    ap.add_argument("--profile", default=os.path.join("scripts", "orc02r", "profiles", "orc.json"))
    ap.add_argument("--out", required=True)
    ap.add_argument("--remeasure", action="store_true", help="пересчитать reference measurements")
    ap.add_argument("--seed", type=int, default=None, help="переопределить seed профиля")
    ap.add_argument("--skip-blender", action="store_true")
    args = ap.parse_args(argv)

    out = os.path.abspath(args.out)
    os.makedirs(out, exist_ok=True)
    # чистим только собственные артефакты прошлых прогонов (генератор не пишет в непустой каталог)
    for name in ("orc_new.glb", "descriptor.json", "validation.json", "report.json", "blender_log.txt"):
        path = os.path.join(out, name)
        if os.path.isfile(path):
            os.remove(path)
    for name in ("blender", "_determinism", "_variation"):
        path = os.path.join(out, name)
        if os.path.isdir(path):
            shutil.rmtree(path)
    report = {"task": "ORC-02R", "started": time.strftime("%Y-%m-%dT%H:%M:%S"),
              "stages": {}, "artifacts": {}, "status": "IN_PROGRESS", "blockers": []}
    t_all = time.time()

    def stage(name, ok, detail="", **extra):
        report["stages"][name] = {"ok": bool(ok), "detail": detail, **extra}
        if not ok:
            report["blockers"].append(f"{name}: {detail}")
        print(f"STAGE {name}: {'OK' if ok else 'FAIL'} {detail}")

    # 1. reference
    ref_path = os.path.abspath(args.reference)
    if args.remeasure or not os.path.isfile(ref_path):
        code, _, dt = run([sys.executable, os.path.join(HERE, "measure_reference.py"),
                           "--out", args.reference], "reference")
        stage("reference_measurement", code == 0 and os.path.isfile(ref_path),
              f"{args.reference} за {dt}s", seconds=dt)
    else:
        stage("reference_measurement", True, f"используется существующий {args.reference}")

    # 2. generate NEW geometry
    gen_cmd = [sys.executable, os.path.join(HERE, "generate_orc.py"),
               "--measurements", ref_path, "--profile", args.profile,
               "--out", os.path.join(out), "--label", "ORC-02R main"]
    if args.seed is not None:
        gen_cmd += ["--override", f"seed={args.seed}"]
    code, gen_out, gen_s = run(gen_cmd, "generate")
    descriptor_path = os.path.join(out, "descriptor.json")
    ok_gen = code == 0 and os.path.isfile(descriptor_path)
    descriptor = read_json(descriptor_path) if ok_gen else {}
    stage("generate_new_geometry", ok_gen,
          f"{descriptor.get('totals', {}).get('voxels')} вокселей, "
          f"{descriptor.get('totals', {}).get('boxes')} кубоидов, "
          f"{descriptor.get('outputs', {}).get('glb_bytes')} байт GLB", seconds=gen_s)
    if not ok_gen:
        report["status"] = "BLOCKED"
        report["finished"] = time.strftime("%Y-%m-%dT%H:%M:%S")
        with open(os.path.join(out, "report.json"), "w", encoding="utf-8") as fh:
            json.dump(report, fh, ensure_ascii=False, indent=2, sort_keys=True)
        return 2

    main_sha = descriptor["outputs"]["glb_sha256"]

    # 3. determinism
    det_dir = os.path.join(out, "_determinism")
    if os.path.isdir(det_dir):
        shutil.rmtree(det_dir)
    code, _, det_s = run([sys.executable, os.path.join(HERE, "generate_orc.py"),
                          "--measurements", ref_path, "--profile", args.profile,
                          "--out", det_dir, "--label", "ORC-02R determinism"],
                         "determinism")
    det_descriptor = os.path.join(det_dir, "descriptor.json")
    det_sha = read_json(det_descriptor)["outputs"]["glb_sha256"] if os.path.isfile(det_descriptor) else None
    det_same = det_sha == main_sha
    stage("determinism", code == 0 and det_same, f"sha256 run1={main_sha[:12]} run2={(det_sha or '')[:12]}",
          seconds=det_s, identical=det_same)

    # 4. profile variation
    var_dir = os.path.join(out, "_variation")
    if os.path.isdir(var_dir):
        shutil.rmtree(var_dir)
    code, _, var_s = run([sys.executable, os.path.join(HERE, "generate_orc.py"),
                          "--measurements", ref_path, "--profile", args.profile,
                          "--out", var_dir, "--label", "ORC-02R profile variation",
                          "--override", "widths.chest_width=19",
                          "--override", "face.jaw_width=12",
                          "--override", "armor.pauldron_side=RIGHT"],
                         "variation")
    var_descriptor = os.path.join(var_dir, "descriptor.json")
    var = read_json(var_descriptor) if os.path.isfile(var_descriptor) else {}
    var_sha = var.get("outputs", {}).get("glb_sha256")
    var_diff = bool(var_sha) and var_sha != main_sha
    var_boxes = var.get("totals", {}).get("boxes")
    diff_boxes = (var_boxes or 0) - descriptor["totals"]["boxes"]
    stage("profile_variation", code == 0 and var_diff,
          f"изменение профиля даёт другой GLB (sha256 {str(var_sha)[:12]}), "
          f"кубоидов {var_boxes} ({diff_boxes:+d})", seconds=var_s, different=var_diff)

    # 5. Blender
    if not args.skip_blender:
        sampler = None
        vram_before = vram_used_mb()
        t0 = time.time()
        env = {"BLENDER_USER_CONFIG": BLENDER_CONFIG, "PYTHONIOENCODING": "utf-8"}
        proc = subprocess.Popen([BLENDER, "--background", "--factory-startup",
                                 "--python-exit-code", "3", "--python",
                                 os.path.join(ROOT, "scripts", "blender", "orc02r_validate.py"),
                                 "--", "--run", out],
                                cwd=ROOT, env={**os.environ, **env},
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        sampler = MemorySampler(proc.pid)
        sampler.start()
        blended_out = []
        for line in proc.stdout:
            blended_out.append(line)
            if any(tag in line for tag in ("STAGE", "VALIDATION", "ORC02R_REPORT")):
                print("   " + line.rstrip())
        proc.wait()
        sampler.stop()
        sampler.join(timeout=2)
        blender_seconds = round(time.time() - t0, 2)
        vram_after = vram_used_mb()
        vram_peak = None
        if vram_before is not None and vram_after is not None:
            vram_peak = max(0, vram_after - vram_before)
        validation_path = os.path.join(out, "validation.json")
        validation = read_json(validation_path) if os.path.isfile(validation_path) else {}
        ok_blender = proc.returncode == 0 and validation.get("result") == "PASS"
        stage("blender", ok_blender,
              f"exit={proc.returncode}, result={validation.get('result')}, "
              f"{blender_seconds}s, пик RAM {round(sampler.peak_bytes / 1048576)} МБ",
              seconds=blender_seconds,
              ram_peak_mb=round(sampler.peak_bytes / 1048576, 1),
              vram_delta_mb=vram_peak,
              stages={k: v["ok"] for k, v in validation.get("stages", {}).items()})
        report["artifacts"]["validation"] = validation_path
        report["artifacts"]["blend"] = os.path.join(out, "blender", "orc_new.blend")
        report["artifacts"]["roundtrip_glb"] = os.path.join(out, "blender", "orc_new_roundtrip.glb")
        report["artifacts"]["renders"] = [os.path.join(out, "blender", f"orc_new_{v}.png")
                                          for v in ("front", "side", "three_quarter")]
        report["resources"] = {"blender_ram_peak_mb": round(sampler.peak_bytes / 1048576, 1),
                               "blender_pagefile_peak_mb": round(sampler.peak_pagefile / 1048576, 1),
                               "vram_delta_mb": vram_peak,
                               "blender_seconds": blender_seconds,
                               "generator_seconds": gen_s}
        with open(os.path.join(out, "blender_log.txt"), "w", encoding="utf-8") as fh:
            fh.writelines(blended_out)
    else:
        stage("blender", True, "пропущено по --skip-blender")

    # 6. отчёт
    ok_all = all(s["ok"] for s in report["stages"].values())
    report["finished"] = time.strftime("%Y-%m-%dT%H:%M:%S")
    report["wall_seconds"] = round(time.time() - t_all, 2)
    report["result"] = "PASS" if ok_all else "FAIL"
    report["status"] = "MODEL_PENDING_REVIEW" if ok_all else "PARTIAL"
    report["artifacts"]["glb"] = descriptor["outputs"]["glb"]
    report["artifacts"]["descriptor"] = descriptor_path
    report["numbers"] = {"main_glb_sha256": main_sha,
                         "determinism_glb_sha256": det_sha,
                         "variation_glb_sha256": var_sha,
                         "modules": descriptor["totals"]["modules"],
                         "voxels": descriptor["totals"]["voxels"],
                         "boxes": descriptor["totals"]["boxes"],
                         "levels": descriptor.get("levels"),
                         "reference_check": descriptor.get("reference_check"),
                         "joint_gaps_voxels": descriptor.get("joints", {}).get("gaps_voxels")}
    with open(os.path.join(out, "report.json"), "w", encoding="utf-8") as fh:
        json.dump(report, fh, ensure_ascii=False, indent=2, sort_keys=True)
    print("RUN_ORC02R=" + report["result"] + " status=" + report["status"])
    print("RUN_REPORT=" + os.path.join(out, "report.json"))
    return 0 if ok_all else 3


if __name__ == "__main__":
    sys.exit(main())
