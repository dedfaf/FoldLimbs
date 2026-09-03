# FoldLimbs

In vanilla RimWorld, installing a bionic arm or bionic leg replaces the pawn's natural limb. This mod provides an alternative option to restrain and disable the existing limb when installing a bionic limb, allowing the bionic limb to be removed later and the original limb's functionality to be restored.

Disabled natural limb parts have 0% efficiency, but can still be injured and cause pain. This behavior can be disabled in the mod settings.

## Features

The following surgery types have been added:

* Disable arm / leg
* Enable arm / leg (restore a disabled natural limb)
* Install bionic limb (without removing the original limb)

In addition, this mod changes the vanilla logic for removing body parts. In vanilla RimWorld,removing a bionic body part deals 99,999 damage to the entire limb, destroying it completely and also removing the associated `Hediff_AddedPart`. When a pawn has both the **Restrained and Disabled** condition and a bionic body part, performing the bionic removal surgery with this mod will only remove the bionic's added-part hediff, leaving the original limb intact. A **natural** limb that is currently restrained/disabled is not offered for removal/amputation at all - it can only be restored by the "enable limb" surgery.

### Combat damage on restrained bionic limbs

Because the natural limb is kept under the bionic, the game treats the whole limb as solid: hits on the natural parts used to become painless, non-bleeding crack wounds. Two in-game mod settings (Mod Settings → FoldLimbs) change how hits on such a limb are handled:

* **Disable combat hits on the natural parts under a restrained bionic limb** (default off). When checked, the natural parts can never be hit; every hit on the limb becomes bionic damage on the shoulder/leg, identical to a normal vanilla bionic install.
* **Bionic damage allocation ratio** (slider + numeric input, 0 to 1, default 0.8). When the option above is unchecked, a hit that would land on a natural part of the limb has this probability to be converted into bionic damage on the shoulder/leg (vanilla-like bionic wounds - cracks, no pain, no bleeding). With `1 - ratio` probability the hit damages the natural limb as if no bionic was installed: normal flesh wound, with the same bleeding and pain as a normal limb.

### Compatibility

Obviously, this mod is incompatible with any mod that modifies the logic for bionic body parts, and there are currently no plans to provide compatibility patches.

This mod should be safe to add to an existing save, but making an additional backup of your save is recommended.

> You can use Dev Mode to restore the original limbs of colonists whose limbs have already been removed, and then use the surgery recipes provided by this mod again.

## Add or fix Translation

Just PR to this repo.

## Build

```
dotnet build .vscode
```
