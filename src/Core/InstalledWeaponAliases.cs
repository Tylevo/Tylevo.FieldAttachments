using System;

namespace Tylevo.FieldAttachments.Core
{
    // Exact installed item definitions sharing a verified prefab or native rig/controller/grip source.
    // This list does not infer compatibility from weapon category or controller name.
    public static class InstalledWeaponAliases
    {
        public static string Canonical(string template)
        {
            switch(template)
            {
                case "52ce1b65b13e1035808c4fd2": return "606587252535c57a13424cfd"; // CMMG Mk47 Mutant 5.45X39 assault rifle
                case "96f5c38a676e11e13544dfba": return "606587252535c57a13424cfd"; // CMMG Mk47 Mutant 9X39 assault rifle
                case "93bcdfda236122e67c098847": return "57dc2fa62459775949412633"; // Century Arms Draco 7.62x39 carbine
                case "d672109946fe88b803449054": return "5ac66cb05acfc40198510a10"; // Kalashnikov AK-101 .300 Blackout assault rifle
                case "ffc95b9d143f52202a311820": return "5ac66d015acfc400180ae6e4"; // Kalashnikov AK-102 .300 Blackout assault rifle
                case "3dc691f607ffed3228bf6ca2": return "6499849fc93611967b034949"; // Kalashnikov AK-15 7.62x39 assault rifle
                case "ed05294ed53c0400ae0e8a55": return "57dc2fa62459775949412633"; // Kalashnikov AKS-74U .300 Blackout Assault Rifle
                case "939c742f7dad852286188029": return "5839a40f24597726f856b511"; // Kalashnikov AKS-74UB .300 Blackout Assault Rifle
                case "627c4fe34b0a558e8a3642a1": return "583990e32459771419544dd2"; // Kalashnikov AKS-74UN .300 Blackout Assault Rifle
                case "68580e9cea46c81b4db2221e": return "583990e32459771419544dd2"; // Kalashnikov AKS-74UN 5.56x45 assault rifle
                case "4b81488c78c8a8ac7d37f9b9": return "5447a9cd4bdc2dbd208b4567"; // Mechanic's Colt M4A1 .300 blackout assault rifle
                case "52500592c7109667abb6cbeb": return "583990e32459771419544dd2"; // Modified Century Arms Draco 7.62x39 Assault Rifle
                case "72bbf927bf5b1d4a0837485b": return "5fbcc1d9016cce60e8341ab3"; // SIG MCX 5.56x45 assault rifle
                case "8d59d8b10a4c2e85b871c317": return "65290f395ae2ae97b80fdf2d"; // SIG MCX SPEAR 7.62x51 assault rifle
                case "e895575bcd1fa1de36d301b6": return "5ac4cd105acfc40016339859"; // Saiga MK Ver. 030 5.45x39 Carbine
                case "57f28a7ffb22e277b0234219": return "5644bd2b4bdc2d3b4c8b4572"; // Saiga SGL31 5.45x39 Carbine
                case "6850956dcf12c18a4d8ed9ab": return "5a7ae0c351dfba0017554310"; // Glock 22 .40 S&W pistol
                case "68bc954419645c0da0e9ea05": return "5926bb2186f7744b1c6c6e60"; // HK MP5/40 .40 S&W submachine gun
                case "0af5f6a5aa9712e11c733fb9": return "5bfea6e90db834001b7347f3"; // Remington Model 700 .277 Sig Fury bolt-action sniper rifle
                case "657857faeff4c850222dff1b": return "64637076203536ad5600c990"; // PKTM: same native PKM prefab and ordinary MachineGun factory; equipped-player gate still required.
                case "62178be9d0050232da3485d9": return "624c0b3340357b5f566e8766"; // ROP-30 reactive flare cartridge (White): exact native rig/controller/idle/LOOK source.
                case "66d98233302686954b0c6f81": return "624c0b3340357b5f566e8766"; // RSP-30 reactive signal cartridge (Blue): exact native rig/controller/idle/LOOK source.
                case "675ea3d6312c0a5c4e04e317": return "624c0b3340357b5f566e8766"; // RSP-30 reactive signal cartridge (Firework): exact native rig/controller/idle/LOOK source.
                case "6217726288ed9f0845317459": return "624c0b3340357b5f566e8766"; // RSP-30 reactive signal cartridge (Green): exact native rig/controller/idle/LOOK source.
                case "62178c4d4ecf221597654e3d": return "624c0b3340357b5f566e8766"; // RSP-30 reactive signal cartridge (Red): exact native rig/controller/idle/LOOK source.
                case "66d9f1abb16d9aacf5068468": return "624c0b3340357b5f566e8766"; // RSP-30 reactive signal cartridge (Special Yellow): exact native rig/controller/idle/LOOK source.
                default: return template;
            }
        }
    }
}
