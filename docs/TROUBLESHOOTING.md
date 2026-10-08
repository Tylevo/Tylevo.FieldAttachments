# Troubleshooting

| Symptom | Check |
| --- | --- |
| Mod does not load | Confirm SPT 4.1.5 or 4.1.6 with the inspected EFT 40743 client, BepInEx 5, UnityToolkit 2.0.2+ installed separately, and one Field Attachments DLL. Look for the plugin load error in `BepInEx/LogOutput.log`. |
| Alt does not open the interface | Check F12 → Controls → Attachment menu key. LeftAlt is the default; RightAlt and None are the other choices. Hold is the default activation mode. A supported gun and local raid or active hideout range are required. An existing O-opened session keeps Alt as its separate cursor modifier. |
| Toggle closes after another Alt tap | This is expected: a later plain Alt tap closes on release. Alt+R or clicking while holding Alt keeps Toggle open. Use Hold mode if the menu should close whenever Alt is released. |
| O plays ordinary inspection | Check the `Controls/AttachmentPose` binding and Display / Use authored weapon poses. Hold a supported gun in a local raid or the active hideout range. Field Attachments does not use native inspection as a fallback; unsupported or disabled authored poses are refused. If the overlay is available, F10 records the pose refusal reason. |
| LIST has fewer items than inventory | Only examined, raid-moddable parts accepted by that existing native slot appear. A missing mount, fixed part, child of a carried assembly, inaccessible storage, or an unknown fit removes a candidate. Alt+R may select a different mounting side. |
| Click does not move an item | Check `General / Allow attachment changes` and compatible/free carried storage. A visible candidate still goes through native simulation, hands/session guards and result observation. Read F10 `install-probe.txt` and the latest journal. |
| Replace leaves an empty gun slot | Replacement removes the outgoing root first. A failed second move leaves it in carried storage. Stop and inspect storage/report before another attempt. No automatic rollback is attempted. |
| Cards disappear during a change | They intentionally hide while the gun lowers and return at the held pose. If they do not return after a settled success, capture F10 and a short clip. |
| Weapon, hands or pose becomes stuck | Close attachment mode if possible; do not issue repeated inventory clicks. Save F10 and the journal. If the result is latched UNKNOWN / RESTART REQUIRED, restart the game before another attempt. |
| Right-click closes only the list | This is expected. Escape closes the interface. In Hold mode, releasing the menu key also closes it; in Toggle mode, a later plain Alt tap closes on release. |
| Wheel is missing from the tooltip | Wheel paging still works in LIST. It is intentionally omitted from the bottom control hint. |
| F12 shows older trial values | Old keys remain in the config for compatibility but are hidden by the current menu. The old native pose trials are inactive in standalone attachment mode. See [configuration](CONFIGURATION.md). |

When reporting a failure, include SPT and UnityToolkit versions, relevant content mods, weapon name/template ID, attachment and mounting point, source/destination storage, range or raid, steps, expected/actual result, and a screenshot or video. Review reports and journals before sharing them; remove local paths, profile identifiers, private item IDs and unrelated log content. This documentation repository does not contain private reports or a tester package.
