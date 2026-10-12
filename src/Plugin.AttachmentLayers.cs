using Tylevo.FieldAttachments.Core;

namespace Tylevo.FieldAttachments
{
    public sealed partial class Plugin
    {
        private void CycleAttachmentLayer()
        {
            if (!_visible || !_enabled.Value || !PointerLocalOwnership() || _overlay?.LayerTransitionActive == true || _clicks.Busy ||
                _replacement.Pending || _probe.Session.Busy || _probe.Session.Blocked) return;
            _overlay?.ResetPointer();
            Capture("attachment layer changed");
            _state.CycleLayer();
            _clickStatus = _state.Layer == 0 ? "Quick attachments" :
                _state.Snapshot.Multitool == null ? "Carry a Leatherman Multitool to change these parts." :
                "Multitool parts - required parts can only be replaced.";
            Trace("ATTACHMENT_LAYER " + _state.Layer);
            Render();
        }
    }
}
