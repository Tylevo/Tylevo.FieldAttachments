# Tylevo Field Attachments

Change optics, muzzle devices, tactical devices and foregrips without opening your inventory. Left Alt brings up attachment cards around your weapon and lets you use the mouse to choose a part.

**1.1.0 · SPT 4.1.5 / 4.1.6 · BepInEx client plugin**

[Installation](#installation) · [Controls](#controls) · [Compatibility](#compatibility-and-coverage) · [Source code](src/Plugin.cs) · [Build instructions](BUILDING.md)

![HK416 held in the attachment pose, with the current angled attachment cards](docs/images/hk416-attachment-menu.png)

*Original HK416 gameplay showing the attachment cards and weapon presentation.*

<details>
<summary>View the HK416 inspection frame</summary>

![HK416 in the held presentation before the attachment cards appear](docs/images/hk416-inspection.png)

</details>

## What it does

- Shows cards for **optics, muzzle devices, tactical devices and foregrips** that follow your weapon, with item thumbnails where available.
- Installs into an empty slot, removes an installed part or replaces it with a carried alternative.
- Shows compatible parts you've examined and are carrying in accessible pockets, rig, backpack or secure container. The game's restrictions on changing parts in raid still apply.
- Lets you cycle between mounting points, including different handguard positions. The slot count and connection line identify the selected point.
- Moves an assembled **optic and mount together**, keeping the sight attached.
- Holds your weapon at an angle so you can see its attachments. Normal weapon inspection still works separately.

**Attachment changes are enabled on a fresh install.** Turn off **F12 → General → Allow attachment changes** to browse without moving parts. Updates preserve your saved setting.

## Installation

Requires **SPT 4.1.5 or 4.1.6 (EFT 40743)**, **BepInEx 5**, and a compatible [UnityToolkit 2.0.2 or newer](https://github.com/ArysWasTaken/UnityToolkit). The weapon packs listed below are optional.

1. Close the game. Back up an existing `BepInEx/plugins/Tylevo.FieldAttachments/` folder and `BepInEx/config/com.tylevo.fieldattachments.cfg` before updating.
2. Merge a packaged plugin release's `BepInEx` folder into your SPT game folder. Keep the plugin's `Animation` folder alongside its DLL and keep only one copy of the Field Attachments DLL installed.
3. Preserve your existing configuration when updating. A plugin-only update keeps saved settings; older tester packages may include a preset that replaces them.
4. Enter the hideout shooting range or a local raid with a supported weapon. Hold **Left Alt** to open the cards and use the cursor. Release it to close.

## Controls

Left Alt opens the menu and gives you mouse control in both Hold and Toggle modes. Choose the mode you prefer in **F12 → Controls → Menu activation**. Hold is the default.

| Action | Current control |
| --- | --- |
| Open the menu and control the mouse | Hold **Left Alt**, or press it in Toggle mode |
| Cancel attachment mode | Perform an action such as sprinting, press **Left Alt** again in Toggle mode, or release it in Hold mode |
| Show carried choices for a card | Click **LIST** |
| Install or replace a part | Click its thumbnail in **LIST** |
| Remove a part | **REMOVE** on its card, or **NONE** in **LIST** |
| Change mounting point | **Left Alt + R** while the cursor is active |
| Close the open list | Right-click while the cursor is active |
| Open mod settings | **F12** |

In Toggle mode, you can let go of Alt and keep using the mouse. Using Alt+R or clicking while Alt is held keeps the menu open; tap Alt on its own to close it.

Alt+R cycles mounting points for the open list or the card under your cursor. It won't swap a part until you choose one.

## How attachment changes work

Click **LIST** to see the parts you're carrying that fit the selected mount. Choose one and your character lowers the weapon to make the change. After a successful change, the weapon returns to the attachment view if you still have the menu open.

Leave room in your gear for anything you remove. When replacing a part, the old one goes into your inventory before the new one is fitted. If fitting the new part fails, the old one stays in your inventory and the weapon's slot may be empty.

The menu uses parts you're carrying, not items in your stash. A sight on its own won't show up if its required mount is missing from the weapon.

Mouse clicks stay in the menu, so selecting a part won't fire your weapon. Closing the menu stops changes that haven't started yet, but it can't undo an item move already underway.

## Settings

Press **F12** to change the menu key, choose Hold or Toggle, and adjust cursor speed. You can also turn item thumbnails and the cards' weapon-following movement on or off.

Custom attachment animations are always enabled.

**Attachment animation speed** controls how quickly parts are fitted or removed. It defaults to **1.12×**, with a range of **1.0–1.15×**. It leaves normal inspection, firing and reloading speeds alone.

## Compatibility and coverage

**1.1.0 supports 996 weapon entries:** 159 standard SPT entries and 837 from seven optional packs. This total includes caliber and configuration variants.

The expanded coverage below requires [Field Attachments 1.1.0](https://github.com/Tylevo/Tylevo.FieldAttachments/releases/tag/v1.1.0). Older 1.0.0 downloads support 194 entries.

| Area | Current scope |
| --- | --- |
| Game versions | SPT 4.1.5 / 4.1.6 (EFT 40743) |
| Where it works | Local raids and the hideout shooting range |
| Fika | Attachment changes are not supported |
| Weapons in 1.1.0 | 996 entries, including the optional packs below |
| Other weapon mods | Support must be added for each weapon; unlisted weapons won't work automatically |

### Supported weapon packs in 1.1.0

**These packs are optional and are not included with Field Attachments.** Install whichever ones you want separately. Standard SPT weapons work without them.

| Optional pack | Supported entries | Pack version checked |
| --- | ---: | --- |
| [WTT - Content Backport](https://sp-mod.com/mod/2512/wtt-content-backport) | 14 | 2.0.5 |
| [Epic's All in One](https://sp-mod.com/mod/1263/epics-all-in-one) | 16 | 5.0.1 |
| [ECOT - Eukyre's Consortium of Things](https://sp-mod.com/mod/2195/ecot-eukyres-consortium-of-things) | 6 | 2.0.1 |
| [Definitive Weapon Variants](https://sp-mod.com/mod/2010/definitive-weapon-variants) | 687 | 4.3.2 + [4.3.3 hotfix](https://github.com/Szonszczyk/DefinitiveWeaponVariants-csharp/releases/tag/v4.3.3) |
| [WTT- Armory](https://sp-mod.com/mod/2246/wtt-armory) | 80 | 3.0.0 |
| [C11 - True North](https://sp-mod.com/mod/2423/c11-true-north) | 29 | 4.0.3 |
| [Make Shotguns Great Again!](https://sp-mod.com/mod/1453/make-shotguns-great-again) | 5 | 1.17.0 |

Pack coverage was checked with the listed downloads on **SPT 4.1.6**. Use a pack release that matches your SPT version and install its listed dependencies. New weapons from later pack updates and custom DWV variants need their own support.

Some supported weapons have no attachment slots to change. Not every weapon and clothing combination has been tested in game yet.
