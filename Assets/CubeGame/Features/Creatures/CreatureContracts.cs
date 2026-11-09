// CubeGame.Features.Creatures — proven Alpha creature/AI contracts (D86 M0-06A/06B, D86 M0-06D).
//
// Ported from the M0-00 pilot (tools/m0_pilots/Npc/NpcContracts.cs) into the game as the single
// source of truth: AI clock 31 ms, threat decay/reset/rescan, both potion branches, dodge, friendly
// regeneration, stable spawn identity and the persistent creature blob. Every constant keeps the
// provenance of its pilot run (PROVEN_ALPHA / PARTIAL); nothing here decides damage — combat math
// stays in Core (S03/S04) and is called by the game, never duplicated.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CubeGame.Content;

namespace CubeGame.Features.Creatures
{
    /// <summary>Alpha faction values written by Server.exe / creature generation.</summary>
    public enum CreatureFaction
    {
        Player = 0,
        Monster = 1,
        Ally = 3,
        Npc = 5,
        /// <summary>Passive objects (plants, ore, egg, dummy) — faction 6, no walk, no actions.</summary>
        Passive = 6,
    }

    public sealed class CreatureState
    {
        public string StableId;
        public int Kind;
        public int Faction;
        public int Level;
        public int Stars;
        public double Health;
        public double MaxHealth;
        public int ActionId = -1;
        public int ActionTimerMs;
        public int StunMs;
        public int ActionLockMs;
        public double X;
        public double Y;
        public double Z;
        public readonly Dictionary<string, double> Threat = new Dictionary<string, double>(StringComparer.Ordinal);

        public bool IsBoss => CreatureKinds.IsBoss(Kind);
        public bool CanThink => StunMs <= 0;
        public bool IsPassiveObject => CreatureKinds.IsPassiveObject(Kind);

        public void AdvanceTime(int elapsedMs)
        {
            if (elapsedMs < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");
            StunMs = Math.Max(0, StunMs - elapsedMs);
            ActionLockMs = Math.Max(0, ActionLockMs - elapsedMs);
            ActionTimerMs += elapsedMs;
        }
    }

    /// <summary>The Alpha creature kind facts the creature layer needs (data comes from Content).</summary>
    public static class CreatureKinds
    {
        // Boss kinds proven in M0-00B (N-05): 0x65 and the ten listed in the pilot contract.
        static readonly HashSet<int> BossKinds = new HashSet<int> { 0x65, 0x6B, 0x6C, 0x6D, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77 };
        static readonly int[] BossKindList = { 0x65, 0x6B, 0x6C, 0x6D, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77 };

        /// <summary>Boss kinds of the Alpha spawn table (M0-00B N-05). Behaviour beyond dragon 0x65 is UNKNOWN.</summary>
        public static IReadOnlyList<int> BossKindsSorted => BossKindList;
        public const string BossEvidence = "M0-00B N-05 list; only 0x65 has a transferred action library (D83)";

        public static bool IsBoss(int kind) => BossKinds.Contains(kind);

        public static bool IsPassiveObject(int kind) => AlphaCreatureCatalog.IsPassiveObject(kind);

        public static int FactionOf(int kind)
        {
            int faction = AlphaCreatureCatalog.FactionOf(kind);
            if (faction >= 0) return faction;
            return IsPassiveObject(kind) ? (int)CreatureFaction.Passive : (int)CreatureFaction.Monster;
        }

        public static int[] ActionsOf(int kind) => AlphaCreatureCatalog.ActionsOf(kind);

        public static string FamilyOf(int kind) =>
            AlphaContentDatabase.TryCreatureKind(kind, out var def) ? def.MotionFamily : string.Empty;

        /// <summary>Action clip name only when the kind's AI really launches the action.</summary>
        public static bool TryActionClipName(int kind, int action, out string clip) =>
            AlphaCreatureCatalog.TryActionClipName(kind, action, out clip);
    }

    /// <summary>AI decision clock: accumulator with a 31 ms threshold (~30 Hz). DIRECT (M0-00B N-04).</summary>
    public sealed class AiClock
    {
        public const int TickMs = 31;
        int _accumulator;
        public int AccumulatorMs => _accumulator;

        public int Add(int elapsedMs)
        {
            if (elapsedMs < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");
            _accumulator += elapsedMs;
            int ticks = 0;
            while (_accumulator >= TickMs) { _accumulator -= TickMs; ticks++; }
            return ticks;
        }
    }

    /// <summary>
    /// Threat/aggro model: decay x0.9 per AI tick, clear after the target stays unreachable longer
    /// than 20 s, rescan after 3 s. DIRECT numbers (M0-00B N-06/N-07); the exact tick cadence was not
    /// observed live (live_check=NOT_CAPTURED in the pilot).
    /// </summary>
    public sealed class ThreatTable
    {
        public const double DecayPerTick = 0.9;
        public const int UnreachableResetMs = 20000;
        public const int RescanMs = 3000;

