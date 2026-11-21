"""Compile COMFY-STYLE-11B's Human Male FRONT comparison without submitting a job."""
from argparse import ArgumentParser
from hashlib import sha256
from pathlib import Path
import sys

from PIL import Image, ImageChops

sys.path.insert(0, str(Path(__file__).resolve().parent))
from compile_character_factory import REPO, bind, image_inputs, read_json, write_json

EXPERIMENT = REPO / "measurements/comfy_style_11b/EXPERIMENT.json"


def prepare_inputs(experiment, original_guide, style_reference, input_dir, frame):
    guide_bytes = original_guide.read_bytes()
    style_bytes = style_reference.read_bytes()
    structure = experiment["structure"]
    source = experiment["style_reference"]
    if sha256(guide_bytes).hexdigest() != structure["original_guide_sha256"]:
        raise ValueError("Source pose guide SHA-256 differs from COMFY-STYLE-11")
    if sha256(style_bytes).hexdigest() != source["sha256"]:
        raise ValueError("Style reference SHA-256 differs from archived Human V1")
    input_dir.mkdir(parents=True, exist_ok=True)
    pose = input_dir / structure["input"]
    with Image.open(original_guide) as image:
        rgb = image.convert("RGB")
        background = Image.new("RGB", rgb.size, (158, 158, 158))
        red, green, blue = ImageChops.difference(rgb, background).split()
        difference = ImageChops.lighter(ImageChops.lighter(red, green), blue)
        difference.point([0] + [255] * 255).convert("RGB").save(pose)
    style_copy = input_dir / source["input"]
    style_copy.write_bytes(style_bytes)
    inputs = image_inputs(input_dir.resolve(),
                          {"geometry_guide": pose.name, "style_reference": style_copy.name}, frame, False)
    if inputs["geometry_guide"]["sha256"] != structure["sha256"]:
        raise ValueError("Compiled silhouette SHA-256 differs from the measured input")
    return inputs


def compile_case(experiment, style, inputs, control, weight):
    template = read_json(REPO / experiment["template"])
    anatomy = read_json(REPO / experiment["profile"])["anatomy"]
    controlnet = style["controlnet"]
    ipadapter = style["initial_experiment"]["ipadapter"]
    if control not in experiment["structure"]["controlnet_strength_series"]:
        raise ValueError("ControlNet strength is outside the recorded comparison")
    if weight not in experiment["style_reference"]["ipadapter_weight_series"]:
        raise ValueError("IPAdapter weight is outside the recorded comparison")
    case = f"CN{int(round(100 * control)):02}_IPA{int(round(100 * weight)):02}"
    filename = f"HUMAN_MALE_BASE_FRONT_V2_{case}"
    geometry = style["geometry"]
    positive = ", ".join((template["positive"], geometry["language"], geometry["block_size_hierarchy"],
                          geometry["texture"], anatomy["body_build"]))
    parameters = {
        **style["frame"], **style["sampling"], **style["model"], **controlnet,
        "controlnet": controlnet["model"], "control_strength": control,
        "control_start": controlnet["start_percent"], "control_end": controlnet["end_percent"],
        "positive_prompt": positive, "negative_prompt": template["negative"] + ", " + style["negative_prompt"],
        "geometry_guide": inputs["geometry_guide"]["file"], "style_reference": inputs["style_reference"]["file"],
        "ipadapter_model": ipadapter["file"], "clip_vision": ipadapter["clip_vision"],
        "style_weight": weight, "reference_prefix": filename,
    }
    return filename, bind(template["graph"], parameters)


def main():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--guide", type=Path, required=True)
    parser.add_argument("--style-reference", type=Path, required=True)
    parser.add_argument("--input-dir", type=Path, required=True, help="Dedicated local ComfyUI input folder")
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--control", type=float, required=True)
    parser.add_argument("--weight", type=float, required=True)
    args = parser.parse_args()
    try:
        experiment = read_json(EXPERIMENT)
        style = read_json(REPO / experiment["style_lock"])
        inputs = prepare_inputs(experiment, args.guide, args.style_reference, args.input_dir, style["frame"])
        filename, graph = compile_case(experiment, style, inputs, args.control, args.weight)
    except (OSError, ValueError, KeyError) as error:
        parser.error(str(error))
    args.out.mkdir(parents=True, exist_ok=True)
    output = args.out / f"{filename}.api.json"
    write_json(output, graph)
    print(output)


if __name__ == "__main__":
    main()
