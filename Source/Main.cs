using System.Reflection;
using HarmonyLib;
using Verse;

namespace FoldLimbs
{
    /// <summary>
    /// Mod entry point. Applies all Harmony patches at startup.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Main
    {
        static Main()
        {
            var harmony = new Harmony("foldlimbs.foldlimbs");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[FoldLimbs] FoldLimbs loaded.");
        }
    }
}
