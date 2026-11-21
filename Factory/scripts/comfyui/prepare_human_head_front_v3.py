"""Prepare the registered Human Male FRONT carrier and compile one ComfyUI graph."""
from argparse import ArgumentParser
from hashlib import sha256
from pathlib import Path
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
from compile_character_factory import REPO, read_json, write_json

SPEC = REPO / "workflows/character_factory/profiles/HUMAN/HUMAN_MALE_HEAD_CARRIER_V1.json"
V2_GRAPH = REPO / "measurements/comfy_style_11b/HUMAN_MALE_BASE_FRONT_V2_CN40_IPA50.api.json"
STEM = "HUMAN_MALE_BASE_FRONT_V3"
HEAD = "HUMAN_MALE_HEAD_CARRIER_V1"

# Broad warm-lit planes sampled against the V2 render, darkest on screen left.
PALETTE = {
    "neck": ((81, 29, 30), (142, 57, 38)),
    "ear_left": ((86, 38, 36), (144, 72, 53)),
    "ear_right": ((171, 94, 59), (240, 166, 105)),
    "ear_inner_left": ((112, 53, 44), (181, 105, 73)),
    "ear_inner_right": ((190, 113, 70), (244, 178, 118)),
    "side_left": ((132, 64, 52), (186, 105, 74)),
    "side_right": ((184, 108, 64), (232, 153, 100)),
    "crown": ((218, 162, 121), (255, 225, 171)),
    "face_plane": ((224, 149, 105), (255, 221, 174)),
}


def checked_image(folder, record, size):
    path = folder / record["file"]
    if sha256(path.read_bytes()).hexdigest() != record["sha256"]:
        raise ValueError(f"Unexpected SHA-256: {path}")
    with Image.open(path) as image:
        if image.size != size:
            raise ValueError(f"Unexpected canvas: {path}")
        return image.convert("RGB")


