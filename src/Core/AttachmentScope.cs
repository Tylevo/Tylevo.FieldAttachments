namespace Tylevo.FieldAttachments.Core
{
    public static class AttachmentScope
    {
        // Concrete families verified in the gated 4.1.5 Assembly-CSharp metadata.
        // This only limits scope. Native filters, raid rules and Move validation
        // still decide whether a particular item can use a particular slot.
        public static bool Supports(AttachmentGroup? group, string? runtimeType)
        {
            switch (group)
            {
                case AttachmentGroup.Optic:
                    return runtimeType=="EFT.InventoryLogic.SightMod" ||
                        runtimeType=="EFT.InventoryLogic.AssaultScope" || runtimeType=="EFT.InventoryLogic.Collimator" ||
                        runtimeType=="EFT.InventoryLogic.CompactCollimator" || runtimeType=="EFT.InventoryLogic.IronSight" ||
                        runtimeType=="EFT.InventoryLogic.OpticScope" || runtimeType=="EFT.InventoryLogic.SpecialScope" ||
                        runtimeType=="EFT.InventoryLogic.NightVision" || runtimeType=="EFT.InventoryLogic.ThermalVision";
                case AttachmentGroup.Muzzle:
                    return runtimeType=="EFT.InventoryLogic.MuzzleMod" ||
                        runtimeType=="EFT.InventoryLogic.Compensator" || runtimeType=="EFT.InventoryLogic.FlashHider" ||
                        runtimeType=="EFT.InventoryLogic.MuzzleCombo" || runtimeType=="EFT.InventoryLogic.Pms" ||
                        runtimeType=="EFT.InventoryLogic.Silencer";
                case AttachmentGroup.Tactical: return runtimeType=="EFT.InventoryLogic.TacticalCombo";
                case AttachmentGroup.Underbarrel: return runtimeType=="EFT.InventoryLogic.Foregrip";
                default: return false;
            }
        }
    }
}
