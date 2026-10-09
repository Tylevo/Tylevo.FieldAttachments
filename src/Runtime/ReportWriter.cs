using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed class ReportWriter
    {
        private readonly ReadAccess _r;
        private readonly string _root;
        public ReportWriter(ReadAccess read, string root) { _r = read; _root = root; }

        public string Write(RaidSnapshot snapshot, SelectionState state, bool includeIl, string probeHistory, bool installEnabled, bool installBlocked, string uiLayout, string pose)
        {
            string folder = Path.Combine(_root, "Reports", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 5));
            Directory.CreateDirectory(folder);
            WriteText(Path.Combine(folder, "snapshot.json"), SnapshotJson(snapshot, state, installEnabled, installBlocked));
            
            WriteText(Path.Combine(folder, "scan-trace.txt"), ScanTrace(snapshot));
            WriteText(Path.Combine(folder, "ui-layout.txt"), uiLayout);
            WriteText(Path.Combine(folder, "inspection-pose.txt"), pose);
            WriteText(Path.Combine(folder, "runtime.txt"), RuntimeInfo());
            WriteText(Path.Combine(folder, "api-map.txt"), new TargetedApiMap(_r).Write(snapshot, includeIl));
            WriteText(Path.Combine(folder, "install-probe.txt"), string.IsNullOrEmpty(probeHistory) ? "No live install attempt recorded during this plugin session.\n" : probeHistory);
            WriteText(Path.Combine(folder, "bindings.txt"), string.Join(Environment.NewLine, _r.Evidence));
            WriteText(Path.Combine(folder, "ABOUT.txt"), "Field Attachments 0.25.2 diagnostic report.\nAn opt-in native install/uninstall probe is present. Read install-probe.txt for attempts and outcomes; this report alone does not mean an item was moved.\n" +
                "Native filter acceptance is not complete compatibility or move authorization. Replacement uses sequential native moves; no atomic swap executor exists.\n" +
                "API signatures/optional IL are from locally loaded assemblies; absence is not proof a capability does not exist.\n" +
                "This install-probe.txt is a history copy at F10 time. The collector also includes the latest automatic session journal and a timestamp index; that journal may contain later requests.\n" +
                "Reports may contain item IDs, names, plugin/type names, and local method IL. No full profile, email, auth token or entire inventory is serialized. Review before sharing.\n" +
                "Send snapshot.json, scan-trace.txt, install-probe.txt, bindings.txt, runtime.txt, and api-map.txt together for analysis. Do not publish game-derived IL in a public source repository.\n");
            return folder;
        }
        private static void WriteText(string path, string content) { File.WriteAllText(path, content, new UTF8Encoding(false)); }
        private static string RuntimeInfo()
        {
            var b = new StringBuilder();
            b.AppendLine("Field Attachments 0.25.2; opt-in native install/remove and sequential replacement; no atomic swap.");
            b.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            b.AppendLine("Unity: " + Application.unityVersion);
            b.AppendLine("Screen: " + Screen.width + "x" + Screen.height);
            b.AppendLine("Batch mode: " + Application.isBatchMode);
            b.AppendLine("Targets SPT 4.1.5/4.1.6 with the inspected native client. Assembly metadata below is observed, not proof of live compatibility.");
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.GetName().Name))
            {
                string n = a.GetName().Name ?? "";
                if (n != "Assembly-CSharp" && n != "BepInEx" && n != "UnityEngine.CoreModule" && n != "UnityEngine.UI" && n != "UnityToolkit" && n != "UniTask" &&
                    n != "Comfort" && n != "ItemComponent.Types" && !n.StartsWith("spt", StringComparison.OrdinalIgnoreCase) &&
                    !n.StartsWith("Fika", StringComparison.OrdinalIgnoreCase) && n != "Tylevo.FieldAttachments") continue;
                try { b.AppendLine(a.GetName().FullName + " | MVID=" + a.ManifestModule.ModuleVersionId); }
                catch { b.AppendLine(n + " | metadata unavailable"); }
            }
            return b.ToString();
        }
        private static string SnapshotJson(RaidSnapshot s, SelectionState state, bool installEnabled, bool installBlocked)
        {
            var b = new StringBuilder("{\n");
            void Field(string name, string value) { b.Append("  ").Append(JsonText.Quote(name)).Append(": ").Append(JsonText.Quote(value)).Append(",\n"); }
            Field("schema", "tfa-probe-0.2"); Field("pluginVersion", "0.25.2"); Field("scanScope", s.ScanScope); Field("capturedUtc", s.CapturedUtc.ToString("O"));
            Field("status", s.Status); Field("weaponId", s.WeaponId); Field("weaponName", s.WeaponName);
            Field("playerType", s.PlayerType); Field("handsType", s.HandsType); Field("inventoryControllerType", s.InventoryControllerType);
            Field("sessionContext", s.SessionContext);
            Field("currentHandsOperationType", s.CurrentHandsOperationType);
            b.Append("  \"inventoryOpened\": ").Append(JsonText.Bool(s.InventoryOpened)).Append(",\n");
            b.Append("  \"isAiming\": ").Append(JsonText.Bool(s.IsAiming)).Append(",\n");
            b.Append("  \"isTriggerPressed\": ").Append(JsonText.Bool(s.IsTriggerPressed)).Append(",\n");
            b.Append("  \"inventoryLocked\": ").Append(JsonText.Bool(s.InventoryLocked)).Append(",\n");
            Field("stagedItemId", state.StagedItemId); Field("stagedSlotPath", state.StagedSlotPath);
            Field("highlightedItemId", state.Selected()?.Item.Id ?? ""); Field("highlightedSlotPath", state.Current(state.Group)?.Slot.Path ?? "");
            Field("stageReason", state.StageReason);
            b.Append("  \"inventoryChangesEnabled\": ").Append(JsonText.Bool(installEnabled)).Append(",\n  \"installSessionBlocked\": ").Append(JsonText.Bool(installBlocked)).Append(",\n  \"truncated\": ").Append(JsonText.Bool(s.Truncated)).Append(",\n");
            b.Append("  \"traversedItems\": ").Append(s.TraversedItems).Append(",\n");
            b.Append("  \"excludedUnexaminedOrUnknown\": ").Append(s.ExcludedUnexamined).Append(",\n");
            b.Append("  \"excludedAssemblies\": ").Append(s.ExcludedAssemblies).Append(",\n");
            b.Append("  \"warnings\": [").Append(string.Join(",", s.Warnings.Select(JsonText.Quote))).Append("],\n");
            b.Append("  \"carriedCandidates\": [").Append(string.Join(",\n", s.Carried.Select(ItemJson))).Append("],\n");
            b.Append("  \"slots\": [");
            b.Append(string.Join(",\n", s.Slots.Select(vm => {
                SlotObservation slot = vm.Slot;
                return "{\"id\":" + JsonText.Quote(slot.Id) + ",\"path\":" + JsonText.Quote(slot.Path) + ",\"parentName\":" + JsonText.Quote(slot.ParentName) +
                    ",\"group\":" + JsonText.Quote(slot.Group.ToString()) + ",\"required\":" + JsonText.Bool(slot.Required) +
                    ",\"locked\":" + JsonText.Bool(slot.Locked) + ",\"installed\":" + (slot.Installed == null ? "null" : ItemJson(slot.Installed)) +
                    ",\"observedAllowIds\":[" + string.Join(",", slot.ExplicitFilterIds.OrderBy(x => x).Select(JsonText.Quote)) + "]" +
                    ",\"candidates\":[" + string.Join(",", vm.Candidates.Select(c => "{\"itemId\":" + JsonText.Quote(c.Item.Id) +
                        ",\"evidence\":" + JsonText.Quote(c.Evidence.ToString()) + ",\"detail\":" + JsonText.Quote(c.Detail) + "}")) + "]}";
            })));
            return b.Append("]\n}\n").ToString();
        }
        private static string ItemJson(ItemObservation i)
        {
            return "{\"id\":" + JsonText.Quote(i.Id) + ",\"templateId\":" + JsonText.Quote(i.TemplateId) +
                ",\"name\":" + JsonText.Quote(i.Name) + ",\"type\":" + JsonText.Quote(i.RuntimeType) +
                ",\"source\":" + JsonText.Quote(i.Location) + ",\"raidModdable\":" + JsonText.Bool(i.RaidModdable) +
                ",\"examined\":" + JsonText.Bool(i.Examined) + ",\"hasChildren\":" + JsonText.Bool(i.HasChildren) + "}";
        }
        private static string ScanTrace(RaidSnapshot snapshot)
        {
            var b = new StringBuilder("READ-ONLY INVENTORY SCAN CLASSIFICATION — 0.25.2\n");
            b.AppendLine("Scan calls native include/exclude filter predicates, but not inventory operations. Live attempts are recorded separately in install-probe.txt.");
            b.AppendLine("Scope: " + snapshot.ScanScope + "; scanned: " + snapshot.CapturedUtc.ToString("O"));
            b.AppendLine("Accessible carried storage including nested containers. Unsearched/unknown contents are excluded; non-attachment names and IDs are omitted.");
            b.AppendLine("At most 128 entry descriptions/root; counts can exceed descriptions. Inventory enumeration is bounded.");
            foreach (ScanRootObservation root in snapshot.ScanRoots)
            {
                b.AppendLine("\nROOT " + root.Root + ": " + root.Status);
                b.AppendLine("grids=" + root.Grids + " entries=" + root.Entries + " acceptedLooseMods=" + root.AcceptedMods +
                    " nonMods=" + root.NonMods + " unresolved=" + root.UnresolvedEntries + " unexaminedOrUnknown=" +
                    root.ExcludedUnexamined + " assembliesExcluded=" + root.ExcludedAssemblies + " inaccessibleContainers=" + root.ExcludedContainers);
                int index = 0;
                foreach (ScanEntryObservation entry in root.Details)
                    b.AppendLine("  ENTRY " + (++index) + " | raw=" + entry.EntryType + " | item=" + entry.ItemType + " | " + entry.Outcome);
                if (root.Entries > root.Details.Count) b.AppendLine("  [Entry-description cap reached]");
            }
            b.AppendLine("\nObserved loose mods=" + snapshot.Carried.Count + "; visited entries=" + snapshot.TraversedItems + "; truncated=" + snapshot.Truncated);
            foreach (string warning in snapshot.Warnings) b.AppendLine("WARNING: " + warning);
            return b.ToString();
        }
    }
}
