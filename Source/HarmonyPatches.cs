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
    /// Only applies while the "disable combat hits on the natural parts" option is on: hits that would
    /// land on a natural part under a "restrained bionic" root are sent to the bionic root instead
    /// (as if those natural parts were missing, like a vanilla bionic install).
    /// When the option is off, natural parts keep receiving their own (natural flesh) damage, and the
    /// "bionic damage allocation ratio" is rolled in <see cref="Patch_HealthUtility_GetHediffDefFromDamage"/>
    /// for every hit that lands on the shoulder/leg root itself.
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
            if (FoldLimbsMod.Instance.Settings.disableNaturalPartsHitChance)
            {
                __result = root;
            }
        }
    }

    /// <summary>
    /// Chooses the wound def for a hit part.
    /// 1) On a natural part kept under a restrained bionic limb the normal flesh wound def is used
    ///    (the "solid" conversion to crack wounds is skipped), so a cut stays a cut etc. - just as if
    ///    no bionic was installed.
    /// 2) On an artificial (bionic) body part - vanilla installs and this mod's installs alike - the
    ///    wound is replaced by the corresponding "bionic" mirror wound (FA_BionicCrack, ...). This
    ///    keeps bionic damage clearly distinct from damage to the original limb, while pain, bleeding,
    ///    healing and the efficiency loss behave exactly like the vanilla wound on the same part.
    /// </summary>
    [HarmonyPatch(typeof(HealthUtility), nameof(HealthUtility.GetHediffDefFromDamage))]
    public static class Patch_HealthUtility_GetHediffDefFromDamage
    {
        // vanilla wound def name -> "bionic" mirror wound def.
        private static Dictionary<string, HediffDef> mirrorWounds;

        public static HediffDef GetMirrorWound(HediffDef vanilla)
        {
            if (mirrorWounds == null)
            {
                mirrorWounds = new Dictionary<string, HediffDef>();
                string[] mirrorNames = { "FA_BionicCrack", "FA_BionicGunshot", "FA_BionicBurn", "FA_BionicFrostbite", "FA_BionicStab", "FA_BionicAcidBurn", "FA_BionicBeamWound" };
                string[] vanillaNames = { "Crack", "Gunshot", "Burn", "Frostbite", "Stab", "AcidBurn", "BeamWound" };
                for (int i = 0; i < vanillaNames.Length; i++)
                {
                    HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(mirrorNames[i]);
                    if (def != null)
                    {
                        mirrorWounds[vanillaNames[i]] = def;
                    }
                }
            }
            if (vanilla == null)
            {
                return null;
            }
            if (mirrorWounds.TryGetValue(vanilla.defName, out HediffDef found))
            {
                return found;
            }
            return null;
        }

        /// <summary>
        /// The wound def vanilla would apply to an artificial body part for this damage, mapped to its
        /// "bionic" mirror wound (or null when there is no mirror for it).
        /// </summary>
        public static HediffDef GetBionicMirrorWoundFor(DamageDef dam, Pawn pawn, BodyPartRecord artificialPart)
        {
            if (dam == null || dam.hediff == null || pawn == null || artificialPart == null)
            {
                return null;
            }
            HediffDef wound = dam.hediff;
            if (artificialPart.def.IsSkinCovered(artificialPart, pawn.health.hediffSet) && dam.hediffSkin != null)
            {
                wound = dam.hediffSkin;
            }
            if (artificialPart.def.IsSolid(artificialPart, pawn.health.hediffSet.hediffs) && dam.hediffSolid != null)
            {
                wound = dam.hediffSolid;
            }
            return GetMirrorWound(wound);
        }

        /// <summary>
        /// Chooses the wound def for a hit part:
        /// 1) natural parts kept under a restrained bionic limb always get the ordinary flesh wound
        ///    (skip the "solid"-to-crack conversion), so a cut stays a cut etc.;
        /// 2) a hit on the shoulder/leg that carries the bionic is rolled against the mod's
        ///    "bionic damage allocation ratio": with that probability it is bionic damage (mirror
        ///    wound, no pain, no bleeding, efficiency loss like vanilla), otherwise it is treated as
        ///    original-limb damage - an ordinary flesh wound on the same part that hurts and bleeds;
        /// 3) a plain artificial part (bionic installed the vanilla way, children already gone) always
        ///    receives the "bionic" mirror wound.
        /// </summary>
        public static bool Prefix(DamageDef dam, Pawn pawn, BodyPartRecord part, ref HediffDef __result)
        {
            if (dam == null || pawn == null || pawn.health == null || pawn.health.hediffSet == null || part == null || dam.hediff == null)
            {
                return true;
            }

            bool foldedRoot = FoldLimbsUtility.IsFoldedArtificialLimb(pawn, part);
            bool naturalChild = FoldLimbsUtility.IsNaturalPartUnderFoldedBionic(pawn, part);

            if (!foldedRoot && !naturalChild)
            {
                // 3) Plain artificial part (vanilla install or an unrelated artificial part): bionic.
                Hediff added = pawn.health.hediffSet.GetDirectlyAddedPartFor(part);
                if (added != null && !added.def.organicAddedBodypart)
                {
                    HediffDef mirror = GetBionicMirrorWoundFor(dam, pawn, part);
                    if (mirror != null)
                    {
                        __result = mirror;
                        return false;
                    }
                }
                return true;
            }

            if (naturalChild)
            {
                // 1) Natural parts kept under our bionic: ordinary flesh wound.
                HediffDef flesh = dam.hediff;
                if (part.def.IsSkinCovered(part, pawn.health.hediffSet) && dam.hediffSkin != null)
                {
                    flesh = dam.hediffSkin;
                }
                __result = flesh;
                return false;
            }

            // 2) Root of a restrained bionic limb: allocate by the ratio (elemental damage like
            //    burn/frostbite is never allocated - it is forced to the bionic here, and the
            //    elemental dual patch additionally applies it to the natural limb).
            bool wantNatural = false;
            if (FoldLimbsMod.Instance != null
                && !Patch_DamageWorker_AddInjury_ElementalDual.IsElemental(dam, pawn, part))
            {
                FoldLimbsSettings settings = FoldLimbsMod.Instance.Settings;
                wantNatural = !settings.disableNaturalPartsHitChance && Rand.Value >= settings.bionicDamageRatio;
            }
            if (!wantNatural)
            {
                HediffDef mirror = GetBionicMirrorWoundFor(dam, pawn, part);
                if (mirror != null)
                {
                    __result = mirror;
                    return false;
                }
                return true;
            }
            HediffDef naturalWound = dam.hediff;
            if (part.def.IsSkinCovered(part, pawn.health.hediffSet) && dam.hediffSkin != null)
            {
                naturalWound = dam.hediffSkin;
            }
            __result = naturalWound;
            return false;
        }
    }

    /// <summary>
    /// Pain from damage to the ORIGINAL limb of a "restrained bionic" limb is NOT suppressed.
    /// Vanilla suppresses pain for any injury on a part that belongs to an artificial limb
    /// (<c>PartOrAnyAncestorHasDirectlyAddedParts</c>). Original-limb damage - whether on the natural
    /// parts kept under the bionic, or an ordinary flesh wound on the shoulder/leg root itself (the
    /// "1 - ratio" branch) - should hurt like a normal flesh wound instead. "Bionic" mirror wounds on
    /// the root keep the vanilla suppression (no pain).
    /// </summary>
    [HarmonyPatch(typeof(Hediff_Injury), "PainOffset", MethodType.Getter)]
    public static class Patch_Hediff_Injury_PainOffset
    {
        private static readonly System.Reflection.FieldInfo CausesNoPainField =
            AccessTools.Field(typeof(Hediff), "causesNoPain");

        public static bool Prefix(Hediff_Injury __instance, ref float __result)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null || FoldLimbsMod.Instance == null)
            {
                return true;
            }
            if (!FoldLimbsUtility.IsNaturalWoundOnFoldedBionicLimb(pawn, __instance))
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
    /// Original-limb damage on a restrained bionic limb DOES bleed like a normal flesh wound.
    /// Vanilla suppresses bleeding for injuries on solid parts (which the kept natural parts and the
    /// shoulder/leg root are, because of the artificial part). Here the bleeding calculation is
    /// reproduced without that suppression for non-bionic wounds on the original limb; "bionic"
    /// mirror wounds on the root stay bloodless (vanilla behavior).
    /// </summary>
    [HarmonyPatch(typeof(Hediff_Injury), "BleedRate", MethodType.Getter)]
    public static class Patch_Hediff_Injury_BleedRate
    {
        public static bool Prefix(Hediff_Injury __instance, ref float __result)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null || FoldLimbsMod.Instance == null)
            {
                return true;
            }
            if (!FoldLimbsUtility.IsNaturalWoundOnFoldedBionicLimb(pawn, __instance))
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

    /// <summary>
    /// Extra destruction layer for folded-bionic shoulders/legs: when the shared health pool of the
    /// root part (the record that carries BOTH the artificial added part and the folded natural limb)
    /// is emptied, the limb is NOT destroyed outright. Instead the bionic is sacrificed first:
    ///   * the added part (bionic) is removed - exactly like the gentle bionic removal surgery,
    ///     the folded natural limb stays in place (disabled by the FA_Folded hediff);
    ///   * every bionic mirror wound (FA_Bionic*) on the root is removed (bionic damage is damage to
    ///     the bionic, which is now gone);
    ///   * the part's health is recalculated from the natural wounds that remain. If the natural
    ///     wounds alone already empty the pool, the whole limb is destroyed as vanilla does.
    /// Natural (original-limb) wounds on the root still count against the same pool, so bionic blows
    /// can never kill the natural limb by themselves: the worst they can do is pop the bionic off.
    /// </summary>
    [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.AddDirect))]
    public static class Patch_HediffSet_AddDirect_BionicSacrifice
    {
        public static bool Prefix(HediffSet __instance, Hediff hediff)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null || pawn.Dead || FoldLimbsMod.Instance == null || hediff == null || hediff.def == null)
            {
                return true;
            }
            if (hediff is Hediff_MissingPart)
            {
                return true;
            }
            BodyPartRecord part = hediff.Part;
            if (part == null || part == pawn.RaceProps.body.corePart || !part.def.destroyableByDamage)
            {
                return true;
            }
            if (!(hediff is Hediff_Injury injury) || !injury.destroysBodyParts || injury.Severity <= 0f)
            {
                return true;
            }
            if (!FoldLimbsUtility.IsFoldedArtificialLimb(pawn, part))
            {
                return true;
            }

            float maxHealth = part.def.GetMaxHealth(pawn);
            if (maxHealth <= 0f)
            {
                return true;
            }

            float naturalWounds = 0f;
            float bionicWounds = 0f;
            for (int i = 0; i < __instance.hediffs.Count; i++)
            {
                Hediff h = __instance.hediffs[i];
                if (h.Part != part || !(h is Hediff_Injury hi) || !hi.destroysBodyParts)
                {
                    continue;
                }
                if (FoldLimbsUtility.IsBionicMirrorWoundDef(hi.def))
                {
                    bionicWounds += hi.Severity;
                }
                else
                {
                    naturalWounds += hi.Severity;
                }
            }

            bool incomingIsBionic = FoldLimbsUtility.IsBionicMirrorWoundDef(injury.def);
            float healthAfterAdd = maxHealth - naturalWounds - bionicWounds - injury.Severity;
            float naturalHealthAfterAdd = maxHealth - naturalWounds - (incomingIsBionic ? 0f : injury.Severity);
            bool wouldDestroy = healthAfterAdd < 0.5f;
            bool naturalLimbSurvives = naturalHealthAfterAdd >= 0.5f;
            if (!wouldDestroy || !naturalLimbSurvives)
            {
                return true;
            }

            Hediff addedPart = __instance.GetDirectlyAddedPartFor(part);
            if (addedPart != null)
            {
                pawn.health.RemoveHediff(addedPart);
            }
            List<Hediff> toRemove = new List<Hediff>();
            for (int j = 0; j < __instance.hediffs.Count; j++)
            {
                Hediff h2 = __instance.hediffs[j];
                if (h2.Part == part && h2 is Hediff_Injury bi && FoldLimbsUtility.IsBionicMirrorWoundDef(bi.def))
                {
                    toRemove.Add(h2);
                }
            }
            for (int k = 0; k < toRemove.Count; k++)
            {
                pawn.health.RemoveHediff(toRemove[k]);
            }
            if (incomingIsBionic)
            {
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Vanilla suppresses the display of removed ("destroyed") natural body parts that lie under an
    /// installed artificial body part: <c>HediffSet.CacheMissingPartsCommonAncestors</c> skips every
    /// part that has a directly added part on itself or an ancestor, and the health card lists
    /// MissingParts only from that list. This patch makes the health card additionally list the
    /// natural sub-parts removed under a bionic/prosthetic (for example the arm/hand amputated when
    /// a bionic arm was installed, or a natural part destroyed under a restrained-bionic limb).
    /// Only the TOP-MOST hidden missing parts are added - if a parent part was removed, its children
    /// stay hidden, exactly like the vanilla "common ancestor" display rule.
    /// Gameplay is untouched: the shared cached list that drives hit coverage, the health summary,
    /// healing pods and resurrection is only temporarily extended while the health card is being
    /// drawn and is restored immediately afterwards.
    /// </summary>
    [HarmonyPatch(typeof(HealthCardUtility), nameof(HealthCardUtility.DrawHediffListing))]
    public static class Patch_HealthCardUtility_ShowRemovedPartsUnderBionic
    {
        private static readonly System.Reflection.FieldInfo CachedMissingPartsField =
            AccessTools.Field(typeof(HediffSet), "cachedMissingPartsCommonAncestors");

        private static List<Hediff_MissingPart> savedCache;
        private static bool active;

        public static void Prefix(Pawn pawn)
        {
            if (FoldLimbsMod.Instance == null
                || pawn == null
                || pawn.health == null
                || pawn.health.hediffSet == null
                || CachedMissingPartsField == null)
            {
                return;
            }
            HediffSet set = pawn.health.hediffSet;
            // If a previous call did not finish (an exception), restore first so we never build on
            // a stale augmented list.
            if (active)
            {
                CachedMissingPartsField.SetValue(set, savedCache);
                active = false;
                savedCache = null;
            }
            List<Hediff_MissingPart> extra = FoldLimbsUtility.GetMissingPartsUnderArtificialPart(pawn);
            if (extra == null || extra.Count == 0)
            {
                return;
            }
            savedCache = (List<Hediff_MissingPart>)CachedMissingPartsField.GetValue(set);
            List<Hediff_MissingPart> combined = new List<Hediff_MissingPart>();
            if (savedCache != null)
            {
                combined.AddRange(savedCache);
            }
            combined.AddRange(extra);
            CachedMissingPartsField.SetValue(set, combined);
            active = true;
        }

        public static void Postfix(Pawn pawn)
        {
            if (active && pawn != null && pawn.health != null && pawn.health.hediffSet != null)
            {
                CachedMissingPartsField?.SetValue(pawn.health.hediffSet, savedCache);
                active = false;
                savedCache = null;
            }
        }
    }

    /// <summary>
    /// Elemental damage (burn / frostbite / acid / beam - the wound types that have a "bionic"
    /// mirror def) affects BOTH layers of a restrained bionic limb at once, instead of being
    /// allocated to only one: fire, frost etc. reach the natural limb under the bionic shell too.
    /// Whenever a single elemental wound is finalized on either layer (the bionic root or a natural
    /// part of the limb), the same damage is applied to the other layer as well (mirror wound on the
    /// bionic, ordinary flesh wound on the natural limb).
    /// </summary>
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "FinalizeAndAddInjury",
        new[] { typeof(Pawn), typeof(float), typeof(DamageInfo), typeof(DamageWorker.DamageResult) })]
    public static class Patch_DamageWorker_AddInjury_ElementalDual
    {
        private static readonly HashSet<string> ElementalMirrorDefNames = new HashSet<string>
        {
            "FA_BionicBurn", "FA_BionicFrostbite", "FA_BionicAcidBurn", "FA_BionicBeamWound"
        };

        public static void Postfix(DamageWorker_AddInjury __instance, Pawn pawn, float totalDamage,
            DamageInfo dinfo, DamageWorker.DamageResult result)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || FoldLimbsMod.Instance == null)
            {
                return;
            }
            if (dinfo.HitPart == null || totalDamage <= 0f)
            {
                return;
            }
            BodyPartRecord hit = dinfo.HitPart;
            BodyPartRecord root;
            bool hitIsRoot;
            if (FoldLimbsUtility.IsFoldedArtificialLimb(pawn, hit))
            {
                root = hit;
                hitIsRoot = true;
            }
            else
            {
                root = FoldLimbsUtility.TryGetFoldedBionicRoot(pawn, hit);
                hitIsRoot = false;
            }
            if (root == null)
            {
                return;
            }
            if (!IsElemental(dinfo.Def, pawn, root))
            {
                return;
            }
            if (hitIsRoot)
            {
                // The bionic root was burned/frozen - the natural limb underneath takes it too.
                BodyPartRecord natural = FoldLimbsUtility.GetAnyNaturalPartUnderFoldedBionic(pawn, root);
                if (natural != null)
                {
                    HediffDef wound = HealthUtility.GetHediffDefFromDamage(dinfo.Def, pawn, natural);
                    AddInjury(pawn, natural, wound, totalDamage);
                }
            }
            else
            {
                // A natural part was burned/frozen - the bionic shell takes it too (mirror wound,
                // bypassing the bionic/natural allocation roll).
                HediffDef wound = Patch_HealthUtility_GetHediffDefFromDamage.GetBionicMirrorWoundFor(dinfo.Def, pawn, root);
                AddInjury(pawn, root, wound, totalDamage);
            }
        }

        public static bool IsElemental(DamageDef dam, Pawn pawn, BodyPartRecord artificialPart)
        {
            HediffDef mirror = Patch_HealthUtility_GetHediffDefFromDamage.GetBionicMirrorWoundFor(dam, pawn, artificialPart);
            return mirror != null && ElementalMirrorDefNames.Contains(mirror.defName);
        }

        private static void AddInjury(Pawn pawn, BodyPartRecord part, HediffDef def, float severity)
        {
            if (def == null || part == null || pawn.health.hediffSet.PartIsMissing(part))
            {
                return;
            }
            Hediff_Injury injury = (Hediff_Injury)HediffMaker.MakeHediff(def, pawn, part);
            injury.Severity = severity;
            pawn.health.AddHediff(injury, null, null, null);
        }
    }
}

