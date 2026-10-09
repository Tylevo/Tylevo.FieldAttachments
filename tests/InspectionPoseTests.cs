using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestInspectionPose()
    {
        TestPoseControllerLease();
        TestNativeSprintReader();
        var pose = new InspectionPoseState();
        Check(!pose.Active && !pose.CanHold(true, true, true), "Ordinary native inspect is never owned or paused");
        Check(pose.Start(10) && !pose.Start(11), "Separate pose request starts once; cannot replace an active hold");
        Check(!pose.CanHold(false, true, true), "Pose cannot pause a different native operation");
        Check(!pose.CanHold(true, false, true), "Pose waits for native start event instead of freezing startup timeout");
        Check(!pose.CanHold(true, true, false), "Pose does not mark an animation transition");
        Check(pose.CanHold(true, true, true), "Owned ready stable inspection may be held");
        Check(pose.Deadline(19).Length > 0, "Missing recorded pose has a bounded approach time");
        pose.Held(12);
        Check(pose.Deadline(43).Length > 0, "Held pose has a bounded watchdog duration");
        pose.Returning();
        Check(pose.Active && !pose.CanHold(true, true, true), "Returning remains blocked from inventory execution and cannot auto-hold again");
        pose.End();
        Check(!pose.Active && pose.Phase == PosePhase.None, "Native return completes owned lifecycle without another action");

        float speed = 0.75f;
        var lease = new PoseSpeedLease();
        lease.Acquire(() => speed, v => speed = v);
        Check(speed == 0 && lease.Active && lease.PreviousSpeed == 0.75f, "Hold pauses only acquired animator and remembers actual previous speed");
        Check(lease.TryRelease() && speed == 0.75f && !lease.Active, "Release restores previous speed, not a hardcoded one");
        Check(lease.TryRelease() && speed == 0.75f, "Repeated cleanup is idempotent");
        lease.Acquire(() => speed, v => speed = v); speed = 0.9f;
        Check(lease.TryRelease() && speed == 0.9f, "External nonzero animator speed is preserved on release");
        bool failRestore = true;
        lease.Acquire(() => speed, v => { if (v != 0 && failRestore) throw new Exception("fixture"); speed = v; });
        Check(!lease.TryRelease() && lease.Active && speed == 0, "Restore failure retains speed ownership for cleanup retry");
        failRestore = false;
        Check(lease.TryRelease() && speed == 0.9f && !lease.Active, "Cleanup retry restores the same animator without another inspection");
        bool failed = false;
        try { lease.Acquire(() => 0, _ => throw new Exception("must not write")); } catch (InvalidOperationException) { failed = true; }
        Check(failed && !lease.Active, "Already frozen animator is not acquired");
        failed = false;
        try { lease.Acquire(() => float.NaN, _ => throw new Exception("must not write")); } catch (InvalidOperationException) { failed = true; }
        Check(failed && !lease.Active, "Unknown animator speed cannot be acquired");
        failed = false; speed = 0.7f;
        try { lease.Acquire(() => speed, v => { speed = v; if (v == 0) throw new Exception("fixture partial write"); }); } catch { failed = true; }
        Check(failed && !lease.Active && speed == 0.7f, "Failed pause write restores its partially changed speed");

        var marker = new PoseMarker { WeaponTemplate = "m4", Controller = "controller/name", Layer = 1, State = -12345, Time = 0.42f };
        var decoded = PoseMarker.Decode(marker.Encode());
        Check(decoded != null && decoded.Matches("m4", "controller/name", 1, -12345) && decoded.Time == marker.Time,
            "Pose marker preserves template/controller/layer/state and normalized time");
        Check(!marker.Matches("mdr", marker.Controller, 1, marker.State) && !marker.Matches("m4", "changed", 1, marker.State),
            "Recorded pose is not applied to a different weapon or animator controller");
        Check(!marker.Matches("m4", marker.Controller, 1, 77), "Other animation state cannot trigger recorded hold");
        Check(!marker.Reached(0.3f) && marker.Reached(0.43f) && !marker.Reached(0.8f) && !marker.Reached(float.NaN),
            "Recorded pose uses normalized progression with bounded overshoot; never seeks or guesses a frame count");
        Check(PoseMarker.Decode("bad") == null && PoseMarker.Decode("1|m4|%%%|0|4|0.2") == null &&
            PoseMarker.Decode("1|m4|YQ==|99|4|0.2") == null && PoseMarker.Decode("1|m4|YQ==|0|4|NaN") == null,
            "Corrupt or out-of-range pose calibration fails closed");
        var keys = new System.Collections.Generic.HashSet<int> { 5, 4, 8 };
        Check(KeyChord.IsDown(4, Array.Empty<int>(), keys.Contains, k => k == 4) && KeyChord.IsHeld(5, Array.Empty<int>(), keys.Contains),
            "Pose mark accepts its held activation chord and existing overlay key");
        keys.Remove(5);
        Check(!KeyChord.IsHeld(5, Array.Empty<int>(), keys.Contains), "Releasing pose key ends pose intent even when F8 remains held");

        var shared = PoseMarker.Resolve(null, "ak", "ak-controller", true, false);
        Check(shared != null && shared.Matches("ak", "ak-controller", 1, PoseMarker.SharedState) && shared.Time == 0.27f,
            "Matching native inspection uses shared 27% fallback without writing a per-weapon marker");
        Check(ReferenceEquals(marker, PoseMarker.Resolve(marker, "m4", marker.Controller, true, false)),
            "Saved per-weapon adjustment wins over shared timing");
        Check(PoseMarker.Resolve(marker, "ak", "ak-controller", false, false) == null,
            "Different controller with no matching shared state cannot inherit a saved timing");
        Check(PoseMarker.Resolve(marker, "m4", marker.Controller, true, true) == null,
            "Shift calibration bypasses both shared timing and saved override");
        Check(PoseMarker.Resolve(null, "unknown", "unknown", false, false) == null,
            "Unknown inspection state remains manual rather than using a guessed timeline");

        var returning = new PoseIdleReturn();
        int blends = 0, cancels = 0;
        bool idleOperation = false;
        Action<bool> cancel = pressed => { Check(!pressed, "Native inspection cancellation never requests a shot"); cancels++; idleOperation = true; };
        Check(!returning.Complete(true, true, true, cancel, () => idleOperation) && cancels == 0,
            "Normal inspect never uses short-return cancellation without explicit return ownership");
        Check(returning.Begin(10, () => blends++) && !returning.Begin(11, () => blends++) && blends == 1,
            "A return requests exactly one blend even with repeated close calls");
        Check(!returning.Complete(false, true, true, cancel, () => idleOperation) && cancels == 0 && returning.Active,
            "Native operation cannot be ended before the visible idle transition completes");
        Check(!returning.Complete(true, true, false, cancel, () => idleOperation) && cancels == 0,
            "Interruption or unknown hands facts deny synthetic trigger-release cancellation");
        Check(returning.Complete(true, true, true, cancel, () => idleOperation) && cancels == 1 && !returning.Active,
            "Return completes only with visible idle and confirmed native Idling");
        returning.Complete(true, true, true, cancel, () => idleOperation);
        Check(cancels == 1, "Completed return cannot issue a second cancellation");
        returning.Begin(20, () => blends++);
        Check(returning.Complete(true, false, true, cancel, () => true) && cancels == 1,
            "Native idle event completion needs no explicit cancellation");

        returning.Begin(30, () => blends++);
        Check(!returning.ReportTimeout(30.9f) && returning.ReportTimeout(31.1f) && !returning.ReportTimeout(32) && returning.Active,
            "Timeout is reported once and does not silently unblock inventory or repeat a mutation");
        try { returning.Complete(true, true, true, _ => { cancels++; throw new Exception("partial cancellation"); }, () => false); } catch { }
        Check(returning.CancelAttempted && returning.Active && cancels == 2,
            "Uncertain cancellation retains observation and records that the call was attempted");
        Check(!returning.Complete(true, true, true, _ => cancels++, () => false) && cancels == 2,
            "Uncertain native cancellation is never automatically retried");
        Check(returning.Complete(true, false, true, _ => cancels++, () => true) && cancels == 2,
            "Late confirmed idle can resolve uncertain cancellation without another call");
        try { returning.Begin(40, () => throw new Exception("partial blend")); } catch { }
        Check(returning.Active && !returning.CancelAttempted, "Partial blend failure retains observation without cancelling inspection early");
        returning.End();
        Check(!returning.Active, "Destroyed or changed context releases return tracking without native actions");
    }
}
