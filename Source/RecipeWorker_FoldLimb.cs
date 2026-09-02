using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FoldLimbs
{
    /// <summary>
    /// Worker for the "fold limb" surgeries ("fold left/right arm/leg").
    /// Adds the folded hediff to the target limb, disabling it as if it had been removed.
    /// </summary>
    public class RecipeWorker_FoldLimb : Recipe_Surgery
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
            // The limb itself and its parent must still be present.
            if (!pawn.health.hediffSet.GetNotMissingParts().Contains(part))
            {
                return false;
            }
            if (part.parent != null && !pawn.health.hediffSet.GetNotMissingParts().Contains(part.parent))
            {
                return false;
            }
            // Cannot fold a limb that has an artificial part installed (on itself or any of its parts).
            if (FoldLimbsUtility.HasAddedPartOnLimb(pawn, part))
            {
                return false;
            }
            // Cannot fold a limb that is already folded.
            if (pawn.health.hediffSet.hediffs.Any(h => h.Part == part && h.def == FoldLimbsDefOf.FA_Folded))
            {
                return false;
            }
            return true;
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
            pawn.health.AddHediff(FoldLimbsDefOf.FA_Folded, part);
        }
    }
}
