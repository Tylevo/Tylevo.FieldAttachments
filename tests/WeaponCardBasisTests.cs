using System;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = Tylevo.FieldAttachments.Core.CardVector;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static Vector3 Rotate(Vector3 v, Quaternion q)
    {
        var n = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(v.X, v.Y, v.Z), q);
        return new Vector3(n.X, n.Y, n.Z);
    }
    private static void TestWeaponCardBasis()
    {
        var bore = new Vector3(-.95f, .08f, .3f);
        Check(WeaponCardBasis.TryCreate(bore, Vector3.UnitY, out var right, out var up), "Broadside barrel establishes a card plane");
        Check(Vector3.Dot(right, Vector3.Normalize(-bore)) > .99999f, "Card long edge follows the barrel instead of the idle rotation delta");
        Check(right.Z < 0, "Receding muzzle makes the muzzle-side card edge farther away, not nearer");
        Check(Math.Abs(right.Y / right.X - bore.Y / bore.X) < .00001f, "Card roll follows the projected handguard slope with the correct sign");
        Check(Math.Abs(Vector3.Dot(right, up)) < .00001 && up.Y > 0 && Vector3.Cross(right, up).Z > 0,
            "Barrel plane is perpendicular, upright and facing the viewer");
        var opposite = new Vector3(-.95f, -.08f, -.3f);
        Check(WeaponCardBasis.TryCreate(opposite, Vector3.UnitY, out var otherRight, out _) && otherRight.Z > 0 && otherRight.Y > 0,
            "Opposite barrel tilt reverses both roll and depth naturally");
        Check(WeaponCardBasis.TryCreate(new Vector3(.95f, .08f, .3f), Vector3.UnitY, out otherRight, out _) && otherRight.X > 0 && otherRight.Z > 0,
            "A muzzle pointing right retains its physical slope without mirroring text");
        var endOn = new Vector3(-.1f, .03f, 1);
        Check(WeaponCardBasis.TryCreate(endOn, Vector3.UnitY, out otherRight, out _) && Math.Abs(otherRight.Z + .82f) < .00001,
            "Near-end-on readability limit preserves the receding muzzle depth sign");
        Check(Math.Abs(otherRight.Y / otherRight.X - endOn.Y / endOn.X) < .00001, "Foreshortening limit does not counter-rotate the visible barrel slope");
        Check(!WeaponCardBasis.TryCreate(Vector3.UnitZ, Vector3.UnitY, out _, out _), "Exactly end-on barrel has no guessed plane");
        Check(!WeaponCardBasis.TryCreate(Vector3.Zero, Vector3.UnitY, out _, out _) &&
            !WeaponCardBasis.TryCreate(bore, Vector3.Zero, out _, out _), "Missing barrel/top references fail to the existing flat layout");
        Check(!WeaponCardBasis.TryCreate(bore, bore, out _, out _), "Collinear top reference cannot create an arbitrary weapon plane");
        Check(!WeaponCardBasis.TryCreate(new Vector3(float.NaN, 0, 1), Vector3.UnitY, out _, out _) &&
            !WeaponCardBasis.TryCreate(bore, new Vector3(0, float.PositiveInfinity, 0), out _, out _), "Invalid bone data cannot reach the projected mesh");
        var cameraTurn = Quaternion.CreateFromYawPitchRoll(.7f, -.2f, .1f);
        var worldBore = Rotate(bore, cameraTurn);
        var worldTop = Rotate(Vector3.UnitY, cameraTurn);
        Check(WeaponCardBasis.TryCreate(Rotate(worldBore, Quaternion.Inverse(cameraTurn)),
            Rotate(worldTop, Quaternion.Inverse(cameraTurn)), out var turnedRight, out var turnedUp) &&
            Vector3.Distance(turnedRight, right) < .00001 && Vector3.Distance(turnedUp, up) < .00001,
            "Turning the player and gun together leaves their camera-relative card plane unchanged");
        // Perspective near/far edge check on the production plane and production projector.
        Vector3 center = new Vector3(0, 0, 2);
        Vector3 a = center - right * .4f + up * .15f;
        Vector3 b = center + right * .4f + up * .15f;
        Vector3 c = center - right * .4f - up * .15f;
        PanelPoint Project(Vector3 v) => new PanelPoint(960 + v.X / v.Z * 900, 540 - v.Y / v.Z * 900, v.Z);
        Check(PanelProjection.TryCreate(Project(a), Project(b), Project(c), out var panel), "Barrel basis feeds a valid perspective card plane");
        var tl = panel!.Map(0, 0); var tr = panel.Map(1, 0); var bl = panel.Map(0, 1); var br = panel.Map(1, 1);
        Check(tl.Depth > tr.Depth && bl.Depth > br.Depth, "Both card edges share the barrel's receding direction");
        float farHeight = (float)Math.Sqrt((bl.X - tl.X) * (bl.X - tl.X) + (bl.Y - tl.Y) * (bl.Y - tl.Y));
        float nearHeight = (float)Math.Sqrt((br.X - tr.X) * (br.X - tr.X) + (br.Y - tr.Y) * (br.Y - tr.Y));
        Check(farHeight < nearHeight, "Muzzle-side edge narrows in perspective instead of widening");
        Check(Math.Abs(WeaponCardBasis.UnitAtPanel(.001f, .5f, .8f) / .8f - .001f / .5f) < .000001,
            "A farther panel retains the same apparent center scale");
        Check(Math.Abs(WeaponCardBasis.UnitAtPanel(.001f, .5f, .3f) / .3f - .001f / .5f) < .000001,
            "A nearer panel retains the same apparent center scale");
        Check(WeaponCardBasis.UnitAtPanel(.001f, .5f, -.1f) == 0 && WeaponCardBasis.UnitAtPanel(float.NaN, .5f, .3f) == 0,
            "Invalid or behind-camera panel scale fails closed");
    }
}
