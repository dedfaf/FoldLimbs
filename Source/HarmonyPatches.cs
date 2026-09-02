using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FoldLimbs
{
    /// <summary>
    /// Patches vanilla <c>Hediff_AddedPart.PostAdd</c> so that, while a fold-bionic surgery is
    /// being applied, the child parts of the limb are NOT marked as missing. This keeps the
    /// natural limb present - it is disabled by the folded hediff instead of being surgically
    /// removed. When the suppression flag is not set, vanilla behavior is completely unchanged.
    /// </summary>
    [HarmonyPatch(typeof(Hediff_AddedPart), "PostAdd")]
    public static class Patch_Hediff_AddedPart_PostAdd
    {
        private static BodyPartRecord patchedPart;
        private static List<BodyPartRecord> originalChildParts;

        public static void Prefix(Hediff_AddedPart __instance)
        {
            if (!FoldLimbsUtility.SuppressAddedPartMissingChildren || __instance.Part == null || patchedPart != null)
            {
                return;
            }
            // Temporarily empty the child list so the vanilla method does not mark the natural
            // limb's child parts as missing.
            patchedPart = __instance.Part;
            originalChildParts = __instance.Part.parts;
            __instance.Part.parts = new List<BodyPartRecord>();
        }

        public static void Postfix()
        {
            if (patchedPart == null)
            {
                return;
            }
            patchedPart.parts = originalChildParts;
            patchedPart = null;
            originalChildParts = null;
        }
    }

    /// <summary>
    /// Makes the vanilla "remove part" surgery remove ONLY the artificial part on limbs that were
    /// created by this mod's "install bionic (folded)" surgery.
    /// Vanilla <c>Recipe_RemoveBodyPart</c> removes an artificial body part by dealing 99999
    /// surgical cut damage to the whole body part (see <c>DamagePart</c>), which destroys the
    /// entire limb - including the folded natural limb that this mod preserved. When the part
    /// carries both the folded hediff and an added part, we instead just remove the added part
    /// hediff (the natural limb underneath stays folded).
    /// </summary>
    [HarmonyPatch(typeof(Recipe_RemoveBodyPart), "DamagePart")]
    public static class Patch_Recipe_RemoveBodyPart_DamagePart
    {
        public static bool Prefix(Pawn pawn, BodyPartRecord part)
        {
            if (!FoldLimbsUtility.IsFoldedArtificialLimb(pawn, part))
            {
                return true; // run the vanilla destructive DamagePart
            }
            // Gentle removal: the bionic item is already spawned by the vanilla flow
            // (MedicalRecipesUtility.SpawnThingsFromHediffs runs before DamagePart), so we only
            // remove the added part. The folded natural limb stays in place.
            Hediff addedPart = pawn.health.hediffSet.GetDirectlyAddedPartFor(part);
            if (addedPart != null)
            {
                pawn.health.RemoveHediff(addedPart);
            }
            return false; // skip the vanilla DamagePart
        }
    }
}

