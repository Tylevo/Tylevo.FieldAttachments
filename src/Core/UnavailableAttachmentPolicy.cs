using System;
using System.Collections.Generic;
using System.Linq;

namespace Tylevo.FieldAttachments.Core
{
    // Display-only explanations cannot carry an item identity into the action path.
    public sealed class UnavailableAttachment
    {
        public string DisplayName { get; }
        public string Source { get; }
        public string Reason { get; }
        public UnavailableAttachment(string displayName, string source, string reason)
        { DisplayName = displayName; Source = source; Reason = reason; }
    }

    public static class UnavailableAttachmentPolicy
    {
        public static List<UnavailableAttachment> Build(RaidSnapshot snapshot, SlotViewModel view,
            IEnumerable<ItemObservation> carried, Func<SlotObservation, ItemObservation, bool?> nativeFilter)
        {
            var result = new List<UnavailableAttachment>();
            var slot = view.Slot;
            if (slot.HiddenInQuickSwap || !AttachmentLayers.IsMenuGroup(slot.Group)) return result;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in carried)
            {
                // Match the picker's first identity wins rule. Only accessible carried
                // observations enter this list; installed parts and other categories do not.
                if (item.Id.Length == 0 || !seen.Add(item.Id) || item.Id == slot.Installed?.Id ||
                    !CarriedStoragePolicy.Allows(item.Location) || !Relevant(slot.Group, item)) continue;
                if (view.Candidates.Any(c => ReferenceEquals(c.Item, item) && c.Evidence == CandidateEvidence.NativeFilterPass) &&
                    AttachmentLayers.Denial(snapshot, slot).Length == 0 &&
                    AttachmentRequest.ScopeDenial(AttachmentRequest.ChoiceAction(slot), slot, item, snapshot.Multitool).Length == 0) continue;
                string reason = Explain(snapshot, slot, item, nativeFilter);
                string name = item.Examined == true ? item.Name :
                    item.Examined == false ? "Unexamined attachment" : "Unidentified attachment";
                result.Add(new UnavailableAttachment(name.Length == 0 ? "Attachment" : name, Source(item.Location), reason));
            }
            result.Sort((a, b) => {
                int order = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
                return order != 0 ? order : string.Compare(a.Source, b.Source, StringComparison.Ordinal);
            });
            return result;
        }

        private static bool Relevant(AttachmentGroup group, ItemObservation item) =>
            AttachmentScope.Supports(group, item.RuntimeType) ||
            (group == AttachmentGroup.Optic && item.RuntimeType == "EFT.InventoryLogic.Mount" &&
             item.Assembly?.Parts.Any(p => p.Item != null && AttachmentScope.Supports(AttachmentGroup.Optic, p.RuntimeType)) == true);

        private static string Explain(RaidSnapshot snapshot, SlotObservation slot, ItemObservation item,
            Func<SlotObservation, ItemObservation, bool?> nativeFilter)
        {
            if (item.Examined == false) return "Examine this attachment first.";
            if (item.Examined != true) return "Can't verify whether this attachment is examined.";
            bool advanced = AttachmentLayers.IsAdvanced(slot.Group);
            if (!item.RaidModdable.HasValue) return "Can't check whether this part can be changed in raid.";
            if (!advanced && item.RaidModdable == false) return "This part can't be changed in raid.";
            if (item.Assembly?.Complete == true && item.Assembly.Parts.Any(p => !p.Deleted && p.Required && p.Item == null))
                return "A required part is missing from this attachment.";
            if (item.HasChildren && item.Assembly?.Complete != true) return "Can't check the parts attached to this item.";
            if (item.Assembly?.Complete == true && item.Assembly.Parts.Any(p => p.Item != null && !p.Examined))
                return "Can't verify that every attached part is examined.";
            if (!AttachmentAssembly.Supports(slot.Group, item)) return "This assembled part isn't supported.";
            if (slot.Required == true && advanced && !AttachmentAssembly.SupportsRequiredPart(slot.Group, item))
                return "Can't verify all required parts on this attachment.";
            bool? fits;
            try { fits = nativeFilter(slot, item); }
            catch { fits = null; }
            if (fits == false) return "Doesn't fit this mounting point.";
            if (fits != true) return "Can't check if this part fits.";
            if (slot.Locked == true) return "This mounting point is locked.";
            if (!slot.Required.HasValue || !slot.Locked.HasValue) return "Can't check this mounting point.";
            if (advanced && !snapshot.MultitoolScanComplete) return "Can't check your multitool. Refresh the menu.";
            if (advanced && snapshot.Multitool == null) return "Carry a Leatherman Multitool to change this part.";
            if (slot.Required == true && (!advanced || slot.Installed == null))
                return "This required part must be changed outside the raid.";
            if (slot.Installed != null &&
                AttachmentRequest.ScopeDenial(AttachmentAction.Replace, slot, item, snapshot.Multitool).Length != 0)
                return "The installed part can't be removed here.";
            return "This part isn't available at this mounting point.";
        }

        private static string Source(string location)
        {
            int slash = location.IndexOf('/');
            string root = slash < 0 ? location : location.Substring(0, slash);
            return root == "TacticalVest" ? "Rig" : root == "SecuredContainer" ? "Secure container" : root;
        }
    }
}
