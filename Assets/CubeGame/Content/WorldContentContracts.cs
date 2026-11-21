// CubeGame.Content — inputs and outputs of the runtime content placement (D86 M0-02/M0-06/M0-07).
//
// The world generator (Claude's Features/World) owns terrain, climate, zones, deltas and persistence.
// The content layer owns what stands on that terrain. This file is the seam between them: a small
// read-only source the world implements, and plain records the content layer returns. Nothing here
// depends on Unity or on the world module's types, so the two branches can be merged without either
// side adopting the other's internals.
//
// Deliverables and honest statuses:
//   * climate → object selection: TRANSLATED_FROM_CODE (W-18 tree 0x5F56C0), not live-verified;
//   * positions/density: CUBEGAME_CONTRACT (Alpha's placement policy is not recovered);
//   * surface height: from the world (a zone without a usable surface produces no placement);
//   * saved removals: from the world (a removed object never comes back).
using System;
using System.Collections.Generic;

namespace CubeGame.Content
{
    /// <summary>What one placement is.</summary>
    public enum ContentPlacementKind
    {
        /// <summary>
        /// Object/creature kind selected by the W-18 climate scatter (plant, rock, animal …). Faction comes from
        /// the creature catalog: a scattered kind is not automatically passive.
        /// </summary>
        Vegetation = 1,
        /// <summary>Creature or NPC spawned from the creature registry.</summary>
        Creature = 2,
        /// <summary>Structure/building footprint from an Alpha structure template.</summary>
        Structure = 3,
    }

    /// <summary>
    /// Climate of one landmark cell (8×8 zones = 2 048 voxels), as the world knows it.
    /// <see cref="Spacing"/> is the fVar6 argument of 0x5F56C0: in Alpha that is the terrain mask value
    /// of 0x5EFFA0, which is not a placement algorithm (see docs/reports/D86_W18_ADDRESS_VERIFICATION.md).
    /// </summary>
    public struct ZoneClimate
    {
        /// <summary>First air or water voxel above the column base at the W-18 sample point.</summary>
        public int? ScatterHeight;

        public double Humidity;
        public double Temperature;
        public double CellValue;
        public double Spacing;
        public int Level;
        public int Mode;
        /// <summary>Alpha landmark type (1 Village, 2 Mountain … 0xF Island); 0 when unknown.</summary>
        public int CellType;

        public static ZoneClimate Unknown => new ZoneClimate { Level = 1 };

        public override string ToString() =>
            $"climate(h={Humidity:0.###},t={Temperature:0.###},cell={CellValue:0.###},spacing={Spacing:0.###},level={Level},type=0x{CellType:x})";
    }

    /// <summary>One concrete object the content layer wants in the world.</summary>
    public sealed class ContentPlacement
    {
        /// <summary>Stable identity used for persistence, reload and "was it removed".</summary>
        public string StableId = string.Empty;

        public ContentPlacementKind Kind;

        /// <summary>Alpha creature/object kind of a vegetation/creature placement; 0 for structures.</summary>
        public int Species;

        /// <summary>Structure template id (<see cref="AlphaStructureTemplates.HouseId"/> …); empty otherwise.</summary>
        public string TemplateId = string.Empty;

        public WorldZoneKey Zone;

        /// <summary>Absolute voxel coordinates of the object's anchor (double: the world is 2^24 wide).</summary>
        public double X;
        public double Z;

        /// <summary>Top solid voxel of the column; the object stands on the next voxel.</summary>
        public int SurfaceY;

        /// <summary>Faction of the spawned entity (Alpha faction ids; 6 = passive object).</summary>
        public int Faction;

        public int Level = 1;
        public int Stars;
        public float Yaw;

        /// <summary>Footprint of a structure in voxels (0 for objects without a footprint).</summary>
        public int Width;
        public int Depth;
        public int Height;

        /// <summary>Evidence status of this record, e.g. TRANSLATED_FROM_CODE or CUBEGAME_CONTRACT.</summary>
        public string Provenance = string.Empty;

        public WorldVoxelPoint Anchor => new WorldVoxelPoint(X, SurfaceY + 1, Z);

        public override string ToString() => $"{Kind} {StableId} at ({X:0.#},{SurfaceY + 1:0.#},{Z:0.#})";
    }

    /// <summary>
    /// What the content layer needs to know about the world. The generator supplies it; a test or a
    /// standalone showcase can supply a synthetic implementation. Read-only on purpose — the content
    /// layer never edits terrain, climate or saves.
    /// </summary>
    public interface IContentZoneSource
    {
        /// <summary>World seed (the same seed and zone must always give the same content).</summary>
        uint Seed { get; }

        /// <summary>Climate of a landmark cell. Throw when the climate is genuinely unavailable.</summary>
        ZoneClimate ClimateOf(WorldCellKey cell);

        /// <summary>Top solid voxel of an absolute world column; false when the world has no data there.</summary>
        bool TrySurfaceVoxel(int voxelX, int voxelZ, out int topSolidY);

        /// <summary>True when a footprint of this size on this column is flat and free (structures).</summary>
        bool IsFlatAndFree(int voxelX, int voxelZ, int width, int depth);

        /// <summary>True when the saved world records this object as removed/destroyed — it must not return.</summary>
        bool IsRemoved(string stableId);
    }

    /// <summary>Deterministic result of planning one zone.</summary>
    public sealed class ContentPlan
    {
        public readonly WorldZoneKey Zone;
        public readonly List<ContentPlacement> Items = new List<ContentPlacement>();
        public readonly List<string> Problems = new List<string>();

        public ContentPlan(WorldZoneKey zone)
        {
            Zone = zone;
        }

        public int VegetationCount => Count(ContentPlacementKind.Vegetation);
        public int CreatureCount => Count(ContentPlacementKind.Creature);
        public int StructureCount => Count(ContentPlacementKind.Structure);

        int Count(ContentPlacementKind kind)
        {
            int n = 0;
            for (int i = 0; i < Items.Count; i++) if (Items[i].Kind == kind) n++;
            return n;
        }

        public string Describe() =>
            $"{Zone} items={Items.Count} (veg={VegetationCount}, creatures={CreatureCount}, structures={StructureCount}) " +
            (Problems.Count == 0 ? "problems=0" : $"problems={Problems.Count}");
    }
}
