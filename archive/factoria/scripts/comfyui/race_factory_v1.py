"""Run or rebuild COMFY-STYLE-08 prompt graphs locally."""
from concurrent.futures import ThreadPoolExecutor
from hashlib import file_digest
from pathlib import Path
import argparse
import copy
import json
import subprocess
import time
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
TEMPLATE = ROOT / "workflows" / "race_factory_v1" / "race_template.api.json"
RACES = ("HUMAN", "ELF", "DWARF", "GOBLIN", "DRAGONKIN", "ORC", "SKELETON", "FILDOR_ENDERMAN")

def read(path):
    return json.loads(path.read_text(encoding="utf-8"))

def digest(path):
    with path.open("rb") as stream:
        return file_digest(stream, "sha256").hexdigest()

def build(profile, template):
    graph = copy.deepcopy(template)
    graph["4"]["inputs"]["text"] = graph["4"]["inputs"]["text"].format(
        race_anatomy=profile["race_anatomy"], appearance=profile["appearance"])
    graph["6"]["inputs"]["image"] = profile["prepared_reference"]
    graph["15"]["inputs"]["filename_prefix"] = "style08/" + profile["race_id"].lower()
    if graph["4"]["inputs"]["text"] != profile["prompt"] or graph["5"]["inputs"]["text"] != profile["negative_prompt"]:
        raise ValueError("Profile prompt differs from the graph")
    expected = profile["seed"], profile["steps"], profile["cfg"], profile["sampler"], profile["scheduler"], profile["denoise"]
    if tuple(graph["13"]["inputs"][key] for key in ("seed", "steps", "cfg", "sampler_name", "scheduler", "denoise")) != expected:
        raise ValueError("Profile sampler differs from the graph")
    return graph

def request(base, endpoint, data=None):
    payload = json.dumps(data).encode() if data is not None else None
    req = urllib.request.Request(base + endpoint, payload, {"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=40) as resp:
        return json.load(resp)

def vram_mib():
    value = subprocess.check_output(
        ["nvidia-smi", "--query-gpu=memory.used", "--format=csv,noheader,nounits"], text=True)
    return int(value.splitlines()[0].strip())

def run(base, graph, out, race, pid, timeout):
    import psutil

    initial_gpu = vram_mib()
    initial_rss = psutil.Process(pid).memory_info().rss
    initial_available = psutil.virtual_memory().available
    peak_gpu, peak_rss, min_available = initial_gpu, initial_rss, initial_available
    start = time.monotonic()
    prompt_id = request(base, "/prompt", {"prompt": graph})["prompt_id"]
    history = {}
    with ThreadPoolExecutor(max_workers=1) as pool:
        gpu = pool.submit(vram_mib)
        while time.monotonic() - start < timeout:
            peak_rss = max(peak_rss, psutil.Process(pid).memory_info().rss)
            min_available = min(min_available, psutil.virtual_memory().available)
            if gpu.done():
                peak_gpu = max(peak_gpu, gpu.result())
                gpu = pool.submit(vram_mib)
            history = request(base, "/history/" + prompt_id).get(prompt_id)
            if history:
                break
            time.sleep(1)
        peak_gpu = max(peak_gpu, gpu.result())
    if not history or history["status"]["status_str"] != "success":
        raise RuntimeError(f"{race} {prompt_id}: {history and history.get('status')}")
    images = history["outputs"]["15"]["images"]
    if len(images) != 1:
        raise RuntimeError(f"{race}: expected one output")
    image = out / images[0]["subfolder"] / images[0]["filename"]
    from PIL import Image
    with Image.open(image) as png:
        png.load()
        if png.size != (768, 1024) or json.loads(png.info["prompt"]) != graph:
            raise RuntimeError(f"{race}: PNG dimensions or executed prompt differ")
    return {
        "prompt_id": prompt_id, "file": image.name, "sha256": digest(image), "size": [768, 1024],
        "seconds": round(time.monotonic() - start, 1),
        "vram_baseline_whole_card_mib": initial_gpu, "vram_peak_whole_card_mib": peak_gpu,
        "rss_baseline_mib": round(initial_rss / 2**20), "rss_peak_mib": round(peak_rss / 2**20),
        "available_ram_baseline_mib": round(initial_available / 2**20),
        "available_ram_min_mib": round(min_available / 2**20), "sampling_interval_seconds": 1,
    }

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-dir", type=Path, required=True)
    parser.add_argument("--race", nargs="+", choices=RACES, default=RACES)
    parser.add_argument("--url")
    parser.add_argument("--comfy-input", type=Path)
    parser.add_argument("--comfy-output", type=Path)
    parser.add_argument("--server-pid", type=int)
    parser.add_argument("--timeout", type=int, default=600)
    args = parser.parse_args()
    if args.url and not all([args.comfy_input, args.comfy_output, args.server_pid]):
        parser.error("Running requires --comfy-input, --comfy-output and --server-pid")
    args.build_dir.mkdir(parents=True, exist_ok=True)
    template = read(TEMPLATE)
    for race in args.race:
        profile = read(ROOT / "races" / race / "generation_profile.json")
        graph = build(profile, template)
        (args.build_dir / f"{race}.api.json").write_text(json.dumps(graph, indent=2) + "\n", encoding="utf-8")
        if args.url:
            if digest(args.comfy_input / profile["prepared_reference"]) != profile["prepared_reference_sha256"]:
                raise ValueError(f"{race}: reference SHA-256 mismatch")
            result = run(args.url, graph, args.comfy_output, race, args.server_pid, args.timeout)
            (args.build_dir / f"{race}.result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
            print(race, result, flush=True)

if __name__ == "__main__":
    main()
