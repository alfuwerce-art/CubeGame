using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CubeGame.M0Pilots.World;

namespace CubeGame.M0Pilots.Npc
{
    public enum Faction
    {
        Player = 0,
        Monster = 1,
        Ally = 3,
        Npc = 5,
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
        public int ActionId;
        public int ActionTimerMs;
        public int StunMs;
        public int ActionLockMs;
        public double X;
        public double Y;
        public double Z;
        public readonly Dictionary<string, double> Threat = new Dictionary<string, double>(StringComparer.Ordinal);

        public bool IsBoss
        {
            get
            {
                switch (Kind)
                {
                    case 0x65:
                    case 0x6B:
                    case 0x6C:
                    case 0x6D:
                    case 0x72:
                    case 0x73:
                    case 0x74:
                    case 0x75:
                    case 0x76:
                    case 0x77:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool CanThink => StunMs <= 0;

        public void AdvanceTime(int elapsedMs)
        {
            if (elapsedMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");
            }

            StunMs = Math.Max(0, StunMs - elapsedMs);
            ActionLockMs = Math.Max(0, ActionLockMs - elapsedMs);
            ActionTimerMs += elapsedMs;
        }
    }

    /// <summary>AI decision clock: accumulator with a 31 ms threshold (~30 Hz). DIRECT (G1).</summary>
    public sealed class AiClock
    {
        public const int TickMs = 31;

        private int _accumulator;

        public int AccumulatorMs => _accumulator;

        public int Add(int elapsedMs)
        {
            if (elapsedMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");
            }

            _accumulator += elapsedMs;
            int ticks = 0;
            while (_accumulator >= TickMs)
            {
                _accumulator -= TickMs;
                ticks++;
            }

            return ticks;
        }
    }

    /// <summary>
    /// Threat/aggro model: decay x0.9 per AI tick, clear after target unreachable &gt; 20 s,
    /// rescan after 3 s. DIRECT numbers (G2), no live verification of the exact tick cadence.
    /// </summary>
    public sealed class ThreatTable
    {
        public const double DecayPerTick = 0.9;
        public const int UnreachableResetMs = 20000;
        public const int RescanMs = 3000;

        private readonly Dictionary<string, double> _threat = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _unreachableMs = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _rescanRemainingMs;

        public int Count => _threat.Count;

        public int RescanRemainingMs => _rescanRemainingMs;

        public void Add(string targetId, double amount)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                throw new ArgumentException("Target id is required.", nameof(targetId));
            }

            if (amount < 0.0 || double.IsNaN(amount) || double.IsInfinity(amount))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Threat must be finite and non-negative.");
            }

            double current;
            _threat.TryGetValue(targetId, out current);
            _threat[targetId] = current + amount;
        }

        public double Get(string targetId)
        {
            double value;
            return _threat.TryGetValue(targetId, out value) ? value : 0.0;
        }

        public string TopTarget()
        {
            string best = null;
            double bestValue = double.NegativeInfinity;
            foreach (KeyValuePair<string, double> pair in _threat)
            {
                if (pair.Value > bestValue)
                {
                    best = pair.Key;
                    bestValue = pair.Value;
                }
            }

            return best;
        }

        public void Tick()
        {
            var keys = new List<string>(_threat.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                double value = _threat[keys[i]] * DecayPerTick;
                if (value < 0.0001)
                {
                    _threat.Remove(keys[i]);
                    _unreachableMs.Remove(keys[i]);
                }
                else
                {
                    _threat[keys[i]] = value;
                }
            }

            if (_rescanRemainingMs > 0)
            {
                _rescanRemainingMs = Math.Max(0, _rescanRemainingMs - AiClock.TickMs);
            }
        }

        public void MarkUnreachable(string targetId, int elapsedMs)
        {
            if (elapsedMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");
            }

            int current;
            _unreachableMs.TryGetValue(targetId, out current);
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

        public void MarkReachable(string targetId)
        {
            _unreachableMs.Remove(targetId);
        }
    }

