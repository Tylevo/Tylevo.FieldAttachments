using System;

namespace Tylevo.FieldAttachments.Core
{
    // A bounded visual translation, separate from native breathing and pose timing.
    public sealed class AttachmentIdleMotion
    {
        public const float DefaultIntensity = .5f;
        public const float MaximumOffset = .004f;
        private double _phase, _gain;
        private float _lastAt, _quietUntil;
        private bool _started;
        public CardVector Offset { get; private set; }

        public CardVector Sample(float now, float intensity, bool held, bool selecting, bool pressed)
        {
            if (!PoseMarker.Finite(now) || now < 0 || !PoseMarker.Finite(intensity))
            { Reset(); return Offset; }
            intensity = Math.Max(0, Math.Min(1, intensity));
            if (intensity == 0) { Reset(); return Offset; }
            if (_started && now < _lastAt) Reset();
            if (!_started) { _started = true; _lastAt = now; return Offset; }
            double elapsed = Math.Min(.1, now - _lastAt);
            _lastAt = now;
            if (selecting) _quietUntil = now + .25f;
            // Retain the last displayed offset, rather than snapping back when a
            // click begins. The caller includes the mouse-release frame here.
            if (held && pressed) return Offset;
            if (elapsed == 0) return Offset;
            if (held) _phase = (_phase + elapsed * (Math.PI * 2 / 4.6)) % (Math.PI * 4);
            double target = held && !selecting && now >= _quietUntil ? intensity : 0;
            double seconds = target < _gain ? .16 : .55;
            _gain = target + (_gain - target) * Math.Exp(-elapsed / seconds);
            if (_gain < .00001) _gain = 0;
            Offset = new CardVector(
                (float)(Math.Sin(_phase * .5) * .00045 * _gain),
                (float)(Math.Sin(_phase) * .0025 * _gain),
                (float)(Math.Sin(_phase * 2) * .0003 * _gain));
            return Offset;
        }

        public void Reset()
        {
            _phase = _gain = 0; _lastAt = _quietUntil = 0; _started = false;
            Offset = default;
        }
    }
}
