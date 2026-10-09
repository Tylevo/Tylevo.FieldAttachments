using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestPointer()
    {
        var capture = new PointerCapture();
        capture.Sample(true,false,false,true,0);
        Check(!capture.Active && !capture.SuppressMouse,"Idle pointer leaves gameplay input untouched");
        capture.Sample(true,true,true,true,1);
        Check(capture.Active && capture.SuppressMouse,"First Alt frame captures look and mouse before plugin Update");
        capture.Sample(true,false,true,true,2);
        Check(!capture.Active && capture.SuppressMouse,"Alt release restores look but retains a held mouse button");
        capture.Sample(true,false,false,true,3);
        Check(capture.SuppressMouse,"Mouse release frame cannot leak a shooting toggle");
        capture.Sample(true,false,false,true,4);
        Check(!capture.SuppressMouse,"Next neutral frame restores shooting input");
        capture.Sample(true,true,false,true,5);
        capture.Sample(false,true,false,true,6);
        Check(!capture.Active && capture.NeedsRelease,"Menu/context invalidation cancels pointer capture");
        capture.Sample(true,true,false,true,7);
        Check(!capture.Active,"Returning from a menu while Alt remains held does not recapture");
        capture.Sample(true,false,false,true,8); capture.Sample(true,true,false,true,9);
        Check(capture.Active,"Fresh Alt press can recapture after a context interruption");
        capture.Sample(true,true,true,false,10);
        Check(!capture.Active && capture.NeedsRelease,"Focus loss cancels capture and requires modifier release");
        capture.Sample(true,false,false,true,11); capture.Sample(true,true,true,true,12); capture.Cancel();
        Check(!capture.Active && capture.SuppressMouse,"Closing the overlay retains mouse-button tail ownership");
        capture.Sample(false,false,true,true,13);
        Check(capture.SuppressMouse,"Held click stays suppressed while overlay is closed");
        capture.Sample(false,false,false,true,14); capture.Sample(false,false,false,true,15);
        Check(!capture.SuppressMouse,"Closed overlay releases its input node after the click drains");
        capture.Sample(false,true,false,true,16); capture.Sample(true,true,false,true,17);
        Check(!capture.Active,"Alt held before an eligible view must be released before capture can start");

        Check(PointerInputPolicy.Block("ToggleShooting",true,true) && PointerInputPolicy.Block("ToggleAlternativeShooting",true,true),
            "Pointer mode blocks starting fire and aim");
        Check(PointerInputPolicy.Block("ToggleShooting",false,true) && !PointerInputPolicy.Block("Reload",false,true),
            "Tail suppression blocks only mouse shooting/aiming starts");
        Check(!PointerInputPolicy.Block("EndShooting",true,true) && !PointerInputPolicy.Block("EndAlternativeShooting",true,true) &&
            !PointerInputPolicy.Block("ResetLookDirection",true,true) && !PointerInputPolicy.Block("EndBreathing",true,true),
            "Native release commands remain available to clear existing hands/look states");
        Check(!PointerInputPolicy.Block("ToggleInventory",true,true) && !PointerInputPolicy.Block("Escape",true,true) &&
            !PointerInputPolicy.Block("F12",true,true) && !PointerInputPolicy.Block("ToggleSprinting",true,true),
            "Native menus and movement remain available while pointing");
        Check(!PointerInputPolicy.Block("ToggleShooting",false,false),"Normal gameplay remains unfiltered without capture or a mouse tail");
        var axes = new float[] {1,2,3,4,5,6,7}; PointerInputPolicy.FilterAxes(axes,true);
        Check(axes[0]==1 && axes[1]==2 && axes[2]==0 && axes[3]==0 && axes[4]==0 && axes[5]==0 && axes[6]==0,
            "Input filter freezes turn/freelook/lean while preserving movement axes");
        axes = new float[] {1,2,3}; PointerInputPolicy.FilterAxes(axes,false);
        Check(axes[2]==3,"Inactive filter leaves look axes unchanged");
        PointerInputPolicy.FilterAxes(axes,true); Check(axes[2]==0,"Short native axis arrays are bounded safely");

        var gesture = new PointerGesture(); gesture.Press("item-a",10);
        Check(gesture.Release("item-a",10),"Press and release on the same item/view yield one click");
        Check(!gesture.Release("item-a",10),"Duplicate mouse releases cannot repeat a selection");
        gesture.Press("item-a",10); Check(!gesture.Release("item-b",10),"Drag release onto another item does not select it");
        gesture.Press("item-a",10); Check(!gesture.Release("item-a",11),"Rescan/page change invalidates a pending click");
        gesture.Press("item-a",10); gesture.Cancel(); Check(!gesture.Release("item-a",10),"Alt release/close cancels a pending click");
        gesture.Press("item-a",10); Check(!gesture.Release(null,10),"Release outside the dropdown consumes the press");

        // Last observed M4 quad includes translation into a category home and depth skew.
        Check(PanelProjection.TryCreate(new PanelPoint(401.4388f,181.4058f,.2639413f),
            new PanelPoint(746.0764f,168.7681f,.1846866f),new PanelPoint(472.2154f,292.1201f,.2978535f),out var plane),
            "Recorded M4 panel can be used for pointer hit tests");
        bool hits=true;
        foreach (float x in new[] {0f,.1f,.25f,.5f,.9f,1f}) foreach (float y in new[] {0f,.1f,.5f,.9f,1f})
        {
            var point=plane!.Map(x,y);
            hits &= plane.TryUnmap(point.X,point.Y,out var u,out var v) && Math.Abs(u-x)<.0001 && Math.Abs(v-y)<.0001;
        }
        Check(hits,"Projected text/tile corners and interior map back to their actual visible hit regions");
        plane!.Translate(210,-50); var translated=plane.Map(.3f,.4f);
        Check(plane.TryUnmap(translated.X,translated.Y,out var a,out var b) && Math.Abs(a-.3)<.0001 && Math.Abs(b-.4)<.0001,
            "Moved cards keep pointer hit tests aligned with the displayed mesh");
        var outside=plane.Map(-.05f,.5f);
        Check(!plane.TryUnmap(outside.X,outside.Y,out _,out _) && !plane.TryUnmap(float.NaN,1,out _,out _),
            "Outside and invalid pointer coordinates cannot hit a card");

        var state=new SelectionState(); var snapshot=new RaidSnapshot {WeaponId="weapon"};
        var slot=new SlotViewModel {Slot=new SlotObservation {Group=AttachmentGroup.Tactical,Path="mount/tactical",ParentName="Mount"}};
        slot.Candidates.Add(new CandidateObservation {Item=Item("one")}); slot.Candidates.Add(new CandidateObservation {Item=Item("two")});
        snapshot.Slots.Add(slot); state.ReplaceSnapshot(snapshot);
        Check(state.Highlight(AttachmentGroup.Tactical,"mount/tactical","one") && state.Selected()?.Item.Id=="one" && !state.ArmedMatches,
            "Highlight helper never arms or executes");
        state.Stage("F6");
        Check(state.Highlight(AttachmentGroup.Tactical,"mount/tactical","two") && state.StagedItemId=="",
            "Highlighting a different item invalidates the old arm");
        Check(!state.Highlight(AttachmentGroup.Tactical,"mount/tactical","stale") && state.Selected()?.Item.Id=="two",
            "Disappeared item identity cannot select an index now occupied by another item");
        Check(!state.Highlight(AttachmentGroup.Tactical,"missing") && !state.Highlight((AttachmentGroup)99,"mount/tactical"),
            "Missing slot and invalid group targets are rejected");
        slot.Candidates.Add(new CandidateObservation {Item=Item("two")});
        Check(!state.Highlight(AttachmentGroup.Tactical,"mount/tactical","two"),"Ambiguous item IDs fail closed for mouse selection");
    }
}
