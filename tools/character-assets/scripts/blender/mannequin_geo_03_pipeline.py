"""Bind sockets, preserve the source clips and export the GEO-03 model."""

import math

import bpy
from mathutils import Matrix, Quaternion, Vector

import mannequin_geo_03 as carrier

GEO = carrier.geo
RIGGING = carrier.rigging
BASE_CLIPS = GEO.CLIPS
CLIPS = {**BASE_CLIPS, "grip_test": 25, "attack_grip": 15, "two_hand_grip": 9,
         "grip_small": 9, "grip_large": 9}
BODY_ANCHORS = {
    "FACE": ((0, -.282, 1.483), "front"),
    "SCALP": ((0, -.020, 1.8), "top"),
    "NOSE": ((0, -.282, 1.464), "front"),
    "BEARD": ((0, -.270, 1.219), "front"),
    "CHEST": ((0, -.143, 1.023), "front"),
    "WAIST": ((0, -.108, .663), "front"),
    "BACK": ((0, .135, .991), "back"),
}
LEFT_ANCHORS = {
    "EAR": ((.306, -.010, 1.448), "left"),
    "FEATURE": ((.306, .024, 1.689), "left"),
    "SHOULDER": ((.277, 0, 1.103), "top"),
    "ARM": ((.449, -.060, 1.019), "front"),
    "GLOVE": ((.587, 0, 1.047), "top"),
    "LEG": ((.136, -.091, .521), "front"),
    "SHIN": ((.135, -.062, .240), "front"),
    "FOOT": ((.14, -.171, .039), "front"),
}


def socket_specs(profile):
    specs = carrier.previous.prepare(
        carrier.read_json(carrier.PROFILE_DIR / profile["socket_contract"]))["sockets"]
    anchors = dict(BODY_ANCHORS)
    for side, sign in carrier.SIDES:
        label = side.upper()
        for name, (p, orient) in LEFT_ANCHORS.items():
            direction = "right" if sign < 0 and orient == "left" else orient
            anchors[f"{name}_{label}"] = ((sign * p[0], *p[1:]), direction)
        p = profile["grip"]["center"]
        anchors["OFF_HAND" if sign == 1 else "MAIN_HAND"] = ((sign * p[0], *p[1:]), "grip")

    for key, spec in specs.items():
        spec.pop("at", None)
        spec.pop("block", None)
        spec["target_m"], spec["orientation"] = anchors[key]
        if spec["fit"] == "segmented_rigid":
            spec["fit"] = "skinned"
        if key == "FACE":
            spec["face_size_m"] = [.48, .286]
    return specs


def attach_sockets(profile, rig, objects):
    specs = socket_specs(profile)
    collection = bpy.data.collections.new("MODULE_SOCKETS")
    bpy.context.scene.collection.children.link(collection)
    document = {
        "profile": profile["profile"], "units": "m",
        "rest_axes": "+X anatomical left, -Y forward, +Z up",
        "socket_axes": "+Z outward, +Y up on vertical surfaces, +Y along grip shaft",
        "coverage_policy": "Hide only fully covered segments by body_part_id. Keep the shared joint weights on connected garment boundaries.",
        "sockets": {}, "body_parts": {},
    }
    for name, obj in objects.items():
        document["body_parts"][name] = {
            "bone": obj["body_part_bone"],
            "rest_bounds_m": bounds(v.co for v in obj.data.vertices),
            "skin_bones": [group.name for group in obj.vertex_groups],
            "hideable_by": [key for key, spec in specs.items() if name in spec["covers"]],
        }

    for key, spec in specs.items():
        obj = objects[spec["part"]]
        direction = spec["orientation"]
        normal = Vector(GEO.NORMALS[direction])
        point = Vector(spec.pop("target_m"))
        offset = spec.get("offset_m", 0)
        if direction != "grip":
            hit, point, normal, _ = obj.ray_cast(point + normal * .060, -normal, distance=.14)
            if not hit:
                raise ValueError(f"Socket {key} missed {obj.name}")
        up = Vector({"top": (0, 1, 0), "grip": (0, -1, 0)}.get(direction, (0, 0, 1)))
        right = up.cross(normal).normalized()
        rest = Matrix((right, normal.cross(right), normal)).transposed().to_4x4()
        rest.translation = point + normal * offset
        bone = rig.data.bones[spec["bone"]]
        bone_local = bone.matrix_local.inverted() @ rest
        anchor = bpy.data.objects.new("SOCKET_" + key, None)
        collection.objects.link(anchor)
        anchor.empty_display_type, anchor.empty_display_size = "PLAIN_AXES", .021
        anchor.parent, anchor.parent_type, anchor.parent_bone = rig, "BONE", bone.name
        anchor.matrix_basis = Matrix.Translation((0, -bone.length, 0)) @ bone_local
        for attr, value in {
            "socket_id": key, "socket_group": spec["group"], "socket_bone": bone.name,
            "socket_fit": spec["fit"], "socket_body_part": obj.name,
            "coverable_part_ids": ",".join(spec["covers"]), "geometry_revision": profile["profile"],
        }.items():
            anchor[attr] = value
        record = {
            **spec, "node": anchor.name, "rest_matrix": GEO.matrix_rows(rest),
            "bone_local_matrix": GEO.matrix_rows(bone_local),
            "carrier_bounds_socket_m": bounds(rest.inverted() @ v.co for v in obj.data.vertices),
        }
        if key == "FACE":
            GEO.face_uv(obj, point.y)
            width, height = spec["face_size_m"]
            z = point.z
            record["uv_bounds"] = [[.5 + sign * width * .36, .028 + (z + sign * height / 2 - 1.17) * .72]
                                   for sign in (-1, 1)]
            record["face_regions_uv_norm"] = {
                "brows": [.05, .69, .95, .86], "eyes": [.05, .43, .95, .70],
                "nose": [.38, .25, .62, .53], "mouth": [.24, .03, .76, .27],
            }
        document["sockets"][key] = record
    bpy.context.view_layer.update()
    carrier.write_json(carrier.RUN / "module_sockets.json", document)


