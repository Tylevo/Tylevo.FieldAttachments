namespace Tylevo.FieldAttachments.Core
{
    public enum MenuActivationMode { Hold, Toggle }

    // Modifier-only toggles defer closing until a plain tap ends, leaving chords
    // usable. Ordinary keys and mouse buttons toggle on each fresh press.
    public sealed class AltMenuActivation
    {
        public bool Open { get; private set; }
        public bool NeedsRelease { get; private set; }
        private bool _closeOnRelease;
        private int _pressFrame = -1;
        private MenuActivationMode? _mode;

        public bool Sample(bool held, bool down, MenuActivationMode mode, bool eligible, bool focused,
            bool close, bool chordUsed, int frame)
            => Sample(held, down, mode, eligible, focused, close, chordUsed, frame,
                physicalMainHeld: held, deferCloseForChord: true);

        public bool Sample(bool held, bool down, MenuActivationMode mode, bool eligible, bool focused,
            bool close, bool chordUsed, int frame, bool physicalMainHeld, bool deferCloseForChord)
        {
            if (_mode.HasValue && _mode.Value != mode) Cancel();
            _mode = mode;
            // Losing Ctrl in Ctrl+M is not a release of the owned M press.
            if (focused && !physicalMainHeld) NeedsRelease = false;
            if (!eligible || !focused || close) { Cancel(); return false; }
            if (NeedsRelease) return false;
            bool pressed = held && down && _pressFrame != frame;
            if (pressed) _pressFrame = frame;
            if (mode == MenuActivationMode.Toggle)
            {
                if (pressed)
                {
                    if (Open && deferCloseForChord) _closeOnRelease = true;
                    else if (Open) { Open = false; NeedsRelease = true; }
                    else { Open = true; _closeOnRelease = false; }
                }
                if (held && chordUsed && deferCloseForChord) _closeOnRelease = false;
                if (!held && _closeOnRelease) { Open = false; _closeOnRelease = false; }
            }
            else
            { if (!held) Open = false; else if (pressed) Open = true; }
            return Open;
        }

        public void Cancel() { Open = false; NeedsRelease = true; _closeOnRelease = false; }
    }
}
