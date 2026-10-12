# Troubleshooting

This page covers the **unreleased 1.1.1 source**. The latest published download is **1.1.0**, so it will not show every feature described here.

| Problem | What to check |
| --- | --- |
| The mod does not load | Check the requirements and installation steps in the [README](../README.md), including UnityToolkit. Keep only one installed copy of Field Attachments. |
| Alt does not open the menu | Check that the mod is enabled and the menu key is set in F12. Hold a supported weapon in a local raid or the hideout shooting range. |
| The menu closes when I release Alt | Hold is the default. Choose Toggle in F12 if you want it to stay open. Gameplay actions such as sprinting can still cancel the menu. |
| A part is missing from LIST | It must be examined, compatible with the selected mounting point and in accessible carried storage. Basic parts must be changeable in a raid. Multitool parts need a carried Leatherman and complete, supported assemblies. Check for a missing mount or required child part, or use Alt+R to select another position. |
| Stocks, handguards or pistol grips are missing | Click the bottom LAYER button to enter Multitool. Carry a Leatherman Multitool and read the status above the controls. This layer has three cards; there is no Receiver card. |
| A required part cannot be removed | This is expected. Choose a compatible carried replacement from LIST. Both assemblies must be complete and verifiable, and the game's validation must accept the move. Repair an already empty required slot outside the raid. |
| An assembled attachment will not move | Only supported intact assemblies can move together. Check for missing required children, unexamined parts, locked or pinned components, and compatibility with the destination. The menu does not transfer children onto another part. |
| Clicking does not change a part | Check Allow attachment changes in F12; a saved disabled setting stays disabled after an update. Leave room in carried storage for the outgoing part and wait until the current weapon action finishes. Attachment changes are not supported in Fika. |
| A preview is green but the change is rejected | Green describes the predicted stat change, not permission to move the item. Storage, compatibility, required-part and current weapon-state checks still apply when you click. |
| Statistics show unknown values | The game's stat preview was unavailable or incomplete for that item. Unknown values are not treated as zero. Hover another choice or reopen the list after the weapon finishes its current action. |
| A failed replacement leaves an empty slot | Look for the old part in your carried storage. Check the weapon and storage before trying again. |
| Cards disappear during a change | They hide while your character changes the attachment. After a successful change, they return if the menu is still open. |
| The layer button does not respond | Wait for the current attachment operation or brief layer fade to finish. A button press made before the state changed must be released; make a fresh click afterward. |
| The status says UNKNOWN - RESTART REQUIRED | Stop attempting changes. The previous operation's result could not be confirmed; restart the game before trying again. Closing and reopening the menu does not clear this state. |
| The weapon or hands seem stuck | Close the menu and stop clicking. If it does not recover, restart the game before attempting another change. |
| Right-click closes only the list | This is expected. Release Alt in Hold mode, press Alt again in Toggle mode, or use Escape to close the menu. |
| Stance Overhaul transitions are not used | Check Fluid stance transitions. Integration requires the checked Stance Overhaul and Realism Common Library 0.0.2 builds; other builds fall back to normal attachment presentation. Neither optional mod is required to use Field Attachments. |
| Closing feels too quick | Turn off Faster menu close (trial). It is off by default, and the change applies to the next normal close. |

If you need help, include your SPT version, relevant mods, weapon and attachment names, whether you were in a raid or the range, and what happened. A short video or screenshot helps.

See the [user guide](USER_GUIDE.md) for controls and [configuration](CONFIGURATION.md) for F12 settings.
