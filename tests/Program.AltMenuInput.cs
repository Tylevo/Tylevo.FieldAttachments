using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestAltMenuInput()
    {
        var hold = new AltMenuActivation();
        Check(!hold.Sample(true,false,MenuActivationMode.Hold,true,true,false,false,0),"Alt held before registration cannot open without a fresh press");
        hold.Sample(false,false,MenuActivationMode.Hold,true,true,false,false,1);
        Check(hold.Sample(true,true,MenuActivationMode.Hold,true,true,false,false,2),"Hold opens on fresh Alt-down before native input");
        Check(hold.Sample(true,true,MenuActivationMode.Hold,true,true,false,false,2),"Native callback and Update may sample the opening frame repeatedly");
        Check(hold.Sample(true,false,MenuActivationMode.Hold,true,true,false,true,3),"RMB LIST close or Alt+R keeps the held menu open");
        Check(!hold.Sample(false,false,MenuActivationMode.Hold,true,true,false,false,4),"Alt release closes Hold mode");
        hold.Sample(true,true,MenuActivationMode.Hold,true,true,false,false,5);
        Check(!hold.Sample(true,false,MenuActivationMode.Hold,true,true,true,false,6),"Escape closes the entire held menu");
        Check(!hold.Sample(true,false,MenuActivationMode.Hold,true,true,false,false,7),"Escape cannot reopen while Alt remains held");
        hold.Sample(false,false,MenuActivationMode.Hold,true,true,false,false,8);
        Check(hold.Sample(true,true,MenuActivationMode.Hold,true,true,false,false,9),"A new released Alt press may reopen after Escape");
        Check(!hold.Sample(true,false,MenuActivationMode.Hold,true,false,false,false,10),"Focus loss closes the requested menu");
        Check(!hold.Sample(true,false,MenuActivationMode.Hold,true,true,false,false,11),"Focus return cannot reopen a held Alt key");
        hold.Sample(false,false,MenuActivationMode.Hold,true,true,false,false,12);
        Check(!hold.Sample(true,true,MenuActivationMode.Hold,false,true,false,false,13),"Local ownership or prior-outro denial refuses a fresh open");
        Check(!hold.Sample(true,false,MenuActivationMode.Hold,true,true,false,false,14),"Restored eligibility does not retry the refused held press");
        hold.Sample(false,false,MenuActivationMode.Hold,true,true,false,false,15);
        hold.Sample(true,true,MenuActivationMode.Hold,true,true,false,false,16);
        hold.Cancel();
        Check(!hold.Sample(true,true,MenuActivationMode.Hold,true,true,false,false,16),"A failed standalone entry cannot retry in the same frame");
        Check(!hold.Sample(true,false,MenuActivationMode.Hold,true,true,false,false,17),"A failed standalone entry requires key release before retry");

        var toggle = new AltMenuActivation();
        Check(toggle.Sample(true,true,MenuActivationMode.Toggle,true,true,false,false,20),"Toggle opens on first Alt-down");
        Check(toggle.Sample(true,true,MenuActivationMode.Toggle,true,true,false,false,20),"Repeated opening-frame callbacks do not schedule a closing tap");
        Check(toggle.Sample(false,false,MenuActivationMode.Toggle,true,true,false,false,21),"Toggle remains open after opening Alt release");
        Check(toggle.Sample(true,true,MenuActivationMode.Toggle,true,true,false,false,22),"A later Alt press stays open while a chord can still follow");
        Check(toggle.Sample(true,false,MenuActivationMode.Toggle,true,true,false,true,23),"Alt+R arriving on a later frame cancels the pending close");
        Check(toggle.Sample(false,false,MenuActivationMode.Toggle,true,true,false,false,24),"Toggle and cursor remain requested after the mount chord releases");
        Check(toggle.Sample(true,true,MenuActivationMode.Toggle,true,true,false,true,25),"Same-frame Alt+R also retains Toggle mode");
        Check(toggle.Sample(true,true,MenuActivationMode.Toggle,true,true,false,true,25),"Repeated same-frame mount chord sampling is idempotent");
        Check(toggle.Sample(false,false,MenuActivationMode.Toggle,true,true,false,false,26),"Same-frame chord release leaves Toggle open");
        toggle.Sample(true,true,MenuActivationMode.Toggle,true,true,false,false,27);
        Check(toggle.Sample(true,false,MenuActivationMode.Toggle,true,true,false,true,28),"Pointer click while Alt is held is an interaction, not a closing plain tap");
        Check(toggle.Sample(false,false,MenuActivationMode.Toggle,true,true,false,false,29),"RMB LIST interaction does not close the menu on Alt release");
        Check(toggle.Sample(true,true,MenuActivationMode.Toggle,true,true,false,false,30),"Next plain tap begins without closing before release");
        Check(!toggle.Sample(false,false,MenuActivationMode.Toggle,true,true,false,false,31),"Next plain Alt release closes Toggle mode");
        toggle.Sample(true,true,MenuActivationMode.Toggle,true,true,false,false,32);
        Check(!toggle.Sample(true,false,MenuActivationMode.Hold,true,true,false,false,33),"Changing activation mode while held cancels the previous mode");
        Check(!toggle.Sample(true,false,MenuActivationMode.Hold,true,true,false,false,34),"Changed mode still requires a released press");
        toggle.Sample(false,false,MenuActivationMode.Hold,true,true,false,false,35);
        Check(toggle.Sample(true,true,MenuActivationMode.Hold,true,true,false,false,36),"New mode accepts a fresh released press");

        var capture = new PointerCapture();
        capture.Sample(false,true,false,true,40);
        capture.Sample(true,true,true,true,41,true);
        Check(capture.Active && capture.SuppressMouse,"Accepted Alt activation captures before rendering despite an earlier legacy modifier latch");
        capture.Sample(true,true,false,true,42,true);
        Check(capture.Active,"Toggle automatic intent keeps the cursor captured after physical Alt release");
        capture.Sample(true,true,true,true,43,true);
        capture.Cancel();
        capture.Sample(false,false,true,true,44);
        Check(!capture.Active && capture.SuppressMouse,"Closing Alt menu retains held LMB/RMB drain");
        Check(PointerInputPolicy.Block("ToggleShooting",false,capture.SuppressMouse) &&
              PointerInputPolicy.Block("ToggleAlternativeShooting",false,capture.SuppressMouse),"Drained buttons cannot leak fire or ADS starts");
        capture.Sample(false,false,false,true,45);
        Check(capture.SuppressMouse,"Mouse release frame remains owned after closing");
        capture.Sample(false,false,false,true,46);
        Check(!capture.SuppressMouse,"Next neutral frame restores ordinary mouse input");
        Check(!PointerInputPolicy.Block("ExamineWeapon",false,false) && !PointerInputPolicy.Block("ResetLookDirection",false,false),"Unrelated native inspect and free-look release remain unchanged outside the menu");
        var axes = new float[] {1,2,3,4,5,6,7};
        PointerInputPolicy.FilterAxes(axes,false);
        Check(axes[2]==3 && axes[4]==5 && axes[5]==6,"Closed Alt menu adds no middle-mouse or look-axis tail filtering");
        capture.Sample(false,true,true,false,47,true);
        Check(!capture.Active,"Automatic intent cannot bypass focus or ownership restrictions");

        var legacy = new PoseActivation();
        var requested = new PoseActivation();
        legacy.Sample(true,true,true);
        var altInLegacy = new AltMenuActivation();
        Check(!altInLegacy.Sample(true,true,MenuActivationMode.Hold,false,true,false,false,50),"Existing O session does not transfer ownership to an Alt hold");
        Check(requested.Sample(legacy.Open,false,false),"Legacy O keeps the shared standalone lifetime requested");
        legacy.Sample(false,false,true);
        Check(requested.Sample(legacy.Open,false,false),"Legacy toggle stays open after O release and cursor release");
        legacy.Sample(true,true,true);
        Check(!requested.Sample(legacy.Open,false,false),"Second legacy O press closes its shared lifetime");
        Check(requested.Sample(true,false,false),"Alt mode uses the same requested lifetime observed by successful pose resume");
        requested.Cancel();
        Check(!requested.Open,"Alt close cancels the same authorization used by pose resume");
    }
}
