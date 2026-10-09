namespace Tylevo.FieldAttachments.Core
{
    // Exact native contexts only. A hideout player is not sufficient: its range copy
    // must be active, separate from the original inventory, and finished rebuilding.
    public sealed class LocalSessionFacts
    {
        public bool KnownBuild, FikaLoaded, SamePlayer, SameController, SameWeapon;
        public string PlayerType = "", HandsType = "", ControllerType = "";
        public bool? InRaid, YourPlayer, Alive, InShootingRange, UpdatingInventory, InPatrol, FirearmsBlocked;
        public bool RangeInventoryMatches, OriginalInventorySeparate, RangeEquipmentSeparate;
        public bool Hideout => PlayerType == "EFT.HideoutPlayer";
        public bool SupportedTypes => (PlayerType == "EFT.LocalPlayer" || Hideout) &&
            HandsType == "EFT.Player+FirearmController" && ControllerType == "EFT.Player+SinglePlayerInventoryController";
        public string Denial
        {
            get
            {
                if (!KnownBuild) return "Game assembly differs from the inspected build.";
                if (FikaLoaded || !SupportedTypes) return "Requires the native single-player raid or hideout range controllers.";
                if (!SamePlayer || !SameController || !SameWeapon || YourPlayer != true || Alive != true)
                    return "Local player/weapon/inventory identity changed or is unavailable.";
                if (!Hideout) return InRaid == true ? "" : "Local raid is inactive or unknown.";
                if (InShootingRange != true || UpdatingInventory != false || InPatrol != false || FirearmsBlocked != false)
                    return "Hideout shooting range is inactive, in patrol, or rebuilding its inventory.";
                if (!RangeInventoryMatches || !OriginalInventorySeparate || !RangeEquipmentSeparate)
                    return "Separate native shooting-range inventory could not be verified.";
                return "";
            }
        }
        public bool Allowed => Denial.Length == 0;
        public string Evidence => "context=" + (Hideout ? "hideout-range (temporary equipment)" : "raid") +
            " player=" + PlayerType + " hands=" + HandsType + " inventory=" + ControllerType +
            " inRaid=" + InRaid + " range=" + InShootingRange + " updating=" + UpdatingInventory +
            " patrol=" + InPatrol + " firearmsBlocked=" + FirearmsBlocked + " rangeInventory=" + RangeInventoryMatches +
            " separateOriginal=" + OriginalInventorySeparate + " separateEquipment=" + RangeEquipmentSeparate +
            " allowed=" + Allowed + " reason=" + Denial;
    }
}
