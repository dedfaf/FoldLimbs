using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
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

    /// <summary>
    /// Redirects combat hits that would land on a natural part under a "restrained bionic" root.
    /// Every AddInjury-based damage picks its part through <c>HediffSet.GetRandomNotMissingPart</c>,
    /// so this single choke point implements both settings:
    /// - "disableNaturalPartsHitChance" = true: the natural parts are never hit (vanilla bionic
    ///   behavior - all hits on the limb land on the shoulder/leg that carries the bionic);
    /// - otherwise, a natural-part hit is sent to the bionic root with "bionicDamageRatio"
    ///   probability, and with 1 - ratio it stays on the natural part (natural flesh damage).
    /// </summary>
    [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.GetRandomNotMissingPart))]
    public static class Patch_HediffSet_GetRandomNotMissingPart
    {
        public static void Postfix(HediffSet __instance, ref BodyPartRecord __result)
        {
            if (__result == null || FoldLimbsMod.Instance == null || __instance.pawn == null)
            {
                return;
            }
            Pawn pawn = __instance.pawn;
            BodyPartRecord root = FoldLimbsUtility.TryGetFoldedBionicRoot(pawn, __result);
            if (root == null)
            {
                return;
            }
            FoldLimbsSettings settings = FoldLimbsMod.Instance.Settings;
            if (settings.disableNaturalPartsHitChance || Rand.Value < settings.bionicDamageRatio)
            {
                __result = root;
            }
        }
    }

    /// <summary>
    /// Wounds on the natural parts under a restrained bionic limb use the normal flesh wound def
    /// (the "solid" conversion to crack wounds is skipped), so a cut stays a cut, a gunshot stays a
    /// gunshot etc. - just as if no bionic was installed.
    /// </summary>
    [HarmonyPatch(typeof(HealthUtility), nameof(HealthUtility.GetHediffDefFromDamage))]
    public static class Patch_HealthUtility_GetHediffDefFromDamage
    {
        public static bool Prefix(DamageDef dam, Pawn pawn, BodyPartRecord part, ref HediffDef __result)
        {
            if (dam == null || pawn == null || part == null || dam.hediff == null || FoldLimbsMod.Instance == null)
            {
                return true;
            }
            if (!FoldLimbsUtility.IsNaturalPartUnderFoldedBionic(pawn, part))
            {
                return true;
            }
            HediffDef result = dam.hediff;
            if (part.def.IsSkinCovered(part, pawn.health.hediffSet) && dam.hediffSkin != null)
            {
                result = dam.hediffSkin;
            }
            __result = result;
            return false;
        }
    }

    /// <summary>
    /// Pain from wounds on the natural parts under a restrained bionic limb is NOT suppressed.
    /// Vanilla suppresses pain for any injury on a part that belongs to an artificial limb
    /// (<c>PartOrAnyAncestorHasDirectlyAddedParts</c>); for the kept natural limb under our bionic
    /// the wound should hurt like a normal flesh wound instead.
    /// </summary>
    [HarmonyPatch(typeof(Hediff_Injury), "PainOffset", MethodType.Getter)]
    public static class Patch_Hediff_Injury_PainOffset
    {
        private static readonly System.Reflection.FieldInfo CausesNoPainField =
            AccessTools.Field(typeof(Hediff), "causesNoPain");

        public static bool Prefix(Hediff_Injury __instance, ref float __result)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null || __instance.Part == null || FoldLimbsMod.Instance == null)
            {
                return true;
            }
            if (!FoldLimbsUtility.IsNaturalPartUnderFoldedBionic(pawn, __instance.Part))
            {
                return true;
            }
            if (pawn.Dead)
            {
                __result = 0f;
                return false;
            }
            if (CausesNoPainField != null && (bool)CausesNoPainField.GetValue(__instance))
            {
                __result = 0f;
                return false;
            }
            HediffComp_GetsPermanent comp = __instance.TryGetComp<HediffComp_GetsPermanent>();
            float num = (comp == null || !comp.IsPermanent)
                ? __instance.Severity * __instance.def.injuryProps.painPerSeverity
                : __instance.Severity * __instance.def.injuryProps.averagePainPerSeverityPermanent * comp.PainFactor;
            __result = num / pawn.HealthScale;
            return false;
        }
    }

    /// <summary>
    /// Wounds on the natural parts under a restrained bionic limb DO bleed like normal flesh wounds.
    /// Vanilla suppresses bleeding for injuries on solid parts (which our kept natural parts are,
    /// because of the artificial part on the shoulder/leg above them); here the bleeding calculation
    /// is reproduced without that suppression.
    /// </summary>
    [HarmonyPatch(typeof(Hediff_Injury), "BleedRate", MethodType.Getter)]
    public static class Patch_Hediff_Injury_BleedRate
    {
        public static bool Prefix(Hediff_Injury __instance, ref float __result)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null || __instance.Part == null || FoldLimbsMod.Instance == null)
            {
                return true;
            }
            if (!FoldLimbsUtility.IsNaturalPartUnderFoldedBionic(pawn, __instance.Part))
            {
                return true;
            }
            if (pawn.Dead)
            {
                __result = 0f;
                return false;
            }
            if (__instance.ageTicks >= AgeTicksToStopBleeding(__instance.Severity))
            {
                __result = 0f;
                return false;
            }
            if (!pawn.health.CanBleed)
            {
                __result = 0f;
                return false;
            }
            if (__instance.IsTended() || __instance.IsPermanent())
            {
                __result = 0f;
                return false;
            }
            Hediff added = pawn.health.hediffSet.GetDirectlyAddedPartFor(__instance.Part);
            if (added != null && !added.def.organicAddedBodypart)
            {
                __result = 0f;
                return false;
            }
            float num = __instance.Severity * __instance.def.injuryProps.bleedRate * pawn.RaceProps.bleedRateFactor;
            num *= __instance.Part.def.bleedRate;
            __result = num;
            return false;
        }

        private static int AgeTicksToStopBleeding(float severity)
        {
            float t = Mathf.Clamp(Mathf.InverseLerp(1f, 30f, severity), 0f, 1f);
            return 90000 + Mathf.RoundToInt(Mathf.Lerp(0f, 90000f, t));
        }
    }
}

