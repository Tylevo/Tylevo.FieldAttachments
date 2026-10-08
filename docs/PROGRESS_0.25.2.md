# 0.25.2 — Pistol hand presentation and weapon coverage

Version 0.25.2 retains the O / Left Alt attachment workflow and the angled REMOVE/LIST cards shown in the repository's gameplay screenshots. It adds firing-hand presentation for 32 exact handgun templates, the newly fitted RShG-2 and seven reactive flare routes. Previously supported rifles and snipers retain their established support-arm presentation path. All 123 original authored animation bundles remain unchanged.

This repository contains public documentation and two original gameplay screenshots. The complete source, animation bundles, test fixtures and installable plugin are separate packages.

## Controls

- Press O to open or close the standalone attachment pose. F12 → Controls → Toggle attachment mode changes this to hold behavior when disabled.
- Hold Left Alt to use the cursor, then click LIST to browse compatible carried parts. The wheel and page buttons change the list page.
- Click a carried thumbnail to install or replace. Click REMOVE on the card or NONE in LIST to uninstall.
- Alt+R cycles the exact native mounting point while the cursor is active. It does not move an item.
- Right-click closes the list; O or Escape exits attachment mode. F10 writes a diagnostic report without moving inventory.

## Presentation and coverage

The firing-hand change redirects rendering through the authored arm pose for the explicitly supported weapons. Native body bones, colliders and gameplay IK remain unchanged. RShG-2 has separate coverage for all four native idle variants because its ready and normal grips differ.

The [weapon coverage summary](WEAPON_COVERAGE_0.25.2.md) accounts for 199 audited definitions: 194 registered routes and five explicit exclusions. The release contains 129 authored animation bundles and 26 verified aliases. Six new fits cover HK337, FNX-45, UMP40, SP-81, RShG-2 and yellow RSP-30; exact aliases extend the supported variants.

The independent animation clock, private sampler, early-close fade, interruption handling and native inventory barriers remain. Attachment mode does not start native inspection. Shift+O uses the same standalone path, and F4 native calibration is inactive.

## Recorded validation

| Check | Recorded result |
| --- | --- |
| Core and metadata suite | 7,875 assertions passed |
| Compilation | Zero warnings and errors against the inspected SPT 4.1.6 client |
| Structural checks | 308 passed |
| Standalone playback | 129 bundles and 26 alias loads; 814,228 checks |
| Renderer helper | 36 scenarios; 302,884 checks; 219 private objects disposed |
| Focused RShG-2 fixture | 32 scenarios across four native idle variants; 37,366 checks |

Compiled public-entrypoint checks confirm that the standalone presentation cannot reach native inspection. The recorded 0.25.2 plugin SHA-256 is `37CA87FD043BA3484E42F294DDCD9EAFAB1CBC4A6DB89A770AA9C50190C7222A`.

These are build and isolated offline results. They verify sampling, skinning, native-state preservation, cancellation, ownership and cleanup within their fixtures. They do not establish live clothing fit, player-loop timing or acceptance for every weapon and attachment combination. No new test execution is implied by publishing this documentation.

The two gameplay screenshots show earlier confirmed rifle behavior and the retained REMOVE/LIST interface. Their footage predates the pistol correction. **Live acceptance of the 0.25.2 pistol correction and newly added weapon routes remains pending.** See the [testing summary](TESTING.md).

## Live checks

1. Check pistol firing-hand grip during entry, hold, early close and normal return, starting with the Desert Eagle L6 WTS and another pistol.
2. Interrupt presentation with ADS, fire, reload, sprint, inventory and weapon switching. Both displayed arms must restore before native action.
3. Check Alt-menu installation, removal and replacement. The pose must restore before a native transaction and reopen only after confirmed success and verified idle.
4. Recheck a previously supported rifle and sniper, then test the added weapon routes individually in the hideout range and a local raid.

Use F10 to report an unavailable route, hand misalignment or failed action. Review diagnostic files before sharing. [User guide](USER_GUIDE.md) · [Configuration](CONFIGURATION.md) · [Troubleshooting](TROUBLESHOOTING.md)
