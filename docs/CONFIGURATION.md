# Configuration

For 0.27.0, the BepInEx file remains `BepInEx/config/com.tylevo.fieldattachments.cfg`. F12 shows friendly labels, but stored section/key names are stable so existing settings survive updates. F12 Reset restores the **current compiled default**, not the value previously saved by the player.

| F12 group / label | Stored section/key | Fresh default | Effect |
| --- | --- | --- | --- |
| General / Enabled | `General/Enabled` | On | Master attachment UI switch. |
| General / Allow attachment changes | `Experiments/EnableEmptySlotInstall` | Off | Enables guarded native item moves. |
| Controls / Attachment menu key | `Controls/MenuKey` | Middle mouse (`Mouse2`) | Opens presentation and captures the menu cursor. Choose Mouse2, a side button (Mouse3/Mouse4), or None to disable this shortcut. |
| Controls / Menu activation (Hold / Toggle) | `Controls/MenuActivation` | Hold | Hold closes on release; Toggle closes on another press. Right-click closes either middle-mouse menu mode. |
| Display / Compact attachment list | `Display/CompactAttachmentList` | On | Hover lists on the original tilted planes, equipped attachment at bottom. |
| Controls / Attachment mode key | `Controls/AttachmentPose` | O | Opens/closes the standalone authored pose. |
| Controls / Toggle attachment mode | `Controls/ToggleAttachmentPose` | On | Toggle instead of hold. |
| Controls / Cursor modifier | `Controls/MouseModifier` | Left Alt | Captures mouse for cards. |
| Controls / Cycle mounting position | `Controls/CycleAttachmentPosition` | Left Alt + R | Cycles the active or most recently focused category's native mounting point; never moves an item. |
| Controls / Cursor speed | `Controls/MouseSensitivity` | 20 | Cursor movement rate. |
| Controls / Attachment animation speed | `Controls/AttachmentAnimationSpeedMultiplier` | 1.12 | Native install/remove playback multiplier, bounded 1.0–1.15. |
| Display / Show attachment thumbnails | `Display/RequestNativeIcons` | On | Requests game item icons, with name fallback. |
| Display / Follow weapon movement | `Display/WeaponFollowingCards` | On | Weapon-relative cards and leader movement. |
| Diagnostics / Save diagnostic report | `Controls/ExportReport` | F10 | Snapshot and operation diagnostics; no item move. |
| Display / Use authored weapon poses *(advanced)* | `Experiments/CustomStmAnimation` | On | Enables standalone authored presentation. Off disables attachment poses; it does not select native inspect. |

Older stored keys still load and are hidden in F12. The native MCX/MDR/STM pose experiments and F4 calibration are inactive in attachment mode; changing their saved flags does not restore those animation routes. Shift-modified opens use the same standalone animation, and saved PoseMarkers are unused. Legacy F6/F7/F8/F9 keyboard actions, older selector modes, card calibration and discovery hints remain separate controls. Historical tester presets enabled the cross presentation and authored poses and left live actions off.

The name `EnableEmptySlotInstall` predates uninstall and replacement. It now gates all supported live clicks. There is no migration of this key. Report files and journals are written under the installed plugin folder; they are not part of the config.

In the legacy O + Left Alt workflow, right-click closes only the expanded list. O or Escape exits the presentation. See the [user guide](USER_GUIDE.md) for the current controls.
