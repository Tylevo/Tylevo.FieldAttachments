using System;
using Vector3 = Tylevo.FieldAttachments.Core.CardVector;

namespace Tylevo.FieldAttachments.Core
{
    // Small value type keeps the production geometry independent of Unity and Numerics.Vectors
    // (the latter is not supplied by this client). Both core tests and the Unity adapter use it.
    public readonly struct CardVector
    {
        public readonly float X, Y, Z;
        public CardVector(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static CardVector Zero => default;
        public static CardVector UnitY => new CardVector(0, 1, 0);
        public static CardVector UnitZ => new CardVector(0, 0, 1);
        public float LengthSquared() => Dot(this, this);
        public static float Dot(CardVector a, CardVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static CardVector Cross(CardVector a, CardVector b) => new CardVector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static CardVector Normalize(CardVector v) => v * (1 / (float)Math.Sqrt(v.LengthSquared()));
        public static float Distance(CardVector a, CardVector b) => (float)Math.Sqrt((a - b).LengthSquared());
        public static CardVector operator +(CardVector a, CardVector b) => new CardVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static CardVector operator -(CardVector a, CardVector b) => new CardVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static CardVector operator -(CardVector v) => v * -1;
        public static CardVector operator *(CardVector v, float k) => new CardVector(v.X * k, v.Y * k, v.Z * k);
    }

    public static class WeaponCardBasis
    {
        public const float MaxSurfacePitchDegrees = 12;
        public const float MaxSurfaceYawDegrees = 30;
        // Camera coordinates (+Z forward). Long card edges run along the actual bore,
        // rather than applying a scaled Euler delta from an arbitrary opening pose.
        public static bool TryCreate(Vector3 towardMuzzle, Vector3 towardTop, out Vector3 right, out Vector3 up)
        {
            right = up = default;
            if (!Valid(towardMuzzle) || !Valid(towardTop) || towardMuzzle.LengthSquared() < .000001f ||
                towardTop.LengthSquared() < .000001f) return false;
            right = Vector3.Normalize(towardMuzzle);
            if (right.X < 0) right = -right; // Keep text readable on either exposed side.
            float xy = (float)Math.Sqrt(right.X * right.X + right.Y * right.Y);
            if (xy < .05f || right.X < .02f) return false; // End-on/vertical: no stable readable side plane.
            // Limit only end-on foreshortening. Preserve the projected bore slope and depth sign.
            const float maxDepth = .82f;
            if (Math.Abs(right.Z) > maxDepth)
            {
                float scale = (float)Math.Sqrt(1 - maxDepth * maxDepth) / xy;
                right = new Vector3(right.X * scale, right.Y * scale, Math.Sign(right.Z) * maxDepth);
            }
            up = towardTop - right * Vector3.Dot(towardTop, right);
            if (up.LengthSquared() < .000001f) return false;
            up = Vector3.Normalize(up);
            if (up.Y < 0) up = -up;
            return Valid(right) && Valid(up) && Vector3.Cross(right, up).Z > .05f;
        }

        // Tune only the card surface. Slot anchors and panel placement retain the native basis.
        // Reducing depth opens the face toward the viewer while retaining live weapon roll.
        public static bool TryTune(Vector3 nativeRight, Vector3 nativeUp, float perspective, float clockwiseDegrees,
            out Vector3 right, out Vector3 up, bool faceCamera = false)
        {
            // Billboard at the live weapon depth; native roll/skew and saved trim do not rotate its face.
            // A muzzle/top basis is unnecessary here, including on end-on inspection poses.
            if (faceCamera) { right = new Vector3(1, 0, 0); up = Vector3.UnitY; return true; }
            right = up = default;
            if (!Valid(nativeRight) || !Valid(nativeUp) || !Finite(perspective) || !Finite(clockwiseDegrees) ||
                perspective < 0 || perspective > 1 || Math.Abs(clockwiseDegrees) > 15) return false;
            right = new Vector3(nativeRight.X, nativeRight.Y, nativeRight.Z * perspective);
            if (right.LengthSquared() < .000001f) return false;
            right = Vector3.Normalize(right);
            // Keep the face wide enough for thumbnails/text on end-on inspection poses.
            // Clamp depth without changing the projected barrel slope or receding side.
            float maxDepth = (float)Math.Sin(MaxSurfaceYawDegrees * Math.PI / 180);
            if (Math.Abs(right.Z) > maxDepth)
            {
                float xy = (float)Math.Sqrt(right.X * right.X + right.Y * right.Y);
                if (xy < .05f) return false;
                float scale = (float)Math.Sqrt(1 - maxDepth * maxDepth) / xy;
                right = new Vector3(right.X * scale, right.Y * scale, Math.Sign(right.Z) * maxDepth);
            }
            up = new Vector3(nativeUp.X, nativeUp.Y, nativeUp.Z * perspective);
            up -= right * Vector3.Dot(up, right);
            if (up.LengthSquared() < .000001f) return false;
            up = Vector3.Normalize(up);
            // A rolled weapon can put its magazine/top direction almost along the camera
            // ray. Scaling Z alone still leaves a paper-thin card after normalization.
            // Limit pitch around the tuned bore, preserving that bore's roll and depth.
            Vector3 faceUp = Vector3.Cross(Vector3.UnitZ, right);
            if (faceUp.LengthSquared() < .000001f) return false;
            faceUp = Vector3.Normalize(faceUp);
            if (Vector3.Dot(up, faceUp) <= 0) return false;
            float minFacing = (float)Math.Cos(MaxSurfacePitchDegrees * Math.PI / 180);
            if (Vector3.Dot(up, faceUp) < minFacing)
            {
                Vector3 depthUp = Vector3.Cross(right, faceUp);
                float sign = Math.Sign(Vector3.Dot(up, depthUp));
                up = faceUp * minFacing + depthUp * (sign * (float)Math.Sqrt(1 - minFacing * minFacing));
            }
            float angle = clockwiseDegrees * (float)Math.PI / 180;
            float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
            Vector3 turnedRight = right * c - up * s;
            up = right * s + up * c; right = turnedRight;
            return Valid(right) && Valid(up) && right.X > .02f && up.Y > 0 && Vector3.Cross(right, up).Z > .05f;
        }

        // Offsetting a tilted panel changes its distance. Size it at that distance,
        // otherwise identical cards near/far from the camera get wildly different sizes.
        public static float UnitAtPanel(float anchorUnit, float anchorDepth, float panelDepth)
        {
            if (!Finite(anchorUnit) || !Finite(anchorDepth) || !Finite(panelDepth) ||
                anchorUnit <= 0 || anchorDepth <= .02f || panelDepth <= .02f) return 0;
            return anchorUnit * panelDepth / anchorDepth;
        }
        private static bool Valid(Vector3 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }
}
