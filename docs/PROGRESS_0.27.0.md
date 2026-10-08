# 0.27.0 — Compact attachment lists

This experimental test build starts from the restored 0.25.2 source and targets the inspected SPT 4.1.6 client. It brings the approved Layout Studio compact-list interaction into the existing projected uGUI interface. The four original category planes keep their saved angles, perspective, anchors and bounded weapon following.

This page summarizes recorded development results. The repository contains documentation and the two historical gameplay screenshots; it is not the complete source or installable plugin package.

## Controls and layout

- Hold middle mouse to open presentation and use the cursor; releasing closes. F12 → Controls → Menu activation (Hold / Toggle) also offers Toggle.
- Hover a category briefly to open its carried alternatives. Only one list opens at a time; a short exit grace lets the pointer cross the connecting space.
- Wheel scrolls compatible carried alternatives. Left-click requests an explicit fit or replacement. Hovering and scrolling do not submit inventory operations.
- The current equipped attachment is the bottom attachment row. Remove current is a separate action beneath it. Empty slots show an empty state in that same place.
- Native mounting positions have previous/next arrows; Alt+R still cycles the selected exact slot. Labels use available native identity and parent information.
- Right-click closes the middle-mouse menu. The legacy O and Left Alt controls remain available; in that workflow, right-click closes only the list and O or Escape exits.

The lists use actual carried items and the existing native icon provider. Disabled changes, no compatible carried alternatives, busy transactions and action denials have distinct feedback. Candidate selections bind to exact item and native-slot identities.

## Preserved behavior

The authored animation assets, support/firing-hand rendering, standalone animation clock, interruption timing and pose-resume implementation remain from 0.25.2. The same request queue, fresh native validation, outgoing-storage checks and transaction observer execute explicit actions. A replacement remains two guarded native moves; if removal succeeds and fitting fails, the slot may remain empty. There is no automatic retry or atomic-swap claim. Existing Fika restrictions remain.

## Verification and live acceptance

The recorded C# build against the inspected SPT 4.1.6 references passes 11,106 core/metadata assertions with zero compiler warnings or errors. All 324 structural checks pass. Compiled public-entrypoint traversal confirms that the presentation path cannot reach native inspection. The recorded plugin SHA-256 is `422EEFFA41F1CB62F57FA8A1523339DE840084D1E513E130BE5DAB6FA3693088`.

The baseline comparison confirms exact preservation of 44 animation, icon-provider, request and transaction source files, plus all 136 animation asset files (129 bundles). The new input tests include Hold/Toggle, repeated same-frame callbacks, close/focus/outro release latches, mouse-button chording, native release handling and common pose intent. Geometry tests cover original anchors, expanded-plane inverse projection, hover grace, paging and stale gesture invalidation.

New settings use Hold, Middle Mouse and Compact List defaults when first loaded. Existing saved settings remain in place during a plugin-only update.

The isolated Unity interface fixture passes 1,509 checks and captures 21 screenshots at 1920×1080, 2560×1440 and 3440×1440. Hovering and scrolling emit zero action events; explicit replacement, removal and installation each emit exactly one. The recorded receipt verifies the tested DLL and 97 source hashes and covers projected meshes/inverse hits, fixed anchors, equipped-row ordering, paging, mounting changes, stale gestures, disabled/busy/unknown states and reference cleanup. See the [testing summary](TESTING.md) for the scope of these results.

This fixture uses synthetic observed item data and seeded placeholder icons to exercise the compiled UI. It does not execute native inventory operations, exercise physical middle-mouse input in EFT, or establish live SPT/Fika acceptance. Prior 0.25.2 receipts describe the unchanged baseline animation behavior.

## Changed code

`Plugin.CompactMenu.cs`, `Plugin.cs`, `MenuActivation.cs`, `PointerCapture.cs` and `AttachmentPointerInput.cs` add menu intent and release-safe native input capture. `SettingsPresentation.cs` exposes the new controls in F12. `CompactAttachmentLayout.cs`, `AttachmentOverlay.CompactList.cs` and the existing overlay partials implement the lists and feedback; `PanelProjection.cs` adds unbounded inverse mapping for the extensions. Focused core tests, the isolated Unity fixture and structural expectations cover these changes.

In game, test the HK416 first, then a pistol and another long gun. Check Hold and Toggle, opening/closing rapidly, hovering between categories, scrolling, mounting arrows, explicit install/replace/remove, and ADS/fire/reload/sprint/inventory/weapon-switch interruptions. Confirm mouse release cannot become a shot and middle mouse does not also activate freelook. Check the bottom equipped row updates only after observed native completion. Use F10 to export a failure report.
