namespace Tylevo.FieldAttachments.Core
{
    public enum MenuActivationMode { Hold, Toggle }

    // A modifier opens immediately. In Toggle mode a later plain tap closes on
    // release, leaving Alt+R and pointer interactions available while it is held.
    public sealed class AltMenuActivation
    {
        public bool Open { get; private set; }
        public bool NeedsRelease { get; private set; }
        private bool _closeOnRelease;
        private int _pressFrame = -1;
        private MenuActivationMode? _mode;

        public bool Sample(bool held, bool down, MenuActivationMode mode, bool eligible, bool focused,
            bool close, bool chordUsed, int frame)
        {
            if (_mode.HasValue && _mode.Value != mode) Cancel();
            _mode = mode;
            if (focused && !held) NeedsRelease = false;
            if (!eligible || !focused || close) { Cancel(); return false; }
            if (NeedsRelease) return false;
            bool pressed = held && down && _pressFrame != frame;
            if (pressed) _pressFrame = frame;
            if (mode == MenuActivationMode.Toggle)
            {
                if (pressed)
                {
                    if (Open) _closeOnRelease = true;
                    else { Open = true; _closeOnRelease = false; }
                }
                if (held && chordUsed) _closeOnRelease = false;
                if (!held && _closeOnRelease) { Open = false; _closeOnRelease = false; }
            }
            else
            { if (!held) Open = false; else if (pressed) Open = true; }
            return Open;
        }

        public void Cancel() { Open = false; NeedsRelease = true; _closeOnRelease = false; }
    }
}
