# Testing scope

Version 0.25.3 changes Alt activation and the bottom control hint while retaining the 0.25.2 cards, animation and inventory behavior. Its build, isolated UI checks and installation verification are recorded in the [current input notes](PROGRESS_0.25.3.md). Historical results below describe the 0.25.2 baseline; neither set establishes physical Alt-input or native inventory behavior in game.

| Baseline check | Recorded 0.25.2 result |
| --- | --- |
| Core and metadata tests | 7,875 assertions passed |
| Plugin compilation | Zero warnings or errors against the inspected SPT 4.1.6 client |
| Structural checks | 308 passed |
| Standalone playback | 129 bundles and 26 alias loads passed |
| Renderer helper | 36 scenarios passed |
| Focused RShG-2 fixture | 32 scenarios passed |

These isolated checks cover sampling, skinning, ownership, restoration and guard behavior within their fixtures. They do not establish live clothing fit, gameplay timing, physical input or native inventory success for every setup. The original gameplay screenshots show earlier rifle behavior and the retained REMOVE/LIST interface; they do not test the new Alt activation.

For 0.25.3, check Hold and Toggle, later plain Alt taps, Alt+R and clicking while Alt is held, LIST paging, right-click closing LIST, Escape, focus loss and the O fallback. Confirm closing does not turn an owned click into a shot. Test explicit installation, removal and replacement separately from browsing.

See the [current input notes](PROGRESS_0.25.3.md) and [troubleshooting guide](TROUBLESHOOTING.md). Publishing these pages does not imply another compile or an in-game test.
