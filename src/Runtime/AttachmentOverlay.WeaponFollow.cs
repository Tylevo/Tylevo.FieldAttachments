using System;
using Tylevo.FieldAttachments.Core;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class AttachmentOverlay
    {
        private readonly WeaponPanelAttachment?[] _crossAttachments = new WeaponPanelAttachment?[4];
        public bool PoseHeld { get; set; }
        private string _crossHiddenReason = "";
        private bool CrossPresentationHidden => ExperimentalCrossSelectors && _crossHiddenReason.Length != 0;
        public void SetCrossPresentation(string hiddenReason)
        {
            if (_crossHiddenReason == hiddenReason) return;
            _crossHiddenReason = hiddenReason; _sampleFrame = -1;
            if (CrossPresentationHidden) { ResetPointer(); HideCrossPresentation(); }
        }
        private void HideCrossPresentation()
        {
            _crossRoot.gameObject.SetActive(false); _crossProjection = null;
            _lastFollowing = false; _lastProjected = 0;
            _layoutReason = "Cross hidden: " + _crossHiddenReason;
            foreach (var panel in _crossGroups) panel.Projection = null;
            foreach (var group in _groups) { group.Projection = null; group.Leader.Show(false); }
            // Retain same-weapon local corners; the next held pose redraws them.
        }
        private void ClearCrossAttachments() => Array.Clear(_crossAttachments,0,_crossAttachments.Length);
        private void FollowCrossPanel(int index, float width, float height, bool current, ref PanelProjection? plane)
        {
            if (!FollowWeapon) { ClearCrossAttachments(); return; }
            var attached=_crossAttachments[index];
            // Rebind only at a verified held pose, never while the gun is being lowered,
            // modified or raised. Same-frame scans keep the original local corners.
            if (current && PoseHeld && (attached==null || !_anchors.OwnsPanel(attached)) && plane!=null)
                attached=_crossAttachments[index]=_anchors.AttachPanel(plane,width,height);
            if (attached==null) return;
            plane=null;
            if (current) _anchors.ProjectPanel(attached,width,height,out plane);
            _layoutReason="Cross cards attached to animated weapon frame"+(plane==null ? "; temporarily not projectable" : "");
        }
    }
}
