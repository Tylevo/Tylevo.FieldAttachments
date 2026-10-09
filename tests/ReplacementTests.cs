using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private sealed class ReplacementFixture
    {
        public readonly object Player=new object(), Weapon=new object(), Controller=new object(), Slot=new object();
        public readonly object OldItem=new object(), NewItem=new object(), Grid=new object(), Address=new object();
        public readonly AttachmentGroup Group;
        public ReplacementFixture(AttachmentGroup group=AttachmentGroup.Tactical) { Group=group; }
        public RaidSnapshot Snapshot(bool removed=false)
        {
            string type=Group==AttachmentGroup.Optic ? "OpticScope" : Group==AttachmentGroup.Muzzle ? "Silencer" :
                Group==AttachmentGroup.Underbarrel ? "Foregrip" : "TacticalCombo";
            ItemObservation Item(string id,object native,string location) => new ItemObservation {Id=id,TemplateId=id+"-template",Name=id,
                NativeItem=native,RuntimeType="EFT.InventoryLogic."+type,Location=location,Examined=true,RaidModdable=true,SourceGrid=Grid,SourceAddress=Address};
            var old=Item("old",OldItem,removed ? "Backpack" : "Held");
            var next=Item("new",NewItem,"SecuredContainer");
            var s=new RaidSnapshot {Player=Player,Weapon=Weapon,Controller=Controller,WeaponId="gun"};
            var vm=new SlotViewModel {Slot=new SlotObservation {Group=Group,NativeSlot=Slot,Path="gun/side-2/leaf",Required=false,Locked=false,Installed=removed ? null : old}};
            vm.Candidates.Add(new CandidateObservation {Item=next,Evidence=CandidateEvidence.NativeFilterPass});
            s.Slots.Add(vm);s.Carried.Add(next);if(removed)s.Carried.Add(old);return s;
        }
        public AttachmentRequest Request()
        {
            Check(AttachmentRequest.Create(AttachmentAction.Replace,Snapshot(),Group,"gun/side-2/leaf","new",out var request)=="","Replacement binds both real items and exact mount");
            return request!;
        }
    }
    private static void NativeSuccess(InstallSession native)
    { Check(native.TryBegin(),"Native move begins once");native.MarkSubmitted();native.Finish("verified",false,true); }
    private static void TestReplacements()
    {
        foreach(AttachmentGroup group in Enum.GetValues(typeof(AttachmentGroup)))
        {
            var f=new ReplacementFixture(group);var request=f.Request();var original=f.Snapshot();
            Check(request.Validate(original)=="" && request.Removal?.Action==AttachmentAction.Uninstall && request.Removal.ItemId=="old","Replacement validates both identities for "+group);
            Check(AttachmentRequest.ChoiceAction(original.Slots[0].Slot)==AttachmentAction.Replace &&
                AttachmentRequest.ChoiceAction(f.Snapshot(true).Slots[0].Slot)==AttachmentAction.Install,"Occupied and empty choices stay distinct for "+group);
            Check(request.AfterRemoval(f.Snapshot(true),out var install)=="" && install!.Action==AttachmentAction.Install &&
                install.ItemId=="new" && install.SlotPath=="gun/side-2/leaf","Same chosen item installs into same handguard position for "+group);
            Check(request.AfterRemoval(original,out _)!="","Second move cannot run while old part is still installed");
            foreach(string change in new[] {"old children","old required","old unexamined","old raidmod","new children","new unknown filter","same item","old identity","new identity","old template","no old","new moved"})
            {
                var s=f.Snapshot();var slot=s.Slots[0].Slot;var next=s.Slots[0].Candidates[0];
                switch(change)
                {
                    case "old children":slot.Installed!.HasChildren=true;break;
                    case "old required":slot.Required=true;break;
                    case "old unexamined":slot.Installed!.Examined=null;break;
                    case "old raidmod":slot.Installed!.RaidModdable=false;break;
                    case "new children":next.Item.HasChildren=true;break;
                    case "new unknown filter":next.Evidence=CandidateEvidence.Unverified;break;
                    case "same item":next.Item.NativeItem=slot.Installed!.NativeItem;break;
                    case "old identity":slot.Installed!.NativeItem=new object();break;
                    case "new identity":next.Item.NativeItem=new object();break;
                    case "old template":slot.Installed!.TemplateId="different";break;
                    case "no old":slot.Installed=null;break;
                    case "new moved":next.Item.SourceAddress=new object();break;
                }
                Check(request.Validate(s).Length!=0,"Before-removal replacement rejects "+change+" for "+group);
            }
            foreach(string change in new[] {"new identity","new location","new address","new filter","old missing","old duplicate","old identity","old template","old location","old address","weapon","controller","player","slot","occupied","truncated"})
            {
                var s=f.Snapshot(true);var next=s.Slots[0].Candidates[0];
                switch(change)
                {
                    case "new identity":next.Item.NativeItem=new object();break;
                    case "new location":next.Item.Location="Pockets";break;
                    case "new address":next.Item.SourceAddress=new object();break;
                    case "new filter":next.Evidence=CandidateEvidence.Unverified;break;
                    case "old missing":s.Carried.RemoveAt(1);break;
                    case "old duplicate":s.Carried.Add(s.Carried[1]);break;
                    case "old identity":s.Carried[1].NativeItem=new object();break;
                    case "old template":s.Carried[1].TemplateId="changed";break;
                    case "old location":s.Carried[1].Location="Stash";break;
                    case "old address":s.Carried[1].SourceAddress=null;break;
                    case "weapon":s.Weapon=new object();break;
                    case "controller":s.Controller=new object();break;
                    case "player":s.Player=new object();break;
                    case "slot":s.Slots[0].Slot.NativeSlot=new object();break;
                    case "occupied":s.Slots[0].Slot.Installed=s.Carried[1];break;
                    case "truncated":s.Truncated=true;break;
                }
                Check(request.AfterRemoval(s,out var denied).Length!=0 && denied==null,"After-removal replacement rejects "+change+" for "+group);
            }
        }
        var fixture=new ReplacementFixture();var bound=fixture.Request();
        var native=new InstallSession();var chain=new AttachmentReplacement();
        Check(chain.Begin(bound,native) && !chain.Begin(bound,native),"Duplicate replacement cannot overwrite pending plan");
        native.TryBegin();native.MarkSubmitted();chain.Observe(native,1);
        Check(chain.Phase==ReplacementPhase.Removing && chain.PrepareInstall(fixture.Snapshot(true),native,1,out _)!="","Removal still pending cannot authorize second leg");
        native.Finish("removed and observed",false,true);chain.Observe(native,2);
        Check(chain.Phase==ReplacementPhase.WaitingForIdle,"Only verified removal unlocks the readiness wait");
        Check(chain.PrepareInstall(fixture.Snapshot(true),native,3,out var nextInstall)=="" && nextInstall!=null && chain.Phase==ReplacementPhase.Installing,"Fresh observed empty slot authorizes one install");
        Check(chain.PrepareInstall(fixture.Snapshot(true),native,3,out _)!="","Second leg authorization cannot be consumed twice");
        NativeSuccess(native);chain.Observe(native,4);
        Check(!chain.Pending && chain.Phase==ReplacementPhase.Succeeded && chain.Status.Contains("REPLACED"),"Replacement completes only after verified install");
        foreach(string failure in new[] {"no storage","rejected","unknown","late success","extra operation","idle timeout","cancel removing","cancel waiting","install rejected","install unknown","changed source"})
        {
            native=new InstallSession();chain=new AttachmentReplacement();chain.Begin(bound,native);
            if(failure=="cancel removing") { chain.Stop("closed");NativeSuccess(native);chain.Observe(native,2); }
            else if(failure=="no storage") { native.TryBegin();native.Finish("no compatible carried space",false);chain.Observe(native,2); }
            else if(failure=="rejected") { native.TryBegin();native.MarkSubmitted();native.Finish("native reject",false);chain.Observe(native,2); }
            else if(failure=="unknown" || failure=="late success")
            {
                native.TryBegin();native.MarkSubmitted();native.TimedOut();chain.Observe(native,2);
                if(failure=="late success") { native.Finish("late",false,true);chain.Observe(native,3); }
            }
            else
            {
                NativeSuccess(native);chain.Observe(native,2);
                if(failure=="idle timeout")chain.Observe(native,10);
                else if(failure=="cancel waiting")chain.Stop("weapon switched");
                else if(failure=="extra operation") { NativeSuccess(native);chain.Observe(native,3); }
                else if(failure=="changed source")
                { var s=fixture.Snapshot(true);s.Slots[0].Candidates.Clear();chain.PrepareInstall(s,native,3,out _); }
                else
                {
                    chain.PrepareInstall(fixture.Snapshot(true),native,3,out _);native.TryBegin();native.MarkSubmitted();
                    native.Finish("second leg failed",failure=="install unknown");chain.Observe(native,4);
                }
            }
            Check(!chain.Pending && chain.Phase==ReplacementPhase.Stopped,"Failure stops replacement with no second attempt: "+failure);
            Check(chain.PrepareInstall(fixture.Snapshot(true),native,12,out var denied)!="" && denied==null,"Stopped replacement cannot submit after "+failure);
        }
        Check(AttachmentRequest.ScopeDenial((AttachmentAction)999,fixture.Snapshot().Slots[0].Slot,fixture.Snapshot().Carried[0])!="","Unknown action cannot authorize a replacement");
    }
}
