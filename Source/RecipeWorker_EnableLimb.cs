using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FoldLimbs
{
    /// <summary>
    /// Worker for the "enable limb" surgeries ("enable left/right arm/leg").
    /// Removes the restraint/disabled hediff from the target limb, restoring its use.
    /// </summary>
    public class RecipeWorker_EnableLimb : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            FoldRecipeDef foldRecipe = recipe as FoldRecipeDef;
            if (foldRecipe == null || foldRecipe.TargetBodyPartDef == null)
            {
                yield break;
            }
            foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
            {
                if (part.def != foldRecipe.TargetBodyPartDef || part.woundAnchorTag != foldRecipe.TargetWoundAnchorTag)
                {
                    continue;
                }
                if (IsValidTarget(pawn, part))
                {
                    yield return part;
                }
            }
        }

        private static bool IsValidTarget(Pawn pawn, BodyPartRecord part)
        {
            // The limb and its parent must still be present.
            if (!pawn.health.hediffSet.GetNotMissingParts().Contains(part))
            {
                return false;
            }
            if (part.parent != null && !pawn.health.hediffSet.GetNotMissingParts().Contains(part.parent))
            {
                return false;
            }
            // Only limbs that are currently disabled by our restraint hediff can be enabled,
            // and only when they are otherwise natural (no artificial part installed).
            return pawn.health.hediffSet.hediffs.Any(h => h.Part == part && h.def == FoldLimbsDefOf.FA_Folded)
                && !FoldLimbsUtility.HasAddedPartOnLimb(pawn, part);
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            }
            // Remove the restraint/disabled hediff from the limb.
            foreach (Hediff h in pawn.health.hediffSet.hediffs.ToList())
            {
                if (h.Part == part && h.def == FoldLimbsDefOf.FA_Folded)
                {
                    pawn.health.RemoveHediff(h);
                }
            }
        }
    }
}
