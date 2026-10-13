# Build from source

The repository contains the **1.2.0 source**, its build scripts and animation bundles. Use the [v1.2.0 tag](https://github.com/Tylevo/Tylevo.FieldAttachments/tree/v1.2.0) for the source that matches the release.

Requirements:

- Windows with PowerShell and **.NET SDK 8 or newer**.
- Your own **SPT 4.1.5 or 4.1.6 game installation (EFT 40743)** with BepInEx 5.
- A compatible **[UnityToolkit 2.0.2 or newer](https://github.com/ArysWasTaken/UnityToolkit)** installed in that game folder.

Download or clone the complete repository, including `assets/Animation/`. Unity Editor and Blender are not needed to compile the plugin.

The supported weapon packs are optional. You do not need their server mods, DLLs or assets to build or run Field Attachments with standard SPT weapons.

Stance Overhaul and Realism Common Library are also optional. Their assemblies and animation bundles are not needed to compile this project and are not included here. The compatibility adapter uses them only when a supported installation is present.

From the repository folder, replace `<SPT_GAME_FOLDER>` with the folder containing `EscapeFromTarkov.exe` and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build.ps1 -SptPath '<SPT_GAME_FOLDER>'
```

The script checks the client references, compiles the plugin and verifies the included animation bundles. It writes the install files to `dist/BepInEx/plugins/Tylevo.FieldAttachments/` and the build log to `dist/build.log`.

The build does not install the mod. Close the game before copying `dist/BepInEx` into your SPT folder. Keep the `Animation` folder beside the DLL.

Game, Unity and UnityToolkit libraries are read from your installation and are not included in this repository. The build checks for the supported client rather than bypassing compatibility checks.

## Matching a local build

Use the same source, SDK and local reference libraries for a byte-for-byte comparison. The build helper omits Git revision and Source Link metadata so a clean checkout and source ZIP can produce the same DLL. The generated `build-manifest.json` records the DLL hash, SDK and reference hashes.
