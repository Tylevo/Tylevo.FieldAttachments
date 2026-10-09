using System;
using Vector3 = Tylevo.FieldAttachments.Core.CardVector;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestCardReadability()
    {
        // Sweep a synthetic rolled-top plane, then replay the recorded M4 below.
        var bore = new Vector3(-.8f, .08f, .6f);
        var nativeRight = Vector3.Normalize(-bore);
        var faceUp = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, nativeRight));
        var depthUp = Vector3.Cross(nativeRight, faceUp);
        Vector3 Top(float degrees) => faceUp * (float)Math.Cos(degrees * Math.PI / 180) +
            depthUp * (float)Math.Sin(degrees * Math.PI / 180);
        PanelProjection Project(Vector3 right, Vector3 up)
        {
            var center = new Vector3(0, 0, 1);
            PanelPoint Point(Vector3 p) => new PanelPoint(960 + 900 * p.X / p.Z, 540 - 900 * p.Y / p.Z, p.Z);
            var a = center - right * .216f + up * .095f;
            if (!PanelProjection.TryCreate(Point(a), Point(a + right * .432f), Point(a - up * .19f), out var plane))
                throw new Exception("Unreadable regression projection");
            return plane!;
        }
        float FaceHeight(PanelProjection p)
        {
            var a = p.Map(0, .5f); var b = p.Map(1, .5f);
            var t = p.Map(.5f, 0); var d = p.Map(.5f, 1);
            float x = b.X - a.X, y = b.Y - a.Y;
            return Math.Abs(x * (d.Y - t.Y) - y * (d.X - t.X)) / (float)Math.Sqrt(x*x + y*y);
        }
        Check(WeaponCardBasis.TryCreate(bore, Top(85), out var r, out var u), "Nearly edge-on top reference reaches the surface tuning path");
        Check(WeaponCardBasis.TryTune(r, u, .55f, 0, out var fixedR, out var fixedU) &&
            FaceHeight(Project(fixedR, fixedU)) > 120,
            "Near-edge-on card regains readable projected face height");
        Check(Math.Abs(fixedR.Y / fixedR.X - bore.Y / bore.X) < .00001 && fixedR.Z < 0 && fixedU.Z > 0,
            "Readability correction preserves barrel slope and native depth signs");
        Check(Math.Abs(Vector3.Dot(fixedR, fixedU)) < .00001 && Math.Abs(fixedU.LengthSquared() - 1) < .00001,
            "Pitch limit leaves an orthonormal card plane");
        bool readable = true, smooth = true, sameHomes = true;
        foreach (float strength in new[] { 0f, .55f, 1f })
        {
            Vector3 previous = default;
            for (int pitch = -85; pitch <= 85; pitch++)
            {
                if (!WeaponCardBasis.TryCreate(bore, Top(pitch), out r, out u) ||
                    !WeaponCardBasis.TryTune(r, u, strength, 0, out fixedR, out fixedU))
                { readable = false; continue; }
                var plane = Project(fixedR, fixedU);
                readable &= FaceHeight(plane) > 120;
                if (pitch > -85) smooth &= Vector3.Distance(previous, fixedU) < .08f;
                previous = fixedU;
                PanelProjection? last = null;
                foreach (AttachmentGroup group in Enum.GetValues(typeof(AttachmentGroup)))
                {
                    if (!DedicatedCardLayout.TryPlace(plane, group, 1920, 1080, 1100, 780, out var placed))
                    { sameHomes = false; continue; }
                    if (last != null) sameHomes &= Math.Abs(FaceHeight(last) - FaceHeight(placed!)) < .01f;
                    last = placed;
                }
            }
        }
        Check(readable, "Both pitch directions remain readable across 171 poses and three perspective settings");
        Check(smooth, "Pitch correction is continuous across the readability limit with no sign flips");
        Check(sameHomes, "All dedicated category homes retain the same corrected face height");
        var readableRight = Vector3.Normalize(new Vector3(.95f, 0, -.3f));
        var readableUp = new Vector3(0, .99f, 0) + Vector3.Cross(readableRight, Vector3.UnitY) * .1f;
        readableUp = Vector3.Normalize(readableUp);
        WeaponCardBasis.TryTune(readableRight, readableUp, 1, 0, out r, out u);
        Check(Vector3.Distance(r, readableRight) < .00001 && Vector3.Distance(u, readableUp) < .00001,
            "Ordinary readable native plane remains unchanged");
        WeaponCardBasis.TryTune(nativeRight, Top(85), .55f, 0, out r, out u);
        WeaponCardBasis.TryTune(nativeRight, Top(85), .55f, 5, out fixedR, out fixedU);
        Check(Vector3.Distance(Vector3.Cross(r, u), Vector3.Cross(fixedR, fixedU)) < .00001,
            "Clockwise trim retains the corrected surface normal");
        Check(!WeaponCardBasis.TryTune(nativeRight, -faceUp, .55f, 0, out _, out _),
            "Back-facing invalid basis still fails closed");

        // F10 20260919-025754-165-502bd: M4 native basis and old tuned basis.
        // This fixed projection checks readable geometry, not exact screenshot pixels.
        Check(WeaponCardBasis.TryCreate(new Vector3(-.2142f, -.0110f, .4981f),
            new Vector3(-.8135f, .1505f, -.5617f), out r, out u), "Recorded M4 native plane is valid");
        var oldPlane = Project(new Vector3(.8347f, .0210f, -.5503f), new Vector3(-.5405f, .2225f, -.8114f));
        Check(FaceHeight(oldPlane) < 50, "Recorded 0.5.4 M4 surface reproduces severe height collapse");
        Check(WeaponCardBasis.TryTune(r, u, .55f, 5, out fixedR, out fixedU) &&
            FaceHeight(Project(fixedR, fixedU)) > 120,
            "Recorded M4 surface becomes readable with its existing perspective and clockwise settings");
        Check(fixedR.Z < 0 && fixedU.Z < 0 && Vector3.Cross(fixedR, fixedU).Z > .6f,
            "Recorded M4 retains its receding directions while the face opens toward the viewer");

        // 0.5.5 F10 20260919-030818-844-e350f: no collapse, but text still shears.
        WeaponCardBasis.TryCreate(new Vector3(-.2250f, -.0105f, .4959f),
            new Vector3(-.8103f, .1749f, -.5593f), out r, out u);
        var leaning = Project(new Vector3(.8194f, -.0288f, -.5726f), new Vector3(-.3629f, .7471f, -.5569f));
        Check(WeaponCardBasis.TryTune(r, u, .55f, 5, out fixedR, out fixedU), "Recorded 0.5.5 M4 accepts gentler surface limits");
        var opened = Project(fixedR, fixedU);
        Check(FaceHeight(opened) > FaceHeight(leaning) * 1.25f,
            "Recorded M4 gains at least 25 percent perpendicular face height over 0.5.5");
        float Shear(PanelProjection p, float x)
        {
            var a = p.Map(x-.01f, .5f); var b = p.Map(x+.01f, .5f);
            var c = p.Map(x, .49f); var d = p.Map(x, .51f);
            float hx=b.X-a.X, hy=b.Y-a.Y, vx=d.X-c.X, vy=d.Y-c.Y;
            float cosine=(hx*vx+hy*vy)/(float)Math.Sqrt((hx*hx+hy*hy)*(vx*vx+vy*vy));
            return (float)(Math.Asin(Math.Min(1, Math.Abs(cosine))) * 180 / Math.PI);
        }
        Check(Shear(opened, .15f) < 12 && Shear(opened, .5f) < 12 && Shear(opened, .85f) < 12,
            "Installed and candidate card columns keep near-square text/icon angles");
        Check(Shear(opened, .15f) < Shear(leaning, .15f) * .5f,
            "Recorded installed-item column loses more than half its previous shear");
        WeaponCardBasis.TryTune(r, u, .55f, 0, out fixedR, out fixedU);
        Check(Math.Abs(fixedR.Y/fixedR.X-r.Y/r.X) < .00001 && fixedR.Z < 0 && fixedU.Z < 0,
            "Yaw limit retains the M4 barrel slope and signed pitch/depth");
        Check(opened.Map(0,.5f).Depth > opened.Map(1,.5f).Depth &&
            Math.Abs(opened.Map(0,0).Depth-opened.Map(0,1).Depth) > .01,
            "Gentler M4 surface retains three-dimensional perspective in both axes");
        WeaponCardBasis.TryCreate(new Vector3(-.4894f,-.0663f,.2799f),
            new Vector3(-.0808f,.9923f,.0936f), out r, out u);
        WeaponCardBasis.TryTune(r,u,.55f,5,out fixedR,out fixedU);
        Check(Vector3.Distance(fixedR,new Vector3(.9525f,.0412f,-.3018f)) < .001 &&
            Vector3.Distance(fixedU,new Vector3(-.0318f,.9988f,.0361f)) < .001,
            "Recorded readable MDR default angle is preserved within report precision");
    }
}
