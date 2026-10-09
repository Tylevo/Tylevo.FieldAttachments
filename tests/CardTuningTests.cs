using System;
using Vector3 = Tylevo.FieldAttachments.Core.CardVector;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestCardTuning()
    {
        // Native directions from the user's 0.5.2 MDR F10 observation; no game objects required.
        Check(WeaponCardBasis.TryCreate(new Vector3(-.4867f, -.0550f, .2820f),
            new Vector3(-.0668f, .9947f, .0787f), out var nativeRight, out var nativeUp), "Recorded MDR plane is valid");
        Check(WeaponCardBasis.TryTune(nativeRight, nativeUp, .55f, 5, out var right, out var up), "Default facing/clockwise tuning accepts the recorded MDR plane");
        Check(Math.Abs(right.Z) < Math.Abs(nativeRight.Z) && right.Z < 0,
            "Default tuning opens the MDR card face while retaining its receding side");
        Check(right.Y / right.X < nativeRight.Y / nativeRight.X,
            "Positive clockwise trim turns the recorded card long edge rightward");
        Check(Math.Abs(right.LengthSquared() - 1) < .00001 && Math.Abs(up.LengthSquared() - 1) < .00001 &&
            Math.Abs(Vector3.Dot(right, up)) < .00001 && Vector3.Cross(right, up).Z > 0,
            "Tuned plane remains orthonormal, upright and front-facing");
        Check(WeaponCardBasis.TryTune(nativeRight, nativeUp, 1, 0, out var originalRight, out var originalUp) &&
            Vector3.Distance(originalRight, nativeRight) < .00001 && Vector3.Distance(originalUp, nativeUp) < .00001,
            "Strength one and zero trim restore the previous native surface orientation");
        Check(WeaponCardBasis.TryTune(nativeRight, nativeUp, 0, 0, out var faceRight, out var faceUp) &&
            faceRight.Z == 0 && faceUp.Z == 0 && Math.Abs(faceRight.Y / faceRight.X - nativeRight.Y / nativeRight.X) < .00001,
            "Strength zero removes depth skew while retaining live weapon roll");
        Check(WeaponCardBasis.TryTune(nativeRight, nativeUp, .55f, 0, out var unturned, out _) &&
            Math.Abs(unturned.Y / unturned.X - nativeRight.Y / nativeRight.X) < .00001,
            "Perspective adjustment alone does not counter-tilt the weapon");
        WeaponCardBasis.TryTune(nativeRight, nativeUp, .55f, -5, out var leftTurn, out _);
        Check(leftTurn.Y / leftTurn.X > unturned.Y / unturned.X && right.Y / right.X < unturned.Y / unturned.X,
            "Clockwise trim supports predictable tuning in both directions");

        // Fixed projection fixture compares the same card center/size before and after tuning.
        // This checks projection behavior, not a claim of exact screenshot pixel matching.
        PanelProjection Card(Vector3 r, Vector3 u)
        {
            var center = new Vector3(-.25f, .16f, 1);
            PanelPoint Project(Vector3 v) => new PanelPoint(960 + 900 * v.X / v.Z, 540 - 900 * v.Y / v.Z, v.Z);
            var a = center - r * .22f + u * .095f;
            if (!PanelProjection.TryCreate(Project(a), Project(a + r * .44f), Project(a - u * .19f), out var card))
                throw new Exception("Invalid tuned projection fixture");
            return card!;
        }
        var before = Card(nativeRight, nativeUp); var after = Card(right, up);
        float Height(PanelProjection p, float x) { var a = p.Map(x, 0); var b = p.Map(x, 1); return (float)Math.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Y-a.Y)*(b.Y-a.Y)); }
        Check(after.Right - after.Left > before.Right - before.Left, "Tuning opens a narrow off-center panel without enlarging its world dimensions");
        Check(Height(after, 0) / Height(after, 1) > Height(before, 0) / Height(before, 1), "Near/far edge height difference decreases with weaker perspective");
        Check(Math.Abs(after.Map(.5f, .5f).X - before.Map(.5f, .5f).X) < .001 &&
            Math.Abs(after.Map(.5f, .5f).Y - before.Map(.5f, .5f).Y) < .001,
            "Surface tuning rotates around the same center rather than translating its attachment anchor");
        var flat = Card(faceRight, faceUp);
        Check(Math.Abs(Height(flat, 0) - Height(flat, 1)) < .001 && flat.Map(0, 0).Depth == flat.Map(1, 1).Depth,
            "Zero strength produces an undistorted face at the same live center");
        WeaponCardBasis.TryCreate(new Vector3(-.8f, .2f, -.4f), Vector3.UnitY, out var movedNative, out var movedTop);
        Check(WeaponCardBasis.TryTune(movedNative, movedTop, .55f, 5, out var movedRight, out _) &&
            movedRight.Z > 0 && movedRight.Y < 0 && Vector3.Distance(movedRight, right) > .1,
            "Tuning still follows a different animated roll and opposite depth direction");
        Check(!WeaponCardBasis.TryTune(nativeRight, nativeUp, float.NaN, 5, out _, out _) &&
            !WeaponCardBasis.TryTune(nativeRight, nativeUp, .55f, float.PositiveInfinity, out _, out _), "Nonfinite tuning cannot reach the mesh");
        Check(!WeaponCardBasis.TryTune(nativeRight, nativeUp, -.1f, 0, out _, out _) &&
            !WeaponCardBasis.TryTune(nativeRight, nativeUp, 1.1f, 0, out _, out _) &&
            !WeaponCardBasis.TryTune(nativeRight, nativeUp, .55f, 16, out _, out _), "Out-of-range display settings fail closed");
        Check(!WeaponCardBasis.TryTune(Vector3.Zero, nativeUp, .55f, 5, out _, out _) &&
            !WeaponCardBasis.TryTune(nativeRight, nativeRight, .55f, 5, out _, out _), "Degenerate surface basis fails closed");
    }
}
