// CubeGame.Presentation.Visual — Alpha sound library of the game (D6, Q-09).
//
// Inventory (93 names) is Alpha data extracted from data2.db (tools/alpha_content/extract_sounds.py); the audio
// files themselves are staged in the local content cache and never committed. Which sound belongs to which game
// event is UNKNOWN except the mission-complete sound (Q-09); Alpha's numeric sound ids are not its data2.db
// rowids, so no id table is invented here — sounds are addressed by name.
using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.Content;

namespace CubeGame.Presentation.Visual
{
    /// <summary>Resolves sound names/events to staged Alpha audio files.</summary>
    public sealed class AlphaSoundLibrary
    {
        public const string Status =
            "ALPHA_DATA: 93 sounds from data2.db; event->sound mapping UNKNOWN except mission complete (Q-09)";

        /// <summary>Directory name inside the staged content cache.</summary>
        public const string DirectoryName = "sounds";

        /// <summary>The only event whose sound is proven.</summary>
        public const string MissionCompleteEvent = "mission_complete";

        readonly string _directory;
        readonly HashSet<string> _names;

        /// <param name="contentRoot">Staged content cache root (the folder that holds "sounds").</param>
        public AlphaSoundLibrary(string contentRoot)
        {
            _directory = string.IsNullOrEmpty(contentRoot) ? null : Path.Combine(contentRoot, DirectoryName);
            _names = new HashSet<string>(AlphaSoundCatalog.Names, StringComparer.OrdinalIgnoreCase);
        }

        public string DirectoryPath => _directory;

        public int Count => _names.Count;

        /// <summary>True when Alpha ships a sound with this name (inventory check, no file access).</summary>
        public bool HasSound(string soundName) => !string.IsNullOrEmpty(soundName) && _names.Contains(soundName);

        /// <summary>Staged file of a sound; false when the name is not Alpha's or the file was not staged.</summary>
        public bool TryResolve(string soundName, out string path)
        {
            path = null;
            if (!HasSound(soundName) || _directory == null) return false;
            string candidate = Path.Combine(_directory, soundName);
            if (!File.Exists(candidate)) return false;
            path = candidate;
            return true;
        }

        /// <summary>
        /// Sound of a game event. Only <see cref="MissionCompleteEvent"/> is mapped (Q-09); every other event
        /// returns false instead of borrowing a random sound.
        /// </summary>
        public bool TryResolveEvent(string eventKey, out string path, out string reason)
        {
            path = null;
            if (string.Equals(eventKey, MissionCompleteEvent, StringComparison.Ordinal))
            {
                if (TryResolve(AlphaSoundCatalog.MissionComplete, out path))
                {
                    reason = string.Empty;
                    return true;
                }

                reason = "SOUND_NOT_STAGED " + AlphaSoundCatalog.MissionComplete;
                return false;
            }

            reason = $"EVENT_SOUND_UNKNOWN {eventKey} (Q-09: only mission complete is mapped)";
            return false;
        }
    }
}
