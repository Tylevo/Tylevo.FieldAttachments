namespace Tylevo.FieldAttachments.Core
{
    // The input owner supplies the binding and its label independently of display wording.
    public readonly struct ControlHint
    {
        private readonly string? _key, _caption;
        public string Key => _key ?? "";
        public string Caption => _caption ?? "";

        public ControlHint(string key, string caption)
        {
            _key = key;
            _caption = caption;
        }
    }
}
