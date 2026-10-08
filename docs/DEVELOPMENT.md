# Building the complete source package

This GitHub repository is the project's documentation page. The commands below apply to a complete source package containing `src/`, `tests/`, `tools/` and the 129 pinned authored animation bundles.

Use Windows with .NET SDK 8 or newer and your own compatible SPT 4.1.5 or 4.1.6 client containing BepInEx 5 and UnityToolkit 2.0.2 or newer. The plugin targets .NET Standard 2.1 and reads game, Unity and Toolkit references from that local client. The folder name alone does not establish the client's version.

From the complete source-package root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build.ps1 -SptPath '<SPT_GAME_ROOT>'
python tools/Validate-Source.py
```

The build runs core tests, compiles the plugin, checks animation hashes and prepares `dist/BepInEx/plugins/Tylevo.FieldAttachments/`. Installation is separate unless the build helper receives `-Install`. Unity Editor is not required to compile the existing C# source and bundled animations.

The source groups pure policies and layout calculations under `src/Core/`, client integration under `src/Runtime/`, and configuration/action orchestration in `src/Plugin*.cs`. Explicit UI actions pass through the existing request queue and fresh native validation before a native move. See [architecture](ARCHITECTURE.md).

The isolated UI and animation fixtures use Unity 2022.3.43f1. They do not replace live SPT tests; see [recorded evidence](TESTING.md). Authored animation work also requires matching local references and a separate asset review.
