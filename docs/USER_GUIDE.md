# User guide

This guide covers Field Attachments 0.25.3: the existing REMOVE/LIST interface with Alt opening the presentation and cursor together. This public repository contains documentation only; an installable plugin release or the complete source package is separate.

## Requirements and installation

- SPT 4.1.5 or 4.1.6 with the inspected EFT 40743 client, BepInEx 5 and [UnityToolkit 2.0.2 or newer](https://github.com/ArysWasTaken/UnityToolkit) compatible with that client. UnityToolkit is a separate dependency; 2.0.2 was used for this build. WTT Content Backport is optional and needed only to test its weapons.
- Close the game, back up an existing `BepInEx/plugins/Tylevo.FieldAttachments` folder and `BepInEx/config/com.tylevo.fieldattachments.cfg`, then merge the release ZIP's `BepInEx` folder into the SPT game root. Keep only one Field Attachments DLL.
- Preserve your current config during a plugin update. Keep PoseMarkers.txt if you want its historical calibration data, although standalone attachment mode does not use those markers. Older full tester ZIPs include a clean preset that replaces the mod's settings if copied.
- Start the game with a supported gun in the hideout range or a local raid. The ordinary game inspection key is separate from this mod's attachment mode.

## Quick swap

1. Hold **Left Alt** to open the standalone attachment pose and its cursor together. Release Alt to close. The game view stops following mouse movement during cursor capture. An unsupported pose is refused without starting the game's inspect animation.
2. For a persistent menu, choose **Toggle** in F12 → Controls → Menu activation (Hold / Toggle). Alt opens on key-down. A later plain Alt tap closes on release; Alt+R or clicking while Alt is held leaves Toggle open.
3. Click **LIST** on a weapon card. The small two-column list contains compatible carried items for that exact native mounting point. Use the wheel or page buttons if needed.
4. Click a thumbnail to install into an empty slot or replace an installed item. Click **NONE** in the list, or **REMOVE** on the card, to uninstall. No separate arm click is needed.
5. While holding Alt, press **R** to cycle the open list's mounting position or the hovered card's position. The slot count and leader line show the selected point, including handguard sides. Alt+R alone never moves an item.

Right-click closes LIST only. Escape closes the interface. The bottom tooltip shows the menu key, selection, close-list and mounting-position controls. Wheel paging still works in LIST even though it is omitted from the tooltip.

The advanced **O** fallback still opens the pose separately. In an O-opened session, hold the legacy cursor modifier (Left Alt by default) to use the menu; releasing Alt returns camera control without closing that O session. O again or Escape closes it.

F10 writes a report and does not change inventory. Shift+O uses the same standalone pose; F4 calibration is inactive. The old F8/F6/F7 keyboard controls remain available in the config and are hidden from the normal F12 menu. Enter, keypad Enter or F6 stages a keyboard selection. F7 first returns an active pose without queuing an action; press F7 separately after hands are ready to confirm the staged selection.

Aim, fire, reload, sprint, weapon switching and normal native inspection cancel the custom presentation. Closing during entry fades out the current pose; closing a held pose plays its authored return. Alt-captured menu clicks retain their input suppression, so they cannot become shots or aim requests. The standalone path does not start a native inspection in the background.

## What can move

The current picker handles compatible optics, muzzle devices, tactical devices and foregrips carried in accessible storage. It shows only examined, raid-moddable items accepted by the existing slot's native filter. A sight requiring a mount that is not on the gun is not offered. A bare optic mount is not shown as a completed sight.

A preassembled mount and sight, such as an ALPHA4 mount carrying a Razor, appears as one root item. Moving it keeps the child sight attached. The picker does not offer that mounted child as another carried part or assemble a mount for the player. Installed removable optic assemblies hide internal positions; a fixed parent can still expose a separately moddable child where native rules allow it. Other multi-piece categories have not been added to this assembly path.

Candidate listing is a fit check, not a promise that a move can finish. Native raid rules, conflicts, exact item and slot identity, free carried space and current hands state are checked again on click. A replacement is two native moves: old part into compatible carried space, then the chosen part onto the gun. If the second move fails, the old part stays in storage and the weapon slot can remain empty. Pending or unknown results must not be retried blindly.

## F12 and safety

F12 shows General, Controls, Display and Diagnostics. **General / Allow attachment changes** enables actual inventory actions; it defaults **OFF** on a fresh install and in the clean tester preset. Browsing and F10 reporting work while it is off. The flag has the older config key `Experiments / EnableEmptySlotInstall`, retained for saved settings. Use a test profile before enabling changes.

Live action requires the inspected native client and a supported local controller. Fika inventory actions are blocked. Pose and card display do not authorize an item move by themselves. A click waits for custom presentation restoration before submitting a native transaction. The pose returns after successful completion and verified weapon/hands idle; interruptions and uncertain outcomes stop that automatic return.

Version 0.25.3 changes menu activation and the control hint while retaining the 0.25.2 interface, animation, hand presentation and inventory rules. See the [current changes](PROGRESS_0.25.3.md) and [weapon coverage](WEAPON_COVERAGE_0.25.2.md).

The 1.12× attachment playback setting changes this mod's native install/remove animation only. It does not change ordinary inspect, firing or reload speed. [Configuration](CONFIGURATION.md) lists the F12 entries.
