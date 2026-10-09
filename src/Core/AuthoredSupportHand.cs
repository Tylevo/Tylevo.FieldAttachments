using System;

namespace Tylevo.FieldAttachments.Core
{
    // Native VisualPass uses IK = 1 - Hand_Left * First_Person_Curve_Weight.
    // Release only the support grip; retain the native firing hand and other curves.
    public static class AuthoredSupportHand
    {
        public static float Blend(float seconds)
        {
            if (!PoseMarker.Finite(seconds) || seconds <= 0 || seconds >= 1.5f) return 0;
            float t = Math.Min(1, Math.Min(seconds / .12f, (1.5f - seconds) / .22f));
            return t * t * (3 - 2 * t);
        }

        public static float Apply(float native, float firstPerson, float seconds, bool owned)
        {
            if (!owned || !PoseMarker.Finite(native) || native < 0 || native > 1 ||
                !PoseMarker.Finite(firstPerson) || Math.Abs(firstPerson - 1) > .0001f) return native;
            return native + (1 - native) * Blend(seconds);
        }
    }
}