    public static class NpcRules
    {
        /// <summary>
        /// M0-00B FINAL, branch B (0x42ce8c): ally auto-potion at HP < 50% of
        /// SELF MaxHP, held item (1,4), action 0x50. This is eligibility only;
        /// inventory removal and healing were not proven for this branch.
        /// </summary>
        public static bool ShouldDrinkPotion(double health, double maxHealth, int faction)
        {
            ValidatePotionHealth(health, maxHealth);
            return faction == (int)Faction.Ally && health < maxHealth * 0.5;
        }

        /// <summary>
        /// M0-00B FINAL, branch A (0x42cbdc): HP < 25% of SELF MaxHP,
        /// requires inventory LifePotion (1,1). Alpha spends one item;
        /// this pilot checks only eligibility, not inventory mutation.
        /// </summary>
        public static bool ShouldDrinkInventoryPotion(double health, double maxHealth, bool hasLifePotion)
        {
            ValidatePotionHealth(health, maxHealth);
            return hasLifePotion && health < maxHealth * 0.25;
        }

        private static void ValidatePotionHealth(double health, double maxHealth)
        {
            if (double.IsNaN(health) || double.IsInfinity(health) ||
                double.IsNaN(maxHealth) || double.IsInfinity(maxHealth) ||
                health < 0.0 || maxHealth <= 0.0 || health > maxHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(health),
                    "Potion HP inputs must be finite and within [0, maxHealth].");
            }
        }

        public static void ApplyPotionAction(CreatureState state)
        {
            state.ActionId = 0x50;
            state.ActionTimerMs = 0;
        }

        /// <summary>AI dodge: rand()%10 == 0, non-boss, distance &lt; 2, lock 600 ms.</summary>
        public static bool TryDodge(int roll, bool isBoss, double distance, CreatureState state)
        {
            if (roll < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roll), "Roll cannot be negative.");
            }

            if (isBoss || distance >= 2.0 || roll % 10 != 0)
            {
                return false;
            }

