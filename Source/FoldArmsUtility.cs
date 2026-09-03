using System.Linq;
using Verse;

namespace FoldLimbs
{
    /// <summary>
    /// Shared helpers for the FoldLimbs mod.
    /// </summary>
    public static class FoldLimbsUtility
    {
        /// <summary>
        /// While this is set, the Harmony patch on <c>Hediff_AddedPart.PostAdd</c> keeps the
        /// natural limb's child parts intact when an artificial body part is installed.
        /// Used by the "install bionic (folded)" recipes so the natural limb is folded instead
        /// of surgically removed.
        /// </summary>
        public static bool SuppressAddedPartMissingChildren;

        /// <summary>
        /// Returns true if the given limb (the part itself, any of its child parts, or any of its
        /// ancestors) already has an artificial body part installed. A limb that is partially or
        /// fully artificial cannot be folded, and there is no natural limb left to fold when
        /// installing a "folded" bionic.
        /// </summary>
        public static bool HasAddedPartOnLimb(Pawn pawn, BodyPartRecord part)
        {
            foreach (BodyPartRecord bp in part.GetPartAndAllChildParts())
            {
                if (pawn.health.hediffSet.HasDirectlyAddedPartFor(bp))
                {
                    return true;
                }
            }
            for (BodyPartRecord parent = part.parent; parent != null; parent = parent.parent)
            {
                if (pawn.health.hediffSet.HasDirectlyAddedPartFor(parent))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if the given body part carries the restraint/disabled hediff and the limb is
        /// otherwise natural (no artificial part on the part, its children or its ancestors).
        /// Such limbs can be restored with the "enable limb" surgery and should NOT be offered for
        /// amputation (the fold is reversible, amputation would permanently destroy the limb).
        /// </summary>
        public static bool IsFoldedNaturalLimb(Pawn pawn, BodyPartRecord part)
        {
            if (FoldLimbsDefOf.FA_Folded == null
                || pawn == null
                || pawn.health == null
                || pawn.health.hediffSet == null
                || part == null)
            {
                return false;
            }
            bool hasFolded = false;
            foreach (Hediff h in pawn.health.hediffSet.hediffs)
            {
                if (h.Part == part && h.def == FoldLimbsDefOf.FA_Folded)
                {
                    hasFolded = true;
                    break;
                }
            }
            return hasFolded && !HasAddedPartOnLimb(pawn, part);
        }

        /// <summary>
        /// Returns true if the given body part carries both the folded hediff and an artificial
        /// (added) part on the same part - i.e. a limb produced by the "install bionic (folded)"
        /// surgeries of this mod. For such limbs the natural limb underneath was NOT surgically
        /// removed, so removing the bionic must only remove the added part.
        /// </summary>
        public static bool IsFoldedArtificialLimb(Pawn pawn, BodyPartRecord part)
        {
            if (FoldLimbsDefOf.FA_Folded == null
                || pawn == null
                || pawn.health == null
                || pawn.health.hediffSet == null
                || part == null)
            {
                return false;
            }
            bool hasFolded = false;
            bool hasAddedPart = false;
            foreach (Hediff h in pawn.health.hediffSet.hediffs)
            {
                if (h.Part != part)
                {
                    continue;
                }
                if (h.def == FoldLimbsDefOf.FA_Folded)
                {
                    hasFolded = true;
                }
                else if (h is Hediff_AddedPart)
                {
                    hasAddedPart = true;
                }
                if (hasFolded && hasAddedPart)
                {
                    return true;
                }
            }
            return false;
        }
    }
}


