"""Compile one Character Factory view to ComfyUI API JSON. Does not submit jobs."""
from argparse import ArgumentParser
from hashlib import sha256
from pathlib import Path, PurePosixPath, PureWindowsPath
import json
import re

REPO = Path(__file__).resolve().parents[2]
FACTORY = REPO / "workflows" / "character_factory"
CATALOG = REPO / "data" / "alpha_character_editor"
TEMPLATES = {
    "BASE_BODY": "base_body/BASE_BODY_V1.json",
    "FACE_MODULE": "face/FACE_MODULE_V1.json",
    "HAIRCUT_MODULE": "haircut/HAIRCUT_MODULE_V1.json",
}
VIEWS = {
    "FRONT": "exact orthographic front view of the same subject",
    "BACK": "exact orthographic rear view of the same subject facing away",
    "LEFT": "exact orthographic view from the subject's anatomical left",
    "RIGHT": "exact orthographic view from the subject's anatomical right",
    "TOP": "exact orthographic plan view from above, head facing the top edge of the image",
}


def read_json(path):
    return json.loads(path.read_bytes())


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def arg_parser():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--asset", required=True, choices=TEMPLATES)
    parser.add_argument("--race", required=True, type=str.upper)
    parser.add_argument("--gender", required=True, type=str.upper, choices=("MALE", "FEMALE"))
    parser.add_argument("--view", default="FRONT", type=str.upper, choices=VIEWS)
    parser.add_argument("--option", type=int, help="Alpha UI number, starting at 1; modules only")
    parser.add_argument("--profile", type=Path, help="Override with another catalog-backed profile")
    parser.add_argument("--reference-pack", type=Path, required=True)
    parser.add_argument("--socket-pack", type=Path, help="Shared head carriers for all Face and Haircut options")
    parser.add_argument("--input-dir", type=Path, required=True, help="Local ComfyUI input directory")
    parser.add_argument("--out", type=Path, required=True, help="Destination for compiled graph and manifest")
    parser.add_argument("--base-img2img", action="store_true", help="Use a same-view anchor plate for Base Body")
    parser.add_argument("--ipadapter", action="store_true", help="Add identity conditioning from the pack's identity image")
    return parser


def select_option(record, asset, ui_number):
    if asset == "BASE_BODY":
        if ui_number is not None:
            raise ValueError("BASE_BODY has no Alpha option number")
        return None
    key = "faces" if asset == "FACE_MODULE" else "haircuts"
    match = next((item for item in record[key] if item["ui_number"] == ui_number), None)
    if match is None or not match["exists"]:
        raise ValueError(f"Unavailable Alpha {key} option {ui_number}")
    return match


def image_inputs(root, view_files, frame, identity_reference):
    from PIL import Image

    frame_size = frame["width"], frame["height"]
    paths = {}
    for role, name in view_files.items():
        if not isinstance(name, str) or "\\" in name or "[" in name or "]" in name:
            raise ValueError(f"{role} must be an unannotated relative input PNG path")
        relative = PurePosixPath(name)
        if relative.is_absolute() or PureWindowsPath(name).drive or ".." in relative.parts or relative.suffix.lower() != ".png":
            raise ValueError(f"{role} must be an unannotated relative input PNG path")
        candidate = (root / name).resolve(strict=True)
        if not candidate.is_relative_to(root):
            raise ValueError(f"{role} resolves outside the ComfyUI input folder")
        data = candidate.read_bytes()
        with Image.open(candidate) as image:
            if image.format != "PNG" or image.n_frames != 1:
                raise ValueError(f"{role} must be one PNG frame")
            if role != "identity_reference" and image.size != frame_size:
                raise ValueError(f"{role} must be {frame_size[0]}x{frame_size[1]}")
            if role in ("edit_mask", "module_matte"):
                values = set(image.convert("RGB").getchannel("R").getextrema())
                if values != {0, 255}:
                    raise ValueError(f"{role} must include both white and black regions")
            image.load()
        paths[role] = {"file": relative.as_posix(), "sha256": sha256(data).hexdigest()}

    if identity_reference and "identity_reference" not in paths:
        raise ValueError("--ipadapter requires an identity_reference in the pack")
    return paths


def bind(value, parameters):
    if isinstance(value, str) and value.startswith("$"):
        return parameters[value[1:]]
    if isinstance(value, dict):
        return {key: bind(item, parameters) for key, item in value.items()}
    if isinstance(value, list):
        return [bind(item, parameters) for item in value]
    return value


def add_node(graph, key, class_type, **inputs):
    graph[key] = {"class_type": class_type, "inputs": inputs}


