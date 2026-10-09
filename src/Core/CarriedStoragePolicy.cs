using System;

namespace Tylevo.FieldAttachments.Core
{
    public static class CarriedStoragePolicy
    {
        // Stable native equipment IDs; no stash, loot, weapon slots or automatic rearrangement.
        public static readonly string[] Roots = { "Pockets", "TacticalVest", "Backpack", "SecuredContainer" };
        public const string Description = "pockets + rig + backpack + secure container (accessible nested storage)";
        public static bool Allows(string location)
        {
            foreach (string root in Roots)
                if (location==root || location.StartsWith(root+"/",StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
