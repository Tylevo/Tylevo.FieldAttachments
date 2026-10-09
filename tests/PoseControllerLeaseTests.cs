using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestPoseControllerLease()
    {
        object original = new object(), replacement = new object(), external = new object();
        object? current = original;
        object owner = new object();
        object? controller = owner;
        var lease = new PoseBindingLease<object, object>();
        int writes = 0;
        Action<object> write = value => { writes++; current = value; };
        Check(lease.TryRestore() && writes == 0, "Ordinary inspect does not acquire or restore a clip");
        lease.Acquire(owner, original, replacement, () => controller, () => current, write);
        Check(lease.Active && ReferenceEquals(current, replacement), "Explicit motion trial owns only its replacement clip");
        bool rejected = false;
        try { lease.Acquire(owner, original, replacement, () => controller, () => current, write); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && writes == 1, "Duplicate clip acquisition cannot overwrite the original restoration handle");
        Check(lease.TryRestore() && ReferenceEquals(current, original) && writes == 2 && !lease.Active,
            "Return restores the exact original clip");
        Check(lease.TryRestore() && writes == 2, "Repeated close does not rewrite clip bindings");

        lease.Acquire(owner, original, replacement, () => controller, () => current, write); current = external;
        Check(lease.TryRestore() && ReferenceEquals(current, external) && lease.ExternalChange && writes == 3,
            "External clip replacement is preserved and flagged for resource cleanup");
        rejected = false;
        try { lease.Acquire(owner, original, replacement, () => controller, () => current, write); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && !lease.Active && writes == 3, "Changed original identity denies acquisition before mutation");

        current = original;
        bool failRestore = true;
        lease.Acquire(owner, original, replacement, () => controller, () => current, value => {
            if (ReferenceEquals(value, original) && failRestore) throw new Exception("fixture");
            current = value;
        });
        Check(!lease.TryRestore() && lease.Active && ReferenceEquals(current, replacement),
            "Failed clip restoration keeps ownership and the inventory block");
        failRestore = false;
        Check(lease.TryRestore() && ReferenceEquals(current, original) && !lease.Active,
            "Restoration retry uses the same original controller without another donor application");

        rejected = false;
        try { lease.Acquire(owner, original, replacement, () => controller, () => current, value => {
            current = value; if (ReferenceEquals(value, replacement)) throw new Exception("partial assignment");
        }); } catch { rejected = true; }
        Check(rejected && !lease.Active && ReferenceEquals(current, original), "Partially applied replacement is restored when acquisition throws");
        try { lease.Acquire(owner, original, replacement, () => controller, () => current, _ => { }); } catch { }
        Check(!lease.Active && ReferenceEquals(current, original), "Silent failed assignment is detected without losing the native clip");

        lease.Acquire(owner, original, replacement, () => controller, () => current, value => current = value);
        current = null;
        Check(lease.TryRestore() && !lease.Active && current == null, "Missing clip is not overwritten by cleanup");
        current = original;
        lease.Acquire(owner, original, replacement, () => controller, () => current, write);
        controller = external;
        int writesBefore = writes;
        Check(lease.TryRestore() && lease.ExternalChange && writes == writesBefore && ReferenceEquals(current, replacement),
            "External controller change prevents writing even an otherwise owned clip");
        rejected = false;
        try { lease.Acquire(owner, original, replacement, () => controller, () => original, write); } catch { rejected = true; }
        Check(rejected && !lease.Active && writes == writesBefore, "Wrong controller denies donor application before mutation");
        controller = owner; current = original;
        lease.Acquire(owner, original, replacement, () => controller, () => current, write);
        controller = null;
        Check(lease.TryRestore() && writes == writesBefore + 1 && !lease.Active, "Destroyed animator releases the lease without restoring a live controller");
        controller = owner; current = original;
        lease.Acquire(owner, original, replacement, () => controller, () => current, write);
        Check(lease.TryRestore() && ReferenceEquals(controller, owner) && ReferenceEquals(current, original),
            "Cleanup restores ordinary native inspection while retaining the same live controller");
        lease.Acquire(owner, original, replacement, () => controller, () => current, write);
        Check(lease.TryRestore() && ReferenceEquals(controller, owner) && ReferenceEquals(current, original),
            "Second pose reuses the same controller and restores the native binding again");

        var cleanup = new PoseCleanupCheck();
        Check(!cleanup.Begin(100, 0, true, 1f), "Unknown native idle cannot arm a post-cleanup check");
        Check(cleanup.Begin(100, 42, true, 1f) && !cleanup.Begin(101, 99, true, 1.01f), "Cleanup check cannot overwrite its expected native state");
        Check(cleanup.Observe(100, true, true, 42, false, false, 1f) == PoseCleanupResult.Pending && cleanup.Active,
            "Same-frame controller/clip restoration cannot claim completed return");
        Check(cleanup.Observe(101, true, true, 42, false, false, 1.01f) == PoseCleanupResult.Confirmed && !cleanup.Active,
            "A later Unity frame must still match visible and native idle");
        cleanup.Begin(200, 42, true, 2f);
        Check(cleanup.Observe(201, true, true, 0, false, false, 2.01f) == PoseCleanupResult.Fault, "Lost visible idle after cleanup is a regression, not success");
        cleanup.Begin(300, 42, true, 3f);
        Check(cleanup.Observe(301, true, true, 42, true, false, 3.01f) == PoseCleanupResult.Fault, "Unexpected transition after cleanup is not stable idle");
        cleanup.Begin(400, 42, true, 4f);
        Check(cleanup.Observe(401, false, true, 0, false, false, 4.01f) == PoseCleanupResult.HandedOff, "Weapon/player change yields without forcing old animator state");
        cleanup.Begin(500, 42, true, 5f);
        Check(cleanup.Observe(501, true, false, 0, false, false, 5.01f) == PoseCleanupResult.HandedOff, "A new native operation is not overwritten by post-cleanup checks");
        cleanup.Begin(600, 42, false, 6);
        Check(cleanup.Observe(601, true, true, PoseMarker.SharedState, false, false, 6.01f) == PoseCleanupResult.Pending && cleanup.Active,
            "Reported early-release regression: native Idling with inspection still playing before cleanup waits instead of faulting");
        Check(cleanup.Observe(602, true, true, PoseMarker.SharedState, true, false, 6.02f) == PoseCleanupResult.Pending,
            "Interrupted native inspection may transition while its visual return settles");
        Check(cleanup.Observe(603, true, true, 42, false, false, 6.03f) == PoseCleanupResult.Confirmed,
            "Settled early-release playback confirms idle without synthetic actions");
        cleanup.Begin(700, 42, false, 7);
        Check(cleanup.Observe(701, true, true, PoseMarker.SharedState, false, true, 7.01f) == PoseCleanupResult.HandedOff && !cleanup.Active,
            "Native sprint cancels the operation ahead of its visual clip; cleanup yields without a global fault");
        Check(cleanup.Begin(702, 42, true, 7.02f), "Fresh explicit pose can be checked after sprint handoff");
        Check(cleanup.Observe(703, true, true, 42, true, true, 7.03f) == PoseCleanupResult.HandedOff,
            "Sprint beginning after normal short return owns the next-frame transition");
        cleanup.Begin(800, 42, false, 8);
        Check(!cleanup.Begin(801, 42, false, 11.99f), "Repeated cleanup cannot extend the settling deadline");
        Check(cleanup.Observe(802, true, true, PoseMarker.SharedState, false, false, 11.99f) == PoseCleanupResult.Pending,
            "Unfinished native playback stays blocked inside the bounded settling period");
        Check(cleanup.Observe(803, true, true, PoseMarker.SharedState, false, false, 12) == PoseCleanupResult.Fault && !cleanup.Active,
            "Inspection that never settles still faults; the safeguard is not removed");
        cleanup.Begin(900, 42, true, 9);
        Check(cleanup.Observe(901, true, true, 42, false, null, 9.01f) == PoseCleanupResult.Pending,
            "Unknown sprint state cannot count as a native handoff or confirmed idle");
        Check(cleanup.Observe(902, true, true, 42, false, false, 9.02f) == PoseCleanupResult.Confirmed,
            "Known stopped sprint plus stable idle resolves observation");
        cleanup.Begin(1000, 42, true, 10);
        Check(cleanup.Observe(1001, true, true, 42, false, null, 14) == PoseCleanupResult.Fault,
            "Persistently unknown sprint state times out instead of silently allowing actions");
        Check(MdrMotionTrial.Supports("5fbcc1d9016cce60e8341ab3") && MdrMotionTrial.Supports("628a60ae6b1d481ff772e9c8"),
            "MDR motion comparison is limited to the observed MCX and RD-704");
        Check(!MdrMotionTrial.Supports("5dcbd56fdbd3d91b3e5468d5") && !MdrMotionTrial.Supports("unknown"),
            "MDR itself and unverified weapons keep their native motion");
    }
}
