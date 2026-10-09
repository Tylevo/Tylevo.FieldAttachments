using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tylevo.FieldAttachments.Core;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed class RaidReader
    {
        private readonly ReadAccess _r;
        private readonly NativeResources _resources;
        private readonly NativeFilter _filter;
        private readonly LocalSessionReader _session;
        public RaidReader(ReadAccess read, NativeResources resources) { _r = read; _resources = resources; _filter = new NativeFilter(read); _session = new LocalSessionReader(read); }

        public object? MainPlayer() => _session.MainPlayer();
        public object? HeldWeapon(object? player)
        {
            object? hands = _r.Get(player, "HandsController");
            object? item = _r.Get(hands, "Item");
            return ReadAccess.IsKind(item, "EFT.InventoryLogic.Weapon") ? item : null;
        }
        public string HeldId()
        {
            object? p = MainPlayer();
            return ReadAccess.Text(_r.Get(HeldWeapon(p), "Id"));
        }
        public bool Alive(object? player)
        {
            bool? alive = ReadAccess.Bool(_r.Get(_r.Get(player, "HealthController"), "IsAlive"));
            return alive != false;
        }
        private bool? Examined(object? controller, object item)
        {
            if (controller == null) return null;
            var methods = ReadAccess.Methods(controller.GetType(), "Examined", false).Where(m => {
                ParameterInfo[] p = m.GetParameters();
                return !m.ContainsGenericParameters && m.ReturnType == typeof(bool) && p.Length == 1 && p[0].ParameterType.IsInstanceOfType(item);
            }).ToList();
            if (methods.Count != 1) { _r.Note("Examined(Item) unresolved/ambiguous; excluding unknown item identities."); return null; }
            try { return (bool?)methods[0].Invoke(controller, new[] { item }); }
            catch (Exception e) { _r.Note("Examined failed: " + e.GetBaseException().GetType().Name); return null; }
        }
        private ItemObservation Observe(object item, string location, object? controller, bool installed)
        {
            object? template = _r.Get(item, "Template");
            string templateId = ReadAccess.Text(_r.Get(item, "TemplateId"));
            string name = ReadAccess.Text(_r.Get(item, "ShortName", "Name"));
            if (string.IsNullOrWhiteSpace(name)) name = ReadAccess.Text(_r.Get(template, "ShortName", "Name"));
            name = _resources.Localize(name);
            if (string.IsNullOrWhiteSpace(name)) name = item.GetType().Name + " / " + Tail(templateId);
            bool hasChildren = _r.Items(_r.Get(item, "Slots")).Any(s => _r.Get(s, "ContainedItem") != null);
            return new ItemObservation {
                Id = ReadAccess.Text(_r.Get(item, "Id")), TemplateId = templateId, Name = name,
                RuntimeType = item.GetType().FullName ?? item.GetType().Name, Location = location,
                RaidModdable = ReadAccess.IsKind(item, "EFT.InventoryLogic.Mod") ? ReadAccess.Bool(_r.Get(item, "RaidModdable")) : null, HasChildren = hasChildren,
                Examined = installed ? true : Examined(controller, item), NativeItem = item,
                Assembly = hasChildren && ReadAccess.IsKind(item,"EFT.InventoryLogic.Mod") ? new AttachmentAssemblyReader(_r).Capture(item,controller) : null
            };
        }
        private static string Tail(string text) { return text.Length > 8 ? text.Substring(text.Length - 8) : text; }

        public RaidSnapshot Capture(bool categoryHints, bool showUnknown)
        {
            var snapshot = new RaidSnapshot();
            try
            {
                object? player = MainPlayer(); snapshot.Player = player;
                if (player == null) { snapshot.Status = "No local player. Enter a raid or the hideout shooting range."; return snapshot; }
                if (!Alive(player)) { snapshot.Status = "Player is not alive."; return snapshot; }
                object? hands = _r.Get(player, "HandsController");
                object? weapon = HeldWeapon(player); snapshot.Weapon = weapon;
                snapshot.PlayerType = player.GetType().FullName ?? "";
                snapshot.HandsType = hands?.GetType().FullName ?? "";
                if (weapon == null) { snapshot.Status = "No held firearm detected (or Item binding unresolved). Equip a gun and rescan."; return snapshot; }
                object? controller = _r.Get(player, "InventoryController");
                snapshot.Controller = controller;
                snapshot.InventoryControllerType = controller?.GetType().FullName ?? "UNRESOLVED";
                LocalSessionFacts session = _session.Read(snapshot);
                snapshot.SessionContext = session.Evidence;
                if (session.Hideout && !session.Allowed) { snapshot.Status = session.Denial; return snapshot; }
                object? operation = _r.Get(hands, "CurrentOperation", "CurrentHandsOperation");
                snapshot.CurrentHandsOperationType = operation?.GetType().FullName ?? "UNRESOLVED";
                snapshot.InventoryOpened = ReadAccess.Bool(_r.Get(hands, "InventoryOpened"));
                snapshot.IsAiming = ReadAccess.Bool(_r.Get(hands, "IsAiming"));
                snapshot.IsTriggerPressed = ReadAccess.Bool(_r.Get(hands, "IsTriggerPressed"));
                snapshot.InventoryLocked = ReadAccess.Bool(_r.Get(controller, "Locked"));
                ItemObservation gun = Observe(weapon, "Held", controller, true);
                snapshot.WeaponId = gun.Id; snapshot.WeaponName = gun.Name;
                var storage=new CarriedStorageReader(_r).Capture(controller);
                snapshot.ScanRoots.AddRange(storage.Roots); snapshot.Warnings.AddRange(storage.Warnings);
                snapshot.TraversedItems=storage.Entries;
                foreach(var grid in storage.Grids)
                {
                    var root=grid.Root;
                    foreach(object item in grid.Items)
                    {
                        var detail=new ScanEntryObservation {EntryType=item.GetType().FullName ?? "",ItemType=item.GetType().FullName ?? ""};
                        if (root.Details.Count<128) root.Details.Add(detail);
                        if (!ReadAccess.IsKind(item,"EFT.InventoryLogic.Mod")) { root.NonMods++; detail.Outcome="not a weapon attachment"; continue; }
                        ItemObservation observed=Observe(item,grid.Location,controller,false);
                        observed.SourceGrid=grid.NativeGrid; observed.SourceAddress=_r.Get(item,"Parent");
                        if (observed.Examined!=true) { snapshot.ExcludedUnexamined++; root.ExcludedUnexamined++; detail.Outcome="unexamined or examination unknown"; continue; }
                        if (observed.HasChildren && !AttachmentAssembly.Supports(AttachmentGroup.Optic,observed))
                        { snapshot.ExcludedAssemblies++; root.ExcludedAssemblies++; detail.Outcome="unsupported/incomplete assembly"; continue; }
                        snapshot.Carried.Add(observed); root.AcceptedMods++; detail.Outcome=observed.HasChildren ? "intact optic assembly" : "accepted loose attachment";
                    }
                }
                List<object> slots = _r.Items(_r.Get(weapon, "AllSlots"), 256).ToList();
                if (slots.Count == 0)
                {
                    var nodes = new HashSet<object>();
                    WalkSlots(weapon, slots, nodes, 0);
                }
                var uniqueSlots = new HashSet<string>(StringComparer.Ordinal);
                foreach (object slot in slots)
                {
                    string slotId = ReadAccess.Text(_r.Get(slot, "ID", "Id"));
                    if (!CandidatePolicy.TryGroup(slotId, out AttachmentGroup group)) continue;
                    object? parent = _r.Get(slot, "ParentItem");
                    string parentId = ReadAccess.Text(_r.Get(parent, "Id"));
                    string path = gun.Id + "/" + (string.IsNullOrEmpty(parentId) ? "unknown-parent" : parentId) + "/" + slotId;
                    if (!uniqueSlots.Add(path)) { snapshot.Warnings.Add("Duplicate/unresolved slot path excluded: " + slotId); continue; }
                    object? installed = _r.Get(slot, "ContainedItem");
                    var observation = new SlotObservation {
                        Id = slotId, Path = path, Group = group, NativeSlot = slot,
                        ParentName = _resources.Localize(ReadAccess.Text(_r.Get(parent, "ShortName", "Name"))),
                        Required = ReadAccess.Bool(_r.Get(slot, "Required")), Locked = ReadAccess.Bool(_r.Get(slot, "Locked")),
                        Installed = installed == null ? null : Observe(installed, "Installed", controller, true)
                    };
                    // Best-effort field observations only. A filter ID hit is never advertised as complete compatibility.
                    foreach (object filter in _r.Items(_r.Get(slot, "Filters"), 32))
                    {
                        foreach (object id in _r.Items(_r.Get(filter, "Filter"), 2048)) observation.ExplicitFilterIds.Add(ReadAccess.Text(id));
                    }
                    var vm = new SlotViewModel { Slot = observation };
                    vm.Candidates.AddRange(CandidatePolicy.Build(observation, snapshot.Carried, categoryHints, showUnknown, (s, i) => _filter.Accepts(s.NativeSlot, i.NativeItem)));
                    snapshot.Slots.Add(vm);
                }
                // A removable optic unit owns its child slots. Never offer its mounted
                // scope as a second quick-swap position. Fixed mounts keep usable child slots.
                AttachmentAssembly.CollapseOpticPositions(snapshot);
                snapshot.Status = snapshot.Slots.Count == 0 ? "Weapon detected; no supported slots resolved. Export the report." :
                    "Native filter results are not full move authorization. Enter stages only; live install is opt-in.";
                if (controller == null) snapshot.Warnings.Add("Inventory controller unresolved: examined status cannot be verified, so carried candidates are withheld.");
                if (snapshot.Truncated) snapshot.Warnings.Add("Scan cap reached; this snapshot is partial.");
            }
            catch (Exception e)
            {
                snapshot.Status = "Read-only scan failed; export the report.";
                snapshot.Warnings.Add(e.GetBaseException().GetType().FullName + ": " + e.GetBaseException().Message);
                _r.Note("Capture exception: " + e.GetBaseException().GetType().Name + ": " + e.GetBaseException().Message);
            }
            return snapshot;
        }
        private void WalkSlots(object item, List<object> output, HashSet<object> visited, int depth)
        {
            if (depth > 12 || output.Count >= 256 || !visited.Add(item)) return;
            foreach (object slot in _r.Items(_r.Get(item, "Slots"), 128))
            {
                output.Add(slot);
                object? child = _r.Get(slot, "ContainedItem");
                if (child != null) WalkSlots(child, output, visited, depth + 1);
                if (output.Count >= 256) return;
            }
        }
    }
}
