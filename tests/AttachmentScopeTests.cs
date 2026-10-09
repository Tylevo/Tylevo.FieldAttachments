using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestAttachmentScope()
    {
        var cases=new[] {
            (AttachmentGroup.Optic,"SightMod"), (AttachmentGroup.Optic,"AssaultScope"),
            (AttachmentGroup.Optic,"Collimator"), (AttachmentGroup.Optic,"CompactCollimator"),
            (AttachmentGroup.Optic,"IronSight"), (AttachmentGroup.Optic,"OpticScope"),
            (AttachmentGroup.Optic,"SpecialScope"), (AttachmentGroup.Optic,"NightVision"), (AttachmentGroup.Optic,"ThermalVision"),
            (AttachmentGroup.Muzzle,"MuzzleMod"), (AttachmentGroup.Muzzle,"Compensator"),
            (AttachmentGroup.Muzzle,"FlashHider"), (AttachmentGroup.Muzzle,"MuzzleCombo"),
            (AttachmentGroup.Muzzle,"Pms"), (AttachmentGroup.Muzzle,"Silencer"),
            (AttachmentGroup.Tactical,"TacticalCombo"), (AttachmentGroup.Underbarrel,"Foregrip") };
        foreach (var entry in cases)
        {
            var group=entry.Item1; string type="EFT.InventoryLogic."+entry.Item2;
            var f=new ClickFixture();
            RaidSnapshot Snapshot(bool installed)
            {
                var s=f.Snapshot(installed); var slot=s.Slots[0]; slot.Slot.Group=group;
                (installed ? slot.Slot.Installed! : slot.Candidates[0].Item).RuntimeType=type;
                return s;
            }
            Check(AttachmentScope.Supports(group,type),"Verified native category accepted: "+entry.Item2);
            bool crossed=false;
            foreach (AttachmentGroup other in Enum.GetValues(typeof(AttachmentGroup)))
                if (other!=group && AttachmentScope.Supports(other,type)) crossed=true;
            Check(!crossed,"Category cannot cross into another slot group: "+entry.Item2);
            foreach (var action in new[] {AttachmentAction.Install,AttachmentAction.Uninstall})
            {
                bool installed=action==AttachmentAction.Uninstall;
                string denial=AttachmentRequest.Create(action,Snapshot(installed),group,"gun/mount/tactical","real-device",out var request);
                Check(denial=="" && request?.Validate(Snapshot(installed))=="",action+" request binds/revalidates "+entry.Item2);
            }
        }
        foreach (var group in new[] {AttachmentGroup.Optic,AttachmentGroup.Muzzle,AttachmentGroup.Tactical,AttachmentGroup.Underbarrel})
        {
            string type=group==AttachmentGroup.Optic ? "OpticScope" : group==AttachmentGroup.Muzzle ? "Silencer" :
                group==AttachmentGroup.Tactical ? "TacticalCombo" : "Foregrip";
            var f=new ClickFixture();
            var s=f.Snapshot(); var slot=s.Slots[0].Slot; var item=s.Carried[0];
            slot.Group=group; item.RuntimeType="EFT.InventoryLogic."+type;
            foreach (var action in new[] {AttachmentAction.Install,AttachmentAction.Uninstall})
            {
                slot.Installed=action==AttachmentAction.Uninstall ? item : null;
                item.HasChildren=true;
                Check(AttachmentRequest.ScopeDenial(action,slot,item).Length!=0,"Child parts block "+action+" for "+group);
                item.HasChildren=false; item.RaidModdable=false;
                Check(AttachmentRequest.ScopeDenial(action,slot,item).Length!=0,"Native raid restriction preserved for "+action+" "+group);
                item.RaidModdable=null;
                Check(AttachmentRequest.ScopeDenial(action,slot,item).Length!=0,"Unknown raid permission denies "+action+" "+group);
                item.RaidModdable=true; slot.Required=true;
                Check(AttachmentRequest.ScopeDenial(action,slot,item).Length!=0,"Required slot blocked for "+action+" "+group);
                slot.Required=false;
            }
            slot.Installed=item;
            Check(AttachmentRequest.ScopeDenial(AttachmentAction.Install,slot,item).Contains("Uninstall"),"Occupied "+group+" slot still requires uninstall first");
            slot.Installed=null; item.Location="Stash";
            Check(AttachmentRequest.ScopeDenial(AttachmentAction.Install,slot,item).Contains("carried"),"New "+group+" category cannot install from unapproved containers");
            foreach(string excluded in new[] {"Mount","Stock","Barrel","Mod","Weapon","Scope"})
                Check(!AttachmentScope.Supports(group,"EFT.InventoryLogic."+excluded),"Exclude "+excluded+" from "+group+" scope");
        }
        Check(!AttachmentScope.Supports(null,"EFT.InventoryLogic.Silencer") &&
            !AttachmentScope.Supports((AttachmentGroup)99,"EFT.InventoryLogic.Silencer") &&
            !AttachmentScope.Supports(AttachmentGroup.Optic,null),"Unknown category/type fails closed");
    }
}
