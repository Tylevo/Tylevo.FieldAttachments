# Troubleshooting

| Problem | What to check |
| --- | --- |
| The mod does not load | Check the requirements and installation steps in the [README](../README.md), including UnityToolkit. Keep only one installed copy of Field Attachments. |
| Alt does not open the menu | Check that the mod is enabled and Attachment menu key is set in F12. Updates keep your saved binding. Hold a supported weapon in a local raid or the hideout shooting range. |
| The menu closes when I release Alt | Hold is the default. Choose Toggle in F12 if you want it to stay open. Gameplay actions such as sprinting can still cancel the menu. |
| How do I open the parts list? | Click the card's installed or empty thumbnail. The full-width REMOVE bar beneath it removes the current optional part. |
| A part is missing from the list | Click Unavailable at the bottom to see why carried parts cannot be used. Check for an unexamined item, a missing mount or required part, or use Left Alt + R to select another mounting point. Items in the stash are not available. |
| Scrolling does not change an attachment | The wheel only browses the open list. Click a part to fit it. Page buttons also let you browse. |
| Stocks, handguards or pistol grips are missing | Press middle mouse or click the layer button to enter Multitool. Carry a Leatherman Multitool in accessible storage or a pocket special slot. Read the menu's message if a change is unavailable. |
| Middle mouse closes the menu instead of changing layers | Check Attachment menu key and Switch attachment layer in F12. Give them different buttons. The defaults are Left Alt for the menu and middle mouse for layers, but updates preserve older bindings. |
| A required part cannot be removed | Choose a compatible replacement from the parts list. Bring it with its required attachments already fitted. A required part cannot be removed on its own, and an already-empty required slot must be repaired outside the raid. |
| An assembled attachment will not move | Check for missing required attachments, unexamined items, locked components or parts that do not fit the destination. The menu can move supported, complete assemblies, but it does not build them for you. |
| Attachments stayed on the old handguard | Attached rails, sights and foregrips stay with the outgoing handguard. They do not transfer to the replacement. Prepare the replacement before entering the raid. |
| Clicking does not change a part | Check Allow attachment changes in F12; a saved disabled setting stays disabled after an update. Leave room in carried storage for the outgoing part and wait until the current weapon action finishes. Attachment changes are not supported in Fika. |
| A comparison is green but the change is rejected | Green means the predicted stat change is an improvement. The part must still fit, your gear must have room for the outgoing part, and the weapon must be ready for the change. |
| A comparison value is unavailable | The game could not provide a complete stat preview for that part. Hover another choice or reopen the list after the weapon finishes its current action. An unavailable value does not mean zero. |
| A failed replacement leaves an empty slot | Look for the old part in your carried storage. Check the weapon and storage before trying again. |
| Cards disappear during a change | They hide while your character changes the attachment. After a successful change, they return if the menu is still open. |
| The layer button does not respond | Wait for the current attachment change or brief layer fade to finish, release the button, then try again. |
| The status says UNKNOWN - RESTART REQUIRED | Stop attempting changes. The result of the previous move could not be confirmed. Restart the game before trying again; reopening the menu does not clear this message. |
| The weapon or hands seem stuck | Close the menu and stop clicking. If it does not recover, restart the game before attempting another change. |
| Right-click closes only the list | This is expected. Release Alt in Hold mode, press Alt again in Toggle mode, or use Escape to close the menu. |
| The weapon moves too much while the menu is open | Lower Display → Idle movement in F12, or set it to 0 to turn it off. The default is 0.5. |
| Stance Overhaul transitions are not used | Check Compatibility → Fluid stance transitions in F12 and read the main-menu notice. Animation support covers Stance Overhaul 0.0.2 with Realism Common Library 0.0.2. Missing or unsupported versions use the usual attachment animation. All features work without either mod. |
| I do not want the Stance Overhaul notice | Dismiss it for the current launch, or turn off Compatibility → Main menu stance notice in F12. |
| Closing feels too quick | Turn off Faster menu close (trial). It is off by default. When optional stance support is active, the stance transition sets the opening and closing pace. |

If you need help, include your SPT version, relevant mods, weapon and attachment names, whether you were in a raid or the range, and what happened. A short video or screenshot helps.

See the [user guide](USER_GUIDE.md) for controls and [configuration](CONFIGURATION.md) for F12 settings.
