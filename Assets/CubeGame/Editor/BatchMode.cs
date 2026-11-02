using UnityEditor;
using UnityEngine;

namespace CubeGame.EditorTools
{
    /// <summary>
    /// Truthful batch-mode failure. A Unity run with "-quit" exits with code 0 even when an
    /// "-executeMethod" tool only logged an error and returned, which is one of the sources of
    /// false PASS results in tools/unity/run_pilot.ps1. Every failing tool path must call Fail()
    /// so that the process exit code and the log agree with reality.
    /// </summary>
    internal static class BatchMode
    {
        public static void Fail(string message)
        {
            Debug.LogError($"[Pilot] BATCH_FAIL: {message}");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
