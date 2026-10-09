using System;

namespace Tylevo.FieldAttachments.Core
{
    // Visual weight only: never pauses, seeks or cancels a native animation.
    public sealed class StmTiltBlend
    {
        public const float MinimumEntrySeconds = .18f, ReturnSeconds = .22f;
        public float Weight { get; private set; }
        public bool Returning { get; private set; }
        private bool _started;
        private float _entryAt, _entryTime, _returnAt, _returnFrom;

        public float Entry(float now, float clipTime, float markerTime, bool held)
        {
            if (!PoseMarker.Finite(now) || !PoseMarker.Finite(clipTime) || !PoseMarker.Finite(markerTime) || markerTime <= 0)
                throw new ArgumentException("Invalid STM blend timeline");
            if (Returning) return Weight; // A duplicate open cannot restart a return.
            if (!_started) { _started = true; _entryAt = now; _entryTime = clipTime; }
            float progress = held ? 1 : (clipTime - _entryTime) / Math.Max(.0001f, markerTime - _entryTime);
            // Reach full tilt with the saved frame. If observation starts late, avoid a
            // one-frame jump by retaining a short minimum blend through the hold.
            float weight = Smooth(Math.Min(progress, (now - _entryAt) / MinimumEntrySeconds));
            return Weight = Math.Max(Weight, weight);
        }
        public bool BeginReturn(float now)
        {
            if (Returning || Weight <= 0 || !PoseMarker.Finite(now)) return false;
            Returning = true; _returnAt = now; _returnFrom = Weight; return true;
        }
        public float Return(float now)
        {
            if (!Returning) return Weight;
            if (!PoseMarker.Finite(now) || now < _returnAt || now - _returnAt >= ReturnSeconds)
            { Cancel(); return 0; }
            return Weight = _returnFrom * (1 - Smooth((now - _returnAt) / ReturnSeconds));
        }
        public void Cancel() { Weight = 0; Returning = false; _started = false; }
        private static float Smooth(float t) { t = Math.Max(0, Math.Min(1, t)); return t * t * (3 - 2 * t); }
    }
}
