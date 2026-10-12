# Configuration

This page describes the **unreleased 1.1.1 source**. The latest published download is **1.1.0**; newer settings may be absent there.

Press **F12** and open the Field Attachments settings.

| Setting | What it does |
| --- | --- |
| Enabled | Turns the attachment menu on or off. |
| Allow attachment changes | On for fresh installs. Allows installing, removing and replacing parts. Turn it off to browse without moving items. |
| Attachment menu key | Left Alt by default. Choose Right Alt instead, or None to disable the shortcut. |
| Menu activation (Hold / Toggle) | Hold is the default: release Alt to close. Toggle keeps the menu open until you press Alt again. |
| Cycle mounting position | Alt+R selects another mounting point for the open list or hovered card. |
| Cursor speed | Adjusts how quickly the menu cursor moves. |
| Attachment animation speed | Defaults to 1.12 times normal speed, with a range of 1.0 to 1.15. Adjusts installation and removal only; ordinary firing, reloading and inspection keep their usual speed. |
| Faster menu close (trial) | Off by default. Shortens the normal exit from a fully open menu to about 0.36 seconds instead of about 0.77 seconds. It does not change opening, early release or immediate gameplay cancellation. Applies to the next close without a restart. |
| Show attachment thumbnails | Shows item pictures in the menu. |
| Follow weapon movement | Makes the cards follow the weapon. |
| Fluid stance transitions | On by default. Uses the supported Stance Overhaul build's transition timing when that optional integration is available. Has no effect without it and does not change stance settings or FOV. |
| Main menu stance notice | On by default. Shows whether optional stance support is ready. Dismiss hides it for this launch; turn this setting off to keep it hidden. The Forge page opens only when you click its button. |

In Toggle mode, using Alt+R or clicking while holding Alt keeps the menu open. If you change the menu key to Right Alt, the mounting shortcut remains Left Alt+R unless you change it too.

## Switching layers

Click the bottom **LAYER** button to switch between Basic and Multitool while the menu is open. This changes the displayed categories without moving an item.

The optional `CycleAttachmentLayer` shortcut is unbound by default and is not shown in the normal F12 settings list. To bind it, close the game and edit that entry under `[Controls]` in `BepInEx/config/com.tylevo.fieldattachments.cfg`. The footer adds a **Switch layer** keycap when a shortcut is assigned.

## Optional stance integration

Field Attachments does not require Stance Overhaul or Realism Common Library. Stance Overhaul is recommended for smoother animations; all Field Attachments features work without it. Integration currently recognizes the checked **0.0.2 builds of both mods**. Other versions or builds use the normal attachment presentation when the integration cannot be enabled. Turning off **Fluid stance transitions** also returns to the normal presentation.

The smoother menu entry and the HUD's keycap styling do not require either optional mod.

Updates keep your saved settings. See the [user guide](USER_GUIDE.md) for controls and [troubleshooting](TROUBLESHOOTING.md) for help.
