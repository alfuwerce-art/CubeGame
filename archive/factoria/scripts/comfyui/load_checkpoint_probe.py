#!/usr/bin/env python3
"""Load the SD1.5 checkpoint through ComfyUI's own loader on CPU - no sampling.

Proves that the downloaded file is not just structurally valid but actually **loads**
into ComfyUI's model objects (UNet / VAE / CLIP), and measures the RAM cost of that load.
Deliberately runs with ``--cpu``: zero VRAM is touched and no image is generated.

Usage:
  set FACTORIA_CKPT=D:\programs\ComfyUI\ComfyUI\models\checkpoints\v1-5-pruned-emaonly-fp16.safetensors
  python_embeded\python.exe -s load_checkpoint_probe.py
"""
from __future__ import annotations

import asyncio
import os
import sys
import time

COMFY = r"D:\programs\ComfyUI\ComfyUI"


def rss_mb() -> int:
    """Current resident set size in MB (psutil ships with the ComfyUI portable)."""
    import psutil
    return int(psutil.Process().memory_info().rss // (1024 * 1024))


def main() -> int:
    ckpt = os.environ.get("FACTORIA_CKPT", "")
    if not ckpt or not os.path.exists(ckpt):
        print(f"MISSING {ckpt}")
        return 2

    # Hide our own argv from comfy.cli_args and force CPU-only inference.
    sys.argv = [sys.argv[0], "--cpu"]
    os.chdir(COMFY)
    sys.path.insert(0, COMFY)

    import nodes  # noqa: E402
    import comfy.sd  # noqa: E402

    asyncio.run(nodes.init_extra_nodes())

    t0 = time.time()
    base_rss = rss_mb()
    model, clip, vae, clip_vision = comfy.sd.load_checkpoint_guess_config(
        ckpt, output_vae=True, output_clip=True, embedding_directory=None)
    dt = time.time() - t0

    unet_params = sum(p.numel() for p in model.model.parameters())
    class_name = type(model.model).__name__
    print(f"checkpoint={ckpt}")
    print(f"load_seconds={dt:.1f}")
    print(f"unet_class={class_name}")
    print(f"unet_params={unet_params:,}")
    print(f"vae_present={vae is not None} clip_present={clip is not None} "
          f"clip_vision_present={clip_vision is not None}")
    print(f"latent_format={getattr(model, 'latent_format', None)}")
    print(f"peak_rss_MB={rss_mb()} (before load {base_rss})")
    print("RESULT=PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
