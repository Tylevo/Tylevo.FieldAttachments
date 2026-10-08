# Weapon coverage — 0.25.2

The audited definition inventory contains 199 weapon-class entries: 164 from SPT and 35 from installed content mods. Version 0.25.2 registers 194 exact templates using 129 authored animation bundles. The original 162 recipient routes and 123 bundle files are preserved; 20 explicit prefab aliases, 6 exact native-rig aliases and 6 individually fitted poses extend coverage.

This count describes the audited installed JSON definitions, not a universal claim about every mod or live attachment combination. Five entries remain excluded: two stationary weapons, two underbarrel metadata entries, and one internal test item whose controller lacks required states. SP-81, RShG-2 and all seven reactive flare variants are included. Their zero attachment slots mean presentation opens without attachment actions.

This public page summarizes the coverage additions and exclusions. The complete source/audit package retains the full 199-entry inventory, asset pins and native compatibility metadata; those files are not published in this documentation repository. See the [animation and coverage baseline notes](PROGRESS_0.25.2.md) and the [testing summary](TESTING.md) for validation limits.

## Added ordinary weapons

| Weapon | Template | Route |
|---|---|---|
| CMMG Mk47 Mutant 5.45X39 assault rifle | `52ce1b65b13e1035808c4fd2` | Exact prefab alias of `606587252535c57a13424cfd` |
| CMMG Mk47 Mutant 9X39 assault rifle | `96f5c38a676e11e13544dfba` | Exact prefab alias of `606587252535c57a13424cfd` |
| Century Arms Draco 7.62x39 carbine | `93bcdfda236122e67c098847` | Exact prefab alias of `57dc2fa62459775949412633` |
| HK 337 .300 Blackout assault rifle | `6963f79ab66d6c6601a8b2d7` | Independent native-grip fit |
| Kalashnikov AK-101 .300 Blackout assault rifle | `d672109946fe88b803449054` | Exact prefab alias of `5ac66cb05acfc40198510a10` |
| Kalashnikov AK-102 .300 Blackout assault rifle | `ffc95b9d143f52202a311820` | Exact prefab alias of `5ac66d015acfc400180ae6e4` |
| Kalashnikov AK-15 7.62x39 assault rifle | `3dc691f607ffed3228bf6ca2` | Exact prefab alias of `6499849fc93611967b034949` |
| Kalashnikov AKS-74U .300 Blackout Assault Rifle | `ed05294ed53c0400ae0e8a55` | Exact prefab alias of `57dc2fa62459775949412633` |
| Kalashnikov AKS-74UB .300 Blackout Assault Rifle | `939c742f7dad852286188029` | Exact prefab alias of `5839a40f24597726f856b511` |
| Kalashnikov AKS-74UN .300 Blackout Assault Rifle | `627c4fe34b0a558e8a3642a1` | Exact prefab alias of `583990e32459771419544dd2` |
| Kalashnikov AKS-74UN 5.56x45 assault rifle | `68580e9cea46c81b4db2221e` | Exact prefab alias of `583990e32459771419544dd2` |
| Mechanic's Colt M4A1 .300 blackout assault rifle | `4b81488c78c8a8ac7d37f9b9` | Exact prefab alias of `5447a9cd4bdc2dbd208b4567` |
| Modified Century Arms Draco 7.62x39 Assault Rifle | `52500592c7109667abb6cbeb` | Exact prefab alias of `583990e32459771419544dd2` |
| SIG MCX 5.56x45 assault rifle | `72bbf927bf5b1d4a0837485b` | Exact prefab alias of `5fbcc1d9016cce60e8341ab3` |
| SIG MCX SPEAR 7.62x51 assault rifle | `8d59d8b10a4c2e85b871c317` | Exact prefab alias of `65290f395ae2ae97b80fdf2d` |
| Saiga MK Ver. 030 5.45x39 Carbine | `e895575bcd1fa1de36d301b6` | Exact prefab alias of `5ac4cd105acfc40016339859` |
| Saiga SGL31 5.45x39 Carbine | `57f28a7ffb22e277b0234219` | Exact prefab alias of `5644bd2b4bdc2d3b4c8b4572` |
| FN FNX-45 Tactical .45 ACP pistol | `6868377c7bb1c07772467ee7` | Independent native-grip fit |
| Glock 22 .40 S&W pistol | `6850956dcf12c18a4d8ed9ab` | Exact prefab alias of `5a7ae0c351dfba0017554310` |
| HK MP5/40 .40 S&W submachine gun | `68bc954419645c0da0e9ea05` | Exact prefab alias of `5926bb2186f7744b1c6c6e60` |
| HK UMP .40 S&W submachine gun | `68cd937be06f1a3a720d6959` | Independent native-grip fit |
| Remington Model 700 .277 Sig Fury bolt-action sniper rifle | `0af5f6a5aa9712e11c733fb9` | Exact prefab alias of `5bfea6e90db834001b7347f3` |

