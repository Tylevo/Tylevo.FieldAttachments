using System;
using System.Collections.Generic;
using System.Linq;

namespace Tylevo.FieldAttachments.Core
{
    public sealed class SelectionState
    {
        public const bool StageChangesInventory = false;
        public RaidSnapshot Snapshot { get; private set; } = new RaidSnapshot();
        public AttachmentGroup Group { get; private set; }
        private readonly int[] _slots = new int[4];
        private readonly int[] _candidates = new int[4];
        public string StagedItemId { get; private set; } = "";
        public string StagedSlotPath { get; private set; } = "";
        public string StageReason { get; private set; } = "The arm key confirms the highlighted item; no inventory action.";
        public event Action<string>? Transition;
        public bool ArmedMatches => StagedItemId.Length != 0 && StagedItemId == Selected()?.Item.Id &&
            StagedSlotPath == Current(Group)?.Slot.Path;
        public string Identity => "weapon=" + Snapshot.WeaponId + " highlighted=" + Selected()?.Item.Id +
            " slot=" + Current(Group)?.Slot.Path + " armed=" + StagedItemId + " armedSlot=" + StagedSlotPath;

        public void ReplaceSnapshot(RaidSnapshot snapshot, bool preserveSelection = false, string reason = "snapshot replaced")
        {
            var oldPaths = new string[4];
            var oldItems = new string[4];
            string oldStagedItem = StagedItemId, oldStagedPath = StagedSlotPath;
            SlotViewModel? armedSlot = Snapshot.Slots.FirstOrDefault(x => x.Slot.Path == oldStagedPath);
            ItemObservation? armedItem = armedSlot?.Candidates.FirstOrDefault(x => x.Item.Id == oldStagedItem)?.Item;
            bool keep = preserveSelection && snapshot.WeaponId == Snapshot.WeaponId && !string.IsNullOrEmpty(snapshot.WeaponId);
            bool sameContext = keep && ReferenceEquals(snapshot.Player, Snapshot.Player) &&
                ReferenceEquals(snapshot.Weapon, Snapshot.Weapon) && ReferenceEquals(snapshot.Controller, Snapshot.Controller);
            if (keep)
            {
                for (int g = 0; g < 4; g++)
                {
                    SlotViewModel? old = Current((AttachmentGroup)g);
                    oldPaths[g] = old?.Slot.Path ?? "";
                    oldItems[g] = old != null && old.Candidates.Count > 0 ? old.Candidates[Wrap(_candidates[g], old.Candidates.Count)].Item.Id : "";
                }
            }
            Snapshot = snapshot;
            Array.Clear(_slots, 0, _slots.Length);
            Array.Clear(_candidates, 0, _candidates.Length);
            StagedItemId = StagedSlotPath = "";
            if (keep)
            {
                for (int g = 0; g < 4; g++)
                {
                    var choices = GroupSlots((AttachmentGroup)g);
                    int index = choices.FindIndex(x => x.Slot.Path == oldPaths[g]);
                    if (index < 0) continue;
                    _slots[g] = index;
                    _candidates[g] = Math.Max(0, choices[index].Candidates.FindIndex(x => x.Item.Id == oldItems[g]));
                }
            }
            if (oldStagedItem.Length == 0) return;
            SlotViewModel? freshSlot = snapshot.Slots.FirstOrDefault(x => x.Slot.Path == oldStagedPath);
            ItemObservation? freshItem = freshSlot?.Candidates.FirstOrDefault(x => x.Item.Id == oldStagedItem)?.Item;
            string invalid = !sameContext ? "weapon/player/controller context changed or reset" :
                snapshot.Truncated || snapshot.Warnings.Count > 0 ? "scan incomplete" :
                freshSlot == null || freshItem == null ? "armed item or slot disappeared" :
                !ReferenceEquals(freshSlot.Slot.NativeSlot, armedSlot?.Slot.NativeSlot) ||
                freshSlot.Slot.Installed?.Id != armedSlot?.Slot.Installed?.Id ||
                freshSlot.Slot.Required != armedSlot?.Slot.Required || freshSlot.Slot.Locked != armedSlot?.Slot.Locked ? "target identity or state changed" :
                !ReferenceEquals(freshItem.NativeItem, armedItem?.NativeItem) || freshItem.TemplateId != armedItem?.TemplateId ||
                freshItem.Location != armedItem?.Location || !ReferenceEquals(freshItem.SourceGrid,armedItem?.SourceGrid) ||
                !Equals(freshItem.SourceAddress,armedItem?.SourceAddress) || freshItem.Examined != true ||
                armedItem==null || !AttachmentAssembly.Same(armedItem,freshItem) ? "source identity or state changed" : "";
            StagedItemId = oldStagedItem; StagedSlotPath = oldStagedPath;
            if (invalid.Length == 0 && !ArmedMatches) invalid = "highlight no longer matches arm";
            if (invalid.Length != 0) ClearStage(reason + ": " + invalid);
            else
            {
                StageReason = "ARMED preserved: " + reason + "; same item and slot.";
                Transition?.Invoke("ARM_PRESERVED " + reason + " | " + Identity);
            }
        }
        public void NextGroup(int direction)
        { Group = (AttachmentGroup)Wrap((int)Group + direction, 4); DisarmChangedHighlight("group changed"); }
        public List<SlotViewModel> GroupSlots(AttachmentGroup group) { return Snapshot.Slots.Where(s => s.Slot.Group == group && !s.Slot.HiddenInQuickSwap)
            .OrderBy(s => DefaultPriority(s.Slot)).ToList(); }
        // Prefer the installed functional leaf; keep mounts and empty slots accessible via PgUp/PgDn.
        // Stable OrderBy preserves the game's relative order among equally ranked slots.
        private static int DefaultPriority(SlotObservation slot)
        {
            if (slot.Locked == true || slot.Required == true) return 4;
            if (slot.Installed == null) return 3;
            if (AttachmentAssembly.Supports(slot.Group,slot.Installed)) return 0;
            if (!slot.Installed.HasChildren && slot.Installed.RuntimeType != "EFT.InventoryLogic.Mount") return 0;
            return slot.Installed.HasChildren ? 2 : 1;
        }
        public int SlotIndex(AttachmentGroup group) { return _slots[(int)group]; }
        public SlotViewModel? Current(AttachmentGroup group)
        {
            List<SlotViewModel> slots = GroupSlots(group);
            return slots.Count == 0 ? null : slots[Wrap(_slots[(int)group], slots.Count)];
        }
        public void NextSlot(int direction)
        {
            int g = (int)Group;
            _slots[g] = Wrap(_slots[g] + direction, GroupSlots(Group).Count);
            _candidates[g] = 0;
            DisarmChangedHighlight("slot changed");
        }
        public void NextCandidate(int direction)
        {
            _candidates[(int)Group] = Wrap(_candidates[(int)Group] + direction, Current(Group)?.Candidates.Count ?? 0);
            DisarmChangedHighlight("candidate changed");
        }
        public int CandidateIndex(AttachmentGroup group) { return _candidates[(int)group]; }
        public bool Highlight(AttachmentGroup group, string slotPath, string? itemId = null)
        {
            if ((int)group < 0 || (int)group >= 4) return false;
            var slots = GroupSlots(group);
            if (slots.Count(s => s.Slot.Path == slotPath) != 1) return false;
            int slot = slots.FindIndex(s => s.Slot.Path == slotPath);
            int candidate = itemId == null ? 0 : slots[slot].Candidates.FindIndex(c => c.Item.Id == itemId);
            if (itemId != null && (candidate < 0 || slots[slot].Candidates.Count(c => c.Item.Id == itemId) != 1)) return false;
            bool sameSlot = _slots[(int)group] == slot;
            Group = group; _slots[(int)group] = slot;
            if (itemId != null || !sameSlot) _candidates[(int)group] = candidate;
            DisarmChangedHighlight("mouse highlight changed");
            return true;
        }
        public CandidateObservation? Selected()
        {
            SlotViewModel? model = Current(Group);
            if (model == null || model.Candidates.Count == 0) return null;
            return model.Candidates[Wrap(_candidates[(int)Group], model.Candidates.Count)];
        }
        public string Stage(string input = "Enter")
        {
            CandidateObservation? candidate = Selected();
            SlotViewModel? slot = Current(Group);
            if (candidate == null || slot == null || string.IsNullOrEmpty(candidate.Item.Id) ||
                string.IsNullOrEmpty(slot.Slot.Path) || string.IsNullOrEmpty(Snapshot.WeaponId))
            {
                ClearStage(input + " rejected: no candidate/slot/weapon identity");
                StageReason = "NOT ARMED: No candidate with verified item/slot/weapon identity. Inventory unchanged.";
                Transition?.Invoke("ARM_REJECTED " + input + " | " + Identity);
                return StageReason;
            }
            StagedItemId = candidate.Item.Id;
            StagedSlotPath = slot.Slot.Path;
            StageReason = "ARMED: " + candidate.Item.Name + " -> " + slot.Slot.ParentName + " / " + slot.Slot.Id + ". " + input + " moved nothing; separate F7 validates and executes.";
            Transition?.Invoke("ARM_ACCEPTED " + input + " | " + Identity);
            return StageReason;
        }
        public string ExecutionDenial(bool enabled, InstallSession session, bool armedThisFrame = false)
        {
            if (session.Busy || session.Blocked) return "Original request is pending or latched UNKNOWN; no new probe.";
            if (!enabled) return "Live install is OFF; no native probe started.";
            if (armedThisFrame) return "Arm and F7 arrived together. Release F7, review ARMED, then press F7 separately.";
            return ArmedMatches ? "" : "NOT ARMED: " + StageReason + " Highlight the intended item/slot, then use the arm key.";
        }
        public void ClearStage(string reason)
        {
            if (StagedItemId.Length == 0) return;
            string before = Identity;
            StagedItemId = StagedSlotPath = "";
            StageReason = "NOT ARMED: " + reason + ".";
            Transition?.Invoke("ARM_CLEARED " + reason + " | " + before);
        }
        private void DisarmChangedHighlight(string reason)
        { if (!ArmedMatches) ClearStage(reason); }
        public static int Wrap(int value, int count) { return count <= 0 ? 0 : ((value % count) + count) % count; }
    }
}
