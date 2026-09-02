using RimWorld;
using Verse;

namespace FoldLimbs
{
    /// <summary>
    /// DefOf for all defs introduced by this mod. Fields are resolved automatically at startup.
    /// </summary>
    [DefOf]
    public static class FoldLimbsDefOf
    {
        /// <summary>
        /// The hediff applied to a folded limb. It disables the limb exactly like the limb
        /// being removed (the stage uses partEfficiencyOffset = -1).
        /// </summary>
        public static HediffDef FA_Folded;

        static FoldLimbsDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FoldLimbsDefOf));
        }
    }
}
