# 0.25.3 — Alt menu activation

This update keeps the 0.25.2 angled cards, LIST picker, NONE/REMOVE actions, mounting selection, weapon presentation and guarded inventory flow. It changes how the interface opens and adds a bottom control hint.

- **Hold Left Alt** opens the pose and cursor together; release closes. Hold is the fresh default.
- **Toggle**, available in F12 → Controls → Menu activation (Hold / Toggle), opens on Alt-down. A later plain Alt tap closes on release. Alt+R or clicking while Alt is held leaves Toggle open.
- The menu key can be **LeftAlt**, **RightAlt** or **None**. The default mounting-position shortcut remains Left Alt + R.
- **Right-click closes LIST only. Escape closes the interface.** Click a thumbnail to install or replace; use NONE or REMOVE to uninstall.
- The bottom hint shows the menu key, selection, close-list and mounting-position controls. Wheel paging still works but is omitted from the hint.

The advanced O fallback remains: O opens the pose separately, and the legacy cursor modifier controls the cursor. Existing saved bindings remain valid. An O-opened session does not become an Alt-opened session just because Alt is pressed.

This is an input update on the 0.25.2 interface. Cards, the LIST picker, animation assets, hand presentation, inventory rules and transaction behavior remain from that baseline. Closing cancels a request still waiting to begin; it does not undo a native operation already submitted.

Verification passed: 7,920 core assertions, zero compiler warnings/errors and 318 structural checks. The compiled standalone entry-point check found no reachable native inspection entry. The isolated Unity UI fixture passed 220 checks and produced seven screenshots across 1080p, 1440p and 3440×1440, using synthetic items/icons and zero native inventory actions. The 44 scoped transaction, pose, icon and request source files and all 136 animation assets, including 129 bundles, are byte-identical to 0.25.2.

The local installation was verified as version 0.25.3 with the expected DLL hash and 133 payload files checked. LeftAlt/Hold was set; other configuration values, markers and reports were preserved. This verifies the installed files, not gameplay.

Physical Alt-input and native inventory testing in game are still pending. Check Hold and Toggle, plain Alt taps, Alt+R, clicking while Alt is held, right-clicking out of LIST, Escape, focus loss and the O fallback. Earlier animation receipts and gameplay screenshots do not establish live acceptance of these new input transitions.

[User guide](USER_GUIDE.md) · [Configuration](CONFIGURATION.md) · [Troubleshooting](TROUBLESHOOTING.md)
