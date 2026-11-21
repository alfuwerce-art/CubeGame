#!/usr/bin/env python3
"""Cheap structural check of the SD1.5 checkpoint (no sampling, no GPU work).

Reads only the safetensors header/metadata and verifies that the tensor names match a
Stable Diffusion 1.5 single-file checkpoint (UNet + VAE + CLIP text encoder present).
This proves the downloaded file is a usable checkpoint rather than a truncated or
wrong-format file, without generating any image.

Usage:
  python check_sd15_checkpoint.py "D:\\programs\\ComfyUI\\ComfyUI\\models\\checkpoints\\v1-5-pruned-emaonly-fp16.safetensors"
"""
import json
import os
import sys

PREFIXES = {
    "unet": "model.diffusion_model.",
    "vae": "first_stage_model.",
    "clip": "cond_stage_model.",
}


def main() -> int:
    path = sys.argv[1]
    if not os.path.exists(path):
        print(f"MISSING {path}")
        return 2
    from safetensors import safe_open

    size = os.path.getsize(path)
    with safe_open(path, framework="pt", device="cpu") as f:
        keys = list(f.keys())
        meta = f.metadata() or {}

    found = {}
    for name, prefix in PREFIXES.items():
        hits = [k for k in keys if k.startswith(prefix)]
        found[name] = len(hits)

    dtypes = set()
    with safe_open(path, framework="pt", device="cpu") as f:
        for k in keys[:50]:
            dtypes.add(str(f.get_slice(k).get_dtype()))

    print(f"path={path}")
    print(f"size_bytes={size}")
    print(f"tensors={len(keys)}")
    for name, n in found.items():
        print(f"  {name}_tensors={n}")
    print(f"sample_dtypes={sorted(dtypes)}")
    print(f"metadata_keys={sorted(meta.keys())}")
    ok = all(v > 0 for v in found.values())
    print("RESULT=" + ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
