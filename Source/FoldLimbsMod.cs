using UnityEngine;
using Verse;

namespace FoldLimbs
{
    /// <summary>
    /// Mod entry used for the in-game settings window.
    /// </summary>
    public class FoldLimbsMod : Mod
    {
        public static FoldLimbsMod Instance { get; private set; }

        public readonly FoldLimbsSettings Settings;

        public FoldLimbsMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<FoldLimbsSettings>();
        }

        public override string SettingsCategory()
        {
            return "FoldLimbs";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
    }

    /// <summary>
    /// Configurable behavior for combat damage on limbs that have a bionic installed while the
    /// natural limb was kept (this mod's "install bionic without removing the natural limb" surgery,
    /// i.e. a limb whose shoulder/leg carries both an artificial part and the restraint hediff).
    /// </summary>
    public class FoldLimbsSettings : ModSettings
    {
        /// <summary>
        /// When true, the natural parts kept under a restrained bionic limb are excluded from combat
        /// hit selection (like vanilla, where those parts are missing) and hits on the shoulder/leg
        /// always damage the bionic: every hit on such a limb becomes bionic damage. Default false.
        /// </summary>
        public bool disableNaturalPartsHitChance;

        /// <summary>
        /// For a hit that lands on the shoulder/leg itself (the part that carries both the bionic and
        /// the kept original limb), this is the probability (0..1, default 0.8) that the hit damages
        /// the bionic (bionic mirror wound: no pain, no bleeding, reduced efficiency like vanilla).
        /// With 1 - ratio the hit hurts the original limb: an ordinary flesh wound on the same part
        /// with bleeding and pain. Ignored while <see cref="disableNaturalPartsHitChance"/> is true.
        /// Elemental damage (burn/frostbite/acid/beam) always hits both layers and ignores this ratio.
        /// </summary>
        public float bionicDamageRatio = 0.8f;

        private string bionicDamageRatioBuffer;

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            list.Label((TaggedString)"Bionic-on-natural-limb damage handling", -1f,
                "Configures how combat damage is handled for limbs that received a bionic while the natural limb was kept (this mod's install-bionic-without-removing surgery). The kept natural limb parts under the bionic are ordinary flesh and can be hit; hits on the shoulder/leg itself are split by the ratio below into bionic and original-limb damage.");

            list.Gap(12f);
            list.CheckboxLabeled("Disable combat hits on the natural parts under a restrained bionic limb",
                ref disableNaturalPartsHitChance,
                "Checked: the natural parts kept under the bionic can never be hit and hits on the shoulder/leg always damage the bionic - identical to a normal vanilla bionic install (where those parts are missing).\n\n" +
                "Unchecked: natural parts can be hit (they take ordinary flesh damage) and hits on the shoulder/leg are allocated between the bionic and the original limb by the ratio below.");

            list.Gap(14f);
            list.Label("Bionic damage allocation ratio: " + bionicDamageRatio.ToStringPercent());
            float previousRatio = bionicDamageRatio;
            bionicDamageRatio = list.Slider(bionicDamageRatio, 0f, 1f);
            if (!Mathf.Approximately(previousRatio, bionicDamageRatio))
            {
                bionicDamageRatioBuffer = null;
            }

            Rect fieldRect = list.GetRect(26f);
            fieldRect.xMin += 12f;
            Widgets.TextFieldNumericLabeled(fieldRect, "Manual input (0 to 1):", ref bionicDamageRatio,
                ref bionicDamageRatioBuffer, 0f, 1f);

            list.Gap(6f);
            list.Label((TaggedString)"For hits that land on the shoulder/leg itself, this is the probability that the damage is bionic damage ('bionic crack/gunshot/...', no pain, no bleeding, reduced efficiency like a vanilla bionic). With 1 - this ratio the hit hurts the original limb: an ordinary flesh wound on the same part with bleeding and pain. Burn/frostbite damage always affects both the bionic and the natural limb instead of being allocated.",
                -1f, "Elemental (burn/frostbite/acid/beam) damage ignores this ratio.");

            list.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref disableNaturalPartsHitChance, "disableNaturalPartsHitChance", false);
            Scribe_Values.Look(ref bionicDamageRatio, "bionicDamageRatio", 0.8f);
        }
    }
}