def compile_view(args):
    if re.fullmatch(r"[A-Z][A-Z0-9_]*", args.race) is None:
        raise ValueError("Race must be an Alpha race identifier")
    if args.base_img2img and args.asset != "BASE_BODY":
        raise ValueError("--base-img2img applies to BASE_BODY")
    template = read_json(FACTORY / TEMPLATES[args.asset])
    if args.view not in template["allowed_views"]:
        raise ValueError(f"{args.view} is not available for {args.asset}")
    catalog_path = CATALOG / args.race / f"{args.gender}.json"
    record = read_json(catalog_path)
    option = select_option(record, args.asset, args.option)
    profile_path = args.profile or FACTORY / "profiles" / args.race / f"{args.gender}.json"
    profile = read_json(profile_path)
    if (profile["race"], profile["gender"], profile["catalog_path"]) != (
        args.race, args.gender, catalog_path.relative_to(REPO).as_posix()
    ):
        raise ValueError("Profile and Alpha catalog do not describe the requested race/gender")
    for allowed, catalog_key in (("permitted_face", "faces"), ("permitted_haircut", "haircuts")):
        if profile[allowed]["observed_count"] != len(record[catalog_key]):
            raise ValueError(f"{allowed} count has drifted from the Alpha catalog")

    style = read_json(FACTORY / "shared" / "STYLE_LOCK_V1.json")
    pack = read_json(args.reference_pack)
    socket_key = "body_socket" if args.asset == "BASE_BODY" else "head_socket"
    socket_id = profile[socket_key]["id"]
    if [pack["race"], pack["gender"], pack["socket_id"], pack["asset_type"], pack.get("option_ui_number")] != [
        args.race, args.gender, socket_id, args.asset, args.option
    ]:
        raise ValueError("Reference pack race/gender, socket, asset type or Alpha option differs")
    view = pack["views"][args.view]
    carrier = None
    if option is not None:
        if args.socket_pack is None:
            raise ValueError("Modules require a shared --socket-pack")
        shared_socket = read_json(args.socket_pack)
        identity_keys = ("race", "gender", "identity_id", "socket_id", "geometry_revision", "camera_revision")
        if any(shared_socket[key] != pack[key] for key in identity_keys):
            raise ValueError("Socket pack and reference pack identify different carriers")
        carrier = shared_socket["views"][args.view]
    elif args.socket_pack is not None:
        raise ValueError("--socket-pack applies to Face and Haircut only")
    files = {"geometry_guide": view["geometry_guide"]}
    if args.base_img2img:
        files["anchor_plate"] = view["anchor_plate"]
    if option is not None:
        files.update(carrier=carrier["file"], edit_mask=view["edit_mask"], module_matte=view["module_matte"])
    if args.ipadapter:
        files["identity_reference"] = pack["identity_reference"]
    paths = image_inputs(args.input_dir.resolve(strict=True), files, style["frame"], args.ipadapter)

    if carrier is not None and carrier["sha256"] != paths["carrier"]["sha256"]:
        raise ValueError("Head carrier SHA-256 differs from the shared socket pack")

    identity = pack["identity_id"]
    if not isinstance(identity, str) or re.fullmatch(r"[\w-]+", identity, re.ASCII) is None:
        raise ValueError("identity_id must contain only ASCII letters, digits, underscores or dashes")
    parts = [pack[key] for key in ("geometry_revision", "camera_revision")]
    if not all(isinstance(part, str) and part.strip() for part in parts):
        raise ValueError("geometry_revision and camera_revision must be recorded")

    geometry, frame = style["geometry"], style["frame"]
    anatomy = profile["anatomy"]
    description = ""
    if option is not None:
        description = f"Alpha {args.asset.removesuffix('_MODULE')} {args.option:02}: {option['description']}"
        if args.asset == "HAIRCUT_MODULE":
            features = [option["visual_type"], *option["secondary_features"]]
            description += " Visual features: " + ", ".join(features)
    if args.asset == "BASE_BODY":
        anatomy_text = (anatomy["body_build"],)
        geometry_fields = ("language", "block_density", "block_size_hierarchy", "proportions", "render")
    else:
        anatomy_text = (anatomy["head_shape"], anatomy["ears"], anatomy["jaw"], *anatomy["racial_features"])
        if args.asset == "HAIRCUT_MODULE":
            anatomy_text = (anatomy["skin"], *anatomy_text)
        geometry_fields = ("language", "block_density", "block_size_hierarchy", "proportions", "texture", "render")
    geometry_text = tuple(geometry[key] for key in geometry_fields)
    positive = ", ".join(filter(None, (*geometry_text, frame["camera"], frame["background"]["description"],
                                      frame["lighting"], VIEWS[args.view], template["positive"], *anatomy_text, description)))
    negative = style["negative_prompt"] + ", " + template["negative"]
    ctl = style["controlnet"]
    stem = f"{args.race}_{args.gender}_{args.asset}"
    if args.option is not None:
        stem += f"_{args.option:02}"
    prefix = f"character_factory_v1/{identity}/{stem}_{args.view}"
    sampling = dict(style["sampling"])
    if args.asset != "BASE_BODY" or args.base_img2img:
        mode = "BASE_BODY_IMG2IMG" if args.base_img2img else args.asset
        sampling["denoise"] = style["initial_experiment"]["denoise"][mode]
    parameters = {
        **style["model"], **style["frame"], **sampling, **ctl,
        "controlnet": ctl["model"], "control_strength": ctl["strength"],
        "control_start": ctl["start_percent"], "control_end": ctl["end_percent"],
        "positive_prompt": positive, "negative_prompt": negative,
        "reference_prefix": prefix + "_reference", "module_prefix": prefix + "_module",
        **{role: entry["file"] for role, entry in paths.items()},
    }
    graph = bind(template["graph"], parameters)
    if args.base_img2img:
        add_node(graph, "anchor_plate", "LoadImage", image=parameters["anchor_plate"])
        add_node(graph, "latent", "VAEEncode", pixels=["anchor_plate", 0], vae=["checkpoint", 2])
    if args.ipadapter:
        ip = style["initial_experiment"]["ipadapter"]
        add_node(graph, "identity_reference", "LoadImage", image=parameters["identity_reference"])
        add_node(graph, "ipadapter_model", "IPAdapterModelLoader", ipadapter_file=ip["file"])
        add_node(graph, "identity_clip", "CLIPVisionLoader", clip_name=ip["clip_vision"])
        add_node(graph, "identity_conditioning", "IPAdapterAdvanced", model=["lora", 0],
                 ipadapter=["ipadapter_model", 0], image=["identity_reference", 0], clip_vision=["identity_clip", 0],
                 **{key: ip[key] for key in ("weight", "weight_type", "combine_embeds", "start_at", "end_at", "embeds_scaling")})
        graph["sampler"]["inputs"]["model"] = ["identity_conditioning", 0]
    manifest = {
        "status": "DESIGN_COMPILED / NO_INFERENCE", "style": style["id"],
        "style_source_sha256": style["source_sha256"], "template": template["id"],
        "race": args.race, "gender": args.gender, "asset_type": args.asset,
        "view": args.view, "socket": socket_id, "identity_id": identity,
        "geometry_revision": pack["geometry_revision"], "camera_revision": pack["camera_revision"],
        "catalog": profile["catalog_path"], "catalog_sha256": sha256(catalog_path.read_bytes()).hexdigest(),
        "profile_sha256": sha256(profile_path.read_bytes()).hexdigest(),
        "reference_pack_sha256": sha256(args.reference_pack.read_bytes()).hexdigest(),
        "socket_pack_sha256": sha256(args.socket_pack.read_bytes()).hexdigest() if carrier is not None else None,
        "alpha_option": {key: option[key] for key in ("ui_number", "index", "asset_id", "description")} if option else None,
        "inputs": paths, "model": style["model"], "sampling": sampling, "controlnet": ctl,
        "ipadapter": style["initial_experiment"]["ipadapter"] if args.ipadapter else None,
        "hair_color": {"stage": "MATERIAL_ONLY", "catalog": "colors.hex_rgb_columns_top_to_bottom",
                       "neutral_base_hex": "A8A29A"} if args.asset == "HAIRCUT_MODULE" else None,
        "output_prefix": prefix, "production_status": style["status"],
    }
    return graph, manifest


def main():
    parser = arg_parser()
    args = parser.parse_args()
    try:
        graph, manifest = compile_view(args)
    except (OSError, KeyError, ValueError) as error:
        parser.error(str(error))
    args.out.mkdir(parents=True, exist_ok=True)
    filename = manifest["output_prefix"].rsplit("/", 1)[-1]
    graph_path = args.out / f"{filename}.api.json"
    manifest_path = args.out / f"{filename}.manifest.json"
    write_json(graph_path, graph)
    manifest["graph_sha256"] = sha256(graph_path.read_bytes()).hexdigest()
    write_json(manifest_path, manifest)
    print(graph_path)


if __name__ == "__main__":
    main()
