using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FoldLimbs
{
    /// <summary>
    /// Replacement worker for the vanilla "RemoveBodyPart" recipe.
    /// A limb that carries the restraint/disabled hediff (and has no artificial part installed)
    /// is deliberately NOT offered for removal: the fold is reversible through the "enable limb"
    /// surgery, while the vanilla removal operation would permanently amputate the whole limb.
    /// Limbs that have an artificial part installed (e.g. a folded-bionic limb) are still offered,
    /// so the bionic can be removed without destroying the natural limb underneath.
    /// The worker is wired up by patching the vanilla recipe's workerClass (see 1.6/Patches).
    /// </summary>
    public class Recipe_RemoveBodyPart_NoFoldAmputation : Recipe_RemoveBodyPart
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            foreach (BodyPartRecord part in base.GetPartsToApplyOn(pawn, recipe))
            {
                if (FoldLimbsUtility.IsFoldedNaturalLimb(pawn, part))
                {
                    continue;
                }
                yield return part;
            }
        }
    }
}
