# User guide

This repository is the public documentation page for Field Attachments 0.27.0. It does not contain an installable plugin or the complete source package. Use an installable plugin release, or build the complete source package separately.

## Requirements and installation

- SPT 4.1.5 or 4.1.6 with the inspected EFT 40743 client, BepInEx 5 and [UnityToolkit 2.0.2 or newer](https://github.com/ArysWasTaken/UnityToolkit) compatible with that client. UnityToolkit is a separate dependency; 2.0.2 was used for this build. WTT Content Backport is optional and needed only to test its weapons.
- Close the game, back up an existing `BepInEx/plugins/Tylevo.FieldAttachments` folder and `BepInEx/config/com.tylevo.fieldattachments.cfg`, then merge the release ZIP's `BepInEx` folder into the SPT game root. Keep only one Field Attachments DLL.
- Preserve your current config during a plugin update. Keep PoseMarkers.txt if you want its historical calibration data, although standalone attachment mode does not use those markers. Older full tester ZIPs include a clean preset that replaces the mod's settings if copied.
- Start the game with a supported gun in the hideout range or a local raid. The ordinary game inspection key is separate from this mod's attachment mode.

## Quick swap

1. Hold **middle mouse** to bring the gun into its standalone attachment pose and use the cursor. Release to close. For click-to-open/click-to-close, choose **Toggle** under F12 → Controls → Menu activation (Hold / Toggle). An unsupported pose is refused without starting the game's inspect animation.
2. Hover a category briefly to open its compact list. The game view stops following mouse movement during cursor capture.
3. Use the wheel over the list to browse compatible carried items for the selected native mounting point. Hovering and scrolling never move an item.
4. Left-click a carried item to install or replace. The equipped item remains at the bottom of the list, with a separate **Remove current** action underneath. Right-click closes the menu.
5. Use the position arrows, or **Alt+R**, to cycle exact native mounting positions. The label and leader line follow the selected point. Cycling alone never moves an item.

The legacy **O** presentation key and **Left Alt** cursor modifier remain available. In that workflow, right-click closes only the open list; use O or Escape to exit attachment mode.

F10 writes a report and does not change inventory. Shift+O uses the same standalone pose; F4 calibration is inactive. The old F8/F6/F7 keyboard controls remain available in the config and are hidden from the normal F12 menu. Enter, keypad Enter or F6 stages a keyboard selection. F7 first returns an active pose without queuing an action; press F7 separately after hands are ready to confirm the staged selection.

Aim, fire, reload, sprint, weapon switching and normal native inspection cancel the custom presentation. Closing during entry fades out the current pose; closing a held pose plays its authored return. Captured menu clicks retain their input suppression through release, so they cannot become shots or aim requests. The standalone path does not start a native inspection in the background.

## What can move

The current picker handles compatible optics, muzzle devices, tactical devices and foregrips carried in accessible storage. It shows only examined, raid-moddable items accepted by the existing slot's native filter. A sight requiring a mount that is not on the gun is not offered. A bare optic mount is not shown as a completed sight.

A preassembled mount and sight, such as an ALPHA4 mount carrying a Razor, appears as one root item. Moving it keeps the child sight attached. The picker does not offer that mounted child as another carried part or assemble a mount for the player. Installed removable optic assemblies hide internal positions; a fixed parent can still expose a separately moddable child where native rules allow it. Other multi-piece categories have not been added to this assembly path.

Candidate listing is a fit check, not a promise that a move can finish. Native raid rules, conflicts, exact item and slot identity, free carried space and current hands state are checked again on click. A replacement is two native moves: old part into compatible carried space, then the chosen part onto the gun. If the second move fails, the old part stays in storage and the weapon slot can remain empty. Pending or unknown results must not be retried blindly.

## F12 and safety

F12 shows General, Controls, Display and Diagnostics. **General / Allow attachment changes** enables actual inventory actions; it defaults **OFF** on a fresh install and in the clean tester preset. Browsing and F10 reporting work while it is off. The flag has the older config key `Experiments / EnableEmptySlotInstall`, retained for saved settings. Use a test profile before enabling changes.

Live action requires the inspected native client and a supported local controller. Fika inventory actions are blocked. Pose and card display do not authorize an item move by themselves. A click waits for custom presentation restoration before submitting a native transaction. The pose returns after successful completion and verified weapon/hands idle; interruptions and uncertain outcomes stop that automatic return.

Version 0.27.0 preserves the animation and visible-hand behavior from 0.25.2 while changing the interface and menu activation. Offline checks do not establish live acceptance for every weapon or clothing combination. See [current validation](PROGRESS_0.27.0.md) and [weapon coverage](WEAPON_COVERAGE_0.25.2.md).

The 1.12× attachment playback setting changes this mod's native install/remove animation only. It does not change ordinary inspect, firing or reload speed. [Configuration](CONFIGURATION.md) lists the F12 entries.