def bounds(points):
    points = list(points)
    return [[round(stat(p[i] for p in points), 6) for i in range(3)] for stat in (min, max)]


def hand(profile, state, side):
    flip = 1 if side == "l" else -1
    result = {}
    for digit, spec in profile["fingers"].items():
        angles = profile["grip"]["poses"].get(state, {}).get(digit)
        if angles is None:
            if state == "open":
                angles = [-4, -3, spec["points"][0][1] * 114, 0]
            elif state == "fist":
                angles = [83, 90, -2, 0] if digit != "thumb" else [24, 25, 4, 18]
            else:
                angles = [0, 0, 0, 0]
        y1, y2, *spread = angles
        z1, z2 = spread or (0, 0)
        for i, y, z in ((1, y1, z1), (2, y2, z2)):
            result[f"{digit}_{i:02d}_{side}"] = [("Y", flip * y), ("Z", flip * z)]
    return result


def both_hands(profile, state):
    return {name: rotations for side, _ in carrier.SIDES
            for name, rotations in hand(profile, state, side).items()}


def two_hand_hold(profile, rig, objects):
    """Pose the original two-bone chains against a single straight, transverse shaft."""
    action = RIGGING.make_action(rig, "two_hand_grip",
                                 [(i, both_hands(profile, "grip")) for i in (1, 9)], objects)
    rest_center = Vector(profile["grip"]["center"])
    for frame in (1, 9):
        bpy.context.scene.frame_set(frame)
        for side, sign in carrier.SIDES:
            upper, lower, wrist = (rig.pose.bones[part + "_" + side]
                                   for part in ("upperarm", "lowerarm", "hand"))
            start = upper.head.copy()
            palm_orientation = Quaternion((1, 0, 0), math.radians(38)) @ Quaternion((0, 0, 1), -sign * math.pi / 2)
            palm_center = Vector((sign * .143, -.245, .860))
            offset = Vector((sign * rest_center.x, rest_center.y, rest_center.z)) - wrist.bone.head_local
            end = palm_center - palm_orientation @ offset
            axis = (end - start).normalized()
            distance = (end - start).length
            a, b = upper.bone.length, lower.bone.length
            if not abs(a - b) < distance < a + b:
                raise ValueError(f"Two-hand shaft is out of reach for the {side} wrist")
            projection = (a*a - b*b + distance*distance) / (2*distance)
            pole = Vector((sign, -.75, -.36))
            bend = (pole - axis * pole.dot(axis)).normalized()
            elbow = start + axis * projection + bend * math.sqrt(a*a - projection*projection)

            def orient(bone, rotation, position):
                matrix = (rotation.to_matrix() @ bone.bone.matrix_local.to_3x3()).to_4x4()
                matrix.translation = position
                bone.matrix = matrix
                bpy.context.view_layer.update()
                bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)

            u_rest = (upper.bone.tail_local - upper.bone.head_local).normalized()
            l_rest = (lower.bone.tail_local - lower.bone.head_local).normalized()
            u = u_rest.rotation_difference(elbow - start)
            l = (u @ l_rest).rotation_difference(end - elbow) @ u
            orient(upper, u, start)
            orient(lower, l, elbow)
            orient(wrist, palm_orientation, end)
    return action


