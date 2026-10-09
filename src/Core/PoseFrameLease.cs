using System;
using System.Collections.Generic;

namespace Tylevo.FieldAttachments.Core
{
    // One rendered frame only. Restore before native Update, never integrate yesterday's offset.
    // Each component is owned separately so an external rotation does not strand our position.
    public sealed class PoseFrameLease<T>
    {
        private readonly Func<int, T> _read;
        private readonly Action<int, T> _write;
        private readonly Func<int, bool> _exists;
        private readonly Func<int, T, T, bool> _restoredEquals;
        private readonly Func<T, string> _describe;
        private readonly T[] _before, _written;
        private readonly bool[] _owned;
        public bool Active { get; private set; }
        public string Fault { get; private set; } = "";
        public PoseFrameLease(int count, Func<int, T> read, Action<int, T> write, Func<int, bool> exists,
            Func<int, T, T, bool>? restoredEquals = null, Func<T, string>? describe = null)
        {
            _read = read; _write = write; _exists = exists;
            _restoredEquals = restoredEquals ?? ((i, a, b) => EqualityComparer<T>.Default.Equals(a, b));
            _describe = describe ?? (v => v?.ToString() ?? "null");
            _before = new T[count]; _written = new T[count]; _owned = new bool[count];
        }
        public void Apply(T[] values)
        {
            if (Active || Fault.Length != 0 || values.Length != _before.Length)
                throw new InvalidOperationException("Presentation frame already owned, faulted or invalid");
            // Complete the read phase before any write. Failure cannot leave a half-captured pose.
            for (int i = 0; i < values.Length; i++)
            {
                if (!_exists(i)) throw new InvalidOperationException("Presentation binding lost");
                _before[i] = _read(i);
            }
            Active = true;
            try
            {
                for (int i = 0; i < values.Length; i++)
                {
                    _written[i] = values[i]; _owned[i] = true;
                    _write(i, values[i]); _written[i] = _read(i); // Store Unity's actual quaternion read-back.
                }
            }
            catch (Exception e) { Latch("presentation write/read-back uncertain: " + e.GetBaseException().Message); Restore(); throw; }
        }
        public bool Restore()
        {
            bool pending = false;
            for (int i = 0; i < _owned.Length; i++)
            {
                if (!_owned[i]) continue;
                try
                {
                    if (_exists(i))
                    {
                        T current = _read(i);
                        if (EqualityComparer<T>.Default.Equals(current, _written[i]))
                        {
                            // Ownership remains exact. Only restoration validation may accept
                            // equivalent representations (e.g. a normalized Unity quaternion).
                            _write(i, _before[i]);
                            T restored = _read(i);
                            _written[i] = restored; // A rejected read-back is still our own write.
                            if (!_restoredEquals(i, restored, _before[i]))
                                throw new InvalidOperationException("restore read-back differs; expected=" + _describe(_before[i]) +
                                    " observed=" + _describe(restored));
                        }
                        else if (!_restoredEquals(i, current, _before[i]))
                            Latch("presentation component " + i + " changed externally; external value retained");
                    }
                    else Latch("presentation binding " + i + " destroyed or reparented; old binding abandoned");
                    _owned[i] = false;
                }
                catch (Exception e)
                {
                    pending = true;
                    Latch("presentation restore pending at component " + i + ": " + e.GetBaseException().GetType().Name +
                        ": " + e.GetBaseException().Message + "; inventory blocked");
                }
            }
            Active = pending;
            return !Active;
        }
        private void Latch(string reason) { if (Fault.Length == 0) Fault = reason; }
    }
}
