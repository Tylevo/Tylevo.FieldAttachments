namespace Tylevo.FieldAttachments.Core
{
    public static class AttachmentLayers
    {
        public const int GroupCount = 8;
        public const int GroupsPerLayer = 4;
        public static bool IsAdvanced(AttachmentGroup group) => (int)group >= 4 && (int)group < GroupCount;
        public static bool IsMenuGroup(AttachmentGroup group) => (int)group >= 0 && (int)group < GroupCount && group != AttachmentGroup.Receiver;
        public static int Layer(AttachmentGroup group) => IsAdvanced(group) ? 1 : 0;
        public static int MenuCount(int layer) => layer == 1 ? 3 : GroupsPerLayer;
        public static AttachmentGroup Home(AttachmentGroup group) => group == AttachmentGroup.PistolGrip ? AttachmentGroup.Underbarrel :
            group == AttachmentGroup.Receiver ? AttachmentGroup.Tactical : (AttachmentGroup)((int)group % GroupsPerLayer);
        // Receiver retains its enum/scan identity but reserves the unused upper home.
        public static AttachmentGroup At(int layer, int physicalIndex) => layer == 1 && physicalIndex == 2 ? AttachmentGroup.Receiver :
            layer == 1 && physicalIndex == 3 ? AttachmentGroup.PistolGrip : (AttachmentGroup)((layer == 1 ? 4 : 0) + physicalIndex);
        public static bool TryAt(int layer, int physicalIndex, out AttachmentGroup group)
        {
            group = AttachmentGroup.Optic;
            if (layer < 0 || layer > 1 || physicalIndex < 0 || physicalIndex >= GroupsPerLayer) return false;
            group = At(layer, physicalIndex);
            return IsMenuGroup(group);
        }
        public static string Label(AttachmentGroup group)
        {
            switch (group)
            {
                case AttachmentGroup.Optic: return "OPTIC";
                case AttachmentGroup.Muzzle: return "MUZZLE";
                case AttachmentGroup.Tactical: return "TACTICAL DEVICE";
                case AttachmentGroup.Underbarrel: return "FOREGRIP";
                case AttachmentGroup.Stock: return "STOCK";
                case AttachmentGroup.Handguard: return "HANDGUARD";
                case AttachmentGroup.PistolGrip: return "PISTOL GRIP";
                case AttachmentGroup.Receiver: return "RECEIVER";
                default: return "ATTACHMENT";
            }
        }
        public static string Denial(RaidSnapshot snapshot, SlotObservation slot)
        {
            if (!IsAdvanced(slot.Group)) return "";
            if (!snapshot.MultitoolScanComplete) return "Multitool access could not be verified. Refresh the menu.";
            if (snapshot.Multitool == null) return "Requires a carried Leatherman Multitool.";
            if (!slot.Required.HasValue) return "This mounting point's requirements could not be verified.";
            if (slot.Locked != false) return "This mounting point is locked or unavailable.";
            return "";
        }
    }
}