def carrier_geometry(spec, socket):
    frame = spec["frame"]
    width, depth, height = socket["size_xyz"]
    centre = socket["local_origin_grid"]
    crown = socket["planes_grid"]["crown_z"]
    scale = frame["pixels_per_voxel"]
    axis, top = frame["axis_x"], frame["crown_y"]
    anchor = socket["anchor_grid"]
    neck = (axis + (anchor[0] - centre[0]) * scale, top + (crown - anchor[2]) * scale)
    ears = sorted((centre[0] - x, crown - z) for x, _, z in socket["ear_centres_grid_left_right"])

    planes = spec["planes"]
    shapes = {
        "neck": [(neck[0] - 39, neck[1] - 1), (neck[0] + 38, neck[1] - 1),
                 (neck[0] + 36, frame["neck_join_y"]), (neck[0] - 39, frame["neck_join_y"])],
    }
    left_x, left_y = ears[0]
    grid = {}
    for name, key in (("ear_left", "ear_left_shape_relative_to_measured_centre"),
                      ("ear_inner_left", "ear_left_plane_relative_to_measured_centre")):
        grid[name] = [(x + left_x, y + left_y) for x, y in planes[key]]
    grid["ear_right"] = [(ears[1][0] - (x - left_x), y + ears[1][1] - left_y) for x, y in grid["ear_left"]]
    grid["ear_inner_right"] = [(ears[1][0] - (x - left_x), y + ears[1][1] - left_y)
                               for x, y in grid["ear_inner_left"]]
    grid["side_left"] = planes["side_left"]
    grid["side_right"] = [(-x, y) for x, y in planes["side_left"]]
    grid["crown"] = planes["crown"]
    grid["face_plane"] = planes["face_plane"]
    visible_points = [point for polygon in grid.values() for point in polygon]
    xs, ys = zip(*visible_points)
    if (min(xs), max(xs), min(ys), max(ys)) != (-width / 2, width / 2, 0, height):
        raise ValueError("Carrier FRONT bounds differ from the head socket")

    for name, polygon in grid.items():
        shapes[name] = [(round(axis + x * scale), round(top + y * scale)) for x, y in polygon]
    landmarks = {
        "socket_size_x_depth_z": [width, depth, height],
        "projected_bounds_edge_xyxy": [axis - width * scale // 2, top, axis + width * scale // 2, top + height * scale],
        "head_centre_xy": [axis, top + height * scale // 2],
        "ear_centres_screen_xy": [[axis + x * scale, top + y * scale] for x, y in ears],
        "neck_anchor_xy": list(neck),
        "neck_join_y": frame["neck_join_y"],
        "plane_polygons_px": shapes,
    }
    return shapes, landmarks


def raster_planes(size, shapes):
    labels = Image.new("L", size)
    draw = ImageDraw.Draw(labels)
    for index, polygon in enumerate(shapes.values(), start=1):
        draw.polygon(polygon, fill=index)
    matte = labels.point([0] + [255] * 255)
    guide = labels.point([0, 100, 155, 155, 192, 192, 143, 143, 220, 255] + [0] * 246)
    return labels, matte, guide


def paint_carrier(labels, shapes):
    layers = Image.new("RGB", labels.size)
    for index, name in enumerate(shapes, start=1):
        mask = labels.point([255 * (value == index) for value in range(256)])
        box = mask.getbbox()
        left, right = PALETTE[name]
        span = box[2] - box[0]
        gradient = Image.new("RGB", (span, 1))
        gradient.putdata([tuple(round(a + (b - a) * x / max(1, span - 1))
                                for a, b in zip(left, right)) for x in range(span)])
        colors = gradient.resize((span, box[3] - box[1]), Image.Resampling.NEAREST)
        layers.paste(colors, box[:2], mask.crop(box))
    return layers


def node(class_type, **inputs):
    return {"class_type": class_type, "inputs": inputs}


def compile_graph(names):
    graph = read_json(V2_GRAPH)
    graph["geometry_guide"]["inputs"]["image"] = names["body_guide"]
    graph["geometry_guide"]["_meta"]["title"] = "Body-only T-pose silhouette"
    graph["guide_conditioning"]["_meta"]["title"] = "Body ControlNet at 0.4"
    graph["style_reference"]["inputs"]["image"] = names["style_reference"]
    graph["style_conditioning"]["inputs"]["attn_mask"] = ["head_mask", 0]
    graph["positive"]["inputs"]["text"] = (
        "wide low rectangular voxel head, broad blank face plane, stepped square jaw, large lateral block ears, "
        "short neck, big head over compact adult voxel torso, matte warm shading matching the existing body, "
        "clean dominant cuboid planes, hard-edged voxel geometry, subtle rendered light falloff"
    )
    graph["head_guide"] = node("LoadImage", image=names["head_guide"])
    graph["head_edges"] = node("Canny", image=["head_guide", 0], low_threshold=0.2, high_threshold=0.45)
    graph["head_conditioning"] = node(
        "ControlNetApplyAdvanced", positive=["guide_conditioning", 0], negative=["guide_conditioning", 1],
        control_net=["canny_mode", 0], image=["head_edges", 0], strength=0.85, start_percent=0, end_percent=1,
    )
    graph["anchor"] = node("LoadImage", image=names["anchor"])
    graph["head_mask_image"] = node("LoadImage", image=names["head_mask"])
    graph["head_mask"] = node("ImageToMask", image=["head_mask_image", 0], channel="red")
    graph["encode_anchor"] = node("VAEEncode", pixels=["anchor", 0], vae=["checkpoint", 2])
    graph["latent"] = node("SetLatentNoiseMask", samples=["encode_anchor", 0], mask=["head_mask", 0])
    graph["sampler"]["inputs"].update(positive=["head_conditioning", 0], negative=["head_conditioning", 1], denoise=0.34)
    graph["save_reference"]["inputs"]["filename_prefix"] = STEM + "_HEAD_SAMPLE"
    return graph


def prepare(source, output, graph_path):
    spec = read_json(SPEC)
    socket = read_json(REPO / spec["profile"])["head_socket"]
    if socket["id"] != spec["socket_id"]:
        raise ValueError("Carrier and profile head socket do not match")
    size = tuple(spec["frame"]["size"])
    v2 = checked_image(source, spec["body_plate"], size)
    body_guide = checked_image(source, spec["body_pose"], size)
    checked_image(source, spec["style_reference"], size)
    shapes, metrics = carrier_geometry(spec, socket)
    labels, mask, guide = raster_planes(size, shapes)
    head = paint_carrier(labels, shapes)
    body_guide.paste((0, 0, 0), (0, 0, size[0], 350))
    names = {
        "style_reference": spec["style_reference"]["file"],
        "head_guide": f"{HEAD}_GEOMETRY.png",
        "head_mask": f"{HEAD}_MASK.png",
        "body_guide": f"{STEM}_BODY_GUIDE.png",
        "anchor": f"{STEM}_ANCHOR.png",
        "carrier": f"{HEAD}.png",
    }
    output.mkdir(parents=True, exist_ok=True)
    body_guide.save(output / names["body_guide"])
    guide.convert("RGB").save(output / names["head_guide"])
    mask.convert("RGB").save(output / names["head_mask"])
    (output / names["style_reference"]).write_bytes((source / names["style_reference"]).read_bytes())
    head.putalpha(mask)
    head.save(output / names["carrier"])
    Image.composite(head.convert("RGB"), v2, mask).save(output / names["anchor"])
    graph_path.parent.mkdir(parents=True, exist_ok=True)
    write_json(graph_path, compile_graph(names))
    return names, metrics


def main():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="COMFY-STYLE-11B local review directory")
    parser.add_argument("--input-dir", type=Path, required=True)
    parser.add_argument("--graph", type=Path, required=True)
    args = parser.parse_args()
    names, metrics = prepare(args.source, args.input_dir, args.graph)
    print(f"{args.graph}: {names['carrier']} at {metrics['projected_bounds_edge_xyxy']}")


if __name__ == "__main__":
    main()
