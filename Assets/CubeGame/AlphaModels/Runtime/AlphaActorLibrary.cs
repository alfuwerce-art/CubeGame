// CubeGame.AlphaModels — actor library over the local content cache (D86 M0-01/M0-06A).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeGame.AlphaModels
{
    /// <summary>
    /// Loads and caches actors from AlphaContentSource. The game asks for an actor by id (the
    /// pipeline's variant name, e.g. "d86_human_0x02_greatsword"); an unknown actor returns null
    /// plus the reason, never a substitute model.
    /// </summary>
    public sealed class AlphaActorLibrary
    {
        static AlphaActorLibrary _shared;

        public static AlphaActorLibrary Shared => _shared ?? (_shared = new AlphaActorLibrary(AlphaContentSource.Resolve()));

        /// <summary>Point the shared library at another cache (tests, tools, a packaged build).</summary>
        public static void Configure(AlphaContentSource source)
        {
            _shared = new AlphaActorLibrary(source ?? AlphaContentSource.Resolve());
        }

        public AlphaContentSource Source { get; }
        readonly Dictionary<string, AlphaActorDefinition> _actors = new Dictionary<string, AlphaActorDefinition>(StringComparer.Ordinal);

        public AlphaActorLibrary(AlphaContentSource source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IReadOnlyList<string> ActorIds => Source.ActorIds;
        public AlphaContentState State => Source.State;
        public string Diagnosis => Source.Diagnosis;

        public bool TryGet(string id, out AlphaActorDefinition def)
        {
            if (id != null && _actors.TryGetValue(id, out def)) return def.Ready;
            def = Source.Load(id);
            if (id != null) _actors[id] = def;
            return def.Ready;
        }

        /// <summary>Load every actor of the cache (used by editor validation and integration tests).</summary>
        public List<AlphaActorDefinition> LoadAll(List<string> problems = null)
        {
            var list = new List<AlphaActorDefinition>();
            foreach (var id in Source.ActorIds)
            {
                TryGet(id, out var def);
                list.Add(def);
                if (!def.Ready && problems != null)
                    foreach (var p in def.Problems) problems.Add(id + ": " + p);
            }
            return list;
        }
    }
}
