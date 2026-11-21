using System;
using System.Collections.Generic;
using CubeGame.M0Pilots.World;
using CubeGame.M0Pilots.Movement;
using CubeGame.M0Pilots.Combat;
using CubeGame.M0Pilots.Npc;
using CubeGame.M0Pilots.Items;
using CubeGame.M0Pilots.Economy;

namespace CubeGame.M0Pilots
{
    /// <summary>
    /// M0-00 preparation lab: five isolated pilots + contracts for economy, quests and saves.
    /// Compiled against the real CubeGame.Core sources (no Unity Editor, no UnityEngine).
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.WriteLine("CubeGame M0-00 full-systems preparation lab");
            Console.WriteLine("Core sources are compiled as-is; no Assets/Packages/ProjectSettings changes.");
            Console.WriteLine("Evidence tables: tools/m0_transfer/data/alpha_confirmed_tables.json");
            Console.WriteLine("Generated constants: tools/m0_pilots/Generated/AlphaTables.g.cs");
            Console.WriteLine(new string('-', 78));

            var results = new List<PilotResult>
            {
                WorldPilot.Run(),
                MovementPilot.Run(),
                CombatPilot.Run(),
                NpcPilot.Run(),
                ItemPilot.Run(),
                EconomyQuestSavePilot.Run(),
            };

            int passed = 0;
            int failed = 0;
            Console.WriteLine();
            Console.WriteLine(new string('-', 78));
            for (int i = 0; i < results.Count; i++)
            {
                PilotResult result = results[i];
                passed += result.Passed;
                failed += result.Failed;
                Console.WriteLine(result.Name + ": checks=" + (result.Passed + result.Failed)
                                  + " failed=" + result.Failed);
            }

            string status = failed == 0 ? "PASS" : "FAIL";
            Console.WriteLine("M0_PILOTS_RESULT=" + status + " checks=" + (passed + failed) + " failed=" + failed);
            return failed == 0 ? 0 : 1;
        }
    }
}
