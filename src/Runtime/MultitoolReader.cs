using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tylevo.FieldAttachments.Core;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed class MultitoolScan
    {
        public bool Complete { get; }
        public MultitoolAccess? Tool { get; }
        public string Reason { get; }
        internal MultitoolScan(bool complete, MultitoolAccess? tool, string reason)
        { Complete = complete; Tool = tool; Reason = reason; }
    }

    // Observes owned storage only. Never equips, consumes, reveals or moves a tool.
    public sealed class MultitoolReader
    {
        private readonly ReadAccess _read;
        public MultitoolReader(ReadAccess read) { _read = read ?? throw new ArgumentNullException(nameof(read)); }

        public MultitoolScan Capture(object? controller)
        {
            try
            {
                object? inventory = _read.Get(controller, "Inventory");
                object? equipment = _read.Get(inventory, "Equipment");
                if (controller == null || !ReadAccess.IsKind(equipment, "EFT.InventoryLogic.InventoryEquipment") ||
                    !ReferenceEquals(_read.Get(equipment, "Owner"), controller))
                    throw new InvalidOperationException("Owned equipment unavailable.");
                var storage = new CarriedStorageReader(_read).Capture(controller);
                if (!storage.Complete) throw new InvalidOperationException("Carried storage scan incomplete.");
                var found = new List<MultitoolAccess>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (StorageGrid grid in storage.Grids)
                    foreach (object item in grid.Items)
                        Observe(item, grid.NativeGrid, grid.Location, controller, false, found, seen);

                object[] roots = Bounded(_read.Get(equipment, "Slots"), 64);
                object[] pockets = roots.Where(s => ReadAccess.Text(_read.Get(s, "ID")) == "Pockets").ToArray();
                if (pockets.Length != 1 || !ReadAccess.IsKind(pockets[0], "EFT.InventoryLogic.Slot") ||
                    !ReferenceEquals(_read.Get(pockets[0], "ParentItem"), equipment))
                    throw new InvalidOperationException("Exact pocket equipment slot unavailable.");
                object? pocket = Contained(pockets[0]);
                if (pocket != null)
                {
                    if (!ReferenceEquals(_read.Get(_read.Get(pocket, "Parent"), "Container"), pockets[0]) ||
                        !ReferenceEquals(_read.Get(pocket, "Owner"), controller))
                        throw new InvalidOperationException("Pocket ownership changed.");
                    // The storage reader already excludes unsearched/unknown pocket contents.
                    if (storage.Roots.Single(r => r.Root == "Pockets").Status == "scanned")
                    {
                        object[] slots = Bounded(_read.Get(pocket, "Slots"), 128);
                        foreach (string id in new[] { "SpecialSlot1", "SpecialSlot2", "SpecialSlot3" })
                        {
                            object[] matches = slots.Where(s => ReadAccess.Text(_read.Get(s, "ID")) == id).ToArray();
                            if (matches.Length == 0) continue; // A valid older/custom pocket layout may have none.
                            if (matches.Length != 1 || !ReadAccess.IsKind(matches[0], "EFT.InventoryLogic.Slot") ||
                                !ReferenceEquals(_read.Get(matches[0], "ParentItem"), pocket))
                                throw new InvalidOperationException("Special slot ownership is ambiguous.");
                            object slot = matches[0];
                            object? item = Contained(slot);
                            if (item == null) continue;
                            Observe(item, slot, "Pockets/" + id, controller, true, found, seen);
                        }
                    }
                }
                if (!ReferenceEquals(_read.Get(controller, "Inventory"), inventory) ||
                    !ReferenceEquals(_read.Get(inventory, "Equipment"), equipment) ||
                    !ReferenceEquals(_read.Get(equipment, "Owner"), controller))
                    throw new InvalidOperationException("Equipment changed during tool observation.");
                MultitoolAccess? tool = found.OrderBy(t => t.Location, StringComparer.Ordinal)
                    .ThenBy(t => t.ItemId, StringComparer.Ordinal).FirstOrDefault();
                return new MultitoolScan(true, tool, tool == null ?
                    "Carry an unlocked Leatherman Multitool in accessible storage or a pocket special slot." : "");
            }
            catch (Exception e)
            { return new MultitoolScan(false, null, "Multitool check unavailable: " + e.GetBaseException().Message); }
        }

        public string Validate(object? controller, MultitoolAccess? expected)
        {
            if (expected == null) return "No carried multitool was bound to this request.";
            MultitoolScan fresh = Capture(controller);
            if (!fresh.Complete || fresh.Tool == null) return fresh.Reason;
            return expected.Same(fresh.Tool) ? "" : "Carried multitool identity or location changed.";
        }

        private void Observe(object item, object container, string location, object controller, bool special,
            List<MultitoolAccess> found, HashSet<string> seen)
        {
            string template = ReadAccess.Text(_read.Get(item, "TemplateId"));
            if (template != MultitoolAccess.LeathermanTemplate) return;
            if (!ReadAccess.IsKind(item, "EFT.InventoryLogic.MultiTool"))
                throw new InvalidOperationException("Leatherman native item type changed.");
            string id = ReadAccess.Text(_read.Get(item, "Id"));
            object? address = _read.Get(item, "Parent");
            if (id.Length == 0 || !seen.Add(id) || !ReadAccess.IsKind(address, special ?
                    "EFT.InventoryLogic.SlotItemAddress" : "EFT.InventoryLogic.GridItemAddress") ||
                !ReferenceEquals(_read.Get(address, "Container"), container) ||
                !ReferenceEquals(_read.Get(item, "Owner"), controller))
                throw new InvalidOperationException("Multitool ownership or address changed.");
            object? stack = _read.Get(item, "StackObjectsCount"), pin = _read.Get(item, "PinLockState");
            if (!(stack is int count) || (pin == null || !(pin is int) && !pin.GetType().IsEnum))
                throw new InvalidOperationException("Multitool stack or lock state is unknown.");
            if (special && ReadAccess.Bool(_read.Get(container, "Locked")) == null)
                throw new InvalidOperationException("Special slot lock state is unknown.");
            if (count != 1 || Convert.ToInt32(pin) != 0 ||
                special && ReadAccess.Bool(_read.Get(container, "Locked")) != false) return;
            if (!ReferenceEquals(_read.Get(item, "Parent"), address))
                throw new InvalidOperationException("Multitool address changed during observation.");
            found.Add(new MultitoolAccess(id, template, location, item, container, address!));
        }

        private static object? Contained(object slot)
        {
            PropertyInfo? member = slot.GetType().GetProperty("ContainedItem", BindingFlags.Public | BindingFlags.Instance);
            if (member == null) throw new InvalidOperationException("ContainedItem getter unavailable.");
            return member.GetValue(slot, null);
        }
        private static object[] Bounded(object? value, int limit)
        {
            if (!(value is IEnumerable sequence) || value is string)
                throw new InvalidOperationException("Slot collection unavailable.");
            var result = new List<object>();
            foreach (object? item in sequence)
            {
                if (item == null || result.Count >= limit) throw new InvalidOperationException("Slot collection invalid or capped.");
                result.Add(item);
            }
            return result.ToArray();
        }
    }
}