        readonly Dictionary<string, double> _threat = new Dictionary<string, double>(StringComparer.Ordinal);
        readonly Dictionary<string, int> _unreachableMs = new Dictionary<string, int>(StringComparer.Ordinal);
        int _rescanRemainingMs;

        public int Count => _threat.Count;
        public int RescanRemainingMs => _rescanRemainingMs;

        public void Add(string targetId, double amount)
        {
            if (string.IsNullOrEmpty(targetId)) throw new ArgumentException("Target id is required.", nameof(targetId));
            if (amount < 0.0 || double.IsNaN(amount) || double.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount), "Threat must be finite and non-negative.");
            _threat.TryGetValue(targetId, out var current);
            _threat[targetId] = current + amount;
        }

        public double Get(string targetId) => _threat.TryGetValue(targetId, out var value) ? value : 0.0;

        public string TopTarget()
        {
            string best = null;
            double bestValue = double.NegativeInfinity;
            foreach (var pair in _threat)
                if (pair.Value > bestValue) { best = pair.Key; bestValue = pair.Value; }
            return best;
        }

        public void Tick()
        {
            var keys = new List<string>(_threat.Keys);
            foreach (var key in keys)
            {
                double value = _threat[key] * DecayPerTick;
                if (value < 0.0001) { _threat.Remove(key); _unreachableMs.Remove(key); }
                else _threat[key] = value;
            }
            if (_rescanRemainingMs > 0) _rescanRemainingMs = Math.Max(0, _rescanRemainingMs - AiClock.TickMs);
        }

        public void MarkUnreachable(string targetId, int elapsedMs)
        {
            if (elapsedMs < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");
            _unreachableMs.TryGetValue(targetId, out var current);
            current += elapsedMs;
            if (current > UnreachableResetMs)
            {
                _threat.Clear();
                _unreachableMs.Clear();
                _rescanRemainingMs = RescanMs;
                return;
            }
            _unreachableMs[targetId] = current;
        }

        public void MarkReachable(string targetId) => _unreachableMs.Remove(targetId);

        public void Reset()
        {
            _threat.Clear();
            _unreachableMs.Clear();
            _rescanRemainingMs = RescanMs;
        }
    }

    public enum PotionDecision
    {
        None = 0,
        TickBlocked = 1,
        InventoryPotion = 2,
        AllyAutoPotion = 3,
    }

    /// <summary>Alpha NPC rules proven in M0-00B FINAL and re-verified by the M0-00 pre-batch review.</summary>
    public static class CreatureRules
    {
        /// <summary>Re-entry guard of the potion action (0x42cbb9): 3000 ms.</summary>
        public const int PotionReentryMs = 3000;
        public const int PotionActionId = 0x50;

        /// <summary>Gates at the head of CombatBehavior::vfunc_0 that end the whole AI tick before both potion branches.</summary>
        public static bool PotionTickBlocked(CreatureState state, bool flag128Set)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return flag128Set || (state.ActionId == PotionActionId && state.ActionTimerMs < PotionReentryMs);
        }

        /// <summary>Branch B (0x42ce8c): ally, out of combat, HP &lt; 50 % of its own max, held item (1,4).</summary>
        public static bool ShouldDrinkPotion(double health, double maxHealth, int faction, bool hasTarget)
        {
            ValidatePotionHealth(health, maxHealth);
            return !hasTarget && faction == (int)CreatureFaction.Ally && health < maxHealth * 0.5;
        }

        /// <summary>Branch A (0x42cbdc): HP &lt; 25 % of its own max, current action over, LifePotion (1,1) in bag.</summary>
        public static bool ShouldDrinkInventoryPotion(double health, double maxHealth, bool hasLifePotion,
            int actionTimerMs, int currentActionDurationMs)
        {
            ValidatePotionHealth(health, maxHealth);
            return hasLifePotion && health < maxHealth * 0.25 && actionTimerMs > currentActionDurationMs;
        }

        public static PotionDecision EvaluatePotion(CreatureState state, bool flag128Set, bool hasLifePotion,
            int currentActionDurationMs, bool hasTarget)
        {
            if (PotionTickBlocked(state, flag128Set)) return PotionDecision.TickBlocked;
            if (ShouldDrinkInventoryPotion(state.Health, state.MaxHealth, hasLifePotion, state.ActionTimerMs, currentActionDurationMs))
                return PotionDecision.InventoryPotion;
            return ShouldDrinkPotion(state.Health, state.MaxHealth, state.Faction, hasTarget)
                ? PotionDecision.AllyAutoPotion
                : PotionDecision.None;
        }

