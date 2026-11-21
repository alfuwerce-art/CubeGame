"""PREP-03: практический smoke test ComfyUI через HTTP API (без тяжёлых моделей).

Что делает:
  1. ждёт доступности сервера ComfyUI и читает /system_stats (версии, устройство);
  2. проверяет, что веб-интерфейс отдаётся (GET /);
  3. отправляет минимальный граф из встроенных нод (EmptyImage → SaveImage),
     НЕ требующий ни одного чекпоинта/веса;
  4. ждёт завершения через /history/<id>, скачивает результат через /view;
  5. проверяет, что файл — валидный PNG и что очередь пуста;
  6. пишет машиночитаемый отчёт JSON.

Запуск (системный Python 3.13, только стандартная библиотека):
    python scripts\\comfyui\\prep03_comfyui_smoke.py --url http://127.0.0.1:8188

Скрипт не устанавливает пакеты и не меняет окружения.
"""

import argparse
import json
import os
import struct
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_OUT = os.path.join(PROJECT_ROOT, "outputs", "comfyui")
DEFAULT_REPORT = os.path.join(PROJECT_ROOT, "outputs", "reports", "prep03_comfyui_smoke.json")

SMOKE_GRAPH = {
    "1": {
        "class_type": "EmptyImage",
        "inputs": {"width": 96, "height": 96, "batch_size": 1, "color": 0x00C8FF},
    },
    "2": {
        "class_type": "SaveImage",
        "inputs": {"images": ["1", 0], "filename_prefix": "factoria_prep03_smoke"},
    },
}


def http_json(url, payload=None, timeout=30):
    data = None
    headers = {"Accept": "application/json"}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, headers=headers, method="POST" if payload else "GET")
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        body = resp.read()
        return resp.status, json.loads(body.decode("utf-8")) if body else None


def http_bytes(url, timeout=60):
    req = urllib.request.Request(url)
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return resp.status, resp.read()


def wait_server(base, timeout_s):
    t0 = time.time()
    last = None
    while time.time() - t0 < timeout_s:
        try:
            status, stats = http_json(base + "/system_stats", timeout=10)
            if status == 200 and stats:
                return stats, round(time.time() - t0, 1)
        except Exception as exc:  # сервер ещё поднимается
            last = f"{type(exc).__name__}: {exc}"
        time.sleep(2.0)
    raise RuntimeError(f"ComfyUI did not answer /system_stats in {timeout_s}s (last error: {last})")


