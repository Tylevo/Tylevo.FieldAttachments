using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestCustomStmInspection()
    {
        foreach (string template in new[] { StmPresentation.Template, CustomStmInspection.M4Template, McxInspectionSegment.Template,
            CustomStmInspection.M700Template, CustomStmInspection.M870Template, CustomStmInspection.Glock17Template })
        {
            Check(CustomStmInspection.Requested(true, template, false) &&
                !CustomStmInspection.Requested(false, template, false) && !CustomStmInspection.Requested(true, template, true),
                "Authored trial selects opted-in recipients but never disabled/calibration: " + template);
            string controller = CustomStmInspection.ControllerFor(template), clip = CustomStmInspection.NativeClipFor(template);
            float duration = template == McxInspectionSegment.Template ? 3.3000011444f : 3.3333339691f;
            Check(CustomStmInspection.RecipientDenial(template, controller, clip, duration, false, false, 0) == "",
                "Audited native recipient metadata accepts authored playback: " + template);
            foreach (string wrong in new[] { "other-controller", controller + "(Clone)", controller.ToUpperInvariant() })
                Check(CustomStmInspection.RecipientDenial(template, wrong, clip, duration, false, false, 0).Length != 0,
                    "Recipient controller must match its own identity: " + wrong);
            Check(CustomStmInspection.RecipientDenial(template, controller, "other_look", duration, false, false, 0).Length != 0 &&
                CustomStmInspection.RecipientDenial(template, controller, clip, duration, true, false, 0).Length != 0 &&
                CustomStmInspection.RecipientDenial(template, controller, clip, duration, false, true, 0).Length != 0 &&
                CustomStmInspection.RecipientDenial(template, controller, clip, duration, false, false, 1).Length != 0,
                "Wrong native clip/type/Unity events block before override: " + template);
            foreach (float bad in new[] { 0f, float.NaN, float.PositiveInfinity, 3.27f, 3.4f })
                Check(CustomStmInspection.RecipientDenial(template, controller, clip, bad, false, false, 0).Length != 0,
                    "Recipient duration guard retained: " + template + "/" + bad);
        }
        foreach (string unsupported in new[] { "", "unknown", "5d52cc5ba4b9367408500062", "unaudited-mod-weapon" })
            Check(!CustomStmInspection.Requested(true, unsupported, false) &&
                CustomStmInspection.RecipientDenial(unsupported, StmPresentation.Controller, "stm9_look", CustomStmInspection.Duration, false, false, 0).Length != 0,
                "No automatic expansion to unaudited recipients: " + unsupported);
        string[] recipients = { StmPresentation.Template, CustomStmInspection.M4Template, McxInspectionSegment.Template,
            CustomStmInspection.M700Template, CustomStmInspection.M870Template, CustomStmInspection.Glock17Template };
        string[] expectedBundles = { "stm_presentation", "m4a1_presentation", "mcx_presentation", "m700_presentation", "m870_presentation", "glock17_presentation" };
        for (int i = 0; i < recipients.Length; i++)
        {
            Check(CustomStmInspection.BundleFor(recipients[i]) == expectedBundles[i], "Shared presentation retains individually fitted grip: " + recipients[i]);
            for (int j = 0; j < recipients.Length; j++)
                Check(CustomStmInspection.ValidClip(recipients[i], CustomStmInspection.ClipFor(recipients[j]), CustomStmInspection.Duration, false, false, 0) == (expectedBundles[i] == expectedBundles[j]),
                    "Cross-family asset cannot replace recipient's selected animation: " + i + "/" + j);
            string name = CustomStmInspection.ClipFor(recipients[i]);
            Check(CustomStmInspection.IsAuthoredClip(name) && !CustomStmInspection.ValidClip(recipients[i], name, float.NaN, false, false, 0) &&
                !CustomStmInspection.ValidClip(recipients[i], name, CustomStmInspection.Duration, false, false, 1), "Every authored family retains duration/event guards");
        }
        Check(CustomStmInspection.BundleFor("unknown") == "" && CustomStmInspection.ClipFor("unknown") == "" &&
            !CustomStmInspection.ValidClip("unknown", "", CustomStmInspection.Duration, false, false, 0) &&
            !CustomStmInspection.IsAuthoredClip(null) && !CustomStmInspection.IsAuthoredClip("glock_look"), "Unknown/native clips never enter authored routing");
        foreach (bool native in new[] { false, true })
            Check(!CustomStmInspection.NativeMcxRequested(native, true, McxInspectionSegment.Template, false),
                "Authored MCX never selects native marker/late seek, regardless of existing MCX flags");
        Check(CustomStmInspection.NativeMcxRequested(true, false, McxInspectionSegment.Template, false) &&
            CustomStmInspection.NativeMcxRequested(true, true, McxInspectionSegment.Template, true) &&
            !CustomStmInspection.NativeMcxRequested(false, false, McxInspectionSegment.Template, false) &&
            !CustomStmInspection.NativeMcxRequested(true, true, CustomStmInspection.M4Template, false),
            "Native MCX selection returns when custom is off or calibration is requested; M4 never uses native MCX seek");
        Check(GlobalMcxInspection.NativeEventDenial(McxEvents()) == "", "Audited M4/MCX lifecycle and audio events permit authored timeline without rewriting them");
        Check(CustomStmInspection.RecipientDenial("5bfea6e90db834001b7347f3", "weapon_remington_model_700_762x51", "m700_look", 3.3333320618f, false, false, 0) == "" &&
            CustomStmInspection.RecipientDenial("5a7828548dc32e5a9c28b516", "weapon_remington_model_870_12g", "m870_look", 3.3333330154f, false, false, 0) == "" &&
            CustomStmInspection.RecipientDenial("5a7ae0c351dfba0017554310", "weapon_glock_glock_17_gen3_9x19", "glock_look", 3.3333334923f, false, false, 0) == "",
            "Actual local sniper, shotgun and pistol identities match their audited inspection clips");
        Check(CustomStmInspection.RecipientDenial(CustomStmInspection.Glock17Template, CustomStmInspection.ControllerFor(CustomStmInspection.Glock17Template), "glock_jam_look", 1.1666641235f, false, false, 0).Length != 0 &&
            CustomStmInspection.RecipientDenial(CustomStmInspection.Glock17Template, CustomStmInspection.ControllerFor(CustomStmInspection.M870Template), "glock_look", CustomStmInspection.Duration, false, false, 0).Length != 0,
            "Glock jam-inspection and cross-weapon controller cannot enter the custom presentation");
        var start = McxEvents()[0];
        var rifleEvents = new[] { start, Sound(.1188524589f, "HandOff"), Sound(.8505976200f, "HandOn") };
        var shotgunEvents = new[] { start, Sound(.0838206634f, "GunFlip"), Sound(.1028225794f, "HandOn"), Sound(.4093567133f, "GunFlip1"),
            Sound(.4721189737f, "HandOff"), Sound(.7918215394f, "HandOn"), Sound(.8089668751f, "GunFlip2") };
        var pistolEvents = new[] { start, Sound(.0396975428f, "HandOn"), Sound(.2759924531f, "HandOff"), Sound(.4120982885f, "HandOn"),
            Sound(.7977315784f, "HandOn"), Sound(.8317580223f, "HandOff") };
        pistolEvents[4].Enabled = false;
        foreach (var profile in new[] { rifleEvents, shotgunEvents, pistolEvents })
            Check(GlobalMcxInspection.NativeEventDenial(profile) == "", "Audited category's native start/audio events allow unchanged authored playback: " + profile.Length);
        Check(!pistolEvents[4].Enabled && pistolEvents[4].Time == .7977315784f, "Native disabled pistol audio row is preserved by validation");
        shotgunEvents[3].Name = "AddAmmoInChamber";
        Check(GlobalMcxInspection.NativeEventDenial(shotgunEvents).Length != 0, "Unexpected shotgun gameplay event still rejects the trial");
        Check(CustomStmInspection.ValidClip(StmPresentation.Template, CustomStmInspection.ClipName, 3.333333015f, false, false, 0), "Custom clip matches actual STM native duration");
        foreach (float value in new[] { float.NaN, float.PositiveInfinity, 0f, 3.3f, 3.34f })
            Check(!CustomStmInspection.ValidClip(StmPresentation.Template, CustomStmInspection.ClipName, value, false, false, 0), "Custom clip rejects mismatched/nonfinite duration " + value);
        Check(!CustomStmInspection.ValidClip(StmPresentation.Template, "stm9_look", CustomStmInspection.Duration, false, false, 0) &&
            !CustomStmInspection.ValidClip(StmPresentation.Template, CustomStmInspection.ClipName, CustomStmInspection.Duration, true, false, 0) &&
            !CustomStmInspection.ValidClip(StmPresentation.Template, CustomStmInspection.ClipName, CustomStmInspection.Duration, false, true, 0) &&
            !CustomStmInspection.ValidClip(StmPresentation.Template, CustomStmInspection.ClipName, CustomStmInspection.Duration, false, false, 1), "Custom clip rejects identity, legacy, humanoid or event changes");
        Check(CustomStmInspection.EarlyEnough(0) && CustomStmInspection.EarlyEnough(.06f) &&
            !CustomStmInspection.EarlyEnough(.061f) && !CustomStmInspection.EarlyEnough(-.01f) && !CustomStmInspection.EarlyEnough(float.NaN), "Custom clip replacement refuses late timeline and never seeks");
        var exit = new CustomStmReturn();
        Check(!exit.Begin(float.NaN) && exit.Begin(10) && !exit.Begin(11), "Duplicate authored exit cannot reset its timeout");
        Check(exit.Observe(10.1f, .22f, true, true, true) == CustomStmReturnResult.Waiting && exit.Pending, "Authored hold release waits for exit motion");
        Check(exit.Observe(10.8f, .45f, true, true, true) == CustomStmReturnResult.BlendToIdle && !exit.Pending, "Authored exit permits a single verified native idle blend");
        Check(exit.Observe(11, .45f, true, true, true) == CustomStmReturnResult.YieldToNative, "Duplicate completed exit cannot repeat blend");
        foreach (bool owned in new[] { false, true })
        {
            exit.Begin(20);
            Check(exit.Observe(20.1f, .3f, false, owned, true) == CustomStmReturnResult.YieldToNative && !exit.Pending, "Unsafe interruption yields to native playback without another action");
        }
        exit.Begin(20);
        Check(exit.Observe(20.1f, .3f, true, false, true) == CustomStmReturnResult.YieldToNative, "Changed operation owns its own return");
        foreach (float time in new[] { float.NaN, -1f, 1.2f })
        {
            exit.Begin(20);
            Check(exit.Observe(20.1f, time, true, true, true) == CustomStmReturnResult.Fault, "Invalid exit timeline latches uncertainty " + time);
        }
        exit.Begin(20);
        Check(exit.Observe(24.01f, .3f, true, true, true) == CustomStmReturnResult.Fault, "Authored exit times out without retry");
        exit.Begin(20);
        Check(exit.Observe(20.1f, .3f, true, true, false) == CustomStmReturnResult.Fault, "External clip replacement rejects authored completion");
        exit.Begin(20); exit.Cancel();
        Check(!exit.Pending && exit.Observe(21, .5f, true, true, true) == CustomStmReturnResult.YieldToNative, "Cancelled exit never issues delayed blend");
        exit.Begin(30,true);
        Check(exit.Observe(30.01f,0,true,true,false,false)==CustomStmReturnResult.Waiting && exit.Pending,
            "Quick close before native start waits without replacing or seeking the original clip");
        Check(exit.Observe(30.04f,.01f,true,true,false,true)==CustomStmReturnResult.BlendToIdle && !exit.Pending,
            "Native ready event lets early close blend directly to idle before custom clip was applied");
        Check(exit.Observe(30.05f,.02f,true,true,false,true)==CustomStmReturnResult.YieldToNative,
            "Repeated close cannot issue another idle blend");
        exit.Begin(80,true);
        Check(exit.Observe(80.01f,20,true,true,false,false)==CustomStmReturnResult.Waiting && exit.Pending,
            "Early close waits through a long-running idle timeline before the inspection is ready");
        Check(exit.Observe(80.04f,.01f,true,true,false,true)==CustomStmReturnResult.BlendToIdle && !exit.Pending,
            "Inspection readiness after a long idle permits one early-close blend");
        Check(exit.Observe(80.05f,.02f,true,true,false,true)==CustomStmReturnResult.YieldToNative,
            "Long-idle early close cannot repeat its completed blend");
        foreach (float time in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 1.2f, 20f })
        {
            exit.Begin(80,true);
            Check(exit.Observe(80.01f,time,true,true,false,false)==CustomStmReturnResult.Waiting && exit.Pending,
                "Unready early close does not interpret the preceding state's timeline: " + time);
            Check(exit.Observe(80.04f,time,true,true,false,true)==CustomStmReturnResult.Fault && !exit.Pending,
                "The same invalid timeline faults once the inspection is ready: " + time);
            exit.Begin(80);
            Check(exit.Observe(80.01f,time,true,true,true,false)==CustomStmReturnResult.Fault && !exit.Pending,
                "A normal authored exit still validates its timeline while unready: " + time);
        }
        foreach (var guard in new[] { (safe:false,owned:true), (safe:true,owned:false), (safe:false,owned:false) })
        {
            exit.Begin(80,true);
            Check(exit.Observe(80.01f,20,guard.safe,guard.owned,false,false)==CustomStmReturnResult.YieldToNative && !exit.Pending,
                "Unready early close still yields immediately when safety or ownership is lost: " + guard);
        }
        foreach (float now in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 79.99f, 84.01f })
        {
            exit.Begin(80,true);
            Check(exit.Observe(now,20,true,true,false,false)==CustomStmReturnResult.Fault && !exit.Pending,
                "Unready early close retains clock validity and its original timeout: " + now);
        }
        exit.Begin(80,true);
        Check(exit.Observe(84,20,true,true,false,false)==CustomStmReturnResult.Waiting && exit.Pending,
            "Early close may still wait at the four-second boundary");
        Check(exit.Observe(84,.01f,true,true,false,true)==CustomStmReturnResult.BlendToIdle && !exit.Pending,
            "Readiness at the existing deadline can complete without extending the wait");
        exit.Begin(40,true);
        Check(exit.Observe(40.1f,.1f,true,true,true)==CustomStmReturnResult.BlendToIdle,
            "Closing mid-custom entrance skips the remaining rise/hold/outro");
        exit.Begin(50,true);
        Check(exit.Observe(50.1f,.1f,false,true,true)==CustomStmReturnResult.YieldToNative && !exit.Pending,
            "Gameplay interruption cancels an early-close blend");
        exit.Begin(60,true);
        Check(exit.Observe(60.1f,.1f,true,false,true)==CustomStmReturnResult.YieldToNative,
            "Early close cannot cancel another native operation");
        exit.Begin(70,true);
        Check(exit.Observe(74.1f,.01f,true,true,false,false)==CustomStmReturnResult.Fault,
            "Missing native start event has a bounded wait and no retry");
        // Directly read STM Hands.LOOK table: one lifecycle start, five audio events.
        var events = new[] {
            new McxInspectionEvent { Name="StartUtilityOperation",Hash=1134400241,Enabled=true,Time=0 },
            Sound(.0407191962f,"HandOff"), Sound(.1123829335f,"GunFlip2"), Sound(.4693028033f,"GunFlip"),
            Sound(.5858480930f,"HandOnHard"), Sound(.8522372246f,"GunFlip1")
        };
        Check(GlobalMcxInspection.NativeEventDenial(events) == "", "Actual STM lifecycle/audio table accepts custom authored timeline");
        events[3].Name = "SpawnItem";
        Check(GlobalMcxInspection.NativeEventDenial(events).Length != 0, "Unexpected native gameplay event rejects custom animation");
    }
    private static McxInspectionEvent Sound(float time, string text) => new McxInspectionEvent {
        Name="Sound",Hash=1554795451,Enabled=true,Time=time,ParameterType=3,Text=text
    };
}
