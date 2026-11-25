"""Check that the two Ranger mannequins share exactly the same head."""

import argparse
import json
from pathlib import Path

from validate_source import validate
from verify_female_torso import load_mesh, reversed_edges, voxel_planes


def verify(female, male):
    female_doc, female_bind, female_parts = load_mesh(female)
    male_doc, male_bind, male_parts = load_mesh(male)
    for field in ("scene", "scenes", "nodes", "skins", "materials", "samplers", "textures"):
        assert female_doc[field] == male_doc[field], field
    assert female_bind == male_bind, "Inverse bind matrices differ"
    assert list(female_parts) == list(male_parts) == ["BaseBody", "Head", "Ears"]
    assert female_parts["BaseBody"] != male_parts["BaseBody"], "The two bodies must remain distinct"
    assert female_parts["Head"] == male_parts["Head"], "Head geometry or vertex attributes differ"
    assert female_parts["Ears"] == male_parts["Ears"], "Ears differ"
    assert voxel_planes(female_parts["Head"]), "The head has off-axis faces"
    assert len({vertex[2] for face in female_parts["Head"] for vertex in face}) == 1

    topology = {}
    for name, path, parts in (("female", female, female_parts), ("male", male, male_parts)):
        topology[name] = validate(path)
        assert all(not count for stats in topology[name].values() for key, count in stats.items()
                   if key != "triangles"), topology[name]
        assert not any(reversed_edges(part) for part in parts.values()), name
    return {
        "shared_head_triangles": sum(female_parts["Head"].values()),
        "shared_ear_triangles": sum(female_parts["Ears"].values()),
        "bind_joints": len(female_doc["skins"][0]["joints"]),
        "topology": topology,
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("female", type=Path)
    parser.add_argument("male", type=Path)
    args = parser.parse_args()
    print(json.dumps(verify(args.female, args.male), indent=2))
