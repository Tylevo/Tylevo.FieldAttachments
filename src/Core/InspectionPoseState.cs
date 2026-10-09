using System;
using System.Globalization;
using System.Text;

namespace Tylevo.FieldAttachments.Core
{
    public enum PosePhase { None, Playing, Held, Returning }

    public sealed class PoseMarker
    {
        // Same Hands inspection state observed on MDR, M4 and AK-101. This is a
        // fallback for matching controllers, not permission to pause another state.
        public const int SharedState = 1355507738;
        public const float SharedTime = 0.27f;
        public static PoseMarker? Resolve(PoseMarker? saved, string weapon, string controller, bool sharedStatePresent, bool learn)
        {
            if (learn) return null;
            if (saved != null && saved.WeaponTemplate == weapon && saved.Controller == controller) return saved;
            return sharedStatePresent ? new PoseMarker { WeaponTemplate = weapon, Controller = controller,
                Layer = 1, State = SharedState, Time = SharedTime } : null;
        }
        public string WeaponTemplate = "", Controller = "";
        public int Layer, State;
        public float Time;
        public bool Matches(string weapon, string controller, int layer, int state) =>
            WeaponTemplate == weapon && Controller == controller && Layer == layer && State == state;
        public bool Reached(float time) => Finite(time) && time >= Time && time <= Math.Min(0.99f, Time + 0.12f);
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public string Encode() => "1|" + WeaponTemplate + "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Controller)) + "|" +
            Layer + "|" + State + "|" + Time.ToString("R", CultureInfo.InvariantCulture);
        public static PoseMarker? Decode(string text)
        {
            try
            {
                if (text.Length > 4096) return null;
                string[] p = text.Split('|');
                if (p.Length != 6 || p[0] != "1" || p[1].Length == 0 ||
                    !int.TryParse(p[3], out int layer) || layer < 0 || layer >= 16 ||
                    !int.TryParse(p[4], out int state) || state == 0 ||
                    !float.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float time) ||
                    !Finite(time) || time <= 0 || time >= 0.95f) return null;
                string controller = Encoding.UTF8.GetString(Convert.FromBase64String(p[2]));
                if (controller.Length == 0) return null;
                return new PoseMarker { WeaponTemplate = p[1], Controller = controller, Layer = layer, State = state, Time = time };
            }
            catch (FormatException) { return null; }
        }
    }

    // Completion stays gated on the visible idle state AND the native idle operation.
    // Native cancellation is attempted once, always with trigger=false; never retries.
    public sealed class PoseIdleReturn
    {
        public const float BlendSeconds = 0.2f;
        public bool Active { get; private set; }
        public bool CancelAttempted { get; private set; }
        private bool _timeoutReported;
        private float _startedAt;
        public bool Begin(float now, Action blend)
        {
            if (Active) return false;
            Active = true; CancelAttempted = false; _timeoutReported = false; _startedAt = now;
            // Retain observation even if the native call partially applied then threw.
            blend(); return true;
        }
        public bool Complete(bool atIdle, bool ownedReady, bool mayCancel, Action<bool> nativeCancel, Func<bool> nativeIdle)
        {
            if (!Active || !atIdle) return false;
            if (ownedReady && mayCancel && !CancelAttempted)
            { CancelAttempted = true; nativeCancel(false); }
            if (!nativeIdle()) return false;
            End(); return true;
        }
        public bool ReportTimeout(float now)
        {
            if (!Active || _timeoutReported || now - _startedAt <= 1) return false;
            _timeoutReported = true; return true;
        }
        public void End() { Active = false; }
    }

    // Holds only the animator acquired by our explicit inspect. Retains the restore
    // handle after a failed write so cleanup can retry; never assumes speed was 1.
    public sealed class PoseSpeedLease
    {
        private Func<float>? _read;
        private Action<float>? _write;
        public bool Active => _write != null;
        public float PreviousSpeed { get; private set; }
        public void Acquire(Func<float> read, Action<float> write)
        {
            if (Active) throw new InvalidOperationException("Pose already owns an animator speed.");
            float previous = read();
            if (!PoseMarker.Finite(previous) || previous <= 0) throw new InvalidOperationException("Animator is already paused or speed is unknown.");
            PreviousSpeed = previous; _read = read; _write = write;
            try { write(0); }
            catch { TryRelease(); throw; }
        }
        public bool TryRelease()
        {
            if (_write == null) return true;
            try
            {
                // An external nonzero speed wins; do not overwrite another owner.
                if (_read!() == 0) _write(PreviousSpeed);
                _read = null; _write = null;
                return true;
            }
            catch { return false; }
        }
        public void ForgetDestroyedAnimator() { _read = null; _write = null; }
    }

    public sealed class InspectionPoseState
    {
        public PosePhase Phase { get; private set; }
        public float StartedAt { get; private set; }
        public float HeldAt { get; private set; }
        public bool Active => Phase != PosePhase.None;
        public bool Start(float now)
        {
            if (Active) return false;
            Phase = PosePhase.Playing; StartedAt = now; return true;
        }
        public bool CanHold(bool sameOwnedOperation, bool startEventFired, bool stableState) =>
            Phase == PosePhase.Playing && sameOwnedOperation && startEventFired && stableState;
        public void Held(float now) { Phase = PosePhase.Held; HeldAt = now; }
        public void Returning() { if (Active) Phase = PosePhase.Returning; }
        public string Deadline(float now) => Phase == PosePhase.Playing && now - StartedAt > 8 ? "inspection pose was not reached" :
            Phase == PosePhase.Held && now - HeldAt > 30 ? "30-second pose hold limit" : "";
        public void End() { Phase = PosePhase.None; }
    }
}
