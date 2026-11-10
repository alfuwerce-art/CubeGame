using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaModels;
using CubeGame.Content;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using CubeGame.Presentation.Visual;
using UnityEngine;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// The only runtime owner of projectile visuals (M0-F2B). Created on <see cref="ProjectileSpawned"/>, placed every
    /// frame from the gameplay state of <see cref="ProjectileSystem"/> (game space → scene, so a floating-origin shift
    /// never moves a projectile away from its target), destroyed with its generated mesh on
    /// <see cref="ProjectileRemoved"/>. It never changes gameplay.
    /// <para>Models: arrow = Alpha item model <c>arrow.cub</c> (weapon subtype 9), boomerang = the wood boomerang of the
    /// item selector (ALPHA model; Alpha draws the flying boomerang with its own renderer — PARTIAL). Magic and the staff
    /// volume have no Alpha model (procedural <c>render_build_projectile_mesh</c>, R-3 BLOCKED): a marked placeholder.</para>
    /// </summary>
    public sealed class ProjectileVisuals : MonoBehaviour
    {
        private readonly Dictionary<long, Visual> _visuals = new Dictionary<long, Visual>();
        private readonly List<Beam> _beams = new List<Beam>();

        /// <summary>CUBEGAME_CONTRACT: how long a hitscan trail stays visible (Alpha trail lifetime is R-3 BLOCKED).</summary>
        public const float BeamSeconds = 0.15f;
        private ProjectileSystem _projectiles;
        private Func<WorldPosition, Vector3> _toScene;
        private Func<float> _modelScale;
        private string _itemsDirectory;
        private GameEventBus _bus;
        private static Material _placeholderMaterial;

        public List<string> Problems { get; } = new List<string>();

        public int Count => _visuals.Count;

        /// <summary>Hitscan trails currently shown.</summary>
        public int BeamCount => _beams.Count;

        /// <summary>Hitscan trails drawn so far (only byte-verified trail actions draw one).</summary>
        public int BeamsDrawn { get; private set; }

        /// <summary>Projectile visuals created so far (tests: one per spawned projectile).</summary>
        public int Created { get; private set; }

        /// <summary>Generated meshes destroyed so far (tests: resource lifecycle).</summary>
        public int MeshesDestroyed { get; private set; }

        public void Initialise(GameEventBus bus, ProjectileSystem projectiles, Func<WorldPosition, Vector3> toScene,
            Func<float> modelScale, string contentRoot)
        {
            Unbind();
            _bus = bus;
            _projectiles = projectiles;
            _toScene = toScene;
            _modelScale = modelScale;
            _itemsDirectory = string.IsNullOrEmpty(contentRoot) ? null : Path.Combine(contentRoot, "items");
            if (_bus != null)
            {
                _bus.Subscribe<ProjectileSpawned>(OnSpawned);
                _bus.Subscribe<ProjectileRemoved>(OnRemoved);
                _bus.Subscribe<HitscanFired>(OnHitscan);
            }
        }

        /// <summary>
        /// Trail of a hitscan attack: a straight line muzzle → end for <see cref="BeamSeconds"/>, only when the action's
        /// trail path is byte-verified (<see cref="HitscanFired.Trail"/>). Alpha's swept mesh (render_build_projectile_mesh
        /// 0x4C04C0) is not ported: the line is PLACEHOLDER_NOT_ALPHA.
        /// </summary>
        private void OnHitscan(HitscanFired e)
        {
            if (!e.Trail) return;
            var go = new GameObject($"HitscanTrail_{e.Action.Value}_PLACEHOLDER");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = 0.15f;
            line.sharedMaterial = PlaceholderMaterial();
            var beam = new Beam { Root = go, Line = line, From = e.Origin, To = e.End, Until = Time.time + BeamSeconds };
            _beams.Add(beam);
            BeamsDrawn++;
            PlaceBeam(beam);
        }

        private void PlaceBeam(Beam beam)
        {
            if (_toScene == null || beam.Line == null) return;
            beam.Line.SetPosition(0, _toScene(beam.From));
            beam.Line.SetPosition(1, _toScene(beam.To));
        }

        public bool TryGet(long projectile, out Transform root, out string model)
        {
            root = null;
            model = null;
            if (!_visuals.TryGetValue(projectile, out Visual visual) || visual.Root == null) return false;
            root = visual.Root.transform;
            model = visual.ModelName;
            return true;
        }

        private void OnSpawned(ProjectileSpawned e)
        {
            if (_visuals.ContainsKey(e.Projectile)) return;
            var visual = new Visual { Root = new GameObject($"Projectile_{e.Projectile}") };
            visual.Root.transform.SetParent(transform, false);
            visual.Model = BuildModel((ProjectileKind)e.Kind, visual.Root.transform, e.Scale);
            visual.ModelName = visual.Model != null ? visual.Model.name : string.Empty;
            _visuals[e.Projectile] = visual;
            Created++;
            Place(e.Projectile, visual, e.Position, e.VelocityX, e.VelocityY, e.VelocityZ);
        }

        private void OnRemoved(ProjectileRemoved e)
        {
            if (_visuals.TryGetValue(e.Projectile, out Visual visual))
            {
                DestroyVisual(visual);
                _visuals.Remove(e.Projectile);
            }
        }

        /// <summary>Places every visual at its gameplay state; the game calls it right after the clock advanced.</summary>
        public void Sync() => LateUpdate();

        private void LateUpdate()
        {
            for (int i = _beams.Count - 1; i >= 0; i--)
            {
                Beam beam = _beams[i];
                if (beam.Root == null || Time.time >= beam.Until)
                {
                    if (beam.Root != null) Destroy(beam.Root);
                    _beams.RemoveAt(i);
                    continue;
                }

                PlaceBeam(beam);
            }

            if (_projectiles == null) return;
            foreach (KeyValuePair<long, Visual> pair in _visuals)
            {
                if (_projectiles.TryGet(pair.Key, out Projectile p))
                {
                    Place(pair.Key, pair.Value, p.Position, p.Vx, p.Vy, p.Vz);
                }
            }
        }

        private void Place(long id, Visual visual, WorldPosition position, double vx, double vy, double vz)
        {
            if (visual.Root == null || _toScene == null) return;
            visual.Root.transform.position = _toScene(position);
            var velocity = new Vector3((float)vx, (float)vy, (float)vz);
            if (velocity.sqrMagnitude > 1e-6f)
            {
                visual.Root.transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            }
        }

        private GameObject BuildModel(ProjectileKind kind, Transform parent, double scale)
        {
            string model = null;
            switch (kind)
            {
                case ProjectileKind.Arrow:
                    model = Projectiles.ModelOf(Projectiles.ArrowSubtype);
                    break;
                case ProjectileKind.Boomerang:
                    // Wood (material 2, as the starting bows/staffs, 0x4775EF), common, seed 0.
                    if (!AlphaCharacterModels.TrySelectItemModel(3, Projectiles.BoomerangSubtype, 2, 0, 0, out model, out _))
                    {
                        model = Projectiles.ModelOf(Projectiles.BoomerangSubtype);
                    }

                    break;
            }

            GameObject built = model != null ? BuildAlphaModel(model, parent) : null;
            return built ?? Placeholder(kind, parent, scale);
        }

        private GameObject BuildAlphaModel(string modelName, Transform parent)
        {
            if (_itemsDirectory == null)
            {
                Problems.Add("DATA_MISSING projectile model: no staged items directory");
                return null;
            }

            string path = Path.Combine(_itemsDirectory, AlphaCharacterModels.PayloadName(modelName));
            if (!File.Exists(path))
            {
                Problems.Add("DATA_MISSING projectile model file " + path);
                return null;
            }

            CubeGame.AlphaTransfer.VoxelMesh.Result result =
                CubeGame.AlphaTransfer.VoxelMesh.Build(File.ReadAllBytes(path), "projectile_" + Path.GetFileNameWithoutExtension(path));
            if (result.mesh == null)
            {
                Problems.Add($"PROJECTILE_MESH {modelName}: {result.problem}");
                return null;
            }

            Material material = AlphaVisualResources.Base;
            if (material == null)
            {
                Destroy(result.mesh);
                MeshesDestroyed++;
                Problems.Add("PROJECTILE_MATERIAL_MISSING " + AlphaVisualResources.ShaderName);
                return null;
            }

            // CUBEGAME_CONTRACT: the model is centred on the projectile and its longest axis follows the flight
            // direction (Alpha's projectile renderer is not ported; which end is the tip is not verified — PARTIAL).
            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            go.transform.SetParent(parent, false);
            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(go.transform, false);
            Bounds bounds = result.mesh.bounds;
            mesh.transform.localPosition = -bounds.center;
            mesh.AddComponent<MeshFilter>().sharedMesh = result.mesh;
            mesh.AddComponent<MeshRenderer>().sharedMaterial = material;
            Vector3 size = bounds.size;
            if (size.x >= size.y && size.x >= size.z) go.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            else if (size.y >= size.x && size.y >= size.z) go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            float modelScale = _modelScale != null ? _modelScale() : 0f;
            go.transform.localScale = Vector3.one * (modelScale > 0f ? modelScale : CubeGame.AlphaModels.AlphaRigBuilder.RootScale);
            return go;
        }

        /// <summary>PLACEHOLDER_NOT_ALPHA: a small sphere (one shared material) sized like the Alpha projectile box.</summary>
        private static GameObject Placeholder(ProjectileKind kind, Transform parent, double scale)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"Projectile_{kind}_PLACEHOLDER";
            Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(parent, false);
            sphere.transform.localScale = Vector3.one * (float)Math.Max(0.2, Math.Min(2.0, scale));
            sphere.GetComponent<Renderer>().sharedMaterial = PlaceholderMaterial();
            return sphere;
        }

        private static Material PlaceholderMaterial()
        {
            if (_placeholderMaterial == null)
            {
                _placeholderMaterial = CubeGame.Presentation.Rendering.RuntimeMaterials.Solid(
                    "Projectile_PLACEHOLDER", new Color(0.4f, 0.6f, 1f));
            }

            return _placeholderMaterial;
        }

        private void DestroyVisual(Visual visual)
        {
            if (visual.Root == null) return;
            visual.Root.SetActive(false);
            foreach (MeshFilter filter in visual.Root.GetComponentsInChildren<MeshFilter>(true))
            {
                // Only meshes generated here; the placeholder shares Unity's built-in sphere mesh.
                if (filter.sharedMesh != null && filter.sharedMesh.name.StartsWith("projectile_", StringComparison.Ordinal))
                {
                    Destroy(filter.sharedMesh);
                    MeshesDestroyed++;
                }
            }

            Destroy(visual.Root);
        }

        private void Unbind()
        {
            if (_bus != null)
            {
                _bus.Unsubscribe<ProjectileSpawned>(OnSpawned);
                _bus.Unsubscribe<ProjectileRemoved>(OnRemoved);
                _bus.Unsubscribe<HitscanFired>(OnHitscan);
            }

            _bus = null;
        }

        private void OnDestroy()
        {
            Unbind();
            foreach (Visual visual in _visuals.Values)
            {
                DestroyVisual(visual);
            }

            _visuals.Clear();
            foreach (Beam beam in _beams)
            {
                if (beam.Root != null) Destroy(beam.Root);
            }

            _beams.Clear();
        }

        private sealed class Beam
        {
            public GameObject Root;
            public LineRenderer Line;
            public WorldPosition From;
            public WorldPosition To;
            public float Until;
        }

        private sealed class Visual
        {
            public GameObject Root;
            public GameObject Model;
            public string ModelName;
        }
    }
}
