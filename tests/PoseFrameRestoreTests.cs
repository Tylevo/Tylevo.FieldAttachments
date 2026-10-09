using System;
using System.Linq;
using System.Numerics;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static bool RotationRestored(Vector4 a, Vector4 b) => StmPresentation.RestoredRotation(
        new CardVector(a.X, a.Y, a.Z), a.W, new CardVector(b.X, b.Y, b.Z), b.W);
    private static bool ComponentRestored(int i, Vector4 a, Vector4 b) => i % 2 == 0 ? a.Equals(b) : RotationRestored(a, b);
    private static Vector4 Qv(Quaternion q) => new Vector4(q.X, q.Y, q.Z, q.W);

    private static void TestPoseFrameRestore()
    {
        Vector4 rotation = Vector4.Normalize(new Vector4(.1234567f, -.3456789f, .2345678f, .8f));
        Vector4 original = rotation * 1.0000002f;
        Check(!original.Equals(Vector4.Normalize(original)) && RotationRestored(original, Vector4.Normalize(original)),
            "Quaternion normalization changes components while retaining the restored orientation");
        Check(RotationRestored(rotation, -rotation), "Opposite quaternion signs represent the same restored orientation");
        foreach (var invalid in new[] { Vector4.Zero, rotation * 2, new Vector4(float.NaN, 0, 0, 1), new Vector4(0, 0, 0, float.PositiveInfinity) })
            Check(!RotationRestored(invalid, rotation) && !RotationRestored(rotation, invalid), "Invalid/nonunit rotations cannot pass restoration");
        foreach (float degrees in new[] { .0005f, .002f, .01f, 1f, 20f })
        {
            var changed = Qv(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, degrees * MathF.PI / 180));
            Check(RotationRestored(Qv(Quaternion.Identity), changed) == (degrees < StmPresentation.RestoreAngleToleranceDegrees),
                "Restored rotation respects the 0.001 degree boundary: " + degrees);
        }

        Vector4[] baseline = { new Vector4(.1f, -.2f, .3f, 0), original,
            new Vector4(-.2f, .1f, .4f, 0), -original, new Vector4(.2f, .1f, .4f, 0), original };
        Vector4[] target = { baseline[0] + new Vector4(.02f, .03f, 0, 0), Qv(Quaternion.CreateFromYawPitchRoll(.4f, .2f, .1f)),
            baseline[2] + new Vector4(.03f, .02f, 0, 0), Qv(Quaternion.CreateFromYawPitchRoll(.3f, -.2f, .1f)),
            baseline[4] + new Vector4(.02f, -.02f, 0, 0), Qv(Quaternion.CreateFromYawPitchRoll(.1f, .2f, .3f)) };
        var native = baseline.ToArray();
        var writes = new int[6];
        void NormalizingSetter(int i, Vector4 v) { native[i] = i % 2 == 0 ? v : Vector4.Normalize(v); writes[i]++; }
        var strict = new PoseFrameLease<Vector4>(6, i => native[i], NormalizingSetter, i => true);
        strict.Apply(target);
        Check(!strict.Restore() && strict.Active && strict.Fault.Contains("component 1") && strict.Fault.Contains("restore read-back differs"),
            "Exact restoration reproduces the false fault with a normalizing setter (fixture)");

        native = baseline.ToArray(); Array.Clear(writes);
        var lease = new PoseFrameLease<Vector4>(6, i => native[i], NormalizingSetter, i => true, ComponentRestored);
        lease.Apply(target);
        Check(lease.Restore() && !lease.Active && lease.Fault.Length == 0 && native.Select((v, i) => ComponentRestored(i, v, baseline[i])).All(v => v),
            "Rotation-aware restoration clears all six owned components without a false fault");

        // Repeated normalization cannot accumulate the visual turn or reuse an old baseline.
        bool stable = true;
        for (int frame = 0; frame < 1000; frame++)
        {
            native[0] = baseline[0] + new Vector4(frame * .00001f, 0, 0, 0);
            native[1] = Qv(Quaternion.CreateFromYawPitchRoll(frame * .0001f, -.2f, .3f)) * 1.0000002f;
            var before = native.ToArray();
            lease.Apply(target);
            stable &= lease.Restore() && lease.Fault.Length == 0 && native.Select((v, i) => ComponentRestored(i, v, before[i])).All(v => v);
        }
        Check(stable, "One thousand normalized restore cycles preserve each fresh native baseline without tilt drift");

        native = baseline.ToArray(); Array.Clear(writes);
        lease = new PoseFrameLease<Vector4>(6, i => native[i], NormalizingSetter, i => true, ComponentRestored);
        lease.Apply(target);
        var written = native[1];
        var nearWritten = Qv(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, .0005f * MathF.PI / 180) *
            new Quaternion(written.X, written.Y, written.Z, written.W));
        Check(!nearWritten.Equals(written) && RotationRestored(nearWritten, written), "Fixture external write is within the restoration angular tolerance");
        native[1] = nearWritten;
        lease.Restore();
        Check(native[1].Equals(nearWritten) && writes[1] == 1 && lease.Fault.Contains("changed externally") &&
            native[0].Equals(baseline[0]) && native[2].Equals(baseline[2]),
            "Ownership remains exact: even a tiny external rotation wins and latches while other components restore");

        native = baseline.ToArray(); Array.Clear(writes);
        lease = new PoseFrameLease<Vector4>(6, i => native[i], NormalizingSetter, i => true, ComponentRestored);
        lease.Apply(target); native[1] = -Vector4.Normalize(baseline[1]);
        Check(lease.Restore() && writes[1] == 1 && lease.Fault.Length == 0,
            "Already-restored equivalent rotation is left untouched without another write");

        native = baseline.ToArray(); bool badPosition = false;
        lease = new PoseFrameLease<Vector4>(6, i => native[i], (i, v) => {
            NormalizingSetter(i, v);
            if (badPosition && i == 0) native[i] += new Vector4(.0000001f, 0, 0, 0);
        }, i => true, ComponentRestored);
        lease.Apply(target); badPosition = true;
        Check(!lease.Restore() && lease.Active && lease.Fault.Contains("component 0") &&
            lease.Fault.Contains("expected=") && lease.Fault.Contains("observed="),
            "Positions still require exact restoration; a tiny setter mismatch retains ownership with value evidence");
        string firstFault = lease.Fault;
        Check(!lease.Restore() && lease.Fault == firstFault, "Pending restoration retains its first failure reason without repeated changing logs");
        badPosition = false;
        Check(lease.Restore() && native[0].Equals(baseline[0]) && lease.Fault == firstFault,
            "Known rejected restore read-back remains owned until it can restore; uncertainty stays latched");
        bool refused = false;
        try { lease.Apply(target); } catch (InvalidOperationException) { refused = true; }
        Check(refused, "Recovery of owned values cannot authorize another visual application after uncertainty");

        native = baseline.ToArray(); bool badRotation = false;
        lease = new PoseFrameLease<Vector4>(6, i => native[i], (i, v) => {
            NormalizingSetter(i, v);
            if (badRotation && i == 1) {
                var q = native[i];
                native[i] = Qv(Quaternion.CreateFromAxisAngle(Vector3.UnitY, .01f * MathF.PI / 180) * new Quaternion(q.X, q.Y, q.Z, q.W));
            }
        }, i => true, ComponentRestored);
        lease.Apply(target); badRotation = true;
        Check(!lease.Restore() && lease.Active && lease.Fault.Contains("component 1"),
            "Meaningful rotation mismatch still blocks release and inventory");
        // A later external write must win even though the failed restore had kept ownership.
        Vector4 external = Qv(Quaternion.CreateFromYawPitchRoll(.7f, .6f, .5f)); native[1] = external;
        Check(lease.Restore() && native[1].Equals(external) && lease.Fault.Length != 0,
            "External rotation after a failed restore is not overwritten");

        native = baseline.ToArray(); bool throwRestore = false;
        lease = new PoseFrameLease<Vector4>(6, i => native[i], (i, v) => {
            if (throwRestore && i == 3) throw new InvalidOperationException("fixture native setter unavailable");
            NormalizingSetter(i, v);
        }, i => true, ComponentRestored);
        lease.Apply(target); throwRestore = true;
        Check(!lease.Restore() && lease.Fault.Contains("component 3") && lease.Fault.Contains("InvalidOperationException") &&
            lease.Fault.Contains("fixture native setter unavailable"), "Setter exceptions retain component, type and cause in the first fault");
    }
}