def png_size(path):
    with open(path, "rb") as fh:
        head = fh.read(33)
    if len(head) < 33 or head[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    width, height = struct.unpack(">II", head[16:24])
    return [width, height]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--url", default="http://127.0.0.1:8188")
    ap.add_argument("--out", default=DEFAULT_OUT)
    ap.add_argument("--report", default=DEFAULT_REPORT)
    ap.add_argument("--server-timeout", type=float, default=300.0)
    ap.add_argument("--job-timeout", type=float, default=300.0)
    args = ap.parse_args()

    base = args.url.rstrip("/")
    os.makedirs(args.out, exist_ok=True)
    os.makedirs(os.path.dirname(args.report), exist_ok=True)

    report = {
        "stage": "PREP-03/comfyui_api_smoke",
        "url": base,
        "python": sys.version.split()[0],
        "stages": {},
        "numbers": {},
        "issues": [],
        "artifacts": {},
    }
    t_all = time.time()

    def stage(name, ok, detail=""):
        report["stages"][name] = {"ok": bool(ok), "detail": detail}
        print(f"STAGE {name}: {'OK' if ok else 'FAIL'} {detail}")

    # 1. сервер и версии
    try:
        stats, waited = wait_server(base, args.server_timeout)
        device = (stats.get("devices") or [{}])[0]
        report["numbers"]["server_ready_after_s"] = waited
        report["numbers"]["system_stats"] = {
            "comfyui_version": stats.get("system", {}).get("comfyui_version"),
            "python_version": stats.get("system", {}).get("python_version"),
            "pytorch_version": stats.get("system", {}).get("pytorch_version"),
            "os": stats.get("system", {}).get("os"),
            "ram_total": stats.get("system", {}).get("ram_total"),
            "ram_free": stats.get("system", {}).get("ram_free"),
            "device_name": device.get("name"),
            "device_type": device.get("type"),
            "vram_total": device.get("vram_total"),
            "vram_free": device.get("vram_free"),
            "torch_vram_total": device.get("torch_vram_total"),
        }
        stage("server_ready", True, f"waited={waited}s version={stats.get('system', {}).get('comfyui_version')}")
    except Exception as exc:
        stage("server_ready", False, f"{type(exc).__name__}: {exc}")
        _write(args.report, report, t_all, "FAIL")
        return 1

    # 2. веб-интерфейс отдаётся
    try:
        status, html = http_bytes(base + "/")
        text = html.decode("utf-8", errors="replace")
        ok = status == 200 and ("ComfyUI" in text or "<div id=\"root\"" in text or "comfyui" in text.lower())
        report["numbers"]["ui_bytes"] = len(html)
        report["numbers"]["ui_has_root_div"] = "id=\"root\"" in text
        stage("web_ui_available", ok, f"status={status} bytes={len(html)}")
    except Exception as exc:
        stage("web_ui_available", False, f"{type(exc).__name__}: {exc}")

    # 3. постановка задачи (без моделей)
    prompt_id = None
    try:
        schemas = {}
        missing = []
        for node in ("EmptyImage", "SaveImage"):
            try:
                _, info = http_json(base + f"/object_info/{node}")
                schemas[node] = sorted((info or {}).get(node, {}).get("input", {}).get("required", {}).keys())
            except Exception as exc:
                missing.append(f"{node}: {type(exc).__name__}: {exc}")
        report["numbers"]["node_schemas"] = schemas
        if missing:
            report["issues"].append("node schema lookup failed: " + "; ".join(missing))
        stage("nodes_available", len(schemas) == 2 and not missing,
              json.dumps(schemas, ensure_ascii=False))
    except Exception as exc:
        stage("nodes_available", False, f"{type(exc).__name__}: {exc}")

    try:
        payload = {"prompt": SMOKE_GRAPH, "client_id": "factoria-prep03"}
        status, resp = http_json(base + "/prompt", payload)
        prompt_id = (resp or {}).get("prompt_id")
        report["numbers"]["prompt_response"] = resp
        stage("submit_prompt", bool(prompt_id), f"status={status} prompt_id={prompt_id}")
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")[:800]
        report["issues"].append(f"POST /prompt HTTP {exc.code}: {body}")
        stage("submit_prompt", False, f"HTTP {exc.code}: {body}")
    except Exception as exc:
        stage("submit_prompt", False, f"{type(exc).__name__}: {exc}")

    # 4. ожидание выполнения
    history = None
    if prompt_id:
        t0 = time.time()
        try:
            while time.time() - t0 < args.job_timeout:
                _, hist = http_json(base + f"/history/{prompt_id}")
                if hist and prompt_id in hist:
                    history = hist[prompt_id]
                    break
                time.sleep(1.0)
            report["numbers"]["execution_seconds"] = round(time.time() - t0, 2)
            state = (history or {}).get("status", {})
            report["numbers"]["job_status"] = state
            ok = bool(history) and state.get("status_str") == "success" and bool(state.get("completed"))
            stage("workflow_executed", ok, f"{report['numbers']['execution_seconds']}s status={state.get('status_str')}")
        except Exception as exc:
            stage("workflow_executed", False, f"{type(exc).__name__}: {exc}")

    # 5. результат сохранён и скачивается
    if history:
        try:
            outputs = history.get("outputs", {})
            images = []
            for node_out in outputs.values():
                images.extend(node_out.get("images", []))
            report["numbers"]["output_images"] = images
            if not images:
                raise RuntimeError("no images in /history outputs")
            img = images[0]
            query = urllib.parse.urlencode({
                "filename": img.get("filename", ""),
                "subfolder": img.get("subfolder", ""),
                "type": img.get("type", "output"),
            })
            status, blob = http_bytes(base + "/view?" + query)
            local = os.path.join(args.out, "prep03_smoke_from_api.png")
            with open(local, "wb") as fh:
                fh.write(blob)
            dims = png_size(local)
            report["artifacts"]["image_from_api"] = local
            report["numbers"]["image_bytes"] = len(blob)
            report["numbers"]["image_dimensions"] = dims
            stage("result_saved", status == 200 and dims == [96, 96], f"{local} dims={dims} bytes={len(blob)}")
        except Exception as exc:
            stage("result_saved", False, f"{type(exc).__name__}: {exc}")

    # 6. очередь пуста (сервер не завис)
    try:
        _, queue = http_json(base + "/queue")
        running = len((queue or {}).get("queue_running", []))
        pending = len((queue or {}).get("queue_pending", []))
        stage("queue_idle", running == 0 and pending == 0, f"running={running} pending={pending}")
    except Exception as exc:
        stage("queue_idle", False, f"{type(exc).__name__}: {exc}")

    ok_all = bool(report["stages"]) and all(s["ok"] for s in report["stages"].values())
    _write(args.report, report, t_all, "PASS" if ok_all else "FAIL")
    return 0 if ok_all else 1


def _write(path, report, t_all, result):
    report["seconds_total"] = round(time.time() - t_all, 2)
    report["result"] = result
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(report, fh, ensure_ascii=False, indent=2)
    print("PREP03_REPORT=" + path)
    print("PREP03_RESULT=" + result)
    for name, st in report["stages"].items():
        print(f"PREP03_STAGE {name}={'PASS' if st['ok'] else 'FAIL'} :: {st['detail']}")
    if report["issues"]:
        print("PREP03_ISSUES=" + json.dumps(report["issues"], ensure_ascii=False))


if __name__ == "__main__":
    raise SystemExit(main())