        static void ValidatePotionHealth(double health, double maxHealth)
        {
            if (double.IsNaN(health) || double.IsInfinity(health) || double.IsNaN(maxHealth) || double.IsInfinity(maxHealth)
                || health < 0.0 || maxHealth <= 0.0 || health > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(health), "Potion HP inputs must be finite and within [0, maxHealth].");
        }

        public static void ApplyPotionAction(CreatureState state)
        {
            state.ActionId = PotionActionId;
            state.ActionTimerMs = 0;
        }

        /// <summary>AI dodge: rand()%10 == 0, non-boss, distance &lt; 2, lock 600 ms.</summary>
        public static bool TryDodge(int roll, bool isBoss, double distance, CreatureState state)
        {
            if (roll < 0) throw new ArgumentOutOfRangeException(nameof(roll), "Roll cannot be negative.");
            if (isBoss || distance >= 2.0 || roll % 10 != 0) return false;
            state.ActionLockMs = 600;
            return true;
        }

        /// <summary>Faction 5 regeneration: +10 % of max HP per second, clamped.</summary>
        public static void RegenerateFriendly(CreatureState state, double elapsedSeconds)
        {
            if (elapsedSeconds < 0.0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Elapsed time cannot be negative.");
            if (state.Faction != (int)CreatureFaction.Npc || state.Health <= 0.0) return;
            state.Health = Math.Min(state.MaxHealth, state.Health + state.MaxHealth * 0.10 * elapsedSeconds);
        }

        public const int PetSummonLimit = 2;
        public const int PetSummonAbilityId = 0x5C;
    }

    /// <summary>
    /// Pet/special-creature facts (M0-06E). Alpha evidence covers only the summon limit and the summon
    /// ability id; taming, riding, pet AI and boss behaviours beyond dragon 0x65 are UNKNOWN and are not
    /// implemented instead of being guessed.
    /// </summary>
    public static class CreaturePets
    {
        public const int SummonLimit = CreatureRules.PetSummonLimit;
        public const int SummonAbilityId = CreatureRules.PetSummonAbilityId;
        public const string Status = "PARTIAL: summon limit/ability proven; taming, riding and pet AI UNKNOWN (M0-06E)";
    }

    /// <summary>
    /// Spawn schema of the Alpha table (30 level rows, 0x118-byte record, star chances 1 %/0.1 %/0.01 %,
    /// clamp 4). The exact roll expression was not recovered, so the statistical helper below is
    /// PARTIAL and must not be presented as Alpha's own roll.
    /// </summary>
    public static class SpawnTable
    {
        public const int LevelRows = 30;
        public const int RecordBytes = 0x118;
        public const int StarClamp = 4;
        public static readonly double[] StarChances = { 0.01, 0.001, 0.0001 };

        public static int RollStars(bool oneIn100, bool oneIn1000, bool oneIn10000) =>
            Math.Min((oneIn100 ? 1 : 0) + (oneIn1000 ? 1 : 0) + (oneIn10000 ? 1 : 0), StarClamp);

        /// <summary>PARTIAL: documented chance values, exact Alpha roll expression not recovered.</summary>
        public static int RollStars(uint roll0, uint roll1, uint roll2)
        {
            const double scale = 65536.0;
            return RollStars(roll0 < scale * StarChances[0], roll1 < scale * StarChances[1], roll2 < scale * StarChances[2]);
        }

