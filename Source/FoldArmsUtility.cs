using System.Collections.Generic;
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

        /// <summary>
        /// If the given body part is a natural part that lies under a "restrained bionic" root (a
        /// part that has both a directly added artificial part and the restraint hediff on it, i.e.
        /// the outcome of this mod's install-bionic-without-removing surgery), returns that root.
        /// Returns null for the root itself and for everything else.
        /// </summary>
        public static BodyPartRecord TryGetFoldedBionicRoot(Pawn pawn, BodyPartRecord part)
        {
            if (FoldLimbsDefOf.FA_Folded == null
                || pawn == null
                || pawn.health == null
                || pawn.health.hediffSet == null
                || part == null)
            {
                return null;
            }
            for (BodyPartRecord p = part.parent; p != null; p = p.parent)
            {
                if (pawn.health.hediffSet.HasDirectlyAddedPartFor(p) && IsFoldedArtificialLimb(pawn, p))
                {
                    return p;
                }
            }
            return null;
        }

        /// <summary>
        /// True if the given body part is a natural part that lies under a "restrained bionic" root
        /// (see <see cref="TryGetFoldedBionicRoot"/>). Wounds on such parts should behave as normal
        /// flesh wounds (the natural limb was kept, only restrained), while wounds on the root itself
        /// behave as vanilla bionic damage.
        /// </summary>
        public static bool IsNaturalPartUnderFoldedBionic(Pawn pawn, BodyPartRecord part)
        {
            return TryGetFoldedBionicRoot(pawn, part) != null;
        }

        /// <summary>
        /// True if the given hediff def is one of this mod's "bionic" mirror wound defs
        /// (FA_BionicCrack, FA_BionicGunshot, ...). Used to tell bionic damage apart from natural
        /// (original limb) damage that may exist on the same body part record.
        /// </summary>
        public static bool IsBionicMirrorWoundDef(HediffDef def)
        {
            return def != null && def.defName.StartsWith("FA_Bionic", System.StringComparison.Ordinal);
        }

        /// <summary>
        /// True if the given injury represents damage to the ORIGINAL limb of a "restrained bionic"
        /// limb: either the injury is on a natural part kept under the bionic, or it is a non-bionic
        /// (ordinary flesh) wound on the shoulder/leg root that carries the bionic. Such wounds should
        /// hurt and bleed like normal flesh wounds; "bionic" mirror wounds on the root stay painless.
        /// </summary>
        public static bool IsNaturalWoundOnFoldedBionicLimb(Pawn pawn, Hediff_Injury injury)
        {
            if (injury == null || pawn == null || pawn.health == null || pawn.health.hediffSet == null || injury.Part == null)
            {
                return false;
            }
            if (IsNaturalPartUnderFoldedBionic(pawn, injury.Part))
            {
                return true;
            }
            if (IsFoldedArtificialLimb(pawn, injury.Part))
            {
                return !IsBionicMirrorWoundDef(injury.def);
            }
            return false;
        }

        /// <summary>
        /// Picks a random present natural part of the limb below a "restrained bionic" root
        /// (weighted by coverage), or null when there is none. Used e.g. by elemental (burn/frostbite)
        /// damage that affects the natural limb underneath the bionic as well.
        /// </summary>
        public static BodyPartRecord GetAnyNaturalPartUnderFoldedBionic(Pawn pawn, BodyPartRecord root)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || root == null)
            {
                return null;
            }
            List<BodyPartRecord> candidates = new List<BodyPartRecord>();
            foreach (BodyPartRecord p in root.GetPartAndAllChildParts())
            {
                if (p == root || p.coverageAbs <= 0f || pawn.health.hediffSet.PartIsMissing(p))
                {
                    continue;
                }
                candidates.Add(p);
            }
            if (candidates.Count == 0)
            {
                return null;
            }
            return candidates.RandomElementByWeight(p => p.coverageAbs);
        }
    }
}


