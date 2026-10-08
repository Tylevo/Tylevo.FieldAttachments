# Configuration

For 0.25.3, the BepInEx file remains `BepInEx/config/com.tylevo.fieldattachments.cfg`. F12 shows friendly labels; existing stored keys retain their values. The Alt shortcut adds `Controls/MenuKey` and `Controls/MenuActivation`. F12 Reset restores the **current compiled default**, not the previously saved value.

| F12 group / label | Stored section/key | Fresh default | Effect |
| --- | --- | --- | --- |
| General / Enabled | `General/Enabled` | On | Master attachment UI switch. |
| General / Allow attachment changes | `Experiments/EnableEmptySlotInstall` | Off | Enables guarded native item moves. |
| Controls / Attachment menu key | `Controls/MenuKey` | LeftAlt | Opens the pose and cursor together. Options: LeftAlt, RightAlt or None. |
| Controls / Menu activation (Hold / Toggle) | `Controls/MenuActivation` | Hold | Hold closes on release. Toggle opens on key-down; a later plain Alt tap closes on release. |
| Controls / Legacy attachment mode key *(advanced)* | `Controls/AttachmentPose` | O | Opens/closes the pose separately from cursor capture. |
| Controls / Legacy pose toggle *(advanced)* | `Controls/ToggleAttachmentPose` | On | Applies only to the legacy pose key; Off requires holding that key. |
| Controls / Legacy cursor modifier *(advanced)* | `Controls/MouseModifier` | Left Alt | Captures the cursor in an O-opened session. |
| Controls / Cycle mounting position | `Controls/CycleAttachmentPosition` | Left Alt + R | Cycles the open or hovered slot. |
| Controls / Cursor speed | `Controls/MouseSensitivity` | 20 | Cursor movement rate. |
| Controls / Attachment animation speed | `Controls/AttachmentAnimationSpeedMultiplier` | 1.12 | Native install/remove playback multiplier, bounded 1.0–1.15. |
| Display / Show attachment thumbnails | `Display/RequestNativeIcons` | On | Requests game item icons, with name fallback. |
| Display / Follow weapon movement | `Display/WeaponFollowingCards` | On | Weapon-relative cards and leader movement. |
| Diagnostics / Save diagnostic report | `Controls/ExportReport` | F10 | Snapshot and operation diagnostics; no item move. |
| Display / Use authored weapon poses *(advanced)* | `Experiments/CustomStmAnimation` | On | Enables standalone authored presentation. Off disables attachment poses; it does not select native inspect. |

Older stored keys still load and are hidden in F12. The native MCX/MDR/STM pose experiments and F4 calibration are inactive in attachment mode; changing their saved flags does not restore those animation routes. Shift-modified opens use the same standalone animation, and saved PoseMarkers are unused. Legacy F6/F7/F8/F9 keyboard actions, older selector modes, card calibration and discovery hints remain separate controls. Historical tester presets enabled the cross presentation and authored poses and left live actions off.

The name `EnableEmptySlotInstall` predates uninstall and replacement. It now gates all supported live clicks. There is no migration of this key. Report files and journals are written under the installed plugin folder; they are not part of the config.

In Toggle mode, Alt+R or clicking while Alt is held keeps the menu open when Alt is released. The default mounting shortcut remains Left Alt + R even if the menu key is changed to RightAlt. Setting MenuKey to None disables the combined shortcut and leaves the advanced O workflow available.

Right-click closes LIST only; Escape closes the interface. The bottom control hint omits wheel paging, which remains available in LIST. See the [user guide](USER_GUIDE.md).