def build_actions(profile, rig, objects):
    RIGGING.build_actions(rig, objects)
    for name, count in BASE_CLIPS.items():
        carrier.previous.bake_ground(rig, objects, name, count, profile["export_sample_factor"])
    states = ("neutral", "open", "fist", "grip", "neutral")
    grip_sequence = RIGGING.make_action(
        rig, "grip_test", [(1 + 6*i, both_hands(profile, state)) for i, state in enumerate(states)], objects)
    channel = grip_sequence.layers[0].strips[0].channelbag(grip_sequence.slots[0])
    for curve in channel.fcurves:
        if "rotation_quaternion" in curve.data_path and ("_01_" in curve.data_path or "_02_" in curve.data_path):
            for key in curve.keyframe_points:
                key.interpolation = "LINEAR"
            curve.update()
    for name, state in (("grip_small", "small"), ("grip_large", "large")):
        RIGGING.make_action(rig, name, [(i, both_hands(profile, state)) for i in (1, 9)], objects)
    attack = bpy.data.actions["attack"].copy()
    attack.name, attack.use_fake_user = "attack_grip", True
    carrier.previous.set_action(rig, attack)
    for frame in RIGGING.report["actions"]["attack"]["frames"]:
        bpy.context.scene.frame_set(frame)
        for name, rotations in hand(profile, "grip", "r").items():
            RIGGING.set_rot(rig, name, rotations)
            rig.pose.bones[name].keyframe_insert("rotation_quaternion", frame=frame, group=name)
    two_hand_hold(profile, rig, objects)
    carrier.posing.apply_frame(rig, None, 1)


def make_fixture(profile, slot, length=None, radius=None):
    grip = profile["grip"]
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=radius or grip["radius_m"],
                                        depth=length or grip["length_m"])
    obj = bpy.context.object
    obj.name = "QA shaft"
    obj.parent = bpy.data.objects["SOCKET_" + slot]
    obj.rotation_euler.x = math.pi/2
    bpy.context.view_layer.update()
    material = bpy.data.materials.get("QA shaft") or bpy.data.materials.new("QA shaft")
    material.use_nodes = True
    shader = material.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (.044, .21, .32, 1)
    shader.inputs["Roughness"].default_value = .45
    obj.data.materials.append(material)
    return obj


def build(profile):
    rig, objects = carrier.create_carrier(profile)
    attach_sockets(profile, rig, objects)
    build_actions(profile, rig, objects)
    scene = bpy.context.scene
    # Keep T-pose and a framed, material-coloured solid viewport in the authoring file.
    bpy.ops.object.select_all(action="DESELECT")
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                space = area.spaces.active
                space.overlay.show_extras = False
                space.shading.color_type = "MATERIAL"
                space.region_3d.view_location = (0, 0, .92)
                space.region_3d.view_rotation = Quaternion((1, 0, 0), math.pi / 2)
                space.region_3d.view_distance = 3.40
                space.region_3d.view_perspective = "ORTHO"
    bpy.ops.wm.save_as_mainfile(filepath=str(carrier.RUN / carrier.BLEND))
    RIGGING.set_export_sampling(scene, profile["export_sample_factor"])
    bpy.ops.export_scene.gltf(filepath=str(carrier.RUN / carrier.GLB), export_format="GLB",
                             export_animations=True, export_skins=True, export_extras=True)
    bones = [{
        "name": bone.name, "parent": bone.parent.name if bone.parent else None,
        "head_m": list(bone.head_local), "tail_m": list(bone.tail_local),
        "rest_matrix": GEO.matrix_rows(bone.matrix_local), "connected": bone.use_connect,
        "deform": bone.use_deform,
    } for bone in rig.data.bones]
    carrier.write_json(carrier.RUN / "skeleton.json", {
        "profile": profile["profile"], "base_rig": profile["rig_profile"],
        "axes": "+X anatomical left, -Y forward, +Z up", "units": "m", "bones": bones,
    })
    carrier.write_json(carrier.RUN / "build_report.json", {
        "profile": profile["profile"], "profile_sha256": carrier.sha256(carrier.PROFILE),
        "blender": bpy.app.version_string, "mesh_count": len(objects),
        "vertices": sum(len(o.data.vertices) for o in objects.values()),
        "polygons": sum(len(o.data.polygons) for o in objects.values()),
        "bones": len(bones), "sockets": len(socket_specs(profile)), "clips_at_24_fps": CLIPS,
        "export_sampling_hz": scene.render.fps,
        "rest_bounds_m": bounds(v.co for obj in objects.values() for v in obj.data.vertices),
        "artifacts": {file: carrier.sha256(carrier.RUN / file) for file in (carrier.BLEND, carrier.GLB)},
    })


def validate(profile):
    import mannequin_geo_03_validate as qa
    qa.run(profile)
