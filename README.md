# Tylevo Field Attachments

Swap carried optics, muzzle devices, tactical devices and foregrips through tilted 3D cards positioned around your weapon. Hold middle mouse, hover a category and pick a compatible item without opening the full inventory.

**0.27.0 experimental test build · SPT 4.1.5 / 4.1.6 · BepInEx client plugin**

This repository is the project's documentation page. Source packages and installable builds are separate; no public download is attached here yet.

[Installation](#installation) · [Controls](#controls) · [Compatibility](#compatibility-and-coverage) · [Report a problem](#reporting-a-problem) · [Build from source](#build-from-source)

![HK416 held in the attachment pose with the earlier 0.25.2 attachment-card layout](docs/images/hk416-attachment-menu.png)

*Original HK416 gameplay capture from the 0.25.2 audit package. The weapon pose and card angles are retained in 0.27.0, but this image shows the earlier REMOVE/LIST interface. The new compact lists are not pictured; the footage also predates the pistol correction.*

<details>
<summary>View the HK416 inspection frame</summary>

![HK416 in the held presentation before the attachment cards appear](docs/images/hk416-inspection.png)

*Second original gameplay frame from the same pre-0.27.0 footage.*

</details>

## What it does

- Displays weapon-following cards for **optics, muzzle devices, tactical devices and foregrips**, using native item thumbnails where available.
- Opens the presentation and cursor together with **middle mouse**. Hold mode closes on release; Toggle mode is available in F12.
- Expands one category on hover, with up to three carried alternatives visible at once. Scroll to browse more; the equipped attachment stays at the bottom, above a separate **Remove current** action.
- Installs into an empty slot, removes an installed part or replaces it with a carried alternative.
- Lists examined, raid-moddable items accepted by the selected mounting point's native filter. Available storage includes pockets, rig, backpack and secure container, subject to native access rules.
- Lets you cycle between mounting points using the list arrows or **Alt+R**. The position indicator and connection line identify the selected point; available parent information appears beside the equipped item.
- Moves a preassembled **optic and mount together** as one item. It keeps the sight attached to its mount and avoids listing that child sight twice.
- Uses its own held weapon presentation. Normal weapon inspection remains separate. After a successful change, the attachment pose can reopen once the weapon and hands are idle.

**Attachment changes are off on a fresh install.** Browsing still works. Enable **F12 → General → Allow attachment changes** on a test profile when you want to try actual item moves.

## Installation

Requirements: **SPT 4.1.5 or 4.1.6** with the inspected **EFT 40743 client**, **BepInEx 5**, and a compatible [UnityToolkit 2.0.2 or newer](https://github.com/ArysWasTaken/UnityToolkit). The audited build was compiled against SPT 4.1.6 with UnityToolkit 2.0.2. WTT Content Backport is optional and supplies some of the weapons included in the coverage audit.

1. Close the game. Back up an existing `BepInEx/plugins/Tylevo.FieldAttachments/` folder and `BepInEx/config/com.tylevo.fieldattachments.cfg` before updating.
2. Merge a packaged plugin release's `BepInEx` folder into your SPT game folder. Keep the plugin's `Animation` folder alongside its DLL and keep only one copy of the Field Attachments DLL installed.
3. Preserve your existing configuration when updating. A plugin-only update keeps saved settings; older tester packages may include a preset that replaces them.
4. Enter the hideout shooting range or a local raid with a supported weapon. **Hold middle mouse** to present the weapon and use the cursor. Hover a category to browse its choices.

Source downloads must be built before installation. Follow [Build from source](#build-from-source), or use an installable plugin release.

## Controls

These are the defaults. Saved bindings can differ; the main controls are configurable in F12.

| Action | Current control |
| --- | --- |
| Open the presentation and use the cursor | Hold **middle mouse** by default |
| Switch Hold / Toggle activation | **F12 → Controls → Menu activation (Hold / Toggle)** |
| Show carried choices for a category | Hover its card briefly |
| Browse the active list | Mouse wheel |
| Install or replace a part | **Left-click** its carried-item row |
| Remove a part | **Remove current**, below the equipped row |
| Change mounting point | Previous/next arrows or **Left Alt + R** while the cursor is active |
| Close the middle-mouse menu | Release in Hold mode; press again in Toggle mode; **right-click** or **Escape** in either mode |
| Legacy presentation and cursor | **O**, then hold **Left Alt** |
| Save a diagnostic report while the overlay is open | **F10** |
| Open mod settings | **F12** |

Alt+R changes the active or most recently focused category's mounting point. Hovering, scrolling and changing mounting positions never move an item. A left-click requests the selected action without a separate confirmation key.

<details>
<summary>Legacy controls and saved settings</summary>

**O** still opens or closes the standalone presentation; hold **Left Alt** to use its cursor. In this legacy workflow, right-click closes only the open list; use O or Escape to exit attachment mode.

The existing keyboard path is retained: **F8** opens the overlay, **F9** rescans, and **Enter / keypad Enter / F6** stages a selection. **F7** is the separate opt-in empty-slot installation action. If a pose is active, F7 returns the weapon and queues no inventory move. Press F7 separately after the hands are ready to confirm a staged selection.

These bindings and older display options remain in the configuration even when hidden from the normal F12 menu. **Shift+O** uses the same standalone presentation. **F4** calibration and saved native inspection markers are inactive in attachment mode.

See the [user guide](docs/USER_GUIDE.md) and [configuration reference](docs/CONFIGURATION.md) for the existing behavior and stored keys.

</details>

## How attachment changes work

The picker checks fit. Clicking an item then triggers fresh native checks for the item, mounting point, storage and hands state. The custom pose returns before an inventory move starts. It reopens only after a confirmed success and verified idle state, provided attachment mode is still active.

Replacement uses **two native moves**: put the old part into compatible free carried space, then install the selected part. If the second move fails, the old part stays in storage and the weapon slot can remain empty. There is no automatic rollback or retry of an uncertain result.

The mod does not take items from the stash, assemble missing mounts or rearrange storage to make room. An optic that needs a missing mount will not appear as a usable choice. Assembly handling currently covers optic/mount combinations.

Aim, fire, reload, sprint, weapon switching and normal inspection interrupt the presentation. Captured menu clicks are suppressed so they do not become shots or aim requests. Closing the menu cancels an action waiting to start; it does not undo a native move already submitted.

## Settings

F12 exposes **Attachment menu key** (middle mouse or a side mouse button), **Menu activation (Hold / Toggle)** and **Compact attachment list**, alongside the existing controls, cursor speed, thumbnails and weapon-following display. New defaults are middle mouse, Hold and compact lists enabled. Saved O and Left Alt bindings remain available.

**Attachment animation speed** defaults to **1.12×** and allows **1.0–1.15×** for this mod's native install/remove playback. It does not speed up normal inspection, firing or reloading.

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

## Current validation

Version 0.27.0 starts from the restored 0.25.2 baseline and adds the compact lists and middle-mouse controls. It preserves the existing animation timing, pistol and long-gun hand presentation, pose-resume behavior and guarded native inventory path.

The recorded 0.27.0 checks are:

| Check | Recorded result |
| --- | --- |
| Core and metadata tests | 11,106 assertions passed |
| Plugin compilation | Zero warnings or errors against the inspected SPT 4.1.6 installation |
| Source structural checks | 324 passed |
| Isolated Unity UI fixture | 1,509 checks passed; 21 screenshots at 1080p, 1440p and 3440×1440 |
| UI action events in the fixture | Zero from hover/wheel; one each from explicit install, replace and remove clicks |
| Baseline preservation | 44 animation, icon-provider, request and transaction source files unchanged; all 136 animation asset files, including 129 bundles, unchanged |

These are build and offline validation results. The Unity UI fixture uses synthetic item data and placeholder icons; it does not perform native inventory moves. **Live middle-mouse input, native thumbnails, attachment transactions and interruption behavior still need in-game testing.** The supplied screenshots show earlier rifle gameplay, not live acceptance of the compact interface or every weapon route.

The unchanged animation baseline also has earlier offline receipts for all 129 bundles, 26 alias loads, 36 renderer scenarios and 32 focused RShG-2 scenarios. Those results were not rerun for this documentation update.

[Current release notes and live checklist](docs/PROGRESS_0.27.0.md) · [Verification scope](docs/TESTING.md) · [Changelog](CHANGELOG.md)

## Reporting a problem

With the overlay open, press **F10** to save diagnostics. F10 does not move items. Include your SPT and UnityToolkit versions, relevant content mods, weapon name/template ID, attachment and mounting point, range or raid, reproduction steps, and a screenshot or short clip. Add the relevant report and journal after checking their contents before sharing.

For missing choices, failed moves or a pose that does not return, start with [Troubleshooting](docs/TROUBLESHOOTING.md). If a replacement leaves an empty slot, check carried storage for the old part. If the operation status is unknown, follow the diagnostic message before attempting another move.

## Build from source

These instructions apply to the complete source package, which includes the build tools and pinned animation bundles. This documentation repository does not contain that package. With Windows, **.NET SDK 8 or newer**, and your own compatible SPT client with BepInEx and UnityToolkit installed, run from the source-package root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build.ps1 -SptPath 'D:\YourSPT'
python .\tools\Validate-Source.py
```

The build runs the core tests, checks the pinned animation hashes and prepares `dist/BepInEx/plugins/Tylevo.FieldAttachments/`. Installation is a separate step unless you add `-Install`.

The C# build uses the included animation bundles and does not require Unity Editor. Reauthoring animations requires **Unity 2022.3.43f1** and matching local references. Game and UnityToolkit assemblies are external dependencies.

## Documentation and license

[User guide](docs/USER_GUIDE.md) · [Documentation index](docs/INDEX.md) · [Architecture](docs/ARCHITECTURE.md) · [Development](docs/DEVELOPMENT.md) · [Animation pipeline](docs/ANIMATION_PIPELINE.md) · [Release process](docs/RELEASING.md)

Code is licensed under [MIT](LICENSE). See [Asset review](docs/ASSET_REVIEW.md) for the recorded provenance questions concerning animation assets and native metadata.
