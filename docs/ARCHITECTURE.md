# Architecture

The client plugin owns input, F12 settings, presentation lifetime and explicit attachment requests. Game-independent selection, layout and guard logic live under `src/Core/`; client observations, Unity UI and native inventory integration live under `src/Runtime/` in the complete source package.

## Input and cards

The middle-mouse menu tracks Hold or Toggle intent separately from physical button state. A local native input child captures the virtual cursor and drains owned mouse releases. O and Left Alt keep the legacy presentation/cursor path.

The compact lists use the original Studio projection: 17° pitch, −39° yaw, 30° roll, perspective 1400 and reference canvas 1902×992. Local rectangles extend within each category's projected plane; rendering and inverse hit testing share those rectangles. Expansion preserves the original plane's pivot and category anchor. Weapon-following motion stays bounded, and native slot leaders identify the selected mounting point.

Only one category expands after a short hover delay. Wheel movement changes the displayed carried choices. The equipped row stays beneath the three visible alternatives, with removal kept separate. Press/release identity and generation checks reject stale gestures after paging, slot changes, refresh, status changes or closing. The projected planes stay fixed during a pressed gesture.

## Standalone presentation

The public attachment pose uses a private sampled skeleton and an independent animation clock. It does not start native weapon inspection, replace the native controller, or pause native animation playback. Unsupported poses have no native-inspection fallback.

UnityToolkit's player loop applies the visual presentation after native late update and restores owned components before the next native update or gameplay interruption. The established 0.25.2 hand presentation and pose timing remain unchanged by the compact UI.

## Inventory requests

An explicit click binds the chosen item and exact native slot to a request. The action waits for presentation restoration, captures fresh observations and validates identities, fit, hands/session state, assembly contents and outgoing storage before native submission.

Replacement is two guarded moves: store the outgoing attachment, then install the chosen carried item. The continuation requires an observed successful removal and fresh validation. A failed install can leave the weapon slot empty and the old part in storage. There is no automatic retry or atomic-swap claim.

The transaction observer determines completion. Presentation may resume only after verified success, idle state and retained attachment-mode intent. Fika live inventory actions remain blocked. See [testing limits](TESTING.md).
