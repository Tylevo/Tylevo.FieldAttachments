using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tylevo.FieldAttachments.Core;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed class StorageGrid
    {
        public object NativeGrid=null!;
        public string Location="";
        public ScanRootObservation Root=null!;
        public object[] Items=Array.Empty<object>();
    }
    public sealed class CarriedStorageScan
    {
        public readonly List<StorageGrid> Grids=new List<StorageGrid>();
        public readonly List<ScanRootObservation> Roots=new List<ScanRootObservation>();
        public readonly List<string> Warnings=new List<string>();
        public int Entries;
        public bool Complete => Warnings.Count==0;
    }
    // Shared bounded discovery and live ownership proof. Queries never search/reveal or move items.
    public sealed class CarriedStorageReader
    {
        private readonly ReadAccess _read;
        public CarriedStorageReader(ReadAccess read) { _read=read; }
        public CarriedStorageScan Capture(object? controller)
        {
            var result=new CarriedStorageScan();
            try
            {
                object? equipment=_read.Get(_read.Get(controller,"Inventory"),"Equipment");
                if (!ReadAccess.IsKind(equipment,"EFT.InventoryLogic.InventoryEquipment")) throw new InvalidOperationException("Equipment unavailable.");
                object[] slots=Bounded(_read.Get(equipment,"Slots"),64);
                var containers=new HashSet<string>(StringComparer.Ordinal);
                var items=new HashSet<string>(StringComparer.Ordinal);
                foreach(string id in CarriedStoragePolicy.Roots)
                {
                    var root=new ScanRootObservation {Root=id}; result.Roots.Add(root);
                    object[] matching=slots.Where(s=>ReadAccess.Text(_read.Get(s,"ID"))==id).ToArray();
                    if (matching.Length!=1 || !ReadAccess.IsKind(matching[0],"EFT.InventoryLogic.Slot") ||
                        !ReferenceEquals(_read.Get(matching[0],"ParentItem"),equipment))
                        throw new InvalidOperationException("Exact "+id+" equipment slot unresolved.");
                    object? container=Contained(matching[0]);
                    if (container==null) { root.Status="no container equipped"; continue; }
                    if (!ReferenceEquals(_read.Get(_read.Get(container,"Parent"),"Container"),matching[0]))
                        throw new InvalidOperationException(id+" root ownership changed.");
                    root.Status="scanned";
                    Walk(container,id,root,controller,result,containers,items,0);
                }
            }
            catch(Exception e) { result.Warnings.Add("Carried storage unavailable: "+e.GetBaseException().Message); }
            return result;
        }
        private void Walk(object container,string location,ScanRootObservation root,object? controller,
            CarriedStorageScan result,HashSet<string> containers,HashSet<string> seen,int depth)
        {
            string id=ReadAccess.Text(_read.Get(container,"Id"));
            if (depth>8 || containers.Count>=128 || id.Length==0 || !containers.Add(id))
                throw new InvalidOperationException("Storage depth/count/identity limit reached.");
            if (!ReadAccess.IsKind(container,"EFT.InventoryLogic.CompoundItem") ||
                ReadAccess.IsKind(container,"EFT.InventoryLogic.Weapon") || ReadAccess.IsKind(container,"EFT.InventoryLogic.Mod"))
                throw new InvalidOperationException("Equipped storage is not a supported container.");
            if (!Accessible(controller,container))
            {
                root.ExcludedContainers++;
                if (depth==0) root.Status="unsearched/unknown contents excluded";
                return;
            }
            var nested=new List<object>();
            foreach(object grid in Bounded(_read.Get(container,"Grids"),32))
            {
                if (result.Grids.Count>=128 || !ReadAccess.IsKind(grid,"EFT.InventoryLogic.Grid") ||
                    !ReferenceEquals(_read.Get(grid,"ParentItem"),container))
                    throw new InvalidOperationException("Storage grid ownership/count unresolved.");
                object[] entries=Bounded(_read.Get(grid,"Items"),512);
                var known=new List<object>(); root.Grids++;
                foreach(object entry in entries)
                {
                    if (++result.Entries>2048) throw new InvalidOperationException("Storage item count limit reached.");
                    root.Entries++;
                    object? item=Unwrap(entry); string itemId=ReadAccess.Text(_read.Get(item,"Id"));
                    if (item==null || itemId.Length==0 || !seen.Add(itemId) ||
                        !ReadAccess.IsKind(_read.Get(item,"Parent"),"EFT.InventoryLogic.GridItemAddress") ||
                        !ReferenceEquals(_read.Get(_read.Get(item,"Parent"),"Container"),grid))
                        throw new InvalidOperationException("Storage item identity/parent missing, duplicated or changed.");
                    known.Add(item);
                    if (ReadAccess.IsKind(item,"EFT.InventoryLogic.CompoundItem") &&
                        !ReadAccess.IsKind(item,"EFT.InventoryLogic.Weapon") && !ReadAccess.IsKind(item,"EFT.InventoryLogic.Mod")) nested.Add(item);
                }
                result.Grids.Add(new StorageGrid {NativeGrid=grid,Location=location,Root=root,Items=known.ToArray()});
            }
            foreach(object child in nested)
                Walk(child,location+"/"+ReadAccess.Text(_read.Get(child,"Id")),root,controller,result,containers,seen,depth+1);
        }
        private bool Accessible(object? controller,object container)
        {
            if (!ReadAccess.IsKind(container,"EFT.InventoryLogic.SearchableItem")) return true;
            Type? contract=_read.FindType("EFT.ISearchController"), item=_read.FindType("EFT.InventoryLogic.SearchableItem");
            object? search=_read.Get(controller,"SearchController");
            if (contract==null || item==null || search==null || !contract.IsInstanceOfType(search)) return false;
            MethodInfo? searched=contract.GetMethod("IsSearched",new[] {item});
            MethodInfo? unknown=contract.GetMethod("ContainsUnknownItems",new[] {item});
            if (searched?.ReturnType!=typeof(bool) || unknown?.ReturnType!=typeof(bool)) return false;
            return Equals(searched.Invoke(search,new[] {container}),true) && Equals(unknown.Invoke(search,new[] {container}),false);
        }
        private object? Unwrap(object entry)
        {
            if (ReadAccess.IsKind(entry,"EFT.InventoryLogic.Item")) return entry;
            object? key=_read.Get(entry,"Key"), value=_read.Get(entry,"Value");
            return ReadAccess.IsKind(key,"EFT.InventoryLogic.Item") ? key : ReadAccess.IsKind(value,"EFT.InventoryLogic.Item") ? value : null;
        }
        private static object? Contained(object slot)
        {
            // Unlike an optional value, a missing/unreadable binding is not an empty equipment slot.
            PropertyInfo? member=slot.GetType().GetProperty("ContainedItem",BindingFlags.Public|BindingFlags.Instance);
            if (member==null) throw new InvalidOperationException("ContainedItem getter unavailable.");
            return member.GetValue(slot,null);
        }
        private static object[] Bounded(object? value,int limit)
        {
            if (!(value is IEnumerable sequence) || value is string) throw new InvalidOperationException("Storage collection unresolved.");
            var result=new List<object>();
            foreach(object? item in sequence)
            {
                if (item==null || result.Count>=limit) throw new InvalidOperationException("Storage collection invalid or capped.");
                result.Add(item);
            }
            return result.ToArray();
        }
    }
}
