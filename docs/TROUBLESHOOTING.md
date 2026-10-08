# Troubleshooting

| Symptom | Check |
| --- | --- |
| Mod does not load | Confirm SPT 4.1.5 or 4.1.6 with the inspected EFT 40743 client, BepInEx 5, UnityToolkit 2.0.2+ installed separately, and one Field Attachments DLL. Look for the plugin load error in `BepInEx/LogOutput.log`. |
| Middle mouse does not open the menu | Check F12 → Controls → Attachment menu key and Menu activation (Hold / Toggle). Hold is the fresh default; None disables the shortcut. Hold a supported weapon in a local raid or the active hideout range, and enable Display / Use authored weapon poses. Native menus or an unsupported pose can prevent opening. |
| O plays ordinary inspection | Check the legacy `Controls/AttachmentPose` binding and Display / Use authored weapon poses. Field Attachments does not use native inspection as a fallback; an unsupported or disabled authored pose is refused. If the overlay is available, F10 records the pose refusal reason. |
| The list has fewer items than inventory | Only examined, raid-moddable parts accepted by that existing native slot appear. A missing mount, fixed part, child of a carried assembly, inaccessible storage, or an unknown fit removes a candidate. Scroll for additional rows; the arrows or Alt+R may select a different mounting point. |
| Click does not move an item | Check `General / Allow attachment changes` and compatible/free carried storage. A visible candidate still goes through native simulation, hands/session guards and result observation. Read F10 `install-probe.txt` and the latest journal. |
| Replace leaves an empty gun slot | Replacement removes the outgoing root first. A failed second move leaves it in carried storage. Stop and inspect storage/report before another attempt. No automatic rollback is attempted. |
| Cards disappear during a change | They intentionally hide while the gun lowers and return at the held pose. If they do not return after a settled success, capture F10 and a short clip. |
| Weapon, hands or pose becomes stuck | Close attachment mode if possible; do not issue repeated inventory clicks. Save F10 and the journal. If the result is latched UNKNOWN / RESTART REQUIRED, restart the game before another attempt. |
| Right-click closes only the list | This is the legacy O + Left Alt workflow. Use O or Escape to exit. Right-click closes the whole menu when it was opened with the middle-mouse shortcut. |
| F12 shows older trial values | Old keys remain in the config for compatibility but are hidden by the current menu. The old native pose trials are inactive in standalone attachment mode. See [configuration](CONFIGURATION.md). |

When reporting a failure, include SPT and UnityToolkit versions, relevant content mods, weapon name/template ID, attachment and mounting point, source/destination storage, range or raid, steps, expected/actual result, and a screenshot or video. Review reports and journals before sharing them; remove local paths, profile identifiers, private item IDs and unrelated log content. This documentation repository does not contain private reports or a tester package.
