# User guide

Hold **Left Alt** to open the attachment menu and use the mouse. Release it to close. If you prefer to leave the menu open, choose **Toggle** in **F12 → Controls → Menu activation**. Tap Alt to open it and tap Alt again to close.

Press **middle mouse** to switch between **Basic** and **Multitool** layers. The layer button beside the bottom controls does the same thing. The top-left header names the current layer and weapon; the bottom hints show your saved controls.

Change **Attachment menu key** or **Switch attachment layer** in F12 to use different shortcuts. Keyboard keys, mouse buttons and modifier combinations are supported. Use different buttons for opening the menu and switching layers. To bind a combination, hold its modifiers, press and release the main key or mouse button, then release the modifiers. Updates keep your saved bindings.

In Toggle mode, Alt+R and clicks made while Alt is held keep the menu open; tap Alt on its own to close it. Ordinary keys and mouse buttons toggle on each press.

## Changing an attachment

1. Click the installed attachment or empty thumbnail on a weapon card to see compatible carried parts for that mounting point. Use the wheel or page buttons to browse more parts.
2. Click a part to install it or replace the current attachment.
3. Choose **NONE** in the list, or the full-width **REMOVE** bar beneath the card, to remove an optional installed part. Required parts can only be replaced.
4. Use **Left Alt + R** to cycle mounting points for the open list or the card under your cursor. The slot count and connection line show the selected point.

**Right-click** closes the list. **Escape** closes the menu. Gameplay actions such as sprinting can also cancel it. While using the menu, left-click selects and right-click closes the list; they do not fire or aim.

Hovering, scrolling and changing mounting points never move items. Click a part to fit it.

## Comparing parts

Hover a choice to compare it with your current setup. The box at the **top right** shows ergonomics, vertical recoil, horizontal recoil and weight. Two smaller bars beside the list show ergonomics and vertical recoil.

Green marks an improvement and red marks a loss. Higher ergonomics is better; lower recoil and weight are better. Hover **NONE** to preview removal. If a value cannot be calculated reliably, the preview marks it unavailable.

## Which parts appear?

The **Basic** layer has cards for optics, muzzle devices, tactical devices and foregrips. It lists compatible, examined parts in accessible pockets, rig, backpack or secure container that can normally be changed during a raid. A sight on its own needs its mount to be fitted to the weapon first.

An assembled sight and mount can appear as one item and move together. Supported stock, handguard and pistol-grip assemblies also stay intact. Required attachments must be present, and the parts must fit their destination. The menu does not build an assembly or bypass locked mounting points.

If a carried part is missing, click **Unavailable** at the bottom of the list. It explains why a part cannot be used at the selected mounting point, such as a poor fit, an unexamined item or a missing required part. Unexamined items stay unnamed. These entries cannot be fitted; choose **Back to parts** to return to your choices.

## Multitool layer

Press **middle mouse** or click the layer button to switch layers. The cards briefly fade as the layer changes. **Handguard** is on the left, **Stock** on the right and **Pistol Grip** at the bottom.

Carry a **Leatherman Multitool** in accessible storage or a pocket special slot to change these parts. You do not need to equip it, and it is not consumed. The menu tells you if the tool is missing or a change is unavailable.

Required parts can only be replaced. Bring a compatible replacement with its required attachments already fitted, and leave room for the outgoing part. Rails, sights and foregrips on the old handguard stay with it; they do not transfer to the replacement. Repair an already-empty required slot outside the raid.

## Attachment changes

**Allow attachment changes** is enabled on fresh installs. Turn it off in **F12** to browse without moving items. Updates preserve your saved setting, so check it if you previously disabled changes.

Use a supported weapon in a local raid or the hideout shooting range. Attachment changes are not supported in Fika.

Your character lowers the weapon to make a change. After a successful change, it returns to the attachment view if the menu is still open. Advanced parts use the same fitting and removal animations, so you may briefly see the empty mounting point between the two moves.

Keep enough free space in your carried storage for a removed part. A replacement puts the old part into storage before fitting the new one. If fitting fails, the old part stays in storage and the weapon's slot may be empty. Check both before trying again. The menu does not use your stash or rearrange gear to make room.

Closing the menu cancels a change that has not started. It cannot undo an item move already underway.

## Movement and optional stance support

**F12 → Display → Idle movement** adds gentle breathing movement while you view attachments. It defaults to **0.5**; set it to **0** to turn it off. Movement settles while you choose a part and pauses during a click.

**Faster menu close (trial)** offers a shorter exit and is off by default. **Attachment animation speed** adjusts fitting and removal playback; it leaves normal inspection, firing and reloading speeds alone.

**[Fontaine's Stance Overhaul](https://sp-mod.com/mod/3136/spt-stance-overhaul) is fully optional, but recommended for smoother weapon transitions. Every Field Attachments feature works without it.** Animation support covers Stance Overhaul **0.0.2** and [Realism Common Library **0.0.2**](https://sp-mod.com/mod/3135/spt-realism-common-lib). Install their listed requirements if you choose to use them.

**F12 → Compatibility → Fluid stance transitions** is on by default. It follows your stance's opening and closing pace, waits for the weapon to settle before you can select a part, and keeps the stance inactive while fitting parts. Ordinary standing keeps a quicker transition. Without the supported versions, or with this setting off, the usual attachment animation plays. Your other mod settings and FOV stay the same.

The main-menu notice shows whether Stance Overhaul is detected and ready. If it is missing, **View on Forge** opens its page. You can dismiss the notice for that launch or turn off **Main menu stance notice** in F12.

See [configuration](CONFIGURATION.md) for useful settings or [troubleshooting](TROUBLESHOOTING.md) if something does not work. Installation instructions are in the [README](../README.md).
