using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestStmTiltBlend()
    {
        const float marker = .1959064f, clipLength = 3.333333f;
        foreach (int fps in new[] { 30, 60, 144 })
        {
            var blend = new StmTiltBlend();
            float previous = 0;
            bool smoothRise = true, hasTiltBeforeHold = false;
            // Native state becomes stable at .03, before the recorded .1959064 hold.
            for (int frame = 0; frame <= fps; frame++)
            {
                float now = frame / (float)fps;
                float time = Math.Min(marker, .03f + now / clipLength);
                bool held = time >= marker;
                float weight = blend.Entry(now, time, marker, held);
                smoothRise &= weight >= previous && weight <= 1 && weight - previous < .13f;
                hasTiltBeforeHold |= !held && weight > .5f;
                previous = weight;
            }
            Check(smoothRise && hasTiltBeforeHold, "Tilt blends during the native rise without a second held-only start: " + fps);
            Check(blend.Weight == 1 && blend.Entry(2, marker + .003f, marker, true) == 1,
                "Recorded STM hold and frame overshoot preserve full stable tilt: " + fps);
            Check(blend.BeginReturn(3) && blend.Return(3) == 1, "O-close starts from the visible weight: " + fps);
            bool smoothReturn = true; previous = 1;
            for (int frame = 1; frame <= fps; frame++)
            {
                float weight = blend.Return(3 + frame / (float)fps);
                smoothReturn &= weight >= 0 && weight <= previous;
                previous = weight;
            }
            Check(smoothReturn && blend.Weight == 0 && !blend.Returning, "Outro fades to zero and ends without a render-frame dependency: " + fps);
        }

        var late = new StmTiltBlend();
        Check(late.Entry(10, marker + .002f, marker, true) == 0, "Late first observation at the held frame cannot snap to full tilt");
        Check(Math.Abs(late.Entry(10.09f, marker, marker, true) - .5f) < .001f, "Late observation uses a short eased fallback");
        Check(late.Entry(10.2f, marker, marker, true) == 1, "Late observation settles within the bounded minimum blend");
        late.BeginReturn(11);
        float halfway = late.Return(11.11f);
        Check(Math.Abs(halfway - .5f) < .001f && !late.BeginReturn(11.11f), "Duplicate close cannot restart or lengthen the return");
        Check(late.Entry(11.12f, marker, marker, true) == halfway, "Duplicate open cannot restart entry during return");
        Check(late.Return(11.3f) == 0 && !late.Returning, "Return still ends at its original deadline");

        var partial = new StmTiltBlend();
        partial.Entry(0, .03f, marker, false);
        float midRise = partial.Entry(.2f, .10f, marker, false);
        Check(midRise > 0 && midRise < 1, "Intermediate native rise has a partial visual weight");
        Check(partial.BeginReturn(.2f) && partial.Return(.2f) == midRise, "A return starts from the displayed partial weight, not full tilt");
        partial.Cancel();
        Check(partial.Weight == 0 && !partial.Returning && !partial.BeginReturn(.3f), "Interruption cancels immediately without an automatic retry");
        Check(partial.Entry(1, .03f, marker, false) == 0, "Fresh explicit opening starts with no stale visual weight");
        partial.Entry(2, marker, marker, true); partial.BeginReturn(2);
        Check(partial.Return(float.NaN) == 0 && !partial.Returning, "Unknown return time fails closed");
        partial.Entry(3, marker, marker, true); partial.Entry(4, marker, marker, true); partial.BeginReturn(4);
        Check(partial.Return(3) == 0 && !partial.Returning, "Reversed return clock cannot retain an indefinite offset");
        bool rejected = false;
        try { partial.Entry(5, float.NaN, marker, false); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Invalid native entry observation cannot write a visual weight");

        // Exercise the blend with the real lease and speed ownership, including native
        // movement during the outro. Every render restores the new native baseline.
        var frameBlend = new StmTiltBlend();
        float position = 2, speed = .8f;
        var lease = new PoseFrameLease<float>(1, _ => position, (_, value) => position = value, _ => true);
        var speedLease = new PoseSpeedLease();
        frameBlend.Entry(0, .03f, marker, false);
        frameBlend.Entry(1, marker, marker, true);
        speedLease.Acquire(() => speed, value => speed = value);
        lease.Apply(new[] { position + frameBlend.Weight });
        Check(lease.Restore() && position == 2, "Visible held offset restores before unpausing");
        frameBlend.BeginReturn(2);
        Check(speedLease.TryRelease() && speed == .8f && frameBlend.Returning, "Native outro resumes original speed while visual fade remains active");
        bool noDrift = true;
        for (int i = 0; i <= 20; i++)
        {
            position = 2 + i * .1f;
            float baseline = position;
            lease.Apply(new[] { baseline + frameBlend.Return(2 + i * .02f) });
            noDrift &= lease.Restore() && position == baseline;
        }
        Check(noDrift && !lease.Active && !frameBlend.Returning, "Outro animation updates its own baseline each frame; visual blend cannot accumulate drift");
        frameBlend.Entry(4, marker, marker, true); frameBlend.Entry(5, marker, marker, true);
        lease.Apply(new[] { position + frameBlend.Weight });
        frameBlend.Cancel();
        Check(lease.Restore() && !lease.Active && frameBlend.Weight == 0, "Safety interruption restores the current rendered frame immediately");
        Check(StmPresentation.DefaultDegrees == 30 && StmPresentation.ValidDegrees(30), "New 30-degree downward default remains inside the existing angle limit");
    }
}
