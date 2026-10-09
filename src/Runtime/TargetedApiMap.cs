using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Tylevo.FieldAttachments.Core;

namespace Tylevo.FieldAttachments.Runtime
{
    // Exports metadata only. No Invoke, Harmony hooks, native validation, simulation or inventory operations.
    public sealed class TargetedApiMap
    {
        private readonly ReadAccess _read;
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private const int MaxMethods = 140, MaxChars = 1800000, MaxDepth = 2;
        public TargetedApiMap(ReadAccess read) { _read = read; }
        private sealed class Target
        {
            public MethodBase Method = null!;
            public string Reason = "";
            public int Depth;
        }
        public string Write(RaidSnapshot snapshot, bool includeIl)
        {
            var text = new StringBuilder("FIELD ATTACHMENTS 0.2.1 — TARGETED LOCAL API MAP\n");
            text.AppendLine("Metadata/IL only; not a trace, not method execution, not proof of safe exchanges.");
            text.AppendLine("Seeds prioritize 0.1.2 missing execution callbacks and add/remove-mod operations. Not all methods or Harmony patches are represented.");
            text.AppendLine("Referenced callees are exported only within selected game type families, max depth 2.");
            text.AppendLine("Caps: 140 methods; 1000 instructions/body; 1.8M characters; ~6 second soft budget. Partial output is marked.");
            text.AppendLine("Snapshot hands state: " + snapshot.CurrentHandsOperationType + ". Virtual overrides/patches may change runtime behavior.\n");
            var queue = new Queue<Target>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            void Add(MethodBase m, string reason, int depth)
            {
                string key;
                try { key = m.Module.ModuleVersionId + ":" + m.MetadataToken + ":" + (m.DeclaringType?.FullName ?? ""); }
                catch { return; }
                if (!seen.Add(key)) return;
                queue.Enqueue(new Target { Method = m, Reason = reason, Depth = depth });
            }
            void Seed(string typeName, params string[] names)
            {
                Type? t = _read.FindType(typeName);
                if (t == null) { text.AppendLine("UNRESOLVED TYPE " + typeName); return; }
                foreach (string name in names)
                {
                    var found = new List<MethodBase>();
                    for (Type? current = t; current != null && found.Count == 0; current = current.BaseType)
                        found.AddRange(name == ".ctor" ? current.GetConstructors(Flags).Cast<MethodBase>() : current.GetMethods(Flags).Where(m => m.Name == name).Cast<MethodBase>());
                    if (found.Count == 0) text.AppendLine("UNRESOLVED METHOD " + typeName + "::" + name);
                    foreach (MethodBase m in found.OrderBy(IlText.Signature)) Add(m, "seed " + typeName + "::" + name, 0);
                }
            }
            // Promote the exact missing callbacks/animation operations observed in
            // the 0.1.2 capture. Do not traverse every quest/preset constructor.
            Seed("EFT.Player+SinglePlayerInventoryController", "method_43", "ExecuteAsync");
            Seed("EFT.Player+PlayerOwnerInventoryController", "Execute");
            Seed("EFT.Player+FirearmController+AddModOperation", "Start", "Reset");
            Seed("EFT.Player+FirearmController+RemoveModOperation", "Start", "Reset");
            Seed("EFT.Player+FirearmController+UtilityOperation", "Start", "Reset");
            Seed("EFT.InventoryLogic.ItemController+CG_TryRunNetworkTransaction", "method_0");
            Seed("EFT.InventoryLogic.ItemController+CG_Execute", "method_0");
            Seed("EFT.InventoryLogic.Operations.AbstractAsyncOperation`1", "Execute", "FinalizeOperation");
            Seed("EFT.InventoryLogic.Operations.MoveOperation", "Execute", "ExecuteInternal", "Dispose");
            Seed("EFT.InventoryLogic.ItemManipulator", "Move", "MovePathCheck");
            Seed("EFT.InventoryLogic.ItemController", "Execute", "TryRunNetworkTransaction", "RunNetworkTransaction");
            Seed("EFT.InventoryLogic.ItemFilterExtension", "CheckItemFilter", "CheckItem", "CanAccept");
            Seed("EFT.InventoryLogic.ItemFilter", "CheckItemFilter", "CheckItemExcludedFilter", "CheckItem");
            Seed("EFT.InventoryLogic.Slot", "CreateItemAddress");
            Seed("EFT.Player+FirearmController", "CanExecute", "CanExecuteWithoutAnimation", "ExamineWeapon");
            Seed("EFT.Player+FirearmController+Idling", "Execute", "ExamineWeapon");
            var watch = Stopwatch.StartNew();
            var typeSummaries = new HashSet<Type>();
            int count = 0;
            while (queue.Count > 0)
            {
                if (count >= MaxMethods || text.Length >= MaxChars || watch.ElapsedMilliseconds > 6000)
                { text.AppendLine("\n[EXPORT CAP REACHED; remaining methods listed below; map is partial]"); break; }
                Target target = queue.Dequeue(); MethodBase m = target.Method;
                count++;
                if (m.DeclaringType != null && typeSummaries.Add(m.DeclaringType))
                {
                    text.AppendLine("\nTYPE " + m.DeclaringType.FullName + " | " + m.Module.Assembly.GetName().Name);
                    FieldInfo[] fields = m.DeclaringType.GetFields(Flags);
                    foreach (FieldInfo field in fields.Take(24)) text.AppendLine("  FIELD " + IlText.TypeName(field.FieldType) + " " + field.Name);
                    if (fields.Length > 24) text.AppendLine("  [Field summary capped; not a complete type dump]");
                }
                text.AppendLine("\nMETHOD " + count + " | depth=" + target.Depth + " | " + target.Reason);
                text.AppendLine(IlText.Signature(m));
                if (!includeIl) { text.AppendLine("  [IL disabled by configuration]"); continue; }
                var references = new List<MethodBase>();
                string dump = IlText.Dump(m, 1000, references);
                int remaining = MaxChars - text.Length;
                if (dump.Length > remaining)
                {
                    text.Append(dump.Substring(0, Math.Max(0, remaining)));
                    text.AppendLine("\n[TEXT CAP REACHED INSIDE METHOD; body/map partial]");
                    break;
                }
                text.Append(dump);
                if (target.Depth >= MaxDepth) continue;
                // The async wrapper does not contain the awaited operation's actual implementation.
                foreach (MethodBase body in IlText.StateMachineBodies(m))
                    Add(body, "state machine for " + m.DeclaringType?.FullName + "::" + m.Name, target.Depth + 1);
                foreach (MethodBase callee in references)
                {
                    if (!Follow(callee)) continue;
                    Add(callee, "referenced by " + m.DeclaringType?.FullName + "::" + m.Name, target.Depth + 1);
                }
            }
            text.AppendLine("\nExported methods=" + count + "; queued=" + queue.Count + "; elapsedMs=" + watch.ElapsedMilliseconds);
            foreach (Target pending in queue.Take(80)) text.AppendLine("NOT EXPORTED " + IlText.Signature(pending.Method));
            if (queue.Count > 80) text.AppendLine("[Remaining-method listing also capped]");
            return text.ToString();
        }
        private static bool Follow(MethodBase method)
        {
            string? assembly = method.Module.Assembly.GetName().Name;
            if (assembly != "Assembly-CSharp") return false;
            string n = method.DeclaringType?.FullName ?? "";
            if (n.Contains("Quest") || n.Contains("Wishlist") || n.Contains("Ragfair")) return false;
            if (method.Name == "ConvertOperationResultToOperation") return false; // already exported; broad constructor fanout
            if (method.Name == ".ctor") return false; // errors/constructors dominate the old breadth-first cap
            // Getters other than these supply data, not the missing operation lifecycle. Their signatures remain in caller IL.
            if (method.Name.StartsWith("get_", StringComparison.Ordinal) && method.Name != "get_Locked" && method.Name != "get_ItemInHands") return false;
            return n.StartsWith("EFT.InventoryLogic.", StringComparison.Ordinal) ||
                n.StartsWith("EFT.Player+SinglePlayerInventoryController", StringComparison.Ordinal) ||
                n.StartsWith("EFT.Player+PlayerInventoryController", StringComparison.Ordinal) ||
                n.StartsWith("EFT.Player+PlayerOwnerInventoryController", StringComparison.Ordinal) ||
                n.StartsWith("EFT.Player+FirearmController+", StringComparison.Ordinal);
        }
    }
}
