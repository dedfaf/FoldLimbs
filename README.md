# FoldLimbs

| **en** | [zh_cn](./README_zh.md) |
|---|---|

In vanilla RimWorld, installing a bionic arm or bionic leg replaces the pawn's natural limb. This mod provides an alternative option to restrain and disable the existing limb when installing a bionic limb, allowing the bionic limb to be removed later and the original limb's functionality to be restored.

Disabled natural limb parts have 0% efficiency, but can still be injured and cause pain. This behavior can be disabled in the mod settings.

## Features

The following surgery types have been added:

* Disable arm / leg
* Enable arm / leg (restore a disabled natural limb)
* Install bionic limb (without removing the original limb)

All of these surgeries require the **Bionics** research project

In addition, this mod changes the vanilla logic for removing body parts. In vanilla RimWorld,removing a bionic body part deals 99,999 damage to the entire limb, destroying it completely and also removing the associated `Hediff_AddedPart`. When a pawn has both the **Restrained and Disabled** condition and a bionic body part, performing the bionic removal surgery with this mod will only remove the bionic's added-part hediff, leaving the original limb intact. A **natural** limb that is currently restrained/disabled is not offered for removal/amputation at all - it can only be restored by the "enable limb" surgery.

### Damage on limbs

The definitions themselves are mostly fine, but things get considerably more complicated when it comes to damage calculation.

In vanilla RimWorld, a `Shoulder`/`Leg` body part can only represent a single limb. In this mod, however, the same `Shoulder`/`Leg` body part represents both the bionic limb and the original natural limb. According to the vanilla damage logic, any damage that lands on a bionic limb inherits the `Solid` property, turning it into the special type of damage used by bionic body parts, which does not cause pain or bleeding.

This mod attempts to simulate a separate damage system to work around this limitation. All damage received by bionic limbs is redefined into a new category of hediffs (`Bionic xxx` damage). When a limb part is damaged, there is a default 0.8 probability that the damage will be converted into the corresponding bionic damage type and applied to the `Shoulder`/`Leg` body part. This probability can be adjusted in the mod settings.

When the `Shoulder`/`Leg` body part's health reaches zero, the bionic body part is removed (the bionic limb is destroyed), all bionic damage is cleared, and the body part's health is recalculated. Of course, if none of the damage was bionic damage, the original natural limb will still be destroyed after its health is recalculated.

Frostbite and burns are not affected by this mechanism. They will damage both the bionic limb and the original natural limb simultaneously.

This system is not completely ideal. For example, if the original limb attached to a `Shoulder`/`Leg` body part is already severely damaged, even a small amount of bionic damage can cause the combined body part's health to reach zero, which will trigger the destruction of the bionic limb.

If you have a better approach to handling this damage calculation, suggestions are welcome.

### Compatibility

Obviously, this mod is incompatible with any mod that modifies the logic for bionic body parts, and there are currently no plans to provide compatibility patches.

This mod is incompatible with any mod that modifies the logic for damage. (e.x. Combat Extension)

This mod should be safe to add to an existing save, but making an additional backup of your save is recommended.

> You can use Dev Mode to restore the original limbs of colonists whose limbs have already been removed, and then use the surgery recipes provided by this mod again.

## Add or fix Translation

Just PR to this repo.

## Build

```
dotnet build .vscode
```
