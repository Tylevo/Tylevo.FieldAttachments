using System;
using System.Collections.Generic;
using System.Reflection;
using Tylevo.FieldAttachments.Core;
using Tylevo.FieldAttachments.Runtime;

internal static partial class Program
{
    private sealed class ClickFixture
    {
        public readonly object Player=new object(), Weapon=new object(), Controller=new object(), Slot=new object(), ItemRef=new object();
        public RaidSnapshot Snapshot(bool installed=false)
        {
            var s=new RaidSnapshot {Player=Player,Weapon=Weapon,Controller=Controller,WeaponId="gun"};
            var item=new ItemObservation {Id="real-device",TemplateId="device-template",Name="WMX200",NativeItem=ItemRef,
                RuntimeType="EFT.InventoryLogic.TacticalCombo",Location=installed ? "Held" : "Pockets",Examined=true,RaidModdable=true};
            var slot=new SlotViewModel {Slot=new SlotObservation {NativeSlot=Slot,Path="gun/mount/tactical",Group=AttachmentGroup.Tactical,
                Required=false,Locked=false,Installed=installed ? item : null}};
            if (!installed) { slot.Candidates.Add(new CandidateObservation {Item=item,Evidence=CandidateEvidence.NativeFilterPass}); s.Carried.Add(item); }
            s.Slots.Add(slot); return s;
        }
    }
    private static void TestClickActions()
    {
        var f=new ClickFixture();
        string Create(RaidSnapshot s, AttachmentAction a, out AttachmentRequest? request) =>
            AttachmentRequest.Create(a,s,AttachmentGroup.Tactical,"gun/mount/tactical","real-device",out request);
        Check(Create(f.Snapshot(),AttachmentAction.Install,out var install)=="" && install!=null,"Explicit install click binds the real candidate without arming");
        Check(Create(f.Snapshot(true),AttachmentAction.Uninstall,out var remove)=="" && remove!=null,"Uninstall click binds the installed identity without a carried candidate");
        Check(install!.Validate(f.Snapshot())=="" && remove!.Validate(f.Snapshot(true))=="","Fresh identical install/uninstall observations remain valid");
        foreach (string change in new[] {"player","weapon","controller","weapon id","slot ref","item ref","template","location","locked","required","children","unexamined","raidmod","incomplete","missing","duplicate","occupied","filter","empty template","duplicate slot"})
        {
            var s=f.Snapshot(); var slot=s.Slots[0]; var item=slot.Candidates[0].Item;
            switch(change)
            {
                case "player": s.Player=new object(); break;
                case "weapon": s.Weapon=new object(); break;
                case "controller": s.Controller=new object(); break;
                case "weapon id": s.WeaponId="other"; break;
                case "slot ref": slot.Slot.NativeSlot=new object(); break;
                case "item ref": item.NativeItem=new object(); break;
                case "template": item.TemplateId="other"; break;
                case "location": item.Location="Backpack"; break;
                case "locked": slot.Slot.Locked=true; break;
                case "required": slot.Slot.Required=null; break;
                case "children": item.HasChildren=true; break;
                case "unexamined": item.Examined=null; break;
                case "raidmod": item.RaidModdable=false; break;
                case "incomplete": s.Truncated=true; break;
                case "missing": slot.Candidates.Clear(); break;
                case "duplicate": slot.Candidates.Add(slot.Candidates[0]); break;
                case "occupied": slot.Slot.Installed=new ItemObservation(); break;
                case "filter": slot.Candidates[0].Evidence=CandidateEvidence.Unverified; break;
                case "empty template": item.TemplateId=""; break;
                case "duplicate slot": s.Slots.Add(slot); break;
            }
            Check(install.Validate(s).Length!=0,"Click fresh validation rejects "+change);
        }
        var removed=f.Snapshot(true); removed.Slots[0].Slot.Installed=null;
        Check(remove!.Validate(removed).Length!=0,"Uninstall cannot remove a replacement after clicked item disappears");
        var replacement=f.Snapshot(true); replacement.Slots[0].Slot.Installed!.NativeItem=new object();
        Check(remove.Validate(replacement).Length!=0,"Same-ID replacement object invalidates uninstall");
        var wrongGroup=f.Snapshot(true); wrongGroup.Slots[0].Slot.Group=AttachmentGroup.Optic;
        Check(AttachmentRequest.ScopeDenial(AttachmentAction.Uninstall,wrongGroup.Slots[0].Slot,wrongGroup.Slots[0].Slot.Installed).Length!=0,
            "A tactical device cannot use the optic category");
        var queue=new ClickRequestQueue(); var native=new InstallSession();
        Check(queue.TryBegin(install,native,10) && !queue.TryBegin(remove,native,11) && ReferenceEquals(queue.Request,install),"Duplicate click retains original waiting request");
        Check(!queue.Expired(17.99) && queue.Expired(18),"Pose return wait has a bounded eight-second deadline");
        Check(ReferenceEquals(queue.Take(),install) && !queue.Busy && queue.Take()==null,"Consumed/cancelled click cannot submit a second time");
        native.TryBegin(); native.MarkSubmitted();
        Check(!queue.TryBegin(remove,native,20),"Native pending request blocks click queue");
        native.TimedOut(); native.Finish("late",false,true);
        Check(!queue.TryBegin(remove,native,21),"Late completion preserves UNKNOWN no-retry guard for clicks");
        var facts=new InstallFacts {Enabled=true,KnownBuild=true,SinglePlayer=true,CurrentSelection=true,SnapshotComplete=true,
            Removing=true,InstalledItemMatches=true,SourceIsCarried=false,SupportedLeafAttachment=true,SupportedContext=true,YourPlayer=true,Alive=true,
            InventoryOpened=false,Aiming=false,TriggerPressed=false,InventoryLocked=false,HasActiveEvents=false,Idle=true,
            Required=false,Locked=false,Deleted=false,Empty=false,Examined=true,RaidModdable=true,FilterPass=true,NeutralPinState=true};
        Check(InstallGate.Denial(facts)=="","Uninstall policy accepts verified installed tactical source");
        foreach(var field in typeof(InstallFacts).GetFields())
        {
            if (field.Name=="Removing" || field.Name=="SourceIsCarried") continue;
            var good=field.GetValue(facts); field.SetValue(facts,!(bool)good!);
            Check(InstallGate.Denial(facts).Length!=0,"Uninstall preserves guard "+field.Name);
            if (field.FieldType==typeof(bool?)) { field.SetValue(facts,null); Check(InstallGate.Denial(facts).Length!=0,"Unknown uninstall fact denies "+field.Name); }
            field.SetValue(facts,good);
        }
        var mode=new PoseActivation();
        Check(mode.Sample(true,true,true),"First F5 press opens toggle mode");
        Check(mode.Sample(false,false,true),"Releasing physical F5 keeps mode and pose requested");
        Check(!mode.Sample(true,true,true),"Second F5 press closes toggle mode");
        mode.Sample(false,false,true); mode.Sample(true,true,true); mode.Cancel();
        Check(!mode.Sample(true,false,true),"Menu/focus close cannot reopen while F5 is held");
        Check(!mode.Sample(false,false,true) && mode.Sample(true,true,true),"After cancellation a fresh press opens toggle mode");
        Check(!mode.Sample(true,false,false),"Changing to hold mode cancels until physical release");
        mode.Sample(false,false,false);
        Check(mode.Sample(true,true,false) && !mode.Sample(false,false,false),"Optional hold mode retains release-to-close behavior");
        var capture=new PointerCapture();
        capture.Sample(true,true,false,true,1); capture.Sample(true,true,true,true,2);
        Check(capture.Active && PointerInputPolicy.Block("ToggleShooting",capture.Active,capture.SuppressMouse),
            "Keeping Alt capture during a pending request suppresses a second click's shooting command");
        capture.Sample(true,false,true,true,3); capture.Sample(true,false,false,true,4);
        Check(!capture.Active && capture.SuppressMouse,"Pending-request Alt release still drains the held button through release");
        TestMoveLayoutObservations();
    }
    private sealed class ObservedItem
    {
        public string Id=>"device"; public string TemplateId=>"template"; public int StackObjectsCount=>1;
        public object? Parent {get;set;}
        public object[] Slots {get;}=Array.Empty<object>();
    }
    private sealed class ObservedContainer
    {
        public object? ContainedItem {get;set;}
        public List<object> Items {get;}=new List<object>();
    }
    private sealed class ObservedAddress { public object Container {get;set;}=null!; }
    private static void TestMoveLayoutObservations()
    {
        // Fake observations test completion verification, not EFT simulation or a live transaction.
        var probe=new NativeInstallProbe(new ReadAccess(),System.IO.Path.GetTempPath());
        void Set(string name,object value) => typeof(NativeInstallProbe).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(probe,value);
        bool Is(string method) => (bool)typeof(NativeInstallProbe).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(probe,null)!;
        var item=new ObservedItem(); var slot=new ObservedContainer(); var pocket=new ObservedContainer();
        var slotAddress=new ObservedAddress {Container=slot}; var pocketAddress=new ObservedAddress {Container=pocket};
        Set("_item",item); Set("_slot",slot); Set("_sourceGrid",pocket); Set("_itemId","device"); Set("_templateId","template"); Set("_stack",1);
        Set("_removing",true); Set("_sourceAddress",slotAddress); Set("_destinationAddress",pocketAddress);
        slot.ContainedItem=item; item.Parent=slotAddress;
        Check(Is("OriginalLayoutIntact") && !Is("InstalledLayoutIntact"),"Uninstall original requires same installed item and absence from pocket");
        pocket.Items.Add(item);
        Check(!Is("OriginalLayoutIntact") && !Is("InstalledLayoutIntact"),"Duplicate slot/pocket observation cannot report uninstall success");
        slot.ContainedItem=null; item.Parent=pocketAddress;
        Check(Is("InstalledLayoutIntact") && !Is("OriginalLayoutIntact"),"Uninstall completion requires empty source and same item at chosen pocket address");
        item.Parent=new ObservedAddress {Container=new ObservedContainer()};
        Check(!Is("InstalledLayoutIntact"),"Wrong pocket address cannot report uninstall success");
        Set("_removing",false); Set("_sourceAddress",pocketAddress); Set("_destinationAddress",slotAddress); item.Parent=pocketAddress;
        Check(Is("OriginalLayoutIntact"),"Install direction retains original pocket verification");
        pocket.Items.Clear(); slot.ContainedItem=item; item.Parent=slotAddress;
        Check(Is("InstalledLayoutIntact"),"Install direction retains same-item empty-slot completion verification");
    }
}
