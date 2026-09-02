using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FoldLimbs
{
    /// <summary>
    /// Worker for the "install bionic arm/leg (folded)" surgeries.
    /// Installs the vanilla bionic part like the vanilla recipes do, but keeps the natural limb
    /// in place and marks it as folded instead of surgically removing it.
    /// </summary>
    public class RecipeWorker_InstallBionicFolded : Recipe_InstallArtificialBodyPart
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
            // The natural limb and its parent must still be present - it gets folded, not replaced.
            if (!pawn.health.hediffSet.GetNotMissingParts().Contains(part))
            {
                return false;
            }
            if (part.parent != null && !pawn.health.hediffSet.GetNotMissingParts().Contains(part.parent))
            {
                return false;
            }
            // Installing on a limb that already has an artificial part makes no sense here,
            // because there is no natural limb left to fold.
            if (FoldLimbsUtility.HasAddedPartOnLimb(pawn, part))
            {
                return false;
            }
            return true;
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            bool isViolation = IsViolationOnPawn(pawn, part, Faction.OfPlayer);
            if (billDoer != null)
            {
                if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            }

            // Install the bionic part. Vanilla Hediff_AddedPart.PostAdd would mark all child parts
            // of the limb as missing (effectively removing the natural limb). We suppress that
            // while the fold surgery is applied so the natural limb stays in place.
            FoldLimbsUtility.SuppressAddedPartMissingChildren = true;
            try
            {
                pawn.health.AddHediff(recipe.addsHediff, part);
            }
            finally
            {
                FoldLimbsUtility.SuppressAddedPartMissingChildren = false;
            }

            // The natural limb is kept, but folded (disabled) instead of removed.
            if (!pawn.health.hediffSet.hediffs.Any(h => h.Part == part && h.def == FoldLimbsDefOf.FA_Folded))
            {
                pawn.health.AddHediff(FoldLimbsDefOf.FA_Folded, part);
            }

            if (isViolation)
            {
                ReportViolation(pawn, billDoer, pawn.HomeFaction, -70);
            }
        }
    }
}
