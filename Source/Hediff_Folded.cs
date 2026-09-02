using Verse;

namespace FoldLimbs
{
    /// <summary>
    /// The hediff used for a folded limb.
    /// Its stage carries partEfficiencyOffset = -1, which makes the limb it is attached to
    /// contribute 0% to every pawn capacity - exactly like the limb being removed.
    /// When an artificial part (e.g. a bionic) is installed on the same limb the fold no longer
    /// cripples it (the bionic takes over), so CurStage returns null in that case.
    /// </summary>
    public class Hediff_Folded : HediffWithComps
    {
        public override HediffStage CurStage
        {
            get
            {
                if (Part != null
                    && pawn != null
                    && pawn.health != null
                    && pawn.health.hediffSet.HasDirectlyAddedPartFor(Part))
                {
                    return null;
                }
                return base.CurStage;
            }
        }
    }
}
