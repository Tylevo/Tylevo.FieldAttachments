using System;
using System.IO;
using Tylevo.FieldAttachments.Core;
using UnityEngine;

namespace Tylevo.FieldAttachments.Runtime
{
    public sealed partial class InspectionPose
    {
        public bool CustomStmAnimationEnabled { get; set; }
        private readonly CustomStmClip _customStmAsset = new CustomStmClip();
        private readonly CustomStmReturn _customStmReturn = new CustomStmReturn();
        private AnimationClip? _customStmClip;
        private bool _customStm, _customStmAttempted, _customStmAwaiting;
        private int _customStmAppliedFrame;
        private string _customStmStatus = "not requested";

        private void PrepareCustomStm(Animator animator, string template, bool learn)
        {
            _customStm = CustomStmInspection.Requested(CustomStmAnimationEnabled, template, learn);
            _customStmAttempted = _customStmAwaiting = false; _customStmReturn.Cancel();
            _customStmStatus = "not requested";
            _inspectionEventsStatus = "not read";
            if (!_customStm) return;
            if (!CustomStmInspection.ControllerMatches(template,animator.runtimeAnimatorController.name))
                throw new InvalidOperationException("custom STM motion requires this recipient's audited native controller");
            string missing = CustomStmInspection.MissingRigPath(template, path => animator.transform.Find(path) != null);
            if (missing.Length != 0) throw new InvalidOperationException("custom STM rig path missing: " + missing);
            _customStmClip = _customStmAsset.Get(Path.GetDirectoryName(_path)!, template);
            if (_customStmClip == null) throw new InvalidOperationException(_customStmAsset.Status);
            _customStmStatus = "prepared recipient=" + template + " controller=" + animator.runtimeAnimatorController.name + " clip=" + _customStmClip.name + "; waiting for owned native start";
            Event("CUSTOM_STM " + _customStmStatus);
        }
        private bool CustomInspectionStateMatches() => _animator != null &&
            CustomStmInspection.StateMatches(_template,_animator.runtimeAnimatorController.name,_animator.GetCurrentAnimatorStateInfo(1).fullPathHash);
        private bool CustomStmClipMatches() => _animator != null && _customStmClip != null && _motion.IsPlaying(_animator) &&
            CustomStmInspection.ValidClip(_template, _customStmClip.name, _customStmClip.length, _customStmClip.legacy, _customStmClip.humanMotion, _customStmClip.events.Length);
        private bool TickCustomStm()
        {
            if (!_customStm) return true;
            if (AuthoredSupportHandPatch.Fault.Length != 0)
            { CustomStmFault("support hand callback: " + AuthoredSupportHandPatch.Fault); return false; }
            if (_state.Phase != PosePhase.Playing) return true;
            bool stable = CustomInspectionStateMatches() && !_animator!.IsInTransition(1);
            if (_customStmAwaiting)
            {
                if (Time.frameCount <= _customStmAppliedFrame) return false;
                if (!stable || !CustomStmClipMatches() || !OwnedOperation() || !StartEventFired())
                { CustomStmFault("custom clip evaluation not confirmed"); return false; }
                _customStmAwaiting = false;
                _customStmStatus = "custom entrance confirmed; hold at 0.22"; Event("CUSTOM_STM " + _customStmStatus);
            }
            if (_customStmAttempted) return true;
            if (!stable || !OwnedOperation() || !StartEventFired()) return false;
            _customStmAttempted = true;
            var playing = _animator!.GetCurrentAnimatorClipInfo(1);
            string nativeClip = MdrInspectionMotion.NativeClipName(playing);
            int maximumEvents = CustomStmInspection.EventLimit(_template,nativeClip);
            string denial = !CustomStmInspection.EarlyEnough(_animator.GetCurrentAnimatorStateInfo(1).normalizedTime) ? "early clip window missed; no seek" :
                ReadInspectionEvents(out var events, false, maximumEvents) ? CustomStmInspection.EventDenial(_template, nativeClip, events) : "native inspection events unreadable: " + _inspectionEventsStatus;
            if (denial.Length != 0)
            { _customStmStatus = "rejected clip=" + nativeClip + ": " + denial; Event("CUSTOM_STM " + _customStmStatus); Release(denial); return false; }
            Event("CUSTOM_STM native events verified clip=" + nativeClip + " " + _inspectionEventsStatus);
            bool applied = _motion.TryApply(_animator, _template, _read.Get(_read.Get(_hands, "FirearmsAnimator"), "Animator")!, _customStmClip!);
            _controller = _animator.runtimeAnimatorController;
            _motionOutcome = _customStmStatus = _motion.Status; Event("CUSTOM_STM " + _customStmStatus);
            if (!applied) { Release("custom STM clip rejected or uncertain"); return false; }
            _customStmAwaiting = true; _customStmAppliedFrame = Time.frameCount;
            _targetMarker = new PoseMarker { WeaponTemplate = _template, Controller = _controllerKey, Layer = 1, State = _animator.GetCurrentAnimatorStateInfo(1).fullPathHash, Time = CustomStmInspection.Hold };
            _timingSource = "custom STM authored hold";
            return false;
        }
        private void BeginCustomStmReturn(bool normalClose)
        {
            if (!_customStm || !normalClose) { _customStmReturn.Cancel(); return; }
            bool early = _state.Phase == PosePhase.Playing;
            if ((early || CustomStmClipMatches()) && SameLiveAnimator() && OwnedOperation() && NeutralHands() &&
                _customStmReturn.Begin(Time.unscaledTime, early))
            { _customStmStatus = early ? "early close; waiting for native start before idle blend" : "playing authored exit"; Event("CUSTOM_STM " + _customStmStatus); }
        }
        private bool WaitCustomStmReturn(bool mayCancel)
        {
            if (!_customStmReturn.Pending) return false;
            bool safe = mayCancel && CustomStmAnimationEnabled && SameLiveAnimator() && NeutralHands();
            bool ready = _animator != null && StartEventFired() &&
                CustomInspectionStateMatches() && !_animator!.IsInTransition(1);
            var result = _customStmReturn.Observe(Time.unscaledTime, _animator == null ? float.NaN : _animator.GetCurrentAnimatorStateInfo(1).normalizedTime,
                safe, OwnedOperation(), CustomStmClipMatches(), ready);
            if (result == CustomStmReturnResult.Waiting) return true;
            if (result == CustomStmReturnResult.Fault) { CustomStmFault("authored exit observation uncertain"); return true; }
            _customStmStatus = result == CustomStmReturnResult.BlendToIdle ? "authored exit complete; verifying native idle" : "exit yielded to native interruption";
            Event("CUSTOM_STM " + _customStmStatus);
            if (result == CustomStmReturnResult.BlendToIdle) TryShortReturn(true);
            return false;
        }
        private void CustomStmFault(string reason)
        {
            _customStmAwaiting = false; _customStmReturn.Cancel(); _customStmStatus = "fault: " + reason;
            Event("CUSTOM_STM " + _customStmStatus); _motion.LatchFault(reason); Release(reason); RestoreMotion();
        }
        private string CustomStmReport => "customStm=" + _customStm + " pending=" + _customStmAwaiting + " exit=" + _customStmReturn.Pending +
            " status=" + _customStmStatus + " asset=" + _customStmAsset.Status + " events=" + _inspectionEventsStatus;
    }
}
