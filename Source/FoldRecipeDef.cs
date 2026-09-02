using RimWorld;
using Verse;

namespace FoldLimbs
{
    /// <summary>Which limb type a fold recipe targets.</summary>
    public enum FoldLimbKind
    {
        /// <summary>An arm (the Shoulder body part).</summary>
        Arm,

        /// <summary>A leg (the Leg body part).</summary>
        Leg
    }

    /// <summary>Which side a fold recipe targets.</summary>
    public enum FoldSide
    {
        Left,
        Right
    }

    /// <summary>
    /// Custom RecipeDef used by the fold surgeries. Stores the target limb type and side so a
    /// single generic worker class can serve all recipes ("fold left arm", "fold right leg", etc.).
    /// The fields are filled from XML via the "Class" attribute.
    /// </summary>
    public class FoldRecipeDef : RecipeDef
    {
        /// <summary>Which limb type this recipe targets.</summary>
        public FoldLimbKind limbKind;

        /// <summary>Which side this recipe targets.</summary>
        public FoldSide side;

        /// <summary>True for the "install bionic (folded)" recipes; false for plain fold recipes.</summary>
        public bool bionic;

        /// <summary>The vanilla body part def this recipe is applied on.</summary>
        public BodyPartDef TargetBodyPartDef =>
            limbKind == FoldLimbKind.Arm ? BodyPartDefOf.Shoulder : BodyPartDefOf.Leg;

        /// <summary>
        /// The wound-anchor tag that uniquely identifies the targeted side on the human body
        /// (e.g. "LeftShoulder", "RightLeg"). It is stable and never localized.
        /// </summary>
        public string TargetWoundAnchorTag =>
            limbKind == FoldLimbKind.Arm
                ? (side == FoldSide.Left ? "LeftShoulder" : "RightShoulder")
                : (side == FoldSide.Left ? "LeftLeg" : "RightLeg");
    }
}
