"""Studio renders of geometry, anatomical details and test poses."""

import bpy
from mathutils import Matrix, Vector

import mannequin_geo_03 as geo3

BODY = {
    "FRONT": (0, -1, 0), "BACK": (0, 1, 0), "LEFT": (1, 0, 0), "RIGHT": (-1, 0, 0),
    "THREE_QUARTER": (0.70, -1, 0.27),
}
BASELINE_VIEWS = {"FRONT", "THREE_QUARTER", "HEAD_EAR", "HAND_OPEN", "FEET_SIDE", "IDLE"}
ACTIONS = {
    "IDLE": ("idle", 1), "WALK": ("walk", 1), "WALK_PASS": ("walk", 5),
    "ATTACK": ("attack", 9), "ATTACK_WINDUP": ("attack", 5),
}


def aim(obj, target, up=(0, 0, 1)):
    view = (obj.location - Vector(target)).normalized()
    right = Vector(up).cross(view).normalized()
    obj.rotation_euler = Matrix((right, view.cross(right), view)).transposed().to_euler()


def make_stage(scene, draft, clay):
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 8 if draft else 20
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 62 if draft else 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "AgX"

    world = bpy.data.worlds.new("Review surroundings")
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs[0].default_value = (.19, .23, .31, 1)
    background.inputs[1].default_value = .37
    scene.world = world

    camera = bpy.data.objects.new("Review camera", bpy.data.cameras.new("Review camera"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    bpy.ops.mesh.primitive_plane_add(size=2000, location=(0, 0, -.0004))
    floor = bpy.context.object
    floor.name = "Review ground"
    ground = bpy.data.materials.new("Slate")
    ground.use_nodes = True
    shader = ground.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (.035, .054, .075, 1)
    shader.inputs["Roughness"].default_value = .91
    floor.data.materials.append(ground)

    shader = clay.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (.49, .48, .45, 1)
    shader.inputs["Roughness"].default_value = .78
    for name, position, power, size, color in (
        ("Key", (-2.8, -4.0, 4.3), 355, 2.4, (1, .91, .82)),
        ("Bounce", (3.1, -2.7, 3.0), 143, 3.1, (.78, .86, 1)),
        ("Contour", (1.4, 2.1, 3.5), 350, 2.1, (.88, .95, 1)),
        ("Detail fill", (0, -3.2, .16), 42, 2.5, (.91, .95, 1)),
    ):
        lamp = bpy.data.lights.new(name, "AREA")
        lamp.energy, lamp.size, lamp.color = power, size, color
        obj = bpy.data.objects.new(name, lamp)
        scene.collection.objects.link(obj)
        obj.location = position
        aim(obj, (0, 0, .9))
    return camera, floor


def render(rig, profile, out, *, draft=False, baseline=False):
    out.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    clay = bpy.data.objects["head"].data.materials[0]
    camera, floor = make_stage(scene, draft, clay)
    manifest = {}
    source = None if draft else geo3.sha256(
        geo3.BASELINE / "mannequin_v3_rig.blend" if baseline else geo3.RUN / geo3.BLEND)

    def shoot(name, direction, focus=(0, 0, .92), scale=2.20, *,
              up=(0, 0, 1), detail=False, mask=False):
        if baseline and name not in BASELINE_VIEWS:
            return
        camera.data.ortho_scale = scale
        camera.location = Vector(focus) + 7 * Vector(direction).normalized()
        aim(camera, focus, up)
        scene.render.film_transparent = mask
        floor.hide_render = detail or mask
        scene.render.filepath = str(out / (name + ".png"))
        bpy.context.view_layer.update()
        bpy.ops.render.render(write_still=True)
        factor = scene.render.resolution_percentage / 100
        width, height = int(scene.render.resolution_x * factor), int(scene.render.resolution_y * factor)
        manifest[name] = {
            "width_px": width, "height_px": height, "center_m": list(focus),
            "ortho_width_m": scale * width / height, "ortho_height_m": scale,
            "camera_world": geo3.geo.matrix_rows(camera.matrix_world),
            "sha256": geo3.sha256(out / (name + ".png")),
        }

    geo3.posing.apply_frame(rig, None, 1)
    for name, direction in BODY.items():
        if not draft or name in ("FRONT", "THREE_QUARTER", "LEFT"):
            shoot(name, direction)
    if not draft and not baseline:
        shoot("tpose_front_silhouette", BODY["FRONT"], mask=True)
    for name, center, direction, scale in (
        ("HEAD_FRONT", (0, 0, 1.48), (0, -1, .01), 1.00),
        ("HEAD_EAR", (.119, 0, 1.49), (1, -.48, .20), .98),
        ("EAR_DETAIL", (.316, 0, 1.447), (1, -.31, .08), .245),
        ("FEET_FRONT", (0, -.03, .121), (0, -1, .22), .58),
        ("FEET_SIDE", (.14, -.047, .112), (1, -.10, .16), .47),
    ):
        shoot(name, direction, center, scale, detail=True)
    hand_center = (-.633, -.018, 1.003)
    hand_direction = (-.28, -.61, -1)
    hand_up = (-1, 0, 0)
    for name, frame in (("HAND_OPEN", 7), ("HAND_FIST", 13), ("HAND_GRIP", 19)):
        if (draft or baseline) and name != "HAND_OPEN":
            continue
        if not draft:
            geo3.posing.apply_frame(rig, "grip_test", frame)
        fixture = None
        if name == "HAND_GRIP":
            from mannequin_geo_03_pipeline import make_fixture
            fixture = make_fixture(profile, "MAIN_HAND")
        center = (-.603, -.015, .981) if name == "HAND_FIST" else hand_center
        scale = .266 if name == "HAND_FIST" else .316
        shoot(name, hand_direction, center, scale, up=hand_up, detail=True)
        if fixture:
            bpy.data.objects.remove(fixture, do_unlink=True)

    if not draft:
        for name, (clip, frame) in ACTIONS.items():
            geo3.posing.apply_frame(rig, clip, frame)
            shoot(name, BODY["THREE_QUARTER"])
    if not draft and not baseline:
        from mannequin_geo_03_pipeline import make_fixture
        for name, clip, frame, length in (
            ("ATTACK_GRIP", "attack_grip", 9, .142),
            ("TWO_HAND_GRIP", "two_hand_grip", 1, .445),
        ):
            geo3.posing.apply_frame(rig, clip, frame)
            fixture = make_fixture(profile, "MAIN_HAND", length)
            if name == "TWO_HAND_GRIP":
                center = (fixture.matrix_world.translation +
                          bpy.data.objects["SOCKET_OFF_HAND"].matrix_world.translation) / 2
                axis = fixture.parent.matrix_world.to_3x3().col[1].normalized()
                fixture.location.y = (center - fixture.matrix_world.translation).dot(axis)
                bpy.context.view_layer.update()
                direction = (.09, -1, .32)
            else:
                center, direction = (0, 0, .92), BODY["THREE_QUARTER"]
            if name == "TWO_HAND_GRIP":
                shoot(name, direction, center, .85, detail=True)
            else:
                shoot(name, direction, center)
            bpy.data.objects.remove(fixture, do_unlink=True)

    manifest = {"source_blend_sha256": source, "views": manifest}
    geo3.write_json(out / "render_manifest.json", manifest)
