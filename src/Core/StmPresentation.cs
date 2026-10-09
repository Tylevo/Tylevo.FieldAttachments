using System;

namespace Tylevo.FieldAttachments.Core
{
    public static class StmPresentation
    {
        // Native STM marker and live hierarchy verified on the installed 4.1.5 client.
        public const string Template = "60339954d62c9b14ed777c06";
        public const string Controller = "weapon_stmarms_stm_9_9x19";
        public const float DefaultDegrees = 30, MaxDegrees = 35;
        public static bool Requested(bool enabled, string template) => enabled && template == Template;
        public static bool ValidDegrees(float degrees) => PoseMarker.Finite(degrees) && Math.Abs(degrees) <= MaxDegrees;
        public static string MarkerDenial(PoseMarker? marker, string controller)
        {
            return marker != null && marker.Matches(Template, controller, 1, PoseMarker.SharedState) &&
                PoseMarker.Finite(marker.Time) && marker.Time > 0 && marker.Time < .95f ? "" :
                "STM trial needs its native saved frame: close attachment mode, use Shift + attachment key, then F4";
        }
        // FirstPersonStrategy scales HandsHierarchy.Self to (1,1,RibcageScaleCurrent)
        // for rendering and resets it to one. EFT.Player's FOV 50..75 range maps to 1..0.65.
        // Accept only that native scale, with no inherited reflection, stretch or shear.
        public static string ScaleDenial(CardVector localScale, CardVector worldX, CardVector worldY, CardVector worldZ, float? nativeDepth)
        {
            const float tolerance = .0001f;
            if (!nativeDepth.HasValue || !PoseMarker.Finite(nativeDepth.Value) ||
                nativeDepth.Value < .65f - tolerance || nativeDepth.Value > 1 + tolerance)
                return "native FOV depth unavailable/out of range";
            if (!Finite(localScale) || Math.Abs(localScale.X - 1) > tolerance || Math.Abs(localScale.Y - 1) > tolerance ||
                (Math.Abs(localScale.Z - 1) > tolerance && Math.Abs(localScale.Z - nativeDepth.Value) > tolerance))
                return "scale differs from native FOV/reset";
            if (!Finite(worldX) || !Finite(worldY) || !Finite(worldZ) ||
                Math.Abs(worldX.LengthSquared() - 1) > tolerance || Math.Abs(worldY.LengthSquared() - 1) > tolerance ||
                Math.Abs(worldZ.LengthSquared() - localScale.Z * localScale.Z) > tolerance ||
                Math.Abs(CardVector.Dot(worldX, worldY)) > tolerance || Math.Abs(CardVector.Dot(worldX, worldZ)) > tolerance ||
                Math.Abs(CardVector.Dot(worldY, worldZ)) > tolerance ||
                CardVector.Dot(CardVector.Cross(worldX, worldY), worldZ) <= 0)
                return "inherited rig scale/shear/reflection";
            return "";
        }
        private static bool Finite(CardVector v) => PoseMarker.Finite(v.X) && PoseMarker.Finite(v.Y) && PoseMarker.Finite(v.Z);

        public const float RestoreAngleToleranceDegrees = .001f;
        // Compare orientation after our own restore, not quaternion storage bits. Double
        // precision avoids float dot/acos rounding that could hide a larger angular error.
        // Near-unit inputs only; q and -q encode the same rotation.
        public static bool RestoredRotation(CardVector a, float aw, CardVector b, float bw)
        {
            if (!Finite(a) || !Finite(b) || !PoseMarker.Finite(aw) || !PoseMarker.Finite(bw)) return false;
            double an = (double)a.X * a.X + (double)a.Y * a.Y + (double)a.Z * a.Z + (double)aw * aw;
            double bn = (double)b.X * b.X + (double)b.Y * b.Y + (double)b.Z * b.Z + (double)bw * bw;
            if (Math.Abs(an - 1) > .001 || Math.Abs(bn - 1) > .001) return false;
            double invA = 1 / Math.Sqrt(an), invB = 1 / Math.Sqrt(bn);
            if ((double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z + (double)aw * bw < 0) invB = -invB;
            double x = a.X * invA - b.X * invB, y = a.Y * invA - b.Y * invB;
            double z = a.Z * invA - b.Z * invB, w = aw * invA - bw * invB;
            double chord = 2 * Math.Sin(RestoreAngleToleranceDegrees * Math.PI / 720);
            return x * x + y * y + z * z + w * w <= chord * chord;
        }

        // Rigid in the shared unscaled rig space. Native FOV compression is inherited once
        // by all three roots afterwards; do not rotate world positions through that scale.
        public static CardVector Rotate(CardVector value, CardVector axis, float degrees)
        {
            if (!ValidDegrees(degrees) || !PoseMarker.Finite(axis.LengthSquared()) || axis.LengthSquared() < .0001f)
                throw new ArgumentException("Invalid STM presentation rotation");
            axis = CardVector.Normalize(axis);
            float radians = degrees * (float)Math.PI / 180;
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            return value * c + CardVector.Cross(axis, value) * s + axis * (CardVector.Dot(axis, value) * (1 - c));
        }
    }
}
