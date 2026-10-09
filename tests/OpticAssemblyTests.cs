using System;
using System.Linq;
using Tylevo.FieldAttachments.Core;
using Tylevo.FieldAttachments.Runtime;

namespace EFT.InventoryLogic
{
    public class AssemblyTestItem { public string Id="",TemplateId=""; public object? Parent; public int PinLockState=0,StackObjectsCount=1;
        public bool RaidModdable=false; public AssemblyTestSlot[] Slots=Array.Empty<AssemblyTestSlot>(); }
    public class Mount : AssemblyTestItem { }
    public class OpticScope : AssemblyTestItem { }
    public class AssemblyTestSlot { public string ID="mod_scope"; public object? ParentItem; public object? ContainedItem {get;set;} public bool Required=false,Locked=false,Deleted=false; }
    public class AssemblyTestAddress { public object? Container; }
}
internal static partial class Program
{
    private sealed class AssemblyController { public bool Known=true; public bool Examined(EFT.InventoryLogic.AssemblyTestItem item)=>Known; }
    private static void TestOpticAssemblies()
    {
        var controller=new AssemblyController(); var reader=new AttachmentAssemblyReader(new ReadAccess());
        var mount=new EFT.InventoryLogic.Mount {Id="alpha",TemplateId="alpha-template",RaidModdable=true};
        var scope=new EFT.InventoryLogic.OpticScope {Id="razor",TemplateId="razor-template"};
        var childSlot=new EFT.InventoryLogic.AssemblyTestSlot {ParentItem=mount,ContainedItem=scope,Required=true};
        scope.Parent=new EFT.InventoryLogic.AssemblyTestAddress {Container=childSlot}; mount.Slots=new[]{childSlot};
        ItemObservation Observe() => new ItemObservation {Id=mount.Id,TemplateId=mount.TemplateId,NativeItem=mount,Name="ALPHA4 + Razor",
            RuntimeType=mount.GetType().FullName!,HasChildren=true,Assembly=reader.Capture(mount,controller),RaidModdable=true,Examined=true,
            Location="SecuredContainer",SourceGrid=controller,SourceAddress=mount};
        var assembled=Observe(); var binding=assembled.Assembly!;
        var slot=new SlotObservation {Group=AttachmentGroup.Optic,Path="gun/receiver/scope",Required=false,Locked=false,NativeSlot=new object()};
        Check(binding.Complete && AttachmentAssembly.Supports(AttachmentGroup.Optic,assembled),"Raid-removable mount supports an intact scope even when scope itself is not raid-removable");
        Check(CandidatePolicy.Build(slot,new[]{assembled},false,false,(s,i)=>true).Count==1,"One native-fitting assembled optic candidate");
        Check(CandidatePolicy.Build(slot,new[]{assembled},true,true,(s,i)=>false).Count==0,"Incompatible assembly hidden despite diagnostic settings");
        Check(CandidatePolicy.Build(slot,new[]{assembled},true,true,(s,i)=>null).Count==0,"Unknown native fit hidden");
        Check(AttachmentRequest.ScopeDenial(AttachmentAction.Install,slot,assembled)=="","Assembly installation authorized as a single root item");
        Check(!AttachmentAssembly.Supports(AttachmentGroup.Tactical,assembled),"Optic assembly scope does not broaden tactical/other categories");
        assembled.RaidModdable=false;
        Check(CandidatePolicy.Build(slot,new[]{assembled},false,false,(s,i)=>true).Count==0,"Fixed mounts do not become carried choices"); assembled.RaidModdable=true;
        var emptyMount=Observe();emptyMount.HasChildren=false;emptyMount.Assembly=null;
        Check(CandidatePolicy.Build(slot,new[]{emptyMount},false,false,(s,i)=>true).Count==0,"Bare optic mounts omitted");
        Check(binding.Same(reader.Capture(mount,controller)),"Unchanged assembly preserves child bindings");
        scope.Id="different"; Check(!binding.Same(reader.Capture(mount,controller)),"Child identity mutation invalidates click binding");scope.Id="razor";
        scope.PinLockState=1;Check(!AttachmentAssembly.Supports(AttachmentGroup.Optic,Observe()),"Pinned descendant withheld");scope.PinLockState=0;
        controller.Known=false;Check(!AttachmentAssembly.Supports(AttachmentGroup.Optic,Observe()),"Unexamined descendant withheld");controller.Known=true;
        childSlot.ContainedItem=null;Check(!AttachmentAssembly.Supports(AttachmentGroup.Optic,Observe()),"Incomplete required scope assembly withheld");childSlot.ContainedItem=scope;
        childSlot.Deleted=true;Check(!AttachmentAssembly.Supports(AttachmentGroup.Optic,Observe()),"Deleted child slot withheld");childSlot.Deleted=false;
        childSlot.ParentItem=scope;Check(!reader.Capture(mount,controller).Complete,"Wrong child parent fails closed");childSlot.ParentItem=mount;
        scope.Parent=new EFT.InventoryLogic.AssemblyTestAddress {Container=mount};Check(!reader.Capture(mount,controller).Complete,"Wrong child address fails closed");
        scope.Parent=new EFT.InventoryLogic.AssemblyTestAddress {Container=childSlot};
        mount.Slots=new[]{childSlot,childSlot};Check(!reader.Capture(mount,controller).Complete,"Duplicate child slot fails closed");mount.Slots=new[]{childSlot};

        assembled=Observe(); slot.Installed=assembled;
        var snapshot=new RaidSnapshot();snapshot.Slots.Add(new SlotViewModel {Slot=slot});
        var leaf=new SlotViewModel {Slot=new SlotObservation {Group=AttachmentGroup.Optic,NativeSlot=childSlot,Path="gun/alpha/scope",Required=false,Locked=false}};
        snapshot.Slots.Add(leaf);AttachmentAssembly.CollapseOpticPositions(snapshot);
        var state=new SelectionState();state.ReplaceSnapshot(snapshot);
        Check(state.GroupSlots(AttachmentGroup.Optic).Count==1 && state.Current(AttachmentGroup.Optic)!.Slot==slot,"Mounted scope suppressed; removable assembly is the sole optic position");
        slot.HiddenInQuickSwap=false;leaf.Slot.HiddenInQuickSwap=false;assembled.RaidModdable=false;
        AttachmentAssembly.CollapseOpticPositions(snapshot);
        Check(slot.HiddenInQuickSwap && !leaf.Slot.HiddenInQuickSwap,"Fixed parent stays installed; its existing accessible child mount remains available");
        assembled.RaidModdable=true;slot.HiddenInQuickSwap=false;

        var fixture=new ReplacementFixture(AttachmentGroup.Optic);
        RaidSnapshot Bound(bool removed=false) {var s=fixture.Snapshot(removed);s.Slots[0].Candidates[0].Item=Observe();return s;}
        var initial=Bound();string path=initial.Slots[0].Slot.Path;
        Check(AttachmentRequest.Create(AttachmentAction.Replace,initial,AttachmentGroup.Optic,path,mount.Id,out var request)=="","Replacement binds assembled incoming root");
        Check(request!.Validate(Bound())=="","Fresh unchanged assembly passes request validation");
        scope.TemplateId="other";Check(request.Validate(Bound()).Length>0,"Replacing attached scope after click invalidates whole request");scope.TemplateId="razor-template";
        Check(request.AfterRemoval(Bound(true),out var installation)=="" && installation?.Action==AttachmentAction.Install,"Direct swap continuation preserves intact incoming assembly");
        scope.Id="changed";Check(request.AfterRemoval(Bound(true),out _).Length>0,"Child changes between replacement legs block install");scope.Id="razor";
        // Exercise the actual native adapter's post-simulation/completion observer,
        // with fake native-shaped objects and no operation or transaction invoked.
        var probe=new NativeInstallProbe(new ReadAccess(),System.IO.Path.GetTempPath());
        void Set(string key,object value)=>typeof(NativeInstallProbe).GetField(key,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(probe,value);
        bool Intact()=>(bool)typeof(NativeInstallProbe).GetMethod("IdentityIntact",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(probe,null)!;
        Set("_item",mount);Set("_itemId",mount.Id);Set("_templateId",mount.TemplateId);Set("_stack",1);
        Set("_hasChildren",true);Set("_assemblyController",controller);Set("_assembly",reader.Capture(mount,controller));
        Check(Intact(),"Native completion observer accepts unchanged child structure");
        childSlot.ContainedItem=null;Check(!Intact(),"Native completion cannot report success with lost child");childSlot.ContainedItem=scope;
        scope.TemplateId="changed";Check(!Intact(),"Native completion catches replaced child template");scope.TemplateId="razor-template";
        mount.Slots=new[]{childSlot,new EFT.InventoryLogic.AssemblyTestSlot {ID="extra",ParentItem=mount}};
        Check(!Intact(),"Native completion catches even added empty child slot");mount.Slots=new[]{childSlot};
        Check(Intact(),"Restored child structure matches original observer again");
    }
}
