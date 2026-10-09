using System;
using System.Linq;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestStmPresentation()
    {
        TestStmFovScale();
        TestPoseFrameRestore();
        TestStmTiltBlend();
        string key = StmPresentation.Controller + "/weapon_stmarms_stm_9_9x19_model.generated(Clone)";
        PoseMarker marker = new PoseMarker { WeaponTemplate = StmPresentation.Template, Controller = key,
            Layer = 1, State = PoseMarker.SharedState, Time = .181475952f };
        Check(StmPresentation.Requested(true, StmPresentation.Template) &&
            !StmPresentation.Requested(false, StmPresentation.Template) && !StmPresentation.Requested(true, McxInspectionSegment.Template),
            "STM opt-in is limited to its exact template; MCX routing stays available");
        Check(StmPresentation.MarkerDenial(marker, key) == "", "Recorded 18.15% native STM marker is accepted");
        Check(StmPresentation.MarkerDenial(null, key) != "" && StmPresentation.MarkerDenial(marker, key + "changed") != "",
            "Missing or wrong-controller STM marker cannot fall back to shared or MCX timing");
        marker.WeaponTemplate = McxInspectionSegment.Template;
        Check(StmPresentation.MarkerDenial(marker, key) != "", "MCX marker cannot arm STM tilt");
        marker.WeaponTemplate = StmPresentation.Template;
        marker.Layer = 0;
        Check(StmPresentation.MarkerDenial(marker, key) != "", "STM tilt needs the native Hands layer");
        marker.Layer = 1; marker.State++;
        Check(StmPresentation.MarkerDenial(marker, key) != "", "STM tilt rejects a different animation state");
        marker.State--;
        foreach (float time in new[] { float.NaN, float.PositiveInfinity, -.1f, 0, .95f, 1 })
        { marker.Time = time; Check(StmPresentation.MarkerDenial(marker, key) != "", "Invalid STM marker time rejected: " + time); }
        foreach (float angle in new[] { float.NaN, float.PositiveInfinity, -36, 36 })
            Check(!StmPresentation.ValidDegrees(angle), "Unbounded STM angle rejected: " + angle);
        Check(StmPresentation.ValidDegrees(-35) && StmPresentation.ValidDegrees(0) && StmPresentation.ValidDegrees(35),
            "STM angle endpoints and neutral setting are valid");

        var hand = new CardVector(.12f, -.25f, .5f);
        var muzzle = hand + new CardVector(-.4f, .65f, .15f);
        var elbow = hand + new CardVector(.2f, -.2f, -.12f);
        var lowered = hand + StmPresentation.Rotate(muzzle - hand, CardVector.UnitZ, 20);
        Check(lowered.Y < muzzle.Y && lowered.X < muzzle.X && Math.Abs(lowered.Z - muzzle.Z) < .00001f,
            "Positive view-axis tilt lowers the upper-left muzzle without changing its depth");
        foreach (CardVector axis in new[] { CardVector.UnitZ, CardVector.UnitY, new CardVector(.3f, -.4f, .8f) })
        foreach (float angle in new[] { -35f, 0, 20, 35 })
        {
            CardVector Move(CardVector v) => hand + StmPresentation.Rotate(v - hand, axis, angle);
            CardVector back = hand + StmPresentation.Rotate(Move(muzzle) - hand, axis, -angle);
            Check(CardVector.Distance(Move(hand), hand) < .00001f && CardVector.Distance(back, muzzle) < .00001f,
                "STM rotation fixes the firing-hand pivot and is reversible: " + angle);
            Check(Math.Abs(CardVector.Distance(Move(muzzle), Move(elbow)) - CardVector.Distance(muzzle, elbow)) < .00001f,
                "Rigid weapon/arm rotation preserves their spacing: " + angle);
        }
        bool rejected = false;
        try { StmPresentation.Rotate(muzzle, CardVector.Zero, 20); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Unavailable view axis cannot produce a guessed STM rotation");

        int[] native = { 1, 2, 3, 4, 5, 6 }, baseline = native.ToArray(), target = { 11, 12, 13, 14, 15, 16 };
        bool[] exists = { true, true, true, true, true, true };
        var lease = new PoseFrameLease<int>(6, i => native[i], (i, v) => native[i] = v, i => exists[i]);
        lease.Apply(target);
        rejected = false;
        try { lease.Apply(target); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && native.SequenceEqual(target), "Duplicate frame application is refused without accumulating tilt");
        Check(lease.Restore() && !lease.Active && native.SequenceEqual(baseline), "All six position/rotation components restore before native Update");
        bool stable = true;
        for (int frame = 0; frame < 1000; frame++)
        {
            native[0] = frame; // Independent native movement is the next frame's baseline.
            baseline = native.ToArray(); lease.Apply(target); lease.Restore();
            stable &= native.SequenceEqual(baseline);
        }
        Check(stable, "One thousand frames preserve fresh native motion with no offset drift");
        lease.Apply(target); native[1] = 999; lease.Restore();
        Check(native[0] == baseline[0] && native[1] == 999 && native.Skip(2).SequenceEqual(baseline.Skip(2)) && lease.Fault.Length != 0,
            "External rotation wins while separately owned position and other roots are restored");
        rejected = false;
        try { lease.Apply(target); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "External transform conflict latches against automatic reapplication");

        native = baseline.ToArray(); bool failWrite = true;
        lease = new PoseFrameLease<int>(6, i => native[i], (i, v) => {
            if (failWrite && i == 3) throw new InvalidOperationException("write failed"); native[i] = v;
        }, i => true);
        try { lease.Apply(target); } catch (InvalidOperationException) { }
        Check(native.SequenceEqual(baseline) && !lease.Active && lease.Fault.Length != 0,
            "Partial write failure rolls back already-owned components and latches uncertainty");

        bool failRestore = false;
        lease = new PoseFrameLease<int>(6, i => native[i], (i, v) => {
            if (failRestore && i == 1 && v == baseline[1]) throw new InvalidOperationException("restore failed"); native[i] = v;
        }, i => true);
        lease.Apply(target); failRestore = true;
        Check(!lease.Restore() && lease.Active && native[0] == baseline[0] && native[1] == target[1],
            "One failed restoration keeps ownership pending while restoring the remaining components");
        failRestore = false;
        Check(lease.Restore() && native.SequenceEqual(baseline) && lease.Fault.Length != 0,
            "Restoration can finish later but does not clear the uncertainty latch");

        lease = new PoseFrameLease<int>(6, i => native[i], (i, v) => native[i] = v, i => exists[i]);
        lease.Apply(target); exists[2] = exists[3] = false;
        Check(lease.Restore() && native[0] == baseline[0] && native[4] == baseline[4] && lease.Fault.Length != 0,
            "Destroyed/reparented root is abandoned without preventing restoration of surviving roots");
        exists[2] = exists[3] = true; native = baseline.ToArray();
        lease = new PoseFrameLease<int>(6, i => i == 4 ? throw new InvalidOperationException("read failed") : native[i],
            (i, v) => native[i] = v, i => true);
        try { lease.Apply(target); } catch (InvalidOperationException) { }
        Check(!lease.Active && native.SequenceEqual(baseline), "Incomplete baseline capture writes no transforms");
        native = new[] { 1, 2, 3, 4, 5, 6 }; baseline = native.ToArray();
        lease = new PoseFrameLease<int>(6, i => native[i], (i, v) => native[i] = Math.Min(v, 10), i => true);
        lease.Apply(target);
        Check(lease.Restore() && native.SequenceEqual(baseline), "Lease uses actual write read-back, not the requested normalized value");
    }

    private static void TestStmFovScale()
    {
        var x = new CardVector(1, 0, 0);
        var y = CardVector.UnitY;
        var z = CardVector.UnitZ;
        string Denial(CardVector scale, float? native) => StmPresentation.ScaleDenial(scale, x * scale.X, y * scale.Y, z * scale.Z, native);
        foreach (float depth in new[] { .65f, .825f, 1f })
        {
            Check(Denial(new CardVector(1, 1, depth), depth) == "", "Native FOV depth accepted: " + depth);
            Check(Denial(new CardVector(1, 1, 1), depth) == "", "Native reset phase accepted at FOV depth: " + depth);
        }
        Check(Denial(new CardVector(1, 1, .65f), .65f) == "", "Recorded 0.14.1 render-time (1,1,0.65) refusal is corrected");
        foreach (float? value in new float?[] { null, float.NaN, float.PositiveInfinity, 0, .64f, 1.01f })
            Check(Denial(new CardVector(1, 1, 1), value) != "", "Unknown/out-of-range native FOV fails closed: " + value);
        foreach (var scale in new[] { new CardVector(.8f, 1, .65f), new CardVector(1, .65f, 1), new CardVector(1, 1, 0),
            new CardVector(1, 1, -.65f), new CardVector(1, 1, .8f), new CardVector(1, 1, float.NaN) })
            Check(Denial(scale, .65f) != "", "Unexpected rig scale cannot masquerade as native FOV");
        var local = new CardVector(1, 1, .65f);
        Check(StmPresentation.ScaleDenial(local, x, y, z * .8f, .65f) != "", "Inherited depth stretch rejected");
        Check(StmPresentation.ScaleDenial(local, x * 2, y * 2, z * 1.3f, .65f) != "", "Inherited uniform enlargement rejected");
        Check(StmPresentation.ScaleDenial(local, x, new CardVector(.1f, (float)Math.Sqrt(.99), 0), z * .65f, .65f) != "",
            "Shear rejected even when all basis lengths match the native scale");
        Check(StmPresentation.ScaleDenial(local, -x, y, z * .65f, .65f) != "", "Reflected basis rejected even with correct lengths");
        Check(StmPresentation.ScaleDenial(local, new CardVector(float.NaN, 0, 0), y, z * .65f, .65f) != "",
            "Nonfinite world basis rejected");

        // Independent Numerics matrices/quaternions model Unity's shared parent transform.
        // Contact points are descendants of separately rotated weapon/arm roots, not just
        // three translated points. The native FOV must be applied once to the entire result.
        var hand = new System.Numerics.Vector3(.12f, -.25f, .5f);
        var support = new System.Numerics.Vector3(-.2f, -.1f, .58f);
        var muzzle = new System.Numerics.Vector3(-.38f, .4f, .7f);
        var roots = new[] { new System.Numerics.Vector3(.05f, -.03f, .3f),
            new System.Numerics.Vector3(-.3f, .1f, .15f), new System.Numerics.Vector3(.3f, .1f, .15f) };
        var rotations = new[] { System.Numerics.Quaternion.CreateFromYawPitchRoll(.3f, -.4f, .1f),
            System.Numerics.Quaternion.CreateFromYawPitchRoll(-.2f, .7f, .3f),
            System.Numerics.Quaternion.CreateFromYawPitchRoll(.6f, .1f, -.25f) };
        CardVector Cv(System.Numerics.Vector3 v) => new CardVector(v.X, v.Y, v.Z);
        System.Numerics.Vector3 Nv(CardVector v) => new System.Numerics.Vector3(v.X, v.Y, v.Z);
        foreach (float depth in new[] { .65f, .825f, 1f })
        foreach (float degrees in new[] { -35f, 0f, 20f, 35f })
        {
            var parentRotation = System.Numerics.Quaternion.CreateFromYawPitchRoll(.6f, -.3f, .15f);
            var parent = System.Numerics.Matrix4x4.CreateScale(1, 1, depth) *
                System.Numerics.Matrix4x4.CreateFromQuaternion(parentRotation) * System.Numerics.Matrix4x4.CreateTranslation(640, 2, 174);
            Check(StmPresentation.ScaleDenial(new CardVector(1, 1, depth),
                Cv(System.Numerics.Vector3.TransformNormal(System.Numerics.Vector3.UnitX, parent)),
                Cv(System.Numerics.Vector3.TransformNormal(System.Numerics.Vector3.UnitY, parent)),
                Cv(System.Numerics.Vector3.TransformNormal(System.Numerics.Vector3.UnitZ, parent)), depth) == "",
                "Native FOV with rotated/translated parent accepted: " + depth + "/" + degrees);
            // A slightly off-axis view also preserves grips under the shared FOV transform.
            var axis = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(.08f, -.04f, 1));
            var turn = System.Numerics.Quaternion.CreateFromAxisAngle(axis, degrees * (float)Math.PI / 180);
            System.Numerics.Vector3 MoveRoot(int i) => hand + Nv(StmPresentation.Rotate(Cv(roots[i] - hand), Cv(axis), degrees));
            System.Numerics.Vector3 Descendant(int i, System.Numerics.Vector3 contact)
            {
                var child = System.Numerics.Vector3.Transform(contact - roots[i], System.Numerics.Quaternion.Inverse(rotations[i]));
                var turnedChild = System.Numerics.Vector3.Transform(child, turn * rotations[i]);
                return System.Numerics.Vector3.Transform(MoveRoot(i) + turnedChild, parent);
            }
            var worldHand = System.Numerics.Vector3.Transform(hand, parent);
            Check(System.Numerics.Vector3.Distance(Descendant(0, hand), worldHand) < .0002f &&
                System.Numerics.Vector3.Distance(Descendant(2, hand), worldHand) < .0002f &&
                System.Numerics.Vector3.Distance(Descendant(0, support), Descendant(1, support)) < .0002f,
                "Weapon and both arm descendants keep their grip through native FOV: " + depth + "/" + degrees);
            var expected = System.Numerics.Vector3.Transform(hand + System.Numerics.Vector3.Transform(muzzle - hand, turn), parent);
            Check(System.Numerics.Vector3.Distance(Descendant(0, muzzle), expected) < .0002f,
                "Rig-space rotation precedes exactly one native FOV transform: " + depth + "/" + degrees);
        }
    }
}
