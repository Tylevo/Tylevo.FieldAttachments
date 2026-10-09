using System;
using System.Linq;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class InspectionPose
    {
        private Animator? _supportBody;
        private bool _supportLease;
        private float _supportNative, _supportFirstPerson, _supportBodyFirstPerson, _supportApplied;

        private void PrepareSupportHand(object player, Animator weapon)
        {
            if (!_customStm) return;
            _supportBody = _read.Get(_read.Get(player, "BodyAnimatorCommon"), "_animator") as Animator;
            if (_supportBody == null || _supportBody == weapon || !_supportBody.isInitialized ||
                !new[] { "Hand_Left", "First_Person_Curve_Weight" }.All(n => _supportBody.parameters.Any(p => p.name == n && p.type == AnimatorControllerParameterType.Float)))
                throw new InvalidOperationException("authored support hand requires separate audited body float parameters");
            AuthoredSupportHandPatch.Acquire(FilterSupportHand); _supportLease = true;
            _supportNative = _supportFirstPerson = _supportBodyFirstPerson = _supportApplied = float.NaN;
        }

        private float FilterSupportHand(object player, float native)
        {
            bool owned = _supportLease && _customStm && CustomStmAnimationEnabled && !_learning && !_customStmAwaiting &&
                ReferenceEquals(player, _context?.Player) && _supportBody != null && _animator != null &&
                ReferenceEquals(_read.Get(_read.Get(player, "BodyAnimatorCommon"), "_animator"), _supportBody) &&
                _motion.Fault.Length == 0 && (_state.Phase == PosePhase.Playing || _state.Phase == PosePhase.Held || _customStmReturn.Pending) &&
                Application.isFocused && SameLiveAnimator() && OwnedOperation() && StartEventFired() && CustomStmClipMatches() &&
                ReadAccess.Text(_read.Get(player, "PointOfView")) == "FirstPerson" && NeutralHands();
            if (!owned || !(player is EFT.Player localPlayer)) return native;
            _supportNative = native;
            _supportBodyFirstPerson = _supportBody!.GetFloat(AuthoredSupportHandPatch.FirstPerson);
            // VisualPass reads this through Player.GetCurveValue (_animators[0]).
            // BodyAnimatorCommon can instead resolve Spirit's animator. Keep its raw
            // value for diagnostics, never as the authority for the native IK gate.
            // The postfix ignores FirstPerson, so this read cannot recurse into our owner.
            _supportFirstPerson = localPlayer.GetCurveValue(AuthoredSupportHandPatch.FirstPerson);
            float seconds = _animator!.GetCurrentAnimatorStateInfo(1).normalizedTime * _customStmClip!.length;
            _supportApplied = AuthoredSupportHand.Apply(native, _supportFirstPerson, seconds, true);
            return _supportApplied;
        }

        private void ClearSupportHand()
        {
            AuthoredSupportHandPatch.Release(FilterSupportHand); _supportLease = false; _supportBody = null;
        }

        private string SupportHandReport => "supportHand owned=" + _supportLease + " native=" + _supportNative +
            " firstPerson=" + _supportFirstPerson + " applied=" + _supportApplied +
            " rawBodyFirstPerson=" + _supportBodyFirstPerson + " firstPersonSource=Player.GetCurveValue fault=" + AuthoredSupportHandPatch.Fault;
    }
}