            state.ActionLockMs = 600;
            return true;
        }

        /// <summary>Faction 5 regeneration: HP += MaxHP * 0.10 per second, clamped.</summary>
        public static void RegenerateFriendly(CreatureState state, double elapsedSeconds)
        {
            if (elapsedSeconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Elapsed time cannot be negative.");
            }

            if (state.Faction != (int)Faction.Npc || state.Health <= 0.0)
            {
                return;
            }

            state.Health = Math.Min(state.MaxHealth, state.Health + state.MaxHealth * 0.10 * elapsedSeconds);
        }

        public const int PetSummonLimit = 2;
        public const int PetSummonAbilityId = 0x5C;
    }

    public static class SpawnTable
    {
        public const int LevelRows = 30;
        public const int RecordBytes = 0x118;
        public const int StarClamp = 4;
        public static readonly double[] StarChances = { 0.01, 0.001, 0.0001 };

        public static int RollStars(bool oneIn100, bool oneIn1000, bool oneIn10000)
        {
            int stars = (oneIn100 ? 1 : 0) + (oneIn1000 ? 1 : 0) + (oneIn10000 ? 1 : 0);
            return Math.Min(stars, StarClamp);
        }

        /// <summary>
        /// Statistical helper for the documented probabilities. The exact Alpha roll expression
        /// is not recovered; this uses the documented chance values and is marked PARTIAL.
        /// </summary>
        public static int RollStars(AlphaRandom random)
        {
            bool first = random.Next() < 32768.0 * StarChances[0];
            bool second = random.Next() < 32768.0 * StarChances[1];
            bool third = random.Next() < 32768.0 * StarChances[2];
            return RollStars(first, second, third);
        }

        /// <summary>Stable spawn id: region + cell + slot. Entity identity survives unload/reload.</summary>
        public static string SpawnId(int regionX, int regionZ, int cellX, int cellZ, int slot)
        {
            if (regionX < 0 || regionX >= AlphaRegion.Grid || regionZ < 0 || regionZ >= AlphaRegion.Grid)
            {
                throw new ArgumentOutOfRangeException(nameof(regionX), "Region coordinate out of bounds.");
            }

            if (cellX < 0 || cellX >= AlphaRegion.CellsPerRegion || cellZ < 0 || cellZ >= AlphaRegion.CellsPerRegion)
            {
                throw new ArgumentOutOfRangeException(nameof(cellX), "Cell coordinate out of bounds.");
            }

            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), "Spawn slot cannot be negative.");
            }

            return "spawn:" + regionX.ToString(CultureInfo.InvariantCulture) + ":"
                   + regionZ.ToString(CultureInfo.InvariantCulture) + ":"
                   + cellX.ToString(CultureInfo.InvariantCulture) + ":"
                   + cellZ.ToString(CultureInfo.InvariantCulture) + ":"
                   + slot.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// State store for unloaded entities. Alpha's proven behaviour is "save a blob by proximity",
    /// not continuous simulation. Continuous off-screen simulation is a CubeGame requirement and is
    /// NOT implemented here; the pilot only proves save/restore and stable identity.
    /// </summary>
    public sealed class CreatureStateStore
    {
        public const string Header = "M0NPC";
        public const int Version = 1;

        private readonly Dictionary<string, string> _blobs = new Dictionary<string, string>(StringComparer.Ordinal);

        public int Count => _blobs.Count;

        public void Save(CreatureState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (string.IsNullOrEmpty(state.StableId))
            {
                throw new ArgumentException("Stable id is required.", nameof(state));
            }

            _blobs[state.StableId] = SerializeState(state);
        }

        public bool TryRestore(string stableId, out CreatureState state, out string error)
        {
            state = null;
            error = string.Empty;
            string blob;
            if (!_blobs.TryGetValue(stableId, out blob))
            {
                error = "entity blob not found";
                return false;
            }

            return TryParseState(blob, out state, out error);
        }

        public bool Contains(string stableId)
        {
            return _blobs.ContainsKey(stableId);
        }

        public static string SerializeState(CreatureState state)
        {
            var builder = new StringBuilder();
            builder.Append(Header).Append('|').Append(Version.ToString(CultureInfo.InvariantCulture)).Append('|')
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
            foreach (KeyValuePair<string, double> pair in state.Threat)
            {
                builder.Append('\n').Append("threat").Append('|').Append(pair.Key).Append('|')
                    .Append(pair.Value.ToString("R", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static bool TryParseState(string blob, out CreatureState state, out string error)
        {
            state = null;
            error = string.Empty;
            if (string.IsNullOrEmpty(blob))
            {
                error = "empty entity blob";
                return false;
            }

            string[] lines = blob.Split(new[] { '\n' });
            string[] parts = lines[0].Split(new[] { '|' });
            if (parts.Length != 16 || parts[0] != Header)
            {
                error = "entity blob header is not recognized";
                return false;
            }

            int version;
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out version) || version != Version)
            {
                error = "unsupported entity blob version";
                return false;
            }

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
                    string line = lines[i].TrimEnd(new[] { '\r' });
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    string[] threat = line.Split(new[] { '|' });
                    if (threat.Length != 3 || threat[0] != "threat")
                    {
                        error = "entity blob threat line " + (i + 1) + " is malformed";
                        return false;
                    }

                    parsed.Threat[threat[1]] = ParseDouble(threat[2], "threat");
                }
            }
            catch (FormatException exception)
            {
                error = exception.Message;
                return false;
            }

            if (parsed.Health < 0.0 || parsed.MaxHealth <= 0.0 || parsed.Health > parsed.MaxHealth)
            {
                error = "entity blob has inconsistent health";
                return false;
            }

            state = parsed;
            return true;
        }

        private static int ParseInt(string value, string field)
        {
            int parsed;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                throw new FormatException("invalid " + field + ": " + value);
            }

            return parsed;
        }

        private static double ParseDouble(string value, string field)
        {
            double parsed;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                throw new FormatException("invalid " + field + ": " + value);
            }

            return parsed;
        }
    }
}
