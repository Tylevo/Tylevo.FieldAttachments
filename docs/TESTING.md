# Testing and evidence

The following results were recorded against the local 0.27.0 build targeting the inspected SPT 4.1.6 client. Publishing this documentation did not rerun the build or perform gameplay tests.

| Check | Result | Scope |
| --- | --- | --- |
| Core and metadata tests | 11,106 assertions passed | Input intent, release handling, layout, selection identity and transaction policy in fixtures |
| Plugin compilation | Zero warnings or errors | Compilation against local client and dependency references |
| Structural validation | 324 checks passed | Expected source guards, version values and asset structure |
| Isolated Unity UI fixture | 1,509 checks passed | Compiled projected uGUI meshes, inverse hits, exact selected item/slot identities, paging, state feedback and cleanup |
| UI screenshots | 21 at 1920×1080, 2560×1440 and 3440×1440 | Layout at three resolutions |
| Explicit UI requests | One each for install, replacement and removal | Events emitted by matched clicks; no native executor attached |
| Browsing requests | Zero from hover or wheel | Browsing does not request an inventory action |
| Baseline preservation | 44 scoped source files and 136 animation files unchanged | Includes all 129 authored animation bundles |

The checked plugin SHA-256 is `422EEFFA41F1CB62F57FA8A1523339DE840084D1E513E130BE5DAB6FA3693088`.

The Unity 2022.3.43f1 UI fixture used synthetic observed items and distinctly colored cached sprites. It checked the production overlay but had no Player, Plugin request subscriber or native inventory adapter attached. It cannot establish live native thumbnail behavior, physical middle-mouse input, gameplay timing or inventory success.

The unchanged animation baseline has earlier offline receipts for 129 bundles and 26 alias loads, 36 renderer scenarios and 32 focused RShG-2 scenarios. Those are separate animation checks, not live acceptance of the new interface.

## Live checks still needed

Test the HK416 first, then a pistol and another long gun. Check Hold and Toggle, rapid opening/closing, category hover, wheel scrolling, mounting arrows and Alt+R. Verify the equipped row updates after native completion.

On a test profile with attachment changes enabled, check empty-slot installation, removal, replacement and insufficient storage. Observe the actual result if the second move of a replacement fails. Interrupt with aiming, firing, reload, sprint, inventory and weapon switching. Captured clicks must not become shots after closing, and middle mouse must not also activate freelook.

See the [current checklist](PROGRESS_0.27.0.md) and [troubleshooting guide](TROUBLESHOOTING.md). The original screenshots in the README show earlier rifle gameplay and do not prove acceptance of the 0.27.0 UI.