## Added handheld special weapons

SP-81 (`620109578d82e67e7911abf2`) and RShG-2 (`676bf44c5539167c3603e869`) have independent owned poses and exact native controller identities. Their native fire operations differ, but both use the existing FirearmController and Idling path. The RShG-2 ready pose has a different native firing grip; its explicit firing-arm rendering policy keeps the authored weapon and visible hand together and restores the live native pose on release.

All seven RSP/ROP-30 variants share an independently fitted yellow RSP-30 pose (`624c0b3340357b5f566e8766`). Each primary prefab was checked: all 55 required local transform values, serialized controller bytes, and idle/inspection clip bytes match. Every variant is separately listed and pinned in the full inventory and additional manifest.

PKTM (`657857faeff4c850222dff1b`) is supported if equipped through the normal local firearm path. It has the same ordinary MachineGun factory and current prefab files as PKM. The native base file matches the historical fit input; the current WTT replacement is distinct and is separately pinned. The alias inherits existing PKM presentation behavior and does not alter vehicles or NPCs.

## Unregistered entries

| Weapon / internal purpose | Template | Reason |
|---|---|---|
| Master Hand | `5ae083b25acfc4001a5fc702` | Master Hand uses the internal test_consumable_items prefab. Its actual weapon_test_consumable_items controller has no Hands.IDLE or Hands.LOOK state. The current stable-idle presentation path therefore cannot run, although the item factory class is AssaultRifle. Supporting it would require a separate controller lifecycle. |
| AGS-30 30x29mm automatic grenade launcher | `5d52cc5ba4b9367408500062` | AGS-30 is explicitly IsStationaryWeapon=true. Stationary weapon interaction is outside the equipped local FirearmController.Idling route. |
| FN40GL Mk2 grenade launcher | `639af924d0446708ee62294e` | GP-25 LinkedWeapon ghost WeaponTemplate settings, referenced by launcher component 62e7e7bbe6da9612f743f1e0. Native Launcher derives GearMod and resolves this metadata through LauncherTemplate.GhostWeapon; it is not an independent FN40GL handheld variant. |
| FN40GL Mk2 grenade launcher | `639c3fbbd0446708ee622ee9` | M203 LinkedWeapon ghost WeaponTemplate settings, referenced by launcher component 6357c98711fb55120211f7e1. Native Launcher derives GearMod and resolves this metadata through LauncherTemplate.GhostWeapon; it is not an independent FN40GL handheld variant. |
| NSV Utyos 12.7x108 heavy machine gun | `5cdeb229d7f00c000e7ce174` | NSV Utyos is explicitly IsStationaryWeapon=true. Stationary weapon interaction is outside the equipped local FirearmController.Idling route. |

## Evidence and limits

The original 19 prefab aliases resolve to 21 installed files whose SHA256 values match historical native audits. PKTM adds two exact-current-prefab references, with its native base historical match and separate WTT replacement pin explicitly distinguished. Six flare aliases are supported by independent exact rig/controller/clip-byte comparisons rather than prefab identity. Each of the 6 new bundles passed 401 playback samples, 378 transform position/rotation curves, native grip and endpoint comparisons, and checks for finite values, zero events, and no camera or scale curves. All 123 original bundle hashes are unchanged.

The release also requires the separate standalone playback and visible-hand renderer fixture receipts, exact source hashes, the core suite, and package validation. Offline validation does not replace visual acceptance of the added firearms in a live raid. The catalog admits exact template/controller/clip identities; category or controller-name similarity alone never enables an unknown template.
