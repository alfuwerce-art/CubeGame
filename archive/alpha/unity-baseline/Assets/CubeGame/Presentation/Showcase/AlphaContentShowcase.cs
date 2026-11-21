// CubeGame.Presentation.Showcase — small runtime showcase of the transferred content (D86 M0-09C).
//
// Producer/Claude can drop this component into an empty scene to see the player visual, Alpha creatures
// with their actions and a couple of structures live, without the full world module. It owns no game
// rules: the player is visual-only, creatures are driven by CreatureSpawner/CreatureAi, structures come
// from AlphaStructurePlacementPlanner (a deterministic CubeGame contract).
using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.Content;
using CubeGame.Features.AI;
using CubeGame.Features.Creatures;
using CubeGame.Content;
using CubeGame.Presentation.Visual;
using UnityEngine;

namespace CubeGame.Presentation.Showcase
{
    public sealed class AlphaContentShowcase : MonoBehaviour
    {
        public uint Seed = 2024;
        public int CreatureCount = 6;
        public int StructureCount = 2;
        public int DungeonRooms = 4;
        public int ChestsPerDungeon = 2;
        public float Radius = 8f;
        public bool SpawnOnStart = true;

        public AlphaActorView Player { get; private set; }
        public CreatureSpawner Spawner { get; private set; }
        public int SpawnedCreatures => Spawner != null ? Spawner.SpawnedCount : 0;
        public int StructureObjects { get; private set; }
        public int DungeonRoomObjects { get; private set; }
        public int ChestObjects { get; private set; }
        public readonly List<string> Problems = new List<string>();

        readonly List<GameObject> _boxes = new List<GameObject>();
        bool _built;

        void Start()
        {
            if (SpawnOnStart) Build();
        }

        public void Build()
        {
            if (_built) return;
            var source = AlphaContentSource.Resolve();
            if (source.State != AlphaContentState.Ready)
            {
                Problems.Add("SHOWCASE_CONTENT_NOT_READY " + source.Diagnosis);
                return;
            }
            var library = new AlphaActorLibrary(source);

            var playerRequest = CharacterVisualRequest.Default;
            playerRequest.Kind = 0x02;
            var player = AlphaPlayerVisualBuilder.Build(playerRequest, transform, library);
            Player = player.View;
            Problems.AddRange(player.Problems);
            if (Player != null) Player.transform.position = new Vector3(0f, 0f, 0f);

            var spawnerGo = new GameObject("ShowcaseCreatures");
            spawnerGo.transform.SetParent(transform, false);
            Spawner = spawnerGo.AddComponent<CreatureSpawner>();
            Spawner.SpawnOnStart = false;
            Spawner.AiFactory = () => new CreatureAi();
            Spawner.ActorLibrary = library;

            var kinds = new List<int>();
            foreach (var staged in AlphaStagedVisuals.All)
                if (staged.ControllerMode == "ACTIONS") kinds.Add(staged.Kind);
            for (int i = 0; i < CreatureCount && kinds.Count > 0; i++)
            {
                int kind = kinds[(int)((Seed + (uint)i * 37u) % (uint)kinds.Count)];
                float angle = i * Mathf.PI * 2f / Mathf.Max(1, CreatureCount);
                var request = new CreatureSpawnRequest
                {
                    StableId = $"showcase:{Seed}:{i}",
                    Kind = kind,
                    Level = 5,
                    Stars = 0,
                    Faction = (int)CreatureFaction.Monster,
                    X = Mathf.Cos(angle) * Radius,
                    Z = Mathf.Sin(angle) * Radius,
                };
                var agent = Spawner.SpawnOne(request, library);
                if (agent == null) Problems.Add($"SHOWCASE_CREATURE_FAILED {i}");
                else if (!agent.VisualAvailable) Problems.Add("SHOWCASE_CREATURE_VISUAL " + agent.VisualStatus);
            }

            // A house fits only into free cells of the planner grid; walk several cells until we have enough.
            for (int cell = 0; cell < 12 && StructureObjects < StructureCount; cell++)
            {
                int regionX = 0;
                int regionZ = cell * 2;
                var houses = AlphaStructurePlacementPlanner.Plan(Seed, regionX, regionZ, 0, 0,
                    new[] { AlphaStructureTemplates.HouseId }, (x, z, w, d) => true, Problems);
                for (int i = 0; i < houses.Count && StructureObjects < StructureCount; i++)
                {
                    var p = houses[i];
                    MakeBox(new Vector3((float)p.X + p.Width * 0.5f, p.Height * 0.5f, (float)p.Z + p.Depth * 0.5f),
                        new Vector3(p.Width, p.Height, p.Depth), "ShowcaseHouse" + StructureObjects);
                    StructureObjects++;
                }
            }
            var dungeon = AlphaDungeonLayoutPlanner.Plan(Seed, DungeonRooms, 0, 40, Problems);
            if (dungeon != null)
            {
                foreach (var room in dungeon.Rooms)
                {
                    MakeBox(new Vector3(room.X + 1.5f, 1.5f, room.Z + 1.5f), new Vector3(3f, 3f, 3f),
                        "ShowcaseDungeonRoom" + room.Index);
                    DungeonRoomObjects++;
                }
                var chests = AlphaDungeonChests.Plan(dungeon, ChestsPerDungeon, Seed, 0, 0, Problems);
                foreach (var chest in chests)
                {
                    MakeBox(new Vector3((float)chest.X, (float)chest.Y, (float)chest.Z), new Vector3(0.5f, 0.5f, 0.5f),
                        "ShowcaseChest" + chest.RoomIndex);
                    ChestObjects++;
                }
            }
            _built = true;
        }

        public void Tick(float deltaSeconds)
        {
            if (!_built) return;
            Spawner?.TickAll(deltaSeconds);
            if (Player != null)
            {
                Player.ApplyLocomotion(new AlphaLocomotionState { SpeedXY = 0f });
                Player.Tick(deltaSeconds);
            }
        }

        void Update()
        {
            if (SpawnOnStart) Tick(Time.deltaTime);
        }

        void MakeBox(Vector3 position, Vector3 scale, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            go.AddComponent<MeshRenderer>().sharedMaterial = AlphaVisualResources.Base;
            _boxes.Add(go);
        }

        void OnDestroy()
        {
            foreach (var box in _boxes) if (box != null) Destroy(box);
            _boxes.Clear();
        }
    }
}
