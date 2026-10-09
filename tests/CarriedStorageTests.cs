using System;
using System.Collections.Generic;
using System.Linq;
using Tylevo.FieldAttachments.Core;
using Tylevo.FieldAttachments.Runtime;

// Small native-shape fixtures for the reflection reader. No game assembly loaded or operation invoked.
namespace EFT
{
    public interface ISearchController
    {
        bool IsSearched(InventoryLogic.SearchableItem item);
        bool ContainsUnknownItems(InventoryLogic.SearchableItem item);
    }
}
namespace EFT.InventoryLogic
{
    public class Item { public string Id=""; public object? Parent; }
    public class CompoundItem : Item { public Grid[] Grids=Array.Empty<Grid>(); }
    public class InventoryEquipment : CompoundItem { public Slot[] Slots=Array.Empty<Slot>(); }
    public class SearchableItem : CompoundItem { public bool Searched=true, Unknown; }
    public class Mod : CompoundItem { }
    public class Weapon : CompoundItem { }
    public class Slot { public string ID=""; public object? ParentItem; public Item? ContainedItem {get;set;} }
    public class Grid
    {
        public object? ParentItem; public readonly List<Item> Items=new List<Item>();
        public bool Compatible=true, Free=true; public int Lookups;
        public GridItemAddress? FindLocationForItem(Item item)
        { Lookups++; return Compatible && Free ? new GridItemAddress {Container=this} : null; }
    }
    public class GridItemAddress { public object? Container; }
}
internal static partial class Program
{
    private sealed class StorageSearch : EFT.ISearchController
    {
        public bool IsSearched(EFT.InventoryLogic.SearchableItem item) => item.Searched;
        public bool ContainsUnknownItems(EFT.InventoryLogic.SearchableItem item) => item.Unknown;
    }
    private sealed class StorageInventory { public EFT.InventoryLogic.InventoryEquipment Equipment=new EFT.InventoryLogic.InventoryEquipment(); }
    private sealed class StorageController
    {
        public StorageInventory Inventory=new StorageInventory();
        public EFT.ISearchController? SearchController=new StorageSearch();
    }
    private sealed class StorageFixture
    {
        public readonly StorageController Controller=new StorageController();
        public readonly EFT.InventoryLogic.SearchableItem[] Roots=new EFT.InventoryLogic.SearchableItem[4];
        public readonly CarriedStorageReader Reader=new CarriedStorageReader(new ReadAccess());
        public StorageFixture()
        {
            var equipment=Controller.Inventory.Equipment;
            equipment.Slots=CarriedStoragePolicy.Roots.Select((id,i)=>
            {
                var slot=new EFT.InventoryLogic.Slot {ID=id,ParentItem=equipment};
                var item=new EFT.InventoryLogic.SearchableItem {Id="container"+i,Parent=new EFT.InventoryLogic.GridItemAddress {Container=slot}};
                item.Grids=new[] {new EFT.InventoryLogic.Grid {ParentItem=item}};
                slot.ContainedItem=Roots[i]=item; return slot;
            }).ToArray();
        }
        public static void Put(EFT.InventoryLogic.CompoundItem container,EFT.InventoryLogic.Item item)
        {
            var grid=container.Grids[0]; grid.Items.Add(item);
            item.Parent=new EFT.InventoryLogic.GridItemAddress {Container=grid};
        }
        public CarriedStorageScan Read() => Reader.Capture(Controller);
    }
    private static void TestCarriedStorage()
    {
        foreach(string root in CarriedStoragePolicy.Roots)
            Check(CarriedStoragePolicy.Allows(root) && CarriedStoragePolicy.Allows(root+"/case"),"Carried policy permits direct and nested "+root);
        foreach(string root in new[] {"Stash","Loot","FirstPrimaryWeapon","SecuredContainerFake","BackpackFake/x","Held",""})
            Check(!CarriedStoragePolicy.Allows(root),"Carried policy excludes "+root);
        var f=new StorageFixture();
        for(int i=0;i<4;i++) StorageFixture.Put(f.Roots[i],new EFT.InventoryLogic.Mod {Id="part"+i});
        var scan=f.Read();
        Check(scan.Complete && scan.Entries==4 && scan.Grids.Select(g=>g.Location).SequenceEqual(CarriedStoragePolicy.Roots),"All carried roots have deterministic placement order including secure storage");
        Check(scan.Grids.All(g=>g.Items.Length==1 && ReferenceEquals(((EFT.InventoryLogic.Item)g.Items[0]).Parent is EFT.InventoryLogic.GridItemAddress a ? a.Container : null,g.NativeGrid)),"Discovery retains exact grid/item ownership");
        var nested=new EFT.InventoryLogic.SearchableItem {Id="case"}; nested.Grids=new[] {new EFT.InventoryLogic.Grid {ParentItem=nested}};
        StorageFixture.Put(f.Roots[3],nested); StorageFixture.Put(nested,new EFT.InventoryLogic.Mod {Id="nested-part"});
        scan=f.Read();
        Check(scan.Complete && scan.Grids.Any(g=>g.Location=="SecuredContainer/case" && g.Items.Any(i=>ReferenceEquals(i,nested.Grids[0].Items[0]))),"Secure nested container candidates retain the exact source path");
        nested.Searched=false; scan=f.Read();
        Check(scan.Complete && scan.Roots[3].ExcludedContainers==1 && scan.Grids.All(g=>g.Location!="SecuredContainer/case"),"Unsearched nested contents are neither candidates nor destinations");
        nested.Searched=true; nested.Unknown=true;
        Check(f.Read().Grids.All(g=>g.Location!="SecuredContainer/case"),"Previously searched container with unknown items stays excluded");
        f.Roots[1].Searched=false; scan=f.Read();
        Check(scan.Complete && scan.Grids.All(g=>g.Location!="TacticalVest") && scan.Grids.Any(g=>g.Location=="Pockets"),"Unsearched rig is excluded without blocking unrelated accessible storage");
        f.Controller.SearchController=null;
        Check(f.Read().Grids.Count==0,"Missing search controller cannot expose searchable contents");
        f=new StorageFixture(); f.Controller.Inventory.Equipment.Slots[2].ContainedItem=null;
        Check(f.Read().Complete && f.Read().Grids.Count==3,"Absent backpack leaves other equipped storage usable");
        f=new StorageFixture(); f.Roots[0].Grids[0].ParentItem=new object();
        Check(!f.Read().Complete,"Wrong grid owner fails the inventory scan closed");
        f=new StorageFixture(); StorageFixture.Put(f.Roots[0],new EFT.InventoryLogic.Mod {Id="part"}); f.Roots[0].Grids[0].Items[0].Parent=new object();
        Check(!f.Read().Complete,"Wrong item parent cannot establish carried ownership");
        f=new StorageFixture(); StorageFixture.Put(f.Roots[0],new EFT.InventoryLogic.Mod {Id="part"}); StorageFixture.Put(f.Roots[1],new EFT.InventoryLogic.Mod {Id="part"});
        Check(!f.Read().Complete,"Duplicate item IDs invalidate the scan");
        f=new StorageFixture(); f.Roots[0].Parent=new object();
        Check(!f.Read().Complete,"Detached equipment container invalidates its storage");
        f=new StorageFixture(); f.Controller.Inventory.Equipment.Slots=f.Controller.Inventory.Equipment.Slots.Concat(new[] {f.Controller.Inventory.Equipment.Slots[0]}).ToArray();
        Check(!f.Read().Complete,"Duplicate equipment roots fail closed");
        f=new StorageFixture();
        for(int i=0;i<513;i++) StorageFixture.Put(f.Roots[0],new EFT.InventoryLogic.Item {Id="i"+i});
        Check(!f.Read().Complete,"Oversized grid enumeration cannot silently authorize a partial scan");
        f=new StorageFixture(); EFT.InventoryLogic.CompoundItem parent=f.Roots[2];
        for(int i=0;i<9;i++)
        {
            var child=new EFT.InventoryLogic.CompoundItem {Id="nested"+i}; child.Grids=new[] {new EFT.InventoryLogic.Grid {ParentItem=child}};
            StorageFixture.Put(parent,child); parent=child;
        }
        Check(!f.Read().Complete,"Nested depth cap fails closed");
        f=new StorageFixture(); var weapon=new EFT.InventoryLogic.Weapon {Id="sparegun"};
        weapon.Grids=new[] {new EFT.InventoryLogic.Grid {ParentItem=weapon}}; StorageFixture.Put(f.Roots[2],weapon);
        Check(f.Read().Complete && f.Read().Grids.Count==4,"Carried weapons are not traversed for attachment sources");
        var click=new ClickFixture(); var original=click.Snapshot(); var item=original.Slots[0].Candidates[0].Item;
        item.Location="SecuredContainer/case"; item.SourceGrid=new object(); item.SourceAddress=new object();
        Check(AttachmentRequest.Create(AttachmentAction.Install,original,AttachmentGroup.Tactical,"gun/mount/tactical","real-device",out var request)=="","Secure-container click can bind a verified source");
        var fresh=click.Snapshot(); var now=fresh.Slots[0].Candidates[0].Item;
        now.Location=item.Location; now.SourceGrid=item.SourceGrid; now.SourceAddress=item.SourceAddress;
        Check(request!.Validate(fresh)=="","Unchanged secure source preserves click identity");
        now.SourceGrid=new object(); Check(request.Validate(fresh)!="","Moving within the same root to another grid invalidates the click");
        now.SourceGrid=item.SourceGrid; now.SourceAddress=new object();
        Check(request.Validate(fresh)!="","Changing source address within the same grid invalidates the click");
        var state=new SelectionState(); state.ReplaceSnapshot(original,false,"initial"); state.NextGroup(2); state.Stage("test");
        Check(state.StagedItemId=="real-device","Storage regression fixture arms the tactical item");
        state.ReplaceSnapshot(fresh,true,"moved within grid");
        Check(state.StagedItemId=="","Source address change invalidates keyboard arming too");
        f=new StorageFixture(); scan=f.Read();
        var probe=new NativeInstallProbe(new ReadAccess(),System.IO.Path.GetTempPath());
        var type=typeof(NativeInstallProbe);
        type.GetField("_item",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(probe,new EFT.InventoryLogic.Mod {Id="installed"});
        var find=type.GetMethod("FindStorageAddress",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        object? Find() => find.Invoke(probe,new object[] {scan.Grids.ToArray(),typeof(EFT.InventoryLogic.Item)});
        for(int i=0;i<3;i++) f.Roots[i].Grids[0].Free=false;
        Check(Find() is EFT.InventoryLogic.GridItemAddress secure && ReferenceEquals(secure.Container,f.Roots[3].Grids[0]),"Uninstall finds secure-container space after other roots are full");
        Check(scan.Grids.All(g=>((EFT.InventoryLogic.Grid)g.NativeGrid).Items.Count==0),"Native space lookup fixture leaves inventory unchanged");
        f.Roots[3].Grids[0].Compatible=false;
        Check(Find()==null,"No compatible space rejects uninstall without a fallback drop");
        f.Roots[1].Grids[0].Free=true;
        Check(Find() is EFT.InventoryLogic.GridItemAddress rig && ReferenceEquals(rig.Container,f.Roots[1].Grids[0]),"Uninstall uses rig space when pockets are full");
    }
}
