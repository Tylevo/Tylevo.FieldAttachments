namespace Tylevo.FieldAttachments.Core
{
    // Raw physical input is separate from the requested pose/UI lifetime.
    public sealed class PoseActivation
    {
        public bool Open { get; private set; }
        private bool _needsRelease;
        private bool? _toggle;
        public bool Sample(bool held, bool down, bool toggle)
        {
            if (_toggle.HasValue && _toggle.Value!=toggle) Cancel();
            _toggle=toggle;
            if (!held) _needsRelease=false;
            if (_needsRelease) return Open=false;
            if (toggle) { if (down) Open=!Open; }
            else Open=held;
            return Open;
        }
        public void Cancel() { Open=false; _needsRelease=true; }
    }
}