        /// <summary>Stable spawn id: region + cell + slot, so entity identity survives unload/reload.</summary>
        public static string SpawnId(int regionX, int regionZ, int cellX, int cellZ, int slot)
        {
            if (regionX < 0 || regionX >= AlphaContentTables.WorldRegionGrid || regionZ < 0 || regionZ >= AlphaContentTables.WorldRegionGrid)
                throw new ArgumentOutOfRangeException(nameof(regionX), "Region coordinate out of bounds.");
            if (cellX < 0 || cellX >= AlphaContentTables.RegionCells || cellZ < 0 || cellZ >= AlphaContentTables.RegionCells)
                throw new ArgumentOutOfRangeException(nameof(cellX), "Cell coordinate out of bounds.");
            if (slot < 0) throw new ArgumentOutOfRangeException(nameof(slot), "Spawn slot cannot be negative.");
            return "spawn:" + regionX.ToString(CultureInfo.InvariantCulture) + ":" + regionZ.ToString(CultureInfo.InvariantCulture)
                   + ":" + cellX.ToString(CultureInfo.InvariantCulture) + ":" + cellZ.ToString(CultureInfo.InvariantCulture)
                   + ":" + slot.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// State store of unloaded entities. Alpha's proven behaviour is "save a blob by proximity", not
    /// continuous simulation; off-screen simulation is a CubeGame extension and is not implied here.
    /// </summary>
    public sealed class CreatureStateStore
    {
        public const string Header = "M0NPC";
        public const int Version = 1;

        readonly Dictionary<string, string> _blobs = new Dictionary<string, string>(StringComparer.Ordinal);
        public int Count => _blobs.Count;
        public IEnumerable<string> Ids => _blobs.Keys;

        public void Save(CreatureState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrEmpty(state.StableId)) throw new ArgumentException("Stable id is required.", nameof(state));
            _blobs[state.StableId] = SerializeState(state);
        }

        public bool TryRestore(string stableId, out CreatureState state, out string error)
        {
            state = null;
            if (!_blobs.TryGetValue(stableId, out var blob)) { error = "entity blob not found"; return false; }
            return TryParseState(blob, out state, out error);
        }

        public bool Contains(string stableId) => _blobs.ContainsKey(stableId);

        public bool Remove(string stableId) => _blobs.Remove(stableId);

        public static string SerializeState(CreatureState state)
        {
            var b = new StringBuilder();
            b.Append(Header).Append('|').Append(Version.ToString(CultureInfo.InvariantCulture)).Append('|')
             .Append(state.StableId).Append('|')
             .Append(state.Kind.ToString(CultureInfo.InvariantCulture)).Append('|')
             .Append(state.Faction.ToString(CultureInfo.InvariantCulture)).Append('|')
             .Append(state.Level.ToString(CultureInfo.InvariantCulture)).Append('|')
             .Append(state.Stars.ToString(CultureInfo.InvariantCulture)).Append('|')
             .Append(state.Health.ToString("R", CultureInfo.InvariantCulture)).Append('|')
             .Append(state.MaxHealth.ToString("R", CultureInfo.InvariantCulture)).Append('|')
             .Append(state.ActionId.ToString(CultureInfo.InvariantCulture)).Append('|')
             .Append(state.ActionTimerMs.ToString(CultureInfo.InvariantCulture)).Append('|')
             .Append(state.StunMs.ToString(CultureInfo.InvariantCulture)).Append('|')
             .Append(state.ActionLockMs.ToString(CultureInfo.InvariantCulture)).Append('|')
             .Append(state.X.ToString("R", CultureInfo.InvariantCulture)).Append('|')
             .Append(state.Y.ToString("R", CultureInfo.InvariantCulture)).Append('|')
             .Append(state.Z.ToString("R", CultureInfo.InvariantCulture));
            foreach (var pair in state.Threat)
                b.Append('\n').Append("threat").Append('|').Append(pair.Key).Append('|')
                 .Append(pair.Value.ToString("R", CultureInfo.InvariantCulture));
            return b.ToString();
        }

        public static bool TryParseState(string blob, out CreatureState state, out string error)
        {
            state = null;
            error = string.Empty;
            if (string.IsNullOrEmpty(blob)) { error = "empty entity blob"; return false; }
            string[] lines = blob.Split('\n');
            string[] parts = lines[0].Split('|');
            if (parts.Length != 16 || parts[0] != Header) { error = "entity blob header is not recognized"; return false; }
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int version) || version != Version)
            { error = "unsupported entity blob version"; return false; }
            var parsed = new CreatureState();
            try
            {
                parsed.StableId = parts[2];
                parsed.Kind = ParseInt(parts[3], "kind");
                parsed.Faction = ParseInt(parts[4], "faction");
                parsed.Level = ParseInt(parts[5], "level");
                parsed.Stars = ParseInt(parts[6], "stars");
                parsed.Health = ParseDouble(parts[7], "health");
                parsed.MaxHealth = ParseDouble(parts[8], "maxHealth");
                parsed.ActionId = ParseInt(parts[9], "actionId");
                parsed.ActionTimerMs = ParseInt(parts[10], "actionTimerMs");
                parsed.StunMs = ParseInt(parts[11], "stunMs");
                parsed.ActionLockMs = ParseInt(parts[12], "actionLockMs");
                parsed.X = ParseDouble(parts[13], "x");
                parsed.Y = ParseDouble(parts[14], "y");
                parsed.Z = ParseDouble(parts[15], "z");
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].TrimEnd('\r');
                    if (line.Length == 0) continue;
                    string[] threat = line.Split('|');
                    if (threat.Length != 3 || threat[0] != "threat") { error = "entity blob threat line " + (i + 1) + " is malformed"; return false; }
                    parsed.Threat[threat[1]] = ParseDouble(threat[2], "threat");
                }
            }
            catch (FormatException e) { error = e.Message; return false; }
            if (parsed.Health < 0.0 || parsed.MaxHealth <= 0.0 || parsed.Health > parsed.MaxHealth)
            { error = "entity blob has inconsistent health"; return false; }
            state = parsed;
            return true;
        }

        static int ParseInt(string value, string field) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed : throw new FormatException("invalid " + field + ": " + value);

        static double ParseDouble(string value, string field) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed : throw new FormatException("invalid " + field + ": " + value);
    }
}
