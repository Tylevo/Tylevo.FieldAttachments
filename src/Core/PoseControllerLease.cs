using System;

namespace Tylevo.FieldAttachments.Core
{
    // Owns one clip binding, never the live controller reference. Restoring a bare
    // controller can reset its state/parameters; cleanup must only restore this clip.
    public sealed class PoseBindingLease<TController, T> where TController : class where T : class
    {
        private TController? _controller;
        private Func<TController?>? _readController;
        private Func<T?>? _read;
        private Action<T>? _write;
        private T? _original, _replacement;
        public bool Active => _write != null;
        public bool ExternalChange { get; private set; }
        public void Acquire(TController controller, T original, T replacement,
            Func<TController?> readController, Func<T?> read, Action<T> write)
        {
            if (Active || !ReferenceEquals(readController(), controller) || ReferenceEquals(original, replacement) || !ReferenceEquals(read(), original))
                throw new InvalidOperationException("Clip/controller ownership changed or already acquired.");
            _controller = controller; _readController = readController;
            _original = original; _replacement = replacement; _read = read; _write = write; ExternalChange = false;
            try
            {
                write(replacement);
                if (!ReferenceEquals(readController(), controller) || !ReferenceEquals(read(), replacement))
                    throw new InvalidOperationException("Clip replacement was not confirmed.");
            }
            catch { TryRestore(); throw; }
        }
        public bool TryRestore()
        {
            if (_write == null) return true;
            try
            {
                if (!ReferenceEquals(_readController!(), _controller))
                { ExternalChange = true; Clear(); return true; }
                T? current = _read!();
                if (ReferenceEquals(current, _replacement))
                {
                    _write(_original!);
                    if (!ReferenceEquals(_readController(), _controller) || !ReferenceEquals(_read(), _original)) return false;
                }
                else ExternalChange = !ReferenceEquals(current, _original);
                Clear();
                return true;
            }
            catch { return false; }
        }
        private void Clear()
        { _read = null; _write = null; _original = _replacement = null; _controller = null; _readController = null; }
    }

    public static class MdrMotionTrial
    {
        // Comparison weapons observed in the user's two videos. Not universal compatibility.
        public static bool Supports(string template) => template == "5fbcc1d9016cce60e8341ab3" || template == "628a60ae6b1d481ff772e9c8";
        public const float HoldTime = 0.275553823f;
    }

    public enum PoseCleanupResult { Pending, Confirmed, HandedOff, Fault }
    public sealed class PoseCleanupCheck
    {
        public const float SettleSeconds = 4;
        private int _frame, _idle;
        private float _started;
        private bool _wasIdle;
        public bool Active { get; private set; }
        public string Reason { get; private set; } = "not requested";
        public bool Begin(int frame, int idle, bool idleBeforeCleanup, float now)
        {
            if (Active || idle == 0) return false;
            _frame = frame; _idle = idle; _wasIdle = idleBeforeCleanup; _started = now;
            Reason = idleBeforeCleanup ? "checking preserved idle" : "native operation ended before visible inspection; waiting for playback";
            Active = true; return true;
        }
        public PoseCleanupResult Observe(int frame, bool sameContext, bool nativeIdle, int state, bool transition, bool? sprinting, float now)
        {
            if (!Active || frame <= _frame) return PoseCleanupResult.Pending;
            if (!sameContext) return Finish(PoseCleanupResult.HandedOff, "weapon/player/animator changed");
            if (!nativeIdle) return Finish(PoseCleanupResult.HandedOff, "another native operation owns the hands");
            if (sprinting == true) return Finish(PoseCleanupResult.HandedOff, "native sprint owns presentation");
            if (sprinting == false)
            {
                if (state == _idle && !transition) return Finish(PoseCleanupResult.Confirmed, "visible/native idle confirmed after cleanup");
                // Only call this a lost-idle regression if idle existed BEFORE cleanup.
                if (_wasIdle) return Finish(PoseCleanupResult.Fault, "previously stable idle changed after cleanup without native handoff");
            }
            if (now - _started >= SettleSeconds)
                return Finish(PoseCleanupResult.Fault, sprinting == null ? "native sprint state unavailable after cleanup" : "visible idle did not settle after native interruption");
            return PoseCleanupResult.Pending; // Observe only; do not blend, replay, cancel or retry.
        }
        private PoseCleanupResult Finish(PoseCleanupResult result, string reason)
        { Active = false; Reason = reason; return result; }
    }
}
