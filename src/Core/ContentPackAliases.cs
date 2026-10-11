namespace Tylevo.FieldAttachments.Core
{
    // Explicit optional-pack receivers using existing native weapon models.
    // Proof and exact pack versions: assets/Animation/content-pack-aliases.json.
    // This maps presentation only; native inventory keeps the original receiver.
    public static class ContentPackAliases
    {
        public const int Count = 6;
        public static string Canonical(string template)
        {
            switch (template)
            {
                case "691098356f593043295f6ddc": return "61f7c9e189e6fb1a5e3ea78d"; // Make Shotguns Great Again!: MP-12 12g single-shot rifle
                case "6910e86bbcb9218635891ae2": return "5580223e4bdc2d1c128b457f"; // Make Shotguns Great Again!: MP-700 .700 Nitro Express Double Rifle
                case "69e90fbb0655531529ff1891": return "5447a9cd4bdc2dbd208b4567"; // C11 - True North: AR-15  22lr
                case "6a67d15e807645163d053cb0": return "620109578d82e67e7911abf2"; // Make Shotguns Great Again!: ZiD SP-81 23x75 pistol
                case "6a8103d9e2f14c7b9a5d3e01": return "64748cb8de82c85eaf0a273a"; // Make Shotguns Great Again!: MP-700 Shorty .700 Nitro Express Double Rifle
                case "68a3836826dffa87b5767c04": return "627e14b21713922ded6f2c15"; // WTT Armory: AXMC .300 WM
                default: return WeaponPackAliases.Canonical(template);
            }
        }
    }
}
