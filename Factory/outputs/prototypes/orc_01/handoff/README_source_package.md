# Factoria modular voxel orc — experimental source package

This is a **geometry-only technical prototype**, not a playable or approved character, and **not a conversion** of the user's existing Orc mesh. It uses original manually parameterized box geometry and a reference-informed silhouette.

## Contents

- `scripts/voxel/orc_prototype.py`: reproducible original Orc prototype generator, **Python 3 standard library only**.
- `scripts/voxel/render_preview.py`: optional orthographic previews (requires Matplotlib).
- `outputs/prototypes/orc_modular_v01.glb`: actual GLB 2.0 with **20 independently named parts** and RGBA vertex colors.
- `outputs/prototypes/orc_modular_v01_manifest.json`: validation metadata.
- `outputs/prototypes/orc_modular_v01_preview.png`: inspection-only preview; not a render from Unity.

## Run

```powershell
python scripts/voxel/orc_prototype.py --out outputs/prototypes --seed 17
```

Open GLB in existing Blender (Import > glTF 2.0). Verify independently editable named parts. Do not launch Unity, download AI weights, run Rigify on this prototype, or declare visual acceptance.

## Scope/limitations

- 20 named parts, 211 individual boxes, 2532 triangles; exported GLB independently checked with trimesh 4.11.1.
- Arm and leg segments separated to leave deliberate physical gaps; **no armature, no bone weights, no animation, no facial emotions**.
- Vertex-color cubes, not UV pixel texture; not the target art fidelity.
- `orc_modular_v01.glb` is ORIGINAL procedural demonstration, **not an input-mesh conversion**, and is **not production game art**.
- A real refurbishment pilot needs the actual independent source GLB/FBX of the orc and correspondence of mesh parts, a visual approval, and Blender/Unity verification at the appropriate authorized stage.

## Checks done here

`python orc_prototype.py` produced GLB. `trimesh.load(..., force='scene')` accepted the result and reported 20 meshes, 2532 faces, bounds x[-1.045,1.045], y[0,3.13], z[-0.406,0.53]. The Blender 5.2.4 Windows round-trip and appearance are **NOT TESTED HERE**.
