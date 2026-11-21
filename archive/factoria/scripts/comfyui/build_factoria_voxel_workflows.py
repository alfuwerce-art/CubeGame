#!/usr/bin/env python3
"""Build the Factoria VOXEL_STYLE ComfyUI workflows (UI / litegraph format).

Outputs (into --out):
  FACTORIA_VOXEL_STYLE_BASE.json              checkpoint -> prompts -> latent -> KSampler -> VAE -> Save
  FACTORIA_VOXEL_STYLE_IPADAPTER.json         + LoadImage / CLIPVisionLoader / IPAdapter chain
  FACTORIA_VOXEL_STYLE_CONTROL_TEMPLATE.json  + bypassed ControlNet branch (Canny / Lineart / DW-Pose)

The graphs are generated from:
  * the VOXEL_STYLE prompt contract (workflows/factoria_voxel_style/voxel_style_prompt_v1.json)
  * the live /object_info contract of the installed ComfyUI (node input order, enums, defaults)

Generating instead of hand-writing keeps widget order correct (a saved UI graph stores
widgets positionally) and lets the validator check every enum value against the server.

Usage:
  python build_factoria_voxel_workflows.py \
      --object-info measurements/comfyui_object_info_2026-11-13_full.json \
      --config  workflows/factoria_voxel_style/voxel_style_prompt_v1.json \
      --out     workflows/factoria_voxel_style \
      --report  measurements/factoria_voxel_style_build_report.json
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.request
import uuid

LINK_TYPES = {
    "MODEL", "CLIP", "VAE", "LATENT", "IMAGE", "MASK", "CONDITIONING", "CLIP_VISION",
    "IPADAPTER", "CONTROL_NET", "UPSCALE_MODEL", "GUIDER", "SAMPLER", "SIGMAS", "NOISE",
    "AUDIO", "VIDEO", "WEBCAM", "SEGS", "DEPTH_MAP", "PIXEL_IMAGE", "VOXEL", "MESH",
    "SVG", "OBJECT", "DICT", "ANY",
}

# Node types that live in the frontend only and are therefore absent from /object_info.
FRONTEND_ONLY = {"Note": ["text"], "MarkdownNote": ["text"], "PrimitiveNode": ["value"], "Reroute": []}

# Default node box sizes (cosmetic only; the user can resize in the UI).
SIZES = {
    "CheckpointLoaderSimple": [350, 110],
    "CLIPTextEncode": [430, 200],
    "EmptyLatentImage": [330, 110],
    "KSampler": [300, 270],
    "VAEDecode": [220, 50],
    "SaveImage": [430, 480],
    "LoadImage": [330, 330],
    "CLIPVisionLoader": [330, 60],
    "IPAdapterUnifiedLoader": [330, 80],
    "IPAdapterAdvanced": [330, 260],
    "ControlNetLoader": [330, 60],
    "ControlNetApplyAdvanced": [300, 180],
    "Canny": [300, 110],
    "LineArtPreprocessor": [300, 110],
    "DWPreprocessor": [330, 260],
    "MarkdownNote": [520, 520],
}
DEFAULT_SIZE = [320, 120]


def load_object_info(src: str) -> dict:
    if src.startswith("http"):
        with urllib.request.urlopen(src, timeout=120) as r:
            return json.loads(r.read().decode("utf-8"))
    with open(src, "r", encoding="utf-8") as f:
        return json.load(f)


class Graph:
    """Minimal builder for ComfyUI UI-format (litegraph) workflows."""

    def __init__(self, object_info: dict):
        self.oi = object_info
        self.nodes: list[dict] = []
        self.links: list[list] = []
        self._next_node = 1
        self._next_link = 1

    # ---- contract helpers -------------------------------------------------
    def _contract(self, ntype: str):
        if ntype in FRONTEND_ONLY:
            return None
        if ntype not in self.oi:
            raise KeyError(f"node type not available on this ComfyUI server: {ntype}")
        return self.oi[ntype]

    @staticmethod
    def _is_widget(spec) -> bool:
        t = spec[0]
        if isinstance(t, list):          # legacy COMBO layout
            return True
        if t == "COMBO":                 # newer object_info layout (options in spec[1])
            return True
        return t in {"INT", "FLOAT", "STRING", "BOOLEAN"}

    def _widget_default(self, spec):
        t = spec[0]
        opts = spec[1] if len(spec) > 1 else {}
        if isinstance(t, list):
            return opts.get("default", t[0] if t else "")
        if t == "COMBO":
            options = opts.get("options") or []
            return opts.get("default", options[0] if options else "")
        if t == "INT":
            return opts.get("default", 0)
        if t == "FLOAT":
            return opts.get("default", 0.0)
        if t == "BOOLEAN":
            return opts.get("default", False)
        if t == "STRING":
            return opts.get("default", "")
        return opts.get("default")

    # ---- graph building ---------------------------------------------------
    def add(self, ntype: str, widgets: dict | None = None, pos=(0, 0), mode=0, title=None,
            extra_widgets: list | None = None, size=None):
        """Add a node. `widgets` is a dict of widget-name -> value (order does not matter)."""
        widgets = widgets or {}
        contract = self._contract(ntype)
        nid = self._next_node
        self._next_node += 1

        inputs, outputs, wvalues, wnamed = [], [], [], {}

        if contract is None:  # frontend-only node (MarkdownNote / Note / Reroute)
            for name in FRONTEND_ONLY[ntype]:
                val = widgets.pop(name, "")
                wvalues.append(val)
                wnamed[name] = val
        else:
            out_types = contract.get("output", []) or []
            out_names = contract.get("output_name", out_types) or out_types
            for i, t in enumerate(out_types):
                outputs.append({"name": out_names[i] if i < len(out_names) else t,
                                "type": t, "links": []})
            for group in ("required", "optional"):
                for name, spec in (contract["input"].get(group) or {}).items():
                    if self._is_widget(spec):
                        val = widgets.pop(name, "__KEEP_DEFAULT__")
                        if val == "__KEEP_DEFAULT__":
                            val = self._widget_default(spec)
                        wvalues.append(val)
                        wnamed[name] = val
                        opts = spec[1] if len(spec) > 1 else {}
                        if opts.get("control_after_generate"):
                            ctl = widgets.pop("control_after_generate", "fixed")
                            wvalues.append(ctl)
                            wnamed["control_after_generate"] = ctl
                    else:
                        typ = spec[0]
                        inputs.append({"name": name, "type": typ if isinstance(typ, str) else "COMBO",
                                       "link": None})
        if extra_widgets:
            wvalues.extend(extra_widgets)
        if widgets:
            raise ValueError(f"{ntype}: unknown widgets {sorted(widgets)}")

        node = {
            "id": nid,
            "type": ntype,
            "pos": list(pos),
            "size": list(size or SIZES.get(ntype, DEFAULT_SIZE)),
            "flags": {},
            "order": len(self.nodes),
            "mode": mode,
            "inputs": inputs,
            "outputs": outputs,
            "properties": {"Node name for S&R": ntype},
            "widgets_values": wvalues,
        }
        if wnamed:
            node["widgets_values_named"] = wnamed
        if title:
            node["title"] = title
        self.nodes.append(node)
        return nid

    def _node(self, nid: int) -> dict:
        for n in self.nodes:
            if n["id"] == nid:
                return n
        raise KeyError(nid)

    def connect(self, src_id: int, src_slot: int, dst_id: int, dst_input: str) -> int:
        src, dst = self._node(src_id), self._node(dst_id)
        out = src["outputs"][src_slot]
        idx = next((i for i, inp in enumerate(dst["inputs"]) if inp["name"] == dst_input), None)
        if idx is None:
            raise KeyError(f"{dst['type']} has no input '{dst_input}'")
        inp = dst["inputs"][idx]
        if inp["type"] != out["type"] and "*" not in (inp["type"], out["type"]):
            raise TypeError(f"type mismatch {src['type']}.{out['name']}({out['type']}) -> "
                            f"{dst['type']}.{dst_input}({inp['type']})")
        lid = self._next_link
        self._next_link += 1
        self.links.append([lid, src_id, src_slot, dst_id, idx, out["type"]])
        out["links"].append(lid)
        inp["link"] = lid
        return lid

    def to_workflow(self, name: str) -> dict:
        return {
            "id": str(uuid.uuid5(uuid.NAMESPACE_URL, f"factoria/{name}")),
            "revision": 0,
            "last_node_id": self._next_node - 1,
            "last_link_id": self._next_link - 1,
            "nodes": self.nodes,
            "links": self.links,
            "groups": [],
            "config": {},
            "extra": {"ds": {"scale": 0.55, "offset": [0, 0]}, "frontendVersion": "1.58.5"},
            "version": 0.4,
        }

    # ---- validation -------------------------------------------------------
    def validate(self) -> list[str]:
        problems: list[str] = []
        node_by_id = {n["id"]: n for n in self.nodes}
        if len(node_by_id) != len(self.nodes):
            problems.append("duplicate node ids")
        link_by_id = {}
        for lid, src, slot, dst, dslot, typ in self.links:
            link_by_id[lid] = (src, slot, dst, dslot, typ)
            s, d = node_by_id.get(src), node_by_id.get(dst)
            if s is None or d is None:
                problems.append(f"link {lid} references missing node")
                continue
            if slot >= len(s["outputs"]):
                problems.append(f"link {lid}: bad output slot {slot} on {s['type']}")
            elif lid not in s["outputs"][slot]["links"]:
                problems.append(f"link {lid}: not registered on {s['type']} output")
            if dslot >= len(d["inputs"]):
                problems.append(f"link {lid}: bad input slot {dslot} on {d['type']}")
            elif d["inputs"][dslot]["link"] != lid:
                problems.append(f"link {lid}: input of {d['type']} does not point back")

        for n in self.nodes:
            c = self._contract(n.get("type")) if n.get("type") not in FRONTEND_ONLY else None
            if c is None:
                if n["type"] not in FRONTEND_ONLY:
                    problems.append(f"unknown node type {n['type']}")
                continue
            # required link inputs must be connected
            for group in ("required", "optional"):
                for name, spec in (c["input"].get(group) or {}).items():
                    if self._is_widget(spec):
                        continue
                    inp = next((i for i in n["inputs"] if i["name"] == name), None)
                    if inp is None:
                        problems.append(f"{n['type']}#{n['id']}: missing declared input {name}")
                    elif group == "required" and inp["link"] is None:
                        problems.append(f"{n['type']}#{n['id']}: required input {name} not connected")
            # enum widget values must be valid
            named = n.get("widgets_values_named", {})
            for group in ("required", "optional"):
                for name, spec in (c["input"].get(group) or {}).items():
                    if not self._is_widget(spec):
                        continue
                    if isinstance(spec[0], list):
                        options = spec[0]
                    elif spec[0] == "COMBO":
                        options = (spec[1] or {}).get("options") or []
                    else:
                        continue
                    val = named.get(name)
                    if options and val not in options:
                        problems.append(f"{n['type']}#{n['id']}: widget {name}={val!r} not in options")
        return problems


# ---------------------------------------------------------------------------
def build_graphs(cfg: dict, oi: dict):
    gd = cfg["generation_defaults"]
    ip = cfg["ipadapter_reference"]
    ckpt = gd["checkpoint"]

    character_block = ("race/species: <fill>, sex: <fill>, body type: <fill>, "
                       "face: <fill>, clothing: <fill>, armor: <fill>, accessories: <fill>, "
                       "palette: <fill>, pose: standing neutral, camera: full body three-quarter view")
    positive_text = f"{cfg['style_prefix']} | {character_block}"
    negative_text = cfg["negative_prompt"]

    base_note = (
        "# FACTORIA VOXEL_STYLE - BASE\n\n"
        "Только базовая генерация SD1.5: без IPAdapter и без ControlNet.\n\n"
        "1. Запусти ComfyUI лаунчером `launch_safe_baseline.bat` (SAFE_BASELINE, `--novram`).\n"
        "2. Открой этот файл в UI: `Workflow -> Open` (или перетащи JSON в окно).\n"
        "3. В ноде **POSITIVE - style + character** замени только блок персонажа (после `|`).\n"
        "   Стилевой префикс не меняй - он и есть фиксация стиля.\n"
        "4. Нажми **Queue**. Результат: `ComfyUI/output/factoria/voxel_style_base_*.png`.\n\n"
        "Ресурсы: панель **Crystools** в верхней строке меню (RAM / VRAM / GPU / CPU, время хода).\n\n"
        "Лимиты этапа: RAM <= 15 GB, дополнительная VRAM <= 2 GB над baseline до запуска."
    )
    ipadapter_note = (
        "# FACTORIA VOXEL_STYLE - IPADAPTER (style reference)\n\n"
        "Reference image задаёт **визуальный voxel-язык**, а не копирует персонажа.\n\n"
        "Нужны веса (пока НЕ скачаны, см. отчёт COMFY-STYLE-02/03):\n"
        "- `models/clip_vision/CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors` (~2.5 GB)\n"
        "- `models/ipadapter/ip-adapter-plus_sd15.safetensors` (~98 MB)\n\n"
        "Порядок:\n"
        "1. Положить локальный style reference в `ComfyUI/input/` и выбрать его в ноде **LoadImage**.\n"
        "   Reference не публикуется в GitHub и не вшивается в workflow.\n"
        "2. Пока веса не установлены, нода IPAdapterUnifiedLoader покажет ошибку - это ожидаемо.\n"
        "3. Style weight по умолчанию 0.75, weight_type = `style transfer`, start 0.0 / end 1.0.\n"
        "4. Что должно сохраняться при смене персонажа: voxel/cuboid язык, иерархия блоков,\n"
        "   детализация лица блоками, жёсткие формы, студийная подача.\n"
    )
    control_note = (
        "# FACTORIA VOXEL_STYLE - CONTROL TEMPLATE\n\n"
        "Содержит IPAdapter-ветку (как в IPADAPTER workflow) плюс **отключаемую** ControlNet-ветку.\n\n"
        "По умолчанию ControlNet-нода в режиме **BYPASS** (mode 4) - базовая генерация работает без неё.\n\n"
        "Включение:\n"
        "1. Скачать веса ControlNet (пока не скачаны, см. отчёт):\n"
        "   `models/controlnet/control_v11p_sd15_canny_fp16.safetensors`,\n"
        "   `models/controlnet/control_v11p_sd15_lineart_fp16.safetensors`,\n"
        "   `models/controlnet/control_v11p_sd15_openpose_fp16.safetensors`.\n"
        "2. Выбрать файл в ноде **ControlNetLoader**.\n"
        "3. Ветку препроцессора: оставить одну из нод (Canny / Lineart / DW-Pose), лишние - MUTE.\n"
        "4. Снять BYPASS с **ControlNetApplyAdvanced** (Ctrl+B) и выставить strength/start/end.\n\n"
        "Первый запуск DW-Pose скачает onnx-модели (~250 MB) в `custom_nodes/comfyui_controlnet_aux/ckpts`.\n"
    )

    # ---------------- BASE ----------------
    g = Graph(oi)
    note = g.add("MarkdownNote", {"text": base_note}, pos=(-560, 40), title="FACTORIA - read me")
    ckpt_n = g.add("CheckpointLoaderSimple", {"ckpt_name": ckpt}, pos=(0, 60), title="CHECKPOINT SD1.5 fp16 ema-only")
    pos_n = g.add("CLIPTextEncode", {"text": positive_text}, pos=(0, 240),
                  title="POSITIVE - style + character")
    neg_n = g.add("CLIPTextEncode", {"text": negative_text}, pos=(0, 540), title="NEGATIVE")
    lat_n = g.add("EmptyLatentImage", {"width": gd["width"], "height": gd["height"],
                                       "batch_size": gd["batch_size"]}, pos=(0, 840))
    lora_n = g.add("LoraLoader", {"lora_name": "", "strength_model": 1.0, "strength_clip": 1.0},
                   pos=(0, 1020), mode=2, title="LoRA (muted - not trained yet)")
    ks_n = g.add("KSampler", {"seed": gd["seed"], "control_after_generate": "fixed",
                              "steps": gd["steps"], "cfg": gd["cfg"],
                              "sampler_name": gd["sampler_name"], "scheduler": gd["scheduler"],
                              "denoise": gd["denoise"]}, pos=(480, 240), title="KSampler (512-class, batch 1)")
    vd_n = g.add("VAEDecode", pos=(830, 240))
    save_n = g.add("SaveImage", {"filename_prefix": "factoria/voxel_style_base"}, pos=(1100, 240))
    g.connect(ckpt_n, 1, pos_n, "clip")
    g.connect(ckpt_n, 1, neg_n, "clip")
    g.connect(ckpt_n, 0, ks_n, "model")
    g.connect(ckpt_n, 0, lora_n, "model")
    g.connect(ckpt_n, 1, lora_n, "clip")
    g.connect(pos_n, 0, ks_n, "positive")
    g.connect(neg_n, 0, ks_n, "negative")
    g.connect(lat_n, 0, ks_n, "latent_image")
    g.connect(ks_n, 0, vd_n, "samples")
    g.connect(ckpt_n, 2, vd_n, "vae")
    g.connect(vd_n, 0, save_n, "images")
    base = g.to_workflow("voxel_style_base")

    # ---------------- IPADAPTER ----------------
    g = Graph(oi)
    g.add("MarkdownNote", {"text": ipadapter_note}, pos=(-900, 40), title="FACTORIA - read me (IPAdapter)")
    ckpt_n = g.add("CheckpointLoaderSimple", {"ckpt_name": ckpt}, pos=(0, 60), title="CHECKPOINT SD1.5 fp16 ema-only")
    load_n = g.add("LoadImage", {"image": ip["expected_input_filename"]}, pos=(0, 240),
                   extra_widgets=["image"], title="STYLE REFERENCE (local only)")
    cv_n = g.add("CLIPVisionLoader", {"clip_name": "CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors"},
                 pos=(0, 620), mode=2, title="CLIP VISION (muted - UnifiedLoader loads it)")
    uni_n = g.add("IPAdapterUnifiedLoader", {"preset": ip["preset"]}, pos=(420, 60),
                  title="IPAdapter loader (weights required)")
    ipa_n = g.add("IPAdapterAdvanced", {"weight": ip["weight"], "weight_type": ip["weight_type"],
                                        "combine_embeds": ip["combine_embeds"],
                                        "start_at": ip["start_at"], "end_at": ip["end_at"],
                                        "embeds_scaling": ip["embeds_scaling"]}, pos=(420, 260),
                   title="IPAdapter apply - STYLE, not identity")
    pos_n = g.add("CLIPTextEncode", {"text": positive_text}, pos=(840, 640),
                  title="POSITIVE - style + character")
    neg_n = g.add("CLIPTextEncode", {"text": negative_text}, pos=(840, 900), title="NEGATIVE")
    lat_n = g.add("EmptyLatentImage", {"width": gd["width"], "height": gd["height"],
                                       "batch_size": gd["batch_size"]}, pos=(840, 1200))
    ks_n = g.add("KSampler", {"seed": gd["seed"], "control_after_generate": "fixed",
                              "steps": gd["steps"], "cfg": gd["cfg"],
                              "sampler_name": gd["sampler_name"], "scheduler": gd["scheduler"],
                              "denoise": gd["denoise"]}, pos=(1320, 400), title="KSampler")
    vd_n = g.add("VAEDecode", pos=(1670, 400))
    save_n = g.add("SaveImage", {"filename_prefix": "factoria/voxel_style_ipadapter"}, pos=(1940, 400))
    g.connect(ckpt_n, 0, uni_n, "model")
    g.connect(uni_n, 0, ipa_n, "model")
    g.connect(uni_n, 1, ipa_n, "ipadapter")
    g.connect(load_n, 0, ipa_n, "image")
    g.connect(ipa_n, 0, ks_n, "model")
    g.connect(ckpt_n, 1, pos_n, "clip")
    g.connect(ckpt_n, 1, neg_n, "clip")
    g.connect(pos_n, 0, ks_n, "positive")
    g.connect(neg_n, 0, ks_n, "negative")
    g.connect(lat_n, 0, ks_n, "latent_image")
    g.connect(ks_n, 0, vd_n, "samples")
    g.connect(ckpt_n, 2, vd_n, "vae")
    g.connect(vd_n, 0, save_n, "images")
    ipa_wf = g.to_workflow("voxel_style_ipadapter")

    # ---------------- CONTROL TEMPLATE ----------------
    g = Graph(oi)
    g.add("MarkdownNote", {"text": control_note}, pos=(-900, 40), title="FACTORIA - read me (Control template)")
    ckpt_n = g.add("CheckpointLoaderSimple", {"ckpt_name": ckpt}, pos=(0, 60), title="CHECKPOINT SD1.5 fp16 ema-only")
    load_n = g.add("LoadImage", {"image": ip["expected_input_filename"]}, pos=(0, 240),
                   extra_widgets=["image"], title="STYLE REFERENCE (local only)")
    cv_n = g.add("CLIPVisionLoader", {"clip_name": "CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors"},
                 pos=(0, 620), mode=2, title="CLIP VISION (muted - UnifiedLoader loads it)")
    ctl_load_n = g.add("LoadImage", {"image": "factoria_control_source.png"}, pos=(0, 900),
                       extra_widgets=["image"], title="CONTROL SOURCE (silhouette / pose)")
    uni_n = g.add("IPAdapterUnifiedLoader", {"preset": ip["preset"]}, pos=(420, 60),
                  title="IPAdapter loader (weights required)")
    ipa_n = g.add("IPAdapterAdvanced", {"weight": ip["weight"], "weight_type": ip["weight_type"],
                                        "combine_embeds": ip["combine_embeds"],
                                        "start_at": ip["start_at"], "end_at": ip["end_at"],
                                        "embeds_scaling": ip["embeds_scaling"]}, pos=(420, 260),
                   title="IPAdapter apply - STYLE, not identity")
    canny_n = g.add("Canny", {"low_threshold": 0.4, "high_threshold": 0.8}, pos=(420, 900), mode=2,
                    title="Canny (MUTED by default)")
    line_n = g.add("LineArtPreprocessor", {"coarse": "disable", "resolution": 512}, pos=(420, 1080),
                   mode=2, title="Lineart (MUTED by default)")
    pose_n = g.add("DWPreprocessor", {"detect_hand": "enable", "detect_body": "enable",
                                      "detect_face": "enable", "resolution": 512,
                                      "bbox_detector": "yolox_l.onnx",
                                      "pose_estimator": "dw-ll_ucoco_384.onnx",
                                      "scale_stick_for_xinsr_cn": "disable"},
                   pos=(420, 1240), mode=2, title="DW-Pose (MUTED by default)")
    cn_load_n = g.add("ControlNetLoader", {"control_net_name": "control_v11p_sd15_canny_fp16.safetensors"},
                      pos=(840, 900), mode=2, title="ControlNet (muted - weights required)")
    pos_n = g.add("CLIPTextEncode", {"text": positive_text}, pos=(840, 260),
                  title="POSITIVE - style + character")
    neg_n = g.add("CLIPTextEncode", {"text": negative_text}, pos=(840, 540), title="NEGATIVE")
    lat_n = g.add("EmptyLatentImage", {"width": gd["width"], "height": gd["height"],
                                       "batch_size": gd["batch_size"]}, pos=(840, 1200))
    cn_apply_n = g.add("ControlNetApplyAdvanced", {"strength": 0.8, "start_percent": 0.0,
                                                   "end_percent": 1.0}, pos=(1320, 620), mode=4,
                       title="ControlNet apply (BYPASS by default)")
    ks_n = g.add("KSampler", {"seed": gd["seed"], "control_after_generate": "fixed",
                              "steps": gd["steps"], "cfg": gd["cfg"],
                              "sampler_name": gd["sampler_name"], "scheduler": gd["scheduler"],
                              "denoise": gd["denoise"]}, pos=(1740, 260), title="KSampler")
    vd_n = g.add("VAEDecode", pos=(2090, 260))
    save_n = g.add("SaveImage", {"filename_prefix": "factoria/voxel_style_control"}, pos=(2360, 260))
    g.connect(ckpt_n, 0, uni_n, "model")
    g.connect(uni_n, 0, ipa_n, "model")
    g.connect(uni_n, 1, ipa_n, "ipadapter")
    g.connect(load_n, 0, ipa_n, "image")
    g.connect(ipa_n, 0, ks_n, "model")
    g.connect(ckpt_n, 1, pos_n, "clip")
    g.connect(ckpt_n, 1, neg_n, "clip")
    # ControlNet branch: source -> preprocessors (muted) -> apply (bypassed) -> KSampler
    g.connect(ctl_load_n, 0, canny_n, "image")
    g.connect(ctl_load_n, 0, line_n, "image")
    g.connect(ctl_load_n, 0, pose_n, "image")
    g.connect(cn_load_n, 0, cn_apply_n, "control_net")
    g.connect(canny_n, 0, cn_apply_n, "image")
    g.connect(pos_n, 0, cn_apply_n, "positive")
    g.connect(neg_n, 0, cn_apply_n, "negative")
    g.connect(cn_apply_n, 0, ks_n, "positive")
    g.connect(cn_apply_n, 1, ks_n, "negative")
    g.connect(lat_n, 0, ks_n, "latent_image")
    g.connect(ks_n, 0, vd_n, "samples")
    g.connect(ckpt_n, 2, vd_n, "vae")
    g.connect(vd_n, 0, save_n, "images")
    ctl_wf = g.to_workflow("voxel_style_control_template")

    return {
        "FACTORIA_VOXEL_STYLE_BASE.json": (base, *Graph.validate_count(base, oi)),
        "FACTORIA_VOXEL_STYLE_IPADAPTER.json": (ipa_wf, *Graph.validate_count(ipa_wf, oi)),
        "FACTORIA_VOXEL_STYLE_CONTROL_TEMPLATE.json": (ctl_wf, *Graph.validate_count(ctl_wf, oi)),
    }


def _validate_count(wf: dict, oi: dict):
    """Standalone validation of a finished workflow dict.

    Returns (problems, warnings). Problems are hard defects (graph cannot load/execute);
    warnings are expected gaps such as a style-reference image that the owner has not
    placed into ComfyUI/input yet.
    """
    problems: list[str] = []
    warnings: list[str] = []
    # widgets whose value is a *file name* picked from a folder listing, not a fixed enum
    file_widgets = {"image", "ckpt_name", "clip_name", "ipadapter_file", "control_net_name",
                    "vae_name", "lora_name", "unet_name", "clip_vision_name"}
    node_by_id = {n["id"]: n for n in wf["nodes"]}
    for lid, src, slot, dst, dslot, typ in wf["links"]:
        s, d = node_by_id.get(src), node_by_id.get(dst)
        if s is None or d is None:
            problems.append(f"link {lid} references missing node")
            continue
        if slot >= len(s["outputs"]) or lid not in s["outputs"][slot]["links"]:
            problems.append(f"link {lid}: bad/absent output registration on {s['type']}")
        if dslot >= len(d["inputs"]) or d["inputs"][dslot]["link"] != lid:
            problems.append(f"link {lid}: bad input registration on {d['type']}")
        if s["outputs"][slot]["type"] != d["inputs"][dslot]["type"]:
            problems.append(f"link {lid}: type mismatch {s['outputs'][slot]['type']} != {d['inputs'][dslot]['type']}")
    for n in wf["nodes"]:
        if n["type"] in FRONTEND_ONLY:
            continue
        c = oi.get(n["type"])
        if c is None:
            problems.append(f"unknown node type {n['type']}")
            continue
        expected_widgets = 0
        for group in ("required", "optional"):
            for name, spec in (c["input"].get(group) or {}).items():
                if Graph._is_widget(spec):
                    expected_widgets += 1
                    opts = spec[1] if len(spec) > 1 else {}
                    if opts.get("control_after_generate"):
                        expected_widgets += 1
                    continue
                inp = next((i for i in n["inputs"] if i["name"] == name), None)
                if inp is None:
                    problems.append(f"{n['type']}#{n['id']}: missing declared input {name}")
                elif group == "required" and inp["link"] is None:
                    problems.append(f"{n['type']}#{n['id']}: required input {name} not connected")
        actual = len(n.get("widgets_values") or [])
        extra = 1 if n["type"] in {"LoadImage"} else 0
        if actual != expected_widgets + extra:
            problems.append(f"{n['type']}#{n['id']}: widgets_values {actual} != expected {expected_widgets + extra}")
        named = n.get("widgets_values_named", {})
        for group in ("required", "optional"):
            for name, spec in (c["input"].get(group) or {}).items():
                if not Graph._is_widget(spec):
                    continue
                if isinstance(spec[0], list):
                    options = spec[0]
                elif spec[0] == "COMBO":
                    options = (spec[1] or {}).get("options") or []
                else:
                    continue
                if not options or named.get(name) in options:
                    continue
                msg = f"{n['type']}#{n['id']}: widget {name}={named.get(name)!r} not in options"
                if name in file_widgets:
                    # A file-based combo only lists files that are already on disk.
                    warnings.append(msg + " (file not present locally yet)")
                else:
                    problems.append(msg)
    return problems, warnings


Graph.validate_count = staticmethod(_validate_count)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--object-info", required=True, help="path or URL of /api/object_info JSON")
    ap.add_argument("--config", required=True, help="VOXEL_STYLE prompt contract JSON")
    ap.add_argument("--out", required=True, help="output directory for workflow JSON files")
    ap.add_argument("--report", default=None, help="optional JSON build report path")
    args = ap.parse_args()

    oi = load_object_info(args.object_info)
    with open(args.config, "r", encoding="utf-8") as f:
        cfg = json.load(f)

    os.makedirs(args.out, exist_ok=True)
    results = build_graphs(cfg, oi)

    report = {"object_info_source": args.object_info, "config": args.config, "workflows": {}}
    rc = 0
    for filename, (wf, problems, warnings) in results.items():
        path = os.path.join(args.out, filename)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(wf, f, ensure_ascii=False, indent=2)
            f.write("\n")
        status = "PASS" if not problems else "FAIL"
        if problems:
            rc = 1
        report["workflows"][filename] = {
            "status": status, "nodes": len(wf["nodes"]), "links": len(wf["links"]),
            "problems": problems, "warnings": warnings,
        }
        print(f"{status} {filename}: nodes={len(wf['nodes'])} links={len(wf['links'])}")
        for p in problems:
            print(f"    ! {p}")
        for w in warnings:
            print(f"    ~ {w}")

    if args.report:
        os.makedirs(os.path.dirname(args.report) or ".", exist_ok=True)
        with open(args.report, "w", encoding="utf-8", newline="\n") as f:
            json.dump(report, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"report: {args.report}")
    return rc


if __name__ == "__main__":
    sys.exit(main())
