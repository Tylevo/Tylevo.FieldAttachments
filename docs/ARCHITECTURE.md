# Architecture

The client plugin owns input, F12 settings, presentation lifetime and explicit attachment requests. Game-independent selection, layout and guard logic live under `src/Core/`; client observations, Unity UI and native inventory integration live under `src/Runtime/` in the complete source package.

## Input and cards

The Alt menu tracks Hold or Toggle intent separately from physical key state. Hold opens presentation and cursor together and closes on release. Toggle opens on key-down; a later plain tap closes on release, while Alt+R or a pointer click keeps it open. A local native input child captures the virtual cursor and drains owned mouse releases. An O-opened session retains the separate legacy cursor modifier.

The 0.25.2 cards retain their Studio projection: 17° pitch, −39° yaw, 30° roll, perspective 1400 and reference canvas 1902×992. Weapon-following motion stays bounded, and native slot leaders identify the selected mounting point. The 0.25.3 input update preserves this layout.

Clicking LIST opens a two-column picker for the selected mounting point. Wheel movement and page buttons browse carried choices; NONE in the picker or REMOVE on the card requests removal. Pointer press/release identity and generation checks reject stale gestures after paging, slot changes, refresh or closing. Right-click closes the picker; Escape closes the interface.

## Standalone presentation

The public attachment pose uses a private sampled skeleton and an independent animation clock. It does not start native weapon inspection, replace the native controller, or pause native animation playback. Unsupported poses have no native-inspection fallback.

UnityToolkit's player loop applies the visual presentation after native late update and restores owned components before the next native update or gameplay interruption. The established 0.25.2 hand presentation and pose timing remain unchanged by the Alt input update.

## Inventory requests

An explicit click binds the chosen item and exact native slot to a request. The action waits for presentation restoration, captures fresh observations and validates identities, fit, hands/session state, assembly contents and outgoing storage before native submission.

Replacement is two guarded moves: store the outgoing attachment, then install the chosen carried item. The continuation requires an observed successful removal and fresh validation. A failed install can leave the weapon slot empty and the old part in storage. There is no automatic retry or atomic-swap claim.

The transaction observer determines completion. Presentation may resume only after verified success, idle state and retained attachment-mode intent. Fika live inventory actions remain blocked. See [testing limits](TESTING.md).
