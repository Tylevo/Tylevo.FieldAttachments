using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestQuickSwap()
    {
        var state=new SelectionState(); var snapshot=new RaidSnapshot {WeaponId="gun"};
        for(int i=0;i<15;i++)
        {
            var slot=new SlotViewModel {Slot=new SlotObservation {Group=AttachmentGroup.Tactical,Path="gun/rail"+i+"/mod_tactical",Id="mod_tactical",ParentName="M-LOK"}};
            for(int j=0;j<19;j++) slot.Candidates.Add(new CandidateObservation {Item=Item("item"+j)});
            snapshot.Slots.Add(slot);
        }
        state.ReplaceSnapshot(snapshot);
        Check(QuickSwapMenu.Pages(15)==3 && QuickSwapMenu.Pages(19)==4 && QuickSwapMenu.Pages(0)==1,"Mouse pages expose every mount and carried choice including partial/empty pages");
        Check(QuickSwapMenu.Page(99,15)==2 && QuickSwapMenu.Page(-1,15)==0,"Mouse wheel cannot overscroll the list");
        for(int i=0;i<15;i++)
        {
            var slot=state.GroupSlots(AttachmentGroup.Tactical)[i];
            Check(state.Highlight(AttachmentGroup.Tactical,slot.Slot.Path) && state.Current(AttachmentGroup.Tactical)==slot,
                "Mouse position selects exact native path "+i+" despite duplicate parent names");
            state.NextSlot(1);
            Check(state.SlotIndex(AttachmentGroup.Tactical)==(i+1)%15 && !state.ArmedMatches,"Position chord cycles without arming inventory "+i);
        }
        Check(!state.Highlight(AttachmentGroup.Tactical,"missing") && state.SlotIndex(AttachmentGroup.Tactical)==0,"Stale mount selection leaves valid position untouched");
        for(int i=0;i<6;i++)
        {
            var r=QuickSwapMenu.Cell(i);
            Check(QuickSwapMenu.Row(r.X,r.Y)==i && QuickSwapMenu.Row(r.X+r.Width,r.Y+r.Height)==i,"Thumbnail bounds accept mouse hits "+i);
            Check(QuickSwapMenu.Row(r.X+20,r.Y+r.Height+2)==-1,"Tile gaps never activate a choice "+i);
        }
        Check(QuickSwapMenu.Row(7,130)==-1 && QuickSwapMenu.Row(200,30)==-1 && QuickSwapMenu.Row(200,350)==-1,"Header, margin and footer cannot hit choices");
        Check(QuickSwapMenu.PopupHeight(2)==180 && QuickSwapMenu.PopupHeight(3)==284 && QuickSwapMenu.PopupHeight(6)==388,"Dropdown height follows visible thumbnail rows");
        Check(QuickSwapMenu.Width==224 && PointerInputPolicy.Block("ReloadWeapon",true,true),"Compact dropdown and captured position chord cannot trigger reload");

        Check(QuickSwapMenu.Row(float.NaN,140)==-1 && QuickSwapMenu.Row(40,float.PositiveInfinity)==-1,"Invalid pointer coordinates never activate a row");
        Check(PointerInputPolicy.Block("DecreaseWalkSpeed",true,true) && PointerInputPolicy.Block("NextWalkPose",true,true) &&
            PointerInputPolicy.Block("PreviousWalkPose",true,true),"Menu wheel cannot leak into movement speed or stance");
        Check(!PointerInputPolicy.Block("DecreaseWalkSpeed",false,false),"Normal mouse wheel gameplay resumes after Alt release");
        var lease=new AttachmentSpeedLease(); float speed=.8f;
        Check(lease.Acquire(()=>speed,v=>speed=v,1.12f) && Math.Abs(speed-.896f)<.00001f,"12 percent boost multiplies actual prior speed");
        Check(!lease.Acquire(()=>speed,v=>speed=v,1.12f),"Repeated native Start cannot stack the speed boost");
        Check(lease.Release() && Math.Abs(speed-.8f)<.00001f && !lease.Active,"Operation end restores non-default original speed");
        Check(lease.Release() && speed==.8f,"Duplicate operation cleanup is harmless");
        lease.Acquire(()=>speed,v=>speed=v,1.12f); speed=.6f;
        Check(lease.Release() && speed==.6f,"Other system's speed write survives attachment cleanup");
        foreach(float value in new[]{0f,-1f,float.NaN,float.PositiveInfinity,5f})
        { speed=value;Check(!lease.Acquire(()=>speed,v=>speed=v,1.12f),"Invalid or held native speed rejected: "+value); }
        speed=1;
        foreach(float factor in new[]{1f,0f,-1f,float.NaN,float.PositiveInfinity,1.16f})
            Check(!lease.Acquire(()=>speed,v=>speed=v,factor) && speed==1,"Disabled or out-of-range multiplier leaves native timing: "+factor);
        bool fail=true;
        lease.Acquire(()=>speed,v=>{if(v==1 && fail)throw new Exception("temporary setter failure");speed=v;},1.12f);
        Check(!lease.Release() && lease.Active,"Failed restore retains ownership for retry");
        fail=false;Check(lease.Release() && speed==1,"Next restore retries the exact original speed");
        try { lease.Acquire(()=>speed,v=>{speed=v;throw new Exception("failure after write");},1.12f); } catch { }
        Check(lease.Active,"Setter exception after acquisition preserves restoration bookkeeping");
        lease.Forget();Check(!lease.Active,"Destroyed animator can release references without touching an unrelated animator");
    }
}
