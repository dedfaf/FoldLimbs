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
        /// When true, the natural parts under a restrained bionic limb are excluded from combat hit
        /// selection entirely (like vanilla, where those parts are missing): every hit on that limb
        /// becomes bionic damage on the shoulder/leg that carries the bionic. Default false.
        /// </summary>
        public bool disableNaturalPartsHitChance;

        /// <summary>
        /// When <see cref="disableNaturalPartsHitChance"/> is false: a hit that lands on a natural
        /// part of a restrained bionic limb is converted into bionic damage (a wound on the
        /// shoulder/leg carrying the bionic, like a vanilla bionic install) with this probability
        /// (0..1, default 0.8). The remaining 1 - ratio damages the natural limb as if no bionic was
        /// installed, causing normal bleeding and pain.
        /// </summary>
        public float bionicDamageRatio = 0.8f;

        private string bionicDamageRatioBuffer;

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            list.Label((TaggedString)"Bionic-on-natural-limb damage handling", -1f,
                "Configures how combat damage is handled for limbs that received a bionic while the natural limb was kept (this mod's install-bionic-without-removing surgery).");

            list.Gap(12f);
            list.CheckboxLabeled("Disable combat hits on the natural parts under a restrained bionic limb",
                ref disableNaturalPartsHitChance,
                "Checked: the natural parts under the bionic can never be hit. Every hit on the limb becomes bionic damage on the shoulder/leg - identical to a normal vanilla bionic install.\n\n" +
                "Unchecked: the natural parts can be hit. Use the damage allocation ratio below to decide how often a hit on a natural part damages the bionic instead of the natural limb.");

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
            list.Label((TaggedString)"When a hit lands on a natural part of the limb, with this probability it is converted into bionic damage on the shoulder/leg (like vanilla bionic wounds: cracks, no pain, no bleeding). With 1 - this ratio the hit damages the natural limb as if no bionic was installed, causing normal bleeding and pain.",
                -1f, "Ignored while the option above is checked.");

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
