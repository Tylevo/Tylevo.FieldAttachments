using System;
using Tylevo.FieldAttachments.Core;

internal static partial class Program
{
    private static void TestAuthoredSupportHand()
    {
        foreach (float time in new[] { 0f, 1.5f, 2f, float.NaN, float.PositiveInfinity })
            Check(AuthoredSupportHand.Apply(.25f, 1, time, true) == .25f, "Idle/invalid time leaves native support weight unchanged");
        foreach (float firstPerson in new[] { 0f, .5f, -1f, 2f, float.NaN })
            Check(AuthoredSupportHand.Apply(0, firstPerson, .733f, true) == 0, "Unexpected body authority fails closed");
        Check(AuthoredSupportHand.Apply(0, 1, .733f, false) == 0, "Unowned/native inspection retains grip");
        Check(AuthoredSupportHand.Apply(0, 1, .733f, true) == 1, "Owned held pose releases native support IK");
        Check(AuthoredSupportHand.Apply(1, 1, .733f, true) == 1, "Already released native hand stays released");
        Check(AuthoredSupportHand.Apply(0, 1, .06f, true) == .5f, "Support release eases into entrance");
        Check(Math.Abs(AuthoredSupportHand.Apply(0, 1, 1.39f, true) - .5f) < .00001f, "Support grip resumes at return end");
        Check(float.IsNaN(AuthoredSupportHand.Apply(float.NaN, 1, .733f, true)), "Invalid native weight is never manufactured");
        float prior = 0;
        for (int i = 0; i <= 12; i++)
        {
            float value = AuthoredSupportHand.Apply(0, 1, i * .01f, true);
            Check(value >= prior && value <= 1, "Support entrance is bounded and monotonic"); prior = value;
        }
    }
}
