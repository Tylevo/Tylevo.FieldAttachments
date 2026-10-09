using System;
using System.Collections.Generic;

namespace Tylevo.FieldAttachments.Core
{
    public static class CandidatePolicy
    {
        public static bool TryGroup(string slotId, out AttachmentGroup group)
        {
            group = AttachmentGroup.Optic;
            if (string.IsNullOrEmpty(slotId)) return false;
            string id = slotId.ToLowerInvariant();
            if (id.StartsWith("mod_scope", StringComparison.Ordinal)) { group = AttachmentGroup.Optic; return true; }
            if (id.StartsWith("mod_muzzle", StringComparison.Ordinal)) { group = AttachmentGroup.Muzzle; return true; }
            if (id.StartsWith("mod_tactical", StringComparison.Ordinal)) { group = AttachmentGroup.Tactical; return true; }
            if (id.StartsWith("mod_foregrip", StringComparison.Ordinal)) { group = AttachmentGroup.Underbarrel; return true; }
            return false;
        }

        public static CandidateEvidence Assess(SlotObservation slot, ItemObservation item)
        {
            if (!string.IsNullOrEmpty(item.TemplateId) && slot.Installed != null &&
                item.TemplateId == slot.Installed.TemplateId) return CandidateEvidence.SameInstalledTemplate;
            if (!string.IsNullOrEmpty(item.TemplateId) && slot.ExplicitFilterIds.Contains(item.TemplateId))
                return CandidateEvidence.ExplicitFilterHit;
            // A same runtime subclass is only a category hint. It says NOTHING about actual fit.
            if (slot.Installed != null && SpecificClass(item.RuntimeType) &&
                item.RuntimeType == slot.Installed.RuntimeType) return CandidateEvidence.CategoryOnly;
            return CandidateEvidence.Unverified;
        }

        private static bool SpecificClass(string name)
        {
            return !string.IsNullOrEmpty(name) && name != "EFT.InventoryLogic.Mod" &&
                name != "EFT.InventoryLogic.Item" && name != "EFT.InventoryLogic.CompoundItem";
        }

        public static List<CandidateObservation> Build(SlotObservation slot, IEnumerable<ItemObservation> carried,
            bool includeCategoryHints, bool includeAllUnknown, Func<SlotObservation, ItemObservation, bool?>? nativeFilter = null)
        {
            var result = new List<CandidateObservation>();
            if (slot.Locked != false || slot.Required != false || slot.HiddenInQuickSwap) return result;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemObservation item in carried)
            {
                if (string.IsNullOrEmpty(item.Id) || !seen.Add(item.Id) || item.Id == slot.Installed?.Id) continue;
                if (item.Examined != true || item.RaidModdable != true || !AttachmentAssembly.Supports(slot.Group,item)) continue;
                bool? native = nativeFilter?.Invoke(slot, item);
                if (native != true) continue; // Picker shows only actual native fit; old diagnostic switches cannot add guesses.
                CandidateEvidence evidence = CandidateEvidence.NativeFilterPass;
                result.Add(new CandidateObservation { Item = item, Evidence = evidence, Detail = Explain(evidence) });
            }
            result.Sort((a, b) => {
                int c = a.Evidence.CompareTo(b.Evidence);
                if (c != 0) return c;
                c = string.Compare(a.Item.Name, b.Item.Name, StringComparison.OrdinalIgnoreCase);
                return c != 0 ? c : string.Compare(a.Item.Id, b.Item.Id, StringComparison.Ordinal);
            });
            return result;
        }

        public static string Explain(CandidateEvidence evidence)
        {
            switch (evidence)
            {
                case CandidateEvidence.NativeFilterPass: return "Native include/exclude filter passed; full move and hands checks still required";
                case CandidateEvidence.SameInstalledTemplate: return "Same template; swap not validated";
                case CandidateEvidence.ExplicitFilterHit: return "Filter ID hit; conflicts/raid rules unchecked";
                case CandidateEvidence.CategoryOnly: return "Same item class only; fit unknown";
                default: return "Diagnostic candidate only; fit unknown";
            }
        }
    }
}
