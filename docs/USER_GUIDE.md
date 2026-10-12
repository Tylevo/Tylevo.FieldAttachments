# User guide

This guide describes the **unreleased 1.1.1 source**. The latest published download is **1.1.0** and does not include all of the features below.

Hold **Left Alt** to open the attachment menu and cursor. Release it to close. If you prefer to leave the menu open, select **Toggle** in F12 under **Menu activation (Hold / Toggle)**. Press Alt to open it and press Alt again to close.

The top-left header identifies the current layer and weapon. The bottom keycaps show menu controls; the separate **LAYER** button switches between Basic and Multitool. You can also assign your own layer shortcut. No layer shortcut is assigned by default.

## Changing an attachment

1. Click **LIST** on a weapon card to see compatible carried parts for that mounting point. Use the wheel or page buttons to browse more parts.
2. Click a part to install it or replace the current attachment.
3. Choose **NONE** in the list, or **REMOVE** on the card, to remove an optional installed part. Required parts can only be replaced.
4. Use **Alt+R** to cycle mounting points for the open list or hovered card. This only changes which mounting point you are viewing.

**Right-click** closes the list. **Escape** closes the menu. Gameplay actions such as sprinting can also cancel it. While using the menu, left-click selects and right-click closes the list; they do not fire or aim.

## Comparing parts

Hover a carried choice to compare it with the current weapon. **WEAPON STATS** stays at the top right and shows ergonomics, vertical recoil, horizontal recoil and weight. A small nearby summary shows the ergonomics and vertical-recoil changes. Green means an improvement; red means a tradeoff. Higher ergonomics is better, while lower recoil and weight are better.

Hovering only previews the change. Reading or clicking the statistics does not move an item. Unavailable values are shown as unknown rather than zero, and the preview is not a guarantee that the game's inventory operation will succeed.

## Which parts appear?

**Basic** has four cards: optics, muzzle devices, tactical devices and foregrips. It lists compatible, examined parts in accessible carried storage that can normally be changed during a raid. A sight on its own needs its mount to be fitted to the weapon first.

**Multitool** has three cards: stocks, handguards and pistol grips. Carry a **Leatherman Multitool** in accessible carried storage to change these parts. There is no Receiver card. The status above the controls explains missing-tool or mounting-point restrictions.

An assembled sight and mount can appear as one item and move together. Supported stock, handguard and pistol-grip assemblies also stay intact. Every required child part must be present, and the assembly must pass the game's compatibility checks. The menu does not build an assembly, transfer children to a replacement, or bypass locked mounting points.

For a required stock, handguard or pistol grip, choose a compatible carried replacement from **LIST**. **REMOVE** and **NONE** cannot leave a required mounting point empty. The game must be able to validate both the outgoing part and its replacement, including any required attachments.

Keep enough free space in your carried storage for a removed part. If a replacement fails after removing the old part, the old part remains in storage and the weapon slot can be empty. Check both before trying again.

## Attachment changes

**Allow attachment changes** is enabled on fresh installs. Turn it off in **F12** to browse without moving items. Updates preserve your saved setting, so check it if you previously disabled changes.

Use a supported weapon in a local raid or the hideout shooting range. Attachment changes are not supported in Fika.

## Presentation and optional mods

Menu entry blends into the attachment view, and changing layers briefly fades the cards in. **Faster menu close (trial)** is optional and off by default; it shortens the normal exit from a fully open menu.

Stance Overhaul and Realism Common Library are optional. With the supported builds installed, **Fluid stance transitions** follows their stance timing and waits for movement to settle before showing the cards. Field Attachments works without either mod and does not change their settings or FOV.

See [configuration](CONFIGURATION.md) for useful settings or [troubleshooting](TROUBLESHOOTING.md) if something does not work. Installation instructions are in the [README](../README.md).
