# Build from source

This repository contains the C# source for the **1.0.0 release**, the build scripts and the animation bundles used by the plugin.

Requirements:

- Windows with PowerShell and **.NET SDK 8 or newer**.
- Your own **SPT 4.1.5 or 4.1.6 game installation (EFT 40743)** with BepInEx 5.
- A compatible **[UnityToolkit 2.0.2 or newer](https://github.com/ArysWasTaken/UnityToolkit)** installed in that game folder.

Download or clone the complete repository, including `assets/Animation/`. Unity Editor and Blender are not needed to compile the plugin.

From the repository folder, replace `<SPT_GAME_FOLDER>` with the folder containing `EscapeFromTarkov.exe` and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build.ps1 -SptPath '<SPT_GAME_FOLDER>'
```

The script checks the client references, compiles the plugin and verifies the included animation bundles. It writes the install files to `dist/BepInEx/plugins/Tylevo.FieldAttachments/` and the build log to `dist/build.log`.

The build does not install the mod. Close the game before copying `dist/BepInEx` into your SPT folder. Keep the `Animation` folder beside the DLL.

Game, Unity and UnityToolkit libraries are read from your installation and are not included in this repository. The build checks for the supported client rather than bypassing compatibility checks.

## Matching the published DLL

The published 1.0.0 DLL was built with **.NET SDK 10.0.201** against the supported SPT 4.1.6 installation. Its SHA-256 is:

```text
A6C5500C8D65B1046251AA6EF03B608CE98029C961B6B58A3B3CE07AF81C939C
```

For a byte-for-byte comparison, use the source ZIP without a `.git` folder, the same SDK and matching local reference libraries. Building inside a Git checkout can add revision metadata to the assembly. The release ZIP's `build-manifest.json` records the reference hashes.
