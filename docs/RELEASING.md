# Plugin release process

This repository currently publishes the project page and documentation. Source and installable archives are separate deliverables; no public binary release is attached yet.

For a plugin release, use the complete source package and a matching local SPT client. Run the build, core tests and structural validation. Verify the pinned animation hashes and record the exact DLL hash and client version. Keep offline results distinct from live acceptance.

An installable archive contains `BepInEx/plugins/Tylevo.FieldAttachments/` with the plugin DLL and its Animation folder. It must not contain game/Toolkit DLLs, profiles, personal settings, reports or journals. Preserve existing configuration during updates. Close SPT, back up the previous plugin and verify copied files before testing.

Publishing source or animation bundles requires reviewing the exact release contents and [asset scope](ASSET_REVIEW.md). Version the README, controls and test results with the actual build; screenshots of older interfaces need explicit labels.
