# Tylevo Field Attachments

Swap carried optics, muzzle devices, tactical devices and foregrips through 3D cards positioned around your weapon. Open the attachment pose, choose a mounting point and pick a compatible item without opening the full inventory.

**0.25.3 test build · SPT 4.1.5 / 4.1.6 · BepInEx client plugin**

The download has not been published yet.

[Installation](#installation) · [Controls](#controls) · [Compatibility](#compatibility-and-coverage)

![HK416 held in the attachment pose, with the current angled attachment cards](docs/images/hk416-attachment-menu.png)

*Original HK416 gameplay capture. The 0.25 card layout and rifle presentation are retained.*

<details>
<summary>View the HK416 inspection frame</summary>

![HK416 in the held presentation before the attachment cards appear](docs/images/hk416-inspection.png)

</details>

## What it does

- Displays weapon-following cards for **optics, muzzle devices, tactical devices and foregrips**, using native item thumbnails where available.
- Installs into an empty slot, removes an installed part or replaces it with a carried alternative.
- Lists examined, raid-moddable items accepted by the selected mounting point's native filter. Available storage includes pockets, rig, backpack and secure container, subject to native access rules.
- Lets you cycle between mounting points, including different handguard positions. The slot count and connection line identify the selected point.
- Moves a preassembled **optic and mount together** as one item. It keeps the sight attached to its mount and avoids listing that child sight twice.
- Uses its own held weapon presentation. Normal weapon inspection remains separate. After a successful change, the attachment pose can reopen once the weapon and hands are idle.

**Attachment changes are off on a fresh install.** Browsing still works. Enable **F12 → General → Allow attachment changes** on a test profile when you want to try actual item moves.

## Installation

Requirements: **SPT 4.1.5 or 4.1.6** with the inspected **EFT 40743 client**, **BepInEx 5**, and a compatible [UnityToolkit 2.0.2 or newer](https://github.com/ArysWasTaken/UnityToolkit). The audited build was compiled against SPT 4.1.6 with UnityToolkit 2.0.2. WTT Content Backport is optional and supplies some of the weapons included in the coverage audit.

1. Close the game. Back up an existing `BepInEx/plugins/Tylevo.FieldAttachments/` folder and `BepInEx/config/com.tylevo.fieldattachments.cfg` before updating.
2. Merge a packaged plugin release's `BepInEx` folder into your SPT game folder. Keep the plugin's `Animation` folder alongside its DLL and keep only one copy of the Field Attachments DLL installed.
3. Preserve your existing configuration when updating. A plugin-only update keeps saved settings; older tester packages may include a preset that replaces them.
4. Enter the hideout shooting range or a local raid with a supported weapon. Hold **Left Alt** to open the cards and use the cursor. Release it to close.

## Controls

These are the defaults. Saved bindings can differ; the main controls are configurable in F12.

| Action | Current control |
| --- | --- |
| Open attachment mode with cursor | Hold **Left Alt** |
| Close attachment mode | Release **Left Alt**, or press **Escape** |
| Show carried choices for a card | Click **LIST** |
| Browse the open list | Mouse wheel or page buttons |
| Install or replace a part | Click its thumbnail in **LIST** |
| Remove a part | **REMOVE** on its card, or **NONE** in **LIST** |
| Change mounting point | **Left Alt + R** while the cursor is active |
| Close the open list | Right-click while the cursor is active |
| Save a diagnostic report while the overlay is open | **F10** |
| Open mod settings | **F12** |

In **F12 → Controls → Menu activation**, choose **Hold** or **Toggle**. Hold is the default. Toggle opens on an Alt press and closes on a later plain Alt tap; using Alt+R or clicking while Alt is held keeps it open. The bottom hint shows the menu, select, close-list and mount controls.

Alt+R changes the open list's mounting point, or the hovered card's point when no list is open. It does not move an item. Mouse selection needs no separate confirmation key.

<details>
<summary>Legacy controls and saved settings</summary>

**O** still opens the legacy presentation, where holding **Left Alt** enables the cursor. **F8** opens the legacy overlay, **F9** rescans, and **Enter / keypad Enter / F6** stages a selection. **F7** is the separate opt-in empty-slot installation action. If a pose is active, F7 first returns the weapon; it needs another confirmation after the hands are ready.

These bindings and older display options remain in the configuration even when hidden from the normal F12 menu. **Shift+O** uses the same standalone presentation. **F4** calibration and saved native inspection markers are inactive in attachment mode.

See the [user guide](docs/USER_GUIDE.md) and [configuration reference](docs/CONFIGURATION.md) for the existing behavior and stored keys.

</details>

## How attachment changes work

The picker checks fit. Clicking an item then triggers fresh native checks for the item, mounting point, storage and hands state. The custom pose returns before an inventory move starts. It reopens only after a confirmed success and verified idle state, provided attachment mode is still active.

Replacement uses **two native moves**: put the old part into compatible free carried space, then install the selected part. If the second move fails, the old part stays in storage and the weapon slot can remain empty. There is no automatic rollback or retry of an uncertain result.

The mod does not take items from the stash, assemble missing mounts or rearrange storage to make room. An optic that needs a missing mount will not appear as a usable choice. Assembly handling currently covers optic/mount combinations.

Aim, fire, reload, sprint, weapon switching and normal inspection interrupt the presentation. Captured menu clicks are suppressed so they do not become shots or aim requests. Closing the menu cancels an action waiting to start; it does not undo a native move already submitted.

## Settings

F12 exposes the main controls, toggle/hold behavior, cursor speed, thumbnails and weapon-following display. **Attachment animation speed** defaults to **1.12×** and allows **1.0–1.15×** for this mod's native install/remove playback. It does not speed up normal inspection, firing or reloading.

Keep **Use authored weapon poses** enabled for the current presentation. Turning it off disables attachment poses; there is no native-inspection fallback. Existing stored setting names remain unchanged, including the older `Experiments/EnableEmptySlotInstall` key behind **Allow attachment changes**.

See [Configuration](docs/CONFIGURATION.md) for defaults and the older options that remain available in the config file.

## Compatibility and coverage

| Area | Current scope |
| --- | --- |
| Game versions | SPT 4.1.5 / 4.1.6 with the inspected EFT 40743 client. Native item moves require the matching game assembly. |
| Play context | Local raids and the active hideout shooting range |
| Fika | Live inventory actions are blocked. Headless graphics sessions skip the UI. |
| Weapon coverage | 194 registered templates from 199 definitions in the audited installation, using 129 authored animation bundles and 26 verified aliases |
| Other content mods | Coverage depends on exact registered weapon identities. Unlisted modded weapons are not automatically supported. |

The five excluded definitions are two stationary weapons, two underbarrel metadata entries and one internal test item missing the required animation states. SP-81, RShG-2 and seven reactive flare variants have presentation routes; with no supported attachment slots, they open without attachment actions.

The coverage count describes the inspected database and implementation. It does not establish a live visual pass for every weapon, clothing set or attachment combination. See the [full coverage list and exclusions](docs/WEAPON_COVERAGE_0.25.2.md).
